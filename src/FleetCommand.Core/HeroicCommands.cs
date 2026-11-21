namespace FleetCommand.Core;

public sealed record HeroicCard(string Id, string Leader, int Side);

public static class HeroicCommands
{
    public static HeroicCard[] Pool(CardRound cards) => cards.HeroicCards ?? [];
    public static bool IsHeroic(BattleState? s, string id) => s?.Cards != null && Pool(s.Cards).Any(h => h.Id == id);
    public static CardDefinition? Find(ContentCatalog c, BattleState? s, string id) => s?.Cards != null && Pool(s.Cards).FirstOrDefault(h => h.Id == id) is { } hero
        ? c.Cards.GetValueOrDefault(c.Leaders[hero.Leader].HeroicCommand) : null;
    public static int HandCount(CardRound cards, IEnumerable<string> hand) => hand.Count(id => !Pool(cards).Any(h => h.Id == id));
    public static bool CanUse(ContentCatalog c, BattleState s, int side, string id) => !IsHeroic(s, id) || Pool(s.Cards!).Any(h => h.Id == id && h.Side == side
        && !(s.Cards!.HeroicUsed ?? []).Contains(id) && s.Units.Any(u => u.Side == side && !u.Hulk && FleetRuleTools.LeaderIds(u).Contains(h.Leader)));
    public static bool Spent(CardRound cards, string id) => cards.Discard.Contains(id) || (cards.HeroicUsed ?? []).Contains(id);
    public static CardRound Spend(CardRound cards, params string[] ids) => cards with
    {
        Hand0 = cards.Hand0.Except(ids).ToArray(), Hand1 = cards.Hand1.Except(ids).ToArray(),
        Discard = cards.Discard.Concat(ids.Where(id => !Pool(cards).Any(h => h.Id == id))).ToArray(),
        HeroicUsed = Pool(cards).Length == 0 ? cards.HeroicUsed : (cards.HeroicUsed ?? []).Concat(ids.Where(id => Pool(cards).Any(h => h.Id == id))).ToArray()
    };
    internal static BattleState Initialize(ContentCatalog c, BattleState s)
    {
        if (!FleetRuleTools.Enabled(c, s)) return s;
        var pool = s.Units.Concat(s.Reserves ?? []).SelectMany(u => FleetRuleTools.LeaderIds(u).Where(id => c.Leaders[id].HeroicCommand != "")
            .Select(id => new HeroicCard(id + "#hero-" + u.Side, id, u.Side))).ToArray();
        if (pool.Length == 0) return s;
        if (pool.Select(h => h.Id).Distinct().Count() != pool.Length) throw new InvalidDataException("A Hero can appear only once per fleet.");
        return s with { Cards = s.Cards! with { HeroicCards = pool, HeroicUsed = [],
            Hand0 = s.Cards.Hand0.Concat(pool.Where(h => h.Side == 0).Select(h => h.Id)).ToArray(),
            Hand1 = s.Cards.Hand1.Concat(pool.Where(h => h.Side == 1).Select(h => h.Id)).ToArray() } };
    }
    internal static void Validate(ContentCatalog c, BattleState s)
    {
        var cards = s.Cards!; var pool = Pool(cards); var used = cards.HeroicUsed ?? [];
        if (pool.Length > 128 || pool.Any(h => h == null || h.Side is < 0 or > 1 || !c.Leaders.TryGetValue(h.Leader, out var leader)
            || leader.HeroicCommand == "" || h.Id != h.Leader + "#hero-" + h.Side)
            || pool.Select(h => h.Id).Distinct().Count() != pool.Length || used.Distinct().Count() != used.Length || used.Any(id => !pool.Any(h => h.Id == id))
            || pool.Length > 0 && !FleetRuleTools.Enabled(c, s)
            || pool.Any(h => (h.Side == 0 ? cards.Hand1 : cards.Hand0).Contains(h.Id))
            || cards.Deck.Concat(cards.Discard).Concat(cards.Interrupts0 ?? []).Concat(cards.Interrupts1 ?? []).Any(id => pool.Any(h => h.Id == id)))
            throw new InvalidDataException("Invalid Heroic card ownership or lifecycle.");
        if (s.Units.Concat(s.Reserves ?? []).Any(u => FleetRuleTools.LeaderIds(u).Any(id => c.Leaders[id].HeroicCommand != "" && !pool.Any(h => h.Leader == id && h.Side == u.Side))))
            throw new InvalidDataException("A living Hero is missing its scenario card.");
        foreach (int side in new[] { 0, 1 })
            if (s.Units.Concat(s.Reserves ?? []).Where(u => u.Side == side).SelectMany(u => FleetRuleTools.LeaderIds(u)).Where(id => c.Leaders[id].HeroicCommand != "").GroupBy(id => id).Any(g => g.Count() > 1))
                throw new InvalidDataException("A Hero can appear only once per fleet.");
    }
}
