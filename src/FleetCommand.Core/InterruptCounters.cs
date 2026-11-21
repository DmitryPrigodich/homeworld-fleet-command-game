namespace FleetCommand.Core;

public sealed record PendingCounter(int Side, BattleCommand Command);
public static partial class InterruptRules
{
    public static bool IsCounter(ContentCatalog c, BattleState? s, string card) => CommandCards.Find(c, s, card) is { Kind: "interrupt", Interrupt: "counter" };
    public static string[] Used(CardRound cards, int side) => (side == 0 ? cards.UsedInterrupts0 : cards.UsedInterrupts1) ?? [];
    private static CardRound MarkUsed(CardRound cards, int side, string kind) => side == 0
        ? cards with { UsedInterrupts0 = Used(cards, side).Append(kind).Distinct().ToArray() }
        : cards with { UsedInterrupts1 = Used(cards, side).Append(kind).Distinct().ToArray() };
    // The opportunity depends on public deck/type usage, never the opponent's hidden hand.
    private static bool CanOfferCounter(ContentCatalog c, BattleState s, int side) => Available(c, s, side, "counter")
        && CommandCards.Deck(c, s).Entries.Any(e => c.Cards[e.Card].Interrupt == "counter");
    private static CommandResult ExecuteInterruptAttack(ContentCatalog c, BattleState before, BattleState spent, int side, BattleCommand command)
    {
        var result = Attack(c, spent, side, command with { Kind = "attack" }, interrupt: true);
        if (!result.Accepted) return result with { State = before };
        if (result.State.Combat != null || result.State.Defense != null || result.State.Overrun != null) return result with { State = result.State with { Sequence = before.Sequence + 1 } };
        return AfterInterrupt(c, before, result.State, "Interrupt Attack: " + result.Message);
    }
    public static CommandResult ResolveCounter(ContentCatalog c, BattleState s, int side, BattleCommand command)
    {
        var cards = s.Cards!; var frame = cards.Counter!;
        CommandResult Reject(string why) => new(false, why, s);
        if (side != 1 - frame.Side) return Reject("The opponent decides whether to counter the declared interrupt.");
        var next = s with { Cards = cards with { Counter = null } };
        if (command.Kind == "decline-counter" && frame.Command.Kind == "heroic-interrupt") return ExecuteHeroic(c, s, next, frame.Side, frame.Command);
        if (command.Kind == "decline-counter") return frame.Command.Kind == "interrupt-repairs" ? ExecuteRepair(c, next, frame.Command)
            : frame.Command.Kind == "interrupt-move" ? ExecuteMoveInterrupt(c, s, next, frame.Side, frame.Command) : ExecuteInterruptAttack(c, s, next, frame.Side, frame.Command);
        if (command.Kind != "counter" || !IsCounter(c, s, command.Card) || !Available(c, s, side, "counter")
            || !(side == 0 ? cards.Hand0 : cards.Hand1).Contains(command.Card)) return Reject("Requires an available Counterintelligence card; Counterintelligence cannot counter itself.");
        var used = MarkUsed(cards, side, "counter");
        next = next with { Cards = used with { Counter = null,
            Hand0 = cards.Hand0.Where(id => id != command.Card).ToArray(), Hand1 = cards.Hand1.Where(id => id != command.Card).ToArray(),
            Interrupts0 = Played(cards, 0).Where(id => id != frame.Command.Card).ToArray(), Interrupts1 = Played(cards, 1).Where(id => id != frame.Command.Card).ToArray(),
            Discard = cards.Discard.Concat(HeroicCommands.IsHeroic(s, frame.Command.Card) ? [] : new[] { frame.Command.Card }).Append(command.Card).ToArray() } };
        string cancelled = HeroicCommands.IsHeroic(s, frame.Command.Card) ? "Counterintelligence: Heroic card spent for the scenario; the reaction has no effect." : "Counterintelligence: both cards discarded; the interrupt has no effect.";
        return frame.Command.Kind == "interrupt-repairs" ? new(true, cancelled, next with { Sequence = s.Sequence + 1 }) : AfterInterrupt(c, s, next, cancelled);
    }
    private static void ValidateCounter(ContentCatalog c, BattleState s)
    {
        if (s.Cards?.Counter is not { } frame) return;
        var cards = s.Cards; var command = frame.Command;
        if (s.Finished || frame.Side is < 0 or > 1 || command == null || !(command.Kind == "heroic-interrupt" ? (cards.HeroicUsed ?? []).Contains(command.Card) : Played(cards, frame.Side).Contains(command.Card)) || !CanOfferCounter(c, s, 1 - frame.Side))
            throw new InvalidDataException("Invalid Counterintelligence opportunity.");
        if (command.Kind == "heroic-interrupt")
        {
            if (!HeroicInterrupts.CanDeclare(c, s, frame.Side, command, spent: true) || s.Combat != null || s.Defense != null || s.Hazard != null) throw new InvalidDataException("Invalid countered Heroic reaction.");
            return;
        }
        if (command.Kind == "interrupt-repairs")
        {
            if (cards.Phase != "orders" || !IsRepair(c, s, command.Card) || !Used(cards, frame.Side).Contains("repairs") || !RepairTargetAllowed(c, s, frame.Side, command)) throw new InvalidDataException("Invalid countered repair.");
            return;
        }
        if (command.Kind == "interrupt-move")
        {
            if (cards.Phase != "orders" || !IsMove(c, s, command.Card) || !Used(cards, frame.Side).Contains("move") || InterruptPath(c, s, frame.Side, command) == null || s.Combat != null || s.Defense != null || s.Hazard != null) throw new InvalidDataException("Invalid countered movement.");
            return;
        }
        if (cards.Response is not { } pause || frame.Side != 1 - pause.Side
            || command.Kind != "interrupt" || command.Power || !IsAttack(c, s, command.Card) || !Eligible(c, s, command.Card, pause)
            || !Used(cards, frame.Side).Contains("attack") || command.Target != pause.Command.Unit || !CanOfferCounter(c, s, 1 - frame.Side)
            || !s.Units.Any(u => u.Id == command.Unit && u.Side == frame.Side && CanShoot(c, s, u, command.Target))
            || FleetRuleTools.Enabled(c, s) && !FleetCombat.Allowed(c, s, s.Units.Single(u => u.Id == command.Unit), s.Units.Find(u => u.Id == command.Target), command)
            || s.Combat != null || s.Defense != null || s.CapEntry != null || s.Overrun != null || s.Hazard != null || s.Repairs is { Length: > 0 })
            throw new InvalidDataException("Invalid Counterintelligence opportunity.");
    }
}
