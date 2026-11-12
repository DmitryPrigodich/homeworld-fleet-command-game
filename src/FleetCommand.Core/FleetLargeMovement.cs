namespace FleetCommand.Core;

public static class FleetLargeMovement
{
    private readonly record struct Pose(Hex Bow, int Facing, int Front, int Rear);
    public static int Limit(ContentCatalog c, BattleUnit u) => Math.Min(2, FleetMovement.Allowance(c, u));
    public static (int Front, int Rear) Cost(ContentCatalog c, BattleState s, BattleUnit u, MovementStep from, MovementStep to)
    {
        var map = c.Maps[s.Map]; var ship = c.Ships[u.Definition];
        var a = FleetFootprint.Cells(ship, from.Cell, from.Facing); var b = FleetFootprint.Cells(ship, to.Cell, to.Facing);
        bool Gravity(Hex h) => map.Objects.Any(o => c.Objects[o.Definition].Kind == "planet" || o.Definition == "core:planet" ? h.Distance(new(o.Q, o.R)) == 1 : false);
        int End(int i)
        {
            int distance = a[i].Distance(b[i]); if (distance == 0) return 0; if (distance != 1) return 1000;
            int cost = FleetTerrain.Objects(c, s).Where(o => new Hex(o.Q, o.R) == b[i]).Select(o => FleetTerrain.Kind(c.Objects[o.Definition]) == "asteroid" ? 1 : c.Objects[o.Definition].MoveCost).DefaultIfEmpty(1).Max();
            return Gravity(a[i]) && !Gravity(b[i]) ? Math.Max(2, cost) : cost;
        }
        return (End(0), End(1));
    }
    private static (Dictionary<Pose, Pose> Previous, Pose Start) Search(ContentCatalog c, BattleState s, BattleUnit u, bool straight = false)
    {
        var start = new Pose(new(u.Q, u.R), u.Facing, 0, 0); int limit = Limit(c, u);
        var previous = new Dictionary<Pose, Pose> { [start] = start }; var queue = new Queue<Pose>(); queue.Enqueue(start);
        while (queue.TryDequeue(out var p))
        {
            var moves = Enumerable.Range(0, 6).Select(d => (Bow: p.Bow.Neighbor(d), Facing: p.Facing)).ToList();
            if (!straight) foreach (int turn in new[] { 1, 5 })
            {
                int facing = (p.Facing + turn) % 6; moves.Add((p.Bow, facing));
                moves.Add((p.Bow.Neighbor(p.Facing + 3).Neighbor(facing), facing)); // Pivot about the stern.
            }
            foreach (var m in moves)
            {
                if (!FleetFootprint.CanPlace(c, s, u, m.Bow, m.Facing, transit: true)) continue;
                var cost = Cost(c, s, u, new(p.Bow, p.Facing), new(m.Bow, m.Facing));
                var n = new Pose(m.Bow, m.Facing, p.Front + cost.Front, p.Rear + cost.Rear);
                if (n.Front > limit || n.Rear > limit || previous.ContainsKey(n)
                    || previous.Keys.Any(old => old.Bow == n.Bow && old.Facing == n.Facing && old.Front <= n.Front && old.Rear <= n.Rear)) continue;
                previous[n] = p; queue.Enqueue(n);
            }
        }
        return (previous, start);
    }
    public static Hex[] Destinations(ContentCatalog c, BattleState s, BattleUnit u) => Search(c, s, u).Previous.Keys
        .Where(p => p.Bow != new Hex(u.Q, u.R) && FleetFootprint.CanPlace(c, s, u, p.Bow, p.Facing)).Select(p => p.Bow).Distinct().OrderBy(h => h.Q).ThenBy(h => h.R).ToArray();
    public static MovementStep[]? Path(ContentCatalog c, BattleState s, BattleUnit u, Hex goal, int facing = -1, bool straight = false)
    {
        var (previous, start) = Search(c, s, u, straight);
        var options = previous.Keys.Where(p => p != start && p.Bow == goal && (facing < 0 || p.Facing == facing) && FleetFootprint.CanPlace(c, s, u, p.Bow, p.Facing))
            .OrderBy(p => Math.Max(p.Front, p.Rear)).ThenBy(p => p.Front + p.Rear).ThenBy(p => p.Facing).ToArray();
        foreach (var destination in options)
        {
            var steps = new List<MovementStep>(); for (var p = destination; p != start; p = previous[p]) steps.Add(new(p.Bow, p.Facing)); steps.Reverse();
            if (straight)
            {
                int direction = SpecialOrders.StraightDirection(start.Bow, goal); var at = start.Bow;
                if (direction < 0 || steps.Any(step => { at = at.Neighbor(direction); return step.Cell != at; })) continue;
            }
            return steps.ToArray();
        }
        return null;
    }
}
