namespace FleetCommand.Core;
public sealed record PendingOverrun(int Side, string Unit, Hex Destination, bool Interrupt);
public static class FleetClose
{
    public static string Window(BattleState s) => $"{s.Round}:{s.ActiveSide}:{(s.ActiveSide == 0 ? s.Orders0 : s.Orders1)}";
    public static bool Allowed(ContentCatalog c, BattleState s, BattleUnit unit, BattleUnit? target, string kind)
    {
        if (kind is not ("ram" or "board") || target == null || unit.Hulk || target.Side == unit.Side || FleetFootprint.Distance(c, unit, target) != 1
            || FleetRuleTools.Class(c.Ships[target.Definition]) == "fighter" || unit.AttackWindow == Window(s)) return false;
        var ship = c.Ships[unit.Definition];
        return kind == "board" ? ship.Crew > 0 : ship.Ram > 0 && (FleetRuleTools.Class(ship) == "fighter" || FleetFootprint.Contains(c, target, new Hex(unit.Q, unit.R).Neighbor(unit.Facing)));
    }
    public static bool CaptureAllowed(ContentCatalog c, BattleState s, BattleUnit target) => (c.Maps[s.Map].CaptureTargets ?? []).Any(id => id == target.Id || id == target.Definition) || c.Maps[s.Map].Objectives?.Any(o => o.Kind == "capture" && o.Target == target.Id) == true;
    public static BattleUnit CheckHulk(ContentCatalog c, BattleUnit unit) => unit with { Hulk = FleetRuleTools.Class(c.Ships[unit.Definition]) is "capital" or "station" && unit.CrewDamage > c.Ships[unit.Definition].Armor };
    internal static BattleState Award(ContentCatalog c, BattleState s, BattleUnit destroyed, int side)
    {
        int Cost(BattleUnit unit) => c.Ships[unit.Definition].Stars + FleetRuleTools.AssignedLeaders(c, unit).Sum(l => l.Stars);
        int stars = Cost(destroyed) + FleetLogistics.Children(s, destroyed.Id).Sum(Cost);
        s = FleetObjectives.RecordLoss(c, s, destroyed, side);
        s = FleetLogistics.LoseChildren(s, destroyed.Id);
        return s with { Stars0 = s.Stars0 + (side == 0 ? stars : 0), Stars1 = s.Stars1 + (side == 1 ? stars : 0) };
    }
    internal static BattleState Outcome(BattleState s, ContentCatalog? c = null)
    {
        if (c != null && FleetObjectives.Enabled(c, s)) return FleetObjectives.Check(c, s);
        bool a = FleetLogistics.HasForce(s, 0), b = FleetLogistics.HasForce(s, 1);
        return s with { Finished = !a || !b, Winner = a == b ? null : a ? 0 : 1 };
    }
    internal static BattleState OfferOverrun(ContentCatalog c, BattleState s, BattleUnit attacker, BattleUnit target, bool interrupt)
    {
        if (s.Finished || !s.Units.Any(u => u.Id == attacker.Id)) return s;
        var options = FleetFootprint.Cells(c, target).Where(h => new Hex(attacker.Q, attacker.R).Distance(h) == 1 && FleetFootprint.CanPlace(c, s, attacker, h, attacker.Facing)).ToArray();
        return options.Length == 0 ? s : s with { Overrun = new(attacker.Side, attacker.Id, options[0], interrupt) };
    }
    public static CommandResult Apply(ContentCatalog c, BattleState s, int side, BattleCommand cmd)
    {
        var overrun = s.Overrun!;
        if (side != overrun.Side || cmd.Kind is not ("overrun" or "decline-overrun")) return new(false, "Решите, занимать ли освобождённый гекс.", s);
        var next = s with { Overrun = null, Units = s.Units.Select(u => u.Id == overrun.Unit && cmd.Kind == "overrun" ? u with { Q = overrun.Destination.Q, R = overrun.Destination.R } : u).ToList() };
        return InterruptRules.CombatResolved(c, s, next, cmd.Kind == "overrun" ? "Отряд занял гекс после ближней атаки." : "Продвижение после атаки пропущено.", overrun.Interrupt);
    }
    internal static void Validate(ContentCatalog c, BattleState s)
    {
        if (s.Overrun is not { } o) return;
        if (s.Finished || s.Combat != null || s.Cards?.Response == null || o.Side != (o.Interrupt ? 1 - s.Cards.Response.Side : s.Cards.Response.Side)
            || !s.Units.Any(u => u.Id == o.Unit && u.Side == o.Side && new Hex(u.Q, u.R).Distance(o.Destination) == 1)
            || !FleetFootprint.CanPlace(c, s, s.Units.Single(u => u.Id == o.Unit), o.Destination, s.Units.Single(u => u.Id == o.Unit).Facing)) throw new InvalidDataException("Invalid overrun decision.");
    }
}
