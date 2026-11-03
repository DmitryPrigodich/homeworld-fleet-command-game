namespace FleetCommand.Core;

public sealed record PendingCombat(int Side, string Attacker, string Target, string Weapon, bool Power, int[] Rolls, bool Interrupt = false,
    bool Rerolled = false, string Kind = "ranged", BattleUnit? Ghost = null, string Effect = "weapons", int AbilityRerolls = 0, int RerollSide = -1);
public static class FleetCombat
{
    public static BattleUnit Actor(BattleState s, PendingCombat combat) => combat.Ghost ?? FleetTerrain.Find(s, combat.Attacker)!;
    public static BattleUnit Target(BattleState s, PendingCombat combat) => FleetTerrain.Find(s, combat.Target)!;
    private static WeaponDefinition Weapon(ContentCatalog c, BattleState s, PendingCombat combat)
    {
        var ship = c.Ships[Actor(s, combat).Definition];
        return combat.Kind is "ranged" or "defense" or "cap" or "dogfight" or "terrain" or "terrain-dogfight" ? FleetWeapons.Weapons(c, ship).Single(w => w.Id == combat.Weapon)
            : new(combat.Kind, combat.Kind, combat.Kind == "board" ? ship.Crew : ship.Ram, 1, Kind: "close");
    }
    private static int[] Roll(int dice, ref uint random)
    {
        var rolls = new int[dice]; for (int i = 0; i < dice; i++) rolls[i] = FleetRuleTools.Roll(ref random);
        return rolls;
    }
    public static int DecisionSide(PendingCombat combat) => combat.AbilityRerolls > 0 ? combat.RerollSide : combat.Side;
    public static int Success(ContentCatalog c, BattleState s, PendingCombat combat)
    {
        var weapon = Weapon(c, s, combat); var ship = c.Ships[Actor(s, combat).Definition];
        return weapon.Barrage > 0 ? weapon.Barrage : Actor(s, combat) is { CapUsed: true, Patrol: not "" } && ship.CapSuccess > 0 ? ship.CapSuccess : ship.Success;
    }
    public static int[] EligibleDice(ContentCatalog c, BattleState s)
    {
        var combat = s.Combat!; int success = Success(c, s, combat);
        return Enumerable.Range(0, combat.Rolls.Length).Where(i => combat.RerollSide == combat.Side ? combat.Rolls[i] < success : combat.Rolls[i] >= success).ToArray();
    }
    private static BattleState Abilities(ContentCatalog c, BattleState s)
    {
        var combat = s.Combat!; var unit = Actor(s, combat); var target = Target(s, combat);
        var defender = c.Ships[target.Definition]; var weapon = Weapon(c, s, combat);
        bool projectile = combat.Kind is "defense" or "cap" && FleetTerrain.Diffuse(s.Defense!.Threat);
        string targetClass = projectile ? s.Defense!.Threat : FleetRuleTools.Class(defender);
        int rapid = weapon.RapidTargets == null || weapon.RapidTargets.Contains(targetClass) ? weapon.RapidFire : 0;
        int evasive = projectile ? s.Defense!.Threat == "drone" && s.Defense.Attack.Power ? 1 : 0 : defender.Evasive + (FleetRuleTools.Has(target, "evasive") ? 1 : 0);
        int rerolls = combat.Kind == "collision" || weapon.Barrage > 0 ? 0 : rapid - evasive;
        var next = s with { Combat = combat with { AbilityRerolls = Math.Abs(rerolls), RerollSide = rerolls < 0 ? target.Side : unit.Side } };
        int count = Math.Min(Math.Abs(rerolls), EligibleDice(c, next).Length);
        return next with { Combat = next.Combat! with { AbilityRerolls = count, RerollSide = count == 0 ? -1 : next.Combat.RerollSide } };
    }
    public static bool CanValor(BattleState s, int side) => s.Combat?.Kind is not ("terrain" or "terrain-dogfight") && s.Combat?.AbilityRerolls is not > 0 && s.ValorOwner == side && (s.ValorUsedMask & (1 << side)) == 0 && s.Combat?.Rerolled != true;
    public static bool CanFaction(ContentCatalog c, BattleState s, PendingCombat combat) => combat.AbilityRerolls == 0 && (combat.Kind is "ranged" or "ram" or "board" or "cap" or "dogfight") && !combat.Rerolled && !(combat.Side == 0 ? s.FactionUsed0 : s.FactionUsed1)
        && FleetRuleTools.Faction(c, s, combat.Side) == "taiidan";
    public static bool Allowed(ContentCatalog c, BattleState s, BattleUnit unit, BattleUnit? target, BattleCommand cmd) => !unit.Hulk && unit.Patrol == "" &&
        (cmd.AttackKind == "ranged" ? FleetWeapons.Attack(c, s, unit, target, cmd.Weapon).Allowed : FleetClose.Allowed(c, s, unit, target, cmd.AttackKind));
    internal static CommandResult Begin(ContentCatalog c, BattleState s, int side, BattleCommand cmd, bool interrupt)
    {
        var unit = s.Units.Single(u => u.Id == cmd.Unit); var target = s.Units.Find(u => u.Id == cmd.Target);
        if (!Allowed(c, s, unit, target, cmd) || !FleetRuleTools.Debuffs.Contains(cmd.Effect)) return new(false, "Выбранная атака недоступна.", s);
        return FleetDefense.Offer(c, s, side, cmd, interrupt) ?? RollAttack(c, s, side, cmd, interrupt);
    }
    public static int AttackDice(ContentCatalog c, BattleState s, BattleCommand cmd)
    {
        var unit = FleetTerrain.Find(s, cmd.Unit)!; var target = FleetTerrain.Find(s, cmd.Target)!;
        var ship = c.Ships[unit.Definition];
        var weapon = cmd.AttackKind == "ranged" ? FleetWeapons.Select(c, s, unit, target!, cmd.Weapon)!
            : new WeaponDefinition(cmd.AttackKind, cmd.AttackKind, cmd.AttackKind == "board" ? ship.Crew : ship.Ram, 1, Kind: "close");
        int dice = weapon.Barrage > 0 ? target.Models : weapon.DicePerModel * unit.Models + (cmd.Power ? weapon.PowerDice : 0) + (unit.Leader == "" ? 0 : c.Leaders[unit.Leader].AttackDice);
        if (cmd.AttackKind == "ranged") dice -= FleetWeapons.Attack(c, s, unit, target, weapon.Id).Cover;
        if (cmd.AttackKind == "board") dice -= unit.CrewDamage;
        if (cmd.AttackKind == "ram" && FleetRuleTools.Strike(ship)) dice += SpecialOrders.Lightning(ship);
        return Math.Max(1, dice);
    }
    internal static CommandResult RollAttack(ContentCatalog c, BattleState s, int side, BattleCommand cmd, bool interrupt, int reduction = 0)
    {
        var unit = FleetTerrain.Find(s, cmd.Unit)!; var target = FleetTerrain.Find(s, cmd.Target)!;
        string weapon = cmd.AttackKind == "ranged" ? FleetWeapons.Select(c, s, unit, target, cmd.Weapon)!.Id : cmd.AttackKind;
        uint random = s.RandomState; var rolls = Roll(Math.Max(1, AttackDice(c, s, cmd) - reduction), ref random);
        bool terrain = s.Hazard?.Source.Id == unit.Id;
        var combat = new PendingCombat(side, unit.Id, target.Id, weapon, cmd.Power, rolls, interrupt, Kind: terrain ? "terrain" : cmd.AttackKind, Effect: cmd.Effect, Ghost: terrain ? unit : null);
        return WaitOrResolve(c, Abilities(c, s with { Combat = combat, RandomState = random }));
    }
    internal static CommandResult DefensiveRoll(ContentCatalog c, BattleState s, WeaponDefinition weapon, int dice)
    {
        var frame = s.Defense!; uint random = s.RandomState;
        var combat = new PendingCombat(1 - frame.Side, frame.Attack.Target, frame.Attack.Unit, weapon.Id, false, Roll(Math.Max(1, dice), ref random), frame.Interrupt, Kind: "defense");
        return WaitOrResolve(c, Abilities(c, s with { Combat = combat, RandomState = random }));
    }
    internal static CommandResult ReactionRoll(ContentCatalog c, BattleState s, BattleUnit unit, BattleUnit target, WeaponDefinition weapon, string kind)
    {
        uint random = s.RandomState; var combat = PrepareReaction(c, s, unit, target, weapon, kind, ref random);
        return ReactionStart(c, s with { RandomState = random }, combat);
    }
    internal static PendingCombat PrepareReaction(ContentCatalog c, BattleState s, BattleUnit unit, BattleUnit target, WeaponDefinition weapon, string kind, ref uint random)
    {
        int dice = weapon.Barrage > 0 ? target.Models : weapon.DicePerModel * unit.Models + (unit.Leader == "" ? 0 : c.Leaders[unit.Leader].AttackDice);
        return new(unit.Side, unit.Id, target.Id, weapon.Id, false, Roll(Math.Max(1, dice), ref random), s.Defense!.Interrupt, Kind: kind);
    }
    internal static CommandResult ReactionStart(ContentCatalog c, BattleState s, PendingCombat combat) => WaitOrResolve(c, Abilities(c, s with { Combat = combat }));
    internal static CommandResult ReactionDamage(ContentCatalog c, BattleState s, PendingCombat combat) => Resolve(c, s with { Combat = combat }, reactionDamage: true);
    private static CommandResult WaitOrResolve(ContentCatalog c, BattleState s)
    {
        var combat = s.Combat!;
        if (combat.AbilityRerolls > 0) return new(true, $"Выберите до {combat.AbilityRerolls} кубиков для способности.", s);
        bool crippleChoice = combat.Kind == "ram" && FleetRuleTools.Class(c.Ships[Actor(s, combat).Definition]) == "fighter"
            && combat.Rolls.Any(d => d >= c.Ships[Actor(s, combat).Definition].Success);
        if (!combat.Rerolled && !CanValor(s, combat.Side) && !CanFaction(c, s, combat) && !CanCapture(c, s) && !crippleChoice) return Resolve(c, s);
        return new(true, $"{combat.Kind}: [{string.Join(", ", combat.Rolls)}]. Подтвердите результат или используйте переброс.", s);
    }
    private static int Armor(ContentCatalog c, BattleState s, PendingCombat combat, BattleUnit target)
    {
        var unit = Actor(s, combat); var defender = c.Ships[target.Definition]; var weapon = Weapon(c, s, combat);
        if (combat.Kind == "board") return Math.Max(0, defender.Armor - target.CrewDamage - target.FocusFire / 2 + (FleetRuleTools.Has(target, "lockdown") ? 1 : 0));
        if (combat.Kind == "defense" && s.Defense!.Threat == "fighter") return 0;
        if (combat.Kind == "collision") return defender.Armor + (defender.ProwArmor ? 1 : 0) + (FleetRuleTools.Has(target, "brace") ? 1 : 0);
        int distance = new Hex(unit.Q, unit.R).Distance(new(target.Q, target.R));
        bool rear = FleetRuleTools.Class(defender) is "frigate" or "capital" && new Hex(unit.Q, unit.R).Distance(new Hex(target.Q, target.R).Neighbor(target.Facing + 3)) == distance - 1;
        bool front = new Hex(unit.Q, unit.R).Distance(new Hex(target.Q, target.R).Neighbor(target.Facing)) == distance - 1;
        return Math.Max(0, defender.Armor + (FleetRuleTools.Has(target, "brace") ? 1 : 0) + (defender.ProwArmor && front ? 1 : 0)
            - (rear ? 1 : 0) - target.FocusFire - weapon.Penetrating - (combat.Power ? weapon.PowerPenetrating : 0));
    }
    public static bool CanCapture(ContentCatalog c, BattleState s)
    {
        if (s.Combat is not { Kind: "board" } combat) return false;
        var target = s.Units.Single(u => u.Id == combat.Target);
        return FleetLogistics.Children(s, target.Id).Length == 0 && FleetCap.Patrol(s, target.Id) == null && FleetClose.CaptureAllowed(c, s, target) && combat.Rolls.Count(d => d >= c.Ships[Actor(s, combat).Definition].Success) - Armor(c, s, combat, target) >= target.Models;
    }
    private static CommandResult Resolve(ContentCatalog c, BattleState s, bool capture = false, bool reactionDamage = false)
    {
        var combat = s.Combat!; var unit = Actor(s, combat); var target = Target(s, combat);
        var ship = c.Ships[unit.Definition]; var defender = c.Ships[target.Definition]; int armor = Armor(c, s, combat, target);
        int successes = combat.Rolls.Count(d => d >= Success(c, s, combat)), killed = Math.Min(target.Models, Math.Max(0, successes - armor));
        if (combat.Kind is "cap" or "dogfight" or "terrain-dogfight" && !reactionDamage) return FleetCap.RecordVolley(c, s, successes);
        if (combat.Kind == "defense" && s.Hazard != null) return FleetTerrain.Defended(s, successes);
        if (combat.Kind == "defense" && FleetDefense.Projectile(s.Defense!.Threat))
            return new(true, $"Защитный огонь: [{string.Join(", ", combat.Rolls)}], входящая атака -{successes} кубиков.", s with { Combat = null, Defense = s.Defense with { Reduction = s.Defense.Reduction + successes } });
        int ones = FleetRuleTools.Class(defender) != "fighter" ? combat.Rolls.Count(d => d == 1) : 0;
        int focus = combat.Kind == "board" ? 0 : ones, crew = combat.Kind == "board" ? ones : 0;
        var survivor = target with { Models = target.Models - killed, FocusFire = Math.Max(0, target.FocusFire + focus - killed), CrewDamage = target.CrewDamage + crew,
            Effects = ones > 0 ? (target.Effects ?? []).Except(FleetRuleTools.Buffs).ToArray() : target.Effects };
        if (combat.Kind == "ram" && FleetRuleTools.Class(ship) == "fighter" && successes > 0)
            survivor = survivor with { Effects = (survivor.Effects ?? []).Append(combat.Effect).Distinct().ToArray() };
        var next = s with { Combat = null, Units = s.Units.Where(u => u.Id != target.Id).ToList() };
        bool captured = capture && killed == target.Models;
        if (captured)
        {
            next = FleetObjectives.RecordCapture(c, next, target.Id, unit.Side);
            // Retain the last ship instead of removing the unit; activation state is retained.
            survivor = survivor with { Side = unit.Side, Models = 1, CrewDamage = Math.Min(survivor.CrewDamage, defender.Armor) };
            next = next with { Units = next.Units.Select(u => u.Id == unit.Id ? FleetClose.CheckHulk(c, u with { CrewDamage = u.CrewDamage + 1, Effects = (u.Effects ?? []).Except(FleetRuleTools.Buffs).ToArray() }) : u).ToList() };
        }
        if (survivor.Models > 0) next.Units.Add(FleetClose.CheckHulk(c, survivor));
        else next = FleetClose.Award(c, next, target, combat.Side);
        if (combat.Kind == "terrain") next = next with { Hazard = next.Hazard! with { DamageReduction = killed > 0 || focus > 0 } };
        next = FleetCap.Sync(s, next);
        if (combat.Kind is "ranged" or "ram" or "board") next = next with { Units = next.Units.Select(u => u.Id == unit.Id ? u with { AttackWindow = FleetClose.Window(s), LastAction = combat.Kind, LastTarget = target.Id, LastActionRound = s.Round } : u).ToList() };
        bool fighterSuicide = combat.Kind == "ram" && FleetRuleTools.Class(ship) == "fighter" && successes > 0;
        if (fighterSuicide) { next = FleetClose.Award(c, next, unit, 1 - unit.Side); next = next with { Units = next.Units.Where(u => u.Id != unit.Id).ToList() }; }
        string message = $"{FleetPresentation.UnitName(c, s, unit)} → {FleetPresentation.UnitName(c, s, target)} ({combat.Kind}): [{string.Join(", ", combat.Rolls)}], броня {armor}, потери {killed}, Focus Fire +{focus}, Crew Damage +{crew}." + (captured ? " Цель захвачена." : "");
        if (combat.Kind is not ("cap" or "dogfight" or "terrain-dogfight")) next = FleetObjectives.Check(c, next);
        if (next.Finished) return new(true, message, next);
        if (combat.Kind is "defense" or "cap" or "dogfight" or "terrain-dogfight") return new(true, message, next);
        if (combat.Kind == "ram" && !fighterSuicide)
        {
            uint random = next.RandomState;
            var rolls = Roll(Math.Max(1, 2 * defender.Armor), ref random);
            next = next with { RandomState = random, Finished = false, Winner = null, Combat = new(target.Side, target.Id, unit.Id, "collision", false, rolls, combat.Interrupt, Kind: "collision", Ghost: target) };
            var collision = WaitOrResolve(c, next); return collision with { Message = message + " " + collision.Message };
        }
        next = FleetClose.Outcome(next, c);
        if (combat.Kind == "board" && survivor.Models == 0) next = FleetClose.OfferOverrun(c, next, unit, target, combat.Interrupt);
        if (combat.Kind == "collision" && combat.Ghost != null && !next.Units.Any(u => u.Id == combat.Ghost.Id)) next = FleetClose.OfferOverrun(c, next, target, combat.Ghost, combat.Interrupt);
        return new(true, message, next);
    }
    public static CommandResult Apply(ContentCatalog c, BattleState s, int side, BattleCommand cmd)
    {
        CommandResult Reject(string message) => new(false, message, s);
        var combat = s.Combat!;
        if (side != DecisionSide(combat)) return Reject("Решение о броске принимает владелец соответствующей способности.");
        if (combat.AbilityRerolls > 0)
        {
            var dice = cmd.Dice ?? []; var eligible = EligibleDice(c, s);
            if (cmd.Kind != "ability-reroll" || dice.Length > combat.AbilityRerolls || dice.Distinct().Count() != dice.Length || dice.Any(i => !eligible.Contains(i))) return Reject("Выберите допустимые результаты для переброса.");
            uint abilityRandom = s.RandomState; var abilityRolls = (int[])combat.Rolls.Clone(); foreach (int i in dice) abilityRolls[i] = FleetRuleTools.Roll(ref abilityRandom);
            var afterAbility = s with { Combat = combat with { Rolls = abilityRolls, AbilityRerolls = 0, RerollSide = -1 }, RandomState = abilityRandom };
            return Finish(c, s, WaitOrResolve(c, afterAbility), combat);
        }
        if (cmd.Kind == "cripple-choice")
        {
            if (combat.Kind != "ram" || FleetRuleTools.Class(c.Ships[Actor(s, combat).Definition]) != "fighter" || !FleetRuleTools.Debuffs.Contains(cmd.Effect)) return Reject("Выбор повреждения недоступен.");
            return new(true, "Выбрано повреждение: " + cmd.Effect + ".", s with { Combat = combat with { Effect = cmd.Effect }, Sequence = s.Sequence + 1 });
        }
        if (cmd.Kind is "resolve" or "capture-result")
        {
            if (cmd.Kind == "capture-result" && !CanCapture(c, s)) return Reject("Эта цель не может быть захвачена этим броском.");
            var result = Resolve(c, s, cmd.Kind == "capture-result");
            return Finish(c, s, result, combat);
        }
        if (cmd.Kind != "valor" && cmd.Kind != "faction-reroll" || cmd.Kind == "valor" && !CanValor(s, side) || cmd.Kind == "faction-reroll" && !CanFaction(c, s, combat)) return Reject("Этот переброс недоступен.");
        uint random = s.RandomState;
        var rolls = Roll(combat.Rolls.Length, ref random);
        var next = Abilities(c, s with { Combat = combat with { Rolls = rolls, Rerolled = true }, RandomState = random, Sequence = s.Sequence + 1 });
        if (cmd.Kind == "valor") { int used = s.ValorUsedMask | (1 << side); next = next with { ValorUsedMask = used, ValorOwner = used == 3 ? -1 : 1 - side }; }
        else next = next with { FactionUsed0 = s.FactionUsed0 || side == 0, FactionUsed1 = s.FactionUsed1 || side == 1 };
        return new(true, $"Переброс [{string.Join(", ", rolls)}]. Подтвердите результат.", next);
    }
    private static CommandResult Finish(ContentCatalog c, BattleState before, CommandResult result, PendingCombat combat) =>
        result.State.Finished && FleetObjectives.Enabled(c, result.State) ? result with { State = FleetObjectives.Close(result.State) with { Sequence = before.Sequence + 1 } } :
        result.State.Combat != null || result.State.Overrun != null ? result with { State = result.State with { Sequence = before.Sequence + 1 } }
            : combat.Kind == "terrain" ? FleetTerrain.AfterDamage(c, before, result) : combat.Kind == "defense" ? FleetDefense.AfterRoll(c, before, result) : combat.Kind is "cap" or "dogfight" or "terrain-dogfight" ? FleetCap.AfterVolley(c, before, result) : InterruptRules.CombatResolved(c, before, result.State, result.Message, combat.Interrupt);
    internal static void Validate(ContentCatalog c, BattleState s)
    {
        if (s.Combat is not { } combat) return;
        var unit = combat.Ghost ?? FleetTerrain.Find(s, combat.Attacker); var target = FleetTerrain.Find(s, combat.Target);
        int sourceSide = s.Hazard?.Source.Side ?? (s.Cards?.Response == null ? -1 : combat.Interrupt ? 1 - s.Cards.Response.Side : s.Cards.Response.Side);
        bool legal = unit != null && target != null && (combat.Kind == "collision" ? combat.Ghost != null && combat.Ghost.Id == combat.Attacker && combat.Ghost.Side == combat.Side && c.Ships.ContainsKey(combat.Ghost.Definition)
            : combat.Kind == "terrain" ? s.Hazard != null && combat.Ghost == FleetTerrain.Source(s) && combat.Target == s.Hazard.Job.Unit
            : combat.Ghost == null && (combat.Kind == "defense" ? s.Defense is { Fired: true } frame && frame.Attack.Unit == target.Id && frame.Attack.Target == unit.Id && FleetDefense.Weapons(c, unit).Any(w => w.Id == combat.Weapon)
                : combat.Kind is "cap" or "dogfight" or "terrain-dogfight" ? FleetCap.ValidVolley(c, s, combat)
                : combat.Kind == "ranged" ? FleetWeapons.Select(c, s, unit, target, combat.Weapon) != null : combat.Kind is "ram" or "board" && new Hex(unit.Q, unit.R).Distance(new(target.Q, target.R)) == 1));
        if (!FleetRuleTools.Enabled(c, s) || s.Finished || s.Cards?.Response == null && s.Hazard == null || !legal || combat.Rolls == null || s.Overrun != null || unit!.Side != combat.Side || target!.Side == combat.Side
            || combat.Rolls.Length is < 1 or > 10000 || combat.Rolls.Any(d => d is < 1 or > 6) || !FleetRuleTools.Debuffs.Contains(combat.Effect)
            || combat.AbilityRerolls < 0 || combat.AbilityRerolls > combat.Rolls.Length || combat.AbilityRerolls > 0 && (combat.RerollSide is < 0 or > 1 || EligibleDice(c, s).Length < combat.AbilityRerolls)
            || combat.Side != (combat.Kind is "collision" or "defense" or "cap" ? 1 - sourceSide : sourceSide)) throw new InvalidDataException("Invalid pending combat.");
    }
}
