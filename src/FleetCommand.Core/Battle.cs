using System.Text.Json;

namespace FleetCommand.Core;

public sealed record BattleUnit(string Id, string Definition, int Side, int Q, int R, int Facing,
    int Models, int FocusFire = 0, bool Activated = false);
public sealed record BattleState(string Map, string ContentFingerprint, List<BattleUnit> Units,
    int Round = 1, int ActiveSide = 0, int Orders0 = 0, int Orders1 = 0, int Sequence = 0,
    uint RandomState = 12345, int? Winner = null, bool Finished = false);
public sealed record BattleCommand(string Kind, string Unit = "", string Target = "", int Q = 0, int R = 0, int Facing = 0);
public sealed record CommandResult(bool Accepted, string Message, BattleState State);

// Rule implementations are ordinary C# modules. JSON selects/configures one; it never executes code.
public interface IRuleModule
{
    string Id { get; }
    CommandResult Apply(ContentCatalog content, BattleState state, int side, BattleCommand command);
}

public sealed class BattleEngine(ContentCatalog content, IEnumerable<IRuleModule>? modules = null)
{
    private readonly Dictionary<string, IRuleModule> _modules = (modules ?? [new SkirmishRules()]).ToDictionary(m => m.Id);
    public BattleState Create(string mapId, uint seed = 12345)
    {
        var map = content.Maps[mapId];
        if (!_modules.ContainsKey(content.Rules[map.Rules].Handler)) throw new InvalidDataException("Unknown rule module.");
        var units = new List<BattleUnit>();
        for (int side = 0; side < map.Fleets.Length; side++)
            foreach (var placement in content.Fleets[map.Fleets[side]].Units)
                units.Add(new($"s{side}-{units.Count}", placement.Ship, side, placement.Q, placement.R, placement.Facing,
                    content.Ships[placement.Ship].Models));
        return new(mapId, content.Fingerprint, units, RandomState: seed == 0 ? 1u : seed);
    }

    public CommandResult Apply(BattleState state, int side, BattleCommand command)
    {
        if (state.ContentFingerprint != content.Fingerprint) return new(false, "Content mismatch.", state);
        return _modules[content.Rules[content.Maps[state.Map].Rules].Handler].Apply(content, state, side, command);
    }
    public string Save(BattleState state) => JsonSerializer.Serialize(state, JsonData.Options);
    public BattleState Load(string json)
    {
        var s = JsonSerializer.Deserialize<BattleState>(json, JsonData.Options) ?? throw new InvalidDataException("Empty save.");
        if (s.ContentFingerprint != content.Fingerprint || string.IsNullOrEmpty(s.Map) || s.Units == null || !content.Maps.ContainsKey(s.Map)) throw new InvalidDataException("Save requires different content or is invalid.");
        var map = content.Maps[s.Map];
        if (s.RandomState == 0 || s.Round < 1 || s.Sequence < 0 || s.ActiveSide is < 0 or > 1 || s.Orders0 < 0 || s.Orders1 < 0
            || s.Units.Select(u => u.Id).Distinct().Count() != s.Units.Count)
            throw new InvalidDataException("Invalid battle state.");
        var cells = new HashSet<Hex>();
        foreach (var u in s.Units)
            if (!content.Ships.TryGetValue(u.Definition, out var d) || u.Models < 1 || u.Models > d.Models
                || u.Side is < 0 or > 1 || u.Facing is < 0 or > 5 || u.FocusFire < 0 || u.Q < 0 || u.Q >= map.Width || u.R < 0 || u.R >= map.Height
                || !cells.Add(new(u.Q, u.R))) throw new InvalidDataException("Invalid unit in save.");
        return s;
    }
}

// First playable slice, intentionally NOT the complete Fleet Command ruleset.
public sealed class SkirmishRules : IRuleModule
{
    public string Id => "skirmish-v1";
    public CommandResult Apply(ContentCatalog c, BattleState original, int side, BattleCommand cmd)
    {
        CommandResult Reject(string text) => new(false, text, original);
        if (original.Finished) return Reject("Battle finished.");
        if (side != original.ActiveSide) return Reject("Wait for your turn.");
        var map = c.Maps[original.Map];
        var rules = c.Rules[map.Rules];
        var state = original with { Units = [.. original.Units] };
        bool Inside(Hex h) => h.Q >= 0 && h.Q < map.Width && h.R >= 0 && h.R < map.Height;
        bool Blocked(Hex h) => map.Objects.Any(o => o.Q == h.Q && o.R == h.R && c.Objects[o.Definition].BlocksMovement);
        var unit = state.Units.Find(u => u.Id == cmd.Unit);
        string message;
        int Remaining(int s) => s == 0 ? rules.OrdersPerSide - state.Orders0 : rules.OrdersPerSide - state.Orders1;
        if (cmd.Kind == "pass")
        {
            state = side == 0 ? state with { Orders0 = rules.OrdersPerSide } : state with { Orders1 = rules.OrdersPerSide };
            message = $"Side {side + 1} passes.";
        }
        else
        {
            if (unit is null || unit.Side != side || unit.Activated || Remaining(side) <= 0) return Reject("Unit cannot receive an order.");
            var definition = c.Ships[unit.Definition];
            var abilities = (definition.Abilities ?? []).Select(a => c.Abilities[a]).ToArray();
            if (cmd.Kind == "move")
            {
                var goal = new Hex(cmd.Q, cmd.R);
                if (!Inside(goal) || goal == new Hex(unit.Q, unit.R) || Blocked(goal) || state.Units.Any(u => u.Q == goal.Q && u.R == goal.R))
                    return Reject("Destination is occupied or outside the map.");
                // Dijkstra includes terrain costs; range alone is not a legal-movement check.
                var best = new Dictionary<Hex, int> { [new(unit.Q, unit.R)] = 0 };
                var queue = new PriorityQueue<Hex, int>();
                queue.Enqueue(new(unit.Q, unit.R), 0);
                while (queue.TryDequeue(out var hex, out var cost))
                {
                    if (cost != best[hex]) continue;
                    for (int dir = 0; dir < 6; dir++)
                    {
                        var next = hex.Neighbor(dir);
                        if (!Inside(next) || Blocked(next) || state.Units.Any(u => u.Q == next.Q && u.R == next.R && (u.Side != side || !rules.FriendlyPassThrough))) continue;
                        int moveCost = map.Objects.Where(o => o.Q == next.Q && o.R == next.R).Select(o => c.Objects[o.Definition].MoveCost).DefaultIfEmpty(1).Max();
                        int total = cost + moveCost;
                        if (total > definition.Move + abilities.Where(a => a.Handler == "move-bonus").Sum(a => a.Value)
                            || best.TryGetValue(next, out var old) && old <= total) continue;
                        best[next] = total; queue.Enqueue(next, total);
                    }
                }
                if (!best.ContainsKey(goal)) return Reject("No legal path within movement allowance.");
                unit = unit with { Q = cmd.Q, R = cmd.R, Facing = (cmd.Facing % 6 + 6) % 6 };
                message = $"{definition.Name} moves to {cmd.Q},{cmd.R}.";
            }
            else if (cmd.Kind == "attack")
            {
                if (definition.DicePerModel == 0) return Reject("This unit has no ranged weapon.");
                var target = state.Units.Find(u => u.Id == cmd.Target);
                if (target is null || target.Side == side || new Hex(unit.Q, unit.R).Distance(new(target.Q, target.R)) > definition.Range)
                    return Reject("Invalid target or target out of range.");
                var targetDef = c.Ships[target.Definition];
                var start = new Hex(unit.Q, unit.R);
                var end = new Hex(target.Q, target.R);
                // Shortest-path DAG: take the greatest penalty over all shortest paths, not a ray cast.
                var memo = new Dictionary<Hex, int>();
                int Cover(Hex h)
                {
                    if (h == end) return 0;
                    if (memo.TryGetValue(h, out var cached)) return cached;
                    int worst = 0;
                    for (int d = 0; d < 6; d++)
                    {
                        var n = h.Neighbor(d);
                        if (!Inside(n) || n.Distance(end) != h.Distance(end) - 1) continue;
                        if (Blocked(n)) return memo[h] = 100000;
                        int penalty = n == end ? 0 : state.Units.Any(u => u.Q == n.Q && u.R == n.R) ? 1 : 0;
                        penalty += map.Objects.Where(o => o.Q == n.Q && o.R == n.R).Sum(o => c.Objects[o.Definition].AttackPenalty);
                        worst = Math.Max(worst, penalty + Cover(n));
                    }
                    return memo[h] = worst;
                }
                int cover = Cover(start);
                if (cover >= 100000) return Reject("Line of sight blocked.");
                uint random = state.RandomState;
                int Roll() { random ^= random << 13; random ^= random >> 17; random ^= random << 5; return (int)(random % 6) + 1; }
                int penetrating = (definition.Abilities ?? []).Select(a => c.Abilities[a]).Where(a => a.Handler == "penetrating").Sum(a => a.Value);
                var rolls = Enumerable.Range(0, Math.Max(1, definition.DicePerModel * unit.Models - cover)).Select(_ => Roll()).ToArray();
                int rerolls = abilities.Where(a => a.Handler == "rapid-fire").Sum(a => a.Value);
                for (int i = 0; i < rolls.Length && rerolls > 0; i++)
                    if (rolls[i] < definition.Success) { rolls[i] = Roll(); rerolls--; }
                bool rear = rules.RearShots && start.Distance(end.Neighbor(target.Facing + 3)) == start.Distance(end) - 1;
                int armor = Math.Max(0, targetDef.Armor - penetrating - target.FocusFire - (rear ? 1 : 0));
                int killed = Math.Max(0, rolls.Count(d => d >= definition.Success) - armor);
                int focus = rules.FocusFire && targetDef.Models < 6 ? rolls.Count(d => d == 1) : 0;
                state.Units.Remove(target);
                if (target.Models > killed) state.Units.Add(target with { Models = target.Models - killed, FocusFire = Math.Max(0, target.FocusFire + focus - killed) });
                state = state with { RandomState = random };
                message = $"{definition.Name}: [{string.Join(",", rolls)}], {killed} losses, {focus} focus fire.";
            }
            else return Reject("Unknown command.");
            state.Units.RemoveAll(u => u.Id == unit.Id);
            state.Units.Add(unit with { Activated = true });
            state = side == 0 ? state with { Orders0 = state.Orders0 + 1 } : state with { Orders1 = state.Orders1 + 1 };
        }
        bool CanAct(int s) => Remaining(s) > 0 && state.Units.Any(u => u.Side == s && !u.Activated);
        if (!state.Units.Any(u => u.Side == 0) || !state.Units.Any(u => u.Side == 1))
            state = state with { Winner = state.Units.Any(u => u.Side == 0) ? 0 : 1, Finished = true };
        else if (!CanAct(0) && !CanAct(1))
        {
            state = state with { Round = state.Round + 1, Orders0 = 0, Orders1 = 0, ActiveSide = state.Round % 2,
                Units = state.Units.Select(u => u with { Activated = false }).ToList() };
            if (state.Round > rules.RoundLimit) state = state with { Finished = true };
        }
        else state = state with { ActiveSide = CanAct(1 - side) && (rules.AlternatingOrders || !CanAct(side)) ? 1 - side : side };
        return new(true, message, state with { Sequence = original.Sequence + 1 });
    }
}
