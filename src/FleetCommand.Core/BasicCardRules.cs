namespace FleetCommand.Core;

public sealed record CardRound(string[] Deck, string[] Hand0, string[] Hand1, string[] Discard,
    string Phase = "initiative", bool Ready0 = false, bool Ready1 = false, string Bid0 = "", string Bid1 = "",
    bool IsView = false, int HiddenHandCount = 0, int DeckCount = 0, PendingOrder? Pending = null,
    string[]? Interrupts0 = null, string[]? Interrupts1 = null, PendingResponse? Response = null);

public static class BasicCardBattle
{
    public const string MapId = "practice:cards";
    public static void Register(ContentCatalog c)
    {
        c.Rules["practice:cards"] = new("practice:cards", "Basic card practice", "cards-basic-v1", OrdersPerSide: 1, RearShots: false);
        c.Maps[MapId] = c.Maps["core:training"] with { Id = MapId, Name = "Учебный бой с картами", Rules = "practice:cards" };
    }
}

public sealed class BasicCardRules : IRuleModule
{
    public string Id => "cards-basic-v1";
    public static BattleState Initialize(BattleState state) => Draw(state with
    { Cards = new(Enumerable.Range(1, 24).Select(i => $"order-{i:00}").ToArray(), [], [], []) }, true);
    private static int Roll(ref uint random) { random ^= random << 13; random ^= random >> 17; random ^= random << 5; return (int)(random % 6) + 1; }
    internal static BattleState Draw(BattleState state, bool shuffle = false)
    {
        var cards = state.Cards!; uint random = state.RandomState;
        var deck = cards.Deck.ToList(); var discard = cards.Discard.Concat(cards.Interrupts0 ?? []).Concat(cards.Interrupts1 ?? []).ToList();
        var hands = new[] { cards.Hand0.ToList(), cards.Hand1.ToList() };
        int needed = hands.Sum(h => Math.Max(0, 2 - h.Count));
        if (shuffle || deck.Count < needed)
        {
            deck.AddRange(discard); discard.Clear();
            for (int i = deck.Count - 1; i > 0; i--) { Roll(ref random); int j = (int)(random % (i + 1)); (deck[i], deck[j]) = (deck[j], deck[i]); }
        }
        foreach (var hand in hands) while (hand.Count < 2 && deck.Count > 0) { hand.Add(deck[0]); deck.RemoveAt(0); }
        return state with { RandomState = random, Cards = new(deck.ToArray(), hands[0].ToArray(), hands[1].ToArray(), discard.ToArray()) };
    }
    public UnitActions GetActions(ContentCatalog c, BattleState state, int side, string unitId) =>
        state.Cards?.Response != null ? InterruptRules.Actions(c, state, side, unitId) :
        state.Cards?.Pending != null ? SpecialOrders.Actions(c, state, side, unitId) :
        state.Cards is { Phase: "orders" } cards && (side == 0 ? cards.Hand0 : cards.Hand1).Length > 0
            ? new SkirmishRules().GetActions(c, state, side, unitId) : UnitActions.Empty;
    public CommandResult Apply(ContentCatalog c, BattleState state, int side, BattleCommand command)
    {
        CommandResult Reject(string text) => new(false, text, state);
        var cards = state.Cards;
        if (state.Finished || side is < 0 or > 1 || cards == null || cards.IsView) return Reject("Нельзя отдать приказ в этом состоянии.");
        if (cards.Response != null) return InterruptRules.Apply(c, state, side, command);
        if (cards.Pending != null) return SpecialOrders.Apply(c, state, side, command);
        var hand = side == 0 ? cards.Hand0 : cards.Hand1;
        if (cards.Phase == "initiative")
        {
            if (command.Kind != "initiative") return Reject("Сначала подтвердите ставку на инициативу.");
            if (side == 0 ? cards.Ready0 : cards.Ready1) return Reject("Ставка уже подтверждена.");
            if (command.Card != "" && !hand.Contains(command.Card)) return Reject("Такой карты нет в вашей руке.");
            cards = side == 0 ? cards with { Ready0 = true, Bid0 = command.Card } : cards with { Ready1 = true, Bid1 = command.Card };
            var next = state with { Cards = cards, Sequence = state.Sequence + 1 };
            if (!cards.Ready0 || !cards.Ready1) return new(true, "Готовность к инициативе подтверждена.", next);
            string[] spent = new[] { cards.Bid0, cards.Bid1 }.Where(id => id != "").ToArray();
            uint random = state.RandomState;
            int a = Math.Min(6, Roll(ref random) + (cards.Bid0 == "" ? 0 : 1));
            int b = Math.Min(6, Roll(ref random) + (cards.Bid1 == "" ? 0 : 1));
            cards = cards with { Hand0 = cards.Hand0.Except(spent).ToArray(), Hand1 = cards.Hand1.Except(spent).ToArray(),
                Discard = cards.Discard.Concat(spent).ToArray(), Ready0 = false, Ready1 = false, Bid0 = "", Bid1 = "", Phase = a == b ? "initiative" : "orders" };
            return new(true, $"Инициатива: {a} : {b}. " + (a == b ? "Ничья: новые ставки и переброс." : $"Первый приказ у стороны {(a > b ? 1 : 2)}."),
                next with { Cards = cards, RandomState = random, ActiveSide = a > b ? 0 : 1 });
        }
        if (command.Kind == "special") return SpecialOrders.Start(c, state, side, command);
        if (command.Kind != "pass" && !hand.Contains(command.Card)) return Reject("Выберите карту из своей руки для базового приказа.");
        if (command.Kind is "move" or "attack") return InterruptRules.Start(c, state, side, command, basic: true);
        var result = new SkirmishRules().Apply(c, state, side, command);
        if (!result.Accepted) return result;
        if (command.Kind != "pass")
            cards = side == 0 ? cards with { Hand0 = cards.Hand0.Where(id => id != command.Card).ToArray() } : cards with { Hand1 = cards.Hand1.Where(id => id != command.Card).ToArray() };
        if (command.Kind != "pass") cards = cards with { Discard = cards.Discard.Append(command.Card).ToArray() };
        var after = result.State with { Cards = cards };
        if (after.Round != state.Round && !after.Finished) after = Draw(after);
        return result with { State = after };
    }
    public static BattleState View(BattleState state, int side)
    {
        if (state.Cards == null) return state;
        var cards = state.Cards;
        return state with { RandomState = 1, Cards = cards with { Deck = [], DeckCount = cards.Deck.Length, IsView = true,
            Hand0 = side == 0 ? cards.Hand0 : [], Hand1 = side == 1 ? cards.Hand1 : [], HiddenHandCount = side == 0 ? cards.Hand1.Length : cards.Hand0.Length,
            Bid0 = side == 0 ? cards.Bid0 : "", Bid1 = side == 1 ? cards.Bid1 : "" } };
    }
    public static void Validate(BattleState state)
    {
        var cards = state.Cards ?? throw new InvalidDataException("Card round is missing.");
        if (cards.Phase is not ("initiative" or "orders") || cards.Deck == null || cards.Hand0 == null || cards.Hand1 == null || cards.Discard == null)
            throw new InvalidDataException("Invalid card round.");
        var all = cards.Deck.Concat(cards.Hand0).Concat(cards.Hand1).Concat(cards.Discard).Concat(cards.Interrupts0 ?? []).Concat(cards.Interrupts1 ?? []).ToArray();
        if (all.Distinct().Count() != all.Length || all.Any(id => !Enumerable.Range(1, 24).Select(i => $"order-{i:00}").Contains(id))
            || (!cards.IsView && all.Length != 24) || cards.Hand0.Length > 2 || cards.Hand1.Length > 2
            || (cards.Bid0 != "" && !cards.Hand0.Contains(cards.Bid0)) || (cards.Bid1 != "" && !cards.Hand1.Contains(cards.Bid1)))
            throw new InvalidDataException("Invalid card ownership or deck conservation.");
        SpecialOrders.Validate(state);
        InterruptRules.Validate(state);
    }
}
