namespace FleetCommand.Core;

public sealed record UnitLoss(string Definition, int Credit, int Stars);
public static class FleetObjectives
{
    public static bool Enabled(ContentCatalog c, BattleState s) => c.Maps[s.Map].Objectives is { Length: > 0 };
    public static int Progress(ContentCatalog c, BattleState s, ObjectiveDefinition objective) => objective.Kind switch
    {
        "destroy-stars" => objective.Classes is { Length: > 0 } ? (s.Losses ?? []).Values.Where(l => l.Credit == objective.Side && objective.Classes.Contains(FleetRuleTools.Class(c.Ships[l.Definition]))).Sum(l => l.Stars)
            : objective.Side == 0 ? s.Stars0 : s.Stars1,
        "destroy-count" => (s.Losses ?? []).Values.Count(l => l.Credit == objective.Side),
        "event" => s.Scenario?.Fired.Contains(objective.Target) == true ? 1 : 0,
        "harvest-stars" => objective.Side == 0 ? s.Harvested0 : s.Harvested1,
        "capture" => s.Captures?.GetValueOrDefault(objective.Target, -1) == objective.Side ? 1 : 0,
        "destroy-unit" => s.Losses?.TryGetValue(objective.Target, out var loss) == true && loss.Credit == objective.Side ? 1 : 0,
        _ => 0
    };
    public static bool Active(BattleState s, ObjectiveDefinition objective) => (s.Claims ?? []).Contains(objective.Id)
        && (!objective.Reversible || s.Captures?.GetValueOrDefault(objective.Target, -1) == objective.Side);
    internal static BattleState Refresh(ContentCatalog c, BattleState s)
    {
        if (!Enabled(c, s)) return s;
        var goals = c.Maps[s.Map].Objectives!; var claims = (s.Claims ?? []).ToList();
        foreach (var goal in goals) if (!claims.Contains(goal.Id) && Progress(c, s, goal) >= goal.Threshold) claims.Add(goal.Id);
        var next = s with { Claims = claims.ToArray() };
        return next with { Medals0 = goals.Where(o => o.Side == 0 && Active(next, o)).Sum(o => o.Medals), Medals1 = goals.Where(o => o.Side == 1 && Active(next, o)).Sum(o => o.Medals) };
    }
    private static BattleState Result(BattleState s, string reason)
    {
        int medals = s.Medals0.CompareTo(s.Medals1), stars = s.Stars0.CompareTo(s.Stars1), comparison = medals != 0 ? medals : stars;
        return s with { Finished = true, Winner = comparison == 0 ? null : comparison > 0 ? 0 : 1, EndReason = reason };
    }
    internal static BattleState Check(ContentCatalog c, BattleState s)
    {
        if (!Enabled(c, s)) return s;
        var next = Refresh(c, s);
        return !next.Finished && Math.Max(next.Medals0, next.Medals1) >= c.Maps[s.Map].VictoryMedals ? Result(next, "medals") : next;
    }
    internal static BattleState AtLimit(ContentCatalog c, BattleState s) => Result(Refresh(c, s), "round-limit");
    internal static BattleState Close(BattleState s) => s with { Combat = null, Defense = null, CapEntry = null, Overrun = null, Repairs = null, Hazard = null, Hazards = null, Reinforce = null,
        Cards = s.Cards == null ? null : s.Cards with { Pending = null, Response = null } };
    internal static BattleState RecordCapture(ContentCatalog c, BattleState s, string unit, int side)
    {
        if (!Enabled(c, s)) return s;
        var captures = new Dictionary<string, int>(s.Captures ?? []) { [unit] = side }; return s with { Captures = captures };
    }
    internal static BattleState RecordLoss(ContentCatalog c, BattleState s, BattleUnit unit, int credit)
    {
        if (!Enabled(c, s)) return s;
        var losses = new Dictionary<string, UnitLoss>(s.Losses ?? []);
        foreach (var lost in new[] { unit }.Concat(FleetLogistics.Children(s, unit.Id)))
            losses.TryAdd(lost.Id, new(lost.Definition, credit, c.Ships[lost.Definition].Stars + FleetRuleTools.AssignedLeaders(c, lost).Sum(l => l.Stars)));
        return s with { Losses = losses };
    }
    public static string Changes(BattleState before, BattleState after) => before.Medals0 == after.Medals0 && before.Medals1 == after.Medals1 ? "" : $" Медали: {before.Medals0}:{before.Medals1} → {after.Medals0}:{after.Medals1}.";
    public static string ResultText(BattleState s) => s.EndReason switch
    {
        "medals" => $"Достигнут порог медалей. Счёт {s.Medals0}:{s.Medals1}.",
        "round-limit" => $"Лимит раундов. Медали {s.Medals0}:{s.Medals1}, уничтожено ★ {s.Stars0}:{s.Stars1}.",
        _ => s.Winner == null ? "Лимит раундов. Ничья." : "Бой завершён."
    };
    internal static void ValidateDefinitions(ContentCatalog c, MapDefinition map)
    {
        if (map.VictoryMedals is < 1 or > 100) throw new InvalidDataException("Invalid medal victory threshold.");
        if (map.Objectives == null) return;
        if (map.Objectives.Length is < 1 or > 32 || !c.Rules[map.Rules].Tabletop || c.Rules[map.Rules].Handler != "cards-basic-v1" || map.Objectives.Any(o => o == null) || map.Objectives.Select(o => o.Id).Distinct().Count() != map.Objectives.Length)
            throw new InvalidDataException("Invalid scenario objectives.");
        var ids = new HashSet<string>(); int counter = 0;
        for (int side = 0; side < map.Fleets.Length; side++) foreach (var _ in (c.Fleets.TryGetValue(map.Fleets[side], out var fleet) ? fleet : throw new InvalidDataException("Missing objective fleet.")).Units) ids.Add($"s{side}-{counter++}");
        foreach (var goal in map.Objectives)
            if (string.IsNullOrWhiteSpace(goal.Id) || goal.Id.Length > 80 || string.IsNullOrWhiteSpace(goal.Name) || goal.Name.Length > 100 || goal.Side is < 0 or > 1 || goal.Medals is < 1 or > 3 || goal.Threshold is < 1 or > 1000000
                || goal.Kind is not ("destroy-stars" or "harvest-stars" or "capture" or "destroy-unit" or "destroy-count" or "event") || goal.Kind is "capture" or "destroy-unit" && (!ids.Contains(goal.Target) || goal.Threshold != 1)
                || goal.Kind == "event" && (goal.Threshold != 1 || !(map.Events ?? []).Any(e => e.Id == goal.Target))
                || goal.Reversible && goal.Kind != "capture" || (goal.Classes ?? []).Any(cls => !FleetRuleTools.Classes.Contains(cls)) || goal.Classes is { Length: > 0 } && goal.Kind != "destroy-stars") throw new InvalidDataException("Invalid objective: " + goal.Id);
    }
    internal static void Validate(ContentCatalog c, BattleState s)
    {
        if (!Enabled(c, s)) { if (s.Medals0 != 0 || s.Medals1 != 0 || s.Claims is { Length: > 0 } || s.Captures?.Count > 0 || s.Losses?.Count > 0 || s.EndReason != "") throw new InvalidDataException("Objectives on a legacy battle."); return; }
        var goals = c.Maps[s.Map].Objectives!;
        if (s.Claims == null || s.Claims.Distinct().Count() != s.Claims.Length || s.Claims.Any(id => !goals.Any(o => o.Id == id)) || s.Medals0 < 0 || s.Medals1 < 0
            || s.Captures?.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value is < 0 or > 1) == true
            || s.Losses?.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value == null || !c.Ships.ContainsKey(p.Value.Definition) || p.Value.Credit is < -1 or > 1 || p.Value.Stars is < 0 or > 1000000 || s.Units.Concat(s.Reserves ?? []).Any(u => u.Id == p.Key)) == true)
            throw new InvalidDataException("Invalid scenario score state.");
        if (goals.Any(o => s.Claims.Contains(o.Id) && (o.Kind == "capture" ? s.Captures?.ContainsKey(o.Target) != true : Progress(c, s, o) < o.Threshold))) throw new InvalidDataException("Objective claim has no supporting progress.");
        var derived = Refresh(c, s);
        if (!derived.Claims!.SequenceEqual(s.Claims) || derived.Medals0 != s.Medals0 || derived.Medals1 != s.Medals1 || s.EndReason is not ("" or "medals" or "round-limit") || s.Finished != (s.EndReason != "")
            || s.EndReason == "round-limit" && s.Round <= c.Rules[c.Maps[s.Map].Rules].RoundLimit || s.EndReason == "medals" && Math.Max(s.Medals0, s.Medals1) < c.Maps[s.Map].VictoryMedals
            || !s.Finished && Math.Max(s.Medals0, s.Medals1) >= c.Maps[s.Map].VictoryMedals || s.Finished && (Result(s, s.EndReason).Winner != s.Winner || s.Combat != null || s.Defense != null || s.Cards?.Pending != null || s.Cards?.Response != null || s.Hazard != null || s.Reinforce != null))
            throw new InvalidDataException("Inconsistent scenario medals/result.");
    }
}
