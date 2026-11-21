namespace FleetCommand.Core;

// Ready to Launch orders the embarked squadron, never the parent carrier.
public static class FleetLaunchOrders
{
    public static bool IsLaunch(CardDefinition card) => card.Lines is { Length: > 0 } lines && lines.All(l => l.Actions.SequenceEqual(["launch", "lightning", "attack"]))
        && (lines.Length == 1 || card.LaunchFromHero && lines.Length == 2);
    public static bool FromOwner(ContentCatalog c, BattleState s, CardDefinition card, BattleUnit child) => !card.LaunchFromHero
        || s.Units.Any(p => p.Id == child.Embarked && p.Side == child.Side && !p.Hulk && FleetRuleTools.LeaderIds(p).Contains(card.HeroicOwner));
    public static bool ArrivalStep(ContentCatalog c, BattleState s, PendingOrder order) => order.BasicAction == "" && IsLaunch(CommandCards.Find(c, s, order.Card)!)
        && order.Step % 3 == 1;
    public static bool CanAssign(ContentCatalog c, BattleState s, BattleUnit child) => FleetRuleTools.Enabled(c, s)
        && child.Embarked != "" && !child.Activated && !child.Hulk && Cells(c, s, child.Id).Length > 0;
    public static Hex[] Cells(ContentCatalog c, BattleState s, string childId)
    {
        var child = s.Reserves?.Find(u => u.Id == childId);
        var parent = child == null ? null : s.Units.Find(u => u.Id == child.Embarked && u.Side == child.Side);
        return parent == null || !FleetLogistics.CanLaunch(c, s, parent)
            || s.Cards?.Pending is { BasicAction: "" } order && CommandCards.Find(c, s, order.Card) is { LaunchFromHero: true } card && !FromOwner(c, s, card, child!) ? [] : FleetLogistics.Adjacent(c, s, parent);
    }
    public static CommandResult Begin(ContentCatalog c, BattleState s, int side, BattleCommand cmd)
    {
        var cards = s.Cards!; var card = CommandCards.Find(c, s, cmd.Card)!;
        string[] ids = card.Lines!.Length == 2 ? [cmd.Unit, cmd.OtherUnit] : [cmd.Unit];
        var children = ids.Where(id => id != "").Select(id => s.Reserves?.Find(u => u.Id == id && u.Side == side)).ToArray();
        if (children.Length == 0 || ids.Distinct().Count() != ids.Length || children.Any(u => u == null)
            || ids.Where((id, line) => id != "" && !SpecialOrders.CanAssign(c, s, card, s.Reserves?.Find(u => u.Id == id), line)).Any()
            || s.ActiveSide != side || cards.Phase != "orders" || !HeroicCommands.CanUse(c, s, side, cmd.Card)
            || !(side == 0 ? cards.Hand0 : cards.Hand1).Contains(cmd.Card) || (side == 0 ? s.Orders0 : s.Orders1) >= FleetRuleTools.PlayLimit(c, s, side))
            return new(false, "Launch order requires different ready embarked units, a matching carrier, a free exit and available Launch Tubes.", s);
        var next = s with { Sequence = s.Sequence + 1, Orders0 = s.Orders0 + (side == 0 ? 1 : 0), Orders1 = s.Orders1 + (side == 1 ? 1 : 0),
            Reserves = s.Reserves!.Select(u => ids.Contains(u.Id) ? FleetRuleTools.NewOrder(u) with { Activated = true } : u).ToList(),
            Cards = HeroicCommands.Spend(cards, cmd.Card) with { Pending = new(cmd.Card, side, ids, cmd.Unit == "" ? 3 : 0) } };
        return new(true, "Launch order: card and Play spent. Choose a green exit hex for the assigned squadron.", next);
    }
    public static CommandResult Place(ContentCatalog c, BattleState s, int side, BattleCommand cmd)
    {
        var order = s.Cards!.Pending!; var child = s.Reserves?.Find(u => u.Id == cmd.Unit && u.Id == SpecialOrders.Actor(c, s, order));
        var cell = new Hex(cmd.Q, cmd.R);
        if (child == null || child.Side != side || !FromOwner(c, s, CommandCards.Find(c, s, order.Card)!, child) || cmd.Facing is < 0 or > 5 || !Cells(c, s, child.Id).Contains(cell))
            return new(false, "Choose a free exit hex for the assigned embarked squadron.", s);
        var arriving = child with { Q = cmd.Q, R = cmd.R, Facing = cmd.Facing, Embarked = "" };
        var next = s with { Cards = s.Cards with { Pending = order with { Step = order.Step + 1, LaunchArrival = true } }, Reserves = s.Reserves!.Where(u => u.Id != child.Id).ToList(),
            Units = s.Units.Select(u => u.Id == child.Embarked ? u with { Launches = u.Launches + 1 } : u).Append(arriving).ToList() };
        next = FleetRuleTools.ActivationControl(c, next, arriving.Id, cmd.Repair, out var repair);
        return FleetTerrain.Activation(c, s, next, [arriving.Id], "launch-order", "Squadron launched; continue with Lightning Move and Attack. " + repair);
    }
    internal static CommandResult Resume(ContentCatalog c, BattleState before, BattleState s, string message)
    {
        var order = s.Cards!.Pending!; string actor = SpecialOrders.Actor(c, s, order);
        var next = s with { Cards = s.Cards with { Pending = order with { LaunchArrival = false } } };
        if (s.Finished || !s.Units.Any(u => u.Id == actor)) return SpecialOrders.Advance(c, before, next, order.Side, message, s.Finished);
        return InterruptRules.ArrivalOpportunity(c, before, next, actor, message) ?? new(true, message, next with { Sequence = before.Sequence + 1 });
    }
}
