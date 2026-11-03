namespace FleetCommand.Core;

// Keeps the declared attack, not its rolls: casualties from defense change the incoming pool.
public sealed record PendingDefense(int Side, BattleCommand Attack, bool Interrupt, string Threat, int IncomingDice,
    bool Fired = false, int Reduction = 0, string Patroller = "", bool CapActive = false, bool CapComplete = false,
    PendingCombat? CapVolley = null, PendingCombat? IncomingVolley = null, PendingCombat? PreparedIncoming = null);

public static class FleetDefense
{
    public static bool Projectile(string threat) => threat is "missile" or "torpedo" or "bomb" or "drone";
    public static string Threat(ContentCatalog c, BattleState s, BattleUnit unit, BattleUnit target, BattleCommand attack)
    {
        if (s.Hazard?.Source.Id == unit.Id) return s.Hazard.Kind;
        if (attack.AttackKind != "ranged") return attack.AttackKind;
        var weapon = FleetWeapons.Select(c, s, unit, target, attack.Weapon);
        if (weapon == null) return "";
        return Projectile(weapon.Kind) || weapon.Kind == "close" ? weapon.Kind : FleetRuleTools.Class(c.Ships[unit.Definition]) == "fighter" ? "fighter" : "";
    }
    public static WeaponDefinition[] Weapons(ContentCatalog c, BattleUnit defender) => defender.Hulk || FleetRuleTools.Has(defender, "weapons") ? []
        : FleetWeapons.Weapons(c, c.Ships[defender.Definition]).Where(w => w.DefensiveFire).ToArray();
    public static bool Locked(BattleState s, BattleUnit defender, BattleCommand attack) => attack.AttackKind == "board"
        && defender.LastActionRound == s.Round && defender.LastAction == "board" && defender.LastTarget == attack.Unit;
    internal static CommandResult? Offer(ContentCatalog c, BattleState s, int side, BattleCommand attack, bool interrupt)
    {
        var unit = FleetTerrain.Find(s, attack.Unit)!; var defender = s.Units.Single(u => u.Id == attack.Target);
        string threat = Threat(c, s, unit, defender, attack);
        if (threat == "" || Locked(s, defender, attack) || Weapons(c, defender).Length == 0 && !FleetCap.Available(s, defender.Id)) return null;
        var frame = new PendingDefense(side, attack, interrupt, threat, FleetCombat.AttackDice(c, s, attack));
        return new(true, "До атаки: противник может применить CAP / защитный огонь или пропустить защиту.", s with { Defense = frame });
    }
    public static int Dice(ContentCatalog c, BattleState s, WeaponDefinition weapon)
    {
        var frame = s.Defense!; var unit = s.Units.Single(u => u.Id == frame.Attack.Target);
        var incoming = FleetTerrain.Find(s, frame.Attack.Unit)!;
        if (weapon.Barrage > 0) return Math.Max(1, (FleetTerrain.Diffuse(frame.Threat) ? frame.IncomingDice : incoming.Models) - unit.FocusFire);
        int support = s.Units.Count(u => u.Patrol == "" && u.Id != unit.Id && u.Side == unit.Side && new Hex(u.Q, u.R).Distance(new(unit.Q, unit.R)) == 1 && Weapons(c, u).Length > 0);
        return Math.Max(1, weapon.DicePerModel * unit.Models + support - unit.FocusFire + FleetRuleTools.AssignedLeaders(c, unit).Sum(l => l.AttackDice));
    }
    public static CommandResult Apply(ContentCatalog c, BattleState s, int side, BattleCommand command)
    {
        var frame = s.Defense!;
        if (side != 1 - frame.Side || frame.Fired) return new(false, "Решение о защитном огне принимает защищающийся.", s);
        if (command.Kind == "cap") return FleetCap.Activate(c, s, side, command);
        if (command.Kind == "decline-defense") return Resume(c, s, s with { Defense = null }, frame, "Защитный огонь пропущен.");
        var defender = s.Units.Single(u => u.Id == frame.Attack.Target);
        var weapon = Weapons(c, defender).FirstOrDefault(w => command.Weapon == "" || w.Id == command.Weapon);
        if (command.Kind != "defend" || weapon == null) return new(false, "Выберите оружие с защитным огнём.", s);
        var result = FleetCombat.DefensiveRoll(c, s with { Defense = frame with { Fired = true } }, weapon, Dice(c, s, weapon));
        return result.State.Combat != null ? result with { State = result.State with { Sequence = s.Sequence + 1 } } : AfterRoll(c, s, result);
    }
    internal static CommandResult AfterRoll(ContentCatalog c, BattleState before, CommandResult result)
    {
        if (result.State.Finished && FleetObjectives.Enabled(c, result.State)) return result with { State = FleetObjectives.Close(result.State) with { Sequence = before.Sequence + 1 } };
        var frame = result.State.Defense!;
        return Resume(c, before, result.State with { Defense = null }, frame, result.Message);
    }
    internal static CommandResult Resume(ContentCatalog c, BattleState before, BattleState next, PendingDefense frame, string message)
    {
        if (next.Hazard is { } hazard && hazard.Kind == "mine" && FleetTerrain.MineCount(next, hazard.Job.Object) == 0) return FleetTerrain.AfterDamage(c, before, new(true, message + " Минное поле снято.", next));
        if (next.Hazard != null)
        {
            var terrain = FleetCombat.RollAttack(c, next, frame.Side, frame.Attack, false, frame.Reduction);
            terrain = terrain with { Message = message + " " + terrain.Message };
            return terrain.State.Combat != null ? terrain with { State = terrain.State with { Sequence = before.Sequence + 1 } } : FleetTerrain.AfterDamage(c, before, terrain);
        }
        var unit = next.Units.Find(u => u.Id == frame.Attack.Unit);
        if (unit == null || unit.Hulk)
            return InterruptRules.CombatResolved(c, before, FleetClose.Outcome(next, c), message + " Атака остановлена защитным огнём.", frame.Interrupt);
        var attack = FleetCombat.RollAttack(c, next, frame.Side, frame.Attack, frame.Interrupt, frame.Reduction);
        if (attack.State.Combat != null || attack.State.Overrun != null)
            return attack with { Message = message + " " + attack.Message, State = attack.State with { Sequence = before.Sequence + 1 } };
        return InterruptRules.CombatResolved(c, before, attack.State, message + " " + attack.Message, frame.Interrupt);
    }
    internal static void Validate(ContentCatalog c, BattleState s)
    {
        if (s.Defense is not { } frame) return;
        if (frame.Attack == null) throw new InvalidDataException("Missing defended attack.");
        var unit = FleetTerrain.Find(s, frame.Attack.Unit);
        var defender = s.Units.Find(u => u.Id == frame.Attack.Target && u.Side == 1 - frame.Side);
        int sourceSide = s.Hazard?.Source.Side ?? (s.Cards?.Response == null ? -1 : frame.Interrupt ? 1 - s.Cards.Response.Side : s.Cards.Response.Side);
        var declared = s.Cards?.Response?.Command;
        if (!FleetRuleTools.Enabled(c, s) || s.Finished || s.Overrun != null || frame.Side != sourceSide || frame.IncomingDice is < 1 or > 10000
            || frame.Reduction < 0 || frame.Reduction > 10000 || unit == null || defender == null || frame.Threat != Threat(c, s, unit, defender, frame.Attack)
            || frame.Threat == "" || Locked(s, defender, frame.Attack) || Weapons(c, defender).Length == 0 && !FleetCap.Available(s, defender.Id) && !frame.CapActive
            || s.Hazard == null && !frame.Interrupt && (declared == null || frame.Attack.Unit != declared.Unit || frame.Attack.Target != declared.Target || frame.Attack.Weapon != declared.Weapon || frame.Attack.AttackKind != declared.AttackKind || frame.Attack.Power != declared.Power)
            || frame.Fired != (s.Combat?.Kind == "defense") || frame.CapActive != (s.Combat?.Kind is "cap" or "dogfight" or "terrain-dogfight")) throw new InvalidDataException("Invalid defensive-fire window.");
    }
}
