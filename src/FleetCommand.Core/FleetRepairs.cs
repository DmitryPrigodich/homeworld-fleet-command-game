namespace FleetCommand.Core;
public sealed record PendingRepair(string Unit, int Side, int Successes, int[] Rolls, bool Advance = false);
public static class FleetRepairs
{
    public static CommandResult Apply(ContentCatalog c, BattleState s, int side, BattleCommand cmd)
    {
        var pending = s.Repairs![0]; var unit = s.Units.Single(u => u.Id == pending.Unit);
        CommandResult Reject(string message) => new(false, message, s);
        if (side != pending.Side || cmd.Kind is not ("repair-choice" or "repair-stop")) return Reject("Allocate Damage Control successes first.");
        int left = pending.Successes;
        if (cmd.Kind == "repair-stop") left = 0;
        else
        {
            if (cmd.Repair == "focus" && unit.FocusFire > 0) unit = unit with { FocusFire = unit.FocusFire - 1 };
            else if (FleetRuleTools.Debuffs.Contains(cmd.Repair) && FleetRuleTools.Has(unit, cmd.Repair)) unit = unit with { Effects = (unit.Effects ?? []).Where(e => e != cmd.Repair).ToArray() };
            else return Reject("Choose an existing Focus Fire or Crippled token.");
            left--;
        }
        if (unit.FocusFire == 0 && !(unit.Effects ?? []).Any(FleetRuleTools.Debuffs.Contains)) left = 0;
        var queue = left == 0 ? s.Repairs.Skip(1).ToArray() : new[] { pending with { Successes = left } }.Concat(s.Repairs.Skip(1)).ToArray();
        var next = s with { Units = s.Units.Select(u => u.Id == unit.Id ? unit : u).ToList(), Repairs = queue.Length == 0 ? null : queue, Sequence = s.Sequence + 1 };
        string message = cmd.Kind == "repair-stop" ? "Remaining repair successes skipped." : "Damage Control: removed " + cmd.Repair + ".";
        if (queue.Length == 0 && next.Hazards is { Length: > 0 }) return FleetTerrain.StartQueued(c, s, next, message);
        if (queue.Length == 0 && next.Reinforce != null) return FleetLogistics.Resume(c, s, next, message);
        if (queue.Length == 0 && next.Cards?.Pending?.Deferred != null) return FleetTransfers.Complete(c, s, next, message);
        return queue.Length == 0 && pending.Advance ? SpecialOrders.Advance(c, s, next, side, message) : new(true, message, next);
    }
    internal static void Validate(BattleState s)
    {
        if (s.Repairs == null) return;
        if (s.Finished || s.Cards == null || s.Cards.Pending == null && s.Reinforce == null || s.Combat != null || s.Cards.Response != null || s.Repairs.Length == 0 || s.Repairs.Length > s.Units.Count
            || s.Repairs.Select(p => p.Unit).Distinct().Count() != s.Repairs.Length || s.Repairs.Any(p => p.Side != s.ActiveSide || p.Successes is < 1 or > 100
                || p.Rolls == null || p.Rolls.Length is < 1 or > 100 || p.Rolls.Any(d => d is < 1 or > 6) || p.Successes > p.Rolls.Length
                || !s.Units.Any(u => u.Id == p.Unit && u.Side == p.Side && u.Activated))) throw new InvalidDataException("Invalid damage control decisions.");
    }
}
