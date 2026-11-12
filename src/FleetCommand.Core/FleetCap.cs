namespace FleetCommand.Core;

public sealed record PendingCapEntry(int Side, string Unit);
public static class FleetCap
{
    public static bool Host(ShipDefinition ship) => FleetRuleTools.Class(ship) is "frigate" or "capital";
    public static BattleUnit? Patrol(BattleState s, string parent) => s.Units.Find(u => u.Patrol == parent);
    public static bool Available(BattleState s, string parent) => Patrol(s, parent) is { CapUsed: false, Hulk: false };
    public static WeaponDefinition[] Weapons(ContentCatalog c, BattleUnit unit) => FleetRuleTools.Has(unit, "weapons") ? [] : FleetWeapons.Weapons(c, c.Ships[unit.Definition]);
    internal static CommandResult Activate(ContentCatalog c, BattleState s, int side, BattleCommand command)
    {
        var frame = s.Defense!; var unit = Patrol(s, frame.Attack.Target);
        var hand = side == 0 ? s.Cards!.Hand0 : s.Cards!.Hand1;
        var weapon = unit == null ? null : Weapons(c, unit).FirstOrDefault(w => command.Weapon == "" || w.Id == command.Weapon);
        if (frame.CapActive || frame.CapComplete || !Available(s, frame.Attack.Target) || unit == null || weapon == null || !hand.Contains(command.Card)) return new(false, "Для CAP нужен неиспользованный патруль, оружие и карта из своей руки.", s);
        var next = s with { Units = s.Units.Select(u => u.Id == unit.Id ? u with { CapUsed = true, Activated = true } : u).ToList(),
            Cards = s.Cards! with { Hand0 = s.Cards.Hand0.Where(id => id != command.Card).ToArray(), Hand1 = s.Cards.Hand1.Where(id => id != command.Card).ToArray(), Discard = s.Cards.Discard.Append(command.Card).ToArray() },
            Defense = frame with { Patroller = unit.Id, CapActive = true } };
        unit = next.Units.Single(u => u.Id == unit.Id); var attacker = FleetTerrain.Find(next, frame.Attack.Unit)!;
        CommandResult result;
        if (FleetTerrain.Exchange(frame.Threat))
        {
            var incomingWeapon = FleetWeapons.Select(c, s, attacker, s.Units.Single(u => u.Id == frame.Attack.Target), frame.Attack.Weapon)!;
            uint random = next.RandomState;
            var cap = FleetCombat.PrepareReaction(c, next, unit, attacker, weapon, "cap", ref random);
            var incoming = FleetCombat.PrepareReaction(c, next, attacker, unit, incomingWeapon, next.Hazard == null ? "dogfight" : "terrain-dogfight", ref random);
            next = next with { RandomState = random, Defense = next.Defense! with { PreparedIncoming = incoming } };
            result = FleetCombat.ReactionStart(c, next, cap);
        }
        else result = FleetCombat.ReactionRoll(c, next, unit, attacker, weapon, "cap");
        return result.State.Combat != null ? result with { State = result.State with { Sequence = s.Sequence + 1 } } : AfterVolley(c, s, result);
    }
    internal static CommandResult RecordVolley(ContentCatalog c, BattleState s, int successes)
    {
        var frame = s.Defense!; var combat = s.Combat!;
        if (FleetTerrain.Exchange(frame.Threat))
        {
            var stored = combat with { Ghost = FleetCombat.Actor(s, combat) };
            return new(true, "Бросок воздушного боя подтверждён; потери будут применены одновременно.", s with { Combat = null, Defense = combat.Kind == "cap" ? frame with { CapVolley = stored } : frame with { IncomingVolley = stored } });
        }
        if (FleetDefense.Projectile(frame.Threat)) return new(true, $"CAP: входящая атака -{successes} кубиков.", s with { Combat = null, Defense = frame with { Reduction = frame.Reduction + successes } });
        return FleetCombat.ReactionDamage(c, s, combat);
    }
    internal static CommandResult AfterVolley(ContentCatalog c, BattleState before, CommandResult result)
    {
        var next = result.State; var frame = next.Defense!; string message = result.Message;
        if (FleetTerrain.Exchange(frame.Threat))
        {
            if (frame.IncomingVolley == null)
            {
                var incoming = FleetCombat.ReactionStart(c, next, frame.PreparedIncoming!);
                return incoming.State.Combat != null ? incoming with { State = incoming.State with { Sequence = before.Sequence + 1 } } : AfterVolley(c, before, incoming);
            }
            if (next.Hazard != null)
            {
                var exchange = FleetTerrain.CapExchange(c, next, frame); next = exchange.State; message = exchange.Message;
                frame = next.Defense!;
            }
            else
            {
                var cap = FleetCombat.ReactionDamage(c, next, frame.CapVolley!); var response = FleetCombat.ReactionDamage(c, cap.State, frame.IncomingVolley);
                next = response.State; message = cap.Message + " " + response.Message;
            }
        }
        next = FleetClose.Outcome(next, c);
        if (next.Finished && FleetObjectives.Enabled(c, next)) return new(true, message, FleetObjectives.Close(next) with { Sequence = before.Sequence + 1 });
        frame = frame with { CapActive = false, CapComplete = true, CapVolley = null, IncomingVolley = null, PreparedIncoming = null };
        next = next with { Defense = frame };
        var attacker = FleetTerrain.Find(next, frame.Attack.Unit);
        if (attacker is { Hulk: false, Models: > 0 } && FleetDefense.Weapons(c, next.Units.Single(u => u.Id == frame.Attack.Target)).Length > 0)
            return new(true, message + " CAP завершён. Можно применить защитный огонь корабля.", next with { Sequence = before.Sequence + 1 });
        return FleetDefense.Resume(c, before, next with { Defense = null }, frame, message);
    }
    internal static bool ValidVolley(ContentCatalog c, BattleState s, PendingCombat volley)
    {
        if (s.Defense is not { CapActive: true } frame) return false;
        string actor = volley.Kind == "cap" ? frame.Patroller : frame.Attack.Unit, target = volley.Kind == "cap" ? frame.Attack.Unit : frame.Patroller;
        var unit = FleetTerrain.Find(s, actor);
        return unit != null && volley.Side == unit.Side && volley.Attacker == actor && volley.Target == target && FleetWeapons.Weapons(c, c.Ships[unit.Definition]).Any(w => w.Id == volley.Weapon);
    }
    public static BattleUnit[] Hosts(ContentCatalog c, BattleState s, BattleUnit unit) => unit.Patrol != "" || FleetRuleTools.Class(c.Ships[unit.Definition]) != "fighter" ? []
        : s.Units.Where(u => u.Side == unit.Side && u.Id != unit.Id && u.Patrol == "" && Host(c.Ships[u.Definition])
            && Patrol(s, u.Id) == null && FleetFootprint.Distance(c, unit, u) == 1).ToArray();
    public static Hex[] ExitCells(ContentCatalog c, BattleState s, BattleUnit unit)
    {
        var parent = s.Units.Find(u => u.Id == unit.Patrol); if (parent == null || parent.Activated || unit.Activated) return [];
        var map = c.Maps[s.Map];
        return FleetFootprint.Adjacent(c, parent).Where(h => FleetFootprint.Inside(map, h)
            && !SkirmishGeometry.Blocked(c, map, h, s) && !s.Units.Any(u => u.Patrol == "" && FleetFootprint.Contains(c, u, h))).ToArray();
    }
    internal static CommandResult? Expand(ContentCatalog c, BattleState s, int side, BattleCommand command)
    {
        if (!FleetRuleTools.Enabled(c, s)) return null;
        string[] actors = command.Kind == "special" && SpecialOrders.Formation(command.Card) ? [command.Unit, command.OtherUnit] : [command.Unit];
        var patrols = s.Units.Where(u => actors.Contains(u.Id) && u.Patrol != "").ToArray(); if (patrols.Length == 0) return null;
        var placements = command.Deployments ?? [];
        if (patrols.Any(u => u.Side != side) || placements.Length != patrols.Length || placements.Select(p => p.Unit).Distinct().Count() != placements.Length
            || placements.Any(p => !patrols.Any(u => u.Id == p.Unit))) return new(false, "Выберите свободный соседний гекс выхода для каждого отряда CAP.", s);
        var expanded = s;
        foreach (var patrol in patrols)
        {
            var placement = placements.Single(p => p.Unit == patrol.Id); var cell = new Hex(placement.Q, placement.R);
            if (!ExitCells(c, expanded, patrol).Contains(cell)) return new(false, "CAP может выйти до приказа, если отряд и корабль ещё не активированы.", s);
            expanded = expanded with { Units = expanded.Units.Select(u => u.Id == patrol.Id ? u with { Patrol = "", Q = cell.Q, R = cell.R } : u).ToList() };
        }
        var result = new BasicCardRules().Apply(c, expanded, side, command with { Deployments = null });
        return result.Accepted ? result with { Message = "Истребители вышли из CAP. " + result.Message } : result with { State = s };
    }
    public static CommandResult ApplyEntry(ContentCatalog c, BattleState s, int side, BattleCommand command)
    {
        var entry = s.CapEntry!; var unit = s.Units.Single(u => u.Id == entry.Unit);
        if (side != entry.Side || command.Kind is not ("cap-enter" or "decline-cap")) return new(false, "Решите, назначать ли истребители в CAP.", s);
        var next = s with { CapEntry = null };
        if (command.Kind == "cap-enter")
        {
            var parent = Hosts(c, s, unit).FirstOrDefault(u => u.Id == command.Target);
            if (parent == null) return new(false, "Нужен соседний союзный корабль без другого CAP.", s);
            next = next with { Units = next.Units.Select(u => u.Id == unit.Id ? u with { Patrol = parent.Id, Q = parent.Q, R = parent.R } : u).ToList() };
        }
        return InterruptRules.Complete(c, s, next, command.Kind == "cap-enter" ? "Истребители назначены в CAP." : "Назначение в CAP пропущено.");
    }
    internal static BattleState Sync(BattleState before, BattleState next) => next with { Units = next.Units.Select(u =>
    {
        if (u.Patrol == "") return u;
        var parent = next.Units.Find(p => p.Id == u.Patrol);
        if (parent != null) return u with { Q = parent.Q, R = parent.R };
        var former = before.Units.Find(p => p.Id == u.Patrol);
        return u with { Patrol = "", Q = former?.Q ?? u.Q, R = former?.R ?? u.R, Activated = true };
    }).ToList() };
    internal static void Validate(ContentCatalog c, BattleState s)
    {
        var patrols = s.Units.Where(u => u.Patrol != "").ToArray();
        if (patrols.Any(u => !FleetRuleTools.Enabled(c, s) || FleetRuleTools.Class(c.Ships[u.Definition]) != "fighter"
            || !s.Units.Any(p => p.Id == u.Patrol && p.Patrol == "" && p.Side == u.Side && Host(c.Ships[p.Definition]) && p.Q == u.Q && p.R == u.R))
            || patrols.Select(u => u.Patrol).Distinct().Count() != patrols.Length) throw new InvalidDataException("Invalid CAP assignment.");
        if (s.Defense is { } defense)
        {
            if (defense.Attack == null) throw new InvalidDataException("Missing CAP attack.");
            if (defense.CapActive)
            {
                var cap = s.Units.Find(u => u.Id == defense.Patroller && u.Patrol == defense.Attack.Target && u.CapUsed);
                if (cap == null || defense.CapComplete || defense.Fired || FleetTerrain.Exchange(defense.Threat) != (defense.PreparedIncoming != null)
                    || s.Combat?.Kind is "dogfight" or "terrain-dogfight" && defense.CapVolley?.Ghost == null || s.Combat?.Kind == "cap" && defense.CapVolley != null
                    || !FleetTerrain.Exchange(defense.Threat) && (defense.CapVolley != null || s.Combat?.Kind is "dogfight" or "terrain-dogfight") || defense.IncomingVolley != null)
                    throw new InvalidDataException("Invalid CAP interception.");
            }
            else if (defense.CapVolley != null || defense.IncomingVolley != null || defense.PreparedIncoming != null) throw new InvalidDataException("Unexpected CAP exchange rolls.");
            foreach (var volley in new[] { defense.CapVolley, defense.IncomingVolley, defense.PreparedIncoming }.Where(v => v != null))
            {
                if (volley!.Kind is not ("cap" or "dogfight" or "terrain-dogfight") || !ValidVolley(c, s, volley) || volley.AbilityRerolls != 0 || volley.Rolls == null || volley.Rolls.Length is < 1 or > 10000 || volley.Rolls.Any(d => d is < 1 or > 6)) throw new InvalidDataException("Invalid saved CAP volley.");
                if (volley.Ghost is { } ghost)
                {
                    var snapshotActor = FleetTerrain.Find(s, ghost.Id);
                    if (snapshotActor == null || snapshotActor.Side != ghost.Side || snapshotActor.Definition != ghost.Definition || snapshotActor.Models != ghost.Models || !FleetRuleTools.LeaderIds(snapshotActor).SequenceEqual(FleetRuleTools.LeaderIds(ghost))
                        || snapshotActor.CapUsed != ghost.CapUsed || snapshotActor.Patrol != ghost.Patrol || snapshotActor.Q != ghost.Q || snapshotActor.R != ghost.R || snapshotActor.FocusFire != ghost.FocusFire || snapshotActor.CrewDamage != ghost.CrewDamage) throw new InvalidDataException("Invalid CAP volley actor snapshot.");
                }
            }
        }
        if (s.CapEntry is not { } entry) return;
        var actor = s.Units.Find(u => u.Id == entry.Unit && u.Side == entry.Side);
        if (!FleetRuleTools.Enabled(c, s) || s.Finished || s.Defense != null || s.Combat != null || s.Overrun != null || s.Repairs is { Length: > 0 }
            || s.Cards?.Response is not { Command.Kind: "move" } pause || pause.Side != entry.Side || pause.Command.Unit != entry.Unit || pause.RemainingPath.Length != 0
            || actor == null || Hosts(c, s, actor).Length == 0) throw new InvalidDataException("Invalid CAP assignment decision.");
    }
}
