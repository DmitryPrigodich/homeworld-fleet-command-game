namespace FleetCommand.Core;

public static class FleetWeapons
{
    public static WeaponDefinition[] Weapons(ContentCatalog c, ShipDefinition ship) => ship.Weapons ?? (ship.DicePerModel == 0 ? [] :
        [new("main", "Основное оружие", ship.DicePerModel, ship.Range,
            Penetrating: (ship.Abilities ?? []).Select(id => c.Abilities[id]).Where(a => a.Handler == "penetrating").Sum(a => a.Value),
            RapidFire: (ship.Abilities ?? []).Select(id => c.Abilities[id]).Where(a => a.Handler == "rapid-fire").Sum(a => a.Value),
            RapidTargets: ship.Id is "core:kushan-interceptor" or "core:taiidan-interceptor" ? ["fighter", "corvette", "platform"] : null)]);
    public static bool Arc(BattleUnit attacker, BattleUnit target, string arc)
    {
        var start = new Hex(attacker.Q, attacker.R); var end = new Hex(target.Q, target.R);
        if (arc == "all") return true;
        if (arc == "spinal") return start.Neighbor(attacker.Facing).Distance(end) == start.Distance(end) - 1;
        int q = end.Q - start.Q, r = end.R - start.R; var f = Hex.Directions[attacker.Facing];
        return 2 * q * f.Q + q * f.R + r * f.Q + 2 * r * f.R >= 0;
    }
    public static (bool Allowed, int Cover, string Error) Attack(ContentCatalog c, BattleState s, BattleUnit unit, BattleUnit? target, string weapon = "")
    {
        if (target == null || target.Side == unit.Side) return (false, 0, "Выберите противника.");
        if (FleetRuleTools.Has(unit, "weapons")) return (false, 0, "Оружие выведено из строя.");
        var choices = Weapons(c, c.Ships[unit.Definition]).Where(w => (weapon == "" || w.Id == weapon)
            && ((w.Targets?.Length ?? 0) == 0 || w.Targets!.Contains(FleetRuleTools.Class(c.Ships[target.Definition]))));
        foreach (var w in choices)
        {
            foreach (var pair in FleetFootprint.Nearest(c, unit, target))
            {
                if (!Arc(unit with { Q = pair.From.Q, R = pair.From.R }, target with { Q = pair.To.Q, R = pair.To.R }, w.Arc)) continue;
                var geometry = SkirmishGeometry.CoverBetween(c, s, unit, target, pair.From, pair.To, w.Range, w.Barrage > 0);
                if (geometry.Allowed) return geometry;
            }
        }
        return (false, 0, "Нет подходящего оружия: дальность, сектор, тип цели или линия огня.");
    }
    public static WeaponDefinition? Select(ContentCatalog c, BattleState s, BattleUnit u, BattleUnit target, string id) =>
        Weapons(c, c.Ships[u.Definition]).FirstOrDefault(w => (id == "" || w.Id == id) && Attack(c, s, u, target, w.Id).Allowed);
}
