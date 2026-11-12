namespace FleetCommand.Core;

public sealed record PendingOrder(string Card, int Side, string[] Units, int Step, int Direction = -1, string BasicAction = "", BattleCommand? Deferred = null);
public static class SpecialOrders
{
    public static bool Formation(ContentCatalog c, BattleState? state, string card) => CommandCards.Find(c, state, card)?.Lines?.Length == 2;
    public static string Name(ContentCatalog c, BattleState? state, string card) => CommandCards.Name(c, state, card);
    public static bool Supported(ContentCatalog c, BattleState? state, string card) => CommandCards.Find(c, state, card)?.Kind == "order";
    public static bool StraightCourse(ContentCatalog c, BattleState state, PendingOrder order) => order.BasicAction == "" && CommandCards.Find(c, state, order.Card)?.StraightCourse == true;
    public static bool AdjacentOnLightning(ContentCatalog c, BattleState state, PendingOrder order) => order.BasicAction == "" && CommandCards.Find(c, state, order.Card)?.AdjacentOnLightning == true;
    private static (int Line, string Action) Step(ContentCatalog c, BattleState state, PendingOrder order)
    {
        int offset = order.Step;
        var lines = CommandCards.Find(c, state, order.Card)!.Lines!;
        for (int line = 0; line < lines.Length; line++)
        {
            if (offset < lines[line].Actions.Length) return (line, lines[line].Actions[offset]);
            offset -= lines[line].Actions.Length;
        }
        throw new InvalidDataException("Invalid command card step.");
    }
    public static string Action(ContentCatalog c, BattleState state, PendingOrder order) => order.BasicAction == "harvest" ? order.Step == 0 ? "move" : "harvest" : order.BasicAction != "" ? order.BasicAction : Step(c, state, order).Action;
    public static string Actor(ContentCatalog c, BattleState state, PendingOrder order) => order.Units[order.BasicAction != "" ? 0 : Step(c, state, order).Line];
    public static string ActionName(ContentCatalog c, BattleState state, PendingOrder order) => Action(c, state, order) switch { "attack" => "Attack", "lightning" => "Lightning Move", "harvest" => "Resource harvesting", _ => "Movement" };
    public static int Steps(ContentCatalog c, BattleState state, PendingOrder order) => order.BasicAction == "harvest" ? 2 : order.BasicAction != "" ? 1 : CommandCards.Find(c, state, order.Card)!.Lines!.Sum(l => l.Actions.Length);
    public static int Lightning(ShipDefinition ship) => ship.Afterburners >= 0 ? ship.Afterburners : ship.Id is "core:kushan-interceptor" or "core:taiidan-interceptor" ? 2 : 0;
    private static BattleState Sandbox(ContentCatalog c, BattleState state, PendingOrder order) => state with
    { Orders0 = FleetRuleTools.Enabled(c, state) ? state.Orders0 : 0, Orders1 = FleetRuleTools.Enabled(c, state) ? state.Orders1 : 0,
        Cards = FleetRuleTools.Enabled(c, state) ? state.Cards! with { PlayLimit0 = state.Orders0 + 1, PlayLimit1 = state.Orders1 + 1 } : state.Cards,
        ActiveSide = order.Side, Units = state.Units.Select(u => u with { Activated = false }).ToList() };
    internal static ContentCatalog Geometry(ContentCatalog content, BattleState state, PendingOrder order)
    {
        if (Action(content, state, order) != "lightning") return content;
        var copy = content.CopyForRuntime(); var unit = state.Units.Single(u => u.Id == Actor(content, state, order)); var ship = copy.Ships[unit.Definition];
        copy.Ships[ship.Id] = ship with { Move = Lightning(ship), Abilities = (ship.Abilities ?? []).Where(id => copy.Abilities[id].Handler != "move-bonus").ToArray() };
        return copy;
    }
    internal static int StraightDirection(Hex start, Hex end)
    {
        for (int d = 0; d < 6; d++) for (int n = 1; n <= start.Distance(end); n++)
            if (new Hex(start.Q + Hex.Directions[d].Q * n, start.R + Hex.Directions[d].R * n) == end) return d;
        return -1;
    }
    private static bool Straight(ContentCatalog c, BattleState state, BattleUnit unit, Hex goal, PendingOrder order)
    {
        int direction = StraightDirection(new(unit.Q, unit.R), goal);
        if (direction < 0 || order.Direction >= 0 && direction != order.Direction) return false;
        if (FleetRuleTools.Enabled(c, state)) return FleetMovement.StraightPath(c, state, unit, goal) != null;
        var map = c.Maps[state.Map]; int cost = 0;
        for (var cell = new Hex(unit.Q, unit.R).Neighbor(direction); ; cell = cell.Neighbor(direction))
        {
            if (SkirmishGeometry.Blocked(c, map, cell, state) || state.Units.Any(u => u.Q == cell.Q && u.R == cell.R && (u.Side != unit.Side || !c.Rules[map.Rules].FriendlyPassThrough))) return false;
            cost += map.Objects.Where(o => o.Q == cell.Q && o.R == cell.R).Select(o => c.Objects[o.Definition].MoveCost).DefaultIfEmpty(1).Max();
            if (cell == goal) break;
        }
        var ship = c.Ships[unit.Definition];
        return cost <= ship.Move + (ship.Abilities ?? []).Select(id => c.Abilities[id]).Where(a => a.Handler == "move-bonus").Sum(a => a.Value);
    }
    public static UnitActions Actions(ContentCatalog c, BattleState state, int side, string id)
    {
        var order = state.Cards?.Pending;
        if (order == null || state.Finished || order.Side != side || id != Actor(c, state, order)) return UnitActions.Empty;
        if (Action(c, state, order) == "harvest") return new([], [], FleetEconomy.Targets(c, state, side, id));
        var unit = state.Units.Single(u => u.Id == id);
        if (Action(c, state, order) == "lightning" && (Lightning(c.Ships[unit.Definition]) == 0 || FleetRuleTools.Enabled(c, state) && FleetRuleTools.Has(unit, "engines"))) return UnitActions.Empty;
        var geometry = Geometry(c, state, order);
        var options = new SkirmishRules().GetActions(geometry, Sandbox(c, state, order), side, id);
        if (Action(c, state, order) == "attack") return new([], options.AttackTargets);
        if (order.BasicAction != "") return new(options.MoveCells, []);
        var moves = options.MoveCells.Where(h => StraightCourse(c, state, order) ? Straight(c, state, unit, h, order) :
            Action(c, state, order) != "lightning" || !AdjacentOnLightning(c, state, order) || state.Units.Any(first => first.Id == order.Units[0] && h.Distance(new(first.Q, first.R)) == 1)).ToArray();
        return new(moves, []);
    }
    public static CommandResult Start(ContentCatalog c, BattleState state, int side, BattleCommand command)
    {
        CommandResult Reject(string message) => new(false, message, state);
        var cards = state.Cards!;
        bool partialFormation = FleetRuleTools.Enabled(c, state) && Formation(c, state, command.Card);
        var unit = state.Units.Find(u => u.Id == (command.Unit == "" && partialFormation ? command.OtherUnit : command.Unit));
        if (!Supported(c, state, command.Card) || !(side == 0 ? cards.Hand0 : cards.Hand1).Contains(command.Card)) return Reject("Select an available special card.");
        if (state.ActiveSide != side || unit == null || unit.Side != side || unit.Activated || unit.Hulk || (side == 0 ? state.Orders0 : state.Orders1) >= FleetRuleTools.PlayLimit(c, state, side)) return Reject("This unit cannot receive an order.");
        string[] units = Formation(c, state, command.Card) ? [command.Unit, command.OtherUnit] : [command.Unit];
        if (units.Distinct().Count() != units.Length || units.All(id => id == "") || units.Any(id => (id != "" || !partialFormation) && !state.Units.Any(u => u.Id == id && u.Side == side && !u.Activated && !u.Hulk))) return Reject("Assign different ready units to the action lines you want to use.");
        var order = new PendingOrder(command.Card, side, units, partialFormation && command.Unit == "" ? CommandCards.Find(c, state, command.Card)!.Lines![0].Actions.Length : 0);
        cards = cards with { Hand0 = cards.Hand0.Where(id => id != command.Card).ToArray(), Hand1 = cards.Hand1.Where(id => id != command.Card).ToArray(), Discard = cards.Discard.Append(command.Card).ToArray(), Pending = order };
        var next = state with { Cards = cards,
            Units = state.Units.Select(u => units.Contains(u.Id) ? (FleetRuleTools.Enabled(c, state) ? FleetRuleTools.NewOrder(u) : u) with { Activated = true } : u).ToList(),
            Orders0 = state.Orders0 + (side == 0 ? 1 : 0), Orders1 = state.Orders1 + (side == 1 ? 1 : 0), Sequence = state.Sequence + 1 };
        string message = Name(c, state, command.Card) + ": order started and units assigned. ";
        if (FleetRuleTools.Enabled(c, state)) for (int i = 0; i < units.Length; i++)
        { if (units[i] == "") continue; next = FleetRuleTools.DamageControl(c, next, units[i], i == 0 ? command.Repair : command.OtherRepair, out var repair); message += repair; }
        return FleetTerrain.Activation(c, state, next, units, "order", message);
    }
    public static CommandResult BeginBasic(ContentCatalog c, BattleState state, int side, BattleCommand command)
    {
        var unit = state.Units.Find(u => u.Id == command.Unit && u.Side == side && !u.Activated && !u.Hulk);
        var cards = state.Cards!;
        if (!FleetRuleTools.Enabled(c, state) || unit == null || state.ActiveSide != side || command.Target is not ("move" or "attack" or "harvest") || command.Target == "harvest" && c.Ships[unit.Definition].Harvest < 0
            || !(side == 0 ? cards.Hand0 : cards.Hand1).Contains(command.Card) || (side == 0 ? state.Orders0 : state.Orders1) >= FleetRuleTools.PlayLimit(c, state, side))
            return new(false, "Basic order unavailable.", state);
        var next = state with { Units = state.Units.Select(u => u.Id == unit.Id ? FleetRuleTools.NewOrder(u) with { Activated = true } : u).ToList(),
            Orders0 = state.Orders0 + (side == 0 ? 1 : 0), Orders1 = state.Orders1 + (side == 1 ? 1 : 0), Sequence = state.Sequence + 1,
            Cards = cards with { Hand0 = cards.Hand0.Where(id => id != command.Card).ToArray(), Hand1 = cards.Hand1.Where(id => id != command.Card).ToArray(),
                Discard = cards.Discard.Append(command.Card).ToArray(), Pending = new(command.Card, side, [unit.Id], 0, BasicAction: command.Target) } };
        next = FleetRuleTools.DamageControl(c, next, unit.Id, command.Repair, out var message);
        return FleetTerrain.Activation(c, state, next, [unit.Id], "order", "Basic order started. " + message);
    }
    public static CommandResult Apply(ContentCatalog c, BattleState state, int side, BattleCommand command)
    {
        var order = state.Cards!.Pending!;
        CommandResult Reject(string message) => new(false, message, state);
        if (order.Side != side) return Reject("Wait for the special order to finish.");
        BattleState next = state; string message;
        if (command.Kind == "damage-control" && FleetRuleTools.Enabled(c, state) && Action(c, state, order) == "attack" && FleetRuleTools.Has(state.Units.Single(u => u.Id == Actor(c, state, order)), "weapons"))
        {
            if (command.Repair != "ask" && command.Repair != "focus" && !FleetRuleTools.Debuffs.Contains(command.Repair)) return Reject("Choose an effect for Damage Control.");
            next = FleetRuleTools.DamageControl(c, state, Actor(c, state, order), command.Repair, out message, advance: true);
            if (next.Repairs is { Length: > 0 }) return new(true, message, next with { Sequence = state.Sequence + 1 });
            return Advance(c, state, next, side, message);
        }
        if (command.Kind == "embark" && Action(c, state, order) is "move" or "lightning" && command.Unit == Actor(c, state, order))
        {
            if (StraightCourse(c, state, order) && order.Direction >= 0
                && !state.Units.Any(p => p.Id == command.Target && StraightDirection(new(state.Units.Single(u => u.Id == command.Unit).Q, state.Units.Single(u => u.Id == command.Unit).R), new(p.Q, p.R)) == order.Direction)) return Reject("Embarkation must continue the order's straight course.");
            return InterruptRules.Start(c, state, side, command, basic: false);
        }
        if (command.Kind == "harvest") return FleetEconomy.Harvest(c, state, side, command);
        if (command.Kind is "skip" or "finish-order") message = command.Kind == "skip" ? "Action skipped." : "Remaining actions skipped.";
        else
        {
            var expected = Action(c, state, order) == "attack" ? "attack" : "move";
            var options = Actions(c, state, side, command.Unit);
            bool turn = expected == "move" && FleetRuleTools.Enabled(c, state) && !command.AutoFacing && state.Units.Any(u => u.Id == command.Unit && u.Q == command.Q && u.R == command.R);
            if (command.Kind != expected || command.Unit != Actor(c, state, order) || (expected == "move" ? !turn && !options.MoveCells.Contains(new(command.Q, command.R)) : !options.AttackTargets.Contains(command.Target)))
                return Reject("The action does not match the order's current step or target.");
            return InterruptRules.Start(c, state, side, command, basic: false);
        }
        return Advance(c, state, next, side, message, command.Kind == "finish-order");
    }
    internal static CommandResult Advance(ContentCatalog c, BattleState original, BattleState next, int side, string message, bool finish = false)
    {
        var order = next.Cards!.Pending!;
        order = order with { Step = finish ? Steps(c, next, order) : order.Step + 1 };
        while (order.Step < Steps(c, next, order) && !next.Units.Any(u => u.Id == Actor(c, next, order) && u.Patrol == "")) order = order with { Step = order.Step + 1 };
        if (next.Finished || order.Step >= Steps(c, next, order))
        {
            next = next with { Cards = next.Cards! with { Pending = null } };
            var result = SkirmishRules.FinishOrder(c, original, next, side, message + " Order complete.");
            return result with { State = result.State.Round != original.Round && !result.State.Finished ? BasicCardRules.NextRound(c, result.State) : result.State };
        }
        return new(true, message, next with { Cards = next.Cards! with { Pending = order }, Sequence = original.Sequence + 1 });
    }
    public static void Validate(ContentCatalog c, BattleState state)
    {
        var order = state.Cards!.Pending;
        if (order == null) return;
        if ((order.BasicAction == "" ? !Supported(c, state, order.Card) : order.BasicAction is not ("move" or "attack" or "harvest" or "transfer-crew" or "transfer-stars")) || order.Side is < 0 or > 1 || state.ActiveSide != order.Side || state.Cards.Phase != "orders" || state.Finished
            || order.Units == null || order.Units.Length != (order.BasicAction == "" && Formation(c, state, order.Card) ? 2 : 1) || order.Units.Distinct().Count() != order.Units.Length
            || order.Units.Any(id => state.Units.Any(u => u.Id == id && (u.Side != order.Side || !u.Activated)))
            || order.Step < 0 || order.Step >= Steps(c, state, order) || order.Direction is < -1 or > 5 || !state.Cards.Discard.Contains(order.Card))
            throw new InvalidDataException("Invalid pending special order.");
        if (!state.Units.Any(u => u.Id == Actor(c, state, order)) && state.Hazards is not { Length: > 0 }) throw new InvalidDataException("Pending actor is missing.");
    }
}
