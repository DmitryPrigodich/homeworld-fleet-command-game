using FleetCommand.Core;

internal static class TabletopScenarios
{
    private sealed class Session
    {
        public readonly ContentCatalog C;
        public readonly BattleEngine E;
        public BattleState S;
        public Session(string root)
        {
            C = ContentCatalog.Load([Path.Combine(root, "content", "base")]); BasicCardBattle.Register(C); E = new(C);
            C.Maps[BasicCardBattle.TabletopMapId] = C.Maps[BasicCardBattle.TabletopMapId] with { Objects = [] };
            S = E.Create(BasicCardBattle.TabletopMapId, 17);
            string[] a = ["order-01", "order-09"], b = ["order-21", "order-22"];
            S = S with { ActiveSide = 0, ValorOwner = 0, Cards = new(Enumerable.Range(1, 24).Select(i => $"order-{i:00}").Except(a).Except(b).ToArray(), a, b, [], Phase: "orders"),
                Units = S.Units.Select(u => u.Id == "s0-0" ? u with { Q = 2, R = 2 } : u.Id == "s1-3" ? u with { Q = 3, R = 2 } : u).ToList() };
            C.Ships["core:taiidan-interceptor"] = C.Ships["core:taiidan-interceptor"] with { Armor = 50 };
        }
        public void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        public void Do(int side, BattleCommand command, bool accepted = true)
        {
            var before = E.Save(S); var a = E.Apply(S, side, command); var b = E.Apply(E.Load(before), side, command);
            Check(a.Accepted == accepted, "Tabletop command: " + a.Message);
            Check(E.Save(S) == before && E.Save(a.State) == E.Save(b.State), "Tabletop state mutated or diverged after resume");
            S = E.Load(E.Save(a.State));
            foreach (int viewer in new[] { 0, 1 })
            {
                var view = E.Load(E.Save(BasicCardRules.View(S, viewer)));
                Check(view.RandomState == 1 && view.Cards!.Deck.Length == 0 && (viewer == 0 ? view.Cards.Hand1 : view.Cards.Hand0).Length == 0, "Tabletop view leaked secrets");
                Check(!E.Apply(view, viewer, command).Accepted, "Client view executed tabletop command");
            }
        }
        public void Abilities()
        { while (S.Combat is { AbilityRerolls: > 0 } a) Do(FleetCombat.DecisionSide(a), new("ability-reroll", Dice: FleetCombat.EligibleDice(C, S).Take(a.AbilityRerolls).ToArray())); }
        public void Attack(bool abilities = true)
        {
            Do(0, new("attack", "s0-0", "s1-3", Card: "order-01"));
            if (S.Cards!.Response != null && S.Combat == null) Do(1, new("decline"));
            if (abilities) Abilities();
            Check(S.Combat != null, "Post-roll decision was not opened");
        }
    }
    public static void Run(string root)
    {
        void Case(string name, Action<Session> test) { test(new(root)); Console.WriteLine("TABLETOP SCENARIO PASS " + name); }
        Case("leader-budgets-and-hidden-multiple-bids", s =>
        {
            s.C.Leaders["core:test-kushan"] = new("core:test-kushan", "Test", "kushan", ["fighter"], Hand: 8, Play: 2);
            s.C.Leaders["core:test-taiidan"] = new("core:test-taiidan", "Test", "taiidan", ["fighter"], Hand: 8, Play: 2);
            var map = s.C.Maps[BasicCardBattle.TabletopMapId];
            foreach (string fleet in map.Fleets)
            { var f = s.C.Fleets[fleet]; s.C.Fleets[fleet] = f with { Units = f.Units.Select((u, i) => i == 0 ? u with { Leader = fleet.Contains("kushan") ? "core:test-kushan" : "core:test-taiidan" } : u).ToArray() }; }
            s.S = s.E.Create(map.Id, 17); s.Check(s.S.Cards is { HandLimit0: 10, HandLimit1: 10, PlayLimit0: 3, PlayLimit1: 3 }, "Leader budgets were not counted");
            var bids0 = s.S.Cards!.Hand0.Take(6).ToArray(); var bids1 = s.S.Cards.Hand1.Take(6).ToArray();
            s.Do(0, new("initiative", Cards: bids0));
            s.Check(BasicCardRules.View(s.S, 1).Cards!.Bids0 == null, "Multi-card bid leaked");
            s.Do(1, new("initiative", Cards: bids1));
            s.Check(s.S.Cards is { Phase: "initiative", Ready0: false, Ready1: false, Discard.Length: 12 } && s.S.Cards.Hand0.Length == 4, "Capped tie did not spend bids and reset commitments");
            s.Do(0, new("initiative", Cards: [bids0[0]]), false);
        });
        Case("cleanup-discard-retain-and-new-budgets", s =>
        {
            s.Do(0, new("pass")); s.Do(1, new("pass"));
            s.Check(s.S.Cards!.Phase == "cleanup", "Cleanup was skipped");
            string retained = s.S.Cards.Hand0[1], discarded = s.S.Cards.Hand0[0];
            s.Do(0, new("cleanup", Cards: [discarded])); s.Do(0, new("cleanup"), false);
            s.Do(1, new("cleanup"));
            s.Check(s.S.Cards is { Phase: "initiative", Hand0.Length: 2 } && s.S.Cards.Hand0.Contains(retained) && s.S.Cards.Discard.Contains(discarded), "Cleanup failed to retain/draw cards");
        });
        Case("recycle-only-after-last-draw-card", s =>
        {
            s.S = s.S with { Cards = new(["order-01"], [], [], Enumerable.Range(2, 23).Select(i => $"order-{i:00}").ToArray(), Phase: "cleanup") };
            s.Do(0, new("cleanup")); s.Do(1, new("cleanup"));
            s.Check(s.S.Cards!.Hand0[0] == "order-01" && s.S.Cards.Hand0.Length == 2 && s.S.Cards.Hand1.Length == 2, "Discard was shuffled into a non-empty deck");
        });
        Case("capital-turning-and-strike-freedom", s =>
        {
            var unit = s.S.Units.Single(u => u.Id == "s0-0") with { Q = 4, R = 3, Facing = 0 };
            s.S = s.S with { Units = [unit] };
            var ship = s.C.Ships[unit.Definition]; s.C.Ships[ship.Id] = ship with { Class = "capital", Move = 1, Abilities = [] };
            var moves = FleetMovement.Destinations(s.C, s.S, unit);
            s.Check(moves.Length == 3 && !moves.Contains(new Hex(4, 3).Neighbor(3)), "Capital turned too far for one move");
            s.Check(FleetMovement.Path(s.C, s.S, unit, new(4, 3), 1) is { Length: 1 } && FleetMovement.Path(s.C, s.S, unit, new(4, 3), 3) == null, "Capital turn-in-place cost is wrong");
            s.C.Ships[ship.Id] = ship with { Class = "fighter", Move = 1, Abilities = [] };
            s.Check(FleetMovement.Destinations(s.C, s.S, unit).Length == 6, "Strike craft could not turn freely before moving");
        });
        Case("gravity-well-exit-cost", s =>
        {
            var unit = s.S.Units[0] with { Q = 5, R = 3 };
            s.S = s.S with { Units = [unit] };
            s.C.Ships[unit.Definition] = s.C.Ships[unit.Definition] with { Move = 1, Abilities = [] };
            s.C.Maps[s.S.Map] = s.C.Maps[s.S.Map] with { Objects = [new("core:planet", 6, 3)] };
            var moves = FleetMovement.Destinations(s.C, s.S, unit);
            s.Check(!moves.Contains(new(4, 3)) && moves.Contains(new(5, 4)), "Gravity costs applied on entry rather than exit");
        });
        Case("turret-and-spinal-arcs", s =>
        {
            var unit = s.S.Units[0] with { Q = 3, R = 3, Facing = 0 }; var target = s.S.Units[3];
            for (int d = 0; d < 6; d++)
            { var h = new Hex(3, 3).Neighbor(d); var t = target with { Q = h.Q, R = h.R }; s.Check(FleetWeapons.Arc(unit, t, "spinal") == (d == 0), "Spinal adjacent arc wrong"); s.Check(FleetWeapons.Arc(unit, t, "turret") == (d is 0 or 1 or 5), "Turret fires behind ship"); }
            s.Check(FleetWeapons.Arc(unit, target with { Q = 4, R = 4 }, "spinal"), "Spinal widening cone is missing");
        });
        Case("weapon-choice-and-target-classes", s =>
        {
            var ship = s.C.Ships["core:kushan-interceptor"];
            s.C.Ships[ship.Id] = ship with { Weapons = [new("slow", "Slow", 4, 4, Targets: ["frigate"]), new("fast", "Fast", 1, 1, Targets: ["fighter"])] };
            var u = s.S.Units[0]; var t = s.S.Units[3];
            s.Check(!FleetWeapons.Attack(s.C, s.S, u, t, "slow").Allowed && FleetWeapons.Attack(s.C, s.S, u, t, "fast").Allowed, "Weapon target restriction ignored");
            s.Do(0, new("attack", u.Id, t.Id, Card: "order-01", Weapon: "slow"), false);
            s.Check(s.S.Orders0 == 0 && s.S.Cards!.Discard.Length == 0, "Invalid weapon spent an order");
        });
        Case("post-roll-valor-resume-and-faction-exclusion", s =>
        {
            s.Attack(); s.Do(1, new("resolve"), false); var before = s.S.Combat!.Rolls.ToArray();
            s.Do(0, new("valor")); s.Check(s.S.ValorOwner == 1 && s.S.ValorUsedMask == 1 && s.S.Combat!.Rerolled, "Valor ownership was not passed");
            s.Do(0, new("valor"), false); s.Do(0, new("faction-reroll"), false); s.Abilities(); s.Do(0, new("resolve"));
            s.Check(s.S.Combat == null && s.S.Cards!.Response == null && s.S.Orders0 == 1, "Combat did not resume its paid order");
        });
        Case("rapid-evasive-cancellation", s =>
        {
            var ship = s.C.Ships["core:kushan-interceptor"]; var target = s.C.Ships["core:taiidan-interceptor"];
            s.C.Ships[ship.Id] = ship with { Weapons = [new("main", "Main", 1, 3, RapidFire: 2)] };
            s.C.Ships[target.Id] = target with { Evasive = 1 }; s.S = s.S with { RandomState = 1 };
            uint expected = 1; int[] rolls = Enumerable.Range(0, 6).Select(_ => FleetRuleTools.Roll(ref expected)).ToArray();
            int failure = Array.FindIndex(rolls, d => d < ship.Success); if (failure >= 0) rolls[failure] = FleetRuleTools.Roll(ref expected);
            s.Attack(abilities: false); s.Do(0, new("ability-reroll", Dice: failure < 0 ? [] : new[] { failure }));
            s.Check(s.S.Combat!.Rolls.SequenceEqual(rolls) && s.S.RandomState == expected, "Rapid Fire and Evasive did not cancel first");
        });
        Case("armor-prow-focus-and-buff-expiry", s =>
        {
            var target = s.C.Ships["core:taiidan-interceptor"]; s.C.Ships[target.Id] = target with { Class = "frigate", Armor = 2, ProwArmor = true };
            var attacker = s.C.Ships["core:kushan-interceptor"]; s.C.Ships[attacker.Id] = attacker with { Weapons = [new("main", "Main", 1, 3, Penetrating: 1)] };
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s1-3" ? u with { Facing = 3, FocusFire = 2, Effects = ["brace"] } : u).ToList() };
            s.Attack(); s.S = s.S with { Combat = s.S.Combat! with { Rolls = [6, 6, 6, 6, 1, 2] } }; s.Do(0, new("resolve"));
            var survivor = s.S.Units.Single(u => u.Id == "s1-3");
            s.Check(survivor.Models == 3 && survivor.FocusFire == 0 && !FleetRuleTools.Has(survivor, "brace"), "Prow/brace/focus/penetration order is wrong");
        });
        Case("damage-control-before-action-and-faction-repair", s =>
        {
            var ship = s.C.Ships["core:kushan-interceptor"]; s.C.Ships[ship.Id] = ship with { Success = 2 };
            s.S = s.S with { RandomState = 1, Units = s.S.Units.Select(u => u.Id == "s0-0" ? u with { Effects = ["brace", "engines"], FocusFire = 2 } : u).ToList() };
            s.Do(0, new("begin-order", "s0-0", "move", Card: "order-01", Repair: "engines"));
            var u = s.S.Units.Single(u => u.Id == "s0-0");
            s.Check(u.Activated && !FleetRuleTools.Has(u, "engines") && !FleetRuleTools.Has(u, "brace") && s.S.Cards!.Pending!.BasicAction == "move", "Activation repair/new-order buff expiry failed");
            s.Do(0, new("skip")); s.Do(1, new("pass")); s.Do(0, new("cleanup")); s.Do(1, new("cleanup"));
            // Separate fixture: faction repair can affect an activated unit and costs no card/Play.
            s.S = s.S with { ActiveSide = 0, Cards = s.S.Cards! with { Phase = "orders" } };
            int count = s.S.Cards.Hand0.Length;
            s.Do(0, new("faction", "s0-0", Repair: "focus")); s.Check(s.S.FactionUsed0 && s.S.Orders0 == 0 && s.S.Cards!.Hand0.Length == count && s.S.Units.Single(u => u.Id == "s0-0").FocusFire == 0, "Faction repair spent an order or failed to remove the whole -2 token");
            s.Do(0, new("faction", "s0-0", Repair: "focus"), false);
        });
        Case("optional-card-design-is-independent", s =>
        {
            string folder = Path.Combine(root, "tmp", "test-results", "card-style-module"); Directory.CreateDirectory(folder);
            string before = s.E.Save(s.S), fingerprint = s.C.Fingerprint;
            File.WriteAllText(Path.Combine(folder, "style.json"), "{\"id\":\"test-style\",\"name\":\"Test cosmetic pack\"}");
            var pack = CardStyleCatalog.Load(folder); s.Check(pack.Definition.Id == "test-style" && s.C.Fingerprint == fingerprint && s.E.Save(s.E.Load(before)) == before, "Artwork affected match content/saves");
            File.WriteAllText(Path.Combine(folder, "style.json"), "{\"id\":\"test-style\",\"name\":\"Invalid\",\"orderFrame\":\"../../outside.png\"}");
            try { CardStyleCatalog.Load(folder); } catch (InvalidDataException) { return; }
            throw new Exception("Card art escaped its pack directory");
        });
        Case("post-roll-repair-decisions-for-two-actors", s =>
        {
            var ship = s.C.Ships["core:kushan-interceptor"]; s.C.Ships[ship.Id] = ship with { Success = 2 };
            s.S = s.S with { RandomState = 1, Units = s.S.Units.Select(u => u.Side == 0 && u.Id is "s0-0" or "s0-1" ? u with { FocusFire = 2, Effects = ["weapons"] } : u).ToList() };
            s.Do(0, new("special", "s0-0", Card: "order-09", OtherUnit: "s0-1", Repair: "ask", OtherRepair: "ask"));
            s.Check(s.S.Repairs is { Length: 2 }, "One activation repair overwrote the other actor's decision");
            s.Do(1, new("repair-choice", Repair: "weapons"), false); s.Do(0, new("skip"), false);
            s.Do(0, new("repair-choice", Repair: "weapons")); s.Check(s.S.Repairs is { Length: 1 } && s.S.Repairs[0].Unit == "s0-1", "Repair queue did not advance");
            s.Do(0, new("repair-choice", Repair: "focus"));
            s.Check(s.S.Repairs == null && s.S.Cards!.Pending is { Step: 0 } && s.S.Units.Single(u => u.Id == "s0-1").FocusFire == 1, "Repair choice consumed an action or failed to apply");
        });
        Case("crippled-attack-becomes-repair", s =>
        {
            var ship = s.C.Ships["core:kushan-interceptor"]; s.C.Ships[ship.Id] = ship with { Success = 2 };
            s.S = s.S with { RandomState = 1, Units = s.S.Units.Select(u => u.Id == "s0-0" ? u with { Effects = ["weapons"] } : u).ToList() };
            s.Do(0, new("begin-order", "s0-0", "attack", Card: "order-01")); s.Do(0, new("damage-control", Repair: "ask"));
            s.Check(s.S.Repairs is { Length: 1 } && s.S.Cards!.Pending != null, "Attack ended before choosing its repair");
            s.Do(0, new("repair-choice", Repair: "weapons"));
            s.Check(s.S.Repairs == null && s.S.Cards!.Pending == null && s.S.Orders0 == 1 && !FleetRuleTools.Has(s.S.Units.Single(u => u.Id == "s0-0"), "weapons"), "Converted attack failed to finish after repair");
        });
        Case("formation-can-assign-only-one-line", s =>
        {
            s.Do(0, new("special", Card: "order-09", OtherUnit: "s0-1"));
            s.Check(s.S.Cards!.Pending is { Step: 2 } && !s.S.Units.Single(u => u.Id == "s0-0").Activated && s.S.Units.Single(u => u.Id == "s0-1").Activated, "Omitted first line activated an unrelated unit");
            s.Check(s.E.GetActions(s.S, 0, "s0-1").MoveCells.Length == 0, "Lightning move used an unassigned adjacency anchor");
            s.Do(0, new("skip")); s.Do(0, new("skip"));
            s.Check(s.S.Cards!.Pending == null && s.S.Cards.Discard.Length == 1 && s.S.Orders0 == 1, "A partial order spent more than one card/Play");
        });
        Case("defender-chooses-evasive-die", s =>
        {
            var ship = s.C.Ships["core:kushan-interceptor"]; s.C.Ships[ship.Id] = ship with { Weapons = [new("main", "Main", 1, 3)] };
            s.C.Ships["core:taiidan-interceptor"] = s.C.Ships["core:taiidan-interceptor"] with { Evasive = 1 }; s.S = s.S with { RandomState = 1 };
            s.Attack(abilities: false); s.Check(s.S.Combat is { AbilityRerolls: 1, RerollSide: 1 }, "Evasive was assigned to the attacking player");
            var eligible = FleetCombat.EligibleDice(s.C, s.S); int invalid = Enumerable.Range(0, s.S.Combat!.Rolls.Length).First(i => !eligible.Contains(i));
            s.Do(0, new("ability-reroll", Dice: [eligible[0]]), false); s.Do(1, new("ability-reroll", Dice: [invalid]), false);
            s.Do(1, new("ability-reroll", Dice: [eligible[^1]])); s.Check(s.S.Combat!.AbilityRerolls == 0 && FleetCombat.CanValor(s.S, 0), "Evasive consumed the later Valor decision");
        });
        Case("rapid-fire-respects-selected-result", s =>
        {
            var ship = s.C.Ships["core:kushan-interceptor"]; s.C.Ships[ship.Id] = ship with { Weapons = [new("main", "Main", 1, 3, RapidFire: 2)] }; s.S = s.S with { RandomState = 1 };
            s.Attack(abilities: false); var before = s.S.Combat!.Rolls.ToArray(); int chosen = FleetCombat.EligibleDice(s.C, s.S).Last(); uint expected = s.S.RandomState;
            int result = FleetRuleTools.Roll(ref expected); s.Do(0, new("ability-reroll", Dice: [chosen]));
            s.Check(s.S.Combat!.Rolls[chosen] == result && s.S.RandomState == expected && before.Where((_, i) => i != chosen).SequenceEqual(s.S.Combat.Rolls.Where((_, i) => i != chosen)), "Rapid Fire rerolled an unselected die");
        });
    }
}
