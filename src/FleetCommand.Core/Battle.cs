using System.Text.Json;

namespace FleetCommand.Core;

public sealed record BattleUnit(string Id, string Definition, int Side, int Q, int R, int Facing,
    int Models, int FocusFire = 0, bool Activated = false, string Leader = "", int CrewDamage = 0, string[]? Effects = null,
    bool Hulk = false, string AttackWindow = "", string LastAction = "", string LastTarget = "", int LastActionRound = 0,
    string Patrol = "", bool CapUsed = false, int Cargo = 0, string Embarked = "", int Launches = 0, string[]? AdditionalLeaders = null);
public sealed record BattleState(string Map, string ContentFingerprint, List<BattleUnit> Units,
    int Round = 1, int ActiveSide = 0, int Orders0 = 0, int Orders1 = 0, int Sequence = 0,
    uint RandomState = 12345, int? Winner = null, bool Finished = false, List<BattleLogEntry>? Log = null, CardRound? Cards = null,
    bool FactionUsed0 = false, bool FactionUsed1 = false, PendingCombat? Combat = null, int ValorOwner = -1, int ValorUsedMask = 0, PendingRepair[]? Repairs = null,
    PendingOverrun? Overrun = null, int Stars0 = 0, int Stars1 = 0, PendingDefense? Defense = null, PendingCapEntry? CapEntry = null,
    Dictionary<int, int>? Mines = null, HazardJob[]? Hazards = null, PendingHazard? Hazard = null, Dictionary<int, AsteroidDeposit>? Deposits = null, int Harvested0 = 0, int Harvested1 = 0, List<BattleUnit>? Reserves = null, PendingReinforce? Reinforce = null, int Funds0 = 0, int Funds1 = 0, int NextUnit = 0, string[]? Claims = null, int Medals0 = 0, int Medals1 = 0, Dictionary<string, int>? Captures = null, Dictionary<string, UnitLoss>? Losses = null, string EndReason = "", ScenarioState? Scenario = null);
public sealed record UnitDeployment(string Unit, int Q, int R);
public sealed record BattleCommand(string Kind, string Unit = "", string Target = "", int Q = 0, int R = 0, int Facing = 0, string Card = "", string OtherUnit = "",
    string[]? Cards = null, string Weapon = "", bool Power = false, string Repair = "", bool AutoFacing = true, string OtherRepair = "", string AttackKind = "ranged", string Effect = "weapons", int Amount = 0, bool TransferLeader = false, int[]? Dice = null, UnitDeployment[]? Deployments = null, int Object = -1, bool Receive = false, string Definition = "", bool ToBay = false, int ReserveSpend = 0, string Embark = "");
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
    private readonly Dictionary<string, IRuleModule> _modules = (modules ?? [new SkirmishRules(), new BasicCardRules()]).ToDictionary(m => m.Id);
    public BattleState Create(string mapId, uint seed = 12345)
    {
        FleetTerrain.Register(content);
        var map = content.Maps[mapId];
        if (!_modules.ContainsKey(content.Rules[map.Rules].Handler)) throw new InvalidDataException("Unknown rule module.");
        var units = new List<BattleUnit>(); var reserves = new List<BattleUnit>(); int counter = 0;
        for (int side = 0; side < map.Fleets.Length; side++)
        {
            int first = counter; var placements = content.Fleets[map.Fleets[side]].Units;
            foreach (var placement in placements)
            {
                bool offboard = placement.Reserve || placement.EmbarkedOn >= 0;
                var unit = new BattleUnit($"s{side}-{counter++}", placement.Ship, side, offboard ? -1 : placement.Q, offboard ? -1 : placement.R, placement.Facing,
                    content.Ships[placement.Ship].Models, Leader: placement.Leader, Cargo: placement.Cargo, Embarked: placement.EmbarkedOn >= 0 ? $"s{side}-{first + placement.EmbarkedOn}" : "", AdditionalLeaders: placement.AdditionalLeaders);
                (offboard ? reserves : units).Add(unit);
            }
        }
        var state = new BattleState(mapId, content.Fingerprint, units, Reserves: reserves.Count == 0 ? null : reserves, NextUnit: counter, Funds0: map.ReserveStars?[0] ?? 0, Funds1: map.ReserveStars?[1] ?? 0, RandomState: seed == 0 ? 1u : seed,
            Log: [new(0, 1, -1, "Battle started."), new(1, 1, 0, "First order.")]);
        state = FleetEconomy.Initialize(content, state);
        state = content.Rules[map.Rules].Handler == "cards-basic-v1" ? BasicCardRules.Initialize(state, content) : state;
        return FleetObjectives.Refresh(content, ScenarioEvents.Process(content, state, "round-start"));
    }

    public CommandResult Apply(BattleState state, int side, BattleCommand command)
    {
        if (state.ContentFingerprint != content.Fingerprint) return new(false, "Game content does not match.", state);
        var result = _modules[content.Rules[content.Maps[state.Map].Rules].Handler].Apply(content, state, side, command);
        if (!result.Accepted) return result;
        var settled = FleetObjectives.Check(content, ScenarioEvents.Prune(FleetCap.Sync(state, result.State)));
        if (settled.Finished && FleetObjectives.Enabled(content, settled)) settled = FleetObjectives.Close(settled);
        result = result with { State = settled, Message = result.Message + FleetObjectives.Changes(state, settled) + ScenarioEvents.Changes(content, state, settled) };
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
        FleetTerrain.Register(content);
        var s = JsonSerializer.Deserialize<BattleState>(json, JsonData.Options) ?? throw new InvalidDataException("Empty save.");
        if (s.ContentFingerprint != content.Fingerprint || string.IsNullOrEmpty(s.Map) || s.Units == null || !content.Maps.ContainsKey(s.Map)) throw new InvalidDataException("Save requires different content or is invalid.");
        var map = content.Maps[s.Map];
        if (s.RandomState == 0 || s.Round < 1 || s.Sequence < 0 || s.ActiveSide is < 0 or > 1 || s.Orders0 < 0 || s.Orders1 < 0 || s.ValorOwner is < -1 or > 1 || s.ValorUsedMask is < 0 or > 3 || s.Stars0 < 0 || s.Stars1 < 0
            || s.Units.Select(u => u.Id).Distinct().Count() != s.Units.Count)
            throw new InvalidDataException("Invalid battle state.");
        var cells = new HashSet<Hex>();
        foreach (var u in s.Units)
            if (!content.Ships.TryGetValue(u.Definition, out var d) || u.Models < 1 || u.Models > d.Models
                || u.Side is < 0 or > 1 || u.Facing is < 0 or > 5 || u.Cargo is < 0 or > 1000000 || u.FocusFire < 0 || u.CrewDamage < 0 || u.Leader == null || !FleetRuleTools.ValidEffects(u.Effects) || !FleetRuleTools.ValidLeader(content, u)
                || u.LastAction is not ("" or "move" or "ranged" or "ram" or "board") || u.LastTarget == null || u.LastActionRound < 0 || u.LastActionRound > s.Round
                || FleetRuleTools.Enabled(content, s) && u.Hulk != FleetClose.CheckHulk(content, u).Hulk
                || !FleetFootprint.Cells(content, u).All(h => FleetFootprint.Inside(map, h))
                || u.Embarked != "" || u.Launches < 0 || u.Patrol == null || u.Patrol == "" && FleetFootprint.Cells(content, u).Any(h => !cells.Add(h))) throw new InvalidDataException("Invalid unit in save.");
        if (s.Log != null && (s.Log.Count > BattleLog.Capacity || s.Log.Any(e => e.Number < 0 || e.Round < 1 || e.Round > s.Round
                || e.Side is < -1 or > 1 || string.IsNullOrWhiteSpace(e.Text) || e.Text.Length > 4096)
            || !s.Log.Select(e => e.Number).SequenceEqual(s.Log.Select(e => e.Number).Distinct().Order())))
            throw new InvalidDataException("Invalid battle log in save.");
        ScenarioEvents.Validate(content, s);
        FleetObjectives.Validate(content, s);
        FleetLogistics.Validate(content, s);
        FleetEconomy.Validate(content, s);
        FleetTerrain.Validate(content, s);
        FleetCap.Validate(content, s);
        if (content.Rules[map.Rules].Handler == "cards-basic-v1") { BasicCardRules.Validate(content, s); InterruptRules.ValidateGeometry(content, s); FleetDefense.Validate(content, s); FleetCombat.Validate(content, s); FleetRepairs.Validate(s); FleetClose.Validate(content, s); }
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
        if (state.Finished || side != state.ActiveSide || unit == null || unit.Side != side || unit.Activated || unit.Hulk || unit.Patrol != ""
            || (side == 0 ? state.Orders0 : state.Orders1) >= FleetRuleTools.PlayLimit(content, state, side)) return UnitActions.Empty;
        return new(SkirmishGeometry.Movement(content, state, unit).OrderBy(h => h.Q).ThenBy(h => h.R).ToArray(),
            state.Units.Where(target => SkirmishGeometry.Attack(content, state, unit, target).Allowed || FleetRuleTools.Enabled(content, state)
                && (FleetClose.Allowed(content, state, unit, target, "ram") || FleetClose.Allowed(content, state, unit, target, "board"))).Select(target => target.Id).ToArray());
    }
    public CommandResult Apply(ContentCatalog c, BattleState original, int side, BattleCommand cmd)
    {
        CommandResult Reject(string text) => new(false, text, original);
        if (original.Finished) return Reject("The battle is already over.");
        if (side != original.ActiveSide) return Reject("Wait for your turn.");
        var map = c.Maps[original.Map];
        var rules = c.Rules[map.Rules];
        var state = original with { Units = [.. original.Units] };
        bool Inside(Hex h) => h.Q >= 0 && h.Q < map.Width && h.R >= 0 && h.R < map.Height;
        var unit = state.Units.Find(u => u.Id == cmd.Unit);
        string message;
        int Remaining(int s) => FleetRuleTools.PlayLimit(c, state, s) - (s == 0 ? state.Orders0 : state.Orders1);
        if (cmd.Kind == "pass")
        {
            state = side == 0 ? state with { Orders0 = FleetRuleTools.PlayLimit(c, state, 0) } : state with { Orders1 = FleetRuleTools.PlayLimit(c, state, 1) };
            message = "Remaining orders passed.";
        }
        else
        {
            if (unit is null || unit.Side != side) return Reject("Select one of your units.");
            if (unit.Activated || unit.Hulk) return Reject("This unit has already acted or is a drifting hulk.");
            if (Remaining(side) <= 0) return Reject("No orders remaining this round.");
            var definition = c.Ships[unit.Definition];
            var abilities = (definition.Abilities ?? []).Select(a => c.Abilities[a]).ToArray();
            if (cmd.Kind == "move")
            {
                var goal = new Hex(cmd.Q, cmd.R);
                if (!Inside(goal) || goal == new Hex(unit.Q, unit.R) || SkirmishGeometry.Blocked(c, map, goal) || state.Units.Any(u => u.Q == goal.Q && u.R == goal.R))
                    return Reject("The hex is occupied, impassable or outside the battlefield.");
                if (!SkirmishGeometry.Movement(c, original, unit).Contains(goal)) return Reject("Not enough movement to reach that hex, or no clear path.");
                unit = unit with { Q = cmd.Q, R = cmd.R, Facing = (cmd.Facing % 6 + 6) % 6 };
                message = $"{FleetPresentation.UnitName(c, original, unit)}: moving to hex ({cmd.Q + 1}, {cmd.R + 1}).";
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
                message = $"{FleetPresentation.UnitName(c, original, unit)} → enemy: {FleetPresentation.UnitName(c, original, target)}. " +
                    $"Rolls: [{string.Join(", ", rolls)}]. Target losses: {Math.Min(killed, target.Models)}; remaining: {Math.Max(0, target.Models - killed)}." +
                    (focus > 0 ? $" Focus Fire tokens: +{focus}." : "") + (killed >= target.Models ? " Unit destroyed." : "");
            }
            else return Reject("Unknown order.");
            state.Units.RemoveAll(u => u.Id == unit.Id);
            state.Units.Add(unit with { Activated = true });
            state = side == 0 ? state with { Orders0 = state.Orders0 + 1 } : state with { Orders1 = state.Orders1 + 1 };
        }
        return FinishOrder(c, original, state, side, message);
    }
    internal static CommandResult FinishOrder(ContentCatalog c, BattleState original, BattleState state, int side, string message)
    {
        var rules = c.Rules[c.Maps[state.Map].Rules];
        state = ScenarioEvents.FinishOrder(c, original, state, side);
        state = FleetObjectives.Check(c, state);
        if (state.Finished) return new(true, message, state with { Sequence = original.Sequence + 1 });
        int Remaining(int s) => FleetRuleTools.PlayLimit(c, state, s) - (s == 0 ? state.Orders0 : state.Orders1);
        bool CanAct(int s) => Remaining(s) > 0 && (FleetLogistics.CanReinforce(c, state, s) || state.Units.Any(u => u.Side == s && !u.Activated && !u.Hulk && (u.Patrol == "" || FleetCap.ExitCells(c, state, u).Length > 0)));
        if (!FleetObjectives.Enabled(c, state) && (!FleetLogistics.HasForce(state, 0) || !FleetLogistics.HasForce(state, 1)))
            state = FleetClose.Outcome(state, c);
        else if (!CanAct(0) && !CanAct(1))
        {
            state = ScenarioEvents.Process(c, state, "round-end");
            state = state with { Round = state.Round + 1, Orders0 = 0, Orders1 = 0, ActiveSide = state.Round % 2,
                Units = state.Units.Select(u => u with { Activated = false, CapUsed = false, Launches = 0 }).ToList(),
                Reserves = state.Reserves?.Select(u => u with { Activated = false, Launches = 0 }).ToList() };
            state = ScenarioEvents.Process(c, state, state.Round > rules.RoundLimit ? "battle-end" : "round-start");
            if (state.Round > rules.RoundLimit) state = FleetObjectives.Enabled(c, state) ? FleetObjectives.AtLimit(c, state) : state with { Finished = true };
        }
        else state = state with { ActiveSide = CanAct(1 - side) && (rules.AlternatingOrders || !CanAct(side)) ? 1 - side : side };
        return new(true, message, state with { Sequence = original.Sequence + 1 });
    }
}
