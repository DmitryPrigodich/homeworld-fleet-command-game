namespace FleetCommand.Core;

// The active side keeps its turn while the other side answers this public pause.
public sealed record PendingResponse(int Side, BattleCommand Command, bool Basic, Hex[] RemainingPath);

public static class InterruptRules
{
    public static bool IsAttack(string card) => card is "order-17" or "order-18" or "order-19" or "order-20";
    public static int Responder(BattleState state) => 1 - state.Cards!.Response!.Side;
    public static string[] Played(CardRound cards, int side) => (side == 0 ? cards.Interrupts0 : cards.Interrupts1) ?? [];
    public static bool Available(CardRound cards, int side) => !Played(cards, side).Any(IsAttack);
    private static bool CanShoot(ContentCatalog c, BattleState state, BattleUnit unit, string target) =>
        SkirmishGeometry.Attack(c, state, unit, state.Units.Find(u => u.Id == target)).Allowed;
    public static UnitActions Actions(ContentCatalog c, BattleState state, int side, string id)
    {
        if (state.Finished || state.Cards?.Response is not { } pause || side != 1 - pause.Side
            || !Available(state.Cards, side) || !(side == 0 ? state.Cards.Hand0 : state.Cards.Hand1).Any(IsAttack)) return UnitActions.Empty;
        var unit = state.Units.Find(u => u.Id == id && u.Side == side);
        return unit != null && CanShoot(c, state, unit, pause.Command.Unit) ? new([], [pause.Command.Unit]) : UnitActions.Empty;
    }
    internal static CommandResult Start(ContentCatalog c, BattleState state, int side, BattleCommand command, bool basic)
    {
        command = command with { Facing = (command.Facing % 6 + 6) % 6 };
        var options = new BasicCardRules().GetActions(c, state, side, command.Unit);
        if (command.Kind == "move" ? !options.MoveCells.Contains(new(command.Q, command.R)) : !options.AttackTargets.Contains(command.Target))
            return new(false, "Действие не соответствует текущему шагу или цели приказа.", state);
        var unit = state.Units.Single(u => u.Id == command.Unit);
        var next = state;
        if (basic)
        {
            var cards = state.Cards!;
            next = state with
            {
                Cards = cards with { Hand0 = cards.Hand0.Where(id => id != command.Card).ToArray(), Hand1 = cards.Hand1.Where(id => id != command.Card).ToArray(), Discard = cards.Discard.Append(command.Card).ToArray() },
                Orders0 = state.Orders0 + (side == 0 ? 1 : 0), Orders1 = state.Orders1 + (side == 1 ? 1 : 0),
                Units = state.Units.Select(u => u.Id == unit.Id ? u with { Activated = true } : u).ToList()
            };
        }
        Hex[] path = [];
        if (command.Kind == "move")
        {
            var pending = state.Cards!.Pending;
            if (!basic && pending != null && !SpecialOrders.Formation(pending.Card))
            {
                int direction = SpecialOrders.StraightDirection(new(unit.Q, unit.R), new(command.Q, command.R));
                path = Enumerable.Range(1, new Hex(unit.Q, unit.R).Distance(new(command.Q, command.R)))
                    .Select(n => new Hex(unit.Q + Hex.Directions[direction].Q * n, unit.R + Hex.Directions[direction].R * n)).ToArray();
                if (pending.Direction < 0) next = next with { Cards = next.Cards! with { Pending = pending with { Direction = direction } } };
            }
            else path = SkirmishGeometry.MovementPath(pending == null ? c : SpecialOrders.Geometry(c, state, pending), state, unit, new(command.Q, command.R));
        }
        var response = new PendingResponse(side, command, basic, path);
        next = next with { Cards = next.Cards! with { Response = response } };
        return Continue(c, state, next, "", offerAttack: true);
    }
    // Resolve only the effect. Preserve the real turn, activation flags, Play and round.
    private static CommandResult Attack(ContentCatalog c, BattleState state, int side, BattleCommand command)
    {
        var sandbox = state with { ActiveSide = side, Orders0 = 0, Orders1 = 0, Units = state.Units.Select(u => u with { Activated = false }).ToList() };
        var result = new SkirmishRules().Apply(c, sandbox, side, command);
        if (!result.Accepted) return result with { State = state };
        var units = result.State.Units.Select(u => u with { Activated = state.Units.Single(old => old.Id == u.Id).Activated }).ToList();
        bool finished = !units.Any(u => u.Side == 0) || !units.Any(u => u.Side == 1);
        return result with { State = state with { Units = units, RandomState = result.State.RandomState, Finished = finished,
            Winner = finished ? units.Any(u => u.Side == 0) ? 0 : 1 : null } };
    }
    private static bool Offer(ContentCatalog c, BattleState state, PendingResponse pause) =>
        Available(state.Cards!, 1 - pause.Side) && state.Units.Any(u => u.Side == 1 - pause.Side && CanShoot(c, state, u, pause.Command.Unit));
    private static CommandResult Paused(BattleState original, BattleState next, string message) => new(true,
        message + " Окно Interrupt Attack: противник может ответить или пропустить.", next with { Sequence = original.Sequence + 1 });
    private static CommandResult Continue(ContentCatalog c, BattleState original, BattleState next, string message, bool offerAttack = false)
    {
        var pause = next.Cards!.Response!;
        var unit = next.Units.Find(u => u.Id == pause.Command.Unit);
        if (unit == null || next.Finished) return Complete(c, original, next, message + " Действия уничтоженного отряда отменены.");
        if (pause.Command.Kind == "attack")
        {
            if (offerAttack && Offer(c, next, pause)) return Paused(original, next, "Атака объявлена.");
            var result = Attack(c, next, pause.Side, pause.Command);
            return Complete(c, original, result.State, message + " " + result.Message);
        }
        var remaining = pause.RemainingPath.ToList();
        while (remaining.Count > 0)
        {
            var cell = remaining[0]; remaining.RemoveAt(0);
            // A pause cannot leave two units sharing a cell: pass through allies first.
            if (next.Units.Any(u => u.Id != unit.Id && u.Q == cell.Q && u.R == cell.R)) continue;
            unit = unit with { Q = cell.Q, R = cell.R, Facing = (pause.Command.Facing % 6 + 6) % 6 };
            pause = pause with { RemainingPath = remaining.ToArray() };
            next = next with { Units = next.Units.Select(u => u.Id == unit.Id ? unit : u).ToList(), Cards = next.Cards! with { Response = pause } };
            if (Offer(c, next, pause)) return Paused(original, next, message + $" {FleetPresentation.UnitName(c, next, unit)}: перемещение в клетку ({cell.Q + 1}, {cell.R + 1}).");
        }
        return Complete(c, original, next, message + $" {FleetPresentation.UnitName(c, next, unit)}: перемещение завершено ({unit.Q + 1}, {unit.R + 1}).");
    }
    private static CommandResult Complete(ContentCatalog c, BattleState original, BattleState next, string message)
    {
        var pause = next.Cards!.Response!;
        next = next with { Cards = next.Cards with { Response = null } };
        if (!pause.Basic) return SpecialOrders.Advance(c, original, next, pause.Side, message);
        var result = SkirmishRules.FinishOrder(c, original, next, pause.Side, message);
        return result with { State = result.State.Round != original.Round && !result.State.Finished ? BasicCardRules.Draw(result.State) : result.State };
    }
    public static CommandResult Apply(ContentCatalog c, BattleState state, int side, BattleCommand command)
    {
        CommandResult Reject(string text) => new(false, text, state);
        var cards = state.Cards!; var pause = cards.Response!;
        if (side != 1 - pause.Side) return Reject("Ожидается ответ противника на прерывание.");
        if (command.Kind == "decline") return Continue(c, state, state, "Возможность прерывания пропущена.");
        if (command.Kind != "interrupt" || !IsAttack(command.Card) || !(side == 0 ? cards.Hand0 : cards.Hand1).Contains(command.Card)
            || !Actions(c, state, side, command.Unit).AttackTargets.Contains(command.Target)) return Reject("Нужна карта Interrupt Attack и свой отряд, способный атаковать действующего противника.");
        cards = cards with { Hand0 = cards.Hand0.Where(id => id != command.Card).ToArray(), Hand1 = cards.Hand1.Where(id => id != command.Card).ToArray(),
            Interrupts0 = side == 0 ? Played(cards, side).Append(command.Card).ToArray() : cards.Interrupts0,
            Interrupts1 = side == 1 ? Played(cards, side).Append(command.Card).ToArray() : cards.Interrupts1 };
        var result = Attack(c, state with { Cards = cards }, side, command with { Kind = "attack" });
        if (!result.Accepted) return Reject(result.Message);
        return Continue(c, state, result.State, "Interrupt Attack: " + result.Message);
    }
    public static void Validate(BattleState state)
    {
        var cards = state.Cards!;
        foreach (int side in new[] { 0, 1 })
            if (Played(cards, side).Length > 1 || Played(cards, side).Any(id => !IsAttack(id))) throw new InvalidDataException("Invalid played interrupts.");
        if (cards.Response is not { } pause) return;
        if (state.Finished || cards.Phase != "orders" || pause.Side != state.ActiveSide || pause.Command == null || pause.Command.Kind is not ("move" or "attack")
            || pause.RemainingPath == null || pause.RemainingPath.Length > 2500 || pause.Command.Facing is < 0 or > 5
            || !state.Units.Any(u => u.Id == pause.Command.Unit && u.Side == pause.Side && u.Activated)
            || (pause.Basic ? cards.Pending != null || !cards.Discard.Contains(pause.Command.Card) : cards.Pending == null || SpecialOrders.Actor(cards.Pending) != pause.Command.Unit))
            throw new InvalidDataException("Invalid response window.");
        var unit = state.Units.Single(u => u.Id == pause.Command.Unit); var previous = new Hex(unit.Q, unit.R);
        if ((pause.Basic && (pause.Side == 0 ? state.Orders0 : state.Orders1) < 1)
            || (!pause.Basic && (SpecialOrders.Action(cards.Pending!) == "attack" ? pause.Command.Kind != "attack" : pause.Command.Kind != "move"))
            || pause.Command.Kind == "move" && (pause.RemainingPath.LastOrDefault(new Hex(unit.Q, unit.R)) != new Hex(pause.Command.Q, pause.Command.R)))
            throw new InvalidDataException("Invalid suspended action.");
        foreach (var cell in pause.RemainingPath)
        {
            if (previous.Distance(cell) != 1 || cell.Q < 0 || cell.R < 0) throw new InvalidDataException("Invalid pending movement path.");
            previous = cell;
        }
        if (pause.Command.Kind == "attack" && pause.RemainingPath.Length != 0) throw new InvalidDataException("Attack has a movement path.");
    }
    internal static void ValidateGeometry(ContentCatalog c, BattleState state)
    {
        if (state.Cards?.Response is not { } pause) return;
        var map = c.Maps[state.Map]; var rules = c.Rules[map.Rules];
        if (pause.RemainingPath.Any(h => h.Q >= map.Width || h.R >= map.Height || SkirmishGeometry.Blocked(c, map, h)
            || state.Units.Any(u => u.Q == h.Q && u.R == h.R && (u.Side != pause.Side || !rules.FriendlyPassThrough)))
            || pause.Command.Kind == "attack" && !SkirmishGeometry.Attack(c, state, state.Units.Single(u => u.Id == pause.Command.Unit), state.Units.Find(u => u.Id == pause.Command.Target)).Allowed)
            throw new InvalidDataException("Invalid suspended action geometry.");
    }
}
