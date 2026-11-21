namespace FleetCommand.Core;

// A definition describes a type; a deck entry describes separately owned physical copies.
public sealed record CardLine(string[] Actions);
public sealed record CardDefinition(string Id, string Name, string Kind, string Description = "",
    CardLine[]? Lines = null, bool StraightCourse = false, bool AdjacentOnLightning = false,
    string Interrupt = "", bool SameTarget = false, string SelfBuff = "", bool RepairAtStart = false,
    string AttackEffect = "", bool ExcludeFighters = false, bool RequireAfterburners = false, int BoardingDice = 0,
    bool BoardAfterMove = false, string[]? InterruptTriggers = null, int InterruptRange = 0, string HeroicOwner = "");
public sealed record DeckEntry(string Card, int Copies = 1, string[]? Instances = null);
public sealed record DeckDefinition(string Id, string Name, DeckEntry[] Entries);

public static class CommandCards
{
    public const string TrainingDeck = "core:training-deck";
    public static DeckDefinition Deck(ContentCatalog c, BattleState? state) =>
        c.Decks[state == null ? TrainingDeck : c.Rules[c.Maps[state.Map].Rules].Deck];

    public static Dictionary<string, string> Instances(DeckDefinition deck)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in deck.Entries)
            for (int i = 0; i < entry.Copies; i++)
            {
                string id = entry.Instances == null ? entry.Card + "#" + (i + 1) : entry.Instances[i];
                if (string.IsNullOrWhiteSpace(id) || id.Length > 160 || !result.TryAdd(id, entry.Card))
                    throw new InvalidDataException($"Invalid or duplicate card instance in {deck.Id}.");
            }
        return result;
    }

    public static CardDefinition? Find(ContentCatalog c, BattleState? state, string instance) =>
        Instances(Deck(c, state)).TryGetValue(instance, out var id) ? c.Cards[id] : HeroicCommands.Find(c, state, instance);
    public static string Name(ContentCatalog c, BattleState? state, string instance) => Find(c, state, instance)?.Name ?? "Unknown card";
    public static string Description(ContentCatalog c, BattleState? state, string instance) => Find(c, state, instance)?.Description ?? "";

    public static void ValidateDefinitions(ContentCatalog c)
    {
        foreach (var card in c.Cards.Values)
        {
            var lines = card.Lines ?? [];
            if (string.IsNullOrWhiteSpace(card.Name) || card.Kind is not ("order" or "interrupt" or "basic")
                || (card.Kind == "order" ? lines.Length is < 1 or > 2 || card.Interrupt != "" : lines.Length != 0)
                || lines.Any(l => l == null || l.Actions == null || l.Actions.Length is < 1 or > 8 || l.Actions.Any(a => a is not ("move" or "attack" or "lightning" or "power" or "choice" or "board" or "launch")))
                || lines.SelectMany(l => l.Actions).Contains("launch") && (lines.Length != 1 || !lines[0].Actions.SequenceEqual(["launch", "lightning", "attack"]) || card.SelfBuff != "" || card.RepairAtStart || card.AttackEffect != "")
                || (card.Kind == "interrupt" ? card.Interrupt is not ("attack" or "counter" or "repairs" or "move") : card.Interrupt != "")
                || card.InterruptTriggers != null && (card.Kind != "interrupt" || card.Interrupt is not ("attack" or "move") || card.InterruptTriggers.Length is < 1 or > 4 || card.InterruptTriggers.Distinct().Count() != card.InterruptTriggers.Length || card.InterruptTriggers.Any(a => a is not ("move" or "lightning" or "attack" or "reinforce")))
                || card.InterruptRange < 0 || card.InterruptRange > 100
                || card.Interrupt == "move" && (card.InterruptRange < 1 || card.InterruptTriggers == null)
                || card.Interrupt == "move" && card.InterruptTriggers!.Contains("attack")
                || card.StraightCourse && (lines.Length != 1 || lines[0].Actions.Any(a => a != "move"))
                || card.AdjacentOnLightning && (lines.Length != 2 || lines[0].Actions.Contains("lightning") || !lines[1].Actions.Contains("lightning"))
                || card.SameTarget && (lines.Length != 2 || lines.Any(l => l.Actions.Count(a => a is "attack" or "power" or "board") != 1))
                || card.HeroicOwner == null || card.HeroicOwner != "" && (card.Kind != "order" || !c.Leaders.TryGetValue(card.HeroicOwner, out var owner) || owner.HeroicCommand != card.Id)
                || card.SelfBuff == "hero-evasive" && card.HeroicOwner == ""
                || card.SelfBuff != "" && !FleetRuleTools.Buffs.Contains(card.SelfBuff)
                || card.AttackEffect != "" && !FleetRuleTools.Debuffs.Contains(card.AttackEffect)
                || card.Kind != "order" && (card.SelfBuff != "" || card.AttackEffect != "" || card.RepairAtStart || card.ExcludeFighters || card.RequireAfterburners)
                || (card.SelfBuff != "" || card.RepairAtStart) && lines.Length != 1
                || card.BoardingDice is < 0 or > 20 || card.BoardingDice > 0 && !lines.SelectMany(l => l.Actions).Contains("board")
                || card.BoardAfterMove && (lines.Length != 1 || !lines[0].Actions.SequenceEqual(["move", "board"])))
                throw new InvalidDataException($"Unsupported command card definition: {card.Id}");
        }
        foreach (var deck in c.Decks.Values)
        {
            if (string.IsNullOrWhiteSpace(deck.Name) || deck.Entries == null || deck.Entries.Length is < 1 or > 512
                || deck.Entries.Any(e => e == null || !c.Cards.ContainsKey(e.Card) || c.Cards[e.Card].HeroicOwner != "" || e.Copies is < 1 or > 512
                    || e.Instances != null && e.Instances.Length != e.Copies)
                || deck.Entries.Sum(e => (long)e.Copies) > 512)
                throw new InvalidDataException($"Invalid command deck: {deck.Id}");
            Instances(deck);
        }
        foreach (var rule in c.Rules.Values.Where(r => r.Handler == "cards-basic-v1"))
            if (!c.Decks.ContainsKey(rule.Deck)) throw new InvalidDataException($"Missing command deck: {rule.Id} / {rule.Deck}");
    }
}
