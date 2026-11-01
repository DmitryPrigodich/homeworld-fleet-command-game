using System.Text.Json;
using FleetCommand.Core;
internal static class CloseCombatScenarios
{
    private sealed class Session
    {
        public readonly ContentCatalog C;
        public readonly BattleEngine E;
        public BattleState S;
        private readonly string _file;
        private readonly List<object> _trace = [];
        public Session(string root, string name)
        {
            C = ContentCatalog.Load([Path.Combine(root, "content", "base")]); BasicCardBattle.Register(C); E = new(C);
            C.Maps[BasicCardBattle.TabletopMapId] = C.Maps[BasicCardBattle.TabletopMapId] with { Objects = [] };
            C.Ships["core:kushan-interceptor"] = C.Ships["core:kushan-interceptor"] with { Class = "frigate", Crew = 3, Ram = 3, Armor = 2, Stars = 2, Success = 2, Abilities = [] };
            C.Ships["core:taiidan-interceptor"] = C.Ships["core:taiidan-interceptor"] with { Class = "capital", Armor = 4, Crew = 3, Stars = 4, Success = 2, Abilities = [] };
            S = E.Create(BasicCardBattle.TabletopMapId, 17);
            string[] a = ["order-01", "order-09"], b = ["order-21", "order-22"];
            S = S with { ActiveSide = 0, ValorOwner = 0, Units = S.Units.Select(u => u.Id == "s0-0" ? u with { Q = 2, R = 2, Facing = 0, Models = 2 } : u.Id == "s1-3" ? u with { Q = 3, R = 2, Facing = 3 } : u).ToList(),
                Cards = new(Enumerable.Range(1, 24).Select(i => $"order-{i:00}").Except(a).Except(b).ToArray(), a, b, [], Phase: "orders") };
            _file = Path.Combine(root, "tmp", "test-results", name + ".trace.json"); Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        }
        public void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        public void Do(int side, BattleCommand cmd, bool accepted = true)
        {
            string before = E.Save(S); var a = E.Apply(S, side, cmd); var b = E.Apply(E.Load(before), side, cmd);
            _trace.Add(new { side, cmd, a.Accepted, a.Message, a.State.Sequence, a.State.Combat, a.State.Overrun }); File.WriteAllText(_file, JsonSerializer.Serialize(_trace, JsonData.Options));
            Check(a.Accepted == accepted, "Close combat: " + a.Message); Check(E.Save(S) == before && E.Save(a.State) == E.Save(b.State), "Close combat mutated an input or diverged after restore");
            S = E.Load(E.Save(a.State));
            foreach (int viewer in new[] { 0, 1 }) { var view = E.Load(E.Save(BasicCardRules.View(S, viewer))); Check(view.RandomState == 1 && view.Cards!.Deck.Length == 0 && !E.Apply(view, viewer, cmd).Accepted, "Close combat view leaked authority"); }
        }
        public void Attack(string kind, string effect = "weapons")
        { Do(0, new("attack", "s0-0", "s1-3", Card: "order-01", AttackKind: kind, Effect: effect)); if (S.Combat == null) Do(1, new("decline")); Check(S.Combat != null, "Close attack did not reach the post-roll choice"); }
    }
    public static void Run(string root)
    {
        void Case(string name, Action<Session> test) { test(new(root, name)); Console.WriteLine("CLOSE SCENARIO PASS " + name); }
        Case("close-target-and-facing-validation", s =>
        {
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s0-0" ? u with { Facing = 3 } : u).ToList() };
            s.Do(0, new("attack", "s0-0", "s1-3", Card: "order-01", AttackKind: "ram"), false);
            s.C.Ships["core:taiidan-interceptor"] = s.C.Ships["core:taiidan-interceptor"] with { Class = "fighter" };
            s.Do(0, new("attack", "s0-0", "s1-3", Card: "order-01", AttackKind: "board"), false);
            s.Check(s.S.Orders0 == 0 && s.S.Cards!.Discard.Length == 0, "Invalid close attack spent a card");
        });
        Case("boarding-armor-crew-and-buff-timing", s =>
        {
            s.C.Ships["core:kushan-interceptor"] = s.C.Ships["core:kushan-interceptor"] with { Success = 3 };
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s1-3" ? u with { CrewDamage = 1, FocusFire = 3, Effects = ["lockdown", "brace"] } : u).ToList() };
            s.Attack("board"); s.S = s.S with { Combat = s.S.Combat! with { Rolls = [6, 6, 6, 6, 1, 2] } }; s.Do(0, new("resolve"));
            var unit = s.S.Units.Single(u => u.Id == "s1-3"); s.Check(unit.Models == 5 && unit.CrewDamage == 2 && unit.FocusFire == 2 && (unit.Effects?.Length ?? 0) == 0, "Boarding armor/crew damage order was wrong");
        });
        Case("boarding-creates-drifting-hulk", s =>
        {
            s.C.Ships["core:taiidan-interceptor"] = s.C.Ships["core:taiidan-interceptor"] with { Armor = 1 };
            s.Attack("board"); s.S = s.S with { Combat = s.S.Combat! with { Rolls = [1, 1, 1, 1, 1, 1] } }; s.Do(0, new("resolve"));
            var hulk = s.S.Units.Single(u => u.Id == "s1-3"); s.Check(hulk.Hulk && hulk.CrewDamage == 6 && hulk.Models == 6 && s.E.GetActions(s.S, 1, hulk.Id).MoveCells.Length == 0, "Hulk could still receive orders");
        });
        Case("ram-collision-after-target-destruction-and-overrun", s =>
        {
            s.C.Ships["core:taiidan-interceptor"] = s.C.Ships["core:taiidan-interceptor"] with { Armor = 1 };
            s.C.Ships["core:kushan-interceptor"] = s.C.Ships["core:kushan-interceptor"] with { Evasive = 20 };
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s1-3" ? u with { Models = 1 } : u).ToList() };
            s.Attack("ram"); s.Do(0, new("valor")); s.S = s.S with { Combat = s.S.Combat! with { Rolls = [6, 6, 6, 6, 6, 6] } }; uint expected = s.S.RandomState;
            int[] collisionRolls = [FleetRuleTools.Roll(ref expected), FleetRuleTools.Roll(ref expected)]; s.Do(0, new("resolve"));
            s.Check(!s.S.Units.Any(u => u.Id == "s1-3") && s.S.Combat is { Kind: "collision", Side: 1 } && s.S.Combat.Rolls.SequenceEqual(collisionRolls), "Destroyed target did not collide, or collision used Evasive rerolls");
            s.S = s.S with { Combat = s.S.Combat! with { Rolls = [1, 1] } }; s.Do(1, new("resolve"));
            s.Check(s.S.Overrun is { Side: 0 } && s.S.Stars0 == 4, "Close kill did not offer overrun/award stars");
            s.Do(1, new("overrun"), false); s.Do(0, new("overrun"));
            s.Check(s.S.Units.Single(u => u.Id == "s0-0") is { Q: 3, R: 2 } && s.S.Cards!.Response == null && s.S.Orders0 == 1, "Overrun failed to finish the original paid order");
        });
        Case("fighter-ram-suicide-and-prearmor-debuff", s =>
        {
            s.C.Ships["core:kushan-interceptor"] = s.C.Ships["core:kushan-interceptor"] with { Class = "fighter", Ram = 1, Afterburners = 2 };
            s.C.Ships["core:taiidan-interceptor"] = s.C.Ships["core:taiidan-interceptor"] with { Armor = 50 };
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s0-0" ? u with { Facing = 3, Models = 1 } : u).ToList() };
            s.Attack("ram", "engines"); s.Check(s.S.Combat!.Rolls.Length == 3, "Strike ram did not add Afterburners");
            s.S = s.S with { Combat = s.S.Combat with { Rolls = [6, 2, 2] } }; s.Do(0, new("resolve"));
            s.Check(!s.S.Units.Any(u => u.Id == "s0-0") && FleetRuleTools.Has(s.S.Units.Single(u => u.Id == "s1-3"), "engines") && s.S.Combat == null && s.S.Stars1 == 2, "Fighter ram did not cripple before armor or remove the ramming fighter");
        });
        Case("scenario-gated-boarding-capture", s =>
        {
            s.C.Maps[s.S.Map] = s.C.Maps[s.S.Map] with { CaptureTargets = ["s1-3"] };
            s.C.Ships["core:taiidan-interceptor"] = s.C.Ships["core:taiidan-interceptor"] with { Armor = 0 };
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s1-3" ? u with { Models = 1 } : u).ToList() };
            s.Attack("board"); s.S = s.S with { Combat = s.S.Combat! with { Rolls = [6, 6, 6, 6, 6, 6] } }; s.Do(0, new("capture-result"));
            s.Check(s.S.Units.Single(u => u.Id == "s1-3").Side == 0 && s.S.Units.Single(u => u.Id == "s0-0").CrewDamage == 1 && s.S.Stars0 == 0, "Capture did not transfer control/crew cost or incorrectly awarded kill stars");
        });
        Case("one-close-attack-per-order-window", s =>
        {
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s0-0" ? u with { AttackWindow = FleetClose.Window(s.S) } : u).ToList() };
            s.Do(0, new("attack", "s0-0", "s1-3", Card: "order-01", AttackKind: "board"), false);
        });
        Case("crew-transfer-recovers-enemy-hulk", s =>
        {
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s1-3" ? u with { CrewDamage = 6, Hulk = true } : u).ToList() };
            s.Do(0, new("transfer-crew", "s0-0", "s1-3", Card: "order-01", Amount: 1), false);
            s.Do(0, new("transfer-crew", "s0-0", "s1-3", Card: "order-01", Amount: 2));
            var recovered = s.S.Units.Single(u => u.Id == "s1-3");
            s.Check(!recovered.Hulk && recovered.Side == 0 && recovered.CrewDamage == 4 && s.S.Units.Single(u => u.Id == "s0-0").CrewDamage == 2 && s.S.Orders0 == 1 && s.S.Stars0 == 0, "Crew transfer failed to recover the hulk or spent incorrectly");
        });
        Case("leader-transfer-with-activation-repair", s =>
        {
            s.C.Leaders["core:transfer-test"] = new("core:transfer-test", "Test", "kushan", ["frigate"], Hand: 2, Play: 1, CoolUnderPressure: 1);
            s.S = s.S with { RandomState = 1, Units = s.S.Units.Select(u => u.Id == "s0-0" ? u with { Leader = "core:transfer-test", FocusFire = 1 } : u.Id == "s0-1" ? u with { Q = 2, R = 3 } : u).ToList() };
            s.Do(0, new("transfer-crew", "s0-0", "s0-1", Card: "order-01", TransferLeader: true, Repair: "ask"));
            s.Check(s.S.Repairs is { Length: 1 } && s.S.Repairs[0].Rolls.Length == 2 && s.S.Units.Single(u => u.Id == "s0-1").Leader == "core:transfer-test", "Transfer lost the activating leader's repair bonus");
            s.Do(0, new("repair-choice", Repair: "focus"));
            s.Check(s.S.Cards!.Pending == null && s.S.Units.Single(u => u.Id == "s0-0").Leader == "" && s.S.Cards.PlayLimit0 == 1, "Transfer failed to finish or changed the round's frozen budget");
        });
    }
}
