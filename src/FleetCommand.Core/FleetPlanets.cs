namespace FleetCommand.Core;

public sealed record PendingPlanetDamage(int Side, string Target, int Remaining, int Initial, string Cripple = "", int CollisionArmor = 0);

// A military planet is one target. Its physical map token survives loss of the unit.
public static class FleetPlanets
{
    public static bool IsPlanet(ContentCatalog c, BattleUnit unit) => FleetRuleTools.Class(c.Ships[unit.Definition]) == "planet";
    public static PlanetFacilityDefinition[] Live(ContentCatalog c, BattleUnit unit) => (c.Ships[unit.Definition].Facilities ?? []).Where(f => !(unit.DestroyedFacilities ?? []).Contains(f.Id)).ToArray();
    public static string FacilityId(string weapon) => weapon.Contains('/') ? weapon[..weapon.IndexOf('/')] : "";
    public static WeaponDefinition[] FacilityWeapons(ContentCatalog c, BattleUnit unit) => Live(c, unit).SelectMany(f => (f.Weapons ?? []).Select(w => w with { Id = f.Id + "/" + w.Id, Name = f.Name + ": " + w.Name })).ToArray();
    public static bool Crippled(BattleUnit unit, string facility) => unit.FacilityEffects?.GetValueOrDefault(facility)?.Contains("weapons") == true;
    public static BattleUnit RepairWeapons(BattleUnit unit) { var effects = new Dictionary<string, string[]>(unit.FacilityEffects ?? []); effects.Remove(unit.ActiveFacility); return unit with { FacilityEffects = effects }; }
    public static int Armor(ContentCatalog c, BattleUnit unit) => Live(c, unit).Sum(f => f.Armor);
    public static IEnumerable<LeaderDefinition> CombatLeaders(ContentCatalog c, BattleUnit unit, string weapon) => !IsPlanet(c, unit) ? FleetRuleTools.AssignedLeaders(c, unit)
        : unit.FacilityLeaders?.TryGetValue(FacilityId(weapon), out string? leader) == true ? new[] { c.Leaders[leader] } : [];
    public static IEnumerable<LeaderDefinition> ActivationLeaders(ContentCatalog c, BattleUnit unit) => !IsPlanet(c, unit) ? FleetRuleTools.AssignedLeaders(c, unit) : CombatLeaders(c, unit, unit.ActiveFacility + "/");
    public static string HeroWeapon(ContentCatalog c, BattleUnit unit, string leader) => (unit.FacilityLeaders ?? []).FirstOrDefault(p => p.Value == leader) is { Key: not null } binding
        ? FacilityWeapons(c, unit).FirstOrDefault(w => FacilityId(w.Id) == binding.Key && !Crippled(unit, binding.Key))?.Id ?? "" : "";
    public static bool CanUseWeapon(ContentCatalog c, BattleState s, BattleUnit unit, string weapon)
    {
        if (!IsPlanet(c, unit)) return true;
        string facility = FacilityId(weapon); if (facility == "" || Crippled(unit, facility)) return false;
        if (s.Cards?.Pending is { } order && SpecialOrders.Actor(c, s, order) == unit.Id && order.Weapon != "") return order.Weapon == weapon;
        bool reaction = s.Cards?.Response is { } response && response.Side != unit.Side;
        return reaction || unit.ActiveFacility != "" ? reaction || unit.ActiveFacility == facility : !(unit.ActivatedFacilities ?? []).Contains(facility);
    }
    public static string ActivationWeapon(ContentCatalog c, BattleState s, BattleUnit unit, string weapon) => !IsPlanet(c, unit) ? weapon
        : FacilityWeapons(c, unit).FirstOrDefault(w => !(unit.ActivatedFacilities ?? []).Contains(FacilityId(w.Id)) && (weapon == "" || w.Id == weapon))?.Id ?? "";
    public static bool CanActivate(ContentCatalog c, BattleState s, BattleUnit unit, string weapon) => !IsPlanet(c, unit) || ActivationWeapon(c, s, unit, weapon) != "";
    public static BattleState Activate(ContentCatalog c, BattleState s, string id, string weapon)
    {
        var unit = s.Units.Single(u => u.Id == id); if (!IsPlanet(c, unit)) return s;
        string facility = FacilityId(ActivationWeapon(c, s, unit, weapon));
        return s with { Units = s.Units.Select(u => u.Id == id ? u with { ActiveFacility = facility, Activated = true, ActivatedFacilities = (u.ActivatedFacilities ?? []).Append(facility).Distinct().ToArray() } : u).ToList() };
    }
    public static BattleState EndOrder(ContentCatalog c, BattleState s) => s with { Units = s.Units.Select(u => !IsPlanet(c, u) ? u : u with { ActiveFacility = "", Activated = !Live(c, u).Any(f => (f.Weapons ?? []).Length > 0 && !(u.ActivatedFacilities ?? []).Contains(f.Id)) }).ToList() };
    public static bool ValidLeaders(ContentCatalog c, BattleUnit unit) => unit.Leader == "" && (unit.AdditionalLeaders ?? []).Length == 0
        && (unit.FacilityLeaders ?? []).Values.Distinct().Count() == (unit.FacilityLeaders?.Count ?? 0)
        && (unit.FacilityLeaders ?? []).All(p => Live(c, unit).Any(f => f.Id == p.Key) && c.Leaders.TryGetValue(p.Value, out var l) && !l.Staff && l.Faction == c.Ships[unit.Definition].Faction && l.Classes.Contains("planet"));

    public static void ValidateDefinitions(ContentCatalog c)
    {
        foreach (var ship in c.Ships.Values)
        {
            var facilities = ship.Facilities ?? [];
            if (FleetRuleTools.Class(ship) != "planet" && facilities.Length != 0 || FleetRuleTools.Class(ship) == "planet" && (ship.Move != 0 || ship.Models != 1 || ship.Footprint != 1 || ship.Afterburners > 0 || ship.Ram != 0 || ship.Crew != 0)
                || facilities.Length > 32 || facilities.Any(f => f == null) || facilities.Select(f => f.Id).Distinct().Count() != facilities.Length
                || facilities.Any(f => f == null || string.IsNullOrWhiteSpace(f.Id) || f.Id.Length > 32 || f.Id.Any(ch => !char.IsAsciiLetterOrDigit(ch) && ch != '-') || string.IsNullOrWhiteSpace(f.Name) || f.Armor is < 0 or > 20 || f.Stars is < 0 or > 100
                    || (f.Weapons ?? []).Length > 8 || (f.Weapons ?? []).Any(w => w == null) || (f.Weapons ?? []).Select(w => w.Id).Distinct().Count() != (f.Weapons ?? []).Length
                    || (f.Weapons ?? []).Any(w => w == null || string.IsNullOrWhiteSpace(w.Id) || w.Id.Contains('/') || w.Id.Length > 32 || w.DicePerModel is < 1 or > 50 || w.Range is < 1 or > 50 || w.Penetrating < 0 || w.PowerPenetrating < 0 || w.PowerDice < 0 || w.Kind is not ("gun" or "missile") || w.Arc != "all" || w.DefenseOnly || w.Barrage != 0 || (w.Targets ?? []).Any(t => !FleetRuleTools.Classes.Contains(t)))))
                throw new InvalidDataException("Invalid planet/facility profile: " + ship.Id);
        }
    }
    public static void Validate(ContentCatalog c, BattleState s)
    {
        foreach (var u in s.Units.Concat(s.Reserves ?? []))
        {
            bool planet = IsPlanet(c, u); var ids = (c.Ships[u.Definition].Facilities ?? []).Select(f => f.Id).ToArray();
            if (!planet && ((u.FacilityLeaders?.Count ?? 0) > 0 || (u.DestroyedFacilities ?? []).Length > 0 || (u.ActivatedFacilities ?? []).Length > 0 || u.ActiveFacility != "" || (u.FacilityEffects?.Count ?? 0) > 0)
                || planet && (u.Embarked != "" || u.Q < 0 || u.Patrol != "" || !c.Maps[s.Map].Objects.Any(o => o.Q == u.Q && o.R == u.R && c.Objects[o.Definition].BlocksMovement && (o.Definition == "core:planet" || c.Objects[o.Definition].Kind == "planet")))
                || (u.DestroyedFacilities ?? []).Distinct().Count() != (u.DestroyedFacilities ?? []).Length || (u.DestroyedFacilities ?? []).Any(id => !ids.Contains(id))
                || (u.ActivatedFacilities ?? []).Distinct().Count() != (u.ActivatedFacilities ?? []).Length || (u.ActivatedFacilities ?? []).Any(id => !ids.Contains(id))
                || u.ActiveFacility == null || u.ActiveFacility != "" && (!Live(c, u).Any(f => f.Id == u.ActiveFacility) || !(u.ActivatedFacilities ?? []).Contains(u.ActiveFacility))
                || (u.FacilityEffects ?? []).Any(p => !Live(c, u).Any(f => f.Id == p.Key) || p.Value == null || p.Value.Length != 1 || p.Value[0] != "weapons"))
                throw new InvalidDataException("Invalid planet facility state.");
        }
        if (s.PlanetDamage is not { } damage) return;
        var target = s.Units.Find(u => u.Id == damage.Target);
        if (target == null || !IsPlanet(c, target) || s.Combat == null || s.Combat.Target != damage.Target || s.Combat.Side != damage.Side || damage.Side is < 0 or > 1
            || damage.Remaining < 0 || damage.Remaining > damage.Initial || damage.Initial > 10000 || damage.CollisionArmor is < 0 or > 10000 || damage.Cripple is not ("" or "weapons") || damage.Remaining == 0 && damage.Cripple == "" || s.Finished)
            throw new InvalidDataException("Invalid planet damage decision.");
    }

    internal static CommandResult Damage(ContentCatalog c, BattleState s, PendingCombat combat, int successes, int armor)
    {
        var target = s.Units.Single(u => u.Id == combat.Target); int hits = Math.Max(0, successes - armor), ones = combat.Rolls.Count(d => d == 1);
        target = target with { FocusFire = target.FocusFire + ones, Effects = ones > 0 ? (target.Effects ?? []).Except(FleetRuleTools.DamageBuffs).ToArray() : target.Effects };
        string effect = combat.Kind == "ram" && FleetRuleTools.Class(c.Ships[FleetCombat.Actor(s, combat).Definition]) == "fighter" && successes > 0 && combat.Effect == "weapons" ? "weapons" : "";
        string orderEffect = combat.Kind == "ranged" && successes > 0 ? SpecialOrders.AttackEffect(c, s, combat.Attacker) : "";
        if (orderEffect == "weapons") effect = "weapons";
        var next = s with { Units = s.Units.Select(u => u.Id == target.Id ? target : u).ToList(), PlanetDamage = new(combat.Side, target.Id, hits, hits, effect, c.Ships[target.Definition].Armor + Armor(c, target)) };
        return ContinueDamage(c, s, next, $"Planet attack: armor {armor}, facility hits {hits}, Focus Fire +{ones}.", resume: false);
    }
    public static string[] DamageChoices(ContentCatalog c, BattleState s)
    {
        if (s.PlanetDamage is not { } d) return [];
        var unit = s.Units.Single(u => u.Id == d.Target); var live = Live(c, unit);
        if (d.Remaining == 0) return live.Where(f => (f.Weapons ?? []).Length > 0).Select(f => f.Id).ToArray();
        return (live.Any(f => f.Hardened) ? live.Where(f => f.Hardened) : live).Select(f => f.Id).ToArray();
    }
    public static CommandResult ChooseDamage(ContentCatalog c, BattleState s, int side, BattleCommand cmd)
    {
        var d = s.PlanetDamage!;
        if (side != d.Side || cmd.Kind != "planet-facility" || cmd.Unit != d.Target || !DamageChoices(c, s).Contains(cmd.Target)) return new(false, "The attacker chooses an eligible planet facility; Hardened Defences must be destroyed first.", s);
        var unit = s.Units.Single(u => u.Id == d.Target); var leaders = new Dictionary<string, string>(unit.FacilityLeaders ?? []); var effects = new Dictionary<string, string[]>(unit.FacilityEffects ?? []);
        int stars = 0;
        if (d.Remaining > 0)
        {
            var f = Live(c, unit).Single(f => f.Id == cmd.Target); stars = f.Stars + (leaders.TryGetValue(f.Id, out var leader) ? c.Leaders[leader].Stars : 0); leaders.Remove(f.Id); effects.Remove(f.Id);
            unit = unit with { DestroyedFacilities = (unit.DestroyedFacilities ?? []).Append(f.Id).ToArray(), FacilityLeaders = leaders, FacilityEffects = effects, ActiveFacility = unit.ActiveFacility == f.Id ? "" : unit.ActiveFacility, FocusFire = Math.Max(0, unit.FocusFire - 1) };
            d = d with { Remaining = d.Remaining - 1 };
        }
        else { effects[cmd.Target] = ["weapons"]; unit = unit with { FacilityEffects = effects }; d = d with { Cripple = "" }; }
        var next = s with { PlanetDamage = d, Units = s.Units.Select(u => u.Id == unit.Id ? unit : u).ToList(), Stars0 = s.Stars0 + (side == 0 ? stars : 0), Stars1 = s.Stars1 + (side == 1 ? stars : 0) };
        return ContinueDamage(c, s, next, "Planet facility selected: " + cmd.Target + ".");
    }
    private static CommandResult ContinueDamage(ContentCatalog c, BattleState before, BattleState s, string message, bool resume = true)
    {
        var d = s.PlanetDamage!; var target = s.Units.Single(u => u.Id == d.Target); var combat = s.Combat!;
        if (d.Remaining > 0 && Live(c, target).Length > 0 || d.Remaining == 0 && d.Cripple != "" && DamageChoices(c, s).Length > 0)
            return new(true, message + " Choose a planet facility.", s with { Sequence = before.Sequence + 1 });
        bool destroyed = d.Remaining > 0;
        var next = s with { PlanetDamage = null, Combat = null };
        if (destroyed) next = FleetClose.Award(c, next with { Units = next.Units.Where(u => u.Id != target.Id).ToList() }, target, d.Side);
        next = next with { Units = next.Units.Select(u => u.Id == combat.Attacker ? u with { AttackWindow = FleetClose.Window(s), LastAction = combat.Kind, LastTarget = target.Id, LastActionRound = s.Round } : u).ToList() };
        bool suicide = combat.Kind == "ram" && FleetRuleTools.Class(c.Ships[FleetCombat.Actor(s, combat).Definition]) == "fighter" && combat.Rolls.Any(r => r >= FleetCombat.Success(c, s, combat));
        if (suicide) { var attacker = next.Units.Find(u => u.Id == combat.Attacker); if (attacker != null) next = FleetClose.Award(c, next with { Units = next.Units.Where(u => u.Id != attacker.Id).ToList() }, attacker, 1 - d.Side); }
        if (FleetObjectives.Enabled(c, next)) next = FleetObjectives.Check(c, next);
        if (combat.Kind == "ram" && !suicide && !next.Finished)
        {
            var collision = FleetCombat.PlanetCollision(c, next, combat, target, d.CollisionArmor);
            return resume ? FleetCombat.Finish(c, before, collision, combat) : collision;
        }
        next = FleetClose.Outcome(next, c);
        var result = new CommandResult(true, message + (destroyed ? " Planet military presence destroyed; physical token remains." : " Planet damage resolved."), next);
        return resume ? FleetCombat.Finish(c, before, result, combat) : result;
    }
}
