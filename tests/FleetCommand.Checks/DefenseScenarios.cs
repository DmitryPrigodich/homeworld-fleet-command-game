using System.Text.Json;
using FleetCommand.Core;

internal static class DefenseScenarios
{
    // Synthetic statistics exercise timing; these are not transcriptions of unavailable Unit cards.
    private sealed class Session
    {
        public readonly ContentCatalog C;
        public readonly BattleEngine E;
        public BattleState S;
        private readonly string _path;
        private readonly List<object> _trace = [];
        public const string Attacker = "s0-0", Defender = "s1-3";
        public Session(string root, string name)
        {
            C = ContentCatalog.Load([Path.Combine(root, "content", "base")]); BasicCardBattle.Register(C); E = new(C);
            C.Maps[BasicCardBattle.TabletopMapId] = C.Maps[BasicCardBattle.TabletopMapId] with { Objects = [] };
            C.Ships["core:kushan-interceptor"] = C.Ships["core:kushan-interceptor"] with { Class = "frigate", Faction = "taiidan", Ram = 2, Crew = 2, Armor = 0, Success = 2, Abilities = [], Weapons = [new("incoming", "Missiles", 3, 4, Kind: "missile")] };
            C.Ships["core:taiidan-interceptor"] = C.Ships["core:taiidan-interceptor"] with { Class = "capital", Armor = 2, Success = 2, Abilities = [], Weapons = [new("flak", "Flak", 1, 0, DefensiveFire: true)] };
            S = E.Create(BasicCardBattle.TabletopMapId, 1);
            string[] a = ["order-01", "order-09"], b = ["order-21", "order-22"];
            S = S with { ActiveSide = 0, ValorOwner = 1, Units = S.Units.Select(u => u.Id == Attacker ? u with { Q = 1, R = 1, Models = 2 } : u.Id == Defender ? u with { Q = 3, R = 1, Models = 3, Activated = true } : u).ToList(),
                Cards = new(Enumerable.Range(1, 24).Select(i => $"order-{i:00}").Except(a).Except(b).ToArray(), a, b, [], Phase: "orders") };
            _path = Path.Combine(root, "tmp", "test-results", name + ".trace.json"); Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        }
        public void Check(bool condition, string text) { if (!condition) throw new Exception(text); }
        public void Do(int side, BattleCommand command, bool accepted = true)
        {
            string before = E.Save(S); var a = E.Apply(S, side, command); var b = E.Apply(E.Load(before), side, command);
            _trace.Add(new { side, command, a.Accepted, a.Message, a.State.Sequence, a.State.Defense, a.State.Combat });
            File.WriteAllText(_path, JsonSerializer.Serialize(_trace, JsonData.Options));
            Check(a.Accepted == accepted, "Defense scenario command: " + a.Message);
            Check(E.Save(S) == before && E.Save(a.State) == E.Save(b.State), "Defense mutated input or diverged after save/load");
            S = E.Load(E.Save(a.State));
            foreach (int viewer in new[] { 0, 1 })
            {
                var view = E.Load(E.Save(BasicCardRules.View(S, viewer)));
                Check(view.RandomState == 1 && view.Cards!.Deck.Length == 0 && !E.Apply(view, viewer, command).Accepted, "Defense view leaked authority");
            }
        }
        public void Attack(string kind = "ranged", bool expectDefense = true)
        {
            Do(0, new("attack", Attacker, Defender, Card: "order-01", AttackKind: kind));
            if (S.Defense == null && S.Combat == null && S.Cards?.Response != null) Do(1, new("decline"));
            Check((S.Defense != null) == expectDefense, "Wrong defensive-fire eligibility");
        }
        public void Rolls(params int[] dice) { Check(S.Combat!.Rolls.Length == dice.Length, "Unexpected defensive-fire pool"); S = S with { Combat = S.Combat with { Rolls = dice } }; }
        public void Adjacent() => S = S with { Units = S.Units.Select(u => u.Id == Attacker ? u with { Q = 2, R = 1 } : u).ToList() };
    }
    public static void Run(string root)
    {
        void Case(string name, Action<Session> test) { test(new(root, name)); Console.WriteLine("DEFENSE SCENARIO PASS " + name); }
        Case("defense-support-focus-free-activation-and-projectiles", s =>
        {
            var asteroid = s.C.Objects.Values.First(o => !o.BlocksMovement);
            s.C.Objects[asteroid.Id] = asteroid with { AttackPenalty = 3 };
            s.C.Maps[s.S.Map] = s.C.Maps[s.S.Map] with { Objects = [new(asteroid.Id, 2, 1)] };
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == Session.Defender ? u with { FocusFire = 2 } : u.Id == "s1-4" ? u with { Definition = "core:taiidan-interceptor", Q = 3, R = 2 } : u).ToList() };
            s.Attack(); uint before = s.S.RandomState; s.Check(s.S.Combat == null && s.S.Defense!.IncomingDice == 3, "Incoming attack rolled before defense or ignored cover");
            s.Do(0, new("defend"), false); s.Check(s.S.RandomState == before, "Rejected defense consumed random dice");
            s.Do(1, new("defend")); s.Rolls(6, 1); s.Do(1, new("resolve"));
            s.Check(s.S.Combat is { Kind: "ranged", Rolls.Length: 2 } && s.S.Units.Single(u => u.Id == Session.Attacker).Models == 2, "Defense damaged the launcher instead of reducing missiles");
            s.Check(s.S.Orders1 == 0 && s.S.Cards!.Hand1.Length == 2 && s.S.Units.Single(u => u.Id == Session.Defender).Activated, "Free defense spent an order/card or changed activation");
            s.Do(0, new("resolve"));
        });
        Case("defense-fighter-casualties-change-incoming-pool", s =>
        {
            var ship = s.C.Ships["core:kushan-interceptor"]; s.C.Ships[ship.Id] = ship with { Class = "fighter", Armor = 20, Weapons = [ship.Weapons![0] with { Kind = "fighter" }] };
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == Session.Attacker ? u with { Models = 4 } : u.Id == Session.Defender ? u with { Models = 2 } : u).ToList() };
            s.Attack(); s.Do(1, new("defend")); s.Rolls(6, 6); s.Do(1, new("resolve"));
            s.Check(s.S.Units.Single(u => u.Id == Session.Attacker).Models == 2 && s.S.Combat!.Rolls.Length == 6, "Fighter armor blocked defense or incoming dice used dead fighters");
        });
        Case("defense-destroys-boarder-before-attack", s =>
        {
            s.Adjacent(); s.S = s.S with { Units = s.S.Units.Select(u => u.Id == Session.Attacker ? u with { Models = 1 } : u).ToList() };
            s.Attack("board"); s.Do(1, new("defend")); s.Rolls(6, 6, 6); s.Do(1, new("resolve"));
            s.Check(!s.S.Units.Any(u => u.Id == Session.Attacker) && s.S.Cards!.Response == null && s.S.Combat == null && s.S.Defense == null && s.S.Orders0 == 1, "Destroyed boarder still attacked or paid twice");
        });
        Case("defense-does-not-consume-rammers-attack-window", s =>
        {
            s.Adjacent(); s.S = s.S with { Units = s.S.Units.Select(u => u.Id == Session.Attacker ? u with { Models = 3 } : u).ToList() };
            s.Attack("ram"); s.Do(1, new("defend")); s.Rolls(6, 1, 1); s.Do(1, new("resolve"));
            s.Check(s.S.Combat is { Kind: "ram", Rolls.Length: 4 } && s.S.Units.Single(u => u.Id == Session.Defender).AttackWindow == "", "Free defense counted as a normal attack or ram used pre-defense model count");
        });
        Case("defense-mutual-boarding-lock-is-round-and-target-specific", s =>
        {
            s.Adjacent(); s.S = s.S with { Units = s.S.Units.Select(u => u.Id == Session.Defender ? u with { LastAction = "board", LastTarget = Session.Attacker, LastActionRound = s.S.Round } : u).ToList() };
            s.Attack("board", false); s.Check(s.S.Combat!.Kind == "board", "Locked boarding did not reach combat");
        });
        Case("defense-previous-round-boarding-does-not-lock", s =>
        {
            s.Adjacent(); s.S = s.S with { Round = 2, Units = s.S.Units.Select(u => u.Id == Session.Defender ? u with { LastAction = "board", LastTarget = Session.Attacker, LastActionRound = 1 } : u).ToList() };
            s.Attack("board"); s.Do(1, new("decline-defense")); s.Check(s.S.Combat!.Kind == "board", "Declining defense lost the boarding attack");
        });
        Case("defense-crippled-weapons-and-unarmed-support", s =>
        {
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == Session.Defender ? u with { Effects = ["weapons"] } : u).ToList() };
            s.Attack(expectDefense: false); s.Check(s.S.Combat!.Kind == "ranged", "Crippled weapons could defend");
        });
        Case("defense-valor-allowed-ferocious-excluded", s =>
        {
            s.Attack(); s.Do(1, new("defend"));
            s.Check(!FleetCombat.CanFaction(s.C, s.S, s.S.Combat!) && FleetCombat.CanValor(s.S, 1), "Wrong post-roll abilities on defensive fire");
            s.Do(1, new("faction-reroll"), false); s.Do(1, new("valor")); s.Do(1, new("resolve"));
            s.Check(s.S.Combat?.Kind == "ranged" && s.S.ValorOwner == 0, "Valor failed to resume the original attack");
        });
        Case("defense-powered-drone-evasive-does-not-use-launcher-evasive", s =>
        {
            var ship = s.C.Ships["core:kushan-interceptor"]; s.C.Ships[ship.Id] = ship with { Evasive = 20, Weapons = [ship.Weapons![0] with { Kind = "drone" }] };
            s.Attack();
            // Simulate a power-granting order, whose printed card data is not available yet.
            s.S = s.S with { Defense = s.S.Defense! with { Attack = s.S.Defense.Attack with { Power = true } }, Cards = s.S.Cards! with { Response = s.S.Cards.Response! with { Command = s.S.Cards.Response.Command with { Power = true } } } };
            s.Do(1, new("defend")); s.Check(s.S.Combat is { AbilityRerolls: 1, RerollSide: 0 }, "Powered drones inherited the launcher's Evasive instead of Evasive 1");
            s.Do(1, new("ability-reroll"), false); s.Do(0, new("ability-reroll", Dice: [])); s.Do(1, new("resolve"));
        });
        Case("defense-barrage-fixed-success-no-extra-dice-or-evasive", s =>
        {
            var ship = s.C.Ships["core:taiidan-interceptor"]; s.C.Ships[ship.Id] = ship with { Weapons = [ship.Weapons![0] with { Barrage = 5, DicePerModel = 20 }] };
            s.C.Leaders["test:gunner"] = new("test:gunner", "Test gunner", "taiidan", ["capital"], AttackDice: 10);
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == Session.Defender ? u with { Leader = "test:gunner", FocusFire = 2 } : u).ToList() };
            s.Attack(); s.Do(1, new("defend")); s.Rolls(5, 4, 6, 1); s.Check(s.S.Combat!.AbilityRerolls == 0 && FleetCombat.Success(s.C, s.S, s.S.Combat) == 5, "Barrage used ordinary success/Evasive");
            s.Do(1, new("resolve")); s.Check(s.S.Combat!.Rolls.Length == 4, "Barrage defense used leader/model dice or wrong success value");
        });
        Case("defense-interrupt-resumes-original-move", s =>
        {
            var attacker = s.C.Ships["core:kushan-interceptor"]; var defender = s.C.Ships["core:taiidan-interceptor"];
            s.C.Ships[attacker.Id] = attacker with { Weapons = [new("guard", "Guard", 1, 4, DefensiveFire: true)] };
            s.C.Ships[defender.Id] = defender with { Weapons = [new("reaction", "Reaction missile", 2, 4, Kind: "missile")] };
            string[] a = ["order-01", "order-09"], b = ["order-17", "order-21"];
            s.S = s.S with { ValorOwner = 0, Cards = new(Enumerable.Range(1, 24).Select(i => $"order-{i:00}").Except(a).Except(b).ToArray(), a, b, [], Phase: "orders") };
            s.Do(0, new("move", Session.Attacker, Q: 2, R: 1, Card: "order-01"));
            s.Do(1, new("interrupt", Session.Defender, Session.Attacker, Card: "order-17", Weapon: "reaction"));
            s.Check(s.S.Defense is { Side: 1, Interrupt: true }, "Interrupt did not offer defense to the original mover");
            s.Do(0, new("defend")); s.Rolls(6, 6); s.Do(0, new("resolve"));
            s.Check(s.S.Combat is { Side: 1, Interrupt: true, Rolls.Length: 4 }, "Defense resumed the wrong attacker or incoming pool");
            s.Rolls(1, 1, 1, 1); s.Do(1, new("resolve"));
            s.Check(s.S.Cards!.Response == null && s.S.Defense == null && s.S.Combat == null && s.S.Orders0 == 1 && s.S.Units.Single(u => u.Id == Session.Attacker) is { Q: 2, R: 1 }, "Defended interrupt did not resume the original movement exactly once");
        });
        Case("barrage-offense-target-count-and-success", s =>
        {
            s.C.Ships["core:kushan-interceptor"] = s.C.Ships["core:kushan-interceptor"] with { Weapons = [new("barrage", "Barrage", 20, 4, Barrage: 5)] };
            s.C.Ships["core:taiidan-interceptor"] = s.C.Ships["core:taiidan-interceptor"] with { Evasive = 20 };
            s.Attack(expectDefense: false); s.Check(s.S.Combat is { Rolls.Length: 3, AbilityRerolls: 0 } && FleetCombat.Success(s.C, s.S, s.S.Combat) == 5, "Offensive Barrage used ordinary model dice/success or Evasive");
            s.Rolls(5, 4, 6); s.Do(0, new("resolve"));
            s.Check(s.S.Units.Single(u => u.Id == Session.Defender).Models == 3, "Barrage used the ship's ordinary success threshold");
        });
        Case("defense-malformed-window-rejected", s =>
        {
            s.Attack(); var corrupt = s.S with { Defense = s.S.Defense! with { Side = 1 } };
            bool rejected = false; try { s.E.Load(s.E.Save(corrupt)); } catch (InvalidDataException) { rejected = true; }
            s.Check(rejected, "Malformed defense side accepted in save");
        });
    }
}
