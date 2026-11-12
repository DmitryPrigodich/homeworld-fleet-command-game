using FleetCommand.Core;

internal static class CommandCardScenarios
{
    private static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
    private static void Reject(Action action)
    {
        try { action(); } catch (InvalidDataException) { return; }
        throw new Exception("Expected invalid-data rejection");
    }
    public static void Run(string root)
    {
        int count = 0;
        ContentCatalog Load() => ContentCatalog.Load([Path.Combine(root, "content", "base"), Path.Combine(root, "tests", "fixtures", "card-mod")]);
        void Case(string name, Action<ContentCatalog> run)
        {
            try { run(Load()); } catch (Exception e) { throw new Exception(name + ": " + e.Message, e); }
            count++; Console.WriteLine("COMMAND CARD PASS " + name);
        }
        BattleState Hands(ContentCatalog c, BattleState s, string[] a, string[] b) => s with
        {
            ActiveSide = 0, Cards = new(CommandCards.Instances(CommandCards.Deck(c, s)).Keys.Except(a).Except(b).ToArray(), a, b, [], Phase: "orders")
        };
        BattleState Do(BattleEngine e, BattleState s, int side, BattleCommand command)
        {
            string before = e.Save(s); var a = e.Apply(s, side, command); var b = e.Apply(e.Load(before), side, command);
            Check(a.Accepted, a.Message);
            Check(e.Save(s) == before && e.Save(a.State) == e.Save(b.State), "Mutation or save/replay divergence");
            foreach (int viewer in new[] { 0, 1 })
            {
                var view = e.Load(e.Save(BasicCardRules.View(a.State, viewer)));
                Check(view.Cards!.Deck.Length == 0 && view.RandomState == 1 && !e.Apply(view, viewer, command).Accepted, "Private view acquired authority");
            }
            return e.Load(e.Save(a.State));
        }
        Case("mod-load-hash-and-runtime-copy", c =>
        {
            var b = ContentCatalog.Load([Path.Combine(root, "content", "base")]);
            Check(c.Fingerprint != b.Fingerprint, "Card mod absent from gameplay hash");
            var copy = c.CopyForRuntime(); copy.Cards["cardtest:maneuver"] = copy.Cards["cardtest:maneuver"] with { Name = "Renamed" };
            Check(c.Cards["cardtest:maneuver"].Name != "Renamed" && copy.Decks.Count == c.Decks.Count, "Catalog copies lost/isolate card definitions incorrectly");
        });
        Case("configured-deck-and-arbitrary-instance-ids", c =>
        {
            var e = new BattleEngine(c); var s = e.Create("cardtest:sector", 5); e.Load(e.Save(s));
            Check(s.Cards!.Deck.Length == 6 && s.Cards.Hand0.Concat(s.Cards.Hand1).All(id => id.StartsWith("cardtest:")), "Hardcoded 24-card deck survived");
            Check(InterruptRules.IsAttack(c, s, "cardtest:reaction#1") && SpecialOrders.Name(c, s, "cardtest:maneuver#1") == "Test coordinated maneuver", "Instance ID still determines the effect/name");
            Reject(() => e.Load(e.Save(s with { Cards = s.Cards with { Deck = s.Cards.Deck.Skip(1).ToArray() } })));
            Reject(() => e.Load(e.Save(s with { Cards = s.Cards with { Deck = s.Cards.Deck.Append("order-01").ToArray() } })));
        });
        Case("variable-action-lines-and-skips", c =>
        {
            var e = new BattleEngine(c); var s = Hands(c, e.Create("cardtest:sector", 1), ["cardtest:maneuver#1"], ["cardtest:basic#1"]);
            s = Do(e, s, 0, new("special", "s0-0", Card: "cardtest:maneuver#1", OtherUnit: "s0-1"));
            Check(SpecialOrders.Steps(c, s, s.Cards!.Pending!) == 3, "Hardcoded four-action sequence survived");
            s = Do(e, s, 0, new("skip"));
            Check(SpecialOrders.Actor(c, s, s.Cards!.Pending!) == "s0-1" && SpecialOrders.Action(c, s, s.Cards.Pending!) == "attack", "Second line begins at wrong step");
            s = Do(e, s, 0, new("skip"));
            Check(SpecialOrders.Action(c, s, s.Cards!.Pending!) == "move", "Data-defined final move missing");
            s = Do(e, s, 0, new("skip"));
            Check(s.Cards!.Pending == null && s.Orders0 == 1 && s.Cards.Discard.Contains("cardtest:maneuver#1"), "Order did not spend exactly one Play/card");
        });
        Case("optional-second-line-offset", c =>
        {
            var e = new BattleEngine(c); var s = Hands(c, e.Create("cardtest:sector"), ["cardtest:maneuver#1"], []);
            s = Do(e, s, 0, new("special", Card: "cardtest:maneuver#1", OtherUnit: "s0-1"));
            Check(s.Cards!.Pending!.Step == 1 && SpecialOrders.Action(c, s, s.Cards.Pending) == "attack", "Omitted first line assumes two actions");
            s = Do(e, s, 0, new("finish-order")); Check(s.Cards!.Pending == null, "Early finish failed");
        });
        Case("single-line-free-course-movement", c =>
        {
            var e = new BattleEngine(c); var s = Hands(c, e.Create("cardtest:sector"), ["cardtest:free-course#1"], []);
            s = Do(e, s, 0, new("special", "s0-0", Card: "cardtest:free-course#1"));
            var u = s.Units.Single(u => u.Id == "s0-0");
            var h = e.GetActions(s, 0, u.Id).MoveCells.First(h => h.Q != u.Q && h.R != u.R && h.Q + h.R != u.Q + u.R);
            s = Do(e, s, 0, new("move", u.Id, Q: h.Q, R: h.R));
            while (s.Cards!.Response is { } r) s = Do(e, s, 1 - r.Side, new("decline"));
            Check(s.Cards.Pending!.Step == 1 && s.Cards.Pending.Direction == -1, "Single-line card inherited All Ahead Full constraints");
        });
        Case("custom-interrupt-and-per-type-limit", c =>
        {
            var e = new BattleEngine(c); var s = Hands(c, e.Create("cardtest:sector", 1), ["cardtest:basic#1"], ["cardtest:reaction#1", "cardtest:reaction#2"]);
            s = s with { Units = s.Units.Where(u => u.Id is "s0-0" or "s1-3").Select(u => u with { Q = u.Side == 0 ? 2 : 3, R = 3, Activated = u.Side == 1 }).ToList() };
            s = Do(e, s, 0, new("attack", "s0-0", "s1-3", Card: "cardtest:basic#1"));
            Check(s.Cards!.Response != null, "Response window missing");
            s = Do(e, s, 1, new("interrupt", "s1-3", "s0-0", Card: "cardtest:reaction#1"));
            Check(InterruptRules.Played(s.Cards!, 1).Contains("cardtest:reaction#1") && !InterruptRules.Available(c, s, 1), "Attack type limit depends on physical copy ID");
        });
        Case("small-deck-recycling-and-deterministic-refill", c =>
        {
            var e = new BattleEngine(c); var s = e.Create("cardtest:sector", 42); int commands = 0;
            while (s.Round < 6 || s.Cards!.Phase == "cleanup")
            {
                Check(++commands < 150, "Round did not terminate");
                var cards = s.Cards!;
                int side = cards.Phase is "initiative" or "cleanup" ? cards.Ready0 ? 1 : 0 : s.ActiveSide;
                s = Do(e, s, side, cards.Phase == "cleanup" ? new("cleanup", Cards: side == 0 ? cards.Hand0 : cards.Hand1) : new(cards.Phase == "initiative" ? "initiative" : "pass"));
            }
            Check(s.Cards!.Hand0.Length == 2 && s.Cards.Hand1.Length == 2, "Refill exhausted without recycling");
        });
        Case("unknown-effects-and-malformed-decks-rejected", c =>
        {
            var card = c.Cards["cardtest:maneuver"]; var deck = c.Decks["cardtest:deck"];
            c.Cards[card.Id] = card with { Lines = [new(["imaginary-effect"])] }; Reject(c.Validate);
            c.Cards[card.Id] = card with { Interrupt = "repair" }; Reject(c.Validate);
            c.Cards[card.Id] = card;
            c.Decks[deck.Id] = deck with { Entries = [new("missing:card")] }; Reject(c.Validate);
            c.Decks[deck.Id] = deck with { Entries = [new(card.Id, 2, ["duplicate", "duplicate"])] }; Reject(c.Validate);
            c.Decks[deck.Id] = deck with { Entries = [new(card.Id, 2, ["one"])] }; Reject(c.Validate);
            c.Decks[deck.Id] = deck with { Entries = [new(card.Id, 513)] }; Reject(c.Validate);
            c.Decks[deck.Id] = deck; c.Rules["cardtest:rules"] = c.Rules["cardtest:rules"] with { Deck = "missing:deck" }; Reject(c.Validate);
        });
        Console.WriteLine($"COMMAND_CARDS_OK {count} scenarios");
    }
}
