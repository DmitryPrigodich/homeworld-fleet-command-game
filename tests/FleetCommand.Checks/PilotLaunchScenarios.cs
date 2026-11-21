using System.Text.Json;
using FleetCommand.Core;

internal static class PilotLaunchScenarios
{
    private const string Pilot = "core:hero-taiidan-veteran-commander", Launch = "core:hero-taiidan-launch-commander";
    private static string Card(string hero) => hero + "#hero-1";
    private sealed class Session
    {
        public ContentCatalog C; public BattleEngine E; public BattleState S;
        private readonly string _path; private readonly List<object> _trace = [];
        public Session(string root, string name)
        {
            C = ContentCatalog.Load([Path.Combine(root, "content", "base")]); BasicCardBattle.Register(C); E = new(C); S = E.Create(HeroicBattle.PilotLaunchMapId, 3);
            foreach (string id in S.Units.Concat(S.Reserves ?? []).Select(u => u.Definition).Distinct()) C.Ships[id] = C.Ships[id] with { Armor = 50, Success = 2, Evasive = 0, Weapons = [new("gun", "Pilot launch test gun", 2, 6, PowerDice: 2)] };
            S = S with { ActiveSide = 1, ValorOwner = 1, Cards = S.Cards! with { Phase = "orders", UsedInterrupts0 = ["attack", "move", "counter"], UsedInterrupts1 = ["attack", "move", "counter"] } };
            _path = Path.Combine(root, "tmp", "test-results", name + ".trace.json");
        }
        public BattleUnit Unit(string id) => S.Units.Concat(S.Reserves ?? []).Single(u => u.Id == id);
        public void Do(int side, BattleCommand cmd, bool accepted = true)
        {
            string before = E.Save(S); var a = E.Apply(S, side, cmd); var b = E.Apply(E.Load(before), side, cmd);
            _trace.Add(new { side, cmd, a.Accepted, a.Message, a.State }); File.WriteAllText(_path, JsonSerializer.Serialize(_trace, JsonData.Options));
            Check(a.Accepted == accepted, a.Message); Check(E.Save(S) == before && E.Save(a.State) == E.Save(b.State), "Pilot/launch replay mutated input or diverged"); S = E.Load(E.Save(a.State));
            foreach (int sideView in new[] { 0, 1 }) { var view = E.Load(E.Save(BasicCardRules.View(S, sideView))); Check(view.RandomState == 1 && !E.Apply(view, sideView, cmd).Accepted, "Pilot/launch view gained authority"); }
        }
        public void StartPilot() => Do(1, new("special", "s1-3", Card: Card(Pilot)));
        public void StartLaunch(string first = "s1-4", string second = "s1-5") => Do(1, new("special", first, Card: Card(Launch), OtherUnit: second));
        public void Place(string id, int q, int r) => S = S with { Units = S.Units.Select(u => u.Id == id ? u with { Q = q, R = r } : u).ToList() };
        public void LaunchChild(string id, int r = 5) => Do(1, new("launch", id, Q: 7, R: r, Facing: 3));
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
            throw new Exception("Pilot/launch continuation did not settle");
        }
        public void Give(int side, string id)
        {
            var cards = S.Cards!; var hand = side == 0 ? cards.Hand0 : cards.Hand1; if (hand.Contains(id)) return;
            string swap = hand.First(c => !HeroicCommands.IsHeroic(S, c)); var all = cards.Deck.Concat(cards.Hand0).Concat(cards.Hand1).Concat(cards.Discard).Where(c => c != id && c != swap);
            var a = side == 0 ? hand.Where(c => c != swap).Append(id).ToArray() : cards.Hand0.Where(c => c != id).ToArray();
            var b = side == 1 ? hand.Where(c => c != swap).Append(id).ToArray() : cards.Hand1.Where(c => c != id).ToArray();
            S = S with { Cards = cards with { Hand0 = a, Hand1 = b, Discard = [], Deck = all.Except(a).Except(b).Append(swap).ToArray() } };
        }
        public void Invalid(BattleState bad) { bool rejected = false; try { E.Load(E.Save(bad)); } catch (InvalidDataException) { rejected = true; } Check(rejected, "Forged pilot/launch frame loaded"); }
    }
    public static void Run(string root)
    {
        int count = 0;
        void Case(string name, Action<Session> test) { try { test(new(root, name)); } catch (Exception e) { throw new Exception(name + ": " + e.Message, e); } count++; Console.WriteLine("PILOT LAUNCH PASS " + name); }
        Case("pilot-launch-published-profiles-and-deck-isolation", s =>
        {
            Check(HeroicCommands.Pool(s.S.Cards!).Length == 2 && CommandCards.Instances(CommandCards.Deck(s.C, s.S)).Count == 54, "New Heroes changed shared deck");
            Check(s.C.Leaders[Pilot] is { Stars: 2, Hand: 2, Play: 1, CoolUnderPressure: 1, AttackAura: 1 } && s.C.Leaders[Launch] is { Stars: 3, Hand: 2, Play: 1, CarrierOnly: true, AttackAuraRange: 2 }, "Printed leader values changed");
        });
        Case("pilot-launch-solo-gives-heroes-to-human-side", s =>
        {
            var solo = s.E.Create(HeroicBattle.PilotLaunchSoloMapId, 3);
            Check(solo.Units.Any(u => u.Side == 0 && u.Leader == Pilot) && solo.Cards!.Hand0.Contains(Launch + "#hero-0"), "Solo drill gave Heroes to automated opponent");
            s.E.Load(s.E.Save(solo));
        });
        Case("supreme-pilot-crosses-enemy-asteroid-and-mine-without-rolls", s =>
        {
            s.StartPilot(); uint random = s.S.RandomState; Check(s.E.GetActions(s.S, 1, "s1-3").MoveCells.Contains(new(5, 2)), "Missing route: " + string.Join(",", s.E.GetActions(s.S, 1, "s1-3").MoveCells)); s.Do(1, new("move", "s1-3", Q: 5, R: 2)); s.Settle();
            Check(s.S.Cards!.Pending?.Step == 1 && s.Unit("s1-3").Q == 5 && s.S.RandomState == random && FleetTerrain.MineCount(s.S, 1) == 5, "Supreme Move collided with a blocker or terrain");
            s.Do(1, new("move", "s1-3", Q: 3, R: 2)); s.Settle(); Check(s.S.Cards!.Pending?.Step == 2 && s.Unit("s1-3").Q == 3 && s.S.RandomState == random, "Supreme Lightning lost terrain immunity");
            s.Do(1, new("attack", "s1-3", "s0-0", Weapon: "gun")); s.Settle(); Check(s.S.Cards!.Pending == null && s.S.Orders1 == 1 && s.S.Cards.HeroicUsed!.Contains(Card(Pilot)), "Pilot order did not finish once");
        });
        Case("supreme-pilot-cannot-end-on-enemy-or-transfer-immunity", s =>
        {
            s.StartPilot(); Check(FleetFootprint.CanPlace(s.C, s.S, s.Unit("s1-3"), new(6, 2), 3, transit: true), "Pilot cannot transit enemy");
            Check(!s.E.GetActions(s.S, 1, "s1-3").MoveCells.Contains(new(6, 2)), "Pilot may finish on an enemy"); s.Do(1, new("move", "s1-3", Q: 6, R: 2), false);
            Check(!FleetFootprint.CanPlace(s.C, s.S, s.Unit("s1-2"), new(6, 2), 3, transit: true), "Pilot immunity leaked to carrier"); s.Do(1, new("finish-order"));
            Check(!SpecialOrders.PilotMovement(s.C, s.S, "s1-3"), "Pilot movement permission survived its order");
        });
        Case("supreme-pilot-basic-payment-does-not-grant-immunity", s =>
        {
            s.Do(1, new("begin-order", "s1-3", "move", Card: Card(Pilot))); Check(!FleetFootprint.CanPlace(s.C, s.S, s.Unit("s1-3"), new(6, 2), 3, transit: true), "Basic Heroic payment granted transit");
            s.Do(1, new("move", "s1-3", Q: 7, R: 2)); Check(s.S.RandomState != 3, "Basic Heroic payment suppressed asteroid attack"); s.Settle();
        });
        Case("supreme-pilot-has-no-blanket-activation-immunity", s =>
        { s.Place("s1-3", 7, 2); s.StartPilot(); Check(s.S.RandomState != 3, "Starting in terrain skipped its activation attack"); s.Settle(); Check(s.S.Cards!.Pending?.Step == 0, "Activation terrain consumed Supreme Move"); s.Do(1, new("finish-order")); });
        Case("supreme-pilot-retains-real-enemy-response-windows", s =>
        {
            s.S = s.S with { Cards = s.S.Cards! with { UsedInterrupts0 = [] } }; s.StartPilot(); uint random = s.S.RandomState; s.Do(1, new("move", "s1-3", Q: 5, R: 2));
            Check(s.S.Cards!.Response is { Command.Kind: "move" } && s.S.Hazard == null && s.Unit("s1-3").Q == 7, "Terrain immunity removed ordinary enemy reactions");
            s.Settle(); Check(s.Unit("s1-3").Q == 5 && s.S.Cards!.Pending?.Step == 1 && s.S.RandomState == random, "Saved response lost protected remaining path"); s.Do(1, new("finish-order"));
        });
        Case("supreme-pilot-does-not-pass-planets-or-hulks", s =>
        {
            s.StartPilot(); s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s0-0" ? u with { Hulk = true } : u).ToList() };
            Check(!FleetFootprint.CanPlace(s.C, s.S, s.Unit("s1-3"), new(6, 2), 3, transit: true), "Pilot passed a drifting hulk");
            s.C.Maps[s.S.Map] = s.C.Maps[s.S.Map] with { Objects = [new("core:planet", 7, 2)] }; Check(!FleetFootprint.CanPlace(s.C, s.S, s.Unit("s1-3"), new(7, 2), 3, transit: true), "Pilot passed a planet");
        });
        Case("launch-all-fighters-assigns-two-without-reordering-carrier", s =>
        {
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s1-2" ? u with { Activated = true, Effects = ["brace"] } : u).ToList() }; s.StartLaunch();
            Check(s.Unit("s1-4").Activated && s.Unit("s1-5").Activated && FleetRuleTools.Has(s.Unit("s1-2"), "brace") && s.Unit("s1-2").Activated && s.S.Orders1 == 1, "Launch assignment reordered carrier or spent extra Play"); s.Do(1, new("finish-order"));
        });
        Case("launch-all-fighters-completes-both-launched-action-lines", s =>
        {
            s.StartLaunch(); s.LaunchChild("s1-4"); s.Settle(); s.Do(1, new("move", "s1-4", Q: 6, R: 5)); s.Settle(); s.Do(1, new("attack", "s1-4", "s0-1", Weapon: "gun")); s.Settle();
            Check(s.S.Cards!.Pending is { Step: 3 } && SpecialOrders.Actor(s.C, s.S, s.S.Cards.Pending) == "s1-5", "First line skipped the unlaunched second fighter");
            s.LaunchChild("s1-5", 6); s.Settle(); Check(s.S.Cards!.Pending?.Step == 4, "Second launch did not reach its Lightning Move"); s.Do(1, new("skip")); s.Do(1, new("attack", "s1-5", "s0-1", Weapon: "gun")); s.Settle();
            Check(s.S.Cards!.Pending == null && s.Unit("s1-2") is { Activated: false, Launches: 2 } && s.S.Orders1 == 1 && !s.S.Cards.Discard.Contains(Card(Launch)), "Two launches changed carrier activation or Heroic spending");
        });
        Case("launch-all-fighters-rejects-wrong-parent-class-and-duplicates", s =>
        {
            s.Do(1, new("special", "s1-4", Card: Card(Launch), OtherUnit: "s1-7"), false); s.Do(1, new("special", "s1-4", Card: Card(Launch), OtherUnit: "s1-4"), false);
            s.S = s.S with { Reserves = s.S.Reserves!.Select(u => u.Id == "s1-5" ? u with { Embarked = "s1-6" } : u).ToList() };
            s.Do(1, new("special", "s1-4", Card: Card(Launch), OtherUnit: "s1-5"), false); Check(s.S.Orders1 == 0 && s.S.Cards!.Hand1.Contains(Card(Launch)), "Invalid pair spent card or Play");
        });
        Case("launch-all-fighters-second-line-only", s =>
        { s.StartLaunch("", "s1-5"); Check(s.S.Cards!.Pending?.Step == 3 && !s.Unit("s1-4").Activated && s.Unit("s1-5").Activated, "Omitted first launch activated its squadron"); s.LaunchChild("s1-5"); s.Settle(); s.Do(1, new("finish-order")); });
        Case("launch-all-fighters-first-line-only", s =>
        { s.StartLaunch("s1-4", ""); s.LaunchChild("s1-4"); s.Settle(); s.Do(1, new("skip")); s.Do(1, new("skip")); Check(s.S.Cards!.Pending == null && !s.Unit("s1-5").Activated, "Omitted second launch activated its squadron"); });
        Case("launch-all-fighters-skipped-first-launch-reaches-second", s =>
        { s.StartLaunch(); s.Do(1, new("skip")); Check(s.S.Cards!.Pending?.Step == 3 && s.Unit("s1-4").Embarked == "s1-2", "Skipping first launch skipped the second line"); s.LaunchChild("s1-5"); s.Settle(); s.Do(1, new("finish-order")); });
        Case("launch-all-fighters-respects-tubes-and-bay-crippling", s =>
        {
            s.StartLaunch(); s.LaunchChild("s1-4"); s.Settle(); s.Do(1, new("skip")); s.Do(1, new("skip"));
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s1-2" ? u with { Effects = ["bays"] } : u).ToList() };
            Check(s.E.GetActions(s.S, 1, "s1-5").MoveCells.Length == 0, "Crippled bays retained launch highlights"); s.LaunchChildRejected(); s.Do(1, new("skip")); Check(s.S.Cards!.Pending == null, "Unavailable second launch could not be skipped");
        });
        Case("launch-all-fighters-cannot-exceed-launch-tubes", s =>
        {
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s1-2" ? u with { Launches = 1 } : u).ToList() };
            s.StartLaunch(); s.LaunchChild("s1-4"); s.Settle(); s.Do(1, new("skip")); s.Do(1, new("skip"));
            Check(s.Unit("s1-2").Launches == 2 && s.E.GetActions(s.S, 1, "s1-5").MoveCells.Length == 0, "Heroic launch bypassed Launch Tubes"); s.LaunchChildRejected(); s.Do(1, new("skip"));
            Check(s.S.Cards!.Pending == null && s.Unit("s1-5").Embarked == "s1-2", "Exhausted launch could not be skipped");
        });
        Case("launch-all-fighters-second-arrival-has-saved-response", s =>
        {
            s.StartLaunch(); s.LaunchChild("s1-4"); s.Settle(); s.Do(1, new("skip")); s.Do(1, new("skip")); s.S = s.S with { Cards = s.S.Cards! with { UsedInterrupts0 = [] } };
            s.LaunchChild("s1-5", 6); Check(s.S.Cards!.Response is { Command.Kind: "reinforce", Command.Unit: "s1-5" } && s.S.Cards.Pending?.Step == 4, "Second fighter lost Reinforce response timing"); s.Settle(); s.Do(1, new("finish-order"));
        });
        Case("launch-all-fighters-first-arrival-death-keeps-second-line", s =>
        {
            s.Give(0, "core:interrupt-attack#1"); s.S = s.S with { Cards = s.S.Cards! with { UsedInterrupts0 = [] } };
            string fighter = s.Unit("s1-4").Definition; s.C.Ships[fighter] = s.C.Ships[fighter] with { Armor = 0, Models = 1 }; s.S = s.S with { Reserves = s.S.Reserves!.Select(u => u.Definition == fighter ? u with { Models = 1 } : u).ToList() };
            string gunner = s.Unit("s0-0").Definition; s.C.Ships[gunner] = s.C.Ships[gunner] with { Weapons = [new("gun", "Lethal arrival fixture", 20, 6)] };
            s.StartLaunch(); s.LaunchChild("s1-4"); s.Do(0, new("interrupt", "s0-0", "s1-4", Card: "core:interrupt-attack#1", Weapon: "gun")); s.Settle();
            Check(!s.S.Units.Any(u => u.Id == "s1-4") && s.S.Cards!.Pending?.Step == 3, "Destroyed first arrival ended both launch lines"); s.LaunchChild("s1-5", 6); s.Settle(); s.Do(1, new("finish-order"));
        });
        Case("launch-commander-aura-filters-attacker-and-two-hex-range", s =>
        {
            s.StartLaunch(); s.LaunchChild("s1-4"); s.Settle(); Check(FleetRuleTools.AttackAura(s.C, s.S, s.Unit("s1-4"), s.Unit("s0-1")) == 1, "Carrier failed to support launched fighter");
            Check(FleetRuleTools.AttackAura(s.C, s.S, s.Unit("s1-2"), s.Unit("s0-1")) == 0, "Strike Coordination boosted its capital carrier");
            s.Place("s1-4", 6, 5); Check(FleetRuleTools.AttackAura(s.C, s.S, s.Unit("s1-4"), s.Unit("s0-1")) == 1, "Strike Coordination did not reach two hexes"); s.Place("s1-4", 5, 6);
            Check(FleetRuleTools.AttackAura(s.C, s.S, s.Unit("s1-4"), s.Unit("s0-1")) == 0, "Strike Coordination exceeded two hexes");
        });
        Case("veteran-commander-strike-command-filters-target-class", s =>
        { Check(FleetRuleTools.AttackAura(s.C, s.S, s.Unit("s1-3"), s.Unit("s1-4")) == 1 && FleetRuleTools.AttackAura(s.C, s.S, s.Unit("s1-3"), s.Unit("s0-0")) == 0, "Strike Command target filter is wrong"); });
        Case("launch-commander-requires-a-compatible-carrier", s =>
        { var ship = s.C.Ships[s.Unit("s1-2").Definition]; Check(FleetRuleTools.CanLead(s.C.Leaders[Launch], ship) && !FleetRuleTools.CanLead(s.C.Leaders[Launch], ship with { Bays = 0 }) && !FleetRuleTools.CanLead(s.C.Leaders[Launch], ship with { LaunchTubes = 0 }), "Carrier-only leader accepts a non-carrier"); });
        Case("launch-saved-second-class-and-activation-forgery-rejected", s =>
        {
            s.StartLaunch(); var order = s.S.Cards!.Pending!;
            s.Invalid(s.S with { Cards = s.S.Cards with { Pending = order with { Units = ["s1-4", "s1-7"] } }, Reserves = s.S.Reserves!.Select(u => u.Id == "s1-7" ? u with { Activated = true } : u).ToList() });
            s.Invalid(s.S with { Reserves = s.S.Reserves!.Select(u => u.Id == "s1-5" ? u with { Activated = false } : u).ToList() });
        });
        Console.WriteLine($"PILOT_LAUNCH_OK {count} scenarios");
    }
    private static void LaunchChildRejected(this Session s) => s.Do(1, new("launch", "s1-5", Q: 7, R: 6, Facing: 3), false);
    private static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
}
