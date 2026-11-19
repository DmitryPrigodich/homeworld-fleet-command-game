using System.Text.Json;
using FleetCommand.Core;

internal static class LaunchOrderScenarios
{
    private sealed class Session
    {
        public ContentCatalog C; public BattleEngine E; public BattleState S;
        private readonly string _path; private readonly List<object> _trace = [];
        public Session(string root, string name)
        {
            C = ContentCatalog.Load([Path.Combine(root, "content", "base")]); BasicCardBattle.Register(C); C.Validate(); E = new(C);
            S = E.Create(BasicCardBattle.LaunchOrdersMapId, 3); string[] a = ["core:ready-to-launch#1"], b = ["core:all-ahead-full#1"];
            S = S with { Cards = S.Cards! with { Phase = "orders", Hand0 = a, Hand1 = b, Discard = [], Deck = CommandCards.Instances(CommandCards.Deck(C, S)).Keys.Except(a).Except(b).ToArray() } };
            _path = Path.Combine(root, "tmp", "test-results", name + ".trace.json");
        }
        public void Do(BattleCommand cmd, bool accepted = true, int side = 0)
        {
            string before = E.Save(S); var a = E.Apply(S, side, cmd); var b = E.Apply(E.Load(before), side, cmd);
            _trace.Add(new { side, cmd, a.Accepted, a.Message, a.State }); File.WriteAllText(_path, JsonSerializer.Serialize(_trace, JsonData.Options));
            Check(a.Accepted == accepted, a.Message); Check(E.Save(S) == before && E.Save(a.State) == E.Save(b.State), "Launch order mutated input or replay diverged");
            S = E.Load(E.Save(a.State));
            foreach (int viewer in new[] { 0, 1 }) { var view = E.Load(E.Save(BasicCardRules.View(S, viewer))); Check(!E.Apply(view, viewer, cmd).Accepted && view.RandomState == 1, "Private launch view gained authority"); }
        }
        public void Begin(string id = "s0-1", bool accepted = true) => Do(new("special", id, Card: "core:ready-to-launch#1"), accepted);
        public void Launch(Hex? cell = null, string repair = "") { var h = cell ?? FleetLaunchOrders.Cells(C, S, "s0-1").First(); Do(new("launch", "s0-1", Q: h.Q, R: h.R, Repair: repair)); }
        public void SetParent(Func<BattleUnit, BattleUnit> change) => S = S with { Units = S.Units.Select(u => u.Id == "s0-0" ? change(u) : u).ToList() };
        public void Settle()
        {
            for (int n = 0; n < 40; n++)
                if (S.Combat is { } combat) Do(new(combat.AbilityRerolls > 0 ? "ability-reroll" : "resolve", Dice: []), side: FleetCombat.DecisionSide(combat));
                else if (S.Defense is { } d) Do(new("decline-defense"), side: 1 - d.Side);
                else if (S.CapEntry is { } cap) Do(new("decline-cap"), side: cap.Side);
                else if (S.Cards?.Response is { } pause) Do(new("decline"), side: 1 - pause.Side);
                else if (S.Repairs is { Length: > 0 } repairs) Do(new("repair-stop"), side: repairs[0].Side);
                else return;
            throw new Exception("Launch continuation did not settle");
        }
    }
    public static void Run(string root)
    {
        int count = 0;
        void Case(string name, Action<Session> test) { try { test(new(root, name)); } catch (Exception e) { throw new Exception(name + ": " + e.Message, e); } count++; Console.WriteLine("LAUNCH ORDER PASS " + name); }
        Case("launch-order-offboard-assignment-and-save", s =>
        {
            s.Begin(); Check(s.S.Reserves!.Single(u => u.Id == "s0-1").Activated && !s.S.Units.Single(u => u.Id == "s0-0").Activated, "Launch activated the carrier");
            Check(s.S.Cards!.Pending!.Step == 0 && s.S.Orders0 == 1 && s.S.Cards.Discard.Contains("core:ready-to-launch#1"), "Launch did not spend exactly one card and Play");
            Check(s.E.GetActions(s.S, 0, "s0-1").MoveCells.Length > 0, "Offboard launch has no exit preview"); s.Launch();
            Check(s.S.Cards.Pending!.Step == 1 && s.S.Units.Single(u => u.Id == "s0-0").Launches == 1 && !s.S.Units.Single(u => u.Id == "s0-0").Activated, "Launch continuation or parent state wrong");
            var h = s.E.GetActions(s.S, 0, "s0-1").MoveCells.First(); s.Do(new("move", "s0-1", Q: h.Q, R: h.R)); s.Settle(); s.Do(new("skip"));
            Check(s.S.Cards.Pending == null && s.S.Orders0 == 1, "Extra Play spent on launched squadron");
        });
        Case("launch-order-assignment-rejections", s => { s.Begin("s0-0", false); s.Begin("s0-3", false); s.Begin("s1-6", false); s.Begin("s0-4", false); });
        Case("launch-order-activated-squadron-rejected", s => { s.S = s.S with { Reserves = s.S.Reserves!.Select(u => u.Id == "s0-1" ? u with { Activated = true } : u).ToList() }; s.Begin(accepted: false); });
        Case("launch-order-parent-activation-preserved", s => { s.SetParent(u => u with { Activated = true, Effects = ["brace"] }); s.Begin(); s.Launch(); Check(s.S.Units.Single(u => u.Id == "s0-0") is { Activated: true, Effects: ["brace"] }, "Carrier was reordered"); });
        Case("launch-order-bays-and-tubes-rejected", s => { s.SetParent(u => u with { Effects = ["bays"] }); s.Begin(accepted: false); s.SetParent(u => u with { Effects = [], Launches = 2 }); s.Begin(accepted: false); });
        Case("launch-order-invalid-placement-spends-nothing", s => { s.Begin(); s.Do(new("launch", "s0-2", Q: 0, R: 3), false); s.Do(new("launch", "s0-1", Q: 9, R: 0), false); s.Launch(); s.Do(new("launch", "s0-1", Q: 0, R: 3), false); });
        Case("launch-order-skipped-launch-cancels-followups", s => { s.Begin(); s.Do(new("skip")); Check(s.S.Cards!.Pending == null && s.S.Reserves!.Any(u => u.Id == "s0-1") && s.S.Units.Single(u => u.Id == "s0-0").Launches == 0, "Skipping launch disembarked or granted actions"); });
        Case("launch-order-activation-repair-and-entry-terrain", s =>
        {
            s.S = s.S with { Reserves = s.S.Reserves!.Select(u => u.Id == "s0-1" ? u with { FocusFire = 2 } : u).ToList() };
            s.C.Ships["practice:launch-kushan-wing"] = s.C.Ships["practice:launch-kushan-wing"] with { Success = 2, Armor = 20 };
            s.C.Maps[s.S.Map] = s.C.Maps[s.S.Map] with { Objects = [new("core:asteroids", 0, 3)] };
            s.Begin(); s.Launch(new(0, 3), "ask"); s.Settle(); Check(s.S.Cards!.Pending?.Step == 1 && s.S.Units.Any(u => u.Id == "s0-1"), "Terrain/repair lost the order step");
        });
        Case("launch-order-lightning-then-real-attack", s =>
        {
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s1-9" ? u with { Q = 4, R = 2, Facing = 3 } : u).ToList(), ValorOwner = 0 };
            s.Begin(); s.Launch(new(2, 2)); s.Settle(); s.Do(new("move", "s0-1", Q: 3, R: 2)); s.Settle();
            Check(s.S.Cards!.Pending!.Step == 2 && s.E.GetActions(s.S, 0, "s0-1").AttackTargets.Contains("s1-9"), "Launched squadron lost attack action");
            s.Do(new("attack", "s0-1", "s1-9")); s.Settle(); Check(s.S.Cards.Pending == null, "Attack did not finish launch order");
        });
        Console.WriteLine($"LAUNCH_ORDERS_OK {count} scenarios");
    }
    private static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
}
