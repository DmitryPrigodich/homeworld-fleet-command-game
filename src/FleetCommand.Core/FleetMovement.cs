namespace FleetCommand.Core;

public readonly record struct MovementStep(Hex Cell, int Facing);
public static class FleetMovement
{
    private readonly record struct Position(Hex Cell, int Facing);
    internal static int Allowance(ContentCatalog c, BattleUnit unit)
    {
        var ship = c.Ships[unit.Definition];
        return FleetRuleTools.Has(unit, "engines") ? Math.Min(1, ship.Move) : ship.Move + (ship.Abilities ?? []).Select(id => c.Abilities[id]).Where(a => a.Handler == "move-bonus").Sum(a => a.Value);
    }
    private static (Dictionary<Position, int> Costs, Dictionary<Position, Position> Previous) Search(ContentCatalog c, BattleState s, BattleUnit unit, string embark = "")
    {
        var map = c.Maps[s.Map]; var rules = c.Rules[map.Rules]; var ship = c.Ships[unit.Definition];
        bool strike = FleetRuleTools.Strike(ship); int allowance = Allowance(c, unit);
        var start = new Position(new(unit.Q, unit.R), unit.Facing);
        var costs = new Dictionary<Position, int> { [start] = 0 }; var previous = new Dictionary<Position, Position>();
        var queue = new PriorityQueue<Position, (int, int, int, int)>(); queue.Enqueue(start, (0, unit.Q, unit.R, unit.Facing));
        bool Gravity(Hex h) => map.Objects.Any(o => (c.Objects[o.Definition].Kind == "planet" || o.Definition == "core:planet") && h.Distance(new(o.Q, o.R)) == 1);
        while (queue.TryDequeue(out var current, out var priority))
        {
            int spent = priority.Item1; if (costs[current] != spent || spent >= allowance) continue;
            var directions = strike ? Enumerable.Range(0, 6) : new[] { (current.Facing + 5) % 6, current.Facing, (current.Facing + 1) % 6 };
            void Add(Position next, int cost)
            {
                int total = spent + cost;
                if (total > allowance || costs.TryGetValue(next, out int old) && old <= total) return;
                costs[next] = total; previous[next] = current; queue.Enqueue(next, (total, next.Cell.Q, next.Cell.R, next.Facing));
            }
            foreach (int direction in directions)
            {
                if (direction != current.Facing) Add(new(current.Cell, direction), 1);
                var cell = current.Cell.Neighbor(direction);
                bool docking = embark != "" && s.Units.Any(p => p.Id == embark && p.Q == cell.Q && p.R == cell.R);
                if (cell.Q < 0 || cell.R < 0 || cell.Q >= map.Width || cell.R >= map.Height || !docking && SkirmishGeometry.Blocked(c, map, cell, s)
                    || s.Units.Any(u => u.Patrol == "" && u.Id != unit.Id && u.Q == cell.Q && u.R == cell.R && (u.Hulk || u.Side != unit.Side || !rules.FriendlyPassThrough && !docking))) continue;
                int cost = FleetTerrain.Objects(c, s).Where(o => o.Q == cell.Q && o.R == cell.R).Select(o => c.Objects[o.Definition].Kind == "asteroid" ? 1 : c.Objects[o.Definition].MoveCost).DefaultIfEmpty(1).Max();
                if (docking) cost = 1;
                if (!docking && Gravity(current.Cell) && !Gravity(cell)) cost = Math.Max(2, cost);
                Add(new(cell, direction), cost);
            }
        }
        return (costs, previous);
    }
    public static Hex[] Destinations(ContentCatalog c, BattleState s, BattleUnit u) => Search(c, s, u).Costs.Keys.Select(p => p.Cell).Distinct()
        .Where(h => h != new Hex(u.Q, u.R) && !s.Units.Any(other => other.Patrol == "" && other.Q == h.Q && other.R == h.R)).OrderBy(h => h.Q).ThenBy(h => h.R).ToArray();
    public static MovementStep[]? Path(ContentCatalog c, BattleState s, BattleUnit u, Hex goal, int facing = -1, string embark = "")
    {
        if (s.Units.Any(other => other.Patrol == "" && other.Id != u.Id && other.Id != embark && other.Q == goal.Q && other.R == goal.R)) return null;
        var (costs, previous) = Search(c, s, u, embark); var start = new Position(new(u.Q, u.R), u.Facing);
        var destinations = costs.Keys.Where(p => p.Cell == goal && (facing < 0 || p.Facing == facing) && p != start)
            .OrderBy(p => costs[p]).ThenBy(p => p.Facing).ToArray();
        if (destinations.Length == 0) return null;
        var result = new List<MovementStep>();
        for (var p = destinations[0]; p != start; p = previous[p]) result.Add(new(p.Cell, p.Facing));
        result.Reverse(); return result.ToArray();
    }
    internal static MovementStep[]? StraightPath(ContentCatalog c, BattleState s, BattleUnit u, Hex goal, string embark = "")
    {
        var map = c.Maps[s.Map];
        if (goal.Q < 0 || goal.R < 0 || goal.Q >= map.Width || goal.R >= map.Height) return null;
        int direction = SpecialOrders.StraightDirection(new(u.Q, u.R), goal); if (direction < 0) return null;
        var result = new List<MovementStep>(); var position = new Hex(u.Q, u.R); int facing = u.Facing, cost = 0;
        bool Gravity(Hex h) => map.Objects.Any(o => (c.Objects[o.Definition].Kind == "planet" || o.Definition == "core:planet") && h.Distance(new(o.Q, o.R)) == 1);
        int turns = Math.Min((direction - facing + 6) % 6, (facing - direction + 6) % 6);
        if (!FleetRuleTools.Strike(c.Ships[u.Definition]))
            while (turns > 1)
            {
                facing = (facing + ((direction - facing + 6) % 6 <= 3 ? 1 : 5)) % 6;
                result.Add(new(position, facing)); cost++; turns--;
            }
        while (position != goal)
        {
            var next = position.Neighbor(direction);
            bool docking = embark != "" && s.Units.Any(p => p.Id == embark && p.Q == next.Q && p.R == next.R);
            if (!docking && SkirmishGeometry.Blocked(c, map, next, s) || s.Units.Any(other => other.Patrol == "" && other.Id != u.Id && other.Q == next.Q && other.R == next.R
                && (other.Hulk || other.Side != u.Side || !c.Rules[map.Rules].FriendlyPassThrough && !docking))) return null;
            int stepCost = FleetTerrain.Objects(c, s).Where(o => o.Q == next.Q && o.R == next.R).Select(o => c.Objects[o.Definition].Kind == "asteroid" ? 1 : c.Objects[o.Definition].MoveCost).DefaultIfEmpty(1).Max();
            if (docking) stepCost = 1;
            if (!docking && Gravity(position) && !Gravity(next)) stepCost = Math.Max(2, stepCost);
            cost += stepCost; result.Add(new(next, direction)); position = next;
        }
        return cost <= Allowance(c, u) ? result.ToArray() : null;
    }
}
