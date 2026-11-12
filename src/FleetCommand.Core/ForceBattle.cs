namespace FleetCommand.Core;

public sealed record PreparedBattle(ForcePlan[] Forces, int Width = 12, int Height = 8, int Objects = 8, int Seed = 12345, string Background = "#07121f", ForceLimit[]? Limits = null);
public static class ForceBattle
{
    public const string MapId = "prepared:skirmish";
    // Synthetic profiles for exercising the builder until the actual cards are available.
    public static ForcePlan PracticeForce(string faction) => new("practice-" + faction, "Training fleet / " + faction, faction, 12,
        [new("u1", "practice:" + faction + "-carrier", Row: 2), new("u2", "practice:" + faction + "-wing", Column: 1, Row: 2)],
        [new("practice:" + faction + "-admiral", "u1")], ReserveStars: 3);
    public static string Register(ContentCatalog content, PreparedBattle setup)
    {
        if (setup == null || setup.Forces is not { Length: 2 } || setup.Width is < 8 or > 40 || setup.Height is < 4 or > 30 || setup.Objects < 0 || setup.Objects > (setup.Width - 4) * setup.Height
            || setup.Limits != null && (setup.Limits.Length != 2 || setup.Limits.Any(l => l == null || l.Budget < 0 || l.ReserveStars < -1))) throw new InvalidDataException("Invalid prepared-battle settings.");
        var reports = setup.Forces.Select((force, side) => ForceBuilder.Validate(content, force, setup.Limits?[side])).ToArray();
        for (int side = 0; side < 2; side++)
        {
            if (!reports[side].Valid) throw new InvalidDataException($"Fleet {side + 1}: " + string.Join(" ", reports[side].Errors));
            if (reports[side].Units.Any(u => u.Entry.Placement == "board" && u.Entry.Row >= setup.Height)) throw new InvalidDataException($"Fleet {side + 1}: ship is outside the battlefield.");
        }
        var runtime = content.CopyForRuntime(); var fleetIds = new[] { "prepared:side-0", "prepared:side-1" };
        for (int side = 0; side < 2; side++)
        {
            var force = setup.Forces[side]; var report = reports[side];
            var placements = report.Units.Select(item =>
            {
                var entry = item.Entry; string[] leaders = force.Leaders.Where(l => l.Unit == entry.Id).Select(l => l.Leader).ToArray();
                return new FleetUnit(entry.Ship, entry.Placement == "board" ? side == 0 ? entry.Column : setup.Width - 1 - entry.Column : -1, entry.Placement == "board" ? entry.Row : -1,
                    entry.Facing < 0 ? side == 0 ? 0 : 3 : entry.Facing, Leader: leaders.FirstOrDefault() ?? "", Reserve: entry.Placement == "reserve",
                    EmbarkedOn: entry.Placement == "bay" ? Array.FindIndex(report.Units, u => u.Entry.Id == entry.Parent) : -1, AdditionalLeaders: leaders.Length > 1 ? leaders.Skip(1).ToArray() : null);
            }).ToArray();
            if (placements.Where(p => !p.Reserve && p.EmbarkedOn < 0).Any(p => FleetFootprint.Cells(content.Ships[p.Ship], new(p.Q, p.R), p.Facing).Any(h => h.Q < 0 || h.Q >= setup.Width || h.R < 0 || h.R >= setup.Height || (side == 0 ? h.Q >= 2 : h.Q < setup.Width - 2)))) throw new InvalidDataException("The entire ship must fit inside the deployment zone.");
            runtime.Fleets[fleetIds[side]] = new(fleetIds[side], force.Name, placements);
        }
        var random = new Random(setup.Seed);
        var cells = Enumerable.Range(2, setup.Width - 4).SelectMany(q => Enumerable.Range(0, setup.Height).Select(r => new Hex(q, r))).ToArray();
        for (int i = cells.Length - 1; i > 0; i--) { int j = random.Next(i + 1); (cells[i], cells[j]) = (cells[j], cells[i]); }
        var types = content.Objects.Keys.Order(StringComparer.Ordinal).ToArray();
        if (setup.Objects > 0 && types.Length == 0) throw new InvalidDataException("No battlefield object definitions.");
        var objects = cells.Take(setup.Objects).Select(h => new MapObject(types[random.Next(types.Length)], h.Q, h.R)).ToArray();
        int asteroids = objects.Count(o => FleetTerrain.Kind(content.Objects[o.Definition]) == "asteroid");
        int[] values = Enumerable.Range(0, asteroids).Select(i => new[] { 0, 2, 3 }[i % 3]).ToArray();
        var goals = Enumerable.Range(0, 2).SelectMany(side => new[] {
            new ObjectiveDefinition("minor-" + side, "Destroy 40% of the enemy's value", side, "destroy-stars", 1, Math.Max(1, (int)Math.Ceiling(reports[1 - side].Spent * 0.4))),
            new ObjectiveDefinition("major-" + side, "Destroy 60% of the enemy's value", side, "destroy-stars", 2, Math.Max(1, (int)Math.Ceiling(reports[1 - side].Spent * 0.6))) }).ToArray();
        runtime.Rules[MapId] = new(MapId, "Prepared fleets", "cards-basic-v1", Tabletop: true);
        runtime.Maps[MapId] = new(MapId, "Prepared fleets", setup.Width, setup.Height, setup.Background, objects, fleetIds, MapId,
            AsteroidStars: values, ReserveStars: setup.Forces.Select(p => p.ReserveStars).ToArray(), Objectives: goals);
        runtime.Validate();
        foreach (string id in fleetIds) content.Fleets[id] = runtime.Fleets[id]; content.Rules[MapId] = runtime.Rules[MapId]; content.Maps[MapId] = runtime.Maps[MapId];
        return MapId;
    }
}
