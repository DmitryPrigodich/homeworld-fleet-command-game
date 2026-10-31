using System.Text.Json;
using FleetCommand.Core;

internal static class InterruptScenarios
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
            Content.Maps[BasicCardBattle.MapId] = Content.Maps[BasicCardBattle.MapId] with { Objects = [] };
            foreach (string id in new[] { "core:kushan-interceptor", "core:taiidan-interceptor" }) Content.Ships[id] = Content.Ships[id] with { Armor = 50 };
            Engine = new(Content); State = Engine.Create(BasicCardBattle.MapId, 17);
            Hex[] positions = [new(1, 1), new(1, 3), new(0, 0), new(2, 1), new(4, 2), new(5, 4)];
            State = State with { Units = State.Units.Select((u, i) => u with { Q = positions[i].Q, R = positions[i].R }).ToList(), ActiveSide = 0 };
            Hands(["order-01", "order-09"], ["order-17", "order-18"]);
            _file = Path.Combine(root, "tmp", "test-results", name + ".trace.json");
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        }
        public void Hands(string[] a, string[] b)
        {
            var deck = Enumerable.Range(1, 24).Select(i => $"order-{i:00}").Except(a).Except(b).ToArray();
            State = State with { Cards = new(deck, a, b, [], Phase: "orders") };
        }
        public void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        public void Do(int side, BattleCommand command, bool accepted = true)
        {
            string before = Engine.Save(State);
            var result = Engine.Apply(State, side, command); var replay = Engine.Apply(Engine.Load(before), side, command);
            _trace.Add(new { side, command, expected = accepted, result.Accepted, result.Message, result.State.Sequence, response = result.State.Cards!.Response });
            File.WriteAllText(_file, JsonSerializer.Serialize(_trace, JsonData.Options));
            Require(result.Accepted == accepted, "Unexpected interrupt result: " + result.Message);
            Require(Engine.Save(State) == before && Engine.Save(result.State) == Engine.Save(replay.State), "Interrupt mutated its input or save/resume diverged");
            State = Engine.Load(Engine.Save(result.State));
            foreach (int viewer in new[] { 0, 1 })
            {
                var view = Engine.Load(Engine.Save(BasicCardRules.View(State, viewer)));
                Require(view.RandomState == 1 && view.Cards!.Deck.Length == 0 && (viewer == 0 ? view.Cards.Hand1 : view.Cards.Hand0).Length == 0, "Interrupt view leaked secrets");
                Require(!Engine.Apply(view, viewer, command).Accepted, "Interrupt view executed as authority");
            }
        }
        public void KillPrimary()
        {
            Content.Ships["core:kushan-interceptor"] = Content.Ships["core:kushan-interceptor"] with { Armor = 0 };
            Content.Ships["core:taiidan-interceptor"] = Content.Ships["core:taiidan-interceptor"] with { DicePerModel = 6, Success = 2 };
            State = State with { Units = State.Units.Select(u => u.Id == "s0-0" ? u with { Models = 1 } : u).ToList() };
        }
    }
    public static void Run(string root)
    {
        void Case(string name, Action<Session> test)
        { var s = new Session(root, name); test(s); Console.WriteLine("INTERRUPT SCENARIO PASS " + name); }
        Case("interrupt-before-attack-resume", s =>
        {
            s.State = s.State with { Units = s.State.Units.Select(u => u.Id == "s1-3" ? u with { Activated = true } : u).ToList() };
            uint random = s.State.RandomState;
            s.Do(0, new("attack", "s0-0", "s1-3", Card: "order-01"));
            s.Require(s.State.Cards!.Response != null && s.State.RandomState == random && s.State.Orders0 == 1, "Attack rolled before response or did not reserve its Play");
            s.Require(s.Engine.GetActions(s.State, 1, "s1-3").AttackTargets.SequenceEqual(new[] { "s0-0" }), "Activated responder has no legal target");
            s.Do(0, new("decline"), false); s.Do(1, new("pass"), false);
            s.Do(1, new("interrupt", "s0-1", "s0-0", Card: "order-17"), false);
            s.Do(1, new("interrupt", "s1-3", "s0-1", Card: "order-17"), false);
            s.Do(1, new("interrupt", "s1-3", "s0-0", Card: "order-09"), false);
            s.Do(1, new("interrupt", "s1-3", "s0-0", Card: "order-17"));
            s.Require(s.State.Cards!.Response == null && s.State.Orders1 == 0 && s.State.Units.Single(u => u.Id == "s1-3").Activated, "Interrupt spent Play or reset activation");
            s.Require(InterruptRules.Played(s.State.Cards, 1).SequenceEqual(new[] { "order-17" }) && !s.State.Cards.Discard.Contains("order-17"), "Interrupt was discarded before round end");
            s.Require(s.State.RandomState != random && s.State.ActiveSide == 1, "Original attack did not resume");
            s.Do(1, new("interrupt", "s1-3", "s0-0", Card: "order-18"), false);
            s.Do(1, new("pass"));
            s.Require(s.State.Round == 2 && InterruptRules.Played(s.State.Cards!, 1).Length == 0 && s.State.Cards!.Discard.Contains("order-17"), "Round did not discard/reset played interrupts");
        });
        Case("interrupt-mid-move-restore", s =>
        {
            s.Do(0, new("move", "s0-0", Q: 2, R: 1, Card: "order-01"), false);
            s.State = s.State with { Units = s.State.Units.Select(u => u.Id == "s1-3" ? u with { Q = 2, R = 2 } : u).ToList() };
            s.Do(0, new("move", "s0-0", Q: 3, R: 1, Card: "order-01"));
            s.Require(s.State.Cards!.Response is { RemainingPath.Length: > 0 } && s.State.Units.Single(u => u.Id == "s0-0").Q == 2, "Move did not pause inside its path");
            s.Do(0, new("move", "s0-1", Q: 1, R: 4, Card: "order-09"), false);
            s.Do(1, new("interrupt", "s1-3", "s0-0", Card: "order-17"));
            s.Require(s.State.Cards!.Response == null && s.State.Units.Single(u => u.Id == "s0-0") is { Q: 3, R: 1 }, "Movement did not finish after response");
        });
        Case("interrupt-allied-pass-through", s =>
        {
            s.State = s.State with { Units = s.State.Units.Select(u => u.Id == "s0-1" ? u with { Q = 2, R = 1 } : u.Id == "s1-3" ? u with { Q = 3, R = 2 } : u).ToList() };
            s.Do(0, new("special", "s0-0", Card: "order-01"));
            s.Do(0, new("move", "s0-0", Q: 3, R: 1));
            s.Require(s.State.Cards!.Response != null && s.State.Units.Single(u => u.Id == "s0-0").Q == 3 && s.State.Units.Select(u => new Hex(u.Q, u.R)).Distinct().Count() == s.State.Units.Count, "Movement paused on an ally");
            s.Do(1, new("decline")); s.Require(s.State.Cards!.Pending is { Step: 1 }, "Move step did not resume");
        });
        Case("interrupt-kills-basic-actor", s =>
        {
            s.KillPrimary(); s.Do(0, new("attack", "s0-0", "s1-3", Card: "order-01"));
            s.Do(1, new("interrupt", "s1-3", "s0-0", Card: "order-17"));
            s.Require(!s.State.Units.Any(u => u.Id == "s0-0") && s.State.Cards!.Response == null && !s.State.Finished && s.State.Orders0 == 1, "Destroyed actor left its action suspended");
            s.Require(s.State.Units.Single(u => u.Id == "s1-3").Models == 6, "Destroyed actor fired back");
        });
        Case("interrupt-kills-formation-actor", s =>
        {
            s.KillPrimary(); s.Do(0, new("special", "s0-0", Card: "order-09", OtherUnit: "s0-1")); s.Do(0, new("skip"));
            s.Do(0, new("attack", "s0-0", "s1-3")); s.Do(1, new("interrupt", "s1-3", "s0-0", Card: "order-17"));
            s.Require(s.State.Cards!.Pending is { Step: 2 } && s.Engine.GetActions(s.State, 0, "s0-1").MoveCells.Length == 0, "Surviving formation actor or adjacency to destroyed primary is wrong");
            s.Do(0, new("skip")); s.Do(0, new("skip")); s.Require(s.State.Cards!.Pending == null, "Formation could not finish after losing its first actor");
        });
        Case("interrupt-immediate-victory", s =>
        {
            s.KillPrimary(); s.State = s.State with { Units = s.State.Units.Where(u => u.Side == 1 || u.Id == "s0-0").ToList() };
            s.Do(0, new("special", "s0-0", Card: "order-01")); s.Do(0, new("move", "s0-0", Q: 1, R: 2));
            s.Do(1, new("interrupt", "s1-3", "s0-0", Card: "order-17"));
            s.Require(s.State.Finished && s.State.Winner == 1 && s.State.Cards!.Pending == null && s.State.Cards.Response == null, "Victory did not clear pending action and order");
            s.Do(0, new("skip"), false);
        });
        Case("interrupt-window-does-not-reveal-hand", s =>
        {
            var before = s.State;
            s.Do(0, new("attack", "s0-0", "s1-3", Card: "order-01")); var withCard = s.State.Cards!.Response;
            s.State = before; s.Hands(["order-01", "order-09"], ["order-21", "order-22"]);
            s.Do(0, new("attack", "s0-0", "s1-3", Card: "order-01"));
            s.Require(JsonSerializer.Serialize(withCard) == JsonSerializer.Serialize(s.State.Cards!.Response), "Public window disclosed card availability");
            s.Require(s.Engine.GetActions(s.State, 1, "s1-3").AttackTargets.Length == 0, "No-card responder received actionable highlights");
            s.Do(1, new("decline"));
        });
        Case("interrupt-once-per-round", s =>
        {
            s.Content.Rules["practice:cards"] = s.Content.Rules["practice:cards"] with { OrdersPerSide = 3, AlternatingOrders = false };
            s.Do(0, new("attack", "s0-0", "s1-3", Card: "order-01")); s.Do(1, new("interrupt", "s1-3", "s0-0", Card: "order-17"));
            s.State = s.State with { Units = s.State.Units.Select(u => u.Id == "s0-1" ? u with { Q = 2, R = 0 } : u).ToList() };
            s.Do(0, new("attack", "s0-1", "s1-3", Card: "order-09"));
            s.Require(s.State.Cards!.Response == null && s.State.Cards.Hand1.Contains("order-18"), "Second same-type interrupt was offered during one round");
        });
        Case("interrupt-invalid-save", s =>
        {
            s.Do(0, new("attack", "s0-0", "s1-3", Card: "order-01"));
            var bad = s.State with { Cards = s.State.Cards! with { Response = s.State.Cards.Response! with { RemainingPath = [new(99, 99)] } } };
            try { s.Engine.Load(s.Engine.Save(bad)); } catch (InvalidDataException) { return; }
            throw new Exception("Corrupt interrupt continuation loaded");
        });
    }
}
