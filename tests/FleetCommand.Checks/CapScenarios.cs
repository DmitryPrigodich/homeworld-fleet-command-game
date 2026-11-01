using System.Text.Json;
using FleetCommand.Core;

internal static class CapScenarios
{
    private const string Attacker = "s0-0", Fighter = "s1-3", Parent = "s1-5";
    private sealed class Session
    {
        public readonly ContentCatalog C;
        public readonly BattleEngine E;
        public BattleState S;
        private readonly string _path;
        private readonly List<object> _trace = [];
        public Session(string root, string name)
        {
            C = ContentCatalog.Load([Path.Combine(root, "content", "base")]); BasicCardBattle.Register(C); E = new(C);
            C.Maps[BasicCardBattle.TabletopMapId] = C.Maps[BasicCardBattle.TabletopMapId] with { Objects = [] };
            foreach (string id in new[] { "core:kushan-interceptor", "core:taiidan-interceptor" })
                C.Ships[id] = C.Ships[id] with { Class = "fighter", Faction = "taiidan", Success = 2, Armor = 0, Stars = 1, Ram = 1, Abilities = [], Weapons = [new("gun", "Fighter guns", 1, 4)] };
            C.Ships["core:taiidan-frigate"] = C.Ships["core:taiidan-frigate"] with { Class = "capital", Success = 2, Armor = 2, Stars = 3, Abilities = [], Weapons = [new("flak", "Hull flak", 1, 0, DefensiveFire: true)] };
            S = E.Create(BasicCardBattle.TabletopMapId, 1);
            string[] a = ["order-01", "order-09"], b = ["order-21", "order-22"];
            S = S with { ActiveSide = 0, ValorOwner = 1, Cards = new(Enumerable.Range(1, 24).Select(i => $"order-{i:00}").Except(a).Except(b).ToArray(), a, b, [], Phase: "orders"),
                Units = S.Units.Select(u => u.Id == Attacker ? u with { Q = 2, R = 2, Models = 2 } : u.Id == Parent ? u with { Q = 4, R = 2, Models = 1 } : u.Id == Fighter ? u with { Q = 4, R = 2, Models = 2, Patrol = Parent } : u).ToList() };
            _path = Path.Combine(root, "tmp", "test-results", name + ".trace.json"); Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        }
        public void Check(bool condition, string text) { if (!condition) throw new Exception(text); }
        public void Do(int side, BattleCommand command, bool accepted = true)
        {
            string before = E.Save(S); var a = E.Apply(S, side, command); var b = E.Apply(E.Load(before), side, command);
            _trace.Add(new { side, command, a.Accepted, a.Message, a.State.Sequence, a.State.CapEntry, a.State.Defense, a.State.Combat });
            File.WriteAllText(_path, JsonSerializer.Serialize(_trace, JsonData.Options));
            Check(a.Accepted == accepted, "CAP scenario command: " + a.Message);
            Check(E.Save(S) == before && E.Save(a.State) == E.Save(b.State), "CAP mutated input or diverged after restore");
            S = E.Load(E.Save(a.State));
            foreach (int viewer in new[] { 0, 1 })
            {
                var view = E.Load(E.Save(BasicCardRules.View(S, viewer)));
                Check(view.RandomState == 1 && view.Cards!.Deck.Length == 0 && !E.Apply(view, viewer, command).Accepted, "CAP view leaked authority");
            }
        }
        public void Attack(string target = Parent, string kind = "ranged")
        {
            Do(0, new("attack", Attacker, target, Card: "order-01", AttackKind: kind));
            if (S.Defense == null && S.Combat == null && S.Cards!.Response != null) Do(1, new("decline"));
        }
        public void Rolls(params int[] rolls) { Check(S.Combat!.Rolls.Length == rolls.Length, "Wrong CAP dice pool"); S = S with { Combat = S.Combat with { Rolls = rolls } }; }
        public void Missiles() { var ship = C.Ships["core:kushan-interceptor"]; C.Ships[ship.Id] = ship with { Weapons = [ship.Weapons![0] with { Kind = "missile", DicePerModel = 3 }] }; }
    }
    public static void Run(string root)
    {
        void Case(string name, Action<Session> test) { test(new(root, name)); Console.WriteLine("CAP SCENARIO PASS " + name); }
        Case("cap-entry-after-move-and-one-patrol-per-host", s =>
        {
            s.S = s.S with { ActiveSide = 1, Units = s.S.Units.Select(u => u.Id == Fighter ? u with { Patrol = "", Q = 2, R = 3 } : u).ToList() };
            s.Do(1, new("move", Fighter, Q: 3, R: 2, Card: "order-21"));
            for (int i = 0; s.S.CapEntry == null && i < 10; i++) s.Do(0, new("decline"));
            s.Check(s.S.CapEntry is { Side: 1 } && s.S.Units.Single(u => u.Id == Fighter).Patrol == "", "Move did not offer optional CAP entry");
            s.Do(0, new("cap-enter", Target: Parent), false); s.Do(1, new("cap-enter", Target: Parent));
            s.Check(s.S.Units.Single(u => u.Id == Fighter) is { Patrol: Parent, Q: 4, R: 2, Activated: true } && s.S.CapEntry == null && s.S.Orders1 == 1, "CAP entry did not attach or paid again");
            var other = s.S.Units.Single(u => u.Id == "s1-4") with { Q = 3, R = 2 };
            s.Check(!FleetCap.Hosts(s.C, s.S, other).Any(u => u.Id == Parent), "Two patrols could join the same host");
        });
        Case("cap-exit-is-atomic-and-requires-ready-parent", s =>
        {
            s.S = s.S with { ActiveSide = 1 };
            s.Do(1, new("begin-order", Fighter, "move", Card: "order-21"), false);
            var cell = FleetCap.ExitCells(s.C, s.S, s.S.Units.Single(u => u.Id == Fighter))[0];
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == Parent ? u with { Activated = true } : u).ToList() };
            s.Do(1, new("begin-order", Fighter, "move", Card: "order-21", Deployments: [new(Fighter, cell.Q, cell.R)]), false);
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == Parent ? u with { Activated = false } : u).ToList() };
            s.Do(1, new("begin-order", Fighter, "move", Card: "order-21", Deployments: [new(Fighter, cell.Q, cell.R)]));
            var fighter = s.S.Units.Single(u => u.Id == Fighter);
            s.Check(fighter.Patrol == "" && fighter.Q == cell.Q && fighter.R == cell.R && s.S.Orders1 == 1 && s.S.Cards!.Pending?.BasicAction == "move", "CAP exit did not precede exactly one paid order");
        });
        Case("cap-follows-parent-and-does-not-occupy-a-second-hex", s =>
        {
            s.S = s.S with { ActiveSide = 1 }; s.Do(1, new("move", Parent, Q: 4, R: 3, Card: "order-21"));
            for (int i = 0; s.S.Cards!.Response != null && i < 10; i++) s.Do(0, new("decline"));
            var p = s.S.Units.Single(u => u.Id == Parent); var f = s.S.Units.Single(u => u.Id == Fighter);
            s.Check(p.Q == 4 && p.R == 3 && f.Q == p.Q && f.R == p.R && !f.Activated, "CAP failed to follow its host or was activated by host movement");
        });
        Case("cap-can-be-targeted-separately", s =>
        {
            s.Attack(Fighter); s.Check(s.S.Combat!.Target == Fighter, "CAP target was replaced by its parent");
            s.Rolls(6, 6); s.Do(0, new("resolve"));
            s.Check(!s.S.Units.Any(u => u.Id == Fighter) && s.S.Units.Single(u => u.Id == Parent).Models == 1, "Targeting CAP damaged the parent");
        });
        Case("cap-returns-activated-after-parent-destruction", s =>
        {
            s.C.Ships["core:taiidan-frigate"] = s.C.Ships["core:taiidan-frigate"] with { Armor = 0 };
            s.Attack(); s.Do(1, new("decline-defense")); s.Rolls(6, 6); s.Do(0, new("resolve"));
            s.Check(!s.S.Units.Any(u => u.Id == Parent) && s.S.Units.Single(u => u.Id == Fighter) is { Patrol: "", Q: 4, R: 2, Activated: true }, "Orphaned CAP did not return to the destroyed parent's hex");
        });
        Case("cap-projectiles-then-defensive-fire-with-one-card-no-play", s =>
        {
            s.Missiles(); s.Attack(); s.Do(1, new("cap", Card: "order-21"));
            s.Check(s.S.Orders1 == 0 && s.S.Cards!.Hand1.Length == 1 && s.S.Units.Single(u => u.Id == Fighter) is { CapUsed: true, Activated: true }, "CAP did not spend exactly one card without Play");
            s.Rolls(6, 1); s.Do(1, new("resolve"));
            s.Check(s.S.Defense is { CapComplete: true, Reduction: 1 } && s.S.Combat == null, "CAP did not hand off to hull defensive fire");
            s.Do(1, new("cap", Card: "order-22"), false); s.Do(1, new("defend")); s.Rolls(6); s.Do(1, new("resolve"));
            s.Check(s.S.Combat is { Kind: "ranged", Rolls.Length: 4 }, "CAP and defensive fire reductions were not combined");
        });
        Case("cap-dogfight-losses-are-simultaneous", s =>
        {
            s.Attack(); s.Do(1, new("cap", Card: "order-21"));
            s.Check(s.S.Defense!.PreparedIncoming?.Rolls.Length == 2, "Incoming dogfight was not rolled before casualty/reroll decisions");
            s.Rolls(6, 6); s.Do(1, new("resolve"));
            s.Check(s.S.Combat?.Kind == "dogfight" && s.S.Units.Single(u => u.Id == Attacker).Models == 2, "CAP applied casualties before both sides could fire");
            s.Rolls(6, 6); s.Do(0, new("resolve"));
            s.Check(!s.S.Units.Any(u => u.Id == Attacker || u.Id == Fighter) && s.S.Defense == null && s.S.Combat == null && s.S.Cards!.Response == null && s.S.Stars0 == 1 && s.S.Stars1 == 1, "Mutually destroyed fighters did not both fire or original attack continued");
        });
        Case("cap-dogfight-survivors-rebuild-original-attack", s =>
        {
            s.Attack(); s.Do(1, new("cap", Card: "order-21")); s.Rolls(6, 1); s.Do(1, new("resolve")); s.Rolls(1, 1); s.Do(0, new("resolve"));
            s.Check(s.S.Defense is { CapComplete: true } && s.S.Units.Single(u => u.Id == Attacker).Models == 1, "Dogfight did not apply losses before hull defense");
            s.Do(1, new("decline-defense")); s.Check(s.S.Combat is { Kind: "ranged", Rolls.Length: 1 }, "Original attack reused pre-dogfight dice");
        });
        Case("cap-no-hand-does-not-leak-window-eligibility", s =>
        {
            var cards = s.S.Cards!; s.S = s.S with { Cards = cards with { Deck = cards.Deck.Concat(cards.Hand1).ToArray(), Hand1 = [] } };
            s.Attack(); s.Check(s.S.Defense != null, "CAP window leaked whether a card was in hand"); s.Do(1, new("cap", Card: "order-21"), false); s.Do(1, new("decline-defense"));
        });
        Case("cap-success-modifier-after-activation", s =>
        {
            s.C.Ships["core:taiidan-interceptor"] = s.C.Ships["core:taiidan-interceptor"] with { Success = 6, CapSuccess = 4 };
            s.Missiles(); s.Attack(); s.Do(1, new("cap", Card: "order-21")); s.Rolls(4, 5); s.Do(1, new("resolve"));
            s.Check(s.S.Defense!.Reduction == 2, "CAP success modifier was not applied to the activated patrol");
        });
        Case("cap-boarding-lock-blocks-patrol-and-hull-defense", s =>
        {
            s.C.Ships["core:kushan-interceptor"] = s.C.Ships["core:kushan-interceptor"] with { Class = "frigate", Crew = 2 };
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == Attacker ? u with { Q = 3, R = 2 } : u.Id == Parent ? u with { LastAction = "board", LastTarget = Attacker, LastActionRound = 1 } : u).ToList() };
            s.Attack(kind: "board"); s.Check(s.S.Defense == null && s.S.Combat?.Kind == "board", "Mutual boarding still allowed CAP");
        });
        Case("cap-stops-boarding-before-any-board-dice", s =>
        {
            s.C.Ships["core:kushan-interceptor"] = s.C.Ships["core:kushan-interceptor"] with { Class = "frigate", Crew = 2 };
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == Attacker ? u with { Q = 3, R = 2, Models = 1 } : u).ToList() };
            s.Attack(kind: "board"); s.Do(1, new("cap", Card: "order-21")); s.Rolls(6, 6); s.Do(1, new("resolve"));
            s.Check(!s.S.Units.Any(u => u.Id == Attacker) && s.S.Combat == null && s.S.Defense == null && s.S.Cards!.Response == null && s.S.Units.Single(u => u.Id == Fighter).Models == 2, "Destroyed boarder attacked or traded a dogfight volley with CAP");
        });
        Case("cap-inside-interrupt-resumes-host-movement", s =>
        {
            s.C.Ships["core:kushan-frigate"] = s.C.Ships["core:kushan-frigate"] with { Class = "capital", Abilities = [], Weapons = [new("flak", "Flak", 1, 0, DefensiveFire: true)] };
            string[] a = ["order-01", "order-09"], b = ["order-17", "order-21"];
            s.S = s.S with { ValorOwner = 0, Cards = new(Enumerable.Range(1, 24).Select(i => $"order-{i:00}").Except(a).Except(b).ToArray(), a, b, [], Phase: "orders"),
                Units = s.S.Units.Select(u => u.Id == Attacker ? u with { Patrol = "s0-2", Q = 2, R = 2 } : u.Id == "s0-2" ? u with { Q = 2, R = 2 } : u.Id == Fighter ? u with { Patrol = "", Q = 4, R = 2 } : u.Id == Parent ? u with { Q = 5, R = 2 } : u).ToList() };
            s.Do(0, new("move", "s0-2", Q: 3, R: 2, Card: "order-01"));
            s.Do(1, new("interrupt", Fighter, "s0-2", Card: "order-17"));
            s.Check(s.S.Defense is { Side: 1, Interrupt: true }, "Interrupt did not offer CAP to the moving host");
            s.Do(0, new("cap", Card: "order-09")); s.Rolls(6, 6); s.Do(0, new("resolve")); s.Rolls(1, 1); s.Do(1, new("resolve"));
            s.Check(s.S.Cards!.Response == null && s.S.Defense == null && s.S.Units.Single(u => u.Id == "s0-2") is { Q: 3, R: 2 } && s.S.Units.Single(u => u.Id == Attacker) is { Patrol: "s0-2", Q: 3, R: 2 }, "CAP interception lost the original move or the attached patrol");
        });
        Case("cap-rejects-corrupt-prepared-volley", s =>
        {
            s.Attack(); s.Do(1, new("cap", Card: "order-21"));
            var corrupt = s.S with { Defense = s.S.Defense! with { PreparedIncoming = s.S.Defense.PreparedIncoming! with { Rolls = [0, 6] } } };
            bool rejected = false; try { s.E.Load(s.E.Save(corrupt)); } catch (InvalidDataException) { rejected = true; }
            s.Check(rejected, "Invalid prepared dogfight dice were accepted");
        });
        Case("cap-reset-and-malformed-snapshots", s =>
        {
            var corrupt = s.S with { Units = s.S.Units.Select(u => u.Id == "s1-4" ? u with { Patrol = Parent, Q = 4, R = 2 } : u).ToList() };
            bool rejected = false; try { s.E.Load(s.E.Save(corrupt)); } catch (InvalidDataException) { rejected = true; } s.Check(rejected, "Duplicate patrol was accepted");
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == Fighter ? u with { CapUsed = true, Activated = true } : u).ToList() };
            s.Do(0, new("pass")); s.Do(1, new("pass")); s.Check(s.S.Round == 2 && !s.S.Units.Single(u => u.Id == Fighter).CapUsed, "CAP token was not refreshed for the next round");
        });
    }
}
