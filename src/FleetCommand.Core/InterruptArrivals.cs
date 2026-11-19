namespace FleetCommand.Core;
public static partial class InterruptRules
{
    // The disembarked/deployed unit counts as activated; the carrier need not be activated.
    internal static CommandResult? ArrivalOpportunity(ContentCatalog c, BattleState before, BattleState s, string id, string message)
    {
        var unit = s.Units.Find(u => u.Id == id);
        if (s.Finished || !FleetRuleTools.Enabled(c, s) || unit == null || unit.Hulk || unit.Side != s.ActiveSide || s.Cards?.Response != null) return null;
        string card = s.Reinforce?.Card ?? s.Cards!.Pending!.Card;
        var pause = new PendingResponse(unit.Side, new("reinforce", unit.Id, Card: card), s.Reinforce != null, []);
        var next = s with { Cards = s.Cards! with { Response = pause } };
        return Offer(c, next, pause) ? Paused(before, next, message + " Reinforcement arrived.") : null;
    }
}
