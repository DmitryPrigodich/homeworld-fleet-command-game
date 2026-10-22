namespace FleetCommand.Core;

public sealed record GenerationOptions(int Width = 12, int Height = 8, int FriendlyUnits = 5,
    int EnemyUnits = 5, int Objects = 10, int Seed = 12345, string Background = "",
    string FriendlyFleet = "core:kushan-patrol", string EnemyFleet = "core:taiidan-patrol",
    string Rules = "core:prototype");

public static class MapGenerator
{
    public const string MapId = "generated:skirmish";
    public static string Generate(ContentCatalog c, GenerationOptions options)
    {
        if (options.Width is < 8 or > 40 || options.Height is < 4 or > 30 || options.FriendlyUnits is < 1 or > 30
            || options.EnemyUnits is < 1 or > 30 || options.Objects < 0 || options.Objects > (options.Width - 4) * options.Height
            || options.FriendlyUnits > 2 * options.Height || options.EnemyUnits > 2 * options.Height)
            throw new InvalidDataException("The requested forces or objects do not fit the map/deployment zones.");
        var random = new Random(options.Seed);
        Hex[] Shuffle(IEnumerable<Hex> cells) => cells.OrderBy(_ => random.Next()).ToArray();
        var friendlyCells = Shuffle(Enumerable.Range(0, 2).SelectMany(q => Enumerable.Range(0, options.Height).Select(r => new Hex(q, r))));
        var enemyCells = Shuffle(Enumerable.Range(options.Width - 2, 2).SelectMany(q => Enumerable.Range(0, options.Height).Select(r => new Hex(q, r))));
        if (!c.Fleets.ContainsKey(options.FriendlyFleet) || !c.Fleets.ContainsKey(options.EnemyFleet) || !c.Rules.ContainsKey(options.Rules))
            throw new InvalidDataException("Missing generator fleet or rules definition.");
        var sides = new[] { c.Fleets[options.FriendlyFleet].Units.Select(u => u.Ship).Distinct().ToArray(),
            c.Fleets[options.EnemyFleet].Units.Select(u => u.Ship).Distinct().ToArray() };
        if (sides.Any(s => s.Length == 0)) throw new InvalidDataException("Generator fleets must contain at least one ship type.");
        FleetUnit[] Units(int count, Hex[] cells, int side) => Enumerable.Range(0, count)
            .Select(i => new FleetUnit(sides[side][random.Next(sides[side].Length)], cells[i].Q, cells[i].R, side == 0 ? 0 : 3)).ToArray();
        c.Fleets["generated:friendly"] = new("generated:friendly", "Friendly force", Units(options.FriendlyUnits, friendlyCells, 0));
        c.Fleets["generated:enemy"] = new("generated:enemy", "Enemy force", Units(options.EnemyUnits, enemyCells, 1));
        var objectCells = Shuffle(Enumerable.Range(2, options.Width - 4).SelectMany(q => Enumerable.Range(0, options.Height).Select(r => new Hex(q, r))));
        var types = c.Objects.Keys.Order(StringComparer.Ordinal).ToArray();
        if (options.Objects > 0 && types.Length == 0) throw new InvalidDataException("No object definitions loaded.");
        var objects = objectCells.Take(options.Objects).Select(h => new MapObject(types[random.Next(types.Length)], h.Q, h.R)).ToArray();
        c.Maps[MapId] = new(MapId, "Randomized Skirmish", options.Width, options.Height, options.Background, objects,
            ["generated:friendly", "generated:enemy"], options.Rules);
        c.Validate();
        return MapId;
    }
}
