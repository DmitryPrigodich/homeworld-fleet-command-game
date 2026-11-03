using FleetCommand.Core;
using System.Text.Json;
internal static class LogisticsScenarios
{
    private const string Parent = "s0-0", Wing = "s0-1", Wing2 = "s0-2", Reserve = "s0-3", Actor = "s0-4";
    private sealed class Session
    {
        public ContentCatalog C; public BattleEngine E; public BattleState S;
        private readonly List<object> _trace = []; private readonly string _path;
        public Session(string root, string name)
        {
            C = ContentCatalog.Load([Path.Combine(root, "content", "base")]); BasicCardBattle.Register(C); E = new(C); S = E.Create(BasicCardBattle.LogisticsMapId, 1);
            string[] a = ["order-01", "order-09"], b = ["order-21", "order-22"];
            S = S with { RandomState = 3, Cards = new(Enumerable.Range(1, 24).Select(i => $"order-{i:00}").Except(a).Except(b).ToArray(), a, b, [], Phase: "orders") };
            _path = Path.Combine(root, "tmp", "test-results", name + ".trace.json"); Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        }
        public void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
        public void Do(int side, BattleCommand cmd, bool accepted = true)
        {
            string original = E.Save(S); var a = E.Apply(S, side, cmd); var b = E.Apply(E.Load(original), side, cmd);
            _trace.Add(new { side, cmd, a.Accepted, a.Message, a.State }); File.WriteAllText(_path, JsonSerializer.Serialize(_trace, JsonData.Options));
            Check(a.Accepted == accepted, "Logistics command: " + a.Message); Check(E.Save(S) == original && E.Save(a.State) == E.Save(b.State), "Logistics mutated input or replay diverged");
            S = E.Load(E.Save(a.State));
            foreach (int viewer in new[] { 0, 1 }) { var view = E.Load(E.Save(BasicCardRules.View(S, viewer))); Check(view.RandomState == 1 && !E.Apply(view, viewer, cmd).Accepted, "Reinforcements leaked authority"); }
        }
        public void Begin(string mode, string source = Parent) => Do(0, new("begin-reinforce", source, mode, Card: "order-01"));
        public void Reset() => S = S with { ActiveSide = 0, Orders0 = 0 };
        public void SetCarrier(Func<ShipDefinition, ShipDefinition> edit) => C.Ships["practice:kushan-carrier"] = edit(C.Ships["practice:kushan-carrier"]);
        public void FillAdjacent()
        {
            var parent = S.Units.Single(u => u.Id == Parent);
            foreach (var h in FleetLogistics.Adjacent(C, S, parent)) S = S with { Units = S.Units.Append(new BattleUnit("block-" + h.Q + "-" + h.R, "practice:kushan-wing", 0, h.Q, h.R, 0, 1)).ToList() };
        }
    }
    public static void Run(string root)
    {
        void Case(string name, Action<Session> run) { run(new(root, name)); Console.WriteLine("LOGISTICS SCENARIO PASS " + name); }
        Case("logistics-setup-separates-reserves-and-embarked-units", s =>
        {
            s.Check(s.S.Units.Count == 4 && s.S.Reserves!.Count == 6 && FleetLogistics.Children(s.S, Parent).Length == 2 && s.S.Funds0 == 3, "Initial off-board placement leaked onto the map");
            s.C.Validate(); s.Check(s.E.Save(s.S) == s.E.Save(s.E.Load(s.E.Save(s.S))), "Initial off-board save did not round-trip");
        });
        Case("logistics-launch-does-not-activate-carrier-and-shares-round-limit", s =>
        {
            s.Begin("launch"); s.Do(0, new("launch", Wing, Q: 2, R: 2)); s.Do(0, new("launch", Wing2, Q: 1, R: 2));
            s.Check(s.S.Units.Single(u => u.Id == Parent) is { Activated: false, Launches: 2 } && s.S.Units.Where(u => u.Id is Wing or Wing2).All(u => u.Activated) && s.S.Orders0 == 1, "Launch activated carrier or double-spent Play");
            s.Do(0, new("finish-reinforce")); s.Reset(); s.Do(0, new("begin-reinforce", Parent, "launch", Card: "order-09"), false);
            s.Check(s.S.Units.Single(u => u.Id == Parent).Launches == 2, "New order reset Launch Tubes");
        });
        Case("logistics-launch-from-already-activated-carrier-and-refresh", s =>
        {
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == Parent ? u with { Activated = true } : u).ToList() };
            s.Begin("launch"); s.Do(0, new("launch", Wing, Q: 2, R: 2)); s.Do(0, new("finish-reinforce")); s.Do(1, new("pass"));
            s.Check(s.S.Round == 2 && s.S.Units.Single(u => u.Id == Parent).Launches == 0 && s.S.Reserves!.All(u => !u.Activated), "Round cleanup did not reset launches and off-board activations");
        });
        Case("logistics-deploy-reserve-without-paying-ship-again", s =>
        {
            s.Begin("deploy", Reserve); s.Do(0, new("deploy", Q: 5, R: 1), false); s.Do(1, new("deploy", Q: 0, R: 1), false); s.Do(0, new("deploy", Q: 0, R: 1, Facing: 2));
            s.Check(s.S.Units.Single(u => u.Id == Reserve) is { Activated: true, Facing: 2 } && s.S.Funds0 == 3 && s.S.Reinforce == null, "Reserve deploy charged stars or did not complete the order");
        });
        Case("logistics-deployed-carrier-keeps-embarked-children", s =>
        {
            var carrier = s.S.Units.Single(u => u.Id == Parent);
            s.S = s.S with { Units = s.S.Units.Where(u => u.Id != Parent).ToList(), Reserves = s.S.Reserves!.Append(carrier with { Q = -1, R = -1 }).ToList() };
            s.Begin("deploy", Parent); s.Do(0, new("deploy", Q: 0, R: 2));
            s.Check(FleetLogistics.Children(s.S, Parent).Length == 2 && s.S.Units.Single(u => u.Id == Parent).Activated, "Carrier deployment prematurely launched strike craft");
        });
        Case("logistics-scenario-round-and-custom-zone-are-enforced", s =>
        {
            s.C.Maps[s.S.Map] = s.C.Maps[s.S.Map] with { ReserveFromRound = 4, SetupZones = [[new(3, 0)], [new(7, 6)]] };
            s.Do(0, new("begin-reinforce", Reserve, "deploy", Card: "order-01"), false); s.S = s.S with { Round = 4 };
            s.Begin("deploy", Reserve); s.Do(0, new("deploy", Q: 0, R: 0), false); s.Do(0, new("deploy", Q: 3, R: 0));
        });
        Case("logistics-build-pays-cargo-and-reserve-and-creates-stable-id", s =>
        {
            s.Begin("build"); s.Do(0, new("build", Definition: "practice:taiidan-wing", Q: 2, R: 2), false);
            s.Do(0, new("build", Definition: "practice:kushan-reserve-frigate", Q: 2, R: 2), false);
            s.Do(0, new("build", Definition: "practice:kushan-wing", Q: 2, R: 2, ReserveSpend: 3), false);
            s.Do(0, new("build", Definition: "practice:kushan-wing", Q: 2, R: 2, ReserveSpend: 1));
            var child = s.S.Units.Single(u => u.Id == "s0-10");
            s.Check(child.Activated && child.Models == 3 && s.S.NextUnit == 11 && s.S.Units.Single(u => u.Id == Parent) is { Cargo: 5, Activated: false, Launches: 1 } && s.S.Funds0 == 2 && s.S.Reinforce == null, "Build cost, identity or carrier activation is incorrect");
        });
        Case("logistics-build-into-bay-without-an-exit-hex", s =>
        {
            s.FillAdjacent(); s.Begin("build"); s.Do(0, new("build", Definition: "practice:kushan-wing", Q: 2, R: 2), false);
            s.Do(0, new("build", Definition: "practice:kushan-wing", ToBay: true));
            s.Check(s.S.Reserves!.Single(u => u.Id == "s0-10") is { Activated: true, Embarked: Parent } && s.S.Units.Single(u => u.Id == Parent).Launches == 0 && s.S.Units.Single(u => u.Id == Parent).Cargo == 4, "Docked production required an exit or spent Launch Tubes");
        });
        Case("logistics-full-bays-and-blocked-board-do-not-consume-build-funds", s =>
        {
            s.SetCarrier(ship => ship with { Bays = 2 }); s.FillAdjacent(); s.Begin("build");
            s.Do(0, new("build", Definition: "practice:kushan-wing", ToBay: true), false); s.Do(0, new("build", Definition: "practice:kushan-wing", Q: 2, R: 2), false);
            s.Check(s.S.Units.Single(u => u.Id == Parent).Cargo == 6 && s.S.Funds0 == 3 && s.S.NextUnit == 10, "Rejected build spent resources or identity");
        });
        Case("logistics-crippled-bays-block-launch-but-not-docked-production", s =>
        {
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == Parent ? u with { Effects = ["bays"] } : u).ToList() };
            s.Do(0, new("begin-reinforce", Parent, "launch", Card: "order-01"), false);
            s.Begin("build"); s.Do(0, new("build", Definition: "practice:kushan-wing", Q: 2, R: 2), false); s.Do(0, new("build", Definition: "practice:kushan-wing", ToBay: true));
        });
        Case("logistics-embark-spends-move-preserves-cargo-and-skips-later-actions", s =>
        {
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == Actor ? u with { Cargo = 2, FocusFire = 1 } : u).ToList() };
            s.Do(0, new("special", Actor, Card: "order-01"));
            string before = s.E.Save(s.S); s.Check(FleetLogistics.EmbarkHosts(s.C, s.S, 0, Actor).Any(u => u.Id == Parent) && before == s.E.Save(s.S), "Pending movement omitted its bay preview or mutated state");
            s.Do(0, new("embark", Actor, Parent));
            s.Check(!s.S.Units.Any(u => u.Id == Actor) && s.S.Reserves!.Single(u => u.Id == Actor) is { Embarked: Parent, Cargo: 2, Activated: true } && s.S.Cards!.Pending == null && s.S.Units.Single(u => u.Id == Parent).Launches == 0, "Embark lost unit state or left its remaining actions active");
        });
        Case("logistics-embark-route-pauses-and-restores-before-entering-bay", s =>
        {
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == Actor ? u with { Q = 4, R = 3 } : u.Id == "s1-9" ? u with { Q = 3, R = 2 } : u).ToList() };
            s.Do(0, new("embark", Actor, Parent, Card: "order-01"));
            s.Check(s.S.Cards!.Response?.Command.Embark == Parent && s.S.Units.Any(u => u.Id == Actor), "Embark route skipped the movement reaction");
            for (int i = 0; s.S.Cards!.Response != null && i < 10; i++) s.Do(1, new("decline"));
            s.Check(s.S.Reserves!.Any(u => u.Id == Actor && u.Embarked == Parent) && s.S.Orders0 == 1, "Saved embark route failed to enter its bay once");
        });
        Case("logistics-embark-rejects-full-bay-enemy-and-insufficient-movement", s =>
        {
            s.SetCarrier(ship => ship with { Bays = 2 }); s.Do(0, new("embark", Actor, Parent, Card: "order-01"), false);
            s.SetCarrier(ship => ship with { Bays = 3 }); s.Do(0, new("embark", Actor, "s1-5", Card: "order-01"), false);
            s.C.Ships["practice:kushan-wing"] = s.C.Ships["practice:kushan-wing"] with { Move = 0 }; s.Do(0, new("embark", Actor, Parent, Card: "order-01"), false);
        });
        Case("logistics-parent-loss-awards-embarked-ships-and-leaders-not-cargo", s =>
        {
            s.C.Leaders["test:pilot"] = new("test:pilot", "Pilot", "kushan", ["fighter"], Stars: 1, Hand: 2, Play: 2);
            s.S = s.S with { Reserves = s.S.Reserves!.Select(u => u.Id == Wing ? u with { Leader = "test:pilot" } : u).ToList(), ActiveSide = 1, FactionUsed1 = true,
                Units = s.S.Units.Select(u => u.Id == Parent ? u with { Q = 1, R = 3 } : u.Id == "s1-9" ? u with { Q = 2, R = 2 } : u).ToList() };
            s.SetCarrier(ship => ship with { Armor = 0, Evasive = 1, Weapons = [] });
            s.C.Ships["practice:taiidan-wing"] = s.C.Ships["practice:taiidan-wing"] with { Success = 2, Weapons = [new("gun", "Gun", 8, 2)] };
            s.Do(1, new("attack", "s1-9", Parent, Card: "order-21")); if (s.S.Cards!.Response != null) s.Do(0, new("decline"));
            s.Do(0, new("ability-reroll", Dice: []));
            s.Check(!s.S.Units.Any(u => u.Id == Parent) && !s.S.Reserves!.Any(u => u.Embarked == Parent) && s.S.Stars1 == 10 && s.S.Cards!.HandLimit0 == 2, "Carrier loss failed to destroy/score embarked ships or off-board leaders affected budgets");
        });
        Case("logistics-reserves-prevent-premature-elimination", s =>
        {
            s.S = s.S with { Units = s.S.Units.Where(u => u.Side == 1).ToList(), Reserves = s.S.Reserves!.Where(u => u.Embarked == "").ToList() };
            s.Begin("deploy", Reserve); s.Do(0, new("deploy", Q: 0, R: 1)); s.Check(!s.S.Finished && s.S.Units.Any(u => u.Side == 0), "A side with deployable reserves was eliminated");
        });
        Case("logistics-arrival-terrain-saves-without-a-movement-response", s =>
        {
            s.C.Maps[s.S.Map] = s.C.Maps[s.S.Map] with { Objects = [new("core:asteroids", 0, 1)] };
            s.C.Ships["practice:kushan-reserve-frigate"] = s.C.Ships["practice:kushan-reserve-frigate"] with { Armor = 20, Evasive = 1 };
            s.Begin("deploy", Reserve); s.Do(0, new("deploy", Q: 0, R: 1));
            s.Check(s.S.Hazard?.Job.Resume == "reinforce" && s.S.Cards!.Response == null && s.S.Reinforce?.Mode == "deploy", "Arrival terrain lost the paid reinforcement continuation");
            s.Do(0, new("ability-reroll", Dice: [])); s.Check(s.S.Reinforce == null && s.S.Units.Single(u => u.Id == Reserve).Activated && s.S.Orders0 == 1, "Terrain failed to finish the arriving unit's original order");
        });
        Case("logistics-neutral-terrain-awards-enemy-parent-and-bay-stars", s =>
        {
            s.C.Maps[s.S.Map] = s.C.Maps[s.S.Map] with { Objects = [new("core:asteroids", 1, 3)] };
            s.SetCarrier(ship => ship with { Armor = 0, Evasive = 1, Weapons = [] });
            s.Do(0, new("begin-order", Parent, "move", Card: "order-01")); s.Do(0, new("ability-reroll", Dice: []));
            s.Check(!s.S.Units.Any(u => u.Id == Parent) && FleetLogistics.Children(s.S, Parent).Length == 0 && s.S.Stars1 == 9, "Neutral terrain left orphaned bays or failed to award parent/embarked stars to the enemy");
        });
        Case("logistics-arriving-leader-changes-budgets-next-round", s =>
        {
            s.C.Leaders["test:leader"] = new("test:leader", "Leader", "kushan", ["fighter"], Hand: 2, Play: 1);
            s.S = s.S with { Reserves = s.S.Reserves!.Select(u => u.Id == Wing ? u with { Leader = "test:leader" } : u).ToList() };
            s.Begin("launch"); s.Do(0, new("launch", Wing, Q: 2, R: 2)); s.Check(s.S.Cards!.HandLimit0 == 2 && s.S.Cards.PlayLimit0 == 1, "Arriving leader changed frozen budgets mid-round");
            s.Do(0, new("finish-reinforce")); s.Do(1, new("pass")); s.Check(s.S.Cards!.HandLimit0 == 4 && s.S.Cards.PlayLimit0 == 2, "Arriving leader did not contribute at the next round");
        });
        Case("logistics-lost-production-identities-are-not-reused", s =>
        {
            s.Begin("build"); s.Do(0, new("build", Definition: "practice:kushan-wing", ToBay: true));
            s.S = s.S with { Reserves = s.S.Reserves!.Where(u => u.Id != "s0-10").ToList() }; s.Reset();
            s.Do(0, new("begin-reinforce", Parent, "build", Card: "order-09")); s.Do(0, new("build", Definition: "practice:kushan-wing", ToBay: true));
            s.Check(s.S.Reserves!.Any(u => u.Id == "s0-11") && s.S.NextUnit == 12, "A lost produced unit's identity was reused");
        });
        Case("logistics-arrival-repair-finishes-before-terrain-or-order", s =>
        {
            s.C.Ships["practice:kushan-wing"] = s.C.Ships["practice:kushan-wing"] with { Success = 2 };
            s.S = s.S with { Reserves = s.S.Reserves!.Select(u => u.Id == Wing ? u with { FocusFire = 1, Effects = ["brace"] } : u).ToList() };
            s.Begin("launch"); s.Do(0, new("launch", Wing, Q: 2, R: 2, Repair: "ask"));
            s.Check(s.S.Repairs is { Length: 1 } && s.S.Reinforce != null && s.S.Units.Single(u => u.Id == Wing).Effects!.Length == 0, "Arriving unit skipped activation repair or retained an old buff");
            s.Do(0, new("repair-choice", Repair: "focus")); s.Check(s.S.Units.Single(u => u.Id == Wing).FocusFire == 0 && s.S.Reinforce?.Mode == "launch", "Arrival repair lost the launch continuation");
        });
        Case("logistics-malformed-offboard-and-frame-are-rejected", s =>
        {
            foreach (var bad in new[] { s.S with { Reserves = s.S.Reserves!.Select(u => u.Id == Wing ? u with { Embarked = "missing" } : u).ToList() }, s.S with { Funds0 = -1 }, s.S with { Reserves = s.S.Reserves!.Select(u => u.Id == Wing ? u with { Q = 0 } : u).ToList() }, s.S with { Reinforce = new(0, "order-01", Parent, "launch") } })
            { bool rejected = false; try { s.E.Load(s.E.Save(bad)); } catch (InvalidDataException) { rejected = true; } s.Check(rejected, "Corrupt reinforcement save was accepted"); }
        });
    }
}
