namespace FleetCommand.Core;
public static class FleetTransfers
{
    public static CommandResult Apply(ContentCatalog c, BattleState s, int side, BattleCommand cmd)
    {
        CommandResult Reject(string text) => new(false, text, s);
        var unit = s.Units.Find(u => u.Id == cmd.Unit && u.Side == side && !u.Activated && !u.Hulk);
        var target = s.Units.Find(u => u.Id == cmd.Target);
        var cards = s.Cards!;
        if (!FleetRuleTools.Enabled(c, s) || unit == null || target == null || unit.Patrol != "" || target.Patrol != "" || target.Side != side && (FleetCap.Patrol(s, target.Id) != null || FleetLogistics.Children(s, target.Id).Length > 0) || side != s.ActiveSide || new Hex(unit.Q, unit.R).Distance(new(target.Q, target.R)) != 1
            || target.Side != side && !target.Hulk || !(side == 0 ? cards.Hand0 : cards.Hand1).Contains(cmd.Card)
            || (side == 0 ? s.Orders0 : s.Orders1) >= FleetRuleTools.PlayLimit(c, s, side)) return Reject("Передача экипажа требует готовый отряд и соседнюю цель.");
        if (cmd.Amount < 0 || cmd.Amount > target.CrewDamage || cmd.Amount == 0 && !cmd.TransferLeader || cmd.Amount > 0 && c.Ships[unit.Definition].Crew <= 0
            || unit.CrewDamage + cmd.Amount > c.Ships[unit.Definition].Armor || target.CrewDamage - cmd.Amount > c.Ships[target.Definition].Armor)
            return Reject("После передачи Crew Damage обеих сторон должен быть не выше брони.");
        if (cmd.TransferLeader && (FleetRuleTools.LeaderIds(unit).Length == 0 || !FleetRuleTools.ValidLeader(c, FleetRuleTools.WithLeaders(target, FleetRuleTools.LeaderIds(target).Append(FleetRuleTools.LeaderIds(unit)[0]))))) return Reject("Лидера нельзя назначить этой цели.");
        var next = s with { Sequence = s.Sequence + 1, Units = s.Units.Select(u => u.Id == unit.Id ? FleetRuleTools.NewOrder(u) with { Activated = true } : u).ToList(),
            Orders0 = s.Orders0 + (side == 0 ? 1 : 0), Orders1 = s.Orders1 + (side == 1 ? 1 : 0),
            Cards = cards with { Hand0 = cards.Hand0.Where(id => id != cmd.Card).ToArray(), Hand1 = cards.Hand1.Where(id => id != cmd.Card).ToArray(),
                Discard = cards.Discard.Append(cmd.Card).ToArray(), Pending = new(cmd.Card, side, [unit.Id], 0, BasicAction: "transfer-crew", Deferred: cmd) } };
        next = FleetRuleTools.DamageControl(c, next, unit.Id, cmd.Repair, out var repairMessage);
        return FleetTerrain.Activation(c, s, next, [unit.Id], "order", repairMessage);
    }
    internal static CommandResult Complete(ContentCatalog c, BattleState before, BattleState next, string message)
    {
        var cmd = next.Cards!.Pending!.Deferred!;
        if (cmd.Kind == "transfer-stars") return FleetEconomy.CompleteTransfer(c, before, next, message);
        var sender = next.Units.Single(u => u.Id == cmd.Unit); var target = next.Units.Single(u => u.Id == cmd.Target);
        int side = sender.Side;
        string moving = cmd.TransferLeader ? FleetRuleTools.LeaderIds(sender)[0] : "";
        var updatedSender = sender with { CrewDamage = sender.CrewDamage + cmd.Amount, Effects = cmd.Amount > 0 ? (sender.Effects ?? []).Except(FleetRuleTools.Buffs).ToArray() : sender.Effects };
        var updatedTarget = target with { CrewDamage = target.CrewDamage - cmd.Amount, Side = target.Hulk ? side : target.Side };
        if (cmd.TransferLeader) { updatedSender = FleetRuleTools.WithLeaders(updatedSender, FleetRuleTools.LeaderIds(sender).Skip(1)); updatedTarget = FleetRuleTools.WithLeaders(updatedTarget, FleetRuleTools.LeaderIds(target).Append(moving)); }
        next = next with { Cards = next.Cards with { Pending = next.Cards.Pending with { Deferred = null } }, Units = next.Units.Select(u => u.Id == sender.Id ? updatedSender : u.Id == target.Id ? FleetClose.CheckHulk(c, updatedTarget) : u).ToList() };
        return SpecialOrders.Advance(c, before, next, side, message + $" Экипаж передан: Crew Damage цели -{cmd.Amount}, передающего отряда +{cmd.Amount}.");
    }
}
