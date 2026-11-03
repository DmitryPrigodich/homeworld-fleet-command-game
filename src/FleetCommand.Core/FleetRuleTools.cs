namespace FleetCommand.Core;

public static class FleetRuleTools
{
    public static readonly string[] Classes = ["fighter", "corvette", "platform", "frigate", "capital", "station"];
    public static readonly string[] Buffs = ["brace", "evasive", "lockdown"];
    public static readonly string[] Debuffs = ["engines", "weapons", "bays"];
    public static bool Enabled(ContentCatalog c, BattleState s) => c.Rules[c.Maps[s.Map].Rules].Tabletop;
    public static string Faction(ContentCatalog c, BattleState s, int side) => c.Fleets[c.Maps[s.Map].Fleets[side]].Units.Select(u => c.Ships[u.Ship].Faction).FirstOrDefault() ?? "";
    public static string Class(ShipDefinition ship) => ship.Class != "" ? ship.Class : FleetPresentation.ShipIcon(ship) switch
    { "triangle" => "fighter", "square" => "corvette", "parallelogram" => "platform", "hexagon" => "capital", "circle" => "station", _ => "frigate" };
    public static bool Strike(ShipDefinition ship) => Class(ship) is "fighter" or "corvette" or "platform";
    public static bool Has(BattleUnit unit, string effect) => (unit.Effects ?? []).Contains(effect);
    public static bool ValidEffects(string[]? effects) => effects == null || effects.Distinct().Count() == effects.Length && effects.All(e => Buffs.Contains(e) || Debuffs.Contains(e));
    public static string[] LeaderIds(BattleUnit unit) => (unit.Leader == "" ? Array.Empty<string>() : new[] { unit.Leader }).Concat(unit.AdditionalLeaders ?? []).ToArray();
    public static IEnumerable<LeaderDefinition> AssignedLeaders(ContentCatalog c, BattleUnit unit) => LeaderIds(unit).Select(id => c.Leaders[id]);
    public static BattleUnit WithLeaders(BattleUnit unit, IEnumerable<string> leaders)
    {
        var ids = leaders.ToArray(); return unit with { Leader = ids.FirstOrDefault() ?? "", AdditionalLeaders = ids.Length <= 1 ? null : ids.Skip(1).ToArray() };
    }
    public static bool ValidLeader(ContentCatalog c, BattleUnit unit)
    {
        if (unit.Leader == null || (unit.AdditionalLeaders ?? []).Any(id => string.IsNullOrWhiteSpace(id))) return false;
        var ids = LeaderIds(unit);
        return ids.Distinct().Count() == ids.Length && c.Ships.TryGetValue(unit.Definition, out var ship)
            && ids.All(id => c.Leaders.TryGetValue(id, out var leader) && leader.Faction == ship.Faction && leader.Classes.Contains(Class(ship)));
    }
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
    public static BattleState DamageControl(ContentCatalog c, BattleState s, string id, string choice, out string message, bool advance = false)
    {
        message = ""; var unit = s.Units.Single(u => u.Id == id);
        if (unit.Hulk || choice == "" || choice != "ask" && choice != "focus" && !Debuffs.Contains(choice)) return s;
        if (choice == "ask" && unit.FocusFire == 0 && !(unit.Effects ?? []).Any(Debuffs.Contains)) return s;
        var ship = c.Ships[unit.Definition]; uint random = s.RandomState;
        int dice = 1 + AssignedLeaders(c, unit).Sum(l => l.CoolUnderPressure);
        var rolls = new int[dice]; for (int i = 0; i < dice; i++) rolls[i] = Roll(ref random);
        int successes = rolls.Count(d => d >= ship.Success);
        if (choice == "ask")
        {
            message = $"Damage Control [{string.Join(", ", rolls)}]: успехов {successes}. ";
            return s with { RandomState = random, Repairs = successes == 0 ? s.Repairs : (s.Repairs ?? []).Append(new(id, unit.Side, successes, rolls, advance)).ToArray() };
        }
        unit = choice == "focus" ? unit with { FocusFire = Math.Max(0, unit.FocusFire - successes) }
            : successes > 0 ? unit with { Effects = (unit.Effects ?? []).Where(e => e != choice).ToArray() } : unit;
        message = $"Damage Control [{string.Join(", ", rolls)}]: успехов {successes}. ";
        return s with { RandomState = random, Units = s.Units.Select(u => u.Id == id ? unit : u).ToList() };
    }
    public static CommandResult FactionRepair(ContentCatalog c, BattleState s, int side, BattleCommand command)
    {
        var unit = s.Units.Find(u => u.Id == command.Unit && u.Side == side);
        if (!Enabled(c, s) || s.ActiveSide != side || s.Cards is not { Phase: "orders", Pending: null, Response: null }
            || (side == 0 ? s.FactionUsed0 : s.FactionUsed1) || unit == null || Faction(c, s, side) != "kushan"
            || (command.Repair == "focus" ? unit.FocusFire == 0 : !Debuffs.Contains(command.Repair) || !Has(unit, command.Repair)))
            return new(false, "Способность фракции сейчас недоступна.", s);
        // Focus is stored as a total and represented canonically with double-sided 2/1 tokens.
        // Errata 1.7 removes the whole selected token, including its -2 face.
        unit = command.Repair == "focus" ? unit with { FocusFire = Math.Max(0, unit.FocusFire - 2) } : unit with { Effects = (unit.Effects ?? []).Where(e => e != command.Repair).ToArray() };
        return new(true, "Kushan: Redundant Systems — эффект снят.", s with { Sequence = s.Sequence + 1,
            FactionUsed0 = s.FactionUsed0 || side == 0, FactionUsed1 = s.FactionUsed1 || side == 1, Units = s.Units.Select(u => u.Id == unit.Id ? unit : u).ToList() });
    }
}
