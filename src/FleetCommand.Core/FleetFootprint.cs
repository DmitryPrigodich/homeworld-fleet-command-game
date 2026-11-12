namespace FleetCommand.Core;

// Q/R identifies the bow. The second hex is the stern, opposite Facing.
public static class FleetFootprint
{
    public static Hex[] Cells(ContentCatalog c, BattleUnit u) => Cells(c.Ships[u.Definition], new(u.Q, u.R), u.Facing);
    public static Hex[] Cells(ShipDefinition ship, Hex bow, int facing) => ship.Footprint == 2 ? [bow, bow.Neighbor(facing + 3)] : [bow];
    public static bool Inside(MapDefinition map, Hex h) => h.Q >= 0 && h.R >= 0 && h.Q < map.Width && h.R < map.Height && (map.Cells == null || map.Cells.Contains(h));
    public static bool Contains(ContentCatalog c, BattleUnit u, Hex h) => Cells(c, u).Contains(h);
    public static int Distance(ContentCatalog c, BattleUnit a, BattleUnit b) => Cells(c, a).Min(h => Distance(c, b, h));
    public static int Distance(ContentCatalog c, BattleUnit u, Hex h) => Cells(c, u).Min(p => p.Distance(h));
    public static Hex[] Adjacent(ContentCatalog c, BattleUnit u) => Cells(c, u).SelectMany(h => Enumerable.Range(0, 6).Select(h.Neighbor)).Except(Cells(c, u)).Distinct().ToArray();
    public static bool CanPlace(ContentCatalog c, BattleState s, BattleUnit unit, Hex bow, int facing, bool transit = false, string docking = "")
    {
        var map = c.Maps[s.Map]; var cells = Cells(c.Ships[unit.Definition], bow, facing);
        return cells.All(h => Inside(map, h) && !SkirmishGeometry.Blocked(c, map, h, s) && !s.Units.Any(other => other.Patrol == "" && other.Id != unit.Id && other.Id != docking
            && Contains(c, other, h) && (!transit || other.Side != unit.Side || other.Hulk || !c.Rules[map.Rules].FriendlyPassThrough)));
    }
    public static (Hex From, Hex To)[] Nearest(ContentCatalog c, BattleUnit a, BattleUnit b)
    {
        var pairs = Cells(c, a).SelectMany(x => Cells(c, b).Select(y => (From: x, To: y))).ToArray();
        int distance = pairs.Min(p => p.From.Distance(p.To)); return pairs.Where(p => p.From.Distance(p.To) == distance).ToArray();
    }
}
