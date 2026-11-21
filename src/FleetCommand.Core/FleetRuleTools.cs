namespace FleetCommand.Core;

public static class FleetRuleTools
{
    public static readonly string[] Classes = ["fighter", "corvette", "platform", "frigate", "capital", "station", "planet"];
    public static readonly string[] DamageBuffs = ["brace", "evasive", "lockdown"];
    public static readonly string[] Buffs = [.. DamageBuffs, "hero-evasive"];
    public static readonly string[] Debuffs = ["engines", "weapons", "bays"];
    public static bool Enabled(ContentCatalog c, BattleState s) => c.Rules[c.Maps[s.Map].Rules].Tabletop;
    public static string Faction(ContentCatalog c, BattleState s, int side) => c.Fleets[c.Maps[s.Map].Fleets[side]].Units.Select(u => c.Ships[u.Ship].Faction).FirstOrDefault() ?? "";
    public static string Class(ShipDefinition ship) => ship.Class != "" ? ship.Class : FleetPresentation.ShipIcon(ship) switch
    { "triangle" => "fighter", "square" => "corvette", "parallelogram" => "platform", "hexagon" => "capital", "circle" => "station", _ => "frigate" };
    public static bool Strike(ShipDefinition ship) => Class(ship) is "fighter" or "corvette" or "platform";
    public static bool Has(BattleUnit unit, string effect) => (unit.Effects ?? []).Contains(effect);
    public static bool ValidEffects(string[]? effects) => effects == null || effects.Distinct().Count() == effects.Length && effects.All(e => Buffs.Contains(e) || Debuffs.Contains(e));
    public static string[] LeaderIds(BattleUnit unit) => (unit.Leader == "" ? Array.Empty<string>() : new[] { unit.Leader }).Concat(unit.AdditionalLeaders ?? []).Concat((unit.FacilityLeaders ?? []).Values).ToArray();
    public static IEnumerable<LeaderDefinition> AssignedLeaders(ContentCatalog c, BattleUnit unit) => LeaderIds(unit).Select(id => c.Leaders[id]);
    public static BattleUnit WithLeaders(BattleUnit unit, IEnumerable<string> leaders)
    {
        var ids = leaders.ToArray(); return unit with { Leader = ids.FirstOrDefault() ?? "", AdditionalLeaders = ids.Length <= 1 ? null : ids.Skip(1).ToArray() };
    }
    public static bool CanLead(LeaderDefinition leader, ShipDefinition ship) => leader.Classes.Contains(Class(ship)) && (!leader.CarrierOnly || ship.Bays > 0 && ship.LaunchTubes > 0);
    public static bool ValidLeader(ContentCatalog c, BattleUnit unit)
    {
        if (unit.Leader == null || (unit.AdditionalLeaders ?? []).Any(id => string.IsNullOrWhiteSpace(id))) return false;
        if (c.Ships.TryGetValue(unit.Definition, out var planetary) && Class(planetary) == "planet") return FleetPlanets.ValidLeaders(c, unit);
        var ids = LeaderIds(unit);
        return ids.Distinct().Count() == ids.Length && c.Ships.TryGetValue(unit.Definition, out var ship)
            && ids.All(id => c.Leaders.TryGetValue(id, out var leader) && leader.Faction == ship.Faction && CanLead(leader, ship))
            && (!ids.Any(id => c.Leaders[id].Staff) || ids.Any(id => !c.Leaders[id].Staff))
            && (!ids.Any(id => c.Leaders[id].HeroicCommand != "") || ids.Count(id => !c.Leaders[id].Staff) <= 1);
    }
    public static int Evasion(ContentCatalog c, BattleUnit unit) => c.Ships[unit.Definition].Evasive + AssignedLeaders(c, unit).Sum(l => l.Evasive)
        + (Has(unit, "evasive") ? 1 : 0) + (Has(unit, "hero-evasive") ? 1 : 0);
    public static int DefensiveAura(ContentCatalog c, BattleState s, BattleUnit unit) => s.Units.Where(u => u.Side == unit.Side && !u.Hulk && FleetFootprint.Distance(c, u, unit) <= 1).SelectMany(u => AssignedLeaders(c, u)).Sum(l => l.DefensiveAura);
    public static BattleUnit NewOrder(BattleUnit u) => u with { Effects = (u.Effects ?? []).Except(Buffs).ToArray() };
    public static int PlayLimit(ContentCatalog c, BattleState s, int side) => Enabled(c, s) && s.Cards != null
        ? side == 0 ? s.Cards.PlayLimit0 : s.Cards.PlayLimit1 : c.Rules[c.Maps[s.Map].Rules].OrdersPerSide;
    public static CardRound Budgets(ContentCatalog c, BattleState s)
    {
        var cards = s.Cards!;
        if (!Enabled(c, s)) return cards;
        int Sum(int side, bool hand) => s.Units.Where(u => u.Side == side).SelectMany(u => AssignedLeaders(c, u)).Sum(l => hand ? l.Hand : l.Play);
        return cards with { HandLimit0 = 2 + Sum(0, true), HandLimit1 = 2 + Sum(1, true), PlayLimit0 = 1 + Sum(0, false), PlayLimit1 = 1 + Sum(1, false) };
    }
    public static int Roll(ref uint random) { random ^= random << 13; random ^= random >> 17; random ^= random << 5; return (int)(random % 6) + 1; }
    public static int AttackAura(ContentCatalog c, BattleState s, BattleUnit unit, BattleUnit target) => s.Units.Where(u => u.Side == unit.Side && !u.Hulk)
        .SelectMany(u => AssignedLeaders(c, u).Where(l => l.AttackAura > 0 && FleetFootprint.Distance(c, u, unit) <= l.AttackAuraRange
            && (l.AttackAuraUnits == null || l.AttackAuraUnits.Contains(Class(c.Ships[unit.Definition])))
            && (l.AttackAuraTargets == null || l.AttackAuraTargets.Contains(Class(c.Ships[target.Definition]))))).Sum(l => l.AttackAura);
    internal static BattleState Medic(ContentCatalog c, BattleState s, string id, out string message)
    {
        message = ""; var unit = s.Units.Single(u => u.Id == id); if (unit.Hulk || unit.CrewDamage == 0) return s;
        var medics = AssignedLeaders(c, unit).Where(l => l.Medic > 0).ToArray(); if (medics.Length == 0) return s;
        uint random = s.RandomState; var rolls = new List<int>(); int removed = 0;
        foreach (var medic in medics) for (int token = 0; token < unit.CrewDamage; token++) { int roll = Roll(ref random); rolls.Add(roll); if (roll >= medic.Medic) removed++; }
        removed = Math.Min(removed, unit.CrewDamage); message = $"Medic [{string.Join(", ", rolls)}]: Crew Damage removed {removed}. ";
        return s with { RandomState = random, Units = s.Units.Select(u => u.Id == id ? u with { CrewDamage = u.CrewDamage - removed } : u).ToList() };
    }
    public static BattleState ActivationControl(ContentCatalog c, BattleState s, string id, string choice, out string message)
    {
        s = Medic(c, s, id, out var medic); s = DamageControl(c, s, id, choice, out var repair); message = medic + repair; return s;
    }
    public static BattleState DamageControl(ContentCatalog c, BattleState s, string id, string choice, out string message, bool advance = false)
    {
        message = ""; var unit = s.Units.Single(u => u.Id == id);
        if (unit.Hulk || choice == "" || choice != "ask" && choice != "focus" && !Debuffs.Contains(choice)) return s;
        if (choice == "ask" && unit.FocusFire == 0 && !(unit.Effects ?? []).Any(Debuffs.Contains) && !FleetPlanets.Crippled(unit, unit.ActiveFacility)) return s;
        var ship = c.Ships[unit.Definition]; uint random = s.RandomState;
        int dice = 1 + FleetPlanets.ActivationLeaders(c, unit).Sum(l => l.CoolUnderPressure);
        var rolls = new int[dice]; for (int i = 0; i < dice; i++) rolls[i] = Roll(ref random);
        int successes = rolls.Count(d => d >= ship.Success);
        if (choice == "ask")
        {
            message = $"Damage Control [{string.Join(", ", rolls)}]: successes {successes}. ";
            return s with { RandomState = random, Repairs = successes == 0 ? s.Repairs : (s.Repairs ?? []).Append(new(id, unit.Side, successes, rolls, advance)).ToArray() };
        }
        if (choice == "weapons" && successes > 0 && FleetPlanets.IsPlanet(c, unit)) unit = FleetPlanets.RepairWeapons(unit);
        unit = choice == "focus" ? unit with { FocusFire = Math.Max(0, unit.FocusFire - successes) }
            : successes > 0 ? unit with { Effects = (unit.Effects ?? []).Where(e => e != choice).ToArray() } : unit;
        message = $"Damage Control [{string.Join(", ", rolls)}]: successes {successes}. ";
        return s with { RandomState = random, Units = s.Units.Select(u => u.Id == id ? unit : u).ToList() };
    }
    public static CommandResult FactionRepair(ContentCatalog c, BattleState s, int side, BattleCommand command)
    {
        var unit = s.Units.Find(u => u.Id == command.Unit && u.Side == side);
        if (!Enabled(c, s) || s.ActiveSide != side || s.Cards is not { Phase: "orders", Pending: null, Response: null }
            || (side == 0 ? s.FactionUsed0 : s.FactionUsed1) || unit == null || Faction(c, s, side) != "kushan"
            || (command.Repair == "focus" ? unit.FocusFire == 0 : !Debuffs.Contains(command.Repair) || !Has(unit, command.Repair)))
            return new(false, "Faction ability unavailable right now.", s);
        // Focus is stored as a total and represented canonically with double-sided 2/1 tokens.
        // Errata 1.7 removes the whole selected token, including its -2 face.
        unit = command.Repair == "focus" ? unit with { FocusFire = Math.Max(0, unit.FocusFire - 2) } : unit with { Effects = (unit.Effects ?? []).Where(e => e != command.Repair).ToArray() };
        return new(true, "Kushan: Redundant Systems — effect removed.", s with { Sequence = s.Sequence + 1,
            FactionUsed0 = s.FactionUsed0 || side == 0, FactionUsed1 = s.FactionUsed1 || side == 1, Units = s.Units.Select(u => u.Id == unit.Id ? unit : u).ToList() });
    }
}
