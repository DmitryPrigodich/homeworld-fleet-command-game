namespace FleetCommand.Core;

// The active side keeps its turn while the other side answers this public pause.
public sealed record PendingResponse(int Side, BattleCommand Command, bool Basic, Hex[] RemainingPath, int[]? PathFacings = null, Hex? Transit = null, int TransitFacing = -1, MovementStep? MoveStart = null, int FrontSpent = 0, int RearSpent = 0);

public static class InterruptRules
{
    public static bool IsAttack(string card) => card is "order-17" or "order-18" or "order-19" or "order-20";
    public static int Responder(BattleState state) => 1 - state.Cards!.Response!.Side;
    public static string[] Played(CardRound cards, int side) => (side == 0 ? cards.Interrupts0 : cards.Interrupts1) ?? [];
    public static bool Available(CardRound cards, int side) => !Played(cards, side).Any(IsAttack);
    private static bool CanShoot(ContentCatalog c, BattleState state, BattleUnit unit, string target) => !unit.Hulk && unit.Patrol == "" &&
        (SkirmishGeometry.Attack(c, state, unit, state.Units.Find(u => u.Id == target)).Allowed || FleetRuleTools.Enabled(c, state)
            && (FleetClose.Allowed(c, state, unit, state.Units.Find(u => u.Id == target), "ram") || FleetClose.Allowed(c, state, unit, state.Units.Find(u => u.Id == target), "board")));
    public static UnitActions Actions(ContentCatalog c, BattleState state, int side, string id)
    {
        if (state.Finished || state.Cards?.Response is not { } pause || side != 1 - pause.Side
            || !Available(state.Cards, side) || !(side == 0 ? state.Cards.Hand0 : state.Cards.Hand1).Any(IsAttack)) return UnitActions.Empty;
        var unit = state.Units.Find(u => u.Id == id && u.Side == side);
        return unit != null && CanShoot(c, state, unit, pause.Command.Unit) ? new([], [pause.Command.Unit]) : UnitActions.Empty;
    }
    internal static CommandResult Start(ContentCatalog c, BattleState state, int side, BattleCommand command, bool basic)
    {
        if (command.Kind == "embark")
        {
            var parent = state.Units.Find(u => u.Id == command.Target);
            if (parent == null) return new(false, "Carrier unavailable.", state);
            command = command with { Kind = "move", Q = parent.Q, R = parent.R, Embark = parent.Id };
        }
        command = command with { Facing = (command.Facing % 6 + 6) % 6 };
        bool tabletop = FleetRuleTools.Enabled(c, state);
        var acting = state.Units.Find(u => u.Id == command.Unit);
        var pendingOrder = state.Cards!.Pending;
        if (tabletop && command.Power && (pendingOrder == null || SpecialOrders.Action(pendingOrder) != "power")) return new(false, "Power Attack requires the corresponding order.", state);
        var geometry = pendingOrder == null ? c : SpecialOrders.Geometry(c, state, pendingOrder);
        bool embarking = command.Embark != "";
        if (embarking && (command.Kind != "move" || !tabletop || acting == null || !state.Units.Any(p => p.Id == command.Embark && FleetLogistics.CanEmbark(c, state, acting, p)))) return new(false, "Cannot embark in this bay.", state);
        if (embarking && pendingOrder != null && SpecialOrders.Action(pendingOrder) == "lightning" && (FleetRuleTools.Has(acting!, "engines") || SpecialOrders.Lightning(c.Ships[acting!.Definition]) == 0
            || !state.Units.Any(first => first.Id == pendingOrder.Units[0] && new Hex(first.Q, first.R).Distance(new(command.Q, command.R)) == 1))) return new(false, "Lightning Move must end adjacent to the first unit.", state);
        MovementStep[]? steps = null;
        if (tabletop && command.Kind == "move" && acting != null)
        {
            bool straight = pendingOrder is { BasicAction: "" } && !SpecialOrders.Formation(pendingOrder.Card);
            steps = straight ? FleetMovement.StraightPath(geometry, state, acting, new(command.Q, command.R), command.Embark)
                : FleetMovement.Path(geometry, state, acting, new(command.Q, command.R), command.AutoFacing ? -1 : command.Facing, command.Embark);
        }
        var options = new BasicCardRules().GetActions(c, state, side, command.Unit);
        bool turning = tabletop && command.Kind == "move" && acting != null && command.Q == acting.Q && command.R == acting.R && !command.AutoFacing && steps != null && (basic || pendingOrder!.BasicAction != "" || SpecialOrders.Formation(pendingOrder.Card));
        if (turning && pendingOrder != null && SpecialOrders.Action(pendingOrder) == "lightning")
            turning = !FleetRuleTools.Has(acting!, "engines") && SpecialOrders.Lightning(c.Ships[acting!.Definition]) > 0
                && state.Units.Any(u => u.Id == pendingOrder.Units[0] && new Hex(u.Q, u.R).Distance(new(command.Q, command.R)) == 1);
        if (command.Kind == "move" ? (!options.MoveCells.Contains(new(command.Q, command.R)) && !turning && !embarking) || tabletop && steps == null : !options.AttackTargets.Contains(command.Target)
            || tabletop && !FleetCombat.Allowed(c, state, acting!, state.Units.Find(u => u.Id == command.Target), command))
            return new(false, "The action does not match the order's current step or target.", state);
        if ((turning || embarking) && (acting!.Hulk || acting.Side != side || state.ActiveSide != side || basic && (acting.Activated || (side == 0 ? state.Orders0 : state.Orders1) >= FleetRuleTools.PlayLimit(c, state, side))))
            return new(false, "The unit cannot receive a turn-in-place order.", state);
        var unit = state.Units.Single(u => u.Id == command.Unit);
        var next = state;
        if (basic)
        {
            var cards = state.Cards!;
            next = state with
            {
                Cards = cards with { Hand0 = cards.Hand0.Where(id => id != command.Card).ToArray(), Hand1 = cards.Hand1.Where(id => id != command.Card).ToArray(), Discard = cards.Discard.Append(command.Card).ToArray() },
                Orders0 = state.Orders0 + (side == 0 ? 1 : 0), Orders1 = state.Orders1 + (side == 1 ? 1 : 0),
                Units = state.Units.Select(u => u.Id == unit.Id ? (FleetRuleTools.Enabled(c, state) ? FleetRuleTools.NewOrder(u) : u) with { Activated = true } : u).ToList()
            };
        }
        Hex[] path = [];
        if (command.Kind == "move")
        {
            var pending = state.Cards!.Pending;
            if (!basic && pending is { BasicAction: "" } && !SpecialOrders.Formation(pending.Card))
            {
                int direction = SpecialOrders.StraightDirection(new(unit.Q, unit.R), new(command.Q, command.R));
                path = Enumerable.Range(1, new Hex(unit.Q, unit.R).Distance(new(command.Q, command.R)))
                    .Select(n => new Hex(unit.Q + Hex.Directions[direction].Q * n, unit.R + Hex.Directions[direction].R * n)).ToArray();
                if (pending.Direction < 0) next = next with { Cards = next.Cards! with { Pending = pending with { Direction = direction } } };
            }
            else path = SkirmishGeometry.MovementPath(pending == null ? c : SpecialOrders.Geometry(c, state, pending), state, unit, new(command.Q, command.R));
        }
        if (tabletop && command.Kind == "move") path = steps!.Select(p => p.Cell).ToArray();
        var response = new PendingResponse(side, command, basic, path, steps?.Select(p => p.Facing).ToArray(), MoveStart: c.Ships[unit.Definition].Footprint == 2 && command.Kind == "move" ? new(new(unit.Q, unit.R), unit.Facing) : null);
        next = next with { Cards = next.Cards! with { Response = response } };
        if (basic && tabletop)
        {
            var activation = FleetTerrain.Activation(c, state, next, [unit.Id], "response-start", "");
            if (activation.State.Hazard != null || activation.State.Combat != null || activation.State.Defense != null || activation.State.Cards?.Response == null) return activation;
            // An automatically resolved terrain attack already resumed the action.
            if (activation.State.Sequence != next.Sequence) return activation;
        }
        return Continue(c, state, next, "", offerAttack: true);
    }
    // Resolve only the effect. Preserve the real turn, activation flags, Play and round.
    private static CommandResult Attack(ContentCatalog c, BattleState state, int side, BattleCommand command, bool interrupt = false)
    {
        if (FleetRuleTools.Enabled(c, state)) return FleetCombat.Begin(c, state, side, command, interrupt);
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
        message + " Interrupt Attack window: the opponent may respond or decline.", next with { Sequence = original.Sequence + 1 });
    private static CommandResult Continue(ContentCatalog c, BattleState original, BattleState next, string message, bool offerAttack = false)
    {
        var pause = next.Cards!.Response!;
        var unit = next.Units.Find(u => u.Id == pause.Command.Unit);
        if (unit == null || next.Finished) return Complete(c, original, next, message + " The destroyed unit's remaining actions were cancelled.");
        if (pause.Command.Kind == "attack")
        {
            if (offerAttack && Offer(c, next, pause)) return Paused(original, next, "Attack declared.");
            var result = Attack(c, next, pause.Side, pause.Command);
            if (result.State.Combat != null || result.State.Defense != null || result.State.Overrun != null) return result with { Message = message + " " + result.Message, State = result.State with { Sequence = original.Sequence + 1 } };
            return Complete(c, original, result.State, message + " " + result.Message);
        }
        if (pause.Command.Embark != "" && !next.Units.Any(p => p.Id == pause.Command.Embark && FleetLogistics.CanEmbark(c, next, unit, p)))
            return Complete(c, original, next, message + " Embarkation cancelled: carrier unavailable.");
        var remaining = pause.RemainingPath.ToList();
        var facings = pause.PathFacings?.ToList();
        while (remaining.Count > 0)
        {
            var cell = remaining[0]; remaining.RemoveAt(0);
            int facing = facings?.FirstOrDefault() ?? (pause.Command.Facing % 6 + 6) % 6; if (facings != null) facings.RemoveAt(0);
            if (pause.Command.Embark != "" && remaining.Count == 0)
                return Complete(c, original, FleetLogistics.Embark(next, unit with { Facing = facing }, pause.Command.Embark), message + " Unit embarked; 1 Move spent entering the bay.");
            // A pause cannot leave two units sharing a cell: pass through allies first.
            var priorStep = new MovementStep(pause.Transit ?? new Hex(unit.Q, unit.R), pause.TransitFacing < 0 ? unit.Facing : pause.TransitFacing);
            var currentStep = new MovementStep(cell, facing);
            var oldCells = FleetFootprint.Cells(c.Ships[unit.Definition], priorStep.Cell, priorStep.Facing);
            var newCells = FleetFootprint.Cells(c.Ships[unit.Definition], cell, facing);
            var entered = newCells.Except(oldCells).ToArray();
            if (pause.MoveStart != null)
            {
                var cost = FleetLargeMovement.Cost(c, next, unit, priorStep, currentStep);
                pause = pause with { FrontSpent = pause.FrontSpent + cost.Front, RearSpent = pause.RearSpent + cost.Rear };
            }
            bool occupied = newCells.Any(h => next.Units.Any(u => u.Patrol == "" && u.Id != unit.Id && FleetFootprint.Contains(c, u, h)));
            if (occupied)
            {
                pause = pause with { RemainingPath = remaining.ToArray(), PathFacings = facings?.ToArray(), Transit = cell, TransitFacing = facing };
                next = next with { Cards = next.Cards! with { Response = pause } };
                var transit = FleetTerrain.Entry(c, original, next, unit.Id, cell, message, entered);
                if (transit != null) return transit;
                continue;
            }
            var previous = pause.Transit ?? new Hex(unit.Q, unit.R);
            unit = unit with { Q = cell.Q, R = cell.R, Facing = facing, LastAction = "move", LastTarget = "", LastActionRound = next.Round };
            pause = pause with { RemainingPath = remaining.ToArray(), PathFacings = facings?.ToArray(), Transit = null, TransitFacing = -1 };
            next = next with { Units = next.Units.Select(u => u.Id == unit.Id ? unit : u).ToList(), Cards = next.Cards! with { Response = pause } };
            if (entered.Length > 0)
            {
                var terrain = FleetTerrain.Entry(c, original, next, unit.Id, cell, message, entered);
                if (terrain != null) return terrain;
            }
            if (Offer(c, next, pause)) return Paused(original, next, message + $" {FleetPresentation.UnitName(c, next, unit)}: moving to hex ({cell.Q + 1}, {cell.R + 1}).");
        }
        var surveyed = FleetEconomy.Survey(c, next, unit);
        if (!ReferenceEquals(surveyed, next)) message += " Adjacent asteroids surveyed; their resources are visible only to your fleet.";
        next = surveyed;
        string completed = message + $" {FleetPresentation.UnitName(c, next, unit)}: movement complete ({unit.Q + 1}, {unit.R + 1}).";
        if (FleetRuleTools.Enabled(c, next) && FleetCap.Hosts(c, next, unit).Length > 0)
            return new(true, completed + " Fighters may be assigned to CAP.", next with { CapEntry = new(pause.Side, unit.Id), Sequence = original.Sequence + 1 });
        return Complete(c, original, next, completed);
    }
    internal static CommandResult Complete(ContentCatalog c, BattleState original, BattleState next, string message)
    {
        var pause = next.Cards!.Response!;
        next = next with { Cards = next.Cards with { Response = null } };
        if (!pause.Basic) return SpecialOrders.Advance(c, original, next, pause.Side, message);
        var result = SkirmishRules.FinishOrder(c, original, next, pause.Side, message);
        return result with { State = result.State.Round != original.Round && !result.State.Finished ? BasicCardRules.NextRound(c, result.State) : result.State };
    }
    internal static CommandResult ResumeTerrain(ContentCatalog c, BattleState original, BattleState next, string message, bool start)
    {
        var pause = next.Cards!.Response!;
        if (!start && pause.Transit == null && !next.Finished && next.Units.Any(u => u.Id == pause.Command.Unit) && Offer(c, next, pause)) return Paused(original, next, message);
        return Continue(c, original, next, message, offerAttack: start);
    }
    public static CommandResult Apply(ContentCatalog c, BattleState state, int side, BattleCommand command)
    {
        CommandResult Reject(string text) => new(false, text, state);
        var cards = state.Cards!; var pause = cards.Response!;
        if (side != 1 - pause.Side) return Reject("Waiting for the opponent's interrupt response.");
        if (command.Kind == "decline") return Continue(c, state, state, "Interrupt opportunity declined.");
        if (FleetRuleTools.Enabled(c, state) && command.Power) return Reject("Interrupt Attack does not grant Power Attack.");
        if (command.Kind != "interrupt" || !IsAttack(command.Card) || !(side == 0 ? cards.Hand0 : cards.Hand1).Contains(command.Card)
            || !Actions(c, state, side, command.Unit).AttackTargets.Contains(command.Target)) return Reject("Requires Interrupt Attack and one of your units able to attack the acting enemy.");
        cards = cards with { Hand0 = cards.Hand0.Where(id => id != command.Card).ToArray(), Hand1 = cards.Hand1.Where(id => id != command.Card).ToArray(),
            Interrupts0 = side == 0 ? Played(cards, side).Append(command.Card).ToArray() : cards.Interrupts0,
            Interrupts1 = side == 1 ? Played(cards, side).Append(command.Card).ToArray() : cards.Interrupts1 };
        var result = Attack(c, state with { Cards = cards }, side, command with { Kind = "attack" }, interrupt: true);
        if (!result.Accepted) return Reject(result.Message);
        if (result.State.Combat != null || result.State.Defense != null || result.State.Overrun != null) return result with { State = result.State with { Sequence = state.Sequence + 1 } };
        return Continue(c, state, result.State, "Interrupt Attack: " + result.Message);
    }
    internal static CommandResult CombatResolved(ContentCatalog c, BattleState original, BattleState next, string message, bool interrupt) =>
        interrupt ? Continue(c, original, next, "Interrupt Attack: " + message) : Complete(c, original, next, message);
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
        var unit = state.Units.Single(u => u.Id == pause.Command.Unit); var previous = pause.Transit ?? new Hex(unit.Q, unit.R);
        if ((pause.Basic && (pause.Side == 0 ? state.Orders0 : state.Orders1) < 1)
            || (!pause.Basic && (SpecialOrders.Action(cards.Pending!) == "attack" ? pause.Command.Kind != "attack" : pause.Command.Kind != "move"))
            || pause.Command.Kind == "move" && (pause.RemainingPath.LastOrDefault(new Hex(unit.Q, unit.R)) != new Hex(pause.Command.Q, pause.Command.R)))
            throw new InvalidDataException("Invalid suspended action.");
        foreach (var cell in pause.RemainingPath)
        {
            if ((previous.Distance(cell) != 1 && (pause.PathFacings == null || previous != cell)) || cell.Q < 0 || cell.R < 0) throw new InvalidDataException("Invalid pending movement path.");
            previous = cell;
        }
        if (pause.Command.Kind == "attack" && pause.RemainingPath.Length != 0) throw new InvalidDataException("Attack has a movement path.");
        if (pause.PathFacings != null && (pause.PathFacings.Length != pause.RemainingPath.Length || pause.PathFacings.Any(f => f is < 0 or > 5))) throw new InvalidDataException("Invalid movement facing path.");
    }
    internal static void ValidateGeometry(ContentCatalog c, BattleState state)
    {
        if (state.Cards?.Response is not { } pause) return;
        var map = c.Maps[state.Map]; var rules = c.Rules[map.Rules];
        if (pause.Command.Embark != "" && (pause.Command.Kind != "move" || !state.Units.Any(p => p.Id == pause.Command.Embark && FleetFootprint.Contains(c, p, new(pause.Command.Q, pause.Command.R)) && FleetLogistics.CanEmbark(c, state, state.Units.Single(u => u.Id == pause.Command.Unit), p)))) throw new InvalidDataException("Invalid embark route.");
        if (pause.Transit is { } transit && (pause.Command.Kind != "move" || state.Hazard == null || state.Hazard.Job.Resume != "movement"
            || !FleetFootprint.CanPlace(c, state, state.Units.Single(u => u.Id == pause.Command.Unit), transit, pause.TransitFacing < 0 ? state.Units.Single(u => u.Id == pause.Command.Unit).Facing : pause.TransitFacing, transit: true)
            || !state.Units.Any(u => u.Id != pause.Command.Unit && u.Patrol == "" && FleetFootprint.Cells(c.Ships[state.Units.Single(a => a.Id == pause.Command.Unit).Definition], transit, pause.TransitFacing < 0 ? state.Units.Single(a => a.Id == pause.Command.Unit).Facing : pause.TransitFacing).Any(h => FleetFootprint.Contains(c, u, h)) && u.Side == pause.Side && !u.Hulk) || !rules.FriendlyPassThrough))
            throw new InvalidDataException("Invalid occupied transit cell.");
        var actor = state.Units.Single(u => u.Id == pause.Command.Unit);
        if (pause.MoveStart != null)
        {
            int limit = FleetLargeMovement.Limit(c, actor); var previous = new MovementStep(pause.Transit ?? new(actor.Q, actor.R), pause.TransitFacing < 0 ? actor.Facing : pause.TransitFacing);
            int front = pause.FrontSpent, rear = pause.RearSpent;
            if (c.Ships[actor.Definition].Footprint != 2 || front < 0 || rear < 0 || front > limit || rear > limit || pause.PathFacings == null) throw new InvalidDataException("Invalid two-hex movement budget.");
            for (int i = 0; i < pause.RemainingPath.Length; i++)
            {
                var step = new MovementStep(pause.RemainingPath[i], pause.PathFacings[i]); var cost = FleetLargeMovement.Cost(c, state, actor, previous, step); front += cost.Front; rear += cost.Rear; previous = step;
                if (front > limit || rear > limit) throw new InvalidDataException("A ship end exceeds its movement allowance.");
            }
        }
        if (pause.RemainingPath.Where((h, i) => !FleetFootprint.CanPlace(c, state, actor, h, pause.PathFacings?[i] ?? actor.Facing, transit: true, docking: pause.Command.Embark)).Any()
            || pause.Command.Kind == "attack" && state.Combat?.Kind != "collision" && state.Overrun == null && !(FleetRuleTools.Enabled(c, state)
                ? FleetCombat.Allowed(c, state, state.Units.Single(u => u.Id == pause.Command.Unit), state.Units.Find(u => u.Id == pause.Command.Target), pause.Command)
                : SkirmishGeometry.Attack(c, state, state.Units.Single(u => u.Id == pause.Command.Unit), state.Units.Find(u => u.Id == pause.Command.Target)).Allowed))
            throw new InvalidDataException("Invalid suspended action geometry.");
    }
}
