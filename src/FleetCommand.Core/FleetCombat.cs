namespace FleetCommand.Core;

public sealed record PendingCombat(int Side, string Attacker, string Target, string Weapon, bool Power, int[] Rolls, bool Interrupt = false, bool Rerolled = false);
public static class FleetCombat
{
    private static int[] Roll(ContentCatalog c, BattleState s, BattleUnit unit, BattleUnit target, WeaponDefinition weapon, int dice, ref uint random)
    {
        var ship = c.Ships[unit.Definition]; var defender = c.Ships[target.Definition];
        var rolls = new int[dice]; for (int i = 0; i < dice; i++) rolls[i] = FleetRuleTools.Roll(ref random);
        int rapid = weapon.RapidTargets == null || weapon.RapidTargets.Contains(FleetRuleTools.Class(defender)) ? weapon.RapidFire : 0;
        int rerolls = rapid - defender.Evasive - (FleetRuleTools.Has(target, "evasive") ? 1 : 0);
        if (rerolls > 0) for (int i = 0; i < dice && rerolls > 0; i++) if (rolls[i] < ship.Success) { rolls[i] = FleetRuleTools.Roll(ref random); rerolls--; }
        if (rerolls < 0) for (int i = 0; i < dice && rerolls < 0; i++) if (rolls[i] >= ship.Success) { rolls[i] = FleetRuleTools.Roll(ref random); rerolls++; }
        return rolls;
    }
    public static bool CanValor(BattleState s, int side) => s.ValorOwner == side && (s.ValorUsedMask & (1 << side)) == 0 && s.Combat?.Rerolled != true;
    public static bool CanFaction(ContentCatalog c, BattleState s, PendingCombat combat) => !combat.Rerolled && !(combat.Side == 0 ? s.FactionUsed0 : s.FactionUsed1)
        && c.Ships[s.Units.Single(u => u.Id == combat.Attacker).Definition].Faction == "taiidan";
    internal static CommandResult Begin(ContentCatalog c, BattleState s, int side, BattleCommand cmd, bool interrupt)
    {
        var unit = s.Units.Single(u => u.Id == cmd.Unit); var target = s.Units.Find(u => u.Id == cmd.Target);
        var weapon = target == null ? null : FleetWeapons.Select(c, s, unit, target, cmd.Weapon);
        if (weapon == null) return new(false, "Выбранное оружие не может атаковать цель.", s);
        int dice = Math.Max(1, weapon.DicePerModel * unit.Models + (cmd.Power ? weapon.PowerDice : 0)
            + (unit.Leader == "" ? 0 : c.Leaders[unit.Leader].AttackDice) - FleetWeapons.Attack(c, s, unit, target, weapon.Id).Cover);
        uint random = s.RandomState; var rolls = Roll(c, s, unit, target!, weapon, dice, ref random);
        var combat = new PendingCombat(side, unit.Id, target!.Id, weapon.Id, cmd.Power, rolls, interrupt);
        var next = s with { Combat = combat, RandomState = random };
        if (!CanValor(next, side) && !CanFaction(c, next, combat)) return Resolve(c, next);
        return new(true, $"Броски [{string.Join(", ", rolls)}]. Подтвердите результат или используйте переброс.", next);
    }
    // The caller retains the suspended action until the damage and any reroll are settled.
    private static CommandResult Resolve(ContentCatalog c, BattleState s)
    {
        var combat = s.Combat!; var unit = s.Units.Single(u => u.Id == combat.Attacker); var target = s.Units.Single(u => u.Id == combat.Target);
        var ship = c.Ships[unit.Definition]; var defender = c.Ships[target.Definition]; var weapon = FleetWeapons.Weapons(c, ship).Single(w => w.Id == combat.Weapon);
        int distance = new Hex(unit.Q, unit.R).Distance(new(target.Q, target.R));
        bool rear = FleetRuleTools.Class(defender) is "frigate" or "capital" && new Hex(unit.Q, unit.R).Distance(new Hex(target.Q, target.R).Neighbor(target.Facing + 3)) == distance - 1;
        bool front = new Hex(unit.Q, unit.R).Distance(new Hex(target.Q, target.R).Neighbor(target.Facing)) == distance - 1;
        int armor = Math.Max(0, defender.Armor + (FleetRuleTools.Has(target, "brace") ? 1 : 0) + (defender.ProwArmor && front ? 1 : 0)
            - (rear ? 1 : 0) - target.FocusFire - weapon.Penetrating - (combat.Power ? weapon.PowerPenetrating : 0));
        int successes = combat.Rolls.Count(d => d >= ship.Success); int killed = Math.Min(target.Models, Math.Max(0, successes - armor));
        int focus = FleetRuleTools.Class(defender) != "fighter" ? combat.Rolls.Count(d => d == 1) : 0;
        var survivor = target with { Models = target.Models - killed, FocusFire = Math.Max(0, target.FocusFire + focus - killed),
            Effects = focus > 0 ? (target.Effects ?? []).Except(FleetRuleTools.Buffs).ToArray() : target.Effects };
        var units = s.Units.Where(u => u.Id != target.Id).ToList(); if (survivor.Models > 0) units.Add(survivor);
        bool finished = !units.Any(u => u.Side == 0) || !units.Any(u => u.Side == 1);
        string message = $"{FleetPresentation.UnitName(c, s, unit)} → {FleetPresentation.UnitName(c, s, target)}: [{string.Join(", ", combat.Rolls)}], броня {armor}, потери {killed}, Focus Fire +{focus}.";
        return new(true, message, s with { Combat = null, Units = units, Finished = finished, Winner = finished ? units.Any(u => u.Side == 0) ? 0 : 1 : null });
    }
    public static CommandResult Apply(ContentCatalog c, BattleState s, int side, BattleCommand cmd)
    {
        CommandResult Reject(string message) => new(false, message, s);
        var combat = s.Combat!;
        if (side != combat.Side) return Reject("Решение о броске принимает атакующая сторона.");
        if (cmd.Kind == "resolve")
        {
            var result = Resolve(c, s);
            return InterruptRules.CombatResolved(c, s, result.State, result.Message, combat.Interrupt);
        }
        if (cmd.Kind != "valor" && cmd.Kind != "faction-reroll" || cmd.Kind == "valor" && !CanValor(s, side) || cmd.Kind == "faction-reroll" && !CanFaction(c, s, combat))
            return Reject("Этот переброс недоступен.");
        var unit = s.Units.Single(u => u.Id == combat.Attacker); var target = s.Units.Single(u => u.Id == combat.Target);
        var weapon = FleetWeapons.Weapons(c, c.Ships[unit.Definition]).Single(w => w.Id == combat.Weapon); uint random = s.RandomState;
        var rolls = Roll(c, s, unit, target, weapon, combat.Rolls.Length, ref random);
        var next = s with { Combat = combat with { Rolls = rolls, Rerolled = true }, RandomState = random, Sequence = s.Sequence + 1 };
        if (cmd.Kind == "valor") { int used = s.ValorUsedMask | (1 << side); next = next with { ValorUsedMask = used, ValorOwner = used == 3 ? -1 : 1 - side }; }
        else next = next with { FactionUsed0 = s.FactionUsed0 || side == 0, FactionUsed1 = s.FactionUsed1 || side == 1 };
        return new(true, $"Переброс [{string.Join(", ", rolls)}]. Подтвердите результат.", next);
    }
    internal static void Validate(ContentCatalog c, BattleState s)
    {
        if (s.Combat is not { } combat) return;
        var unit = s.Units.Find(u => u.Id == combat.Attacker && u.Side == combat.Side); var target = s.Units.Find(u => u.Id == combat.Target && u.Side != combat.Side);
        if (!FleetRuleTools.Enabled(c, s) || s.Finished || s.Cards?.Response == null || unit == null || target == null || combat.Rolls == null
            || combat.Rolls.Length is < 1 or > 10000 || combat.Rolls.Any(d => d is < 1 or > 6) || FleetWeapons.Select(c, s, unit, target, combat.Weapon) == null
            || combat.Side != (combat.Interrupt ? 1 - s.Cards.Response.Side : s.Cards.Response.Side)) throw new InvalidDataException("Invalid pending combat.");
    }
}
