namespace FleetCommand.Core;

public static partial class InterruptRules
{
    public static bool IsRepair(ContentCatalog c, BattleState? s, string card) => CommandCards.Find(c, s, card) is { Kind: "interrupt", Interrupt: "repairs" };
    public static BattleUnit[] RepairTargets(ContentCatalog c, BattleState s, int side) => s.Units.Concat(s.Reserves ?? []).Where(u => u.Side == side && !u.Hulk
        && (u.FocusFire > 0 || FleetRuleTools.Class(c.Ships[u.Definition]) == "fighter" && u.Models < c.Ships[u.Definition].Models)).ToArray();
    private static bool RepairTargetAllowed(ContentCatalog c, BattleState s, int side, BattleCommand cmd)
    {
        var unit = s.Units.Concat(s.Reserves ?? []).FirstOrDefault(u => u.Id == cmd.Unit && u.Side == side && !u.Hulk);
        return FleetRuleTools.Enabled(c, s) && unit != null && (cmd.Repair == "focus" ? unit.FocusFire > 0
            : cmd.Repair == "fighter" && FleetRuleTools.Class(c.Ships[unit.Definition]) == "fighter" && unit.Models < c.Ships[unit.Definition].Models);
    }
    public static CommandResult Repair(ContentCatalog c, BattleState s, int side, BattleCommand cmd)
    {
        var cards = s.Cards!;
        if (cards.Counter != null || cards.Phase != "orders" || !IsRepair(c, s, cmd.Card) || !Available(c, s, side, "repairs")
            || !(side == 0 ? cards.Hand0 : cards.Hand1).Contains(cmd.Card) || !RepairTargetAllowed(c, s, side, cmd)) return new(false, "Interrupt Repairs requires your damaged fighter or Focus Fire token and an unused Repairs interrupt.", s);
        cards = MarkUsed(cards, side, "repairs") with { Hand0 = cards.Hand0.Where(id => id != cmd.Card).ToArray(), Hand1 = cards.Hand1.Where(id => id != cmd.Card).ToArray(),
            Interrupts0 = side == 0 ? Played(cards, side).Append(cmd.Card).ToArray() : cards.Interrupts0, Interrupts1 = side == 1 ? Played(cards, side).Append(cmd.Card).ToArray() : cards.Interrupts1 };
        var next = s with { Cards = cards };
        return CanOfferCounter(c, next, 1 - side)
            ? new(true, "Interrupt Repairs declared; the opponent may use Counterintelligence before the repair.", next with { Sequence = s.Sequence + 1, Cards = cards with { Counter = new(side, cmd) } })
            : ExecuteRepair(c, next, cmd);
    }
    private static CommandResult ExecuteRepair(ContentCatalog c, BattleState s, BattleCommand cmd)
    {
        var unit = s.Units.Concat(s.Reserves ?? []).Single(u => u.Id == cmd.Unit);
        var repaired = cmd.Repair == "focus" ? unit with { FocusFire = Math.Max(0, unit.FocusFire - 2) } : unit with { Models = unit.Models + 1 };
        // Already rolled CAP/collision pools remain unchanged; their actor snapshots must track
        // the repaired physical unit so simultaneous resolution and save validation agree.
        PendingCombat? Volley(PendingCombat? roll) => roll?.Ghost is { } ghost && ghost.Id == unit.Id
            ? roll with { Ghost = ghost with { Models = repaired.Models, FocusFire = repaired.FocusFire } } : roll;
        var next = s with { Sequence = s.Sequence + 1, Units = s.Units.Select(u => u.Id == unit.Id ? repaired : u).ToList(),
            Reserves = s.Reserves?.Select(u => u.Id == unit.Id ? repaired : u).ToList(), Combat = Volley(s.Combat),
            Defense = s.Defense == null ? null : s.Defense with { CapVolley = Volley(s.Defense.CapVolley), IncomingVolley = Volley(s.Defense.IncomingVolley), PreparedIncoming = Volley(s.Defense.PreparedIncoming) } };
        return new(true, "Interrupt Repairs: " + (cmd.Repair == "focus" ? "one Focus Fire token removed from " : "one fighter restored to ") + FleetPresentation.UnitName(c, next, repaired) + ".", next);
    }
}
