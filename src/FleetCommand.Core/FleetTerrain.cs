namespace FleetCommand.Core;

public sealed record HazardJob(string Unit, int Object, string Resume);
public sealed record PendingHazard(HazardJob Job, BattleUnit Source, string Kind, bool ReduceAfter = false, bool DamageReduction = false);

public static class FleetTerrain
{
    public static string Kind(ObjectDefinition obj) => obj.Kind is "asteroid" or "mine" ? obj.Kind : obj.Id == "core:asteroids" ? "asteroid" : "";
    public static HazardDefinition? Attack(ObjectDefinition obj) => Kind(obj) == "" ? null : obj.Hazard ?? new(Kind(obj) == "mine" ? 1 : 3);
    private static string ProfileId(string obj) => "runtime:terrain/" + obj;
    internal static void Register(ContentCatalog c)
    {
        foreach (var obj in c.Objects.Values)
        {
            var attack = Attack(obj); if (attack == null) continue;
            string id = ProfileId(obj.Id);
            c.Ships[id] = new(id, FleetPresentation.ObjectName(obj), "terrain", "", 5, 0, attack.Dice, attack.Success, 0, 0,
                Class: "station", Weapons: [new("terrain", "Terrain effect", attack.Dice, 1, Kind: "close", Penetrating: attack.Penetrating)]);
        }
    }
    public static int MineCount(BattleState s, int index) => s.Mines?.GetValueOrDefault(index, 5) ?? 5;
    public static bool Visible(ContentCatalog c, BattleState s, int index) => Kind(c.Objects[c.Maps[s.Map].Objects[index].Definition]) != "mine" || MineCount(s, index) > 0;
    public static IEnumerable<MapObject> Objects(ContentCatalog c, BattleState s) => c.Maps[s.Map].Objects.Where((_, i) => Visible(c, s, i));
    public static BattleUnit? Find(BattleState s, string id) => s.Units.Find(u => u.Id == id) ?? (s.Hazard?.Source.Id == id ? Source(s) : null);
    public static BattleUnit Source(BattleState s) => s.Hazard!.Kind == "mine" ? s.Hazard.Source with { Models = MineCount(s, s.Hazard.Job.Object) } : s.Hazard.Source;
    public static bool Diffuse(string threat) => FleetDefense.Projectile(threat) || threat is "asteroid" or "mine";
    public static bool Exchange(string threat) => threat is "fighter" or "asteroid" or "mine";
    internal static BattleState ReduceMine(BattleState s)
    {
        int index = s.Hazard!.Job.Object; var mines = new Dictionary<int, int>(s.Mines ?? []);
        mines[index] = MineCount(s, index) == 5 ? 3 : 0;
        return s with { Mines = mines };
    }
    private static HazardJob[] Jobs(ContentCatalog c, BattleState s, IEnumerable<string> units, string resume, Hex[]? entered = null)
    {
        if (!FleetRuleTools.Enabled(c, s)) return [];
        var map = c.Maps[s.Map];
        return units.Distinct().SelectMany(id =>
        {
            var unit = s.Units.Find(u => u.Id == id && u.Patrol == ""); if (unit == null) return Array.Empty<HazardJob>();
            var at = entered ?? FleetFootprint.Cells(c, unit);
            return map.Objects.Select((obj, index) => (obj, index)).Where(p => at.Contains(new(p.obj.Q, p.obj.R)) && Attack(c.Objects[p.obj.Definition]) is { Dice: > 0 }
                && (Kind(c.Objects[p.obj.Definition]) != "mine" || p.obj.Owner != unit.Side && MineCount(s, p.index) > 0)).Select(p => new HazardJob(id, p.index, resume)).ToArray();
        }).ToArray();
    }
    internal static CommandResult Activation(ContentCatalog c, BattleState before, BattleState next, IEnumerable<string> units, string resume, string message)
    {
        var jobs = Jobs(c, next, units, resume);
        if (jobs.Length == 0 && resume == "reinforce" && next.Repairs is not { Length: > 0 }) return FleetLogistics.Resume(c, before, next, message);
        if (jobs.Length == 0) return next.Repairs is not { Length: > 0 } && next.Cards?.Pending?.Deferred != null ? FleetTransfers.Complete(c, before, next, message) : new(true, message, next);
        return StartQueued(c, before, next with { Hazards = jobs }, message);
    }
    internal static CommandResult? Entry(ContentCatalog c, BattleState before, BattleState next, string unit, Hex cell, string message, Hex[]? entered = null)
    {
        var jobs = Jobs(c, next, [unit], "movement", entered ?? [cell]);
        return jobs.Length == 0 ? null : StartQueued(c, before, next with { Hazards = jobs }, message);
    }
    internal static CommandResult StartQueued(ContentCatalog c, BattleState before, BattleState next, string message)
    {
        if (next.Repairs is { Length: > 0 }) return new(true, message, next);
        var jobs = next.Hazards ?? []; string resume = jobs.FirstOrDefault()?.Resume ?? "order";
        while (jobs.Length > 0)
        {
            var job = jobs[0]; var unit = next.Units.Find(u => u.Id == job.Unit); var placement = c.Maps[next.Map].Objects[job.Object];
            var obj = c.Objects[placement.Definition];
            if (unit == null || unit.Hulk || Kind(obj) == "mine" && MineCount(next, job.Object) == 0 || next.Finished)
            { jobs = jobs.Skip(1).ToArray(); next = next with { Hazards = jobs.Length == 0 ? null : jobs }; continue; }
            Register(c);
            var source = new BattleUnit("terrain:" + job.Object, ProfileId(obj.Id), placement.Owner >= 0 ? placement.Owner : 1 - unit.Side, unit.Q, unit.R, 0,
                Kind(obj) == "mine" ? MineCount(next, job.Object) : 1);
            next = next with { Hazard = new(job, source, Kind(obj)), Hazards = jobs };
            var command = new BattleCommand("attack", source.Id, unit.Id, Weapon: "terrain");
            var result = FleetDefense.Offer(c, next, source.Side, command, false) ?? FleetCombat.RollAttack(c, next, source.Side, command, false);
            result = result with { Message = message + " " + result.Message };
            return result.State.Combat != null || result.State.Defense != null ? result with { State = result.State with { Sequence = before.Sequence + 1 } } : AfterDamage(c, before, result);
        }
        return Resume(c, before, next with { Hazard = null, Hazards = null }, resume, message);
    }
    internal static CommandResult AfterDamage(ContentCatalog c, BattleState before, CommandResult result)
    {
        var next = result.State; var hazard = next.Hazard!;
        if (hazard.Kind == "mine")
        {
            if (hazard.ReduceAfter) next = ReduceMine(next);
            if (hazard.DamageReduction) next = ReduceMine(next);
        }
        var jobs = next.Hazards!.Skip(1).ToArray();
        next = next with { Hazard = null, Hazards = jobs.Length == 0 ? null : jobs, Combat = null, Defense = null };
        if (jobs.Length > 0 && !next.Finished) return StartQueued(c, before, next, result.Message);
        return Resume(c, before, next with { Hazards = null }, hazard.Job.Resume, result.Message);
    }
    internal static CommandResult Resume(ContentCatalog c, BattleState before, BattleState next, string resume, string message)
    {
        if (resume == "reinforce") return FleetLogistics.Resume(c, before, next, message);
        if (resume != "order") return InterruptRules.ResumeTerrain(c, before, next, message, resume == "response-start");
        var order = next.Cards?.Pending;
        if (order == null) return new(true, message, next with { Sequence = before.Sequence + 1 });
        if (order.Deferred != null && !next.Finished && next.Units.Any(u => u.Id == order.Deferred.Unit)) return FleetTransfers.Complete(c, before, next, message);
        if (next.Finished || !next.Units.Any(u => u.Id == SpecialOrders.Actor(order))) return SpecialOrders.Advance(c, before, next, order.Side, message, next.Finished);
        return new(true, message, next with { Sequence = before.Sequence + 1 });
    }
    internal static CommandResult Defended(BattleState s, int successes)
    {
        var frame = s.Defense!;
        return s.Hazard!.Kind == "mine" ? new(true, "Defensive fire: the minefield will be reduced after its attack.", s with { Combat = null, Hazard = s.Hazard with { ReduceAfter = s.Hazard.ReduceAfter || successes > 0 } })
            : new(true, $"Defensive fire: asteroid attack -{successes} dice.", s with { Combat = null, Defense = frame with { Reduction = frame.Reduction + successes } });
    }
    internal static CommandResult CapExchange(ContentCatalog c, BattleState next, PendingDefense frame)
    {
        var cap = frame.CapVolley!; int successes = cap.Rolls.Count(d => d >= FleetCombat.Success(c, next, cap));
        var fighter = next.Units.Single(u => u.Id == frame.Patroller);
        var result = FleetCombat.ReactionDamage(c, next, frame.IncomingVolley!); next = result.State;
        if (next.Hazard!.Kind == "mine")
        {
            if (successes > 0) next = ReduceMine(next);
            if (!next.Units.Any(u => u.Id == fighter.Id && u.Models == fighter.Models)) next = ReduceMine(next);
        }
        else next = next with { Defense = next.Defense! with { Reduction = frame.Reduction + successes } };
        return result with { State = next, Message = "CAP encountered hazardous terrain. " + result.Message };
    }
    internal static void Validate(ContentCatalog c, BattleState s)
    {
        var map = c.Maps[s.Map];
        if (s.Mines?.Any(p => p.Key < 0 || p.Key >= map.Objects.Length || Kind(c.Objects[map.Objects[p.Key].Definition]) != "mine" || p.Value is not (0 or 3 or 5)) == true) throw new InvalidDataException("Invalid mine tokens.");
        if (s.Hazards == null && s.Hazard == null) return;
        if (!FleetRuleTools.Enabled(c, s) || s.Finished || s.Hazards is not { Length: > 0 } jobs || jobs.Length > 10000 || s.CapEntry != null || s.Overrun != null
            || jobs.Any(j => j == null || j.Object < 0 || j.Object >= map.Objects.Length || j.Resume is not ("order" or "movement" or "response-start" or "reinforce") || Attack(c.Objects[map.Objects[j.Object].Definition]) is not { Dice: > 0 })
            || s.Reinforce == null && s.Cards?.Pending == null && s.Cards?.Response == null) throw new InvalidDataException("Invalid pending terrain attacks.");
        if (s.Hazard is not { } h) { if (s.Repairs is not { Length: > 0 }) throw new InvalidDataException("Unstarted terrain queue."); return; }
        if (h.Job == null || h.Job != jobs[0]) throw new InvalidDataException("Invalid active terrain job.");
        var unit = s.Units.Find(u => u.Id == h.Job.Unit); var obj = map.Objects[h.Job.Object];
        var pose = h.Job.Resume == "movement" && s.Cards?.Response?.Transit is { } transit ? transit : unit == null ? new Hex(-1, -1) : new Hex(unit.Q, unit.R);
        int facing = s.Cards?.Response is { Transit: not null, TransitFacing: >= 0 } pause ? pause.TransitFacing : unit?.Facing ?? 0;
        var positions = unit == null ? [] : FleetFootprint.Cells(c.Ships[unit.Definition], pose, facing);
        if (unit == null || h.Source == null || !positions.Contains(new(obj.Q, obj.R)) || h.Kind == "mine" && obj.Owner == unit.Side || h.Kind != Kind(c.Objects[obj.Definition]) || h.Source.Definition != ProfileId(obj.Definition)
            || h.Source.Id != "terrain:" + h.Job.Object || s.Units.Any(u => u.Id == h.Source.Id) || h.Source.Side != (obj.Owner >= 0 ? obj.Owner : 1 - unit.Side)
            || h.Source.Q != unit.Q || h.Source.R != unit.R || h.Source.Models != (h.Kind == "mine" ? 5 : 1) && !(h.Kind == "mine" && h.Source.Models == 3)
            || s.Repairs is { Length: > 0 } || s.Defense == null && s.Combat == null) throw new InvalidDataException("Invalid active terrain attack.");
    }
}
