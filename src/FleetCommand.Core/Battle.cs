using System.Text.Json;

namespace FleetCommand.Core;

public sealed record BattleUnit(string Id, string Definition, int Side, int Q, int R, int Facing,
    int Models, int FocusFire = 0, bool Activated = false);
public sealed record BattleState(string Map, string ContentFingerprint, List<BattleUnit> Units,
    int Round = 1, int ActiveSide = 0, int Orders0 = 0, int Orders1 = 0, int Sequence = 0,
    uint RandomState = 12345, int? Winner = null, bool Finished = false, List<BattleLogEntry>? Log = null);
public sealed record BattleCommand(string Kind, string Unit = "", string Target = "", int Q = 0, int R = 0, int Facing = 0);
public sealed record CommandResult(bool Accepted, string Message, BattleState State);

// Rule implementations are ordinary C# modules. JSON selects/configures one; it never executes code.
public interface IRuleModule
{
    string Id { get; }
    CommandResult Apply(ContentCatalog content, BattleState state, int side, BattleCommand command);
    UnitActions GetActions(ContentCatalog content, BattleState state, int side, string unitId) => UnitActions.Empty;
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
        return new(mapId, content.Fingerprint, units, RandomState: seed == 0 ? 1u : seed,
            Log: [new(0, 1, -1, "Бой начался."), new(1, 1, 0, "Первый приказ.")]);
    }

    public CommandResult Apply(BattleState state, int side, BattleCommand command)
    {
        if (state.ContentFingerprint != content.Fingerprint) return new(false, "Набор игровых данных не совпадает.", state);
        var result = _modules[content.Rules[content.Maps[state.Map].Rules].Handler].Apply(content, state, side, command);
        if (!result.Accepted) return result;
        return result with { State = result.State with { Log = BattleLog.AfterCommand(state, result.State, side, result.Message) } };
    }
    public UnitActions GetActions(BattleState state, int side, string unitId)
    {
        if (state.ContentFingerprint != content.Fingerprint) return UnitActions.Empty;
        return _modules[content.Rules[content.Maps[state.Map].Rules].Handler].GetActions(content, state, side, unitId);
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
        if (s.Log != null && (s.Log.Count > BattleLog.Capacity || s.Log.Any(e => e.Number < 0 || e.Round < 1 || e.Round > s.Round
                || e.Side is < -1 or > 1 || string.IsNullOrWhiteSpace(e.Text) || e.Text.Length > 4096)
            || !s.Log.Select(e => e.Number).SequenceEqual(s.Log.Select(e => e.Number).Distinct().Order())))
            throw new InvalidDataException("Invalid battle log in save.");
        return s;
    }
}

// First playable slice, intentionally NOT the complete Fleet Command ruleset.
public sealed class SkirmishRules : IRuleModule
{
    public string Id => "skirmish-v1";
    public UnitActions GetActions(ContentCatalog content, BattleState state, int side, string unitId)
    {
        var unit = state.Units.Find(u => u.Id == unitId);
        var rules = content.Rules[content.Maps[state.Map].Rules];
        if (state.Finished || side != state.ActiveSide || unit == null || unit.Side != side || unit.Activated
            || (side == 0 ? state.Orders0 : state.Orders1) >= rules.OrdersPerSide) return UnitActions.Empty;
        return new(SkirmishGeometry.Movement(content, state, unit).OrderBy(h => h.Q).ThenBy(h => h.R).ToArray(),
            state.Units.Where(target => SkirmishGeometry.Attack(content, state, unit, target).Allowed).Select(target => target.Id).ToArray());
    }
    public CommandResult Apply(ContentCatalog c, BattleState original, int side, BattleCommand cmd)
    {
        CommandResult Reject(string text) => new(false, text, original);
        if (original.Finished) return Reject("Бой уже завершён.");
        if (side != original.ActiveSide) return Reject("Дождись своего хода.");
        var map = c.Maps[original.Map];
        var rules = c.Rules[map.Rules];
        var state = original with { Units = [.. original.Units] };
        bool Inside(Hex h) => h.Q >= 0 && h.Q < map.Width && h.R >= 0 && h.R < map.Height;
        var unit = state.Units.Find(u => u.Id == cmd.Unit);
        string message;
        int Remaining(int s) => s == 0 ? rules.OrdersPerSide - state.Orders0 : rules.OrdersPerSide - state.Orders1;
        if (cmd.Kind == "pass")
        {
            state = side == 0 ? state with { Orders0 = rules.OrdersPerSide } : state with { Orders1 = rules.OrdersPerSide };
            message = "Оставшиеся приказы пропущены.";
        }
        else
        {
            if (unit is null || unit.Side != side) return Reject("Выбери своё подразделение.");
            if (unit.Activated) return Reject("Это подразделение уже выполнило приказ. Дождись нового раунда.");
            if (Remaining(side) <= 0) return Reject("Приказы на этот раунд закончились.");
            var definition = c.Ships[unit.Definition];
            var abilities = (definition.Abilities ?? []).Select(a => c.Abilities[a]).ToArray();
            if (cmd.Kind == "move")
            {
                var goal = new Hex(cmd.Q, cmd.R);
                if (!Inside(goal) || goal == new Hex(unit.Q, unit.R) || SkirmishGeometry.Blocked(c, map, goal) || state.Units.Any(u => u.Q == goal.Q && u.R == goal.R))
                    return Reject("Клетка занята, непроходима или находится за границей поля.");
                if (!SkirmishGeometry.Movement(c, original, unit).Contains(goal)) return Reject("До этой клетки не хватает движения или нет свободного пути.");
                unit = unit with { Q = cmd.Q, R = cmd.R, Facing = (cmd.Facing % 6 + 6) % 6 };
                message = $"{FleetPresentation.UnitName(c, original, unit)}: перемещение в клетку ({cmd.Q + 1}, {cmd.R + 1}).";
            }
            else if (cmd.Kind == "attack")
            {
                var target = state.Units.Find(u => u.Id == cmd.Target);
                var attack = SkirmishGeometry.Attack(c, original, unit, target);
                if (!attack.Allowed || target is null) return Reject(attack.Error);
                var targetDef = c.Ships[target.Definition];
                var start = new Hex(unit.Q, unit.R);
                var end = new Hex(target.Q, target.R);
                int cover = attack.Cover;
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
                message = $"{FleetPresentation.UnitName(c, original, unit)} → противник: {FleetPresentation.UnitName(c, original, target)}. " +
                    $"Броски: [{string.Join(", ", rolls)}]. Потери цели: {Math.Min(killed, target.Models)}; осталось: {Math.Max(0, target.Models - killed)}." +
                    (focus > 0 ? $" Метки сосредоточенного огня: +{focus}." : "") + (killed >= target.Models ? " Подразделение уничтожено." : "");
            }
            else return Reject("Неизвестный приказ.");
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
