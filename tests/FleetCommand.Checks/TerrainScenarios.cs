using System.Text.Json;
using FleetCommand.Core;

internal static class TerrainScenarios
{
    private const string Actor = "s0-0";
    private sealed class Session
    {
        public readonly ContentCatalog C;
        public readonly BattleEngine E;
        public BattleState S;
        private readonly List<object> _trace = [];
        private readonly string _path;
        public Session(string root, string name)
        {
            C = ContentCatalog.Load([Path.Combine(root, "content", "base")]); BasicCardBattle.Register(C); E = new(C);
            C.Objects["test:asteroid"] = new("test:asteroid", "Asteroid", Kind: "asteroid", Hazard: new(3));
            C.Objects["test:mine"] = new("test:mine", "Mines", Kind: "mine", Hazard: new(1));
            C.Maps[BasicCardBattle.TabletopMapId] = C.Maps[BasicCardBattle.TabletopMapId] with { Objects = [new("test:asteroid", 2, 2)] };
            C.Ships["core:kushan-interceptor"] = C.Ships["core:kushan-interceptor"] with { Class = "capital", Armor = 20, Success = 2, Evasive = 1, Stars = 2, Move = 5, Abilities = [], Weapons = [new("guard", "Guard", 1, 3, DefensiveFire: true)] };
            S = E.Create(BasicCardBattle.TabletopMapId, 1);
            string[] a = ["order-01", "order-09"], b = ["order-21", "order-22"];
            S = S with { ActiveSide = 0, ValorOwner = 0, RandomState = 1, Units = S.Units.Select(u => u.Id == Actor ? u with { Q = 1, R = 2, Models = 3, Facing = 0 } : u).ToList(),
                Cards = new(Enumerable.Range(1, 24).Select(i => $"order-{i:00}").Except(a).Except(b).ToArray(), a, b, [], Phase: "orders") };
            _path = Path.Combine(root, "tmp", "test-results", name + ".trace.json"); Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        }
        public void Check(bool value, string text) { if (!value) throw new Exception(text); }
        public void Do(int side, BattleCommand command, bool accepted = true)
        {
            string before = E.Save(S); var a = E.Apply(S, side, command); var b = E.Apply(E.Load(before), side, command);
            _trace.Add(new { side, command, a.Accepted, a.Message, a.State.Sequence, a.State.Hazard, a.State.Hazards, a.State.Defense, a.State.Combat, a.State.Mines });
            File.WriteAllText(_path, JsonSerializer.Serialize(_trace, JsonData.Options));
            Check(a.Accepted == accepted, "Terrain command: " + a.Message);
            Check(E.Save(S) == before && E.Save(a.State) == E.Save(b.State), "Terrain mutated input or diverged after restore");
            S = E.Load(E.Save(a.State));
            foreach (int viewer in new[] { 0, 1 })
            {
                var view = E.Load(E.Save(BasicCardRules.View(S, viewer)));
                Check(view.RandomState == 1 && view.Cards!.Deck.Length == 0 && !E.Apply(view, viewer, command).Accepted, "Terrain view leaked authority");
            }
        }
        public void Mine(int owner = -1) => C.Maps[S.Map] = C.Maps[S.Map] with { Objects = [new("test:mine", 2, 2, owner)] };
        public void Move() => Do(0, new("move", Actor, Q: 3, R: 2, Card: "order-01"));
        public void Guard(int successes)
        {
            Do(0, new("defend")); Check(S.Combat?.Kind == "defense", "Expected a saved defensive roll");
            S = S with { Combat = S.Combat! with { Rolls = Enumerable.Range(0, S.Combat!.Rolls.Length).Select(i => i < successes ? 6 : 1).ToArray() } };
            Do(0, new("resolve"));
        }
        public void Impact(params int[] rolls)
        {
            Check(S.Combat is { Kind: "terrain", AbilityRerolls: > 0 } && S.Combat.Rolls.Length == rolls.Length, "Expected terrain Evasive choice with the specified pool");
            S = S with { Combat = S.Combat! with { Rolls = rolls } }; Do(0, new("ability-reroll", Dice: []));
        }
        public void Drain()
        {
            for (int i = 0; S.Hazard != null && i < 40; i++)
                if (S.Combat is { } combat) Do(FleetCombat.DecisionSide(combat), combat.AbilityRerolls > 0 ? new("ability-reroll", Dice: []) : new("resolve"));
                else if (S.Defense is { } defense) Do(1 - defense.Side, new("decline-defense"));
            Check(S.Hazard == null, "Terrain sequence did not terminate");
        }
        public void AttachCap()
        {
            C.Ships["test:patrol"] = new("test:patrol", "Patrol", "kushan", "", 2, 3, 1, 2, 0, 1, Class: "fighter", Weapons: [new("gun", "Gun", 1, 1)]);
            S = S with { Units = S.Units.Select(u => u.Id == "s0-1" ? u with { Definition = "test:patrol", Models = 2, Q = 1, R = 2, Patrol = Actor } : u).ToList() };
        }
    }
    public static void Run(string root)
    {
        void Case(string name, Action<Session> test) { test(new(root, name)); Console.WriteLine("TERRAIN SCENARIO PASS " + name); }
        Case("terrain-entry-pauses-path-and-resumes-once", s =>
        {
            s.Move(); s.Check(s.S.Hazard?.Kind == "asteroid" && s.S.Units.Single(u => u.Id == Actor).Q == 2 && s.S.Cards!.Response!.RemainingPath.Length == 1, "Movement did not pause on entering the asteroid");
            s.Do(1, new("defend"), false); s.Guard(3); s.Impact(6);
            s.Check(s.S.Units.Single(u => u.Id == Actor) is { Q: 3, R: 2, Models: 3 } && s.S.Cards!.Response == null && s.S.Orders0 == 1 && s.S.Cards.Discard.Length == 1, "Terrain did not return to the original paid move");
        });
        Case("terrain-activation-waits-for-repair-before-actions", s =>
        {
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == Actor ? u with { Q = 2, FocusFire = 1 } : u).ToList() };
            s.Do(0, new("begin-order", Actor, "move", Card: "order-01", Repair: "ask"));
            s.Check(s.S.Repairs is { Length: 1 } && s.S.Hazard == null && s.S.Hazards is { Length: 1 }, "Activation did not retain the pending terrain while repairing");
            s.Do(0, new("repair-choice", Repair: "focus")); s.Check(s.S.Hazard != null && s.S.Units.Single(u => u.Id == Actor).FocusFire == 0, "Repair advanced the order past its terrain attack");
            s.Drain(); s.Check(s.S.Cards!.Pending?.BasicAction == "move" && s.S.Orders0 == 1, "Activation terrain consumed the pending move");
        });
        Case("terrain-mine-owner-is-immune", s =>
        {
            s.Mine(0); s.Move(); s.Check(s.S.Hazard == null && s.S.Units.Single(u => u.Id == Actor).Q == 3 && FleetTerrain.MineCount(s.S, 0) == 5, "Owned mines attacked their own fleet");
        });
        Case("terrain-mines-reduce-after-their-attack", s =>
        {
            s.Mine(); s.Move(); s.Guard(3);
            s.Check(s.S.Hazard!.ReduceAfter && FleetTerrain.MineCount(s.S, 0) == 5 && s.S.Combat!.Rolls.Length == 5, "Defensive fire changed the mine pool before its pending attack");
            s.Impact(6, 6, 5, 2, 2); s.Check(FleetTerrain.MineCount(s.S, 0) == 3 && s.S.Units.Single(u => u.Id == Actor).Models == 3, "One defense reduction incorrectly removed the minefield");
        });
        Case("terrain-mines-defense-and-focus-remove-both-faces", s =>
        {
            s.Mine(); s.Move(); s.Guard(1); s.Impact(6, 1, 2, 2, 2);
            s.Check(FleetTerrain.MineCount(s.S, 0) == 0 && !FleetTerrain.Visible(s.C, s.S, 0) && s.S.Units.Single(u => u.Id == Actor).FocusFire == 1, "Defense and inflicted Focus Fire did not reduce mines twice");
        });
        Case("terrain-mines-casualty-and-focus-are-one-damage-reduction", s =>
        {
            s.Mine(); s.C.Ships["core:kushan-interceptor"] = s.C.Ships["core:kushan-interceptor"] with { Armor = 0 }; s.Move(); s.Do(0, new("decline-defense")); s.Impact(5, 1, 2, 3, 4);
            s.Check(FleetTerrain.MineCount(s.S, 0) == 3 && s.S.Units.Single(u => u.Id == Actor).Models == 2 && s.S.Stars1 == 0, "A casualty plus Focus Fire counted as two damage reductions or credited neutral terrain kills");
        });
        Case("terrain-owned-mines-kill-and-cancel-rest-of-move", s =>
        {
            s.Mine(1); s.C.Ships["core:kushan-interceptor"] = s.C.Ships["core:kushan-interceptor"] with { Armor = 0 };
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == Actor ? u with { Models = 1 } : u).ToList() };
            s.Move(); s.Do(0, new("decline-defense")); s.Impact(5, 5, 2, 2, 2);
            s.Check(!s.S.Units.Any(u => u.Id == Actor) && s.S.Cards!.Response == null && s.S.Orders0 == 1 && s.S.Stars1 == 2, "Killed mover continued or owned mine did not credit its fleet");
        });
        Case("terrain-crossing-occupied-friendly-hex-preserves-unique-cells", s =>
        {
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s0-2" ? u with { Q = 2, R = 2 } : u).ToList() };
            s.Move(); s.Check(s.S.Cards!.Response?.Transit == new Hex(2, 2) && s.S.Units.Single(u => u.Id == Actor).Q == 1, "Friendly transit did not retain its separate path position");
            s.Guard(3); s.Impact(6); s.Check(s.S.Units.Single(u => u.Id == Actor).Q == 3 && s.S.Units.Single(u => u.Id == "s0-2").Q == 2, "Transit hazard attacked the ally or lost the original path");
        });
        Case("terrain-turning-does-not-enter-the-field-again", s =>
        {
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == Actor ? u with { Q = 2 } : u).ToList() };
            s.Do(0, new("move", Actor, Q: 2, R: 2, Facing: 1, AutoFacing: false, Card: "order-01")); s.Guard(3); s.Impact(6);
            s.Check(s.S.Hazard == null && s.S.Units.Single(u => u.Id == Actor).Facing == 1 && s.S.Log!.Count(e => e.Text.Contains("(terrain)")) == 1, "Turning in place triggered both activation and entry attacks");
        });
        Case("terrain-cannot-be-targeted-like-a-ship", s =>
        {
            s.Mine(); s.Do(0, new("attack", Actor, "terrain:0", Card: "order-01"), false); s.Check(s.S.Orders0 == 0, "An invalid terrain target spent an order");
        });
        Case("terrain-cap-mines-exchange-can-clear-field-before-parent", s =>
        {
            s.Mine(); s.AttachCap(); s.Move(); s.Do(0, new("cap", Card: "order-09", Weapon: "gun"));
            s.Check(s.S.Defense!.PreparedIncoming?.Kind == "terrain-dogfight", "Mines did not roll a simultaneous attack against CAP");
            s.S = s.S with { Combat = s.S.Combat! with { Rolls = [6, 1] }, Defense = s.S.Defense with { PreparedIncoming = s.S.Defense.PreparedIncoming! with { Rolls = [5, 2, 2, 2, 2] } } };
            s.Do(0, new("resolve"));
            s.Check(FleetTerrain.MineCount(s.S, 0) == 0 && s.S.Hazard == null && s.S.Units.Single(u => u.Id == Actor) is { Q: 3, Models: 3, FocusFire: 0 } && s.S.Units.Single(u => u.Id == "s0-1").Models == 1, "CAP did not clear mines before the parent's attack or failed simultaneous casualties");
        });
        Case("terrain-cap-asteroids-reduce-parent-pool-and-keep-field", s =>
        {
            s.AttachCap(); s.Move(); s.Do(0, new("cap", Card: "order-09", Weapon: "gun"));
            s.S = s.S with { Combat = s.S.Combat! with { Rolls = [6, 1] }, Defense = s.S.Defense! with { PreparedIncoming = s.S.Defense!.PreparedIncoming! with { Rolls = [5, 2, 2] } } };
            s.Do(0, new("resolve")); s.Check(s.S.Defense is { Reduction: 1, CapComplete: true } && s.S.Units.Single(u => u.Id == "s0-1").Models == 1, "CAP did not preserve asteroid reduction/return casualties");
            s.Drain(); s.Check(FleetTerrain.Visible(s.C, s.S, 0) && s.S.Units.Single(u => u.Id == Actor).Q == 3, "CAP destroyed an asteroid field or lost parent movement");
        });
        Case("terrain-cap-return-reroll-preserves-simultaneous-pools", s =>
        {
            s.AttachCap(); s.C.Ships["test:patrol"] = s.C.Ships["test:patrol"] with { Evasive = 1 };
            s.Move(); s.Do(0, new("cap", Card: "order-09", Weapon: "gun"));
            s.S = s.S with { Combat = s.S.Combat! with { Rolls = [6, 1] }, Defense = s.S.Defense! with { PreparedIncoming = s.S.Defense!.PreparedIncoming! with { Rolls = [5, 2, 2] } } };
            s.Do(0, new("resolve"));
            s.Check(s.S.Combat is { Kind: "terrain-dogfight", AbilityRerolls: 1 } && s.S.Defense!.CapVolley != null && s.S.Units.Single(u => u.Id == "s0-1").Models == 2, "CAP casualties occurred before the saved terrain reroll choice");
            s.Do(0, new("ability-reroll", Dice: []));
            s.Check(s.S.Defense is { Reduction: 1, CapComplete: true } && s.S.Units.Single(u => u.Id == "s0-1").Models == 1, "CAP return reroll lost either simultaneous volley");
            s.Drain(); s.Check(s.S.Units.Single(u => u.Id == Actor).Q == 3, "CAP reroll failed to resume the original move");
        });
        Case("terrain-formation-continues-with-surviving-second-actor", s =>
        {
            s.C.Ships["core:kushan-interceptor"] = s.C.Ships["core:kushan-interceptor"] with { Armor = 0 };
            s.C.Maps[s.S.Map] = s.C.Maps[s.S.Map] with { Objects = [new("test:asteroid", 2, 2), new("test:asteroid", 2, 4)] };
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == Actor ? u with { Q = 2 } : u.Id == "s0-1" ? u with { Q = 2, R = 4 } : u).ToList() };
            s.Do(0, new("special", Actor, Card: "order-09", OtherUnit: "s0-1")); s.Guard(0); s.Impact(6, 6, 5);
            s.Check(!s.S.Units.Any(u => u.Id == Actor) && s.S.Hazard?.Job.Unit == "s0-1", "Destruction skipped the second actor's activation terrain");
            s.Drain(); s.Check(s.S.Cards!.Pending is { Step: 2 } && s.S.Orders0 == 1, "Formation did not resume with the surviving second actor");
        });
        Case("terrain-removed-mines-stay-gone-after-another-entry", s =>
        {
            s.Mine(); s.S = s.S with { Mines = new() { [0] = 0 } }; s.Move(); s.Check(s.S.Hazard == null && s.S.Units.Single(u => u.Id == Actor).Q == 3, "Removed minefield respawned during movement");
        });
        Case("terrain-malformed-token-and-source-rejected", s =>
        {
            s.Mine(); s.Move();
            foreach (var corrupt in new[] { s.S with { Mines = new() { [0] = 4 } }, s.S with { Hazard = s.S.Hazard! with { Source = s.S.Hazard.Source with { Side = 0 } } } })
            { bool rejected = false; try { s.E.Load(s.E.Save(corrupt)); } catch (InvalidDataException) { rejected = true; } s.Check(rejected, "Corrupt terrain state was accepted"); }
        });
    }
}
