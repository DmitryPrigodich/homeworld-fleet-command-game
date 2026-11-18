namespace FleetCommand.Core;

// Ready to Launch orders the embarked squadron, never the parent carrier.
public static class FleetLaunchOrders
{
    public static bool IsLaunch(CardDefinition card) => card.Lines is [{ Actions: ["launch", "lightning", "attack"] }];
    public static bool CanAssign(ContentCatalog c, BattleState s, BattleUnit child) => FleetRuleTools.Enabled(c, s)
        && child.Embarked != "" && !child.Activated && !child.Hulk && Cells(c, s, child.Id).Length > 0;
    public static Hex[] Cells(ContentCatalog c, BattleState s, string childId)
    {
        var child = s.Reserves?.Find(u => u.Id == childId);
        var parent = child == null ? null : s.Units.Find(u => u.Id == child.Embarked && u.Side == child.Side);
        return parent == null || !FleetLogistics.CanLaunch(c, s, parent) ? [] : FleetLogistics.Adjacent(c, s, parent);
    }
    public static CommandResult Begin(ContentCatalog c, BattleState s, int side, BattleCommand cmd)
    {
        var cards = s.Cards!; var child = s.Reserves?.Find(u => u.Id == cmd.Unit && u.Side == side);
        if (child == null || !CanAssign(c, s, child) || s.ActiveSide != side || cards.Phase != "orders"
            || !(side == 0 ? cards.Hand0 : cards.Hand1).Contains(cmd.Card) || (side == 0 ? s.Orders0 : s.Orders1) >= FleetRuleTools.PlayLimit(c, s, side))
            return new(false, "Ready to Launch requires a ready embarked unit, a free exit and available Launch Tubes.", s);
        var next = s with { Sequence = s.Sequence + 1, Orders0 = s.Orders0 + (side == 0 ? 1 : 0), Orders1 = s.Orders1 + (side == 1 ? 1 : 0),
            Reserves = s.Reserves!.Select(u => u.Id == child.Id ? FleetRuleTools.NewOrder(u) with { Activated = true } : u).ToList(),
            Cards = cards with { Hand0 = cards.Hand0.Where(id => id != cmd.Card).ToArray(), Hand1 = cards.Hand1.Where(id => id != cmd.Card).ToArray(),
                Discard = cards.Discard.Append(cmd.Card).ToArray(), Pending = new(cmd.Card, side, [child.Id], 0) } };
        return new(true, "Ready to Launch: card and Play spent. Choose a green exit hex for the assigned squadron.", next);
    }
    public static CommandResult Place(ContentCatalog c, BattleState s, int side, BattleCommand cmd)
    {
        var order = s.Cards!.Pending!; var child = s.Reserves?.Find(u => u.Id == cmd.Unit && u.Id == SpecialOrders.Actor(c, s, order));
        var cell = new Hex(cmd.Q, cmd.R);
        if (child == null || child.Side != side || cmd.Facing is < 0 or > 5 || !Cells(c, s, child.Id).Contains(cell))
            return new(false, "Choose a free exit hex for the assigned embarked squadron.", s);
        var arriving = child with { Q = cmd.Q, R = cmd.R, Facing = cmd.Facing, Embarked = "" };
        var next = s with { Cards = s.Cards with { Pending = order with { Step = 1 } }, Reserves = s.Reserves!.Where(u => u.Id != child.Id).ToList(),
            Units = s.Units.Select(u => u.Id == child.Embarked ? u with { Launches = u.Launches + 1 } : u).Append(arriving).ToList() };
        next = FleetRuleTools.DamageControl(c, next, arriving.Id, cmd.Repair, out var repair);
        return FleetTerrain.Activation(c, s, next, [arriving.Id], "order", "Squadron launched; continue with Lightning Move and Attack. " + repair);
    }
}
