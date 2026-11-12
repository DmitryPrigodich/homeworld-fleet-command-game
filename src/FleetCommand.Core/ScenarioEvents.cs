namespace FleetCommand.Core;

public sealed record ScenarioEvent(string Id, string Trigger, int Side, string Effect, string Name, string Class = "", int Object = -1, int Radius = 0,
    Hex[]? Zone = null, string Token = "", string RequiresToken = "", int Round = 0, bool Once = true, string IfNoEvent = "", string Fleet = "");
public sealed record ScenarioToken(string Unit, string Token);
public sealed record ScenarioState(string[] Fired, ScenarioToken[] Tokens, BattleUnit[] Escaped);

public static class ScenarioEvents
{
    public static bool HasToken(BattleState s, string unit, string token) => s.Scenario?.Tokens.Any(t => t.Unit == unit && t.Token == token) == true;
    internal static BattleState FinishOrder(ContentCatalog c, BattleState before, BattleState s, int side)
    {
        string[] actors = (before.Cards?.Pending?.Units ?? []).Concat(before.Cards?.Response is { } pause ? [pause.Command.Unit] : Array.Empty<string>())
            .Concat(s.Units.Where(u => u.Side == side && u.Activated && !before.Units.Any(old => old.Id == u.Id && old.Activated)).Select(u => u.Id)).Distinct().ToArray();
        return Process(c, s, "end-order", side, actors);
    }
    internal static BattleState Process(ContentCatalog c, BattleState s, string trigger, int side = -1, string[]? actors = null)
    {
        if (s.Finished && trigger == "end-order") return s;
        foreach (var e in c.Maps[s.Map].Events ?? [])
        {
            var progress = s.Scenario ?? new([], [], []);
            if (e.Trigger != trigger || side >= 0 && e.Side != side || e.Round > 0 && (trigger == "battle-end" ? s.Round - 1 : s.Round) != e.Round
                || e.Once && progress.Fired.Contains(e.Id) || e.IfNoEvent != "" && progress.Fired.Contains(e.IfNoEvent)) continue;
            var candidates = s.Units.Where(u => u.Side == e.Side && u.Patrol == "" && !u.Hulk && (actors == null || actors.Contains(u.Id))
                && (e.Class == "" || FleetRuleTools.Class(c.Ships[u.Definition]) == e.Class) && (e.RequiresToken == "" || HasToken(s, u.Id, e.RequiresToken))
                && (e.Object < 0 || FleetFootprint.Distance(c, u, new Hex(c.Maps[s.Map].Objects[e.Object].Q, c.Maps[s.Map].Objects[e.Object].R)) <= e.Radius)
                && (e.Zone == null || FleetFootprint.Cells(c, u).Any(e.Zone.Contains))).ToArray();
            bool fired = false;
            if (e.Effect == "mark")
            {
                var tokens = progress.Tokens.ToList();
                foreach (var unit in candidates) if (!tokens.Any(t => t.Unit == unit.Id && t.Token == e.Token)) { tokens.Add(new(unit.Id, e.Token)); fired = true; if (e.Once) break; }
                if (fired) s = s with { Scenario = progress with { Tokens = tokens.ToArray() } };
            }
            else if (e.Effect == "deliver" && candidates.Length > 0)
            {
                var delivered = (e.Once ? candidates.Take(1) : candidates).Select(u => u.Id).ToHashSet();
                s = s with { Scenario = progress with { Tokens = progress.Tokens.Where(t => !delivered.Contains(t.Unit) || t.Token != e.RequiresToken).ToArray() } }; fired = true;
            }
            else if (e.Effect == "escape" && candidates.Length > 0)
            {
                var escaping = e.Once ? candidates.Take(1).ToArray() : candidates; var ids = escaping.Select(u => u.Id).ToHashSet();
                var children = (s.Reserves ?? []).Where(u => ids.Contains(u.Embarked)).ToArray(); foreach (var child in children) ids.Add(child.Id);
                s = s with { Units = s.Units.Where(u => !ids.Contains(u.Id)).ToList(), Reserves = s.Reserves?.Where(u => !ids.Contains(u.Id)).ToList(),
                    Scenario = progress with { Escaped = progress.Escaped.Concat(escaping).Concat(children).ToArray(), Tokens = progress.Tokens.Where(t => !ids.Contains(t.Unit)).ToArray() } }; fired = true;
            }
            else if (e.Effect == "award") fired = true;
            else if (e.Effect == "reinforce")
            {
                var placements = c.Fleets[e.Fleet].Units; int first = Math.Max(s.NextUnit, c.Maps[s.Map].Fleets.Sum(id => c.Fleets[id].Units.Length));
                var arrivals = placements.Select((p, i) => new BattleUnit($"s{e.Side}-{first + i}", p.Ship, e.Side, -1, -1, p.Facing, c.Ships[p.Ship].Models,
                    Leader: p.Leader, Cargo: p.Cargo, Embarked: p.EmbarkedOn < 0 ? "" : $"s{e.Side}-{first + p.EmbarkedOn}", AdditionalLeaders: p.AdditionalLeaders)).ToArray();
                s = s with { Reserves = (s.Reserves ?? []).Concat(arrivals).ToList(), NextUnit = first + arrivals.Length }; fired = true;
            }
            if (fired)
            {
                progress = s.Scenario ?? progress; s = s with { Scenario = progress with { Fired = progress.Fired.Append(e.Id).Distinct().ToArray() } };
            }
        }
        return s;
    }
    internal static BattleState Prune(BattleState s) => s.Scenario == null ? s : s with { Scenario = s.Scenario with { Tokens = s.Scenario.Tokens.Where(t => s.Units.Concat(s.Reserves ?? []).Any(u => u.Id == t.Unit)).ToArray() } };
    public static string Changes(ContentCatalog c, BattleState before, BattleState after)
    {
        var fired = (after.Scenario?.Fired ?? []).Except(before.Scenario?.Fired ?? []).ToHashSet();
        string message = string.Join(" ", (c.Maps[after.Map].Events ?? []).Where(e => fired.Contains(e.Id)).Select(e => e.Name + "."));
        int tokens = (after.Scenario?.Tokens.Length ?? 0) - (before.Scenario?.Tokens.Length ?? 0);
        if (tokens > 0 && fired.Count == 0) message += " New intelligence gathered.";
        if ((before.Scenario?.Tokens ?? []).Any(t => !after.Units.Concat(after.Reserves ?? []).Any(u => u.Id == t.Unit) && !(after.Scenario?.Escaped ?? []).Any(u => u.Id == t.Unit))) message += " The destroyed unit's intelligence was lost.";
        return message == "" ? "" : " " + message;
    }
    internal static void ValidateDefinitions(ContentCatalog c, MapDefinition map)
    {
        var events = map.Events ?? [];
        if (events.Length > 64 || events.Any(e => e == null) || events.Select(e => e.Id).Distinct().Count() != events.Length) throw new InvalidDataException("Invalid scenario events.");
        foreach (var e in events)
        {
            if (string.IsNullOrWhiteSpace(e.Id) || e.Id.Length > 80 || string.IsNullOrWhiteSpace(e.Name) || e.Name.Length > 160 || e.Side is < 0 or > 1
                || e.Trigger is not ("end-order" or "round-start" or "round-end" or "battle-end") || e.Effect is not ("mark" or "escape" or "deliver" or "award" or "reinforce") || e.Round < 0 || e.Round > 1000
                || e.Class != "" && !FleetRuleTools.Classes.Contains(e.Class) || e.Object < -1 || e.Object >= map.Objects.Length || e.Radius is < 0 or > 50
                || e.Zone != null && (e.Zone.Length == 0 || e.Zone.Any(h => !FleetFootprint.Inside(map, h))) || e.Token == null || e.RequiresToken == null
                || e.Effect == "mark" && string.IsNullOrWhiteSpace(e.Token) || e.IfNoEvent != "" && !events.Any(other => other.Id == e.IfNoEvent)
                || e.RequiresToken != "" && !events.Any(other => other.Effect == "mark" && other.Token == e.RequiresToken)
                || e.Effect == "reinforce" && (!e.Once || !c.Fleets.ContainsKey(e.Fleet))) throw new InvalidDataException("Invalid scenario event: " + e.Id);
            if (e.Effect == "reinforce")
            {
                var fleet = c.Fleets[e.Fleet]; FleetLogistics.ValidatePlacements(c, fleet, true);
                if (fleet.Units.Any(u => u.EmbarkedOn < 0 && FleetRuleTools.Class(c.Ships[u.Ship]) is not ("frigate" or "capital"))) throw new InvalidDataException("Event reinforcements need reserve-eligible parent units.");
            }
        }
    }
    internal static void Validate(ContentCatalog c, BattleState s)
    {
        if (s.Scenario is not { } p) return;
        var events = c.Maps[s.Map].Events ?? [];
        if (p.Fired == null || p.Tokens == null || p.Escaped == null || p.Fired.Distinct().Count() != p.Fired.Length || p.Fired.Any(id => !events.Any(e => e.Id == id))
            || p.Tokens.Distinct().Count() != p.Tokens.Length || p.Tokens.Any(t => t == null || !s.Units.Concat(s.Reserves ?? []).Any(u => u.Id == t.Unit) || !events.Any(e => e.Effect == "mark" && e.Token == t.Token))
            || p.Escaped.Select(u => u.Id).Distinct().Count() != p.Escaped.Length || p.Escaped.Any(u => !c.Ships.ContainsKey(u.Definition) || u.Models < 1 || u.Models > c.Ships[u.Definition].Models || u.Side is < 0 or > 1
                || !FleetRuleTools.ValidLeader(c, u) || s.Units.Concat(s.Reserves ?? []).Any(live => live.Id == u.Id) || s.Losses?.ContainsKey(u.Id) == true)) throw new InvalidDataException("Invalid scenario progress.");
    }
}
