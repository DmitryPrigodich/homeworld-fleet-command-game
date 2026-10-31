using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using FleetCommand.Core;

internal sealed record Scenario(string Name, string Map, uint Seed, int MaxCommands = 400, bool CloseContact = false, bool UseSpecialOrders = false);
internal static class ScenarioRunner
{
    public static void Run(string root)
    {
        var reports = new List<object>();
        string output = Path.Combine(root, "tmp", "test-results"); Directory.CreateDirectory(output);
        foreach (string file in Directory.GetFiles(Path.Combine(root, "tests", "scenarios"), "*.json").Order())
        {
            var scenario = JsonData.Read<Scenario>(file);
            var content = ContentCatalog.Load([Path.Combine(root, "content", "base")]); BasicCardBattle.Register(content);
            var engine = new BattleEngine(content);
            var original = engine.Create(scenario.Map, scenario.Seed);
            if (scenario.CloseContact) original = original with { Units = [original.Units.First(u => u.Side == 0) with { Q = 1, R = 1 }, original.Units.First(u => u.Side == 1) with { Q = 2, R = 1 }] };
            var live = original; var restored = engine.Load(engine.Save(original));
            var trace = new List<object>();
            void Require(bool condition, string message) { if (!condition) throw new Exception(scenario.Name + ": " + message); }
            for (int step = 0; step < scenario.MaxCommands && !live.Finished; step++)
            {
                string before = engine.Save(live);
                int side = live.Cards is { Phase: "initiative" } cards ? cards.Ready0 ? 1 : 0 : live.ActiveSide;
                var command = Plan(engine, live, side, scenario.UseSpecialOrders);
                var a = engine.Apply(live, side, command); var b = engine.Apply(restored, side, command);
                trace.Add(new { step, side, command, a.Accepted, a.Message, a.State.Round, a.State.Sequence,
                    hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(engine.Save(a.State)))) });
                File.WriteAllText(Path.Combine(output, Path.GetFileNameWithoutExtension(file) + ".trace.json"), JsonSerializer.Serialize(trace, JsonData.Options));
                Require(a.Accepted && b.Accepted, "scenario command rejected: " + a.Message);
                Require(before == engine.Save(live), "snapshot mutated");
                Require(engine.Save(a.State) == engine.Save(b.State), "save/resume replay diverged");
                live = a.State; restored = engine.Load(engine.Save(b.State));
                if (live.Cards != null)
                {
                    foreach (int viewer in new[] { 0, 1 })
                    {
                        var view = engine.Load(engine.Save(BasicCardRules.View(live, viewer)));
                        Require(view.Cards!.Deck.Length == 0 && (viewer == 0 ? view.Cards.Hand1 : view.Cards.Hand0).Length == 0 && view.RandomState == 1, "private cards or RNG leaked");
                        Require(!engine.Apply(view, viewer, command).Accepted, "client view executed as authority");
                    }
                }
            }
            Require(live.Finished, "battle did not finish within command budget");
            if (scenario.CloseContact) Require(live.Winner != null, "close-contact battle must produce a winner");
            var rejected = engine.Apply(live, live.ActiveSide, new("pass"));
            Require(!rejected.Accepted && engine.Save(rejected.State) == engine.Save(live), "finished battle accepted a command");
            reports.Add(new { scenario.Name, scenario.Seed, commands = trace.Count, live.Winner, live.Round, trace });
            Console.WriteLine($"SCENARIO PASS {scenario.Name}: {trace.Count} commands, round {live.Round}, winner {live.Winner?.ToString() ?? "draw"}");
        }
        // Negative and hidden-commit scenarios, using the same authoritative command interface.
        var c = ContentCatalog.Load([Path.Combine(root, "content", "base")]); BasicCardBattle.Register(c); var e = new BattleEngine(c);
        var s = e.Create(BasicCardBattle.MapId, 17); string saved = e.Save(s);
        if (e.Apply(s, 0, new("move", s.Units[0].Id)).Accepted || e.Save(s) != saved) throw new Exception("pre-initiative action accepted");
        if (e.Apply(s, 0, new("initiative", Card: s.Cards!.Hand1[0])).Accepted) throw new Exception("opponent card accepted as bid");
        var committed = e.Apply(s, 0, new("initiative", Card: s.Cards.Hand0[0])).State;
        var publicView = BasicCardRules.View(committed, 1);
        if (publicView.Cards!.Bid0 != "" || publicView.Cards.Hand0.Length != 0 || publicView.Cards.Deck.Length != 0) throw new Exception("pending bid leaked");
        if (e.Apply(committed, 0, new("initiative")).Accepted) throw new Exception("duplicate commitment accepted");
        Console.WriteLine("SCENARIO PASS rejected orders, private bids, duplicate commitment");
        SpecialOrderScenarios.Run(root);
        File.WriteAllText(Path.Combine(output, "scenarios.json"), JsonSerializer.Serialize(reports, JsonData.Options));
    }
    private static BattleCommand Plan(BattleEngine engine, BattleState state, int side, bool specials)
    {
        if (state.Cards?.Phase == "initiative") return new("initiative");
        if (state.Cards?.Pending is { } pending)
        {
            string actor = SpecialOrders.Actor(pending); var options = engine.GetActions(state, side, actor);
            if (options.AttackTargets.Length > 0) return new("attack", actor, options.AttackTargets[0]);
            if (options.MoveCells.Length > 0) { var h = options.MoveCells[0]; return new("move", actor, Q: h.Q, R: h.R); }
            return new("skip");
        }
        string card = (side == 0 ? state.Cards?.Hand0 : state.Cards?.Hand1)?.FirstOrDefault() ?? "";
        if (specials && SpecialOrders.Supported(card))
        {
            var ready = state.Units.Where(u => u.Side == side && !u.Activated).OrderBy(u => u.Id).ToArray();
            if (ready.Length > 0 && (!SpecialOrders.Formation(card) || ready.Length > 1))
                return new("special", ready[0].Id, Card: card, OtherUnit: ready.Length > 1 ? ready[1].Id : "");
        }
        var enemies = state.Units.Where(u => u.Side != side).ToArray();
        foreach (var unit in state.Units.Where(u => u.Side == side).OrderBy(u => u.Id))
        {
            var options = engine.GetActions(state, side, unit.Id);
            if (options.AttackTargets.Length > 0) return new("attack", unit.Id, options.AttackTargets[0], Card: card);
        }
        foreach (var unit in state.Units.Where(u => u.Side == side).OrderBy(u => u.Id))
        {
            var moves = engine.GetActions(state, side, unit.Id).MoveCells;
            if (moves.Length == 0) continue;
            var goal = moves.OrderBy(h => enemies.Min(t => h.Distance(new(t.Q, t.R)))).ThenBy(h => h.Q).ThenBy(h => h.R).First();
            return new("move", unit.Id, Q: goal.Q, R: goal.R, Card: card);
        }
        return new("pass");
    }
}
