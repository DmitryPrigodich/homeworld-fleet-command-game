using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private string _repairInterruptUnit = "";
    private void DrawRepairInterrupt(Control parent)
    {
        if (!FleetBattle || _state!.Finished || _state.Cards is not { Phase: "orders", Counter: null } cards) return;
        int side = LocalCommandSide;
        string card = (side == 0 ? cards.Hand0 : cards.Hand1).FirstOrDefault(id => InterruptRules.IsRepair(_content, _state, id)) ?? "";
        var targets = InterruptRules.RepairTargets(_content, _state, side);
        if (card == "" || targets.Length == 0 || !InterruptRules.Available(_content, _state, side, "repairs")) return;
        if (!targets.Any(u => u.Id == _repairInterruptUnit)) _repairInterruptUnit = targets.FirstOrDefault(u => u.Id == _selected)?.Id ?? targets[0].Id;
        Text(parent, "INTERRUPT REPAIRS", 14, _colors.Highlight);
        Text(parent, "No Play or activation spent.\nDice already rolled are not rerolled.", 12, _colors.Muted);
        var picker = new OptionButton { FitToLongestItem = false }; picker.AddThemeFontSizeOverride("font_size", 14);
        foreach (var unit in targets) picker.AddItem("Unit #" + FleetPresentation.UnitNumber(_content, _state, unit) + " · " + FleetRuleTools.Class(_content.Ships[unit.Definition]));
        picker.Select(Array.FindIndex(targets, u => u.Id == _repairInterruptUnit)); picker.ItemSelected += i => { _repairInterruptUnit = targets[(int)i].Id; DrawBattle(); }; parent.AddChild(picker);
        var target = targets.Single(u => u.Id == _repairInterruptUnit);
        picker.TooltipText = FleetPresentation.UnitName(_content, _state, target);
        Button(parent, "REMOVE FOCUS TOKEN", () => Submit(new("interrupt-repairs", target.Id, Card: card, Repair: "focus")), target.FocusFire == 0);
        Button(parent, "RESTORE ONE FIGHTER", () => Submit(new("interrupt-repairs", target.Id, Card: card, Repair: "fighter")), FleetRuleTools.Class(_content.Ships[target.Definition]) != "fighter" || target.Models >= _content.Ships[target.Definition].Models);
    }
}
