using System.Text.Json;
using FleetCommand.Core;
internal static class CounterScenarios
{
    private const string Counter = "core:interrupt-counterintelligence#1", Attack = "core:interrupt-attack#1";
    private sealed class Session
    {
        public ContentCatalog C; public BattleEngine E; public BattleState S;
        private readonly string _path; private readonly List<object> _trace = [];
        public Session(string root, string name)
        {
            C = ContentCatalog.Load([Path.Combine(root, "content", "base")]); BasicCardBattle.Register(C);
            foreach (string id in new[] { "practice:orders-kushan-boarder", "practice:orders-taiidan-boarder" }) C.Ships[id] = C.Ships[id] with { Armor = 50, Move = 3, Weapons = [new("gun", "Counter test gun", 2, 5)] };
            foreach (var ship in C.Ships.Values.Where(d => d.Id.StartsWith("practice:orders-") && d.Faction == "taiidan").ToArray()) C.Ships[ship.Id] = ship with { Move = 0 };
            E = new(C); S = E.Create(BasicCardBattle.OrdersMapId, 17);
            string[] a = [Counter, "core:all-ahead-full#1"], b = [Attack, "core:interrupt-attack#2"];
            S = S with { Cards = S.Cards! with { Phase = "orders", Hand0 = a, Hand1 = b, Discard = [], Deck = CommandCards.Instances(CommandCards.Deck(C, S)).Keys.Except(a).Except(b).ToArray() },
                Units = S.Units.Select(u => u.Id == "s0-0" ? u with { Q = 1, R = 1 } : u.Id == "s1-3" ? u with { Q = 2, R = 2, Activated = true } : u).ToList() };
            _path = Path.Combine(root, "tmp", "test-results", name + ".trace.json");
        }
        public void Do(int side, BattleCommand cmd, bool accepted = true)
        {
            string before = E.Save(S); var a = E.Apply(S, side, cmd); var b = E.Apply(E.Load(before), side, cmd);
            _trace.Add(new { side, cmd, a.Accepted, a.Message, a.State }); File.WriteAllText(_path, JsonSerializer.Serialize(_trace, JsonData.Options));
            Check(a.Accepted == accepted, a.Message); Check(E.Save(S) == before && E.Save(a.State) == E.Save(b.State), "Counter mutated input or replay diverged"); S = E.Load(E.Save(a.State));
            foreach (int viewer in new[] { 0, 1 }) { var view = E.Load(E.Save(BasicCardRules.View(S, viewer))); Check(view.RandomState == 1 && !E.Apply(view, viewer, cmd).Accepted, "Counter view gained authority"); }
        }
        public void Move() => Do(0, new("move", "s0-0", Q: 3, R: 1, Card: "core:all-ahead-full#1"));
        public void Offer() { Move(); Do(1, new("interrupt", "s1-3", "s0-0", Card: Attack)); }
        public void Cancel() => Do(0, new("counter", Card: Counter));
        public void Settle()
        {
            for (int i = 0; i < 30; i++)
                if (S.Cards?.Counter is { } counter) Do(1 - counter.Side, new("decline-counter"));
                else if (S.Combat is { } combat) Do(FleetCombat.DecisionSide(combat), new(combat.AbilityRerolls > 0 ? "ability-reroll" : "resolve", Dice: []));
                else if (S.Defense is { } defense) Do(1 - defense.Side, new("decline-defense"));
                else if (S.Cards?.Response is { } pause) Do(1 - pause.Side, new("decline"));
                else return;
            throw new Exception("Counter did not settle");
        }
        public void InvalidSave(BattleState state) { bool rejected = false; try { E.Load(E.Save(state)); } catch (InvalidDataException) { rejected = true; } Check(rejected, "Malformed counter save loaded"); }
    }
    public static void Run(string root)
    {
        int count = 0;
        void Case(string name, Action<Session> test) { try { test(new(root, name)); } catch (Exception e) { throw new Exception(name + ": " + e.Message, e); } count++; Console.WriteLine("COUNTER PASS " + name); }
        Case("counter-before-effect-and-saved-movement", s =>
        {
            s.Move(); uint random = s.S.RandomState; s.Do(1, new("interrupt", "s1-3", "s0-0", Card: Attack));
            Check(s.S.Cards!.Counter != null && s.S.Combat == null && s.S.RandomState == random, "Attack rolled before counter opportunity");
            Check(s.E.GetActions(s.S, 1, "s1-3") == UnitActions.Empty || s.E.GetActions(s.S, 1, "s1-3").AttackTargets.Length == 0, "Actions available during counter choice");
            s.Cancel(); Check(s.S.Cards.Counter == null && s.S.Cards.Response == null && s.S.Units.Single(u => u.Id == "s0-0").Q == 3 && s.S.RandomState == random, "Cancelled reaction damaged or lost movement");
            Check(s.S.Cards.Discard.Contains(Counter) && s.S.Cards.Discard.Contains(Attack) && InterruptRules.Played(s.S.Cards, 1).Length == 0, "Cancelled cards not discarded");
            Check(s.S.Orders0 == 1 && s.S.Orders1 == 0 && s.S.Units.Single(u => u.Id == "s1-3").Activated, "Counter changed Play/activation");
        });
        Case("counter-decline-executes-original-interrupt", s => { s.Offer(); s.S = s.S with { ValorOwner = 1 }; s.Do(0, new("decline-counter")); Check(s.S.Cards!.Counter == null && s.S.Combat?.Interrupt == true, "Declining counter skipped interrupt"); s.Settle(); Check(s.S.Cards.Response == null, "Original movement did not finish"); });
        Case("counter-cancelled-types-remain-used", s => { s.Offer(); s.Cancel(); Check(!InterruptRules.Available(s.C, s.S, 0, "counter") && !InterruptRules.Available(s.C, s.S, 1), "Cancelled types reusable in same round"); });
        Case("counter-wrong-side-and-card-rejected", s => { s.Offer(); s.Do(1, new("counter", Card: Counter), false); s.Do(0, new("counter", Card: Attack), false); s.Do(0, new("move", "s0-0", Q: 3, R: 1), false); s.Cancel(); });
        Case("counter-cannot-counter-counterintelligence", s => { s.Offer(); s.Cancel(); s.Do(1, new("counter", Card: "core:interrupt-counterintelligence#2"), false); });
        Case("counter-window-independent-of-hidden-hand", s =>
        { s.S = s.S with { Cards = s.S.Cards! with { Hand0 = ["core:all-ahead-full#1"], Deck = s.S.Cards.Deck.Append(Counter).ToArray() } }; s.Offer(); Check(s.S.Cards!.Counter != null, "Counter window revealed absence of a hidden card"); s.Do(0, new("counter", Card: Counter), false); s.Do(0, new("decline-counter")); s.Settle(); });
        Case("counter-invalid-interrupt-weapon-spends-nothing", s => { s.Move(); s.Do(1, new("interrupt", "s1-3", "s0-0", Card: Attack, Weapon: "missing"), false); Check(s.S.Cards!.Counter == null && s.S.Cards.Hand1.Contains(Attack), "Invalid reaction consumed card"); });
        Case("counter-malformed-saves-rejected", s =>
        {
            s.Offer(); var cards = s.S.Cards!; var frame = cards.Counter!;
            s.InvalidSave(s.S with { Cards = cards with { Counter = frame with { Side = 2 } } });
            s.InvalidSave(s.S with { Cards = cards with { Counter = frame with { Command = frame.Command with { Card = Counter } } } });
            s.InvalidSave(s.S with { Cards = cards with { Counter = frame with { Command = frame.Command with { Weapon = "missing" } } } });
            s.InvalidSave(s.S with { Cards = cards with { Counter = frame with { Command = frame.Command with { Power = true } } } });
            s.InvalidSave(s.S with { Cards = cards with { UsedInterrupts1 = ["attack", "attack"] } });
        });
        Case("counter-type-use-resets-next-round", s =>
        {
            s.Offer(); s.Cancel(); for (int i = 0; i < 8 && s.S.Round == 1; i++) s.Do(s.S.ActiveSide, new("pass"));
            if (s.S.Cards!.Phase == "cleanup") { s.Do(0, new("cleanup", Cards: [])); s.Do(1, new("cleanup", Cards: [])); }
            Check(s.S.Round == 2 && InterruptRules.Used(s.S.Cards!, 0).Length == 0 && InterruptRules.Used(s.S.Cards, 1).Length == 0, "Interrupt type use survived new round");
        });
        Console.WriteLine($"COUNTERS_OK {count} scenarios");
    }
    private static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
}
