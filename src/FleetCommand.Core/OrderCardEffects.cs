namespace FleetCommand.Core;

public static partial class SpecialOrders
{
    public static bool IsAttackAction(string action) => action is "attack" or "power" or "board";
    private static bool RestrictedTarget(ContentCatalog c, BattleState s, PendingOrder order) => order.BasicAction == ""
        && CommandCards.Find(c, s, order.Card)!.SameTarget && Step(c, s, order).Line == 1;
    private static BattleState ChooseAction(ContentCatalog c, BattleState s, PendingOrder order, string action) =>
        order.BasicAction == "" && Step(c, s, order).Action == "choice" ? s with { Cards = s.Cards! with { Pending = order with { Choice = action } } } : s;
    public static bool CanAssign(ContentCatalog c, BattleState s, CardDefinition card, BattleUnit? unit) => unit != null
        && (FleetLaunchOrders.IsLaunch(card) ? FleetLaunchOrders.CanAssign(c, s, unit) : unit.Embarked == "")
        && (card.HeroicOwner == "" || FleetRuleTools.LeaderIds(unit).Contains(card.HeroicOwner))
        && (!card.ExcludeFighters || FleetRuleTools.Class(c.Ships[unit.Definition]) != "fighter")
        && (!card.RequireAfterburners || Lightning(c.Ships[unit.Definition]) > 0)
        && (FleetRuleTools.Enabled(c, s) || card.SelfBuff == "" && card.AttackEffect == "" && !card.RepairAtStart
            && !(card.Lines ?? []).SelectMany(l => l.Actions).Any(a => a is "power" or "choice" or "board"));
    private static string CardRepair(BattleUnit unit, string choice) => choice == "auto"
        ? unit.FocusFire > 0 ? "focus" : FleetRuleTools.Debuffs.FirstOrDefault(e => FleetRuleTools.Has(unit, e)) ?? "" : choice;
    private static bool ValidCardRepair(BattleUnit unit, string choice)
    {
        choice = CardRepair(unit, choice);
        return choice == "" || choice == "focus" && unit.FocusFire > 0 || FleetRuleTools.Debuffs.Contains(choice) && FleetRuleTools.Has(unit, choice);
    }
    private static BattleState ApplyStartEffect(BattleState s, CardDefinition card, string actor, string repair, out string message)
    {
        message = "";
        if (card.SelfBuff == "" && !card.RepairAtStart) return s;
        var unit = s.Units.Single(u => u.Id == actor);
        if (card.RepairAtStart)
        {
            repair = CardRepair(unit, repair);
            if (repair == "focus") unit = unit with { FocusFire = Math.Max(0, unit.FocusFire - 2) };
            else if (repair != "") unit = unit with { Effects = (unit.Effects ?? []).Where(e => e != repair).ToArray() };
            if (repair != "") message += "Card repair: removed " + repair + ". ";
        }
        if (card.SelfBuff != "") { unit = unit with { Effects = (unit.Effects ?? []).Append(card.SelfBuff).Distinct().ToArray() }; message += "Card effect: " + FleetPresentation.EffectName(card.SelfBuff) + ". "; }
        return s with { Units = s.Units.Select(u => u.Id == actor ? unit : u).ToList() };
    }
    internal static string AttackEffect(ContentCatalog c, BattleState s, string actor) => s.Cards?.Pending is { BasicAction: "" } order
        && Actor(c, s, order) == actor && IsAttackAction(Action(c, s, order)) ? CommandCards.Find(c, s, order.Card)!.AttackEffect : "";
    internal static int BoardingBonus(ContentCatalog c, BattleState s, string actor) => s.Cards?.Pending is { BasicAction: "" } order
        && Actor(c, s, order) == actor && Action(c, s, order) == "board" ? CommandCards.Find(c, s, order.Card)!.BoardingDice : 0;
}
