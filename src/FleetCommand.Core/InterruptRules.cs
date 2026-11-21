namespace FleetCommand.Core;

// The active side keeps its turn while the other side answers this public pause.
public sealed record PendingResponse(int Side, BattleCommand Command, bool Basic, Hex[] RemainingPath, int[]? PathFacings = null, Hex? Transit = null, int TransitFacing = -1, MovementStep? MoveStart = null, int FrontSpent = 0, int RearSpent = 0,
    bool ReactionMove = false, bool Replan = false, int MoveSpent = 0, int MoveBudget = -1, bool ReplanAfterResponse = false);

public static partial class InterruptRules
{
    public static bool IsAttack(ContentCatalog c, BattleState? state, string card) => CommandCards.Find(c, state, card) is { Kind: "interrupt", Interrupt: "attack" };
    public static string Trigger(ContentCatalog c, BattleState state, PendingResponse pause) => pause.Command.Kind != "move" ? pause.Command.Kind
        : state.Cards?.Pending is { } order && SpecialOrders.Action(c, state, order) == "lightning" ? "lightning" : "move";
    public static bool Eligible(ContentCatalog c, BattleState state, string card, PendingResponse pause) => CommandCards.Find(c, state, card) is { Kind: "interrupt" } definition
        && (definition.InterruptTriggers ?? ["move", "lightning", "attack"]).Contains(Trigger(c, state, pause));
    public static int Responder(BattleState state) => state.Cards!.Response is { Replan: true } pause ? pause.Side : 1 - state.Cards!.Response!.Side;
    public static string[] Played(CardRound cards, int side) => (side == 0 ? cards.Interrupts0 : cards.Interrupts1) ?? [];
    public static bool Available(ContentCatalog c, BattleState state, int side, string kind = "attack") => !Used(state.Cards!, side).Contains(kind)
        && !Played(state.Cards!, side).Any(id => CommandCards.Find(c, state, id)?.Interrupt == kind);
    private static bool CanShoot(ContentCatalog c, BattleState state, BattleUnit unit, string target) => !unit.Hulk && unit.Patrol == "" &&
        (SkirmishGeometry.Attack(c, state, unit, state.Units.Find(u => u.Id == target)).Allowed || FleetRuleTools.Enabled(c, state)
            && (FleetClose.Allowed(c, state, unit, state.Units.Find(u => u.Id == target), "ram") || FleetClose.Allowed(c, state, unit, state.Units.Find(u => u.Id == target), "board")));
    public static UnitActions Actions(ContentCatalog c, BattleState state, int side, string id)
    {
        if (state.Cards?.Counter == null && state.Cards?.Response is { Replan: true } replanning)
            return side == replanning.Side && id == replanning.Command.Unit ? new(ReplanCells(c, state, replanning), []) : UnitActions.Empty;
        var moveCells = MoveCells(c, state, side, id);
        if (state.Finished || state.Cards?.Counter != null || state.Cards?.Response is not { } pause || side != 1 - pause.Side
            || !Available(c, state, side) || !(side == 0 ? state.Cards.Hand0 : state.Cards.Hand1).Any(id => IsAttack(c, state, id) && Eligible(c, state, id, pause))) return new(moveCells, []);
        var unit = state.Units.Find(u => u.Id == id && u.Side == side);
        return unit != null && CanShoot(c, state, unit, pause.Command.Unit) ? new(moveCells, [pause.Command.Unit]) : new(moveCells, []);
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
        if (tabletop && command.Power && (pendingOrder == null || SpecialOrders.Action(c, state, pendingOrder) != "power")) return new(false, "Power Attack requires the corresponding order.", state);
        var geometry = pendingOrder == null ? c : SpecialOrders.Geometry(c, state, pendingOrder);
        bool embarking = command.Embark != "";
        if (embarking && (command.Kind != "move" || !tabletop || acting == null || !state.Units.Any(p => p.Id == command.Embark && FleetLogistics.CanEmbark(c, state, acting, p)))) return new(false, "Cannot embark in this bay.", state);
        if (embarking && pendingOrder != null && SpecialOrders.Action(c, state, pendingOrder) == "lightning" && (FleetRuleTools.Has(acting!, "engines") || SpecialOrders.Lightning(c, acting!) == 0
            || SpecialOrders.AdjacentOnLightning(c, state, pendingOrder) && !state.Units.Any(first => first.Id == pendingOrder.Units[0] && new Hex(first.Q, first.R).Distance(new(command.Q, command.R)) == 1))) return new(false, "Lightning Move must end adjacent to the first unit.", state);
        MovementStep[]? steps = null;
        if (tabletop && command.Kind == "move" && acting != null)
        {
            bool straight = pendingOrder != null && SpecialOrders.StraightCourse(c, state, pendingOrder);
            steps = straight ? FleetMovement.StraightPath(geometry, state, acting, new(command.Q, command.R), command.Embark)
                : FleetMovement.Path(geometry, state, acting, new(command.Q, command.R), command.AutoFacing ? -1 : command.Facing, command.Embark);
        }
        var options = new BasicCardRules().GetActions(c, state, side, command.Unit);
        bool turning = tabletop && command.Kind == "move" && acting != null && command.Q == acting.Q && command.R == acting.R && !command.AutoFacing && steps != null && (basic || pendingOrder!.BasicAction != "" || !SpecialOrders.StraightCourse(c, state, pendingOrder));
        if (turning && pendingOrder != null && SpecialOrders.Action(c, state, pendingOrder) == "lightning")
            turning = !FleetRuleTools.Has(acting!, "engines") && SpecialOrders.Lightning(c, acting!) > 0
                && (!SpecialOrders.AdjacentOnLightning(c, state, pendingOrder) || state.Units.Any(u => u.Id == pendingOrder.Units[0] && new Hex(u.Q, u.R).Distance(new(command.Q, command.R)) == 1));
        if (command.Kind == "move" ? (!options.MoveCells.Contains(new(command.Q, command.R)) && !turning && !embarking) || tabletop && steps == null : !options.AttackTargets.Contains(command.Target)
            || tabletop && !FleetCombat.Allowed(c, state, acting!, state.Units.Find(u => u.Id == command.Target), command))
            return new(false, "The action does not match the order's current step or target.", state);
        if ((turning || embarking) && (acting!.Hulk || acting.Side != side || state.ActiveSide != side || basic && (acting.Activated || (side == 0 ? state.Orders0 : state.Orders1) >= FleetRuleTools.PlayLimit(c, state, side))))
            return new(false, "The unit cannot receive a turn-in-place order.", state);
        var unit = state.Units.Single(u => u.Id == command.Unit);
        if (basic && FleetPlanets.IsPlanet(c, unit) && (command.Kind != "attack" || !FleetPlanets.CanActivate(c, state, unit, command.Weapon))) return new(false, "Select a ready planet weapon facility for this Attack order.", state);
        var next = state;
        if (basic)
        {
            var cards = state.Cards!;
            next = state with
            {
                Cards = HeroicCommands.Spend(cards, command.Card),
                Orders0 = state.Orders0 + (side == 0 ? 1 : 0), Orders1 = state.Orders1 + (side == 1 ? 1 : 0),
                Units = state.Units.Select(u => u.Id == unit.Id ? (FleetRuleTools.Enabled(c, state) ? FleetRuleTools.NewOrder(u) : u) with { Activated = true } : u).ToList()
            };
        }
        if (basic) next = FleetPlanets.Activate(c, next, unit.Id, command.Weapon);
        Hex[] path = [];
        if (command.Kind == "move")
        {
            var pending = state.Cards!.Pending;
            if (!basic && pending != null && SpecialOrders.StraightCourse(c, state, pending))
            {
                int direction = SpecialOrders.StraightDirection(new(unit.Q, unit.R), new(command.Q, command.R));
                path = Enumerable.Range(1, new Hex(unit.Q, unit.R).Distance(new(command.Q, command.R)))
                    .Select(n => new Hex(unit.Q + Hex.Directions[direction].Q * n, unit.R + Hex.Directions[direction].R * n)).ToArray();
                if (pending.Direction < 0) next = next with { Cards = next.Cards! with { Pending = pending with { Direction = direction } } };
            }
            else path = SkirmishGeometry.MovementPath(pending == null ? c : SpecialOrders.Geometry(c, state, pending), state, unit, new(command.Q, command.R));
        }
        if (tabletop && command.Kind == "move") path = steps!.Select(p => p.Cell).ToArray();
        if (command.Kind == "move" && HeroicInterrupts.Active(next, next.Cards?.Pending)) next = next with { Cards = next.Cards! with { HeroicInterrupt = next.Cards!.HeroicInterrupt! with { Moved = true } } };
        var response = new PendingResponse(side, command, basic, path, steps?.Select(p => p.Facing).ToArray(), MoveStart: c.Ships[unit.Definition].Footprint == 2 && command.Kind == "move" ? new(new(unit.Q, unit.R), unit.Facing) : null,
            MoveBudget: tabletop && command.Kind == "move" ? FleetMovement.Allowance(geometry, unit) : -1);
        next = next with { Cards = next.Cards! with { Response = response } };
        string activationMessage = "";
        if (basic && tabletop)
        {
            next = FleetPlanets.IsPlanet(c, unit) ? FleetRuleTools.ActivationControl(c, next, unit.Id, command.Repair, out activationMessage) : FleetRuleTools.Medic(c, next, unit.Id, out activationMessage);
            var activation = FleetTerrain.Activation(c, state, next, [unit.Id], "response-start", activationMessage);
            if (activation.State.Repairs is { Length: > 0 } || activation.State.Hazard != null || activation.State.Combat != null || activation.State.Defense != null || activation.State.Cards?.Response == null) return activation;
            // An automatically resolved terrain attack already resumed the action.
            if (activation.State.Sequence != next.Sequence) return activation;
        }
        return Continue(c, state, next, activationMessage, offerAttack: true);
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
    private static bool Offer(ContentCatalog c, BattleState state, PendingResponse pause) => !pause.Replan &&
        (HeroicInterrupts.Offer(c, state, pause) || Available(c, state, 1 - pause.Side) && HasTrigger(c, state, "attack", pause)
            && state.Units.Any(u => u.Side == 1 - pause.Side && CanShoot(c, state, u, pause.Command.Unit)) || OfferMove(c, state, pause));
    private static CommandResult Paused(BattleState original, BattleState next, string message) => new(true,
        message + " Interrupt window: the opponent may respond or decline.", next with { Sequence = original.Sequence + 1 });
    private static CommandResult Continue(ContentCatalog c, BattleState original, BattleState next, string message, bool offerAttack = false)
    {
        var pause = next.Cards!.Response!;
        var unit = next.Units.Find(u => u.Id == pause.Command.Unit);
        if (unit == null || unit.Hulk || unit.Patrol != "" || unit.Side != pause.Side || next.Finished) return Complete(c, original, next, message + " The unavailable unit's remaining actions were cancelled.");
        if (pause.ReplanAfterResponse) return new(true, message + " Revise the remaining movement route or stop here.", next with { Sequence = original.Sequence + 1, Cards = next.Cards with { Response = pause with { Replan = true, ReplanAfterResponse = false } } });
        if (pause.Command.Kind == "reinforce") return Complete(c, original, next, message + " Reinforcement response complete.");
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
                return Complete(c, original, FleetLogistics.Embark(next, unit with { Facing = facing }, pause.Command.Embark, preserveActivation: pause.ReactionMove), message + " Unit embarked; 1 Move spent entering the bay.");
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
            else pause = pause with { MoveSpent = pause.MoveSpent + FleetMovement.Cost(c, next, unit, priorStep, currentStep, pause.Command.Embark) };
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
        if (pause.ReactionMove) return FinishMoveInterrupt(c, original, next, message);
        next = next with { Cards = next.Cards with { Response = null } };
        if (pause.Command.Kind == "reinforce")
        {
            if (next.Reinforce != null) return FleetLogistics.Resume(c, original, next, message);
            if (next.Cards.Pending is { } launchOrder && !next.Units.Any(u => u.Id == SpecialOrders.Actor(c, next, launchOrder) && !u.Hulk && u.Side == launchOrder.Side))
                return SpecialOrders.Advance(c, original, next, launchOrder.Side, message, next.Finished);
            return new(true, message, next with { Sequence = original.Sequence + 1 });
        }
        if (!pause.Basic && pause.Command.Kind == "move" && next.Cards.Pending is { } order && next.Units.Any(u => u.Id == pause.Command.Unit))
            next = next with { Cards = next.Cards with { Pending = order with { Moved = true } } };
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
        if (pause.Replan) return Replan(c, state, side, command);
        if (command.Kind == "heroic-interrupt") return BeginHeroic(c, state, side, command);
        if (command.Kind == "interrupt-move") return BeginMoveInterrupt(c, state, side, command);
        if (side != 1 - pause.Side) return Reject("Waiting for the opponent's interrupt response.");
        if (command.Kind == "decline") return Continue(c, state, state, "Interrupt opportunity declined.");
        if (FleetRuleTools.Enabled(c, state) && command.Power) return Reject("Interrupt Attack does not grant Power Attack.");
        if (command.Kind != "interrupt" || !IsAttack(c, state, command.Card) || !Eligible(c, state, command.Card, pause) || !(side == 0 ? cards.Hand0 : cards.Hand1).Contains(command.Card)
            || !Actions(c, state, side, command.Unit).AttackTargets.Contains(command.Target)) return Reject("Requires Interrupt Attack and one of your units able to attack the acting enemy.");
        if (FleetRuleTools.Enabled(c, state) && !FleetCombat.Allowed(c, state, state.Units.Single(u => u.Id == command.Unit), state.Units.Find(u => u.Id == command.Target), command)) return Reject("The selected interrupt attack is unavailable.");
        cards = cards with { Hand0 = cards.Hand0.Where(id => id != command.Card).ToArray(), Hand1 = cards.Hand1.Where(id => id != command.Card).ToArray(),
            Interrupts0 = side == 0 ? Played(cards, side).Append(command.Card).ToArray() : cards.Interrupts0,
            Interrupts1 = side == 1 ? Played(cards, side).Append(command.Card).ToArray() : cards.Interrupts1 };
        cards = MarkUsed(cards, side, "attack");
        var spent = state with { Cards = cards };
        if (CanOfferCounter(c, spent, 1 - side)) return new(true, "Interrupt declared. The opponent may use Counterintelligence before its effect.",
            spent with { Cards = cards with { Counter = new(side, command) }, Sequence = state.Sequence + 1 });
        return ExecuteInterruptAttack(c, state, spent, side, command);
    }
    internal static CommandResult CombatResolved(ContentCatalog c, BattleState original, BattleState next, string message, bool interrupt) =>
        interrupt ? AfterInterrupt(c, original, next, "Interrupt Attack: " + message) : Complete(c, original, next, message);
    public static void Validate(ContentCatalog c, BattleState state)
    {
        var cards = state.Cards!;
        foreach (int side in new[] { 0, 1 })
        {
            if (Played(cards, side).Length > 4 || Played(cards, side).Any(id => !IsAttack(c, state, id) && !IsRepair(c, state, id) && !IsMove(c, state, id))
                || Played(cards, side).Select(id => CommandCards.Find(c, state, id)!.Interrupt).Distinct().Count() != Played(cards, side).Length) throw new InvalidDataException("Invalid played interrupts.");
            var used = Used(cards, side);
            if (used.Length > 4 || used.Distinct().Count() != used.Length || used.Any(k => k is not ("attack" or "move" or "repairs" or "counter"))) throw new InvalidDataException("Invalid used interrupt types.");
        }
        ValidateCounter(c, state);
        ValidateMoveInterrupt(c, state);
        HeroicInterrupts.Validate(c, state);
        if (cards.Response is not { } pause) return;
        if (state.Finished || cards.Phase != "orders" || pause.Side != state.ActiveSide || pause.Command == null || pause.Command.Kind is not ("move" or "attack" or "reinforce")
            || pause.RemainingPath == null || pause.RemainingPath.Length > 2500 || pause.Command.Facing is < 0 or > 5
            || !state.Units.Any(u => u.Id == pause.Command.Unit && u.Side == pause.Side && (u.Activated || pause.ReactionMove || HeroicInterrupts.Active(state, cards.Pending))) && state.Combat == null && state.Overrun == null
            || pause.ReactionMove != (cards.MoveInterrupt != null)
            || !pause.ReactionMove && (pause.Basic ? cards.Pending != null || !HeroicCommands.Spent(cards, pause.Command.Card) : cards.Pending == null || SpecialOrders.Actor(c, state, cards.Pending) != pause.Command.Unit))
            throw new InvalidDataException("Invalid response window.");
        var unit = state.Units.Find(u => u.Id == pause.Command.Unit); if (unit == null) return;
        var previous = pause.Transit ?? new Hex(unit.Q, unit.R);
        if ((pause.Basic && (pause.Side == 0 ? state.Orders0 : state.Orders1) < 1)
            || (!pause.Basic && !pause.ReactionMove && pause.Command.Kind != "reinforce" && (SpecialOrders.IsAttackAction(SpecialOrders.Action(c, state, cards.Pending!)) ? pause.Command.Kind != "attack" : pause.Command.Kind != "move"))
            || pause.Command.Kind == "move" && (pause.RemainingPath.LastOrDefault(new Hex(unit.Q, unit.R)) != new Hex(pause.Command.Q, pause.Command.R)))
            throw new InvalidDataException("Invalid suspended action.");
        foreach (var cell in pause.RemainingPath)
        {
            if ((previous.Distance(cell) != 1 && (pause.PathFacings == null || previous != cell)) || cell.Q < 0 || cell.R < 0) throw new InvalidDataException("Invalid pending movement path.");
            previous = cell;
        }
        if (pause.Command.Kind != "move" && pause.RemainingPath.Length != 0) throw new InvalidDataException("Non-movement action has a movement path.");
        if (pause.MoveBudget is < -1 or > 100000 || pause.MoveSpent < 0 || pause.MoveBudget >= 0 && pause.MoveSpent > pause.MoveBudget
            || (pause.Replan || pause.ReplanAfterResponse) && (pause.Command.Kind != "move" || pause.RemainingPath.Length != 0 || pause.Transit != null)
            || pause.Replan && (state.Combat != null || state.Defense != null || state.Hazard != null)) throw new InvalidDataException("Invalid remaining movement state.");
        if (pause.Command.Kind == "reinforce" && (state.Reinforce == null && !(cards.Pending is { } launch && FleetLaunchOrders.ArrivalStep(c, state, launch)) || pause.Replan || pause.ReplanAfterResponse)) throw new InvalidDataException("Invalid reinforcement response.");
        if (pause.PathFacings != null && (pause.PathFacings.Length != pause.RemainingPath.Length || pause.PathFacings.Any(f => f is < 0 or > 5))) throw new InvalidDataException("Invalid movement facing path.");
    }
    internal static void ValidateGeometry(ContentCatalog c, BattleState state)
    {
        if (state.Cards?.Response is not { } pause) return;
        var map = c.Maps[state.Map]; var rules = c.Rules[map.Rules];
        var actor = state.Units.Find(u => u.Id == pause.Command.Unit); if (actor == null) return;
        if (pause.Command.Embark != "" && (pause.Command.Kind != "move" || !state.Units.Any(p => p.Id == pause.Command.Embark && FleetFootprint.Contains(c, p, new(pause.Command.Q, pause.Command.R)) && FleetLogistics.CanEmbark(c, state, state.Units.Single(u => u.Id == pause.Command.Unit), p)))) throw new InvalidDataException("Invalid embark route.");
        if (pause.Transit is { } transit && (pause.Command.Kind != "move" || state.Hazard == null || state.Hazard.Job.Resume != "movement"
            || !FleetFootprint.CanPlace(c, state, state.Units.Single(u => u.Id == pause.Command.Unit), transit, pause.TransitFacing < 0 ? state.Units.Single(u => u.Id == pause.Command.Unit).Facing : pause.TransitFacing, transit: true)
            || !state.Units.Any(u => u.Id != pause.Command.Unit && u.Patrol == "" && FleetFootprint.Cells(c.Ships[state.Units.Single(a => a.Id == pause.Command.Unit).Definition], transit, pause.TransitFacing < 0 ? state.Units.Single(a => a.Id == pause.Command.Unit).Facing : pause.TransitFacing).Any(h => FleetFootprint.Contains(c, u, h)) && u.Side == pause.Side && !u.Hulk) || !rules.FriendlyPassThrough))
            throw new InvalidDataException("Invalid occupied transit cell.");
        var geometry = !pause.ReactionMove && state.Cards?.Pending is { } order ? SpecialOrders.Geometry(c, state, order) : c;
        if (pause.MoveStart != null)
        {
            int limit = FleetLargeMovement.Limit(geometry, actor); var previous = new MovementStep(pause.Transit ?? new(actor.Q, actor.R), pause.TransitFacing < 0 ? actor.Facing : pause.TransitFacing);
            int front = pause.FrontSpent, rear = pause.RearSpent;
            int originalLimit = pause.MoveBudget < 0 ? limit : Math.Min(2, pause.MoveBudget);
            if (c.Ships[actor.Definition].Footprint != 2 || front < 0 || rear < 0 || front > originalLimit || rear > originalLimit || pause.PathFacings == null) throw new InvalidDataException("Invalid two-hex movement budget.");
            for (int i = 0; i < pause.RemainingPath.Length; i++)
            {
                var step = new MovementStep(pause.RemainingPath[i], pause.PathFacings[i]); var cost = FleetLargeMovement.Cost(c, state, actor, previous, step); front += cost.Front; rear += cost.Rear; previous = step;
                if (front > limit || rear > limit) throw new InvalidDataException("A ship end exceeds its movement allowance.");
            }
        }
        else if (pause.Command.Kind == "move" && pause.MoveBudget >= 0 && !pause.Replan && !pause.ReplanAfterResponse)
        {
            int spent = pause.MoveSpent; var prior = new MovementStep(pause.Transit ?? new(actor.Q, actor.R), pause.TransitFacing < 0 ? actor.Facing : pause.TransitFacing);
            for (int i = 0; i < pause.RemainingPath.Length; i++) { var step = new MovementStep(pause.RemainingPath[i], pause.PathFacings?[i] ?? actor.Facing); spent += FleetMovement.Cost(geometry, state, actor, prior, step, pause.Command.Embark); prior = step; }
            if (spent > Math.Min(pause.MoveBudget, FleetMovement.Allowance(geometry, actor))) throw new InvalidDataException("Pending path exceeds remaining movement.");
        }
        if (pause.RemainingPath.Where((h, i) => !FleetFootprint.CanPlace(c, state, actor, h, pause.PathFacings?[i] ?? actor.Facing, transit: true, docking: pause.Command.Embark)).Any()
            || pause.Command.Kind == "attack" && state.Combat?.Kind != "collision" && state.Overrun == null && !(FleetRuleTools.Enabled(c, state)
                ? FleetCombat.Allowed(c, state, state.Units.Single(u => u.Id == pause.Command.Unit), state.Units.Find(u => u.Id == pause.Command.Target), pause.Command)
                : SkirmishGeometry.Attack(c, state, state.Units.Single(u => u.Id == pause.Command.Unit), state.Units.Find(u => u.Id == pause.Command.Target)).Allowed))
            throw new InvalidDataException("Invalid suspended action geometry.");
    }
}
