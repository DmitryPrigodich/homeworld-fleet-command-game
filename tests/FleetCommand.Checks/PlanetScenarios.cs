using System.Text.Json;
using FleetCommand.Core;
internal static class PlanetScenarios
{
    private const string Leader = "core:hero-taiidan-battery-coordinator", Hero = Leader + "#hero-0", Ion = "ions/heavy-ion", Missiles = "missiles/ballistic";
    private sealed class Session
    {
        public ContentCatalog C; public BattleEngine E; public BattleState S;
        private readonly string _path; private readonly List<object> _trace = [];
        public Session(string root, string name)
        {
            C = ContentCatalog.Load([Path.Combine(root, "content", "base")]); BasicCardBattle.Register(C); E = new(C); S = E.Create(PlanetBattle.MapId, 3);
            foreach (string id in S.Units.Concat(S.Reserves ?? []).Select(u => u.Definition).Distinct()) C.Ships[id] = C.Ships[id] with { Success = 2, Armor = C.Ships[id].Class == "planet" ? 5 : 50, Evasive = 0, Weapons = C.Ships[id].Class == "planet" ? C.Ships[id].Weapons : [new("gun", "Planet test gun", 2, 6, PowerDice: 2)] };
            S = S with { ActiveSide = 1, ValorOwner = 0, Cards = S.Cards! with { Phase = "orders", UsedInterrupts0 = ["attack", "move", "counter"], UsedInterrupts1 = ["attack", "move", "counter"] } };
            _path = Path.Combine(root, "tmp", "test-results", name + ".trace.json");
        }
        public BattleUnit Unit(string id) => S.Units.Concat(S.Reserves ?? []).Single(u => u.Id == id);
        public void Do(int side, BattleCommand cmd, bool accepted = true)
        {
            string before = E.Save(S); var a = E.Apply(S, side, cmd); var b = E.Apply(E.Load(before), side, cmd);
            _trace.Add(new { side, cmd, a.Accepted, a.Message, a.State }); File.WriteAllText(_path, JsonSerializer.Serialize(_trace, JsonData.Options));
            Check(a.Accepted == accepted, a.Message); Check(E.Save(S) == before && E.Save(a.State) == E.Save(b.State), "Planet replay replay mutated input or diverged"); S = E.Load(E.Save(a.State));
            foreach (int sideView in new[] { 0, 1 }) { var view = E.Load(E.Save(BasicCardRules.View(S, sideView))); Check(view.RandomState == 1 && !E.Apply(view, sideView, cmd).Accepted, "Planet replay view gained authority"); }
        }
        public void EnemyMove()
        {
            string card = S.Cards!.Hand1.First(); Do(1, new("begin-order", "s1-2", "move", Card: card)); Do(1, new("move", "s1-2", Q: 4, R: 3));
            Check(S.Cards!.Response?.Command.Unit == "s1-2", "Enemy Move did not offer Heroic reaction");
        }
        public void Rolls(int value) { Check(S.Combat != null, "Expected held combat"); S = S with { Combat = S.Combat! with { Rolls = Enumerable.Repeat(value, S.Combat!.Rolls.Length).ToArray(), AbilityRerolls = 0, RerollSide = -1 } }; }
        public void AttackPlanet(int dice)
        {
            string id = Unit("s1-2").Definition; C.Ships[id] = C.Ships[id] with { Weapons = [new("gun", "Planet damage fixture", dice, 6)] };
            S = S with { ActiveSide = 1, ValorOwner = 1, Cards = HeroicCommands.Spend(S.Cards!, Hero) }; Do(1, new("begin-order", "s1-2", "attack", Card: S.Cards!.Hand1.First())); Do(1, new("attack", "s1-2", "s0-0", Weapon: "gun")); Rolls(6); Do(1, new("resolve"));
        }
        public void Pick(string facility) => Do(1, new("planet-facility", "s0-0", facility));
        public void Basic(string weapon)
        { S = S with { ActiveSide = 0 }; Do(0, new("begin-order", "s0-0", "attack", Card: S.Cards!.Hand0.First(id => !HeroicCommands.IsHeroic(S, id)), Weapon: weapon)); }
        public void Begin() => Do(0, new("heroic-interrupt", "s0-0", "s1-2", Card: Hero));
        public void Settle()
        {
            for (int n = 0; n < 100; n++)
                if (S.PlanetDamage is { } planet) Do(planet.Side, new("planet-facility", planet.Target, FleetPlanets.DamageChoices(C, S).First()));
                else if (S.Repairs is { Length: > 0 }) Do(S.Repairs[0].Side, new("repair-stop"));
                else if (S.Cards?.Counter is { } counter) Do(1 - counter.Side, new("decline-counter"));
                else if (S.Combat is { } combat) Do(FleetCombat.DecisionSide(combat), new(combat.AbilityRerolls > 0 ? "ability-reroll" : "resolve", Dice: []));
                else if (S.Defense is { } defense) Do(1 - defense.Side, new("decline-defense"));
                else if (S.CapEntry is { } cap) Do(cap.Side, new("decline-cap"));
                else if (S.Overrun is { } overrun) Do(overrun.Side, new("decline-overrun"));
                else if (S.Cards?.Response is { } pause) Do(InterruptRules.Responder(S), new(pause.Replan ? "stop-move" : "decline"));
                else return;
            throw new Exception("Planet replay continuation did not settle");
        }
        public void Give(int side, string id)
        {
            var cards = S.Cards!; var hand = side == 0 ? cards.Hand0 : cards.Hand1; if (hand.Contains(id)) return;
            string swap = hand.First(c => !HeroicCommands.IsHeroic(S, c)); var all = cards.Deck.Concat(cards.Hand0).Concat(cards.Hand1).Concat(cards.Discard).Where(c => c != id && c != swap);
            var a = side == 0 ? hand.Where(c => c != swap).Append(id).ToArray() : cards.Hand0.Where(c => c != id).ToArray();
            var b = side == 1 ? hand.Where(c => c != swap).Append(id).ToArray() : cards.Hand1.Where(c => c != id).ToArray();
            S = S with { Cards = cards with { Hand0 = a, Hand1 = b, Discard = [], Deck = all.Except(a).Except(b).Append(swap).ToArray() } };
        }
        public void Invalid(BattleState bad) { bool rejected = false; try { E.Load(E.Save(bad)); } catch (InvalidDataException) { rejected = true; } Check(rejected, "Forged planet/facility frame loaded"); }
    }
    public static void Run(string root)
    {
        int count = 0;
        void Case(string name, Action<Session> test) { try { test(new(root, name)); } catch (Exception e) { throw new Exception(name + ": " + e.Message, e); } count++; Console.WriteLine("PLANET PASS " + name); }
        Case("planet-published-battery-owner-and-facility-profiles", s =>
        {
            Check(s.C.Leaders[Leader] is { Stars: 3, Hand: 3, Play: 2, AttackDice: 1 } && s.Unit("s0-0").Leader == "" && s.Unit("s0-0").FacilityLeaders!["ions"] == Leader, "Coordinator is not assigned to its own facility");
            Check(FleetPlanets.Live(s.C, s.Unit("s0-0")).Length == 3 && FleetPlanets.Armor(s.C, s.Unit("s0-0")) == 2 && s.S.Cards!.HandLimit0 == 5, "Facilities or leader budget missing");
        });
        Case("planet-overwatch-move-power-target-and-no-activation", s =>
        {
            s.EnemyMove(); s.Begin(); Check(s.S.Cards!.Pending is { Step: 0, Weapon: Ion } && !s.Unit("s0-0").Activated && (s.Unit("s0-0").ActivatedFacilities ?? []).Length == 0 && s.S.Orders0 == 0, "Overwatch activated a facility or lost binding");
            s.Do(0, new("attack", "s0-0", "s1-2", Weapon: Missiles), false); s.Do(0, new("attack", "s0-0", "s1-3", Weapon: Ion), false); s.Do(0, new("attack", "s0-0", "s1-2", Weapon: Ion));
            Check(s.S.Combat is { Power: true, Weapon: Ion } && s.S.Combat.Rolls.Length == 7, "Bound Power Attack or facility-only leader bonus lost"); s.Settle();
            Check(s.S.Cards!.HeroicInterrupt == null && s.S.Orders0 == 0 && s.S.Orders1 == 1 && (s.Unit("s0-0").ActivatedFacilities ?? []).Length == 0, "Overwatch changed ordinary facility activation");
        });
        Case("planet-overwatch-interrupts-an-ordinary-attack-before-roll", s =>
        {
            s.Do(1, new("begin-order", "s1-2", "attack", Card: s.S.Cards!.Hand1.First())); s.Do(1, new("attack", "s1-2", "s0-0", Weapon: "gun")); Check(s.S.Combat == null && s.S.Cards!.Response?.Command.Kind == "attack", "Enemy attack rolled before Overwatch opportunity");
            s.Begin(); s.Do(0, new("attack", "s0-0", "s1-2", Weapon: Ion)); s.Settle(); Check(s.S.Cards!.Response == null && s.S.Cards.Pending == null && s.S.Cards.HeroicInterrupt == null && s.S.Orders1 == 1, "Enemy attack failed to resume after Overwatch");
        });
        Case("planet-overwatch-cannot-declare-another-facility", s =>
        { s.EnemyMove(); s.Do(0, new("heroic-interrupt", "s0-0", "s1-2", Card: Hero, Weapon: Missiles), false); Check(s.S.Cards!.Hand0.Contains(Hero), "Wrong facility declaration spent Heroic card"); });
        Case("planet-overwatch-cannot-start-with-crippled-bound-battery", s =>
        {
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s0-0" ? u with { FacilityEffects = new() { ["ions"] = ["weapons"] } } : u).ToList() }; string card = s.S.Cards!.Hand1.First(); s.Do(1, new("begin-order", "s1-2", "move", Card: card)); s.Do(1, new("move", "s1-2", Q: 4, R: 3));
            Check(s.S.Cards!.Response == null && s.S.Cards.Hand0.Contains(Hero), "Crippled Hero's battery offered an unusable public window");
        });
        Case("planet-facilities-activate-separately-for-one-card-each", s =>
        {
            s.Basic(Ion); Check(s.Unit("s0-0").ActiveFacility == "ions", "Selected facility not recorded"); s.Do(0, new("skip")); Check(!s.Unit("s0-0").Activated && s.Unit("s0-0").ActivatedFacilities!.SequenceEqual(["ions"]), "First activation disabled all facilities");
            s.Do(1, new("pass")); s.Do(0, new("begin-order", "s0-0", "attack", Card: s.S.Cards!.Hand0.First(id => !HeroicCommands.IsHeroic(s.S, id)), Weapon: Ion), false); s.Basic(Missiles); s.Do(0, new("skip")); Check(s.Unit("s0-0").Activated && s.Unit("s0-0").ActivatedFacilities!.Length == 2 && s.S.Orders0 == 2, "Second weapon did not get independent activation");
        });
        Case("planet-activation-includes-free-damage-control", s =>
        {
            s.S = s.S with { ActiveSide = 0, Units = s.S.Units.Select(u => u.Id == "s0-0" ? u with { FocusFire = 2 } : u).ToList() };
            s.Do(0, new("begin-order", "s0-0", "attack", Card: s.S.Cards!.Hand0.First(), Weapon: Ion, Repair: "focus")); Check(s.Unit("s0-0").FocusFire == 1 && s.S.RandomState != 3, "Facility activation missed free control"); s.Do(0, new("finish-order"));
        });
        Case("planet-activation-repairs-only-selected-facility", s =>
        {
            s.S = s.S with { ActiveSide = 0, Units = s.S.Units.Select(u => u.Id == "s0-0" ? u with { FacilityEffects = new() { ["ions"] = ["weapons"], ["missiles"] = ["weapons"] } } : u).ToList() };
            s.Do(0, new("begin-order", "s0-0", "attack", Card: s.S.Cards!.Hand0.First(), Weapon: Ion, Repair: "weapons")); Check(!FleetPlanets.Crippled(s.Unit("s0-0"), "ions") && FleetPlanets.Crippled(s.Unit("s0-0"), "missiles"), "Control repaired the wrong facility or all facilities"); s.Do(0, new("finish-order"));
        });
        Case("planet-hardened-first-and-attacker-chooses-destruction", s =>
        {
            s.AttackPlanet(9); Check(s.S.PlanetDamage is { Remaining: 2 } && FleetPlanets.DamageChoices(s.C, s.S).SequenceEqual(["hardened"]), "Hardened priority or armor reduction changed");
            s.Do(1, new("planet-facility", "s0-0", "ions"), false); s.Do(0, new("planet-facility", "s0-0", "hardened"), false); s.Pick("hardened"); Check(FleetPlanets.DamageChoices(s.C, s.S).Order().SequenceEqual(new[] { "ions", "missiles" }), "Attacker lost choice after hardened defence"); s.Pick("ions");
            Check(s.S.PlanetDamage == null && s.Unit("s0-0").Models == 1 && !FleetRuleTools.LeaderIds(s.Unit("s0-0")).Contains(Leader) && !HeroicCommands.CanUse(s.C, s.S, 0, Hero), "Destroyed facility retained its Hero or removed whole planet");
        });
        Case("planet-last-facility-absorbs-hit-without-destroying-body", s =>
        { s.AttackPlanet(10); s.Pick("hardened"); s.Pick("ions"); s.Pick("missiles"); Check(s.S.PlanetDamage == null && s.S.Units.Any(u => u.Id == "s0-0") && FleetPlanets.Live(s.C, s.Unit("s0-0")).Length == 0, "Last facility hit also destroyed military body"); });
        Case("planet-surplus-hit-destroys-military-but-keeps-physical-token", s =>
        {
            s.AttackPlanet(11); s.Pick("hardened"); s.Pick("ions"); s.Pick("missiles"); Check(!s.S.Units.Any(u => u.Id == "s0-0") && s.S.PlanetDamage == null && s.S.Stars1 == 15 && s.C.Maps[s.S.Map].Objects.Any(o => o.Definition == "core:planet" && o.Q == 3 && o.R == 3), "Planet destruction lost token or counted value incorrectly");
            Check(!FleetFootprint.CanPlace(s.C, s.S, s.Unit("s1-2"), new(3, 3), 3), "Destroyed military planet no longer blocks movement");
        });
        Case("planet-leader-bonus-does-not-leak-to-another-facility", s =>
        {
            s.S = s.S with { ActiveSide = 0 }; Check(FleetCombat.AttackDice(s.C, s.S, new("attack", "s0-0", "s1-2", Weapon: Ion)) == 7 && FleetCombat.AttackDice(s.C, s.S, new("attack", "s0-0", "s1-2", Weapon: Missiles)) == 6, "Coordinator boosted another facility");
            Check(!FleetWeapons.Attack(s.C, s.S, s.Unit("s0-0"), s.Unit("s1-2"), "defense").Allowed, "Gravity-bound Defence Batteries used for a normal attack");
        });
        Case("planet-token-is-targetable-but-blocks-lines-behind-it", s =>
        {
            Check(FleetWeapons.Attack(s.C, s.S, s.Unit("s1-2"), s.Unit("s0-0"), "gun").Allowed, "Planet's own token blocked attacks against it");
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s0-1" ? u with { Q = 1, R = 3 } : u).ToList() }; Check(!FleetWeapons.Attack(s.C, s.S, s.Unit("s1-2"), s.Unit("s0-1"), "gun").Allowed, "Planet failed to block attack behind it");
        });
        Case("planet-cannot-move-or-board-and-all-damage-is-facility-aware", s =>
        { s.S = s.S with { ActiveSide = 0 }; s.Do(0, new("begin-order", "s0-0", "move", Card: s.S.Cards!.Hand0.First()), false); Check(s.E.GetActions(s.S, 0, "s0-0").MoveCells.Length == 0, "Planet received a movement destination"); Check(!FleetClose.Allowed(s.C, s.S, s.Unit("s1-2") with { Q = 4, R = 3 }, s.Unit("s0-0"), "board"), "Planet accepted ordinary boarding"); });
        Case("planet-overwatch-trigger-includes-lightning-and-attack-not-arrivals", s =>
        {
            Check(InterruptRules.Eligible(s.C, s.S, Hero, new(1, new("attack", "s1-2", "s0-1"), true, [])) && !InterruptRules.Eligible(s.C, s.S, Hero, new(1, new("reinforce", "s1-2"), true, [])), "Overwatch trigger does not match printed icons");
            var card = s.C.Cards["core:hero-planetary-overwatch"]; Check(card.InterruptTriggers!.SequenceEqual(["move", "lightning", "attack"]), "Overwatch lost Lightning trigger");
        });
        Case("planet-overwatch-countered-without-activating-facility", s =>
        {
            s.Give(1, "core:interrupt-counterintelligence#1"); s.S = s.S with { Cards = s.S.Cards! with { UsedInterrupts1 = ["attack", "move"] } }; s.EnemyMove(); s.Begin(); s.Do(1, new("counter", Card: "core:interrupt-counterintelligence#1")); s.Settle();
            Check(s.S.Cards!.HeroicUsed!.Contains(Hero) && !s.S.Cards.Discard.Contains(Hero) && (s.Unit("s0-0").ActivatedFacilities ?? []).Length == 0, "Counter activated or recycled Overwatch");
        });
        Case("planet-overwatch-works-with-already-used-bound-facility", s =>
        {
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s0-0" ? u with { Activated = true, ActivatedFacilities = ["ions", "missiles"] } : u).ToList() }; s.EnemyMove(); s.Begin(); s.Do(0, new("attack", "s0-0", "s1-2", Weapon: Ion)); s.Settle();
            Check(s.Unit("s0-0").ActivatedFacilities!.Length == 2 && s.S.Orders0 == 0, "Overwatch refreshed or spent ordinary facility activation");
        });
        Case("planet-facility-activations-refresh-next-round", s =>
        {
            s.Basic(Ion); s.Do(0, new("skip")); s.Do(1, new("pass")); s.Basic(Missiles); s.Do(0, new("skip")); s.Do(0, new("pass"));
            Check(s.S.Round == 2 && (s.Unit("s0-0").ActivatedFacilities ?? []).Length == 0 && !s.Unit("s0-0").Activated, "Facility activation tokens did not refresh");
        });
        Case("planet-force-builder-binds-leader-and-creates-physical-token", s =>
        {
            var force = new ForcePlan("planet-force", "Planet fleet", "taiidan", 20, [new("world", "practice:industrial-planet", Row: 3)], [new(Leader, "world", "ions")], CheckTabs: false);
            Check(ForceBuilder.Validate(s.C, force).Valid && ForceBuilder.Validate(s.C, force).Spent == 15, "Force builder missed facility cost or leader binding");
            Check(!ForceBuilder.Validate(s.C, force with { Leaders = [new(Leader, "world")] }).Valid, "Planet leader accepted without a facility");
            string map = ForceBattle.Register(s.C, new([force, ForceBattle.PracticeForce("kushan")])); var state = s.E.Create(map, 3); s.E.Load(s.E.Save(state));
            Check(s.C.Maps[map].Objects.Any(o => o.Definition == "core:planet" && o.Q == 0 && o.R == 3) && state.Units.Single(u => u.Side == 0).FacilityLeaders!["ions"] == Leader, "Prepared planet lost its token or facility leader");
        });
        Case("planet-nested-overwatch-destroys-original-firing-facility", s =>
        {
            var fleet = s.C.Fleets["practice:planet-taiidan"]; s.C.Fleets["practice:planet-mirror"] = fleet with { Id = "practice:planet-mirror", Units = fleet.Units.Select((u, i) => u with { Q = i == 0 ? 6 : 8, Facing = 3 }).ToArray() };
            s.C.Maps[s.S.Map] = s.C.Maps[s.S.Map] with { Fleets = ["practice:planet-taiidan", "practice:planet-mirror"], Objects = [new("core:planet", 3, 3), new("core:planet", 6, 3)] }; s.S = s.E.Create(s.S.Map, 3);
            s.S = s.S with { ActiveSide = 1, ValorOwner = 1, Cards = s.S.Cards! with { Phase = "orders", UsedInterrupts0 = ["attack", "move", "counter"], UsedInterrupts1 = ["attack", "move", "counter"] } };
            s.Do(1, new("begin-order", "s1-2", "attack", Card: s.S.Cards!.Hand1.First(id => !HeroicCommands.IsHeroic(s.S, id)), Weapon: Ion)); s.Do(1, new("attack", "s1-2", "s0-0", Weapon: Ion)); s.Begin();
            s.Do(0, new("attack", "s0-0", "s1-2", Weapon: Ion)); s.Do(1, new("heroic-interrupt", "s1-2", "s0-0", Card: Leader + "#hero-1")); s.Do(1, new("attack", "s1-2", "s0-0", Weapon: Ion)); s.Rolls(6); s.Do(1, new("resolve")); s.Pick("hardened"); s.Pick("ions");
            Check(s.S.Cards!.HeroicInterrupt == null && !FleetPlanets.Live(s.C, s.Unit("s0-0")).Any(f => f.Id == "ions"), "Lost original facility stranded a Heroic reaction"); s.Settle();
        });
        Case("planet-direct-basic-activation-repairs-before-attack-response", s =>
        {
            s.S = s.S with { ActiveSide = 0, Units = s.S.Units.Select(u => u.Id == "s0-0" ? u with { FacilityEffects = new() { ["ions"] = ["weapons"] }, FocusFire = 1 } : u).ToList() };
            s.Do(0, new("attack", "s0-0", "s1-2", Weapon: Missiles, Card: s.S.Cards!.Hand0.First(id => !HeroicCommands.IsHeroic(s.S, id)), Repair: "ask"));
            Check(s.S.Repairs is { Length: 1 } && s.S.Combat == null, "Direct activation did not pause before reaction");
            s.Do(0, new("repair-choice", Repair: "focus")); s.Settle(); Check(s.Unit("s0-0").FocusFire == 0 && s.S.Orders0 == 1, "Direct repair failed to resume the paid attack");
        });
        Case("planet-overwatch-responds-to-lightning-step", s =>
        {
            s.Give(1, "core:intercept#1"); s.Do(1, new("special", "s1-2", Card: "core:intercept#1")); s.Do(1, new("move", "s1-2", Q: 5, R: 3));
            // The first Move and its Overwatch window are skipped deliberately to reach Lightning.
            if (s.S.Cards!.Response != null) s.Do(0, new("decline"));
            Check(SpecialOrders.Action(s.C, s.S, s.S.Cards!.Pending!) == "lightning", "Intercept did not reach Lightning");
            s.Do(1, new("move", "s1-2", Q: 4, R: 3)); Check(s.S.Cards!.Response != null, "Lightning did not offer Overwatch"); s.Begin(); s.Settle();
        });
        Case("planet-missile-facility-fires-through-defensive-window", s =>
        {
            string id = s.Unit("s1-2").Definition; s.C.Ships[id] = s.C.Ships[id] with { Weapons = [new("guard", "Missile defense fixture", 2, 6, DefensiveFire: true)] };
            s.Basic(Missiles); s.S = s.S with { ValorOwner = 1 }; s.Do(0, new("attack", "s0-0", "s1-2", Weapon: Missiles)); Check(s.S.Defense is { Threat: "missile", IncomingDice: 6 }, "Facility missile did not offer projectile defense");
            s.Do(1, new("defend", Weapon: "guard")); s.Rolls(6); s.Do(1, new("resolve")); Check(s.S.Combat is { Weapon: Missiles } && s.S.Combat.Rolls.Length == 4, "Projectile defense did not reduce facility pool"); s.Settle();
        });
        Case("planet-ram-removes-facilities-and-rolls-return-collision", s =>
        {
            string id = s.Unit("s1-2").Definition; s.C.Ships[id] = s.C.Ships[id] with { Ram = 9 };
            s.S = s.S with { ValorOwner = 1, Units = s.S.Units.Select(u => u.Id == "s1-2" ? u with { Q = 4, R = 3, Facing = 3 } : u).ToList(), Cards = HeroicCommands.Spend(s.S.Cards!, Hero) };
            s.Do(1, new("begin-order", "s1-2", "attack", Card: s.S.Cards!.Hand1.First())); s.Do(1, new("attack", "s1-2", "s0-0", AttackKind: "ram"));
            Check(s.S.Defense?.Attack.Target == "s0-0", "Planet did not offer its base defensive batteries"); s.Do(0, new("decline-defense")); s.Rolls(6); s.Do(1, new("resolve"));
            s.Pick("hardened"); s.S = s.S with { ValorOwner = 0 }; s.Pick("ions"); Check(s.S.Combat is { Kind: "collision" } && s.S.Combat.Rolls.Length == 14, "Return collision lost the planet's armor at impact"); s.Settle();
        });
        Case("planet-last-body-ram-still-resolves-return-collision", s =>
        {
            string id = s.Unit("s1-2").Definition; s.C.Ships[id] = s.C.Ships[id] with { Ram = 11 };
            s.S = s.S with { ValorOwner = 1, Units = s.S.Units.Where(u => u.Id is "s0-0" or "s1-2").Select(u => u.Id == "s1-2" ? u with { Q = 4, R = 3, Facing = 3 } : u).ToList(), Cards = HeroicCommands.Spend(s.S.Cards!, Hero) };
            s.Do(1, new("begin-order", "s1-2", "attack", Card: s.S.Cards!.Hand1.First())); s.Do(1, new("attack", "s1-2", "s0-0", AttackKind: "ram")); s.Do(0, new("decline-defense")); s.Rolls(6); s.Do(1, new("resolve"));
            s.Pick("hardened"); s.Pick("ions"); s.S = s.S with { ValorOwner = 0 }; s.Pick("missiles");
            Check(!s.S.Finished && s.S.Combat is { Kind: "collision", Ghost.Id: "s0-0" } && !s.S.Units.Any(u => u.Id == "s0-0"), "Elimination skipped the destroyed planet's return collision"); s.Settle(); Check(s.S.Finished && s.S.Winner == 1, "Return collision failed to finish the battle");
        });
        Case("planet-saved-state-rejects-forged-facilities-and-leaders", s =>
        {
            s.Invalid(s.S with { Units = s.S.Units.Select(u => u.Id == "s0-0" ? u with { DestroyedFacilities = ["bogus"] } : u).ToList() });
            s.Invalid(s.S with { Units = s.S.Units.Select(u => u.Id == "s0-0" ? u with { DestroyedFacilities = ["ions"] } : u).ToList() });
            s.Invalid(s.S with { Units = s.S.Units.Select(u => u.Id == "s0-1" ? u with { ActivatedFacilities = ["ions"] } : u).ToList() });
            s.AttackPlanet(9); s.Invalid(s.S with { PlanetDamage = s.S.PlanetDamage! with { Remaining = 99 } }); s.Settle();
        });
        Console.WriteLine($"PLANETARY_OVERWATCH_OK {count} scenarios");
    }
    private static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
}
