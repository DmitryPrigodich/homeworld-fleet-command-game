namespace FleetCommand.Core;

public sealed record CardRound(string[] Deck, string[] Hand0, string[] Hand1, string[] Discard,
    string Phase = "initiative", bool Ready0 = false, bool Ready1 = false, string Bid0 = "", string Bid1 = "",
    bool IsView = false, int HiddenHandCount = 0, int DeckCount = 0, PendingOrder? Pending = null,
    string[]? Interrupts0 = null, string[]? Interrupts1 = null, PendingResponse? Response = null,
    int HandLimit0 = 2, int HandLimit1 = 2, int PlayLimit0 = 1, int PlayLimit1 = 1, string[]? Bids0 = null, string[]? Bids1 = null);

public static class BasicCardBattle
{
    public const string MapId = "practice:cards";
    public const string TabletopMapId = "practice:rules";
    public const string TerrainMapId = "practice:hazards";
    public static void Register(ContentCatalog c)
    {
        c.Rules["practice:cards"] = new("practice:cards", "Basic card practice", "cards-basic-v1", OrdersPerSide: 1, RearShots: false);
        c.Maps[MapId] = c.Maps["core:training"] with { Id = MapId, Name = "Учебный бой с картами", Rules = "practice:cards" };
        c.Rules[TabletopMapId] = new(TabletopMapId, "Rulebook mechanics", "cards-basic-v1", OrdersPerSide: 1, Tabletop: true);
        c.Maps[TabletopMapId] = c.Maps["core:training"] with { Id = TabletopMapId, Name = "Бой по правилам / разработка", Rules = TabletopMapId };
        c.Objects["practice:mines"] = new("practice:mines", "Минное поле", Kind: "mine", Color: "#9a8782");
        c.Maps[TerrainMapId] = c.Maps[TabletopMapId] with { Id = TerrainMapId, Name = "Мины и астероиды", Objects = [new("practice:mines", 3, 2), new("core:asteroids", 4, 2), new("core:asteroids", 3, 3), new("core:planet", 5, 3)] };
    }
}

public sealed class BasicCardRules : IRuleModule
{
    public string Id => "cards-basic-v1";
    public static BattleState Initialize(BattleState state, ContentCatalog? c = null) => Draw(state with
    { Cards = new(Enumerable.Range(1, 24).Select(i => $"order-{i:00}").ToArray(), [], [], []) }, true, c);
    private static int Roll(ref uint random) { random ^= random << 13; random ^= random >> 17; random ^= random << 5; return (int)(random % 6) + 1; }
    internal static BattleState Draw(BattleState state, bool shuffle = false, ContentCatalog? c = null)
    {
        var cards = c == null ? state.Cards! : FleetRuleTools.Budgets(c, state); uint random = state.RandomState;
        var deck = cards.Deck.ToList(); var discard = cards.Discard.Concat(cards.Interrupts0 ?? []).Concat(cards.Interrupts1 ?? []).ToList();
        var hands = new[] { cards.Hand0.ToList(), cards.Hand1.ToList() };
        int[] limits = [cards.HandLimit0, cards.HandLimit1];
        int needed = Enumerable.Range(0, 2).Sum(i => Math.Max(0, limits[i] - hands[i].Count));
        bool tabletop = c != null && FleetRuleTools.Enabled(c, state);
        void Shuffle()
        { for (int i = deck.Count - 1; i > 0; i--) { Roll(ref random); int j = (int)(random % (i + 1)); (deck[i], deck[j]) = (deck[j], deck[i]); } }
        if (shuffle || !tabletop && deck.Count < needed)
        {
            deck.AddRange(discard); discard.Clear();
            Shuffle();
        }
        for (int side = 0; side < 2; side++) while (hands[side].Count < limits[side])
        {
            if (deck.Count == 0 && tabletop && discard.Count > 0) { deck.AddRange(discard); discard.Clear(); Shuffle(); }
            if (deck.Count == 0) break;
            hands[side].Add(deck[0]); deck.RemoveAt(0);
        }
        return state with { RandomState = random, Cards = new(deck.ToArray(), hands[0].ToArray(), hands[1].ToArray(), discard.ToArray(), HandLimit0: cards.HandLimit0, HandLimit1: cards.HandLimit1, PlayLimit0: cards.PlayLimit0, PlayLimit1: cards.PlayLimit1) };
    }
    public UnitActions GetActions(ContentCatalog c, BattleState state, int side, string unitId) =>
        state.Hazard != null || state.Hazards is { Length: > 0 } || state.Combat != null || state.Defense != null || state.CapEntry != null || state.Overrun != null || state.Repairs is { Length: > 0 } ? UnitActions.Empty :
        state.Cards?.Response != null ? InterruptRules.Actions(c, state, side, unitId) :
        state.Cards?.Pending != null ? SpecialOrders.Actions(c, state, side, unitId) :
        state.Cards is { Phase: "orders" } cards && (side == 0 ? cards.Hand0 : cards.Hand1).Length > 0
            ? new SkirmishRules().GetActions(c, state, side, unitId) : UnitActions.Empty;
    public CommandResult Apply(ContentCatalog c, BattleState state, int side, BattleCommand command)
    {
        CommandResult Reject(string text) => new(false, text, state);
        var cards = state.Cards;
        if (state.Finished || side is < 0 or > 1 || cards == null || cards.IsView) return Reject("Нельзя отдать приказ в этом состоянии.");
        if (state.Repairs is { Length: > 0 }) return FleetRepairs.Apply(c, state, side, command);
        if (state.Overrun != null) return FleetClose.Apply(c, state, side, command);
        if (state.Combat != null) return FleetCombat.Apply(c, state, side, command);
        if (state.Defense != null) return FleetDefense.Apply(c, state, side, command);
        if (state.CapEntry != null) return FleetCap.ApplyEntry(c, state, side, command);
        if (cards.Response != null) return InterruptRules.Apply(c, state, side, command);
        if (cards.Pending != null) return SpecialOrders.Apply(c, state, side, command);
        if (cards.Phase == "orders" && command.Kind is "begin-order" or "special" or "move" or "attack")
        {
            var expanded = FleetCap.Expand(c, state, side, command);
            if (expanded != null) return expanded;
        }
        var hand = side == 0 ? cards.Hand0 : cards.Hand1;
        if (cards.Phase == "cleanup")
        {
            if (command.Kind != "cleanup" || (side == 0 ? cards.Ready0 : cards.Ready1)) return Reject("Подтвердите карты, оставляемые на следующий раунд.");
            var discards = command.Cards ?? [];
            if (discards.Distinct().Count() != discards.Length || discards.Any(id => !hand.Contains(id))) return Reject("Можно сбросить только свои карты.");
            var budgets = FleetRuleTools.Budgets(c, state);
            if (hand.Length - discards.Length > (side == 0 ? budgets.HandLimit0 : budgets.HandLimit1)) return Reject("Сбросьте лишние карты: уменьшился лимит руки.");
            cards = cards with { Hand0 = side == 0 ? cards.Hand0.Except(discards).ToArray() : cards.Hand0, Hand1 = side == 1 ? cards.Hand1.Except(discards).ToArray() : cards.Hand1,
                Ready0 = cards.Ready0 || side == 0, Ready1 = cards.Ready1 || side == 1, Discard = cards.Discard.Concat(discards).ToArray() };
            var next = state with { Cards = cards, Sequence = state.Sequence + 1 };
            return new(true, "Сохранение руки подтверждено.", cards.Ready0 && cards.Ready1 ? Draw(next, c: c) : next);
        }
        if (cards.Phase == "initiative")
        {
            if (command.Kind != "initiative") return Reject("Сначала подтвердите ставку на инициативу.");
            if (side == 0 ? cards.Ready0 : cards.Ready1) return Reject("Ставка уже подтверждена.");
            var bid = command.Cards ?? (command.Card == "" ? [] : new[] { command.Card });
            if (bid.Distinct().Count() != bid.Length || bid.Any(id => !hand.Contains(id)) || !FleetRuleTools.Enabled(c, state) && bid.Length > 1) return Reject("Неверная ставка из своей руки.");
            cards = side == 0 ? cards with { Ready0 = true, Bid0 = bid.FirstOrDefault() ?? "", Bids0 = bid } : cards with { Ready1 = true, Bid1 = bid.FirstOrDefault() ?? "", Bids1 = bid };
            var next = state with { Cards = cards, Sequence = state.Sequence + 1 };
            if (!cards.Ready0 || !cards.Ready1) return new(true, "Готовность к инициативе подтверждена.", next);
            var bids0 = cards.Bids0 ?? (cards.Bid0 == "" ? [] : new[] { cards.Bid0 }); var bids1 = cards.Bids1 ?? (cards.Bid1 == "" ? [] : new[] { cards.Bid1 });
            string[] spent = bids0.Concat(bids1).ToArray();
            uint random = state.RandomState;
            int a = Math.Min(6, Roll(ref random) + bids0.Length);
            int b = Math.Min(6, Roll(ref random) + bids1.Length);
            cards = cards with { Hand0 = cards.Hand0.Except(spent).ToArray(), Hand1 = cards.Hand1.Except(spent).ToArray(),
                Discard = cards.Discard.Concat(spent).ToArray(), Ready0 = false, Ready1 = false, Bid0 = "", Bid1 = "", Bids0 = null, Bids1 = null, Phase = a == b ? "initiative" : "orders" };
            return new(true, $"Инициатива: {a} : {b}. " + (a == b ? "Ничья: новые ставки и переброс." : $"Первый приказ у стороны {(a > b ? 1 : 2)}."),
                next with { Cards = cards, RandomState = random, ActiveSide = a > b ? 0 : 1,
                    ValorOwner = FleetRuleTools.Enabled(c, state) && a != b && state.ValorOwner == -1 ? a > b ? 1 : 0 : state.ValorOwner });
        }
        if (command.Kind == "faction") return FleetRuleTools.FactionRepair(c, state, side, command);
        if (command.Kind == "begin-order") return SpecialOrders.BeginBasic(c, state, side, command);
        if (command.Kind == "special") return SpecialOrders.Start(c, state, side, command);
        if (command.Kind != "pass" && !hand.Contains(command.Card)) return Reject("Выберите карту из своей руки для базового приказа.");
        if (command.Kind == "transfer-crew") return FleetTransfers.Apply(c, state, side, command);
        if (command.Kind is "move" or "attack") return InterruptRules.Start(c, state, side, command, basic: true);
        var result = new SkirmishRules().Apply(c, state, side, command);
        if (!result.Accepted) return result;
        if (command.Kind != "pass")
            cards = side == 0 ? cards with { Hand0 = cards.Hand0.Where(id => id != command.Card).ToArray() } : cards with { Hand1 = cards.Hand1.Where(id => id != command.Card).ToArray() };
        if (command.Kind != "pass") cards = cards with { Discard = cards.Discard.Append(command.Card).ToArray() };
        var after = result.State with { Cards = cards };
        if (after.Round != state.Round && !after.Finished) after = NextRound(c, after);
        return result with { State = after };
    }
    public static BattleState View(BattleState state, int side)
    {
        if (state.Cards == null) return state;
        var cards = state.Cards;
        return state with { RandomState = 1, Cards = cards with { Deck = [], DeckCount = cards.Deck.Length, IsView = true,
            Hand0 = side == 0 ? cards.Hand0 : [], Hand1 = side == 1 ? cards.Hand1 : [], HiddenHandCount = side == 0 ? cards.Hand1.Length : cards.Hand0.Length,
            Bid0 = side == 0 ? cards.Bid0 : "", Bid1 = side == 1 ? cards.Bid1 : "", Bids0 = side == 0 ? cards.Bids0 : null, Bids1 = side == 1 ? cards.Bids1 : null } };
    }
    internal static BattleState NextRound(ContentCatalog c, BattleState s) => FleetRuleTools.Enabled(c, s)
        ? s with { ValorUsedMask = 0, Cards = FleetRuleTools.Budgets(c, s) with { Phase = "cleanup", Ready0 = false, Ready1 = false, Pending = null, Response = null } } : Draw(s, c: c);
    public static void Validate(BattleState state)
    {
        var cards = state.Cards ?? throw new InvalidDataException("Card round is missing.");
        if (cards.Phase is not ("initiative" or "orders" or "cleanup") || cards.Deck == null || cards.Hand0 == null || cards.Hand1 == null || cards.Discard == null
            || cards.HandLimit0 is < 1 or > 100 || cards.HandLimit1 is < 1 or > 100 || cards.PlayLimit0 is < 1 or > 100 || cards.PlayLimit1 is < 1 or > 100)
            throw new InvalidDataException("Invalid card round.");
        var all = cards.Deck.Concat(cards.Hand0).Concat(cards.Hand1).Concat(cards.Discard).Concat(cards.Interrupts0 ?? []).Concat(cards.Interrupts1 ?? []).ToArray();
        if (all.Distinct().Count() != all.Length || all.Any(id => !Enumerable.Range(1, 24).Select(i => $"order-{i:00}").Contains(id))
            || (!cards.IsView && all.Length != 24) || cards.Phase != "cleanup" && (cards.Hand0.Length > cards.HandLimit0 || cards.Hand1.Length > cards.HandLimit1)
            || (cards.Bid0 != "" && !cards.Hand0.Contains(cards.Bid0)) || (cards.Bid1 != "" && !cards.Hand1.Contains(cards.Bid1)))
            throw new InvalidDataException("Invalid card ownership or deck conservation.");
        if ((cards.Bids0 ?? []).Any(id => !cards.Hand0.Contains(id)) || (cards.Bids1 ?? []).Any(id => !cards.Hand1.Contains(id))
            || (cards.Bids0 ?? []).Distinct().Count() != (cards.Bids0 ?? []).Length || (cards.Bids1 ?? []).Distinct().Count() != (cards.Bids1 ?? []).Length)
            throw new InvalidDataException("Invalid initiative commitments.");
        SpecialOrders.Validate(state);
        InterruptRules.Validate(state);
    }
}
