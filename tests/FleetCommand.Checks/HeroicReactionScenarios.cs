using System.Text.Json;
using FleetCommand.Core;
internal static class HeroicReactionScenarios
{
    private const string Leader = "core:hero-kushan-recon-commander", Hero = Leader + "#hero-0";
    private sealed class Session
    {
        public ContentCatalog C; public BattleEngine E; public BattleState S;
        private readonly string _path; private readonly List<object> _trace = [];
        public Session(string root, string name)
        {
            C = ContentCatalog.Load([Path.Combine(root, "content", "base")]); BasicCardBattle.Register(C); E = new(C); S = E.Create(HeroicBattle.ReactionMapId, 3);
            foreach (string id in S.Units.Concat(S.Reserves ?? []).Select(u => u.Definition).Distinct()) C.Ships[id] = C.Ships[id] with { Armor = 50, Success = 2, Evasive = 0, Weapons = [new("gun", "Pilot launch test gun", 2, 6, PowerDice: 2)] };
            S = S with { ActiveSide = 1, ValorOwner = 0, Cards = S.Cards! with { Phase = "orders", UsedInterrupts0 = ["attack", "move", "counter"], UsedInterrupts1 = ["attack", "move", "counter"] } };
            _path = Path.Combine(root, "tmp", "test-results", name + ".trace.json");
        }
        public BattleUnit Unit(string id) => S.Units.Concat(S.Reserves ?? []).Single(u => u.Id == id);
        public void Do(int side, BattleCommand cmd, bool accepted = true)
        {
            string before = E.Save(S); var a = E.Apply(S, side, cmd); var b = E.Apply(E.Load(before), side, cmd);
            _trace.Add(new { side, cmd, a.Accepted, a.Message, a.State }); File.WriteAllText(_path, JsonSerializer.Serialize(_trace, JsonData.Options));
            Check(a.Accepted == accepted, a.Message); Check(E.Save(S) == before && E.Save(a.State) == E.Save(b.State), "Heroic reaction replay mutated input or diverged"); S = E.Load(E.Save(a.State));
            foreach (int sideView in new[] { 0, 1 }) { var view = E.Load(E.Save(BasicCardRules.View(S, sideView))); Check(view.RandomState == 1 && !E.Apply(view, sideView, cmd).Accepted, "Heroic reaction view gained authority"); }
        }
        public void EnemyMove()
        {
            string card = S.Cards!.Hand1.First(); Do(1, new("begin-order", "s1-2", "move", Card: card)); Do(1, new("move", "s1-2", Q: 4, R: 3));
            Check(S.Cards!.Response?.Command.Unit == "s1-2", "Enemy Move did not offer Heroic reaction");
        }
        public void Begin() => Do(0, new("heroic-interrupt", "s0-0", "s1-2", Card: Hero));
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
            throw new Exception("Heroic reaction continuation did not settle");
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
        void Case(string name, Action<Session> test) { try { test(new(root, name)); } catch (Exception e) { throw new Exception(name + ": " + e.Message, e); } count++; Console.WriteLine("HEROIC REACTION PASS " + name); }
        Case("recon-corrected-profile-and-heroic-trigger", s =>
        {
            Check(s.C.Leaders[Leader] is { Stars: 2, Hand: 2, Play: 1, CoolUnderPressure: 2, Afterburners: 1, CloseDice: 0 } && SpecialOrders.Lightning(s.C, s.Unit("s0-0")) == 2, "Corrected Recon profile or Afterburners missing");
            Check(InterruptRules.Eligible(s.C, s.S, Hero, new(1, new("move", "s1-2"), true, [])) && !InterruptRules.Eligible(s.C, s.S, Hero, new(1, new("attack", "s1-2", "s0-0"), true, [])) && !InterruptRules.Eligible(s.C, s.S, Hero, new(1, new("reinforce", "s1-2"), true, [])), "Rapid Reaction trigger exceeds printed Move");
        });
        Case("rapid-reaction-move-attack-and-paid-parent-resume", s =>
        {
            s.EnemyMove(); var original = s.S.Cards!.Response!; s.Begin(); Check(s.S.ActiveSide == 0 && s.S.Orders0 == 0 && !s.Unit("s0-0").Activated && s.S.Cards!.HeroicUsed!.Contains(Hero), "Heroic start spent activation/Play or missed ledger");
            s.Do(0, new("move", "s0-0", Q: 3, R: 3)); s.Settle(); Check(s.S.Cards!.Pending?.Step == 1, "Heroic Move failed to reach Attack");
            s.Do(0, new("attack", "s0-0", "s1-3", Weapon: "gun"), false); s.Do(0, new("attack", "s0-0", "s1-2", Weapon: "gun")); s.Do(0, new("resolve"));
            Check(s.S.Cards!.HeroicInterrupt == null && s.S.Cards.Pending?.BasicAction == "move" && s.S.Cards.Response is { Replan: true } && s.S.Cards.Response.MoveSpent == original.MoveSpent && s.S.ActiveSide == 1, "Paid parent order or remaining movement budget was lost");
            s.Do(1, new("resume-move", "s1-2", Q: 4, R: 3)); s.Settle(); Check(s.S.Cards!.Pending == null && s.S.Orders0 == 0 && s.S.Orders1 == 1 && !s.Unit("s0-0").Activated, "Heroic reaction consumed extra Play/activation");
        });
        Case("rapid-reaction-same-hero-on-two-sides-binds-correct-owner", s =>
        {
            var fleet = s.C.Fleets["practice:reaction-kushan"]; s.C.Fleets["practice:reaction-mirror"] = fleet with { Id = "practice:reaction-mirror", Units = fleet.Units.Select((u, i) => u with { Q = 6, R = i == 0 ? 3 : 5, Facing = 3 }).ToArray() };
            s.C.Maps[s.S.Map] = s.C.Maps[s.S.Map] with { Fleets = ["practice:reaction-kushan", "practice:reaction-mirror"] }; s.S = s.E.Create(s.S.Map, 3);
            s.S = s.S with { ActiveSide = 0, ValorOwner = 1, Cards = s.S.Cards! with { Phase = "orders", UsedInterrupts0 = ["attack", "move", "counter"], UsedInterrupts1 = ["attack", "move", "counter"] } };
            string other = Leader + "#hero-1"; Check(HeroicInterrupts.Owner(s.C, s.S, other)?.Id == "s1-2" && HeroicInterrupts.Owner(s.C, s.S, Hero)?.Id == "s0-0", "Owner lookup selected the other side's identical Hero");
            s.Do(0, new("begin-order", "s0-0", "move", Card: s.S.Cards!.Hand0.First(id => !HeroicCommands.IsHeroic(s.S, id)))); s.Do(0, new("move", "s0-0", Q: 3, R: 3)); s.Do(1, new("heroic-interrupt", "s1-2", "s0-0", Card: other)); s.Do(1, new("finish-order")); s.Settle();
            Check(s.S.Cards!.HeroicUsed!.Contains(other) && s.S.Cards.Hand0.Contains(Hero), "Mirrored Heroic spending consumed the opponent's card");
        });
        Case("rapid-reaction-preserves-activated-hero-and-buffs", s =>
        {
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s0-0" ? u with { Activated = true, Effects = ["brace"], CrewDamage = 1 } : u).ToList() }; s.EnemyMove(); uint random = s.S.RandomState; s.Begin(); s.Do(0, new("skip")); s.Do(0, new("finish-order")); s.Settle();
            Check(s.Unit("s0-0").Activated && FleetRuleTools.Has(s.Unit("s0-0"), "brace") && s.Unit("s0-0").CrewDamage == 1 && s.S.RandomState == random && s.S.Repairs == null, "Reaction ran NewOrder, activation control or changed readiness");
        });
        Case("rapid-reaction-rejects-owner-side-and-target-before-spending", s =>
        {
            s.EnemyMove(); s.Do(0, new("heroic-interrupt", "s0-1", "s1-2", Card: Hero), false); s.Do(0, new("heroic-interrupt", "s0-0", "s1-3", Card: Hero), false); s.Do(1, new("heroic-interrupt", "s0-0", "s1-2", Card: Hero), false);
            Check(s.S.Cards!.Hand0.Contains(Hero) && s.S.Cards.HeroicUsed!.Length == 0, "Rejected Heroic declaration spent card");
        });
        Case("rapid-reaction-countered-card-stays-outside-discard", s =>
        {
            s.Give(1, "core:interrupt-counterintelligence#1"); s.S = s.S with { Cards = s.S.Cards! with { UsedInterrupts1 = ["attack", "move"] } }; s.EnemyMove(); s.Begin();
            Check(s.S.Cards!.Counter?.Command.Kind == "heroic-interrupt" && s.S.Cards.HeroicInterrupt == null, "Counter did not precede Heroic effects"); s.Do(1, new("counter", Card: "core:interrupt-counterintelligence#1")); s.Settle();
            Check(s.Unit("s0-0").Q == 2 && s.S.Cards!.HeroicUsed!.Contains(Hero) && !s.S.Cards.Discard.Contains(Hero) && s.S.Cards.Discard.Contains("core:interrupt-counterintelligence#1"), "Counter recycled Heroic card or executed movement");
        });
        Case("rapid-reaction-declined-counter-starts-saved-actions", s =>
        {
            s.S = s.S with { Cards = s.S.Cards! with { UsedInterrupts1 = ["attack", "move"] } }; s.EnemyMove(); s.Begin(); s.Do(1, new("decline-counter")); Check(s.S.Cards!.HeroicInterrupt != null && s.S.ActiveSide == 0, "Declined counter did not start Heroic actions"); s.Do(0, new("finish-order")); s.Settle();
        });
        Case("rapid-reaction-independent-of-normal-interrupt-types", s =>
        {
            s.EnemyMove(); s.Begin(); Check(InterruptRules.Used(s.S.Cards!, 0).SequenceEqual(["attack", "move", "counter"]), "Heroic declaration altered ordinary type ledger"); s.Do(0, new("finish-order")); s.Settle();
            Check(s.S.Cards!.HeroicUsed!.Contains(Hero) && !s.S.Cards.Hand0.Contains(Hero) && InterruptRules.Played(s.S.Cards, 0).Length == 0, "Heroic card entered ordinary reaction pile");
        });
        Case("rapid-reaction-normal-move-can-nest-and-restore-heroic-order", s =>
        {
            s.Give(1, "core:interrupt-move#1"); s.S = s.S with { Cards = s.S.Cards! with { UsedInterrupts1 = ["attack", "counter"] } }; s.EnemyMove(); s.Begin(); s.Do(0, new("move", "s0-0", Q: 3, R: 3));
            Check(s.S.Cards!.Response?.Side == 0, "Heroic Move suppressed enemy reactions"); s.Do(1, new("interrupt-move", "s1-2", Q: 5, R: 4, Card: "core:interrupt-move#1")); s.Settle();
            Check(s.S.Cards!.HeroicInterrupt != null && s.S.Cards.Pending?.Card == Hero && s.S.Cards.Pending.Step == 1, "Nested normal Move lost Heroic continuation"); s.Do(0, new("finish-order")); s.Settle(); Check(s.S.Cards!.HeroicInterrupt == null && s.S.Cards.MoveInterrupt == null, "Nested reaction frames survived completion");
        });
        Case("rapid-reaction-cannot-be-played-as-special-on-own-turn", s =>
        { s.S = s.S with { ActiveSide = 0 }; s.Do(0, new("special", "s0-0", Card: Hero), false); s.Do(0, new("heroic-interrupt", "s0-0", "s1-2", Card: Hero), false); });
        Case("rapid-reaction-basic-payment-has-no-extra-actions", s =>
        { s.S = s.S with { ActiveSide = 0 }; s.Do(0, new("begin-order", "s0-1", "move", Card: Hero)); Check(s.S.Cards!.Pending?.BasicAction == "move" && s.S.Cards.HeroicInterrupt == null && s.S.Orders0 == 1, "Basic payment started Heroic reaction"); s.Do(0, new("finish-order")); });
        Case("rapid-reaction-absent-owner-produces-no-public-window", s =>
        {
            s.S = s.S with { Units = s.S.Units.Where(u => u.Id != "s0-0").ToList() }; string card = s.S.Cards!.Hand1.First(); s.Do(1, new("begin-order", "s1-2", "move", Card: card)); s.Do(1, new("move", "s1-2", Q: 4, R: 3)); Check(s.S.Cards!.Response == null, "Dead Hero still offered a reaction");
        });
        Case("rapid-reaction-does-not-repair-instead-of-attack", s =>
        {
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s0-0" ? u with { Effects = ["weapons"] } : u).ToList() }; s.EnemyMove(); s.Begin(); s.Do(0, new("skip")); s.Do(0, new("damage-control", Repair: "weapons"), false); s.Do(0, new("finish-order")); s.Settle(); Check(FleetRuleTools.Has(s.Unit("s0-0"), "weapons"), "Reaction repaired crippled weapons");
        });
        Case("rapid-reaction-terrain-entry-keeps-both-continuations", s =>
        {
            s.C.Maps[s.S.Map] = s.C.Maps[s.S.Map] with { Objects = [new("practice:mines", 3, 3)] }; s.EnemyMove(); s.Begin(); uint random = s.S.RandomState;
            s.Do(0, new("move", "s0-0", Q: 3, R: 3)); s.Settle(); Check(s.S.RandomState != random && s.S.Cards!.HeroicInterrupt != null && s.S.Cards.Pending?.Step == 1, "Terrain skipped or lost Heroic Attack"); s.Do(0, new("finish-order")); s.Settle(); Check(s.S.Cards!.HeroicInterrupt == null && s.S.Orders1 == 1, "Terrain lost the paid enemy continuation");
        });
        Case("rapid-reaction-killed-mover-cancels-only-its-parent-order", s =>
        {
            string id = s.Unit("s1-2").Definition; s.C.Ships[id] = s.C.Ships[id] with { Armor = 0 }; string heroShip = s.Unit("s0-0").Definition; s.C.Ships[heroShip] = s.C.Ships[heroShip] with { Weapons = [new("gun", "Lethal reaction fixture", 20, 6)] };
            s.EnemyMove(); s.Begin(); s.Do(0, new("skip")); s.Do(0, new("attack", "s0-0", "s1-2", Weapon: "gun")); s.Settle();
            Check(!s.S.Units.Any(u => u.Id == "s1-2") && s.S.Cards!.Pending == null && s.S.Cards.Response == null && s.S.Cards.HeroicInterrupt == null && s.S.Orders0 == 0 && s.S.Orders1 == 1, "Destroyed mover left a paid order or reaction frame stranded");
        });
        Case("rapid-reaction-forged-suspended-context-rejected", s =>
        {
            s.EnemyMove(); s.Begin(); var f = s.S.Cards!.HeroicInterrupt!;
            s.Invalid(s.S with { Cards = s.S.Cards with { HeroicInterrupt = f with { Target = "s1-3" } } }); s.Invalid(s.S with { Cards = s.S.Cards with { HeroicInterrupt = f with { ParentSide = 0 } } });
            s.Invalid(s.S with { Cards = s.S.Cards with { HeroicInterrupt = f with { ParentPose = new(new(99, 99), 0) } } });
            s.Invalid(s.S with { Cards = s.S.Cards with { Pending = null } }); s.Do(0, new("finish-order")); s.Settle();
        });
        Console.WriteLine($"HEROIC_REACTION_OK {count} scenarios");
    }
    private static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
}
