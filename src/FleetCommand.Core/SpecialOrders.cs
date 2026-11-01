namespace FleetCommand.Core;

public sealed record PendingOrder(string Card, int Side, string[] Units, int Step, int Direction = -1, string BasicAction = "");
public static class SpecialOrders
{
    public static bool Formation(string card) => card != null && card.Length >= 6 && int.TryParse(card.AsSpan(6), out int number) && number is >= 9 and <= 16;
    public static string Name(string card) => int.TryParse(card != null && card.Length >= 6 ? card.AsSpan(6) : "".AsSpan(), out int n) ? n switch
    { <= 8 => "All Ahead Full", <= 16 => "Stay in Formation", <= 20 => "Interrupt Attack", _ => "Базовый приказ " + n } : "Неизвестная карта";
    public static bool Supported(string card) => card != null && card.StartsWith("order-") && int.TryParse(card.AsSpan(6), out int n) && n is >= 1 and <= 16;
    public static string Action(PendingOrder order) => order.BasicAction != "" ? order.BasicAction : Formation(order.Card) ? new[] { "move", "attack", "lightning", "attack" }[order.Step] : "move";
    public static string Actor(PendingOrder order) => order.Units[order.BasicAction == "" && Formation(order.Card) && order.Step >= 2 ? 1 : 0];
    public static string ActionName(PendingOrder order) => Action(order) switch { "attack" => "Атака", "lightning" => "Lightning Move", _ => "Перемещение" };
    public static int Steps(PendingOrder order) => order.BasicAction != "" ? 1 : Formation(order.Card) ? 4 : 2;
    public static int Lightning(ShipDefinition ship) => ship.Afterburners >= 0 ? ship.Afterburners : ship.Id is "core:kushan-interceptor" or "core:taiidan-interceptor" ? 2 : 0;
    private static BattleState Sandbox(ContentCatalog c, BattleState state, PendingOrder order) => state with
    { Orders0 = FleetRuleTools.Enabled(c, state) ? state.Orders0 : 0, Orders1 = FleetRuleTools.Enabled(c, state) ? state.Orders1 : 0,
        Cards = FleetRuleTools.Enabled(c, state) ? state.Cards! with { PlayLimit0 = state.Orders0 + 1, PlayLimit1 = state.Orders1 + 1 } : state.Cards,
        ActiveSide = order.Side, Units = state.Units.Select(u => u with { Activated = false }).ToList() };
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
        if (FleetRuleTools.Enabled(c, state)) return FleetMovement.StraightPath(c, state, unit, goal) != null;
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
        if (Action(order) == "lightning" && (Lightning(c.Ships[unit.Definition]) == 0 || FleetRuleTools.Enabled(c, state) && FleetRuleTools.Has(unit, "engines"))) return UnitActions.Empty;
        var geometry = Geometry(c, state, order);
        var options = new SkirmishRules().GetActions(geometry, Sandbox(c, state, order), side, id);
        if (Action(order) == "attack") return new([], options.AttackTargets);
        if (order.BasicAction != "") return new(options.MoveCells, []);
        var moves = options.MoveCells.Where(h => !Formation(order.Card) ? Straight(c, state, unit, h, order) :
            Action(order) != "lightning" || state.Units.Any(first => first.Id == order.Units[0] && h.Distance(new(first.Q, first.R)) == 1)).ToArray();
        return new(moves, []);
    }
    public static CommandResult Start(ContentCatalog c, BattleState state, int side, BattleCommand command)
    {
        CommandResult Reject(string message) => new(false, message, state);
        var cards = state.Cards!;
        bool partialFormation = FleetRuleTools.Enabled(c, state) && Formation(command.Card);
        var unit = state.Units.Find(u => u.Id == (command.Unit == "" && partialFormation ? command.OtherUnit : command.Unit));
        if (!Supported(command.Card) || !(side == 0 ? cards.Hand0 : cards.Hand1).Contains(command.Card)) return Reject("Выберите доступную специальную карту.");
        if (state.ActiveSide != side || unit == null || unit.Side != side || unit.Activated || unit.Hulk || (side == 0 ? state.Orders0 : state.Orders1) >= FleetRuleTools.PlayLimit(c, state, side)) return Reject("Это подразделение не может получить приказ.");
        string[] units = Formation(command.Card) ? [command.Unit, command.OtherUnit] : [command.Unit];
        if (units.Distinct().Count() != units.Length || units.All(id => id == "") || units.Any(id => (id != "" || !partialFormation) && !state.Units.Any(u => u.Id == id && u.Side == side && !u.Activated && !u.Hulk))) return Reject("Назначьте разные готовые подразделения для используемых строк.");
        var order = new PendingOrder(command.Card, side, units, partialFormation && command.Unit == "" ? 2 : 0);
        cards = cards with { Hand0 = cards.Hand0.Where(id => id != command.Card).ToArray(), Hand1 = cards.Hand1.Where(id => id != command.Card).ToArray(), Discard = cards.Discard.Append(command.Card).ToArray(), Pending = order };
        var next = state with { Cards = cards,
            Units = state.Units.Select(u => units.Contains(u.Id) ? (FleetRuleTools.Enabled(c, state) ? FleetRuleTools.NewOrder(u) : u) with { Activated = true } : u).ToList(),
            Orders0 = state.Orders0 + (side == 0 ? 1 : 0), Orders1 = state.Orders1 + (side == 1 ? 1 : 0), Sequence = state.Sequence + 1 };
        string message = Name(command.Card) + ": приказ начат, подразделения назначены. ";
        if (FleetRuleTools.Enabled(c, state)) for (int i = 0; i < units.Length; i++)
        { if (units[i] == "") continue; next = FleetRuleTools.DamageControl(c, next, units[i], i == 0 ? command.Repair : command.OtherRepair, out var repair); message += repair; }
        return new(true, message, next);
    }
    public static CommandResult BeginBasic(ContentCatalog c, BattleState state, int side, BattleCommand command)
    {
        var unit = state.Units.Find(u => u.Id == command.Unit && u.Side == side && !u.Activated && !u.Hulk);
        var cards = state.Cards!;
        if (!FleetRuleTools.Enabled(c, state) || unit == null || state.ActiveSide != side || command.Target is not ("move" or "attack")
            || !(side == 0 ? cards.Hand0 : cards.Hand1).Contains(command.Card) || (side == 0 ? state.Orders0 : state.Orders1) >= FleetRuleTools.PlayLimit(c, state, side))
            return new(false, "Базовый приказ недоступен.", state);
        var next = state with { Units = state.Units.Select(u => u.Id == unit.Id ? FleetRuleTools.NewOrder(u) with { Activated = true } : u).ToList(),
            Orders0 = state.Orders0 + (side == 0 ? 1 : 0), Orders1 = state.Orders1 + (side == 1 ? 1 : 0), Sequence = state.Sequence + 1,
            Cards = cards with { Hand0 = cards.Hand0.Where(id => id != command.Card).ToArray(), Hand1 = cards.Hand1.Where(id => id != command.Card).ToArray(),
                Discard = cards.Discard.Append(command.Card).ToArray(), Pending = new(command.Card, side, [unit.Id], 0, BasicAction: command.Target) } };
        next = FleetRuleTools.DamageControl(c, next, unit.Id, command.Repair, out var message);
        return new(true, "Базовый приказ начат. " + message, next);
    }
    public static CommandResult Apply(ContentCatalog c, BattleState state, int side, BattleCommand command)
    {
        var order = state.Cards!.Pending!;
        CommandResult Reject(string message) => new(false, message, state);
        if (order.Side != side) return Reject("Дождитесь завершения специального приказа.");
        BattleState next = state; string message;
        if (command.Kind == "damage-control" && FleetRuleTools.Enabled(c, state) && Action(order) == "attack" && FleetRuleTools.Has(state.Units.Single(u => u.Id == Actor(order)), "weapons"))
        {
            if (command.Repair != "ask" && command.Repair != "focus" && !FleetRuleTools.Debuffs.Contains(command.Repair)) return Reject("Выберите эффект для Damage Control.");
            next = FleetRuleTools.DamageControl(c, state, Actor(order), command.Repair, out message, advance: true);
            if (next.Repairs is { Length: > 0 }) return new(true, message, next with { Sequence = state.Sequence + 1 });
            return Advance(c, state, next, side, message);
        }
        if (command.Kind is "skip" or "finish-order") message = command.Kind == "skip" ? "Действие пропущено." : "Оставшиеся действия пропущены.";
        else
        {
            var expected = Action(order) == "attack" ? "attack" : "move";
            var options = Actions(c, state, side, command.Unit);
            bool turn = expected == "move" && FleetRuleTools.Enabled(c, state) && !command.AutoFacing && state.Units.Any(u => u.Id == command.Unit && u.Q == command.Q && u.R == command.R);
            if (command.Kind != expected || command.Unit != Actor(order) || (expected == "move" ? !turn && !options.MoveCells.Contains(new(command.Q, command.R)) : !options.AttackTargets.Contains(command.Target)))
                return Reject("Действие не соответствует текущему шагу или цели приказа.");
            return InterruptRules.Start(c, state, side, command, basic: false);
        }
        return Advance(c, state, next, side, message, command.Kind == "finish-order");
    }
    internal static CommandResult Advance(ContentCatalog c, BattleState original, BattleState next, int side, string message, bool finish = false)
    {
        var order = next.Cards!.Pending!;
        order = order with { Step = finish ? Steps(order) : order.Step + 1 };
        while (order.Step < Steps(order) && !next.Units.Any(u => u.Id == Actor(order) && u.Patrol == "")) order = order with { Step = order.Step + 1 };
        if (next.Finished || order.Step >= Steps(order))
        {
            next = next with { Cards = next.Cards! with { Pending = null } };
            var result = SkirmishRules.FinishOrder(c, original, next, side, message + " Приказ завершён.");
            return result with { State = result.State.Round != original.Round && !result.State.Finished ? BasicCardRules.NextRound(c, result.State) : result.State };
        }
        return new(true, message, next with { Cards = next.Cards! with { Pending = order }, Sequence = original.Sequence + 1 });
    }
    public static void Validate(BattleState state)
    {
        var order = state.Cards!.Pending;
        if (order == null) return;
        if ((order.BasicAction == "" ? !Supported(order.Card) : order.BasicAction is not ("move" or "attack" or "transfer-crew")) || order.Side is < 0 or > 1 || state.ActiveSide != order.Side || state.Cards.Phase != "orders" || state.Finished
            || order.Units == null || order.Units.Length != (order.BasicAction == "" && Formation(order.Card) ? 2 : 1) || order.Units.Distinct().Count() != order.Units.Length
            || order.Units.Any(id => state.Units.Any(u => u.Id == id && (u.Side != order.Side || !u.Activated)))
            || order.Step < 0 || order.Step >= Steps(order) || order.Direction is < -1 or > 5 || !state.Cards.Discard.Contains(order.Card))
            throw new InvalidDataException("Invalid pending special order.");
        if (!state.Units.Any(u => u.Id == Actor(order))) throw new InvalidDataException("Pending actor is missing.");
    }
}
