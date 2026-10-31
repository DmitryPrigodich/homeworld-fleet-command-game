namespace FleetCommand.Core;

public sealed record PendingOrder(string Card, int Side, string[] Units, int Step, int Direction = -1);
public static class SpecialOrders
{
    public static bool Formation(string card) => card.Length >= 6 && int.TryParse(card.AsSpan(6), out int number) && number is >= 9 and <= 16;
    public static string Name(string card) => int.TryParse(card.Length >= 6 ? card.AsSpan(6) : "".AsSpan(), out int n) ? n switch
    { <= 8 => "All Ahead Full", <= 16 => "Stay in Formation", <= 20 => "Interrupt Attack", _ => "Базовый приказ " + n } : "Неизвестная карта";
    public static bool Supported(string card) => card.StartsWith("order-") && int.TryParse(card.AsSpan(6), out int n) && n is >= 1 and <= 16;
    public static string Action(PendingOrder order) => Formation(order.Card) ? new[] { "move", "attack", "lightning", "attack" }[order.Step] : "move";
    public static string Actor(PendingOrder order) => order.Units[Formation(order.Card) && order.Step >= 2 ? 1 : 0];
    public static string ActionName(PendingOrder order) => Action(order) switch { "attack" => "Атака", "lightning" => "Lightning Move", _ => "Перемещение" };
    public static int Steps(PendingOrder order) => Formation(order.Card) ? 4 : 2;
    public static int Lightning(ShipDefinition ship) => ship.Afterburners >= 0 ? ship.Afterburners : ship.Id is "core:kushan-interceptor" or "core:taiidan-interceptor" ? 2 : 0;
    private static BattleState Sandbox(BattleState state, PendingOrder order) => state with
    { Orders0 = 0, Orders1 = 0, ActiveSide = order.Side, Units = state.Units.Select(u => u with { Activated = false }).ToList() };
    internal static ContentCatalog Geometry(ContentCatalog content, BattleState state, PendingOrder order)
    {
        if (Action(order) != "lightning") return content;
        var copy = content.CopyForRuntime(); var unit = state.Units.Single(u => u.Id == Actor(order)); var ship = copy.Ships[unit.Definition];
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
        var map = c.Maps[state.Map]; int cost = 0;
        for (var cell = new Hex(unit.Q, unit.R).Neighbor(direction); ; cell = cell.Neighbor(direction))
        {
            if (SkirmishGeometry.Blocked(c, map, cell) || state.Units.Any(u => u.Q == cell.Q && u.R == cell.R && (u.Side != unit.Side || !c.Rules[map.Rules].FriendlyPassThrough))) return false;
            cost += map.Objects.Where(o => o.Q == cell.Q && o.R == cell.R).Select(o => c.Objects[o.Definition].MoveCost).DefaultIfEmpty(1).Max();
            if (cell == goal) break;
        }
        var ship = c.Ships[unit.Definition];
        return cost <= ship.Move + (ship.Abilities ?? []).Select(id => c.Abilities[id]).Where(a => a.Handler == "move-bonus").Sum(a => a.Value);
    }
    public static UnitActions Actions(ContentCatalog c, BattleState state, int side, string id)
    {
        var order = state.Cards?.Pending;
        if (order == null || state.Finished || order.Side != side || id != Actor(order)) return UnitActions.Empty;
        var unit = state.Units.Single(u => u.Id == id);
        if (Action(order) == "lightning" && Lightning(c.Ships[unit.Definition]) == 0) return UnitActions.Empty;
        var geometry = Geometry(c, state, order);
        var options = new SkirmishRules().GetActions(geometry, Sandbox(state, order), side, id);
        if (Action(order) == "attack") return new([], options.AttackTargets);
        var moves = options.MoveCells.Where(h => !Formation(order.Card) ? Straight(c, state, unit, h, order) :
            Action(order) != "lightning" || state.Units.Any(first => first.Id == order.Units[0] && h.Distance(new(first.Q, first.R)) == 1)).ToArray();
        return new(moves, []);
    }
    public static CommandResult Start(ContentCatalog c, BattleState state, int side, BattleCommand command)
    {
        CommandResult Reject(string message) => new(false, message, state);
        var cards = state.Cards!; var unit = state.Units.Find(u => u.Id == command.Unit);
        if (!Supported(command.Card) || !(side == 0 ? cards.Hand0 : cards.Hand1).Contains(command.Card)) return Reject("Выберите доступную специальную карту.");
        if (state.ActiveSide != side || unit == null || unit.Side != side || unit.Activated || (side == 0 ? state.Orders0 : state.Orders1) >= c.Rules[c.Maps[state.Map].Rules].OrdersPerSide) return Reject("Это подразделение не может получить приказ.");
        string[] units = Formation(command.Card) ? [command.Unit, command.OtherUnit] : [command.Unit];
        if (units.Distinct().Count() != units.Length || units.Any(id => !state.Units.Any(u => u.Id == id && u.Side == side && !u.Activated))) return Reject("Назначьте два разных готовых подразделения.");
        var order = new PendingOrder(command.Card, side, units, 0);
        cards = cards with { Hand0 = cards.Hand0.Where(id => id != command.Card).ToArray(), Hand1 = cards.Hand1.Where(id => id != command.Card).ToArray(), Discard = cards.Discard.Append(command.Card).ToArray(), Pending = order };
        return new(true, Name(command.Card) + ": приказ начат, подразделения назначены.", state with { Cards = cards,
            Units = state.Units.Select(u => units.Contains(u.Id) ? u with { Activated = true } : u).ToList(),
            Orders0 = state.Orders0 + (side == 0 ? 1 : 0), Orders1 = state.Orders1 + (side == 1 ? 1 : 0), Sequence = state.Sequence + 1 });
    }
    public static CommandResult Apply(ContentCatalog c, BattleState state, int side, BattleCommand command)
    {
        var order = state.Cards!.Pending!;
        CommandResult Reject(string message) => new(false, message, state);
        if (order.Side != side) return Reject("Дождитесь завершения специального приказа.");
        BattleState next = state; string message;
        if (command.Kind is "skip" or "finish-order") message = command.Kind == "skip" ? "Действие пропущено." : "Оставшиеся действия пропущены.";
        else
        {
            var expected = Action(order) == "attack" ? "attack" : "move";
            var options = Actions(c, state, side, command.Unit);
            if (command.Kind != expected || command.Unit != Actor(order) || (expected == "move" ? !options.MoveCells.Contains(new(command.Q, command.R)) : !options.AttackTargets.Contains(command.Target)))
                return Reject("Действие не соответствует текущему шагу или цели приказа.");
            return InterruptRules.Start(c, state, side, command, basic: false);
        }
        return Advance(c, state, next, side, message, command.Kind == "finish-order");
    }
    internal static CommandResult Advance(ContentCatalog c, BattleState original, BattleState next, int side, string message, bool finish = false)
    {
        var order = next.Cards!.Pending!;
        order = order with { Step = finish ? Steps(order) : order.Step + 1 };
        while (order.Step < Steps(order) && !next.Units.Any(u => u.Id == Actor(order))) order = order with { Step = order.Step + 1 };
        if (next.Finished || order.Step >= Steps(order))
        {
            next = next with { Cards = next.Cards! with { Pending = null } };
            var result = SkirmishRules.FinishOrder(c, original, next, side, message + " Приказ завершён.");
            return result with { State = result.State.Round != original.Round && !result.State.Finished ? BasicCardRules.Draw(result.State) : result.State };
        }
        return new(true, message, next with { Cards = next.Cards! with { Pending = order }, Sequence = original.Sequence + 1 });
    }
    public static void Validate(BattleState state)
    {
        var order = state.Cards!.Pending;
        if (order == null) return;
        if (!Supported(order.Card) || order.Side is < 0 or > 1 || state.ActiveSide != order.Side || state.Cards.Phase != "orders" || state.Finished
            || order.Units == null || order.Units.Length != (Formation(order.Card) ? 2 : 1) || order.Units.Distinct().Count() != order.Units.Length
            || order.Units.Any(id => state.Units.Any(u => u.Id == id && (u.Side != order.Side || !u.Activated)))
            || order.Step < 0 || order.Step >= Steps(order) || order.Direction is < -1 or > 5 || !state.Cards.Discard.Contains(order.Card))
            throw new InvalidDataException("Invalid pending special order.");
        if (!state.Units.Any(u => u.Id == Actor(order))) throw new InvalidDataException("Pending actor is missing.");
    }
}
