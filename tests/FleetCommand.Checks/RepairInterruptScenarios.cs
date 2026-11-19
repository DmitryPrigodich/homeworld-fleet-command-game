using System.Text.Json;
using FleetCommand.Core;
internal static class RepairInterruptScenarios
{
    private const string RepairCard = "core:interrupt-repairs#1";
    private sealed class Session
    {
        public ContentCatalog C; public BattleEngine E; public BattleState S;
        private readonly string _path; private readonly List<object> _trace = [];
        public Session(string root, string name)
        {
            C = ContentCatalog.Load([Path.Combine(root, "content", "base")]); BasicCardBattle.Register(C); E = new(C);
            S = E.Create(BasicCardBattle.OrdersMapId, 17);
            string[] a = [RepairCard, "core:interrupt-repairs#2", "core:all-ahead-full#1", "core:interrupt-counterintelligence#1"], b = ["core:interrupt-counterintelligence#2", "core:interrupt-attack#1", "core:all-ahead-full#2"];
            S = S with { Cards = S.Cards! with { Phase = "orders", Hand0 = a, Hand1 = b, Discard = [], Deck = CommandCards.Instances(CommandCards.Deck(C, S)).Keys.Except(a).Except(b).ToArray() },
                Units = S.Units.Select(u => u.Id == "s0-0" ? u with { Q = 1, R = 1, FocusFire = 4 } : u.Id == "s0-2" ? u with { Models = 2, FocusFire = 4 } : u.Id == "s1-3" ? u with { Q = 3, R = 2 } : u.Id == "s1-5" ? u with { Models = 2, FocusFire = 4 } : u).ToList() };
            foreach (string id in new[] { "practice:orders-kushan-boarder", "practice:orders-taiidan-boarder" }) C.Ships[id] = C.Ships[id] with { Armor = 50, Move = 3, Weapons = [new("gun", "Repair test gun", 2, 5)] };
            _path = Path.Combine(root, "tmp", "test-results", name + ".trace.json");
        }
        public void Do(int side, BattleCommand cmd, bool accepted = true)
        {
            string before = E.Save(S); var a = E.Apply(S, side, cmd); var b = E.Apply(E.Load(before), side, cmd);
            _trace.Add(new { side, cmd, a.Accepted, a.Message, a.State }); File.WriteAllText(_path, JsonSerializer.Serialize(_trace, JsonData.Options));
            Check(a.Accepted == accepted, a.Message); Check(E.Save(S) == before && E.Save(a.State) == E.Save(b.State), "Repair interrupt mutated input or replay diverged"); S = E.Load(E.Save(a.State));
            foreach (int viewer in new[] { 0, 1 }) { var view = E.Load(E.Save(BasicCardRules.View(S, viewer))); Check(!E.Apply(view, viewer, cmd).Accepted && view.RandomState == 1, "Repair view gained authority"); }
        }
        public BattleUnit Unit(string id) => S.Units.Concat(S.Reserves ?? []).Single(u => u.Id == id);
        public void Repair(string unit = "s0-2", string choice = "fighter") { Do(0, new("interrupt-repairs", unit, Card: RepairCard, Repair: choice)); Do(1, new("decline-counter")); }
        public void Settle()
        {
            for (int i = 0; i < 40; i++)
                if (S.Cards?.Counter is { } counter) Do(1 - counter.Side, new("decline-counter"));
                else if (S.Combat is { } combat) Do(FleetCombat.DecisionSide(combat), new(combat.AbilityRerolls > 0 ? "ability-reroll" : "resolve", Dice: []));
                else if (S.Defense is { } defense) Do(1 - defense.Side, new("decline-defense"));
                else if (S.Cards?.Response is { } pause) Do(1 - pause.Side, new("decline"));
                else return;
            throw new Exception("Repair continuation did not settle");
        }
    }
    public static void Run(string root)
    {
        int count = 0;
        void Case(string name, Action<Session> test) { try { test(new(root, name)); } catch (Exception e) { throw new Exception(name + ": " + e.Message, e); } count++; Console.WriteLine("REPAIR INTERRUPT PASS " + name); }
        Case("repair-interrupt-restores-one-fighter-without-order", s => { uint rng = s.S.RandomState; s.Repair(); Check(s.Unit("s0-2") is { Models: 3, FocusFire: 4, Activated: false } && s.S.RandomState == rng && s.S.Orders0 == 0 && s.S.ActiveSide == 0, "Repair changed activation, other tokens, RNG or Play"); });
        Case("repair-interrupt-removes-whole-focus-token", s => { s.Repair("s0-0", "focus"); Check(s.Unit("s0-0").FocusFire == 2 && s.S.Orders0 == 0, "Repair did not remove whole two-point token"); });
        Case("repair-interrupt-one-point-token", s => { s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s0-0" ? u with { FocusFire = 1, Activated = true } : u).ToList() }; s.Repair("s0-0", "focus"); Check(s.Unit("s0-0") is { FocusFire: 0, Activated: true }, "One-point token or activation handled incorrectly"); });
        Case("repair-interrupt-enemy-turn", s => { s.S = s.S with { ActiveSide = 1 }; s.Repair(); Check(s.S.ActiveSide == 1 && s.S.Orders0 == 0 && s.S.Orders1 == 0, "Repair stole the turn"); });
        Case("repair-interrupt-countered-before-effect", s => { s.Do(0, new("interrupt-repairs", "s0-2", Card: RepairCard, Repair: "fighter")); Check(s.Unit("s0-2").Models == 2, "Repair applied before counter"); s.Do(1, new("counter", Card: "core:interrupt-counterintelligence#2")); Check(s.Unit("s0-2").Models == 2 && s.S.Cards!.Discard.Contains(RepairCard) && !InterruptRules.Available(s.C, s.S, 0, "repairs"), "Counter failed to cancel repair and consume type"); });
        Case("repair-interrupt-once-per-type", s => { s.Repair(); s.Do(0, new("interrupt-repairs", "s0-0", Card: "core:interrupt-repairs#2", Repair: "focus"), false); Check(InterruptRules.Available(s.C, s.S, 0), "Repair consumed Attack interrupt availability"); });
        Case("repair-interrupt-invalid-owner-type-and-choice", s => { s.Do(0, new("interrupt-repairs", "s1-5", Card: RepairCard, Repair: "fighter"), false); s.Do(0, new("interrupt-repairs", "s0-0", Card: RepairCard, Repair: "fighter"), false); s.Do(0, new("interrupt-repairs", "s0-2", Card: RepairCard, Repair: "weapons"), false); s.Do(0, new("interrupt-repairs", "missing", Card: RepairCard, Repair: "fighter"), false); s.Do(0, new("interrupt-repairs", "s0-2", Card: "core:all-ahead-full#1", Repair: "fighter"), false); });
        Case("repair-interrupt-cannot-exceed-profile-models", s => { s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s0-2" ? u with { Models = 3 } : u).ToList() }; s.Do(0, new("interrupt-repairs", "s0-2", Card: RepairCard, Repair: "fighter"), false); });
        Case("repair-interrupt-during-post-roll-choice", s =>
        {
            s.S = s.S with { ValorOwner = 0 }; s.Do(0, new("attack", "s0-0", "s1-3", Card: "core:all-ahead-full#1")); if (s.S.Combat == null) s.Do(1, new("decline"));
            var rolls = s.S.Combat!.Rolls.ToArray(); uint rng = s.S.RandomState; s.Repair("s0-0", "focus");
            Check(s.S.Combat!.Rolls.SequenceEqual(rolls) && s.S.RandomState == rng && s.Unit("s0-0").FocusFire == 2, "Repair rerolled or resolved incoming combat"); s.Settle();
        });
        Case("repair-interrupt-preserves-movement-and-other-type", s =>
        {
            s.Do(0, new("move", "s0-0", Q: 3, R: 1, Card: "core:all-ahead-full#1"));
            // The responder repairs first, then may still use the distinct Attack type.
            var cards = s.S.Cards!; s.S = s.S with { Cards = cards with { Hand0 = cards.Hand0.Where(id => id != RepairCard).ToArray(), Hand1 = cards.Hand1.Append(RepairCard).ToArray() } };
            s.Do(1, new("interrupt-repairs", "s1-5", Card: RepairCard, Repair: "focus")); s.Do(0, new("decline-counter"));
            Check(s.S.Cards!.Response != null && s.Unit("s1-5").FocusFire == 2, "Repair consumed or advanced enemy movement");
            s.Do(1, new("interrupt", "s1-3", "s0-0", Card: "core:interrupt-attack#1")); s.Settle();
            Check(InterruptRules.Played(s.S.Cards!, 1).Length == 2 && s.S.Orders1 == 0, "Different interrupt types conflict or spend Play");
        });
        Case("repair-interrupt-offboard-squadron", s =>
        {
            var cards = s.S.Cards; s.S = s.E.Create(BasicCardBattle.LaunchOrdersMapId, 17) with { Cards = cards };
            s.S = s.S with { Reserves = s.S.Reserves!.Select(u => u.Id == "s0-1" ? u with { Models = 2 } : u).ToList() };
            s.Repair("s0-1"); Check(s.Unit("s0-1") is { Models: 3, Embarked: "s0-0", Activated: false }, "Repair disembarked the squadron");
        });
        Case("repair-interrupt-simultaneous-cap-snapshot", s =>
        {
            foreach (string id in new[] { "practice:orders-kushan-wing", "practice:orders-taiidan-wing" }) s.C.Ships[id] = s.C.Ships[id] with { Success = 2, Armor = 50, Weapons = [new("gun", "CAP test gun", 1, 5)], Abilities = [] };
            s.S = s.S with { ActiveSide = 1, ValorOwner = 0, Units = s.S.Units.Select(u => u.Id == "s0-0" ? u with { Q = 2, R = 2, FocusFire = 0 } : u.Id == "s0-2" ? u with { Q = 2, R = 2, Patrol = "s0-0" } : u.Id == "s1-5" ? u with { Q = 3, R = 2, FocusFire = 0 } : u.Id == "s1-3" ? u with { Q = 7, R = 1 } : u).ToList() };
            s.Do(1, new("attack", "s1-5", "s0-0", Card: "core:all-ahead-full#2")); if (s.S.Defense == null && s.S.Combat == null) s.Do(0, new("decline")); s.Do(0, new("cap", Card: "core:all-ahead-full#1", Weapon: "gun"));
            s.Do(FleetCombat.DecisionSide(s.S.Combat!), new("resolve"));
            Check(s.S.Defense!.CapVolley?.Ghost?.Id == "s0-2", "CAP fixture did not reach stored volley");
            var rolls = s.S.Combat!.Rolls.ToArray(); s.Repair();
            Check(s.S.Defense.CapVolley!.Ghost!.Models == 3 && s.S.Combat!.Rolls.SequenceEqual(rolls), "Repair corrupted CAP snapshot or rerolled incoming volley"); s.Settle();
        });
        Console.WriteLine($"REPAIR_INTERRUPTS_OK {count} scenarios");
    }
    private static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
}
