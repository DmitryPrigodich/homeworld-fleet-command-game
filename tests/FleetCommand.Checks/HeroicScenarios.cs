using System.Text.Json;
using FleetCommand.Core;

internal static class HeroicScenarios
{
    private const string Pilot = "core:hero-taiidan-hot-shot-pilot", Engineer = "core:hero-taiidan-master-engineer",
        Strategist = "core:hero-kushan-strategist", Overseer = "core:hero-kushan-station-overseer";
    private static string Card(string leader, int side) => leader + "#hero-" + side;
    private sealed class Session
    {
        public ContentCatalog C; public BattleEngine E; public BattleState S;
        private readonly string _path; private readonly List<object> _trace = [];
        public Session(string root, string name)
        {
            C = ContentCatalog.Load([Path.Combine(root, "content", "base")]); BasicCardBattle.Register(C); E = new(C); S = E.Create(HeroicBattle.MapId, 17);
            S = S with { Cards = S.Cards! with { Phase = "orders", UsedInterrupts0 = ["attack", "move", "counter"], UsedInterrupts1 = ["attack", "move", "counter"] } };
            _path = Path.Combine(root, "tmp", "test-results", name + ".trace.json");
        }
        public BattleUnit Unit(string id) => S.Units.Single(u => u.Id == id);
        public void Do(int side, BattleCommand cmd, bool accepted = true)
        {
            string before = E.Save(S); var a = E.Apply(S, side, cmd); var b = E.Apply(E.Load(before), side, cmd);
            _trace.Add(new { side, cmd, a.Accepted, a.Message, a.State }); File.WriteAllText(_path, JsonSerializer.Serialize(_trace, JsonData.Options));
            Check(a.Accepted == accepted, a.Message); Check(E.Save(S) == before && E.Save(a.State) == E.Save(b.State), "Heroic replay mutated input or diverged"); S = E.Load(E.Save(a.State));
            foreach (int viewer in new[] { 0, 1 }) { var view = E.Load(E.Save(BasicCardRules.View(S, viewer))); Check(view.RandomState == 1 && !E.Apply(view, viewer, cmd).Accepted, "Private Heroic view gained authority"); }
        }
        public void NormalHand(params string[] ids)
        {
            var h0 = S.Cards!.Hand0.Where(id => HeroicCommands.IsHeroic(S, id)).Concat(ids).ToArray(); var h1 = S.Cards.Hand1.Where(id => HeroicCommands.IsHeroic(S, id)).ToArray();
            S = S with { Cards = S.Cards with { Hand0 = h0, Hand1 = h1, Discard = [], Deck = CommandCards.Instances(CommandCards.Deck(C, S)).Keys.Except(ids).ToArray() } };
        }
        public void Start(string leader, int side, string unit, string repair = "auto")
        { S = S with { ActiveSide = side }; Do(side, new("special", unit, Card: Card(leader, side), CardRepair: repair)); }
        public void Settle()
        {
            for (int n = 0; n < 100; n++)
                if (S.Cards?.Counter is { } counter) Do(1 - counter.Side, new("decline-counter"));
                else if (S.Combat is { } combat) Do(FleetCombat.DecisionSide(combat), new(combat.AbilityRerolls > 0 ? "ability-reroll" : "resolve", Dice: []));
                else if (S.Defense is { } defense) Do(1 - defense.Side, new("decline-defense"));
                else if (S.CapEntry is { } cap) Do(cap.Side, new("decline-cap"));
                else if (S.Overrun is { } overrun) Do(overrun.Side, new("decline-overrun"));
                else if (S.Cards?.Response is { } pause) Do(InterruptRules.Responder(S), new(pause.Replan ? "stop-move" : "decline"));
                else return;
            throw new Exception("Heroic continuation did not settle");
        }
        public void Invalid(BattleState s) { bool bad = false; try { E.Load(E.Save(s)); } catch (InvalidDataException) { bad = true; } Check(bad, "Malformed Heroic save loaded"); }
    }
    public static void Run(string root)
    {
        int count = 0;
        void Case(string name, Action<Session> test) { try { test(new(root, name)); } catch (Exception e) { throw new Exception(name + ": " + e.Message, e); } count++; Console.WriteLine("HEROIC PASS " + name); }
        Case("heroic-initial-hands-outside-shared-deck", s =>
        {
            var cards = s.S.Cards!; Check(HeroicCommands.Pool(cards).Length == 4 && cards.Hand0.Length == cards.HandLimit0 + 2 && cards.Hand1.Length == cards.HandLimit1 + 2, "Heroic hand counted towards normal Hand");
            Check(CommandCards.Instances(CommandCards.Deck(s.C, s.S)).Count == 54 && cards.Deck.All(id => !HeroicCommands.IsHeroic(s.S, id)), "Heroic faces entered the shared deck");
            Check(CommandCards.Find(s.C, s.S, Card(Pilot, 1))?.Id == "core:hero-best-of-the-best", "Heroic instance resolution failed");
        });
        Case("heroic-special-owner-and-side-required", s =>
        {
            s.Do(0, new("special", "s0-1", Card: Card(Strategist, 0)), false); s.Do(1, new("special", "s1-3", Card: Card(Pilot, 1)), false);
            s.Do(0, new("special", "s0-0", Card: Card(Pilot, 1)), false); Check(s.S.Orders0 == 0 && s.S.Orders1 == 0, "Invalid Heroic assignment spent Play");
        });
        Case("heroic-start-buff-before-actions-and-once-per-game", s =>
        {
            s.Start(Strategist, 0, "s0-0"); Check(FleetRuleTools.Has(s.Unit("s0-0"), "brace") && s.S.Orders0 == 1 && s.S.Cards!.HeroicUsed!.Contains(Card(Strategist, 0)), "Heroic start effect or payment missing");
            Check(!s.S.Cards!.Discard.Contains(Card(Strategist, 0)), "Heroic card entered discard"); s.Do(0, new("skip"));
            Check(SpecialOrders.Action(s.C, s.S, s.S.Cards.Pending!) == "power", "Printed Power Attack missing"); s.Do(0, new("finish-order"));
            s.S = s.S with { ActiveSide = 0, Units = s.S.Units.Select(u => u.Id == "s0-0" ? u with { Activated = false } : u).ToList() }; s.Do(0, new("special", "s0-0", Card: Card(Strategist, 0)), false);
        });
        Case("heroic-lockdown-applies-before-power-attack", s => { s.Start(Overseer, 0, "s0-1"); Check(FleetRuleTools.Has(s.Unit("s0-1"), "lockdown") && SpecialOrders.Action(s.C, s.S, s.S.Cards!.Pending!) == "power", "Lockdown timing or sequence wrong"); s.Do(0, new("finish-order")); });
        Case("heroic-evading-stacks-with-pilot-ability", s =>
        {
            Check(FleetRuleTools.Evasion(s.C, s.Unit("s1-3")) == s.C.Ships[s.Unit("s1-3").Definition].Evasive + 1, "Lead by Example missing");
            s.Start(Pilot, 1, "s1-3"); Check(FleetRuleTools.Has(s.Unit("s1-3"), "hero-evasive") && FleetRuleTools.Evasion(s.C, s.Unit("s1-3")) == s.C.Ships[s.Unit("s1-3").Definition].Evasive + 2, "Corrected Best of the Best did not stack"); s.Do(1, new("finish-order"));
        });
        Case("heroic-engineer-whole-token-and-permanent-removal", s =>
        {
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s1-4" ? u with { FocusFire = 3, Effects = ["weapons"] } : u).ToList() }; s.Start(Engineer, 1, "s1-4", "focus");
            Check(s.Unit("s1-4").FocusFire == 1 && FleetRuleTools.Has(s.Unit("s1-4"), "weapons"), "Engineer did not remove one whole selected token"); s.Do(1, new("finish-order")); Check(s.Unit("s1-4").FocusFire == 1, "Engineer repair returned at end of order");
        });
        Case("heroic-engineer-invalid-selection-spends-nothing", s => { s.S = s.S with { ActiveSide = 1 }; s.Do(1, new("special", "s1-4", Card: Card(Engineer, 1), CardRepair: "engines"), false); });
        Case("heroic-engineer-crippled-repair", s => { s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s1-4" ? u with { Effects = ["engines", "bays"] } : u).ToList() }; s.Start(Engineer, 1, "s1-4", "engines"); Check(!FleetRuleTools.Has(s.Unit("s1-4"), "engines") && FleetRuleTools.Has(s.Unit("s1-4"), "bays"), "Wrong Crippled token removed"); s.Do(1, new("finish-order")); });
        Case("heroic-basic-order-may-target-another-ready-unit", s =>
        { s.Do(0, new("begin-order", "s0-2", "move", Card: Card(Strategist, 0))); Check(!FleetRuleTools.Has(s.Unit("s0-2"), "brace") && s.S.Cards!.HeroicUsed!.Contains(Card(Strategist, 0)), "Basic use granted the special Hero effect"); s.Do(0, new("finish-order")); });
        Case("heroic-direct-basic-move-spends-outside-discard", s =>
        { s.Do(0, new("move", "s0-2", Q: 3, R: 4, Card: Card(Strategist, 0))); s.Settle(); Check(s.S.Cards!.HeroicUsed!.Contains(Card(Strategist, 0)) && !s.S.Cards!.Discard.Contains(Card(Strategist, 0)) && s.S.Orders0 == 1, "Direct basic Heroic payment failed"); });
        Case("heroic-initiative-commitment-spends-on-resolution", s =>
        {
            s.S = s.S with { Cards = s.S.Cards! with { Phase = "initiative" } }; s.Do(0, new("initiative", Cards: [Card(Strategist, 0)]));
            Check(!s.S.Cards!.HeroicUsed!.Contains(Card(Strategist, 0)) && s.S.Cards.Hand0.Contains(Card(Strategist, 0)), "Bid spent before both commitments"); s.Do(1, new("initiative"));
            Check(s.S.Cards.HeroicUsed!.Contains(Card(Strategist, 0)) && !s.S.Cards!.Discard.Contains(Card(Strategist, 0)), "Heroic bid recycled");
        });
        Case("heroic-retention-and-refill-ignore-heroic-hand", s =>
        {
            s.NormalHand("core:all-ahead-full#1"); s.S = s.S with { Cards = s.S.Cards! with { Phase = "cleanup" } };
            s.Do(0, new("cleanup")); s.Do(1, new("cleanup")); Check(s.S.Cards!.Hand0.Length == s.S.Cards.HandLimit0 + 2 && s.S.Cards.Hand1.Length == s.S.Cards.HandLimit1 + 2 && HeroicCommands.Pool(s.S.Cards).Length == 4, "Round refill lost or counted Heroic cards");
        });
        Case("heroic-used-card-does-not-refresh-next-round", s =>
        {
            s.Start(Strategist, 0, "s0-0"); s.Do(0, new("finish-order")); s.S = s.S with { Cards = s.S.Cards! with { Phase = "cleanup", Ready0 = false, Ready1 = false } };
            s.Do(0, new("cleanup")); s.Do(1, new("cleanup")); Check(s.S.Cards!.HeroicUsed!.Contains(Card(Strategist, 0)) && !s.S.Cards.Hand0.Contains(Card(Strategist, 0)) && s.S.Cards.Deck.All(id => !HeroicCommands.IsHeroic(s.S, id)), "Heroic command was replenished");
        });
        Case("heroic-dead-or-hulk-owner-disables-all-uses", s =>
        {
            s.S = s.S with { Units = s.S.Units.Where(u => u.Id != "s0-0").ToList() }; s.Do(0, new("begin-order", "s0-2", "move", Card: Card(Strategist, 0)), false);
            s.S = s.S with { Cards = s.S.Cards! with { Phase = "initiative" } }; s.Do(0, new("initiative", Cards: [Card(Strategist, 0)]), false);
        });
        Case("heroic-private-views-hide-opponent-hand", s =>
        { var view = BasicCardRules.View(s.S, 0); Check(view.Cards!.Hand1.Length == 0 && view.Cards.HiddenHandCount == s.S.Cards!.Hand1.Length && view.Cards.Deck.Length == 0 && view.Cards.HeroicCards!.Length == 4, "Heroic registry leaked a private hand or hid public leaders"); });
        Case("heroic-save-validation-rejects-forged-registry-and-piles", s =>
        {
            var cards = s.S.Cards!; var pool = cards.HeroicCards!;
            s.Invalid(s.S with { Cards = cards with { HeroicCards = [.. pool, pool[0]] } }); s.Invalid(s.S with { Cards = cards with { HeroicCards = pool.Skip(1).ToArray() } });
            s.Invalid(s.S with { Cards = cards with { HeroicUsed = ["fake"] } }); s.Invalid(s.S with { Cards = cards with { HeroicUsed = [Card(Strategist, 0)] } });
            s.Invalid(s.S with { Cards = cards with { Hand0 = cards.Hand0.Except([Card(Strategist, 0)]).ToArray(), Discard = cards.Discard.Append(Card(Strategist, 0)).ToArray() } });
        });
        Case("heroic-staff-prerequisite-and-leader-exclusivity", s =>
        {
            Check(!FleetRuleTools.ValidLeader(s.C, s.Unit("s1-4") with { Leader = Engineer, AdditionalLeaders = null }), "Staff accepted without a leader");
            Check(FleetRuleTools.ValidLeader(s.C, s.Unit("s1-4")), "Staff and base leader rejected");
            var duplicated = s.Unit("s1-3") with { AdditionalLeaders = ["practice:taiidan-pilot"] };
            Check(!FleetRuleTools.ValidLeader(s.C, duplicated), "Hero accepted an extra ordinary leader");
        });
        Case("heroic-published-leader-tabs-costs-and-bonuses", s =>
        {
            var pilot = s.C.Leaders[Pilot]; var strategist = s.C.Leaders[Strategist]; var engineer = s.C.Leaders[Engineer];
            Check(pilot is { Stars: 3, Hand: 2, Play: 1, CloseDice: 3, Evasive: 1 } && strategist is { Stars: 5, Hand: 4, Play: 2, CoolUnderPressure: 1, DefensiveAura: 1 } && strategist.Tabs!.Length == 3 && engineer.Staff, "Published Heroic profile differs from supplied faces");
        });
        Case("heroic-defense-aura-self-adjacent-and-range", s =>
        {
            Check(FleetRuleTools.DefensiveAura(s.C, s.S, s.Unit("s0-1")) == 1 && FleetRuleTools.DefensiveAura(s.C, s.S, s.Unit("s0-2")) == 1, "Overlapping Arcs missed self or neighbor");
            var far = s.Unit("s0-2") with { Q = 4, R = 4 }; Check(FleetRuleTools.DefensiveAura(s.C, s.S, far) == 0, "Defense aura exceeded adjacency");
        });
        Case("heroic-evading-survives-real-focus-damage-until-new-order", s =>
        {
            // A modded corvette recipient isolates token lifetime; printed fighters cannot receive Focus Fire.
            var ship = s.C.Ships[s.Unit("s1-3").Definition]; s.C.Ships[ship.Id] = ship with { Class = "corvette" };
            s.C.Leaders[Pilot] = s.C.Leaders[Pilot] with { Classes = ["corvette"] };
            s.Start(Pilot, 1, "s1-3"); s.Do(1, new("finish-order"));
            s.S = s.S with { ActiveSide = 0, ValorOwner = 0, Units = s.S.Units.Select(u => u.Id == "s1-3" ? u with { Q = 5, R = 4 } : u).ToList() };
            string basic = s.S.Cards!.Hand0.First(id => !HeroicCommands.IsHeroic(s.S, id)); s.Do(0, new("attack", "s0-1", "s1-3", Card: basic));
            Check(s.S.Combat != null, "Missing post-roll boundary"); s.S = s.S with { Combat = s.S.Combat! with { Rolls = Enumerable.Repeat(1, s.S.Combat!.Rolls.Length).ToArray(), AbilityRerolls = 0, RerollSide = -1 } };
            s.Do(0, new("resolve")); s.Settle(); Check(s.Unit("s1-3").FocusFire > 0 && FleetRuleTools.Has(s.Unit("s1-3"), "hero-evasive"), "Focus Fire removed corrected Evading");
            s.S = s.S with { ActiveSide = 1, Units = s.S.Units.Select(u => u.Id == "s1-3" ? u with { Activated = false } : u).ToList() };
            s.Do(1, new("begin-order", "s1-3", "move", Card: Card(Engineer, 1))); Check(!FleetRuleTools.Has(s.Unit("s1-3"), "hero-evasive"), "New order retained Evading"); s.Do(1, new("finish-order"));
        });
        Case("heroic-engineer-repair-before-separate-activation-control", s =>
        {
            s.C.Ships[s.Unit("s1-4").Definition] = s.C.Ships[s.Unit("s1-4").Definition] with { Success = 2 };
            s.S = s.S with { ActiveSide = 1, Units = s.S.Units.Select(u => u.Id == "s1-4" ? u with { FocusFire = 3, Effects = ["engines"] } : u).ToList() };
            s.Do(1, new("special", "s1-4", Card: Card(Engineer, 1), CardRepair: "engines", Repair: "ask"));
            Check(!FleetRuleTools.Has(s.Unit("s1-4"), "engines") && s.S.Repairs is [{ Rolls.Length: 3 }], "Engineer effect replaced normal leader-assisted Damage Control");
            s.Do(1, new("repair-stop")); s.Do(1, new("finish-order"));
        });
        Case("heroic-staff-transfer-follows-owner-without-new-card", s =>
        {
            s.S = s.S with { ActiveSide = 1, Units = s.S.Units.Select(u => u.Id == "s1-5" ? u with { Leader = "practice:taiidan-orders" } : u).ToList() };
            string basic = s.S.Cards!.Hand1.First(id => !HeroicCommands.IsHeroic(s.S, id));
            s.Do(1, new("transfer-crew", "s1-4", "s1-5", Card: basic, TransferLeader: true, Leader: Engineer));
            Check(FleetRuleTools.LeaderIds(s.Unit("s1-5")).Contains(Engineer) && HeroicCommands.Pool(s.S.Cards!).Length == 4, "Hero transfer regenerated or lost card");
            s.Do(1, new("special", "s1-4", Card: Card(Engineer, 1)), false); s.S = s.S with { ActiveSide = 1 }; s.Do(1, new("special", "s1-5", Card: Card(Engineer, 1))); s.Do(1, new("finish-order"));
        });
        Case("heroic-staff-cannot-lose-required-primary-leader", s =>
        {
            s.S = s.S with { ActiveSide = 1 }; string basic = s.S.Cards!.Hand1.First(id => !HeroicCommands.IsHeroic(s.S, id));
            s.Do(1, new("transfer-crew", "s1-4", "s1-5", Card: basic, TransferLeader: true), false);
        });
        Case("heroic-command-can-pay-for-reserve-deployment", s =>
        {
            var reserve = s.Unit("s0-2") with { Q = -1, R = -1 };
            s.S = s.S with { Units = s.S.Units.Where(u => u.Id != reserve.Id).ToList(), Reserves = [reserve] };
            s.Do(0, new("begin-reinforce", reserve.Id, "deploy", Card: Card(Strategist, 0))); s.Do(0, new("deploy", Q: 0, R: 3)); s.Settle();
            Check(s.S.Reinforce == null && s.S.Cards!.HeroicUsed!.Contains(Card(Strategist, 0)) && s.S.Orders0 == 1, "Heroic Reinforce payment or continuation failed");
        });
        Case("heroic-close-bonus-and-defense-dice-are-functional", s =>
        {
            var ship = s.C.Ships[s.Unit("s1-3").Definition]; s.C.Ships[ship.Id] = ship with { Ram = 1, Weapons = [new("close", "Close weapon", 1, 1, Kind: "close")] };
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s1-3" ? u with { Q = 3, R = 4 } : u).ToList() };
            var pilot = s.Unit("s1-3"); var cmd = new BattleCommand("attack", pilot.Id, "s0-2", AttackKind: "ram");
            int withHero = FleetCombat.AttackDice(s.C, s.S, cmd); var plain = s.S with { Units = s.S.Units.Select(u => u.Id == pilot.Id ? u with { Leader = "" } : u).ToList() };
            Check(withHero == FleetCombat.AttackDice(s.C, plain, cmd) + 3, "Ace Pilot did not add close-attack dice");
            var shot = cmd with { AttackKind = "ranged", Weapon = "close" }; Check(FleetCombat.AttackDice(s.C, s.S, shot) == FleetCombat.AttackDice(s.C, plain, shot) + 3, "Ace Pilot missed the fighter close weapon");
            var target = s.Unit("s0-1"); var weapon = FleetDefense.Weapons(s.C, target).Single();
            s.S = s.S with { Defense = new(1, new("attack", "s1-3", target.Id), false, "fighter", 6) };
            int dice = FleetDefense.Dice(s.C, s.S, weapon); plain = s.S with { Units = s.S.Units.Select(u => u.Id == target.Id ? u with { Leader = "" } : u).ToList() };
            Check(dice == FleetDefense.Dice(s.C, plain, weapon) + 1, "Overlapping Arcs did not add defensive dice");
        });
        Case("heroic-unique-per-fleet-and-hulk-owner-rejection", s =>
        {
            var fleet = s.C.Fleets["practice:hero-kushan"]; s.C.Fleets[fleet.Id] = fleet with { Units = [fleet.Units[0], fleet.Units[0] with { Q = 2, R = 1 }] };
            bool rejected = false; try { s.E.Create(HeroicBattle.MapId); } catch (InvalidDataException) { rejected = true; } Check(rejected, "Duplicate Hero allowed at scenario start"); s.C.Fleets[fleet.Id] = fleet;
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s0-1" ? u with { CrewDamage = s.C.Ships[u.Definition].Armor + 1, Hulk = true } : u).ToList() };
            s.Do(0, new("begin-order", "s0-2", "move", Card: Card(Overseer, 0)), false);
        });
        Case("heroic-force-builder-requires-staff-primary", s =>
        {
            var plan = new ForcePlan("hero-test", "Hero test", "taiidan", 100, [new("u1", "practice:hero-frigate")], [new(Engineer, "u1")], CheckTabs: false);
            Check(!ForceBuilder.Validate(s.C, plan).Valid, "Fleet builder accepted solitary Staff");
            Check(ForceBuilder.Validate(s.C, plan with { Leaders = [new("practice:taiidan-officer", "u1"), new(Engineer, "u1")] }).Valid, "Fleet builder rejected compatible Staff");
        });
        Case("heroic-mod-bindings-and-shared-deck-rejected", s =>
        {
            void Reject(Action action) { bool bad = false; try { action(); } catch (InvalidDataException) { bad = true; } Check(bad, "Invalid Heroic content accepted"); }
            var hero = s.C.Cards["core:hero-best-of-the-best"]; s.C.Cards[hero.Id] = hero with { HeroicOwner = Engineer }; Reject(s.C.Validate); s.C.Cards[hero.Id] = hero;
            var deck = s.C.Decks["core:available-commands"]; s.C.Decks[deck.Id] = deck with { Entries = [.. deck.Entries, new(hero.Id)] }; Reject(s.C.Validate); s.C.Decks[deck.Id] = deck;
        });
        Console.WriteLine($"HEROIC_COMMANDS_OK {count} scenarios");
    }
    private static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
}
