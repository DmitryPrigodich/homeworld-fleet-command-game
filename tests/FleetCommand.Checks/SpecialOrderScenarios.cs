using System.Text.Json;
using FleetCommand.Core;

internal static class SpecialOrderScenarios
{
    private sealed class Session
    {
        public ContentCatalog Content;
        public BattleEngine Engine;
        public BattleState State;
        private readonly string _file;
        private readonly List<object> _trace = [];
        public Session(string root, string name)
        {
            Content = ContentCatalog.Load([Path.Combine(root, "content", "base")]); BasicCardBattle.Register(Content);
            Engine = new(Content); State = Engine.Create(BasicCardBattle.MapId, 17);
            var all = State.Cards!.Deck.Concat(State.Cards.Hand0).Concat(State.Cards.Hand1).ToArray();
            string[] own = ["order-01", "order-09"]; var rest = all.Except(own).ToArray();
            State = State with { ActiveSide = 0, Cards = new(rest.Skip(2).ToArray(), own, rest.Take(2).ToArray(), [], Phase: "orders") };
            _file = Path.Combine(root, "tmp", "test-results", name + ".trace.json");
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        }
        public void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        public void Do(BattleCommand command, bool accepted = true, int side = 0)
        {
            string before = Engine.Save(State);
            var result = Engine.Apply(State, side, command);
            var resumed = Engine.Apply(Engine.Load(before), side, command);
            _trace.Add(new { command, side, expected = accepted, actual = result.Accepted, result.Message, result.State.Sequence, pending = result.State.Cards!.Pending });
            File.WriteAllText(_file, JsonSerializer.Serialize(_trace, JsonData.Options));
            Require(result.Accepted == accepted, "Unexpected result: " + result.Message);
            Require(Engine.Save(State) == before, "Prior snapshot mutated");
            Require(Engine.Save(result.State) == Engine.Save(resumed.State), "Mid-order save/resume diverged");
            State = Engine.Load(Engine.Save(result.State));
            foreach (int viewer in new[] { 0, 1 })
            {
                var view = Engine.Load(Engine.Save(BasicCardRules.View(State, viewer)));
                Require(view.Cards!.Deck.Length == 0 && (viewer == 0 ? view.Cards.Hand1 : view.Cards.Hand0).Length == 0 && view.RandomState == 1, "Hidden state leaked during pending order");
                Require(!Engine.Apply(view, viewer, command).Accepted, "Client view acted as authority");
            }
            if (State.Cards!.Response != null) Do(new("decline"), side: InterruptRules.Responder(State));
        }
    }
    public static void Run(string root)
    {
        void Case(string name, Action<Session> run)
        {
            var s = new Session(root, name); run(s);
            Console.WriteLine("SPECIAL SCENARIO PASS " + name);
        }
        Case("special-straight-resume", s =>
        {
            s.Do(new("special", "s0-0", Card: "order-01"));
            s.Require(s.State.Orders0 == 1 && s.State.Units.Single(u => u.Id == "s0-0").Activated, "Order did not reserve exactly one activation");
            s.Do(new("move", "s0-0", Q: 2, R: 2));
            s.Do(new("move", "s0-0", Q: 2, R: 3), false);
            s.Do(new("pass"), false, 1);
            s.Do(new("attack", "s0-0", "s1-3"), false);
            s.Do(new("move", "s0-0", Q: 3, R: 2));
            s.Require(s.State.Cards!.Pending == null && s.State.Orders0 == 1 && s.State.ActiveSide == 1 && s.State.Cards.Discard.Length == 1, "Two actions consumed more than one order/card or failed to end");
        });
        Case("special-skip-and-early-finish", s =>
        {
            s.Do(new("special", "s0-0", Card: "order-01")); s.Do(new("skip"));
            s.Do(new("move", "s0-0", Q: 1, R: 3));
            s.Require(s.State.Cards!.Pending == null, "Skipping first action failed to advance");
        });
        Case("special-finish-remaining", s =>
        {
            s.Do(new("special", "s0-0", Card: "order-09", OtherUnit: "s0-0"), false);
            s.Do(new("special", "s0-0", Card: "order-09", OtherUnit: "s1-3"), false);
            s.Do(new("special", "s0-0", Card: "order-09", OtherUnit: "s0-1"));
            s.Require(s.State.Units.Where(u => u.Id is "s0-0" or "s0-1").All(u => u.Activated), "Both formation units must be assigned before acting");
            s.Do(new("finish-order")); s.Require(s.State.Cards!.Pending == null, "Early completion failed");
        });
        Case("special-straight-blocked-detour", s =>
        {
            var map = s.Content.Maps[BasicCardBattle.MapId];
            s.Content.Maps[map.Id] = map with { Objects = [.. map.Objects, new("core:planet", 2, 2)] };
            s.Require(s.Engine.GetActions(s.State, 0, "s0-0").MoveCells.Contains(new(3, 2)), "Fixture must allow a normal detour");
            s.Do(new("special", "s0-0", Card: "order-01"));
            s.Require(!s.Engine.GetActions(s.State, 0, "s0-0").MoveCells.Contains(new(3, 2)), "Straight order allowed a detour");
            s.Do(new("move", "s0-0", Q: 3, R: 2), false); s.Do(new("finish-order"));
        });
        Case("special-formation-destroyed-target", s =>
        {
            var ship = s.Content.Ships["core:kushan-interceptor"];
            s.Content.Ships[ship.Id] = ship with { DicePerModel = 6, Success = 2 };
            s.State = s.State with { Units = s.State.Units.Select(u => u.Id == "s1-3" ? u with { Q = 4, R = 2, Models = 1 } : u).ToList() };
            s.Do(new("special", "s0-0", Card: "order-09", OtherUnit: "s0-1"));
            s.Do(new("move", "s0-1", Q: 2, R: 2), false);
            s.Do(new("move", "s0-0", Q: 3, R: 2));
            s.Do(new("attack", "s0-0", "s1-3"));
            s.Require(!s.State.Units.Any(u => u.Id == "s1-3") && s.State.Cards!.Pending!.Step == 2, "Early target destruction failed");
            s.Do(new("move", "s0-1", Q: 0, R: 4), false);
            s.Do(new("move", "s0-1", Q: 2, R: 2));
            s.Do(new("attack", "s0-1", "s1-3"), false);
            s.Do(new("skip"));
            s.Require(s.State.Cards!.Pending == null && s.State.Orders0 == 1 && s.State.Cards.Discard.Length == 1, "Formation did not consume exactly one order and one card");
        });
        Case("special-missing-afterburners", s =>
        {
            s.Content.Ships["core:kushan-interceptor"] = s.Content.Ships["core:kushan-interceptor"] with { Afterburners = 0 };
            s.Do(new("special", "s0-0", Card: "order-09", OtherUnit: "s0-1")); s.Do(new("skip")); s.Do(new("skip"));
            s.Require(s.Engine.GetActions(s.State, 0, "s0-1").MoveCells.Length == 0, "Lightning Move available without Afterburners");
            s.Do(new("skip")); s.Do(new("skip"));
        });
        Case("special-invalid-save", s =>
        {
            s.Do(new("special", "s0-0", Card: "order-01"));
            var bad = s.State with { Cards = s.State.Cards! with { Pending = s.State.Cards.Pending! with { Step = 99 } } };
            try { s.Engine.Load(s.Engine.Save(bad)); } catch (InvalidDataException) { return; }
            throw new Exception("Invalid pending order loaded");
        });
    }
}
