using System.Text.Json;
using FleetCommand.Core;

internal static class PublishedOrderScenarios
{
    private sealed class Session
    {
        public ContentCatalog C; public BattleEngine E; public BattleState S;
        private readonly string _path; private readonly List<object> _trace = [];
        public Session(string root, string name)
        {
            C = ContentCatalog.Load([Path.Combine(root, "content", "base")]); BasicCardBattle.Register(C); C.Validate();
            var map = C.Maps[BasicCardBattle.OrdersMapId];
            foreach (int side in new[] { 0, 1 })
            foreach (var u in C.Fleets[map.Fleets[side]].Units.Take(2))
            {
                var ship = C.Ships[u.Ship];
                C.Ships[ship.Id] = ship with { Models = 2, Class = "frigate", Success = 2, Armor = side == 0 ? 2 : 10, Crew = 3, Ram = 2,
                    Afterburners = 2, Abilities = [], Evasive = 0, Weapons = [new("gun", "Synthetic test gun", 2, 5, PowerDice: 3, PowerPenetrating: 1)] };
            }
            E = new(C); S = E.Create(map.Id, 17);
            var places = new Dictionary<string, Hex> { ["s0-0"] = new(2, 2), ["s0-1"] = new(2, 3), ["s0-2"] = new(1, 4), ["s1-3"] = new(3, 2), ["s1-4"] = new(4, 2), ["s1-5"] = new(8, 4), ["s1-6"] = new(8, 1) };
            S = S with { ActiveSide = 0, ValorOwner = 0, Units = S.Units.Select(u => u with { Q = places[u.Id].Q, R = places[u.Id].R }).ToList() };
            _path = Path.Combine(root, "tmp", "test-results", name + ".trace.json");
        }
        public void Hand(params string[] cards)
        {
            string[] enemy = ["core:all-ahead-full#2"];
            S = S with { Cards = S.Cards! with { Hand0 = cards, Hand1 = enemy, Phase = "orders", Discard = [], Interrupts0 = null, Interrupts1 = null,
                Deck = CommandCards.Instances(CommandCards.Deck(C, S)).Keys.Except(cards).Except(enemy).ToArray(), Pending = null, Response = null } };
        }
        public void Do(int side, BattleCommand cmd, bool accepted = true)
        {
            string before = E.Save(S); var a = E.Apply(S, side, cmd); var b = E.Apply(E.Load(before), side, cmd);
            _trace.Add(new { side, cmd, a.Accepted, a.Message, a.State }); File.WriteAllText(_path, JsonSerializer.Serialize(_trace, JsonData.Options));
            Check(a.Accepted == accepted, a.Message); Check(E.Save(S) == before && E.Save(a.State) == E.Save(b.State), "Input mutation or save/replay divergence");
            S = E.Load(E.Save(a.State));
            foreach (int viewer in new[] { 0, 1 })
            {
                var view = E.Load(E.Save(BasicCardRules.View(S, viewer)));
                Check(view.Cards!.Deck.Length == 0 && view.RandomState == 1 && !E.Apply(view, viewer, cmd).Accepted, "Private snapshot gained authority");
            }
        }
        public void Start(string card, string actor = "s0-0", string other = "", string repair = "auto")
        { Hand("core:" + card + "#1"); Do(0, new("special", actor, Card: "core:" + card + "#1", OtherUnit: other, CardRepair: repair)); }
        public void Roll(string actor = "s0-0", string target = "s1-3", string kind = "ranged")
        {
            Do(0, new("attack", actor, target, AttackKind: kind));
            if (S.Combat == null) Do(1, new("decline"));
            Check(S.Combat != null, "Attack did not reach a saved dice decision");
        }
        public void Fixed(params int[] rolls)
        { S = S with { Combat = S.Combat! with { Rolls = rolls, AbilityRerolls = 0, RerollSide = -1 } }; Do(S.Combat!.Side, new("resolve")); }
        public void Settle()
        {
            for (int i = 0; i < 40; i++)
            {
                if (S.Combat is { } combat) Do(FleetCombat.DecisionSide(combat), new(combat.AbilityRerolls > 0 ? "ability-reroll" : "resolve", Dice: []));
                else if (S.Defense is { } d) Do(1 - d.Side, new("decline-defense"));
                else if (S.CapEntry is { } cap) Do(cap.Side, new("decline-cap"));
                else if (S.Overrun is { } overrun) Do(overrun.Side, new("decline-overrun"));
                else if (S.Cards?.Response is { } response) Do(1 - response.Side, new("decline"));
                else return;
            }
            throw new Exception("Continuation did not settle");
        }
        public BattleUnit Unit(string id) => S.Units.Single(u => u.Id == id);
        public void At(string id, Hex h) => S = S with { Units = S.Units.Select(u => u.Id == id ? u with { Q = h.Q, R = h.R } : u).ToList() };
    }
    private static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
    public static void Run(string root)
    {
        int count = 0;
        void Case(string name, Action<Session> test)
        { try { test(new(root, name)); } catch (Exception e) { throw new Exception(name + ": " + e.Message, e); } count++; Console.WriteLine("PUBLISHED ORDER PASS " + name); }
        Case("available-order-deck-conservation", s =>
        {
            var deck = CommandCards.Deck(s.C, s.S); Check(deck.Entries.Length == 17 && CommandCards.Instances(deck).Count == 47, "Wrong supported-card inventory");
            Check(deck.Entries.Single(e => e.Card == "core:weapons-to-maximum").Copies == 6, "Printed copy count lost");
            s.E.Load(s.E.Save(s.S));
        });
        foreach (var (card, sequence) in new[] { ("intercept", "move,lightning,attack"), ("hit-and-run", "move,attack,lightning"), ("weapons-to-maximum", "move,power") })
            Case("printed-sequence-" + card, s =>
            {
                s.Start(card); var actions = new List<string>();
                while (s.S.Cards!.Pending is { } order) { actions.Add(SpecialOrders.Action(s.C, s.S, order)); s.Do(0, new("skip")); }
                Check(string.Join(',', actions) == sequence && s.S.Orders0 == 1, "Incorrect action sequence or multiple Play spending");
            });
        Case("power-attack-granted-and-saved", s =>
        {
            s.Start("weapons-to-maximum"); s.Do(0, new("skip")); s.Roll();
            Check(s.S.Combat!.Power && s.S.Cards!.Response!.Command.Power && s.S.Combat.Rolls.Length == 7, "Power flag or extra weapon dice missing"); s.Fixed(6);
        });
        Case("power-attack-cannot-be-forged", s =>
        { s.Start("target-engines"); s.Do(0, new("skip")); s.Do(0, new("attack", "s0-0", "s1-3", Power: true), false); });
        foreach (string effect in new[] { "engines", "weapons", "bays" })
            Case("debuff-before-armor-" + effect, s =>
            {
                s.Start("target-" + effect); s.Do(0, new("skip")); s.Roll(); s.Fixed(6);
                Check(s.Unit("s1-3").Models == 2 && FleetRuleTools.Has(s.Unit("s1-3"), effect), "Armor incorrectly prevented the card's debuff");
                Check(s.S.Log!.Any(e => e.Text.Contains("Target system crippled: " + effect)), "System damage is absent from the event log");
                if (effect == "weapons") Check(!FleetClose.Allowed(s.C, s.S, s.Unit("s1-3"), s.Unit("s0-0"), "board"), "Crippled Weapons still permits a boarding attack");
            });
        Case("debuff-needs-a-success", s =>
        { s.Start("target-engines"); s.Do(0, new("skip")); s.Roll(); s.Fixed(1); Check(!FleetRuleTools.Has(s.Unit("s1-3"), "engines"), "A failed attack applied a debuff"); });
        Case("debuff-fighter-immunity", s =>
        {
            s.At("s1-3", new(8, 3)); s.At("s1-5", new(3, 2)); s.Start("target-bays"); s.Do(0, new("skip")); s.Roll(target: "s1-5"); s.Fixed(6);
            Check(!FleetRuleTools.Has(s.Unit("s1-5"), "bays"), "A fighter received the targeted debuff");
        });
        foreach (var (card, buff) in new[] { ("brace-for-impact", "brace"), ("repel-boarders", "lockdown"), ("evasive-pattern-delta", "evasive") })
            Case("buff-start-and-new-order-expiry-" + buff, s =>
            {
                s.Start(card); Check(FleetRuleTools.Has(s.Unit("s0-0"), buff), "Card buff missing at activation");
                s.Do(0, new("finish-order")); Check(FleetRuleTools.Has(s.Unit("s0-0"), buff), "Buff expired at order completion");
                s.S = s.S with { ActiveSide = 0, Orders0 = 0, Units = s.S.Units.Select(u => u with { Activated = false }).ToList() };
                s.Start("intercept"); Check(!FleetRuleTools.Has(s.Unit("s0-0"), buff), "A new order retained the old buff");
            });
        Case("buff-assignment-restrictions", s =>
        {
            s.Hand("core:brace-for-impact#1"); s.Do(0, new("special", "s0-2", Card: "core:brace-for-impact#1"), false);
            s.Hand("core:repel-boarders#1"); s.Do(0, new("special", "s0-2", Card: "core:repel-boarders#1"), false);
            var ship = s.C.Ships[s.Unit("s0-0").Definition]; s.C.Ships[ship.Id] = ship with { Afterburners = 0 };
            s.Hand("core:evasive-pattern-delta#1"); s.Do(0, new("special", "s0-0", Card: "core:evasive-pattern-delta#1"), false);
        });
        Case("choice-move-locks-and-resumes", s =>
        {
            s.Start("brace-for-impact"); var options = s.E.GetActions(s.S, 0, "s0-0"); Check(options.MoveCells.Length > 0 && options.AttackTargets.Length > 0, "Choice did not offer both actions");
            var h = options.MoveCells.First(); s.Do(0, new("move", "s0-0", Q: h.Q, R: h.R));
            if (s.S.Cards!.Response != null) Check(s.S.Cards.Pending!.Choice == "move", "Choice lost during movement response");
            s.Settle(); Check(s.S.Cards!.Pending == null && s.S.Orders0 == 1, "Choice spent more than one action");
        });
        Case("choice-attack-locks-and-resumes", s =>
        { s.Start("repel-boarders"); s.Roll(); Check(s.S.Cards!.Pending!.Choice == "attack", "Attack choice not persisted"); s.Fixed(6); Check(s.S.Cards!.Pending == null, "Choice was executed twice"); });
        Case("buff-removed-by-focus-fire", s =>
        {
            s.Start("brace-for-impact"); s.Do(0, new("skip")); s.Do(1, new("attack", "s1-3", "s0-0", Card: "core:all-ahead-full#2"));
            if (s.S.Combat == null) s.Do(0, new("decline")); s.Fixed(1);
            Check(s.Unit("s0-0").FocusFire == 1 && !FleetRuleTools.Has(s.Unit("s0-0"), "brace"), "Focus Fire did not expire the buff");
        });
        Case("deploy-engineers-removes-one-chosen-token", s =>
        {
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s0-0" ? u with { FocusFire = 4, Effects = ["weapons", "engines"] } : u).ToList() };
            s.Start("deploy-engineers", repair: "weapons"); Check(!FleetRuleTools.Has(s.Unit("s0-0"), "weapons") && FleetRuleTools.Has(s.Unit("s0-0"), "engines") && s.Unit("s0-0").FocusFire == 4, "Engineers removed unchosen tokens");
            s.Do(0, new("finish-order")); Check(!FleetRuleTools.Has(s.Unit("s0-0"), "weapons"), "Corrected repair reverted at turn end");
        });
        Case("deploy-engineers-whole-focus-token-and-invalid-choice", s =>
        {
            s.S = s.S with { Units = s.S.Units.Select(u => u.Id == "s0-0" ? u with { FocusFire = 4 } : u).ToList() };
            s.Hand("core:deploy-engineers#1"); s.Do(0, new("special", "s0-0", Card: "core:deploy-engineers#1", CardRepair: "weapons"), false);
            s.Do(0, new("special", "s0-0", Card: "core:deploy-engineers#1", CardRepair: "focus")); Check(s.Unit("s0-0").FocusFire == 2, "Engineers did not remove a whole two-point token");
        });
        foreach (string card in new[] { "pincer-attack", "hammer-and-anvil" })
            Case("same-target-anchor-" + card, s =>
            {
                s.Start(card, other: "s0-1"); if (card == "pincer-attack") s.Do(0, new("skip")); s.Roll();
                Check(s.S.Cards!.Pending!.FirstTarget == "s1-3", "First target absent before dice"); s.Fixed(6); s.Do(0, new("skip"));
                var targets = s.E.GetActions(s.S, 0, "s0-1").AttackTargets;
                Check(targets.SequenceEqual(["s1-3"]), "Second actor can attack a different target");
                s.Do(0, new("attack", "s0-1", "s1-4"), false); s.Roll("s0-1"); s.Fixed(6);
                Check(s.S.Cards!.Pending == null && s.S.Orders0 == 1, "Two actors spent multiple orders");
            });
        Case("same-target-no-first-attack", s =>
        {
            s.Start("pincer-attack", other: "s0-1"); s.Do(0, new("skip")); s.Do(0, new("skip")); s.Do(0, new("skip"));
            Check(s.E.GetActions(s.S, 0, "s0-1").AttackTargets.Length == 0, "Omitted first attack left the second unrestricted");
        });
        Case("ship-assault-boarding-only-plus-two-dice", s =>
        {
            s.Start("ship-assault"); s.Do(0, new("move", "s0-0", Q: 3, R: 1)); s.Settle(); s.Roll();
            Check(s.S.Combat!.Kind == "board" && s.S.Combat.Rolls.Length == 8, "Assault did not force boarding with +2 dice");
            s.Fixed(6); Check(s.Unit("s0-0").LastAction == "board", "Assault used a ranged attack");
        });
        Case("ship-assault-needs-a-completed-move", s =>
        {
            s.Start("ship-assault"); s.Do(0, new("skip"));
            Check(s.E.GetActions(s.S, 0, "s0-0").AttackTargets.Length == 0, "Skipped movement granted a boarding trigger");
            s.Do(0, new("attack", "s0-0", "s1-3"), false);
        });
        Console.WriteLine($"PUBLISHED_ORDERS_OK {count} scenarios");
    }
}
