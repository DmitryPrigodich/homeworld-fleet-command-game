namespace FleetCommand.Core;

public sealed record PendingMoveInterrupt(int Side, string Unit, string Card, int ParentSide, PendingResponse Response,
    PendingOrder? Order, PendingReinforce? Reinforce, PendingMoveInterrupt? Parent = null);
public static partial class InterruptRules
{
    public static bool IsMove(ContentCatalog c, BattleState? s, string card) => CommandCards.Find(c, s, card) is { Kind: "interrupt", Interrupt: "move" };
    private static bool HasTrigger(ContentCatalog c, BattleState s, string kind, PendingResponse pause) => CommandCards.Deck(c, s).Entries
        .Select(e => c.Cards[e.Card]).Any(d => d.Interrupt == kind && (d.InterruptTriggers ?? ["move", "lightning", "attack"]).Contains(Trigger(c, s, pause)));
    private static bool InMoveRange(ContentCatalog c, BattleState s, BattleUnit unit, CardDefinition card)
    {
        var target = s.Units.Find(u => u.Id == s.Cards!.Response!.Command.Unit);
        return target != null && !unit.Hulk && unit.Patrol == "" && unit.Side != target.Side && FleetFootprint.Distance(c, unit, target) <= card.InterruptRange;
    }
    private static bool OfferMove(ContentCatalog c, BattleState s, PendingResponse pause) => FleetRuleTools.Enabled(c, s) && Available(c, s, 1 - pause.Side, "move")
        && CommandCards.Deck(c, s).Entries.Select(e => c.Cards[e.Card]).Any(d => d.Interrupt == "move" && (d.InterruptTriggers ?? []).Contains(Trigger(c, s, pause))
            && s.Units.Any(u => u.Side == 1 - pause.Side && InMoveRange(c, s, u, d) && FleetMovement.Allowance(c, u) > 0));
    private static string MoveEmbark(ContentCatalog c, BattleState s, BattleUnit unit, Hex destination) => s.Units
        .FirstOrDefault(parent => FleetFootprint.Contains(c, parent, destination) && FleetLogistics.CanEmbark(c, s, unit, parent))?.Id ?? "";
    private static Hex[] MoveCells(ContentCatalog c, BattleState s, int side, string id)
    {
        if (s.Finished || !FleetRuleTools.Enabled(c, s) || s.Cards?.Counter != null || s.Cards?.Response is not { Replan: false } pause || side != 1 - pause.Side || !Available(c, s, side, "move")) return [];
        var unit = s.Units.Find(u => u.Id == id && u.Side == side);
        return unit != null && (side == 0 ? s.Cards.Hand0 : s.Cards.Hand1).Any(card => IsMove(c, s, card) && Eligible(c, s, card, pause) && InMoveRange(c, s, unit, CommandCards.Find(c, s, card)!))
            ? FleetMovement.Destinations(c, s, unit).Concat(s.Units.Where(parent => FleetLogistics.CanEmbark(c, s, unit, parent)
                && FleetMovement.Path(c, s, unit, new(parent.Q, parent.R), embark: parent.Id) != null).Select(parent => new Hex(parent.Q, parent.R))).Distinct().ToArray() : [];
    }
    private static MovementStep[]? InterruptPath(ContentCatalog c, BattleState s, int side, BattleCommand cmd)
    {
        var unit = s.Units.Find(u => u.Id == cmd.Unit && u.Side == side);
        if (s.Cards?.Response is not { Replan: false } pause || side != 1 - pause.Side || !IsMove(c, s, cmd.Card) || !Eligible(c, s, cmd.Card, pause)
            || unit == null || !InMoveRange(c, s, unit, CommandCards.Find(c, s, cmd.Card)!) || cmd.Facing is < 0 or > 5) return null;
        string embark = MoveEmbark(c, s, unit, new(cmd.Q, cmd.R));
        if (cmd.Embark != "" && cmd.Embark != embark) return null;
        return FleetMovement.Path(c, s, unit, new(cmd.Q, cmd.R), cmd.AutoFacing ? -1 : cmd.Facing, embark);
    }
    private static CommandResult BeginMoveInterrupt(ContentCatalog c, BattleState s, int side, BattleCommand cmd)
    {
        var cards = s.Cards!;
        if (!FleetRuleTools.Enabled(c, s) || !Available(c, s, side, "move") || !(side == 0 ? cards.Hand0 : cards.Hand1).Contains(cmd.Card) || InterruptPath(c, s, side, cmd) == null)
            return new(false, "Interrupt Move requires an allied unit within range of the acting enemy and a legal movement destination.", s);
        cards = MarkUsed(cards, side, "move") with { Hand0 = cards.Hand0.Where(id => id != cmd.Card).ToArray(), Hand1 = cards.Hand1.Where(id => id != cmd.Card).ToArray(),
            Interrupts0 = side == 0 ? Played(cards, side).Append(cmd.Card).ToArray() : cards.Interrupts0, Interrupts1 = side == 1 ? Played(cards, side).Append(cmd.Card).ToArray() : cards.Interrupts1 };
        var spent = s with { Cards = cards };
        return CanOfferCounter(c, spent, 1 - side) ? new(true, "Interrupt Move declared; the opponent may counter before movement.", spent with { Sequence = s.Sequence + 1, Cards = cards with { Counter = new(side, cmd) } })
            : ExecuteMoveInterrupt(c, s, spent, side, cmd);
    }
    private static CommandResult ExecuteMoveInterrupt(ContentCatalog c, BattleState before, BattleState s, int side, BattleCommand cmd)
    {
        var steps = InterruptPath(c, s, side, cmd)!; var unit = s.Units.Single(u => u.Id == cmd.Unit); var cards = s.Cards!;
        var frame = new PendingMoveInterrupt(side, unit.Id, cmd.Card, s.ActiveSide, cards.Response!, cards.Pending, s.Reinforce, cards.MoveInterrupt);
        var move = cmd with { Kind = "move", Card = "", Embark = MoveEmbark(c, s, unit, new(cmd.Q, cmd.R)) };
        var pause = new PendingResponse(side, move, false, steps.Select(p => p.Cell).ToArray(), steps.Select(p => p.Facing).ToArray(),
            MoveStart: c.Ships[unit.Definition].Footprint == 2 ? new(new(unit.Q, unit.R), unit.Facing) : null, ReactionMove: true, MoveBudget: FleetMovement.Allowance(c, unit));
        var next = s with { ActiveSide = side, Reinforce = null, Cards = cards with { Pending = null, Response = pause, MoveInterrupt = frame } };
        return Continue(c, before, next, "Interrupt Move: no Play, activation or activation Damage Control spent.");
    }
    private static CommandResult FinishMoveInterrupt(ContentCatalog c, BattleState before, BattleState s, string message)
    {
        var frame = s.Cards!.MoveInterrupt!; var actor = s.Units.Find(u => u.Id == frame.Response.Command.Unit);
        var parent = frame.Response;
        if (parent.Command.Kind == "move" && actor != null) parent = parent with { Replan = false, ReplanAfterResponse = true, RemainingPath = [], PathFacings = [],
            Command = parent.Command with { Q = actor.Q, R = actor.R, Embark = "" } };
        var next = s with { ActiveSide = frame.ParentSide, Reinforce = frame.Reinforce,
            Cards = s.Cards with { Pending = frame.Order, Response = parent, MoveInterrupt = frame.Parent } };
        return AfterInterrupt(c, before, next, message + " The original action resumes with its remaining movement allowance.");
    }
    private static CommandResult AfterInterrupt(ContentCatalog c, BattleState before, BattleState next, string message)
    {
        var pause = next.Cards!.Response!; var actor = next.Units.Find(u => u.Id == pause.Command.Unit);
        if (actor == null || actor.Hulk || actor.Patrol != "" || actor.Side != pause.Side) return Continue(c, before, next, message);
        if (pause.Command.Kind == "move" && !pause.ReplanAfterResponse && FleetRuleTools.Has(actor, "engines") && !FleetRuleTools.Has(before.Units.FirstOrDefault(u => u.Id == actor.Id) ?? actor, "engines"))
            next = next with { Cards = next.Cards with { Response = pause = pause with { ReplanAfterResponse = true, RemainingPath = [], PathFacings = [], Command = pause.Command with { Q = actor.Q, R = actor.R, Embark = "" } } } };
        return Offer(c, next, pause) ? Paused(before, next, message) : Continue(c, before, next, message);
    }
    private static ContentCatalog ReplanGeometry(ContentCatalog c, BattleState s, PendingResponse pause)
    {
        var geometry = s.Cards!.Pending is { } order ? SpecialOrders.Geometry(c, s, order) : c;
        var unit = s.Units.Single(u => u.Id == pause.Command.Unit);
        if (c.Ships[unit.Definition].Footprint == 2) return geometry;
        int current = FleetMovement.Allowance(geometry, unit);
        int left = Math.Max(0, (pause.MoveBudget < 0 ? current : Math.Min(current, pause.MoveBudget)) - pause.MoveSpent);
        var copy = geometry.CopyForRuntime(); var ship = copy.Ships[unit.Definition];
        copy.Ships[ship.Id] = ship with { Move = left, Abilities = (ship.Abilities ?? []).Where(id => copy.Abilities[id].Handler != "move-bonus").ToArray() };
        return copy;
    }
    private static MovementStep[]? ReplanPath(ContentCatalog c, BattleState s, PendingResponse pause, Hex cell, int facing, out string embark)
    {
        var geometry = ReplanGeometry(c, s, pause); var unit = s.Units.Single(u => u.Id == pause.Command.Unit); var order = s.Cards!.Pending;
        embark = s.Units.FirstOrDefault(p => FleetFootprint.Contains(c, p, cell) && FleetLogistics.CanEmbark(c, s, unit, p))?.Id ?? "";
        bool straight = order != null && SpecialOrders.StraightCourse(c, s, order);
        if (straight && order!.Direction >= 0 && SpecialOrders.StraightDirection(new(unit.Q, unit.R), cell) != order.Direction) return null;
        if (order != null && SpecialOrders.Action(c, s, order) == "lightning" && (FleetRuleTools.Has(unit, "engines") || SpecialOrders.Lightning(c, unit) == 0
            || SpecialOrders.AdjacentOnLightning(c, s, order) && !s.Units.Any(first => first.Id == order.Units[0] && cell.Distance(new(first.Q, first.R)) == 1))) return null;
        return c.Ships[unit.Definition].Footprint == 2 ? FleetLargeMovement.Path(geometry, s, unit, cell, facing, straight, pause.FrontSpent, pause.RearSpent)
            : straight ? FleetMovement.StraightPath(geometry, s, unit, cell, embark) : FleetMovement.Path(geometry, s, unit, cell, facing, embark);
    }
    private static Hex[] ReplanCells(ContentCatalog c, BattleState s, PendingResponse pause)
    {
        var unit = s.Units.Single(u => u.Id == pause.Command.Unit); var geometry = ReplanGeometry(c, s, pause);
        var cells = c.Ships[unit.Definition].Footprint == 2 ? FleetLargeMovement.Destinations(geometry, s, unit, pause.FrontSpent, pause.RearSpent) : FleetMovement.Destinations(geometry, s, unit);
        return cells.Concat(s.Units.Where(p => FleetLogistics.CanEmbark(c, s, unit, p)).Select(p => new Hex(p.Q, p.R))).Distinct()
            .Where(h => ReplanPath(c, s, pause, h, -1, out _) != null).ToArray();
    }
    private static CommandResult Replan(ContentCatalog c, BattleState s, int side, BattleCommand cmd)
    {
        var pause = s.Cards!.Response!;
        if (side != pause.Side) return new(false, "The moving unit's owner chooses the remaining route.", s);
        if (cmd.Kind == "stop-move") return Complete(c, s, s, "Remaining movement skipped.");
        var path = cmd.Kind == "resume-move" && cmd.Unit == pause.Command.Unit && cmd.Facing is >= 0 and <= 5 ? ReplanPath(c, s, pause, new(cmd.Q, cmd.R), cmd.AutoFacing ? -1 : cmd.Facing, out _) : null;
        if (path == null) return new(false, "Choose a legal destination using only the remaining movement allowance.", s);
        ReplanPath(c, s, pause, new(cmd.Q, cmd.R), cmd.AutoFacing ? -1 : cmd.Facing, out string embark);
        var command = pause.Command with { Q = cmd.Q, R = cmd.R, Facing = cmd.Facing, AutoFacing = cmd.AutoFacing, Embark = embark };
        var next = s with { Cards = s.Cards with { Response = pause with { Replan = false, Command = command, RemainingPath = path.Select(p => p.Cell).ToArray(), PathFacings = path.Select(p => p.Facing).ToArray() } } };
        return Continue(c, s, next, "Remaining movement route selected.");
    }
    private static void ValidateMoveInterrupt(ContentCatalog c, BattleState s)
    {
        var frame = s.Cards?.MoveInterrupt;
        if (frame == null) return;
        if (s.Cards!.Response is not { ReactionMove: true } pause || pause.Command.Unit != frame.Unit || frame.Side != s.ActiveSide || pause.Side != frame.Side || s.Reinforce != null || s.Cards.Pending != null)
            throw new InvalidDataException("Invalid active Interrupt Move.");
        int depth = 0;
        for (; frame != null; frame = frame.Parent)
        {
            if (++depth > 2 || frame.Side is < 0 or > 1 || frame.ParentSide != 1 - frame.Side || frame.Response == null || frame.Response.Command == null
                || frame.Response.Side != frame.ParentSide || frame.Response.Command.Kind is not ("move" or "reinforce") || !IsMove(c, s, frame.Card)
                || !Played(s.Cards, frame.Side).Contains(frame.Card) || !Used(s.Cards, frame.Side).Contains("move") || frame.Response.ReactionMove != (frame.Parent != null)
                || !frame.Response.Basic && !frame.Response.ReactionMove && frame.Order == null
                || frame.Order != null && (frame.Order.Side != frame.ParentSide || frame.Response.Basic) || frame.Reinforce != null && frame.Reinforce.Side != frame.ParentSide
                || string.IsNullOrWhiteSpace(frame.Response.Command.Unit) || frame.Response.Command.Unit.Length > 80
                || s.Units.Any(u => u.Id == frame.Response.Command.Unit && u.Side != frame.ParentSide)
                || frame.Parent != null && frame.Response.Command.Unit != frame.Parent.Unit) throw new InvalidDataException("Invalid suspended movement context.");
            var restored = s with { ActiveSide = frame.ParentSide, Reinforce = frame.Reinforce, Cards = s.Cards with { Pending = frame.Order, Response = frame.Response, Counter = null, MoveInterrupt = frame.Parent } };
            // A nested reaction may destroy the original mover. Its saved order is cancelled when restored.
            SpecialOrders.Validate(c, restored, suspended: true);
            if (frame.Order != null && SpecialOrders.Actor(c, restored, frame.Order) != frame.Response.Command.Unit) throw new InvalidDataException("Suspended order actor differs from its movement.");
        }
    }
}
