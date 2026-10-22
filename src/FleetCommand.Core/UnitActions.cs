namespace FleetCommand.Core;

public sealed record UnitActions(Hex[] MoveCells, string[] AttackTargets)
{
    public static UnitActions Empty { get; } = new([], []);
}

// Shared geometry for command validation and read-only action previews.
internal static class SkirmishGeometry
{
    public static bool Blocked(ContentCatalog content, MapDefinition map, Hex hex) =>
        map.Objects.Any(o => o.Q == hex.Q && o.R == hex.R && content.Objects[o.Definition].BlocksMovement);

    public static HashSet<Hex> Movement(ContentCatalog content, BattleState state, BattleUnit unit)
    {
        var map = content.Maps[state.Map];
        var rules = content.Rules[map.Rules];
        var ship = content.Ships[unit.Definition];
        int allowance = ship.Move + (ship.Abilities ?? []).Select(id => content.Abilities[id]).Where(a => a.Handler == "move-bonus").Sum(a => a.Value);
        var occupied = state.Units.ToDictionary(u => new Hex(u.Q, u.R));
        bool Inside(Hex h) => h.Q >= 0 && h.Q < map.Width && h.R >= 0 && h.R < map.Height;
        var start = new Hex(unit.Q, unit.R);
        var best = new Dictionary<Hex, int> { [start] = 0 };
        var queue = new PriorityQueue<Hex, int>(); queue.Enqueue(start, 0);
        while (queue.TryDequeue(out var hex, out int cost))
        {
            if (cost != best[hex]) continue;
            for (int direction = 0; direction < 6; direction++)
            {
                var next = hex.Neighbor(direction);
                if (!Inside(next) || Blocked(content, map, next)
                    || occupied.TryGetValue(next, out var other) && (other.Side != unit.Side || !rules.FriendlyPassThrough)) continue;
                int moveCost = map.Objects.Where(o => o.Q == next.Q && o.R == next.R).Select(o => content.Objects[o.Definition].MoveCost).DefaultIfEmpty(1).Max();
                int total = cost + moveCost;
                if (total > allowance || best.TryGetValue(next, out int old) && old <= total) continue;
                best[next] = total; queue.Enqueue(next, total);
            }
        }
        return best.Keys.Where(h => h != start && !occupied.ContainsKey(h)).ToHashSet();
    }

    public static (bool Allowed, int Cover, string Error) Attack(ContentCatalog content, BattleState state, BattleUnit unit, BattleUnit? target)
    {
        var ship = content.Ships[unit.Definition];
        if (ship.DicePerModel == 0) return (false, 0, "У этого подразделения нет оружия для стрельбы.");
        if (target == null || target.Side == unit.Side || new Hex(unit.Q, unit.R).Distance(new(target.Q, target.R)) > ship.Range)
            return (false, 0, "Выбери противника в пределах дальности стрельбы.");
        var map = content.Maps[state.Map];
        var end = new Hex(target.Q, target.R);
        bool Inside(Hex h) => h.Q >= 0 && h.Q < map.Width && h.R >= 0 && h.R < map.Height;
        var memo = new Dictionary<Hex, int>();
        // The prototype takes the greatest cover penalty over all shortest paths.
        int Cover(Hex h)
        {
            if (h == end) return 0;
            if (memo.TryGetValue(h, out int cached)) return cached;
            int worst = 0;
            for (int direction = 0; direction < 6; direction++)
            {
                var next = h.Neighbor(direction);
                if (!Inside(next) || next.Distance(end) != h.Distance(end) - 1) continue;
                if (Blocked(content, map, next)) return memo[h] = 100000;
                int penalty = next == end ? 0 : state.Units.Any(u => u.Q == next.Q && u.R == next.R) ? 1 : 0;
                penalty += map.Objects.Where(o => o.Q == next.Q && o.R == next.R).Sum(o => content.Objects[o.Definition].AttackPenalty);
                worst = Math.Max(worst, penalty + Cover(next));
            }
            return memo[h] = worst;
        }
        int cover = Cover(new(unit.Q, unit.R));
        return cover >= 100000 ? (false, 0, "Объект перекрывает линию огня.") : (true, cover, "");
    }
}
