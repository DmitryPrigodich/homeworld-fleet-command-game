using System.Text.Json;
using FleetCommand.Core;

internal static class MoreHeroicScenarios
{
    private const string Medic = "core:hero-kushan-medical-chief", Arms = "core:hero-kushan-master-of-arms", Ace = "core:hero-kushan-fighter-ace",
        Veteran = "core:hero-kushan-grizzled-veteran", Hunter = "core:hero-taiidan-hunter", Captain = "core:hero-taiidan-veteran-captain";
    private static string Card(string hero, int side = 0) => hero + "#hero-" + side;
    private sealed class Session
    {
        public ContentCatalog C; public BattleEngine E; public BattleState S;
        private readonly string _path; private readonly List<object> _trace = [];
        public Session(string root, string name)
        {
            C = ContentCatalog.Load([Path.Combine(root, "content", "base")]); BasicCardBattle.Register(C); E = new(C); S = E.Create(HeroicBattle.AdvancedMapId, 3);
            foreach (string id in S.Units.Select(u => u.Definition).Distinct()) C.Ships[id] = C.Ships[id] with { Armor = 50, Success = 2, Evasive = 0, Weapons = [new("gun", "Hero sequence test gun", 2, 6, PowerDice: 2)] };
            S = S with { Cards = S.Cards! with { Phase = "orders", UsedInterrupts0 = ["attack", "move", "counter"], UsedInterrupts1 = ["attack", "move", "counter"] } };
            _path = Path.Combine(root, "tmp", "test-results", name + ".trace.json");
        }
        public BattleUnit Unit(string id) => S.Units.Single(u => u.Id == id);
        public void Do(int side, BattleCommand cmd, bool accepted = true)
        {
            string before = E.Save(S); var a = E.Apply(S, side, cmd); var b = E.Apply(E.Load(before), side, cmd);
            _trace.Add(new { side, cmd, a.Accepted, a.Message, a.State }); File.WriteAllText(_path, JsonSerializer.Serialize(_trace, JsonData.Options));
            Check(a.Accepted == accepted, a.Message); Check(E.Save(S) == before && E.Save(a.State) == E.Save(b.State), "More Heroic replay mutated input or diverged"); S = E.Load(E.Save(a.State));
            foreach (int sideView in new[] { 0, 1 }) { var view = E.Load(E.Save(BasicCardRules.View(S, sideView))); Check(view.RandomState == 1 && !E.Apply(view, sideView, cmd).Accepted, "Heroic view gained authority"); }
        }
        public void Start(string hero, string unit, int side = 0, string second = "")
        { S = S with { ActiveSide = side }; Do(side, new("special", unit, Card: Card(hero, side), OtherUnit: second)); }
        public void Settle()
        {
            for (int n = 0; n < 100; n++)
                if (S.Repairs is { Length: > 0 }) Do(S.Repairs[0].Side, new("repair-stop"));
                else if (S.Cards?.Counter is { } counter) Do(1 - counter.Side, new("decline-counter"));
                else if (S.Combat is { } combat) Do(FleetCombat.DecisionSide(combat), new(combat.AbilityRerolls > 0 ? "ability-reroll" : "resolve", Dice: []));
                else if (S.Defense is { } defense) Do(1 - defense.Side, new("decline-defense"));
                else if (S.CapEntry is { } cap) Do(cap.Side, new("decline-cap"));
                else if (S.Overrun is { } overrun) Do(overrun.Side, new("decline-overrun"));
                else if (S.Cards?.Response is { } pause) Do(InterruptRules.Responder(S), new(pause.Replan ? "stop-move" : "decline"));
                else return;
            throw new Exception("More Heroic continuation did not settle");
        }
        public void Rolls(int value)
        { Check(S.Combat != null, "Expected a held roll"); S = S with { Combat = S.Combat! with { Rolls = Enumerable.Repeat(value, S.Combat!.Rolls.Length).ToArray(), AbilityRerolls = 0, RerollSide = -1 } }; }
        public void Crew(string id, int amount) => S = S with { Units = S.Units.Select(u => u.Id == id ? u with { CrewDamage = amount } : u).ToList() };
        public void Place(string id, int q, int r) => S = S with { Units = S.Units.Select(u => u.Id == id ? u with { Q = q, R = r } : u).ToList() };
        public void Invalid(BattleState bad) { bool rejected = false; try { E.Load(E.Save(bad)); } catch (InvalidDataException) { rejected = true; } Check(rejected, "Forged Heroic action line loaded"); }
    }
    public static void Run(string root)
    {
        int count = 0;
        void Case(string name, Action<Session> test) { try { test(new(root, name)); } catch (Exception e) { throw new Exception(name + ": " + e.Message, e); } count++; Console.WriteLine("MORE HEROIC PASS " + name); }
        Case("more-heroic-six-profiles-outside-base-deck", s =>
        {
            Check(HeroicCommands.Pool(s.S.Cards!).Length == 6 && CommandCards.Instances(CommandCards.Deck(s.C, s.S)).Count == 54, "Extra Heroes altered the base deck");
            Check(s.C.Leaders[Medic] is { Staff: true, Medic: 5, Stars: 2, Hand: 1, Play: 1 } && s.C.Leaders[Veteran] is { BoardingDice: 2, CoolUnderPressure: 1 }, "Leader face transcription changed");
        });
        Case("patch-em-up-before-medic-and-optional-control", s =>
        {
            s.Crew("s0-0", 3); s.Start(Medic, "s0-0");
            Check(s.Unit("s0-0").CrewDamage == 1 && s.S.RandomState == 4018217795u, "Patch/Medic did not use one guaranteed token followed by two threshold-five dice");
            Check(s.S.Repairs == null && s.S.Orders0 == 1 && s.S.Cards!.HeroicUsed!.Contains(Card(Medic)), "Patch unexpectedly queued optional control or skipped spending"); s.Do(0, new("finish-order"));
        });
        Case("medic-runs-on-basic-order-even-with-control-skipped", s =>
        {
            s.Crew("s0-0", 2); string card = s.S.Cards!.Hand0.First(id => !HeroicCommands.IsHeroic(s.S, id));
            s.Do(0, new("begin-order", "s0-0", "move", Card: card, Repair: "")); Check(s.Unit("s0-0").CrewDamage == 1 && s.S.RandomState == 4018217795u, "Medic depended on Damage Control choice or ship Success"); s.Do(0, new("finish-order"));
        });
        Case("medic-runs-on-direct-basic-move", s =>
        {
            s.Crew("s0-0", 2); string card = s.S.Cards!.Hand0.First(id => !HeroicCommands.IsHeroic(s.S, id)); s.Do(0, new("move", "s0-0", Q: 3, R: 2, Card: card)); s.Settle();
            Check(s.Unit("s0-0").CrewDamage == 1 && s.S.RandomState == 4018217795u, "Direct basic move missed Medic");
        });
        Case("medic-does-not-repeat-for-second-action", s =>
        {
            s.Crew("s0-0", 2); s.Start(Arms, "s0-0"); uint random = s.S.RandomState;
            s.Do(0, new("move", "s0-0", Q: 3, R: 2)); s.Settle(); Check(s.Unit("s0-0").CrewDamage == 1 && s.S.RandomState == random, "Second action triggered Medic again");
            s.Do(0, new("finish-order"));
        });
        Case("patch-empty-crew-still-allows-the-action-sequence", s => { uint random = s.S.RandomState; s.Start(Medic, "s0-0"); Check(s.S.RandomState == random && SpecialOrders.Steps(s.C, s.S, s.S.Cards!.Pending!) == 2, "Empty Patch rolled dice or lost actions"); s.Do(0, new("finish-order")); });
        Case("power-and-precision-cripples-before-armor", s =>
        {
            s.S = s.S with { ValorOwner = 0 }; s.Place("s1-5", 5, 2); s.Start(Arms, "s0-0"); s.Do(0, new("skip")); s.Do(0, new("attack", "s0-0", "s1-5")); s.Rolls(6);
            Check(s.S.Combat!.Power, "Power and Precision lost Power Attack"); s.Do(0, new("resolve")); s.Settle(); Check(FleetRuleTools.Has(s.Unit("s1-5"), "weapons") && s.Unit("s1-5").Models == s.C.Ships[s.Unit("s1-5").Definition].Models, "Power effect waited for casualties");
        });
        Case("no-escape-cripples-engines-before-armor", s =>
        {
            s.S = s.S with { ValorOwner = 1 }; s.Start(Hunter, "s1-4", 1); s.Do(1, new("skip")); s.Do(1, new("attack", "s1-4", "s0-0")); s.Rolls(6); s.Do(1, new("resolve")); s.Settle();
            Check(FleetRuleTools.Has(s.Unit("s0-0"), "engines") && s.S.Cards!.HeroicUsed!.Contains(Card(Hunter, 1)), "No Escape lost its system effect or scenario spending");
        });
        Case("heroic-system-effect-requires-a-success", s =>
        {
            s.S = s.S with { ValorOwner = 0 }; s.Place("s1-5", 5, 2); s.Start(Arms, "s0-0"); s.Do(0, new("skip")); s.Do(0, new("attack", "s0-0", "s1-5")); s.Rolls(1); s.Do(0, new("resolve")); s.Settle();
            Check(!FleetRuleTools.Has(s.Unit("s1-5"), "weapons"), "Missed Heroic attack applied a system effect");
        });
        Case("heroic-system-effects-ignore-fighters", s =>
        {
            s.S = s.S with { ValorOwner = 1 }; s.Place("s1-4", 5, 1); s.Start(Hunter, "s1-4", 1); s.Do(1, new("skip")); s.Do(1, new("attack", "s1-4", "s0-1")); s.Rolls(6); s.Do(1, new("resolve")); s.Settle(); Check(!FleetRuleTools.Has(s.Unit("s0-1"), "engines"), "Heroic engine effect affected a fighter");
        });
        Case("lead-attack-validates-second-fighter-and-one-play", s =>
        {
            s.Do(0, new("special", "s0-1", Card: Card(Ace), OtherUnit: "s0-2"), false); s.Start(Ace, "s0-1", second: "s0-3");
            Check(s.Unit("s0-1").Activated && s.Unit("s0-3").Activated && s.S.Orders0 == 1, "Two-unit Heroic order did not activate both for one Play"); s.Do(0, new("finish-order"));
        });
        Case("lead-attack-remembers-first-target-through-saves", s =>
        {
            s.S = s.S with { ValorOwner = 0 }; s.Place("s0-1", 4, 3); s.Place("s0-3", 4, 4); s.Start(Ace, "s0-1", second: "s0-3"); s.Do(0, new("skip"));
            s.Do(0, new("attack", "s0-1", "s1-5")); s.Settle(); Check(s.S.Cards!.Pending is { Step: 2, FirstTarget: "s1-5" }, "Lead target was not retained"); s.Do(0, new("skip"));
            Check(!s.E.GetActions(s.S, 0, "s0-3").AttackTargets.Contains("s1-6"), "Second Heroic fighter can choose another target"); s.Do(0, new("attack", "s0-3", "s1-6"), false); s.Do(0, new("finish-order"));
        });
        Case("lead-attack-skipped-first-attack-grants-no-second-target", s => { s.Start(Ace, "s0-1", second: "s0-3"); s.Do(0, new("skip")); s.Do(0, new("skip")); s.Do(0, new("skip")); Check(s.E.GetActions(s.S, 0, "s0-3").AttackTargets.Length == 0, "Skipped first Heroic attack invented a shared target"); s.Do(0, new("finish-order")); });
        Case("heroic-second-line-only-does-not-activate-owner", s =>
        {
            s.Do(0, new("special", Card: Card(Ace), OtherUnit: "s0-3")); Check(!s.Unit("s0-1").Activated && s.Unit("s0-3").Activated && s.S.Cards!.Pending!.Step == 2, "Omitted Hero line activated its owner"); s.Do(0, new("finish-order"));
        });
        Case("frigate-attack-validates-lines-and-skipping", s =>
        {
            s.S = s.S with { ActiveSide = 1 }; s.Do(1, new("special", "s1-5", Card: Card(Captain, 1), OtherUnit: "s1-4"), false); s.Start(Captain, "s1-5", 1, "s1-6");
            s.Do(1, new("skip")); s.Do(1, new("skip")); Check(SpecialOrders.Actor(s.C, s.S, s.S.Cards!.Pending!) == "s1-6", "Frigate Attack failed to hand off to second frigate"); s.Do(1, new("finish-order"));
        });
        Case("heroic-saved-line-class-and-owner-forgery-rejected", s =>
        {
            s.Start(Ace, "s0-1", second: "s0-3"); var order = s.S.Cards!.Pending!;
            s.Invalid(s.S with { Cards = s.S.Cards with { Pending = order with { Units = ["s0-1", "s0-2"] } }, Units = s.S.Units.Select(u => u.Id == "s0-2" ? u with { Activated = true } : u).ToList() });
        });
        Case("lightning-strike-boarding-after-actual-movement", s =>
        {
            s.Place("s1-5", 4, 5); s.Start(Veteran, "s0-2"); s.Do(0, new("move", "s0-2", Q: 3, R: 5)); s.Settle();
            Check(s.E.GetActions(s.S, 0, "s0-2").AttackTargets.Contains("s1-5"), "Completed Move did not grant optional boarding");
            var cmd = new BattleCommand("attack", "s0-2", "s1-5", AttackKind: "board"); int withCard = FleetCombat.AttackDice(s.C, s.S, cmd);
            var plain = s.S with { Cards = s.S.Cards! with { Pending = null } }; Check(withCard == FleetCombat.AttackDice(s.C, plain, cmd) + 2, "Lightning Strike did not add two dice beyond Marines");
            s.Do(0, cmd); s.Settle(); Check(s.S.Cards!.Pending?.Step == 2, "Boarding did not return to Lightning Move");
            s.Do(0, new("move", "s0-2", Q: 4, R: 4)); s.Settle(); Check(s.S.Cards!.Pending?.Step == 3 && !s.E.GetActions(s.S, 0, "s0-2").AttackTargets.Contains("s1-5"), "Lightning Strike bypassed once-per-order boarding limit"); s.Do(0, new("finish-order"));
        });
        Case("lightning-strike-skipped-moves-grant-no-boarding", s =>
        { s.Start(Veteran, "s0-2"); s.Do(0, new("skip")); Check(s.E.GetActions(s.S, 0, "s0-2").AttackTargets.Length == 0, "Skipped Move granted boarding"); s.Do(0, new("skip")); s.Do(0, new("skip")); Check(s.E.GetActions(s.S, 0, "s0-2").AttackTargets.Length == 0, "Skipped Lightning reused prior movement"); s.Do(0, new("finish-order")); });
        Case("lightning-strike-second-move-has-independent-boarding-eligibility", s =>
        { s.Place("s1-5", 4, 5); s.Start(Veteran, "s0-2"); s.Do(0, new("skip")); s.Do(0, new("skip")); s.Do(0, new("move", "s0-2", Q: 3, R: 5)); s.Settle(); Check(s.S.Cards!.Pending?.Step == 3 && s.E.GetActions(s.S, 0, "s0-2").AttackTargets.Contains("s1-5"), "Actual Lightning Move did not grant boarding"); s.Do(0, new("finish-order")); });
        Case("leader-marines-bonus-applies-only-to-boarding", s =>
        {
            s.Place("s1-5", 3, 5); var cmd = new BattleCommand("attack", "s0-2", "s1-5", AttackKind: "board"); var plain = s.S with { Units = s.S.Units.Select(u => u.Id == "s0-2" ? u with { Leader = "" } : u).ToList() };
            Check(FleetCombat.AttackDice(s.C, s.S, cmd) == FleetCombat.AttackDice(s.C, plain, cmd) + 2, "Marines missing from boarding"); var ram = cmd with { AttackKind = "ram" }; Check(FleetCombat.AttackDice(s.C, s.S, ram) == FleetCombat.AttackDice(s.C, plain, ram), "Marines leaked into ram dice");
        });
        Case("capital-killer-aura-filters-target-class-and-adjacency", s =>
        {
            s.Place("s0-2", 3, 2); Check(FleetRuleTools.AttackAura(s.C, s.S, s.Unit("s0-2"), s.Unit("s1-4")) == 1, "Adjacent Capital Killer aura missing");
            Check(FleetRuleTools.AttackAura(s.C, s.S, s.Unit("s0-2"), s.Unit("s0-1")) == 0, "Capital Killer matched fighter class"); s.Place("s0-2", 4, 2); Check(FleetRuleTools.AttackAura(s.C, s.S, s.Unit("s0-2"), s.Unit("s1-4")) == 0, "Capital Killer exceeded adjacency");
        });
        Case("fleet-command-aura-includes-self-and-any-target", s =>
        { s.Place("s1-5", 8, 2); Check(FleetRuleTools.AttackAura(s.C, s.S, s.Unit("s1-4"), s.Unit("s0-1")) == 1 && FleetRuleTools.AttackAura(s.C, s.S, s.Unit("s1-5"), s.Unit("s0-1")) == 1, "Fleet Command missed self or fighter-target support"); });
        Case("attack-auras-stack-without-boosting-missile-defense", s =>
        {
            s.Place("s1-5", 8, 2); Check(FleetRuleTools.AttackAura(s.C, s.S, s.Unit("s1-5"), s.Unit("s0-0")) == 2, "Eligible leader auras did not stack");
            var defender = s.Unit("s1-5"); var weapon = new WeaponDefinition("defense", "Test defense", 2, 1, DefensiveFire: true);
            s.S = s.S with { Defense = new(0, new("attack", "s0-0", defender.Id), false, "missile", 4) }; int dice = FleetDefense.Dice(s.C, s.S, weapon);
            var plain = s.S with { Units = s.S.Units.Select(u => u.Side == 1 ? u with { Leader = "" } : u).ToList() }; Check(FleetDefense.Dice(s.C, plain, weapon) == dice, "Attack aura used the launcher's class to boost missile defense");
        });
        Case("actual-ranged-pool-consumes-eligible-attack-aura", s =>
        {
            s.Place("s0-2", 3, 2); var cmd = new BattleCommand("attack", "s0-2", "s1-4", Weapon: "gun");
            var plain = s.S with { Units = s.S.Units.Select(u => u.Id == "s0-0" ? u with { Leader = "", AdditionalLeaders = null } : u).ToList() };
            Check(FleetCombat.AttackDice(s.C, s.S, cmd) == FleetCombat.AttackDice(s.C, plain, cmd) + 1, "Combat pool did not consume the adjacent leader's aura");
        });
        Case("attack-auras-ignore-hulks-and-opponents", s =>
        {
            s.Place("s0-2", 3, 2); s.Place("s1-5", 4, 2); s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s0-0" ? u with { Hulk = true } : u).ToList() };
            Check(FleetRuleTools.AttackAura(s.C, s.S, s.Unit("s0-2"), s.Unit("s1-4")) == 0, "Hulk or enemy leader supplied an allied aura");
        });
        Console.WriteLine($"MORE_HEROIC_OK {count} scenarios");
    }
    private static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
}
