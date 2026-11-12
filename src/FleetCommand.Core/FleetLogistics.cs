namespace FleetCommand.Core;

public sealed record PendingReinforce(int Side, string Card, string Source, string Mode);
public static class FleetLogistics
{
    public static BattleUnit[] Children(BattleState s, string parent) => (s.Reserves ?? []).Where(u => u.Embarked == parent).ToArray();
    public static BattleState LoseChildren(BattleState s, string parent) => s with { Reserves = s.Reserves?.Where(u => u.Embarked != parent).ToList() };
    public static bool HasForce(BattleState s, int side) => s.Units.Any(u => u.Side == side) || s.Reserves?.Any(u => u.Side == side) == true;
    public static bool Empty(ContentCatalog c, BattleState s, Hex h) => FleetFootprint.Inside(c.Maps[s.Map], h)
        && !SkirmishGeometry.Blocked(c, c.Maps[s.Map], h, s) && !s.Units.Any(u => u.Patrol == "" && FleetFootprint.Contains(c, u, h));
    public static Hex[] Adjacent(ContentCatalog c, BattleState s, BattleUnit parent) => FleetFootprint.Adjacent(c, parent).Where(h => Empty(c, s, h)).ToArray();
    public static Hex[] DeployCells(ContentCatalog c, BattleState s, int side)
    {
        var map = c.Maps[s.Map]; if (s.Round < map.ReserveFromRound) return [];
        return (map.SetupZones?[side] ?? Enumerable.Range(side == 0 ? 0 : map.Width - 2, 2).SelectMany(q => Enumerable.Range(0, map.Height).Select(r => new Hex(q, r))).ToArray())
            .Where(h => Empty(c, s, h)).Distinct().ToArray();
    }
    public static bool CanLaunch(ContentCatalog c, BattleState s, BattleUnit parent) => !parent.Hulk && parent.Patrol == "" && !FleetRuleTools.Has(parent, "bays")
        && parent.Launches < c.Ships[parent.Definition].LaunchTubes;
    public static string[] Buildable(ContentCatalog c, BattleState s, BattleUnit parent) => parent.Hulk || parent.Patrol != "" ? [] : c.Ships.Values
        .Where(ship => ship.Faction == c.Ships[parent.Definition].Faction && ship.Stars > 0 && (c.Ships[parent.Definition].Factory ?? []).Contains(FleetRuleTools.Class(ship)))
        .Select(ship => ship.Id).Order(StringComparer.Ordinal).ToArray();
    public static bool CanReinforce(ContentCatalog c, BattleState s, int side) => FleetRuleTools.Enabled(c, s) &&
        ((s.Reserves ?? []).Any(u => u.Side == side && u.Embarked == "" && !u.Activated) && DeployCells(c, s, side).Length > 0
        || s.Units.Any(p => p.Side == side && !p.Hulk && (CanLaunch(c, s, p) && Children(s, p.Id).Any(u => !u.Activated) && Adjacent(c, s, p).Length > 0
            || Buildable(c, s, p).Any(id => c.Ships[id].Stars <= p.Cargo + (side == 0 ? s.Funds0 : s.Funds1)
                && (CanLaunch(c, s, p) && Adjacent(c, s, p).Length > 0 || FleetRuleTools.Strike(c.Ships[id]) && Children(s, p.Id).Length < c.Ships[p.Definition].Bays)))));
    public static CommandResult Begin(ContentCatalog c, BattleState s, int side, BattleCommand cmd)
    {
        var cards = s.Cards!; var board = s.Units.Find(u => u.Id == cmd.Unit && u.Side == side && !u.Hulk && u.Patrol == "");
        var reserve = s.Reserves?.Find(u => u.Id == cmd.Unit && u.Side == side && u.Embarked == "" && !u.Activated);
        bool allowed = cmd.Target switch
        {
            "deploy" => reserve != null && DeployCells(c, s, side).Length > 0,
            "launch" => board != null && CanLaunch(c, s, board) && Children(s, board.Id).Any(u => !u.Activated) && Adjacent(c, s, board).Length > 0,
            "build" => board != null && Buildable(c, s, board).Length > 0,
            _ => false
        };
        if (!FleetRuleTools.Enabled(c, s) || !allowed || side != s.ActiveSide || !(side == 0 ? cards.Hand0 : cards.Hand1).Contains(cmd.Card)
            || (side == 0 ? s.Orders0 : s.Orders1) >= FleetRuleTools.PlayLimit(c, s, side)) return new(false, "Reinforce unavailable: check reserves, bays, Launch Tubes and the order card.", s);
        return new(true, "Reinforce: card and Play spent. Choose placement; launching units does not activate their carrier.", s with
        {
            Sequence = s.Sequence + 1, Reinforce = new(side, cmd.Card, cmd.Unit, cmd.Target), Orders0 = s.Orders0 + (side == 0 ? 1 : 0), Orders1 = s.Orders1 + (side == 1 ? 1 : 0),
            Cards = cards with { Hand0 = cards.Hand0.Where(id => id != cmd.Card).ToArray(), Hand1 = cards.Hand1.Where(id => id != cmd.Card).ToArray(), Discard = cards.Discard.Append(cmd.Card).ToArray() }
        });
    }
    private static CommandResult Finish(ContentCatalog c, BattleState before, BattleState s, string message)
    {
        int side = s.Reinforce!.Side;
        var result = SkirmishRules.FinishOrder(c, before, s with { Reinforce = null }, side, message + " Reinforce complete.");
        return result with { State = result.State.Round != before.Round && !result.State.Finished ? BasicCardRules.NextRound(c, result.State) : result.State };
    }
    internal static CommandResult Resume(ContentCatalog c, BattleState before, BattleState s, string message)
    {
        var frame = s.Reinforce!;
        if (s.Finished || frame.Mode is "deploy" or "build" || !s.Units.Any(u => u.Id == frame.Source)) return Finish(c, before, s, message);
        return new(true, message + " Continue reinforcing or finish the order.", s with { Sequence = before.Sequence + 1 });
    }
    public static CommandResult Apply(ContentCatalog c, BattleState s, int side, BattleCommand cmd)
    {
        var frame = s.Reinforce!; CommandResult Reject(string why) => new(false, why, s);
        if (frame.Side != side) return Reject("The order's owner places the reinforcements.");
        if (cmd.Kind == "finish-reinforce") return Finish(c, s, s, "Remaining reinforcements skipped.");
        var parent = s.Units.Find(u => u.Id == frame.Source); var next = s; BattleUnit? arriving;
        var destination = new Hex(cmd.Q, cmd.R);
        if (cmd.Facing is < 0 or > 5) return Reject("Invalid reinforcement facing.");
        if (frame.Mode == "deploy" && cmd.Kind == "deploy")
        {
            arriving = s.Reserves?.Find(u => u.Id == frame.Source && u.Embarked == "" && !u.Activated);
            if (arriving == null || !DeployCells(c, s, side).Contains(destination)) return Reject("Choose a free hex in your deployment zone.");
            next = s with { Reserves = s.Reserves!.Where(u => u.Id != arriving.Id).ToList() };
        }
        else if (frame.Mode == "launch" && cmd.Kind == "launch")
        {
            arriving = Children(s, frame.Source).FirstOrDefault(u => u.Id == cmd.Unit && !u.Activated);
            if (arriving == null || parent == null || !CanLaunch(c, s, parent) || !Adjacent(c, s, parent).Contains(destination)) return Reject("Requires a ready embarked unit, Launch Tubes and a free adjacent hex.");
            next = s with { Reserves = s.Reserves!.Where(u => u.Id != arriving.Id).ToList() };
        }
        else if (frame.Mode == "build" && cmd.Kind == "build")
        {
            if (parent == null || !Buildable(c, s, parent).Contains(cmd.Definition)) return Reject("This factory cannot produce the selected ship type.");
            var ship = c.Ships[cmd.Definition]; int funds = side == 0 ? s.Funds0 : s.Funds1;
            if (cmd.ReserveSpend < 0 || cmd.ReserveSpend > ship.Stars || cmd.ReserveSpend > funds || ship.Stars - cmd.ReserveSpend > parent.Cargo) return Reject("Insufficient resources or invalid payment from reserves.");
            if (cmd.ToBay ? !FleetRuleTools.Strike(ship) || Children(s, parent.Id).Length >= c.Ships[parent.Definition].Bays
                : !CanLaunch(c, s, parent) || !Adjacent(c, s, parent).Contains(destination)) return Reject("No space for a new unit, or the Launch Tubes limit is exhausted.");
            int firstFree = Math.Max(s.NextUnit, c.Maps[s.Map].Fleets.Sum(id => c.Fleets[id].Units.Length));
            while (s.Units.Concat(s.Reserves ?? []).Any(u => u.Id == $"s{side}-{firstFree}")) firstFree++;
            arriving = new($"s{side}-{firstFree}", ship.Id, side, -1, -1, cmd.Facing, ship.Models, Activated: true);
            next = s with { NextUnit = firstFree + 1, Funds0 = s.Funds0 - (side == 0 ? cmd.ReserveSpend : 0), Funds1 = s.Funds1 - (side == 1 ? cmd.ReserveSpend : 0),
                Units = s.Units.Select(u => u.Id == parent.Id ? u with { Cargo = u.Cargo - ship.Stars + cmd.ReserveSpend } : u).ToList() };
            if (cmd.ToBay) return Finish(c, s, next with { Reserves = (next.Reserves ?? []).Append(arriving with { Embarked = parent.Id }).ToList() }, $"Unit built: {FleetPresentation.ShipName(ship)}. Placed in a bay and counts as activated.");
        }
        else return Reject("Choose an action for the current Reinforce order.");
        if (!FleetFootprint.CanPlace(c, next, arriving, destination, cmd.Facing)) return Reject("There must be space for the ship's entire hull.");
        if (frame.Mode == "deploy" && FleetFootprint.Cells(c.Ships[arriving.Definition], destination, cmd.Facing).Any(h => !DeployCells(c, s, side).Contains(h))) return Reject("The entire ship must fit inside the deployment zone.");
        arriving = FleetRuleTools.NewOrder(arriving) with { Q = cmd.Q, R = cmd.R, Facing = cmd.Facing, Embarked = "", Activated = true };
        next = next with { Units = next.Units.Append(arriving).Select(u => parent != null && u.Id == parent.Id ? u with { Launches = u.Launches + 1 } : u).ToList() };
        next = FleetRuleTools.DamageControl(c, next, arriving.Id, cmd.Repair, out var repair);
        return FleetTerrain.Activation(c, s, next, [arriving.Id], "reinforce", $"Unit placed: {FleetPresentation.UnitName(c, next, arriving)}. " + repair);
    }
    public static bool CanEmbark(ContentCatalog c, BattleState s, BattleUnit unit, BattleUnit parent) => FleetRuleTools.Strike(c.Ships[unit.Definition]) && unit.Id != parent.Id
        && !unit.Hulk && unit.Patrol == "" && !parent.Hulk && parent.Patrol == "" && unit.Side == parent.Side && Children(s, unit.Id).Length == 0 && FleetCap.Patrol(s, unit.Id) == null
        && Children(s, parent.Id).Length < c.Ships[parent.Definition].Bays;
    public static BattleUnit[] EmbarkHosts(ContentCatalog c, BattleState s, int side, string id)
    {
        var unit = s.Units.Find(u => u.Id == id && u.Side == side); var order = s.Cards?.Pending;
        if (!FleetRuleTools.Enabled(c, s) || s.Finished || unit == null || side != s.ActiveSide || s.Cards is not { Phase: "orders", Response: null } cards || s.Reinforce != null || s.Hazard != null || s.Combat != null || s.Defense != null || s.CapEntry != null || s.Repairs is { Length: > 0 }) return [];
        if (order == null ? unit.Activated || (side == 0 ? cards.Hand0 : cards.Hand1).Length == 0 || (side == 0 ? s.Orders0 : s.Orders1) >= FleetRuleTools.PlayLimit(c, s, side)
            : order.Side != side || SpecialOrders.Actor(c, s, order) != id || SpecialOrders.Action(c, s, order) is not ("move" or "lightning")) return [];
        var geometry = order == null ? c : SpecialOrders.Geometry(c, s, order);
        return s.Units.Where(parent => CanEmbark(c, s, unit, parent)).Where(parent =>
        {
            var goal = new Hex(parent.Q, parent.R);
            if (order != null && SpecialOrders.Action(c, s, order) == "lightning" && (FleetRuleTools.Has(unit, "engines") || SpecialOrders.Lightning(c.Ships[unit.Definition]) == 0 || SpecialOrders.AdjacentOnLightning(c, s, order) && !s.Units.Any(first => first.Id == order.Units[0] && goal.Distance(new(first.Q, first.R)) == 1))) return false;
            bool straight = order != null && SpecialOrders.StraightCourse(c, s, order);
            if (straight && order!.Direction >= 0 && SpecialOrders.StraightDirection(new(unit.Q, unit.R), goal) != order.Direction) return false;
            return (straight ? FleetMovement.StraightPath(geometry, s, unit, goal, parent.Id) : FleetMovement.Path(geometry, s, unit, goal, embark: parent.Id)) != null;
        }).ToArray();
    }
    internal static BattleState Embark(BattleState s, BattleUnit unit, string parent) => s with { Units = s.Units.Where(u => u.Id != unit.Id).ToList(),
        Reserves = (s.Reserves ?? []).Append(unit with { Q = -1, R = -1, Embarked = parent, Patrol = "", Activated = true }).ToList() };
    internal static void ValidatePlacements(ContentCatalog c, FleetDefinition fleet, bool tabletop)
    {
        for (int i = 0; i < fleet.Units.Length; i++)
        {
            var u = fleet.Units[i];
            if (u.EmbarkedOn < -1) throw new InvalidDataException("Invalid bay assignment index.");
            if (u.Reserve || u.EmbarkedOn >= 0)
            {
                if (!tabletop || u.EmbarkedOn < -1 || u.EmbarkedOn >= fleet.Units.Length || u.EmbarkedOn == i || u.EmbarkedOn < 0 && FleetRuleTools.Class(c.Ships[u.Ship]) is not ("frigate" or "capital")) throw new InvalidDataException("Invalid reserve placement.");
                if (u.EmbarkedOn >= 0 && (!FleetRuleTools.Strike(c.Ships[u.Ship]) || fleet.Units[u.EmbarkedOn].EmbarkedOn >= 0 || fleet.Units.Count(p => p.EmbarkedOn == u.EmbarkedOn) > c.Ships[fleet.Units[u.EmbarkedOn].Ship].Bays)) throw new InvalidDataException("Invalid embarked assignment.");
            }
        }
    }
    internal static void Validate(ContentCatalog c, BattleState s)
    {
        var offboard = s.Reserves ?? []; var all = s.Units.Concat(offboard).ToArray();
        if (s.Funds0 is < 0 or > 1000000 || s.Funds1 is < 0 or > 1000000 || s.NextUnit < 0 || all.Select(u => u.Id).Distinct().Count() != all.Length) throw new InvalidDataException("Invalid reinforcement state.");
        foreach (var u in offboard)
            if (!FleetRuleTools.Enabled(c, s) || !c.Ships.TryGetValue(u.Definition, out var ship) || u.Side is < 0 or > 1 || u.Q != -1 || u.R != -1 || u.Facing is < 0 or > 5 || u.Models < 1 || u.Models > ship.Models
                || u.Hulk != FleetClose.CheckHulk(c, u).Hulk || u.Leader == null || u.LastTarget == null || u.LastActionRound < 0 || u.LastActionRound > s.Round || u.LastAction is not ("" or "move" or "ranged" or "ram" or "board") || u.FocusFire < 0 || u.CrewDamage < 0 || u.Cargo is < 0 or > 1000000 || u.Embarked == null || u.Patrol != "" || u.Launches < 0 || !FleetRuleTools.ValidEffects(u.Effects) || !FleetRuleTools.ValidLeader(c, u)
                || u.Embarked == "" && FleetRuleTools.Class(ship) is not ("frigate" or "capital")
                || u.Embarked != "" && (!FleetRuleTools.Strike(ship) || !all.Any(p => p.Id == u.Embarked && p.Embarked == "" && p.Side == u.Side) || Children(s, u.Embarked).Length > c.Ships[all.Single(p => p.Id == u.Embarked).Definition].Bays))
                throw new InvalidDataException("Invalid off-board unit.");
        if (s.Reinforce is not { } frame) return;
        if (!FleetRuleTools.Enabled(c, s) || s.Finished || frame.Side != s.ActiveSide || s.Cards is not { Phase: "orders", Pending: null, Response: null } cards || !cards.Discard.Contains(frame.Card)
            || (frame.Side == 0 ? s.Orders0 : s.Orders1) < 1 || frame.Mode is not ("deploy" or "launch" or "build") || s.Overrun != null || s.CapEntry != null
            || !all.Any(u => u.Id == frame.Source && u.Side == frame.Side) && !(frame.Mode == "deploy" && s.Hazard?.Job.Unit == frame.Source)) throw new InvalidDataException("Invalid pending Reinforce.");
        var source = all.Single(u => u.Id == frame.Source);
        if (frame.Mode == "deploy" ? source.Embarked != "" || FleetRuleTools.Class(c.Ships[source.Definition]) is not ("frigate" or "capital") || s.Hazard == null && s.Repairs?.Any(p => p.Unit == source.Id) != true && !offboard.Contains(source)
            : !s.Units.Contains(source) || source.Hulk || source.Patrol != "" || (frame.Mode == "build" ? (c.Ships[source.Definition].Factory ?? []).Length == 0 : c.Ships[source.Definition].Bays == 0)) throw new InvalidDataException("Invalid reinforcement source.");
    }
}
