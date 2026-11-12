namespace FleetCommand.Core;

// A definition describes a type; a deck entry describes separately owned physical copies.
public sealed record CardLine(string[] Actions);
public sealed record CardDefinition(string Id, string Name, string Kind, string Description = "",
    CardLine[]? Lines = null, bool StraightCourse = false, bool AdjacentOnLightning = false,
    string Interrupt = "");
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
        Instances(Deck(c, state)).TryGetValue(instance, out var id) ? c.Cards[id] : null;
    public static string Name(ContentCatalog c, BattleState? state, string instance) => Find(c, state, instance)?.Name ?? "Unknown card";
    public static string Description(ContentCatalog c, BattleState? state, string instance) => Find(c, state, instance)?.Description ?? "";

    public static void ValidateDefinitions(ContentCatalog c)
    {
        foreach (var card in c.Cards.Values)
        {
            var lines = card.Lines ?? [];
            if (string.IsNullOrWhiteSpace(card.Name) || card.Kind is not ("order" or "interrupt" or "basic")
                || (card.Kind == "order" ? lines.Length is < 1 or > 2 || card.Interrupt != "" : lines.Length != 0)
                || lines.Any(l => l == null || l.Actions == null || l.Actions.Length is < 1 or > 8 || l.Actions.Any(a => a is not ("move" or "attack" or "lightning")))
                || (card.Kind == "interrupt" ? card.Interrupt != "attack" : card.Interrupt != "")
                || card.StraightCourse && (lines.Length != 1 || lines[0].Actions.Any(a => a != "move"))
                || card.AdjacentOnLightning && (lines.Length != 2 || lines[0].Actions.Contains("lightning") || !lines[1].Actions.Contains("lightning")))
                throw new InvalidDataException($"Unsupported command card definition: {card.Id}");
        }
        foreach (var deck in c.Decks.Values)
        {
            if (string.IsNullOrWhiteSpace(deck.Name) || deck.Entries == null || deck.Entries.Length is < 1 or > 512
                || deck.Entries.Any(e => e == null || !c.Cards.ContainsKey(e.Card) || e.Copies is < 1 or > 512
                    || e.Instances != null && e.Instances.Length != e.Copies)
                || deck.Entries.Sum(e => (long)e.Copies) > 512)
                throw new InvalidDataException($"Invalid command deck: {deck.Id}");
            Instances(deck);
        }
        foreach (var rule in c.Rules.Values.Where(r => r.Handler == "cards-basic-v1"))
            if (!c.Decks.ContainsKey(rule.Deck)) throw new InvalidDataException($"Missing command deck: {rule.Id} / {rule.Deck}");
    }
}
