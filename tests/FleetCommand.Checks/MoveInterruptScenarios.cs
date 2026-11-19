using System.Text.Json;
using FleetCommand.Core;
internal static class MoveInterruptScenarios
{
    private const string Move0 = "core:interrupt-move#1", Move1 = "core:interrupt-move#2";
    private sealed class Session
    {
        public ContentCatalog C; public BattleEngine E; public BattleState S;
        private readonly string _path; private readonly List<object> _trace = [];
        public Session(string root, string name)
        {
            C = ContentCatalog.Load([Path.Combine(root, "content", "base")]); BasicCardBattle.Register(C); E = new(C); S = E.Create(BasicCardBattle.OrdersMapId, 17);
            foreach (var ship in C.Ships.Values.Where(d => d.Id.StartsWith("practice:orders-")).ToArray()) C.Ships[ship.Id] = ship with { Armor = 50, Success = 2, Abilities = [], Weapons = [new("gun", "Move test gun", 1, 5)] };
            C.Ships["practice:orders-kushan-boarder"] = C.Ships["practice:orders-kushan-boarder"] with { Move = 3 };
            C.Ships["practice:orders-taiidan-boarder"] = C.Ships["practice:orders-taiidan-boarder"] with { Move = 3 };
            S = S with { Units = S.Units.Select(u => u.Id == "s0-0" ? u with { Q = 1, R = 1 } : u.Id == "s1-3" ? u with { Q = 2, R = 2 } : u).ToList() };
            Hands(["core:all-ahead-full#1", Move0, "core:interrupt-attack#1", "core:interrupt-counterintelligence#1", "core:interrupt-repairs#1"],
                ["core:all-ahead-full#2", Move1, "core:interrupt-attack#2", "core:interrupt-counterintelligence#2", "core:interrupt-repairs#2"]);
            _path = Path.Combine(root, "tmp", "test-results", name + ".trace.json");
        }
        public BattleUnit Unit(string id) => S.Units.Single(u => u.Id == id);
        public void Hands(string[] a, string[] b) => S = S with { Cards = S.Cards! with { Phase = "orders", Hand0 = a, Hand1 = b, Discard = [], Deck = CommandCards.Instances(CommandCards.Deck(C, S)).Keys.Except(a).Except(b).ToArray() } };
        public void Do(int side, BattleCommand cmd, bool accepted = true)
        {
            string before = E.Save(S); var a = E.Apply(S, side, cmd); var b = E.Apply(E.Load(before), side, cmd);
            _trace.Add(new { side, cmd, a.Accepted, a.Message, a.State }); File.WriteAllText(_path, JsonSerializer.Serialize(_trace, JsonData.Options));
            Check(a.Accepted == accepted, a.Message); Check(E.Save(S) == before && E.Save(a.State) == E.Save(b.State), "Move interrupt mutated input or replay diverged"); S = E.Load(E.Save(a.State));
            foreach (int viewer in new[] { 0, 1 }) { var view = E.Load(E.Save(BasicCardRules.View(S, viewer))); Check(view.RandomState == 1 && !E.Apply(view, viewer, cmd).Accepted, "Move interrupt view gained authority"); }
        }
        public void Start(int q = 4, int r = 1) => Do(0, new("move", "s0-0", Q: q, R: r, Card: "core:all-ahead-full#1"));
        public void Move(int q = 3, int r = 1, string unit = "s1-3") => Do(1, new("interrupt-move", unit, Q: q, R: r, Card: Move1));
        public void Settle(bool stopAtReplan = true)
        {
            for (int i = 0; i < 100; i++)
                if (S.Cards?.Counter is { } counter) Do(1 - counter.Side, new("decline-counter"));
                else if (S.Combat is { } combat) Do(FleetCombat.DecisionSide(combat), new(combat.AbilityRerolls > 0 ? "ability-reroll" : "resolve", Dice: []));
                else if (S.Defense is { } defense) Do(1 - defense.Side, new("decline-defense"));
                else if (S.CapEntry is { } cap) Do(cap.Side, new("decline-cap"));
                else if (S.Overrun is { } overrun) Do(overrun.Side, new("decline-overrun"));
                else if (S.Cards?.Response is { Replan: true } replan) { if (stopAtReplan) return; Do(replan.Side, new("stop-move")); }
                else if (S.Cards?.Response is { } pause) Do(InterruptRules.Responder(S), new("decline"));
                else return;
            throw new Exception("Move interrupt did not settle");
        }
        public void LaunchMap()
        {
            var cards = S.Cards; S = E.Create(BasicCardBattle.LaunchOrdersMapId, 17) with { Cards = cards };
            C.Ships["practice:launch-taiidan-wing"] = C.Ships["practice:launch-taiidan-wing"] with { Armor = 50, Success = 2, Weapons = [new("gun", "Arrival test gun", 1, 5)] };
            S = S with { Units = S.Units.Select(u => u.Id == "s1-9" ? u with { Q = 3, R = 2 } : u).ToList() };
        }
        public void InvalidSave(BattleState state) { bool rejected = false; try { E.Load(E.Save(state)); } catch (InvalidDataException) { rejected = true; } Check(rejected, "Malformed movement save loaded"); }
    }
    public static void Run(string root)
    {
        int count = 0;
        void Case(string name, Action<Session> test) { try { test(new(root, name)); } catch (Exception e) { throw new Exception(name + ": " + e.Message, e); } count++; Console.WriteLine("MOVE INTERRUPT PASS " + name); }
        Case("move-interrupt-proposal-counter-and-remaining-route", s =>
        {
            s.Start(); uint rng = s.S.RandomState; Check(s.S.Cards!.Response!.MoveSpent == 1, "Initial movement cost missing");
            s.Move(); Check(s.S.Cards.Counter?.Command.Kind == "interrupt-move" && s.Unit("s1-3") is { Q: 2, R: 2 }, "Movement applied before counter opportunity");
            s.Settle(); Check(s.Unit("s1-3") is { Q: 3, R: 1, Activated: false } && s.Unit("s0-0") is { Q: 2, R: 1 } && s.S.Cards.Response is { Replan: true, MoveSpent: 1 } && s.S.Cards.MoveInterrupt == null, "Parent action or activation lost");
            Check(s.S.RandomState == rng && s.S.Orders0 == 1 && s.S.Orders1 == 0, "Interrupt spent RNG, Play or activation");
            s.Do(0, new("resume-move", "s0-0", Q: 4, R: 1), false); // Blocked route needs three more points.
            s.Do(0, new("resume-move", "s0-0", Q: 4, R: 0)); s.Settle(); Check(s.Unit("s0-0") is { Q: 4, R: 0 } && s.S.Cards.Response == null, "Revised route did not finish");
        });
        Case("move-interrupt-range-owner-and-destination-rejections", s =>
        {
            s.Start(); s.Do(0, new("interrupt-move", "s0-2", Q: 1, R: 2, Card: Move0), false);
            s.Do(1, new("interrupt-move", "s1-4", Q: 6, R: 4, Card: Move1), false); s.Do(1, new("interrupt-move", "s1-3", Q: 9, R: 0, Card: Move1), false);
            s.Do(1, new("interrupt-move", "s0-0", Q: 1, R: 0, Card: Move1), false); s.Do(1, new("interrupt-move", "s1-3", Q: 3, R: 1, Card: "core:interrupt-attack#2"), false);
        });
        Case("move-interrupt-counter-cancels-without-moving", s => { s.Start(); s.Move(); s.Do(0, new("counter", Card: "core:interrupt-counterintelligence#1")); s.Settle(); Check(s.Unit("s1-3") is { Q: 2, R: 2 } && s.Unit("s0-0") is { Q: 4, R: 1 } && !InterruptRules.Available(s.C, s.S, 1, "move") && s.S.Cards!.Discard.Contains(Move1), "Cancelled move changed position or left type reusable"); });
        Case("move-interrupt-preserves-buff-and-activation", s =>
        {
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s1-3" ? u with { Activated = true, Effects = ["brace"] } : u).ToList() };
            s.Start(); s.Move(); s.Settle(); Check(s.Unit("s1-3").Activated && FleetRuleTools.Has(s.Unit("s1-3"), "brace"), "Interrupt issued a new order"); s.Do(0, new("stop-move"));
        });
        Case("move-interrupt-does-not-run-activation-repair", s =>
        {
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s1-3" ? u with { FocusFire = 4 } : u).ToList() };
            s.Start(); uint rng = s.S.RandomState; s.Move(); s.Settle(); Check(s.Unit("s1-3").FocusFire == 4 && s.S.RandomState == rng, "Interrupt ran Damage Control"); s.Do(0, new("stop-move"));
        });
        Case("move-interrupt-can-turn-in-place", s => { s.Start(); s.Do(1, new("interrupt-move", "s1-3", Q: 2, R: 2, Facing: 4, AutoFacing: false, Card: Move1)); s.Settle(); Check(s.Unit("s1-3") is { Q: 2, R: 2, Facing: 4 } && s.S.Cards!.Response?.Replan == true, "Turn-in-place reaction failed"); s.Do(0, new("stop-move")); });
        Case("move-interrupt-straight-course-is-retained", s =>
        {
            s.Do(0, new("special", "s0-0", Card: "core:all-ahead-full#1")); s.Do(0, new("move", "s0-0", Q: 4, R: 1)); s.Move(); s.Settle();
            s.Do(0, new("resume-move", "s0-0", Q: 3, R: 0), false); s.Do(0, new("stop-move")); Check(s.S.Cards!.Pending?.Step == 1 && s.S.Cards.Pending.Direction == 0, "Straight order lost step/course"); s.Do(0, new("finish-order"));
        });
        Case("move-interrupt-weighted-cost-is-not-refunded", s =>
        {
            s.C.Objects["test:slow"] = new("test:slow", "Slow field", MoveCost: 2, Kind: "nebula"); s.C.Maps[s.S.Map] = s.C.Maps[s.S.Map] with { Objects = [new("test:slow", 2, 1)] };
            s.Start(3, 1); Check(s.S.Cards!.Response!.MoveSpent == 2, "Terrain cost not recorded"); s.Move(1, 2); s.Settle();
            s.Do(0, new("resume-move", "s0-0", Q: 4, R: 1), false); s.Do(0, new("resume-move", "s0-0", Q: 3, R: 1)); s.Settle(); Check(s.Unit("s0-0").Q == 3, "Remaining terrain budget wrong");
        });
        Case("move-interrupt-lightning-budget-and-order-step", s =>
        {
            s.Hands(["core:intercept#1", Move0, "core:interrupt-attack#1", "core:interrupt-counterintelligence#1"], [Move1, "core:interrupt-attack#2"]);
            var ship = s.C.Ships[s.Unit("s0-0").Definition]; s.C.Ships[ship.Id] = ship with { Afterburners = 2 };
            s.Do(0, new("special", "s0-0", Card: "core:intercept#1")); s.Do(0, new("skip")); s.Do(0, new("move", "s0-0", Q: 3, R: 1)); s.Move(1, 2); s.Settle();
            Check(s.S.Cards!.Pending?.Step == 1 && s.S.Cards.Response?.MoveBudget == 2, "Lightning order context lost"); s.Do(0, new("resume-move", "s0-0", Q: 4, R: 1), false);
            s.Do(0, new("resume-move", "s0-0", Q: 3, R: 1)); s.Settle(); Check(s.S.Cards.Pending?.Step == 2, "Lightning continuation skipped attack"); s.Do(0, new("finish-order"));
        });
        Case("move-interrupt-two-hex-endpoint-budgets", s =>
        {
            var ship = s.C.Ships[s.Unit("s0-0").Definition]; s.C.Ships[ship.Id] = ship with { Class = "capital", Footprint = 2, Move = 2 };
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s0-0" ? u with { Q = 2, R = 2, Leader = "" } : u.Id == "s1-3" ? u with { Q = 4, R = 3 } : u.Id == "s1-6" ? u with { Q = 7, R = 0 } : u).ToList() };
            s.Start(4, 2); s.Move(4, 1); s.Settle(); Check(s.S.Cards!.Response is { FrontSpent: 1, RearSpent: 1 }, "Two-end travel already spent was lost");
            s.Do(0, new("resume-move", "s0-0", Q: 5, R: 2), false); s.Do(0, new("resume-move", "s0-0", Q: 4, R: 2)); s.Settle(); Check(s.Unit("s0-0").Q == 4, "Two-hex continuation failed");
        });
        Case("move-interrupt-nested-opposing-moves", s =>
        {
            s.Start(); s.Move(1, 2); s.Do(0, new("decline-counter")); Check(s.S.Cards!.MoveInterrupt != null && s.S.ActiveSide == 1, "First reaction context not installed");
            s.Do(0, new("interrupt-move", "s0-0", Q: 2, R: 0, Card: Move0)); s.Do(1, new("decline-counter")); s.Settle();
            Check(s.S.Cards!.MoveInterrupt is { Parent: null, Side: 1 } && s.S.Cards.Response?.Replan == true && s.Unit("s0-0").R == 0, "Nested context did not restore outer reaction");
            s.Do(1, new("stop-move")); s.Settle(); Check(s.S.Cards.MoveInterrupt == null && s.S.Cards.Response is { Replan: true, MoveSpent: 1, Side: 0 }, "Root movement context did not restore");
            var h = s.E.GetActions(s.S, 0, "s0-0").MoveCells.First(); s.Do(0, new("resume-move", "s0-0", Q: h.Q, R: h.R)); s.Settle(); Check(s.S.Orders0 == 1 && s.S.Orders1 == 0, "Nested moves spent Play");
        });
        Case("move-interrupt-reinforce-arrival-and-parent-resume", s =>
        {
            s.LaunchMap(); s.Do(0, new("begin-reinforce", "s0-0", "launch", Card: "core:all-ahead-full#1")); Check(s.S.Cards!.Response == null, "Carrier interrupted before arrival");
            s.Do(0, new("launch", "s0-1", Q: 2, R: 2)); Check(s.S.Cards.Response is { Command.Kind: "reinforce", Command.Unit: "s0-1" }, "Reinforce arrival has no correct trigger/target");
            s.Move(4, 2, "s1-9"); s.Settle(); Check(s.S.Reinforce is { Mode: "launch" } && s.S.Cards.Response == null && s.Unit("s0-0") is { Activated: false, Launches: 1 }, "Parent Reinforce context not restored"); s.Do(0, new("finish-reinforce"));
        });
        Case("move-interrupt-reserve-deployment-window", s => { s.LaunchMap(); s.Do(0, new("begin-reinforce", "s0-3", "deploy", Card: "core:all-ahead-full#1")); s.Do(0, new("deploy", Q: 0, R: 1)); Check(s.S.Reinforce != null && s.S.Cards!.Response is { Command.Kind: "reinforce", Command.Unit: "s0-3" }, "Reserve arrival finished before reactions"); s.Settle(); Check(s.S.Reinforce == null && s.S.Orders0 == 1, "Reserve response did not finish order"); });
        Case("move-interrupt-factory-arrival-window", s => { s.LaunchMap(); s.Do(0, new("begin-reinforce", "s0-0", "build", Card: "core:all-ahead-full#1")); s.Do(0, new("build", Definition: "practice:launch-kushan-wing", Q: 2, R: 2)); Check(s.S.Cards!.Response is { Command.Kind: "reinforce", Command.Unit: "s0-10" }, "Factory output did not open arrival window"); s.Settle(); Check(s.S.Reinforce == null && s.Unit("s0-0").Cargo == 4, "Factory continuation lost payment"); });
        Case("move-interrupt-malformed-context-and-budget", s =>
        {
            s.Start(); s.Move(); s.Do(0, new("decline-counter")); var cards = s.S.Cards!; var frame = cards.MoveInterrupt!;
            s.InvalidSave(s.S with { Cards = cards with { MoveInterrupt = frame with { ParentSide = frame.Side } } });
            s.InvalidSave(s.S with { Cards = cards with { MoveInterrupt = frame with { Card = "core:interrupt-attack#2" } } });
            s.InvalidSave(s.S with { Cards = cards with { Response = cards.Response! with { MoveSpent = -1 } } });
            s.Settle(); s.InvalidSave(s.S with { Cards = s.S.Cards! with { Response = s.S.Cards.Response! with { MoveSpent = 4 } } }); s.Do(1, new("stop-move"), false); s.Do(0, new("stop-move"));
        });
        Case("printed-interrupts-do-not-trigger-on-an-attack", s =>
        {
            s.S = s.S with { ValorOwner = 0 }; s.Do(0, new("attack", "s0-0", "s1-3", Card: "core:all-ahead-full#1"));
            Check(s.S.Combat != null && s.S.Cards!.Counter == null, "Published Attack/Move offered during an ordinary attack");
            s.Do(1, new("interrupt", "s1-3", "s0-0", Card: "core:interrupt-attack#2"), false); s.Do(1, new("interrupt-move", "s1-3", Q: 1, R: 2, Card: Move1), false); s.Settle();
        });
        Case("printed-interrupt-attack-targets-arriving-unit", s =>
        {
            s.LaunchMap(); s.Do(0, new("begin-reinforce", "s0-0", "launch", Card: "core:all-ahead-full#1")); s.Do(0, new("launch", "s0-1", Q: 2, R: 2));
            s.Do(1, new("interrupt", "s1-9", "s0-0", Card: "core:interrupt-attack#2"), false);
            s.Do(1, new("interrupt", "s1-9", "s0-1", Card: "core:interrupt-attack#2")); s.Settle();
            Check(InterruptRules.Played(s.S.Cards!, 1).Contains("core:interrupt-attack#2") && s.S.Orders1 == 0 && !s.Unit("s0-0").Activated, "Arrival response reordered carrier or spent Play");
            s.Do(0, new("finish-reinforce"));
        });
        Case("printed-ready-to-launch-arrival-is-interruptible", s =>
        {
            s.LaunchMap(); s.Hands(["core:ready-to-launch#1", Move0, "core:interrupt-attack#1", "core:interrupt-counterintelligence#1"], [Move1, "core:interrupt-attack#2"]);
            s.Do(0, new("special", "s0-1", Card: "core:ready-to-launch#1")); s.Do(0, new("launch", "s0-1", Q: 2, R: 2));
            Check(s.S.Cards!.Response is { Command.Kind: "reinforce", Basic: false } && s.S.Cards.Pending?.Step == 1, "Ready to Launch skipped its arrival opportunity");
            s.Settle(); Check(s.S.Cards.Pending?.Step == 1 && s.S.Cards.Response == null, "Arrival response advanced past Lightning Move"); s.Do(0, new("finish-order"));
        });
        Case("move-interrupt-range-uses-both-hull-hexes", s =>
        {
            var ship = s.C.Ships[s.Unit("s1-3").Definition]; s.C.Ships[ship.Id] = ship with { Class = "capital", Footprint = 2, Move = 2 };
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s1-3" ? u with { Q = 6, R = 1, Facing = 0, Leader = "" } : u).ToList() };
            s.Start(); s.Move(6, 2); Check(s.S.Cards!.Counter != null, "Nearest stern hex at range three was ignored"); s.Settle(); s.Do(0, new("stop-move"));
        });
        Case("move-interrupt-entry-mine-can-destroy-reactor", s =>
        {
            s.S = s.S with { ValorOwner = 0, Units = s.S.Units.Select(u => u.Id == "s1-3" ? u with { Q = 7, R = 1 } : u.Id == "s1-5" ? u with { Q = 2, R = 2, Models = 1 } : u).ToList() };
            var ship = s.C.Ships[s.Unit("s1-5").Definition]; s.C.Ships[ship.Id] = ship with { Armor = 0, Evasive = 0, Weapons = [] };
            s.C.Maps[s.S.Map] = s.C.Maps[s.S.Map] with { Objects = [new("practice:mines", 1, 2, Owner: 0)] };
            s.Start(); s.Move(1, 2, "s1-5"); s.Do(0, new("decline-counter"));
            if (s.S.Combat != null)
            {
                s.S = s.S with { Combat = s.S.Combat with { Rolls = Enumerable.Repeat(6, s.S.Combat.Rolls.Length).ToArray(), AbilityRerolls = 0, RerollSide = -1 } };
                s.Do(FleetCombat.DecisionSide(s.S.Combat), new("resolve"));
            }
            s.Settle(); Check(!s.S.Units.Any(u => u.Id == "s1-5") && s.S.Cards!.MoveInterrupt == null && s.S.Cards.Response?.Replan == true, "Destroyed reactor lost the parent move"); s.Do(0, new("stop-move"));
        });
        Case("interrupt-crippled-engines-revises-remaining-budget", s =>
        {
            s.S = s.S with { ValorOwner = 1, Units = s.S.Units.Select(u => u.Id == "s1-3" ? u with { Q = 7, R = 1 } : u.Id == "s1-5" ? u with { Q = 2, R = 2, Models = 1 } : u).ToList() };
            var ship = s.C.Ships[s.Unit("s1-5").Definition]; s.C.Ships[ship.Id] = ship with { Ram = 1, Evasive = 0 };
            s.Start(); s.Do(1, new("interrupt", "s1-5", "s0-0", Card: "core:interrupt-attack#2", AttackKind: "ram", Effect: "engines")); s.Do(0, new("decline-counter"));
            s.S = s.S with { Combat = s.S.Combat! with { Rolls = Enumerable.Repeat(6, s.S.Combat!.Rolls.Length).ToArray(), AbilityRerolls = 0, RerollSide = -1 } };
            s.Do(FleetCombat.DecisionSide(s.S.Combat!), new("resolve")); s.Settle(); Check(FleetRuleTools.Has(s.Unit("s0-0"), "engines") && s.S.Cards!.Response?.Replan == true && s.E.GetActions(s.S, 0, "s0-0").MoveCells.Length == 0, "Crippled mover kept full remaining allowance"); s.Do(0, new("stop-move"));
        });
        Case("printed-deployment-ram-destruction-and-return-collision", s =>
        {
            s.LaunchMap(); var attacker = s.C.Ships[s.Unit("s1-9").Definition];
            s.C.Ships["practice:move-rammer"] = attacker with { Id = "practice:move-rammer", Class = "frigate", Ram = 12, Armor = 50, Success = 2, Evasive = 0, Weapons = [] };
            var target = s.S.Reserves!.Single(u => u.Id == "s0-3"); s.C.Ships[target.Definition] = s.C.Ships[target.Definition] with { Armor = 0, Ram = 1, Weapons = [], ProwArmor = false };
            s.S = s.S with { ValorOwner = 1, Units = s.S.Units.Select(u => u.Id == "s1-9" ? u with { Definition = "practice:move-rammer", Q = 1, R = 1, Facing = 3 } : u).ToList() };
            s.Do(0, new("begin-reinforce", "s0-3", "deploy", Card: "core:all-ahead-full#1")); s.Do(0, new("deploy", Q: 0, R: 1));
            s.Do(1, new("interrupt", "s1-9", "s0-3", Card: "core:interrupt-attack#2", AttackKind: "ram")); s.Do(0, new("decline-counter"));
            s.S = s.S with { Combat = s.S.Combat! with { Rolls = Enumerable.Repeat(6, s.S.Combat!.Rolls.Length).ToArray(), AbilityRerolls = 0, RerollSide = -1 } };
            s.Do(1, new("resolve")); s.Settle(); Check(!s.S.Units.Any(u => u.Id == "s0-3") && s.S.Reinforce == null && s.S.Cards!.Response == null && s.S.Orders0 == 1, "Destroyed reinforcement left a pending order");
        });
        Case("nested-move-can-destroy-original-mover-in-terrain", s =>
        {
            var actor = s.C.Ships[s.Unit("s0-0").Definition]; s.C.Ships[actor.Id] = actor with { Armor = 0, Weapons = [], Evasive = 0, Models = 1 };
            s.C.Maps[s.S.Map] = s.C.Maps[s.S.Map] with { Objects = [new("practice:mines", 3, 0, Owner: 1)] };
            s.Do(0, new("special", "s0-0", Card: "core:all-ahead-full#1")); s.Do(0, new("move", "s0-0", Q: 4, R: 1)); s.Move(); s.Do(0, new("decline-counter"));
            s.Do(0, new("interrupt-move", "s0-0", Q: 3, R: 0, Card: Move0)); s.Do(1, new("decline-counter"));
            s.Settle(false); Check(!s.S.Units.Any(u => u.Id == "s0-0") && s.S.Cards!.MoveInterrupt == null && s.S.Cards.Response == null && s.S.Cards.Pending == null && s.S.Orders0 == 1, "Nested terrain death lost the suspended original action");
        });
        Case("move-interrupt-can-embark-without-activation", s =>
        {
            s.LaunchMap(); s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s1-5" ? u with { Q = 4, R = 2 } : u).ToList() };
            var carrier = s.C.Ships[s.Unit("s1-5").Definition]; s.C.Ships[carrier.Id] = carrier with { Bays = 4 };
            s.Do(0, new("begin-reinforce", "s0-0", "launch", Card: "core:all-ahead-full#1")); s.Do(0, new("launch", "s0-1", Q: 2, R: 2));
            Check(s.E.GetActions(s.S, 1, "s1-9").MoveCells.Contains(new(4, 2)), "Reachable bay missing from reaction preview");
            s.Move(4, 2, "s1-9"); s.Settle(); Check(!s.S.Units.Any(u => u.Id == "s1-9") && s.S.Reserves!.Single(u => u.Id == "s1-9") is { Embarked: "s1-5", Activated: false }, "Reaction embark changed activation or lost unit"); s.Do(0, new("finish-reinforce"));
        });
        Case("nested-move-original-fighter-can-end-in-cap", s =>
        {
            var actor = s.C.Ships[s.Unit("s0-0").Definition]; s.C.Ships[actor.Id] = actor with { Class = "fighter" };
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s0-0" ? u with { Leader = "" } : u.Id == "s0-1" ? u with { Q = 2, R = 0 } : u).ToList() };
            s.Start(); s.Move(); s.Do(0, new("decline-counter")); s.Do(0, new("interrupt-move", "s0-0", Q: 3, R: 0, Card: Move0)); s.Do(1, new("decline-counter"));
            for (int n = 0; n < 10 && s.S.CapEntry == null; n++) s.Do(1, new("decline"));
            Check(s.S.CapEntry?.Unit == "s0-0", "Nested movement lost the CAP option"); s.Do(0, new("cap-enter", Target: "s0-1")); s.Settle(false);
            Check(s.Unit("s0-0").Patrol == "s0-1" && s.S.Cards!.Response == null && s.S.Cards.MoveInterrupt == null && s.S.Orders0 == 1, "CAP fighter resumed an on-board movement path");
        });
        Console.WriteLine($"MOVE_INTERRUPTS_OK {count} scenarios");
    }
    private static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
}
