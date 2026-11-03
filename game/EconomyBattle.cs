using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private static void ConstrainEconomyPanel(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is OptionButton option) option.FitToLongestItem = false;
            if (child is Button button) { button.AddThemeFontSizeOverride("font_size", 14); button.ClipText = true; if (button.TooltipText == "") button.TooltipText = button.Text; }
            ConstrainEconomyPanel(child);
        }
    }
    private bool HasHarvestTarget(BattleUnit unit) => _content.Ships[unit.Definition].Harvest >= 0 && _state!.Deposits?.Any(p => !p.Value.Harvested
        && new Hex(unit.Q, unit.R).Distance(new(_content.Maps[_state.Map].Objects[p.Key].Q, _content.Maps[_state.Map].Objects[p.Key].R)) <= _content.Ships[unit.Definition].Harvest) == true;
    private string _cargoTarget = "";
    private int _cargoAmount = 1;
    private bool _cargoReceive;
    private string DepositDescription(int index)
    {
        if (_state == null || _state.Deposits == null || !_state.Deposits.TryGetValue(index, out var deposit)) return "";
        if (deposit.Harvested) return $"Собрано: {deposit.Stars} ★. Повторная добыча невозможна.";
        int? known = FleetEconomy.KnownStars(_state, index, LocalCommandSide);
        return known.HasValue ? $"Запасы: {known.Value} ★ (разведано вашим флотом)." : "Запасы неизвестны. Разведчик раскрывает соседние поля после движения.";
    }
    private bool TryHarvestObject(MapObject placement)
    {
        if (_state?.Cards?.Pending is not { BasicAction: "harvest", Step: 1 } order) return false;
        int index = Array.IndexOf(_content.Maps[_state.Map].Objects, placement);
        if (!FleetEconomy.Targets(_content, _state, LocalCommandSide, SpecialOrders.Actor(order)).Contains(index)) return false;
        Submit(new("harvest", SpecialOrders.Actor(order), Object: index)); return true;
    }
    private void DrawEconomy(Control parent, BattleUnit? unit)
    {
        if (!FleetBattle || unit == null) return;
        var ship = _content.Ships[unit.Definition];
        if (unit.Cargo > 0 || ship.Harvest >= 0) Text(parent, $"Груз ресурсов: {unit.Cargo} ★" + (ship.Harvest >= 0 ? $"\nHarvest {ship.Harvest}" : "") + (ship.Survey ? " · Survey" : ""), 14);
        if (_state!.Cards?.Pending is { BasicAction: "harvest" } order && order.Side == LocalCommandSide && SpecialOrders.Actor(order) == unit.Id && !FleetDecision && _state.Cards.Response == null)
        {
            if (order.Step == 0) Text(parent, "Сначала можно бесплатно переместиться. Чтобы добывать с места, пропусти это действие слева.", 14);
            else
            {
                var targets = FleetEconomy.Targets(_content, _state, LocalCommandSide, unit.Id);
                Text(parent, "Выбери астероиды на карте или кнопкой. Чужие корабли добыче не мешают.", 14);
                foreach (int index in targets)
                {
                    int target = index; var field = _content.Maps[_state.Map].Objects[index]; int? known = FleetEconomy.KnownStars(_state, index, LocalCommandSide);
                    Button(parent, $"ДОБЫТЬ: {field.Q + 1}, {field.R + 1} · " + (known.HasValue ? known.Value + " ★" : "? ★"), () => Submit(new("harvest", unit.Id, Object: target)));
                }
                if (targets.Length == 0) Text(parent, "Несобранных полей в пределах Harvest нет. Можно завершить приказ.", 13);
            }
            return;
        }
        if (FleetDecision || unit.Side != LocalCommandSide || unit.Activated || unit.Hulk || unit.Patrol != "" || _state.ActiveSide != LocalCommandSide || _state.Cards is not { Phase: "orders", Pending: null, Response: null }) return;
        if (ship.Harvest >= 0) Button(parent, "ДОБЫЧА РЕСУРСОВ", () => ChooseBattleAction("harvest"));
        var neighbors = _state.Units.Where(u => u.Side == unit.Side && !u.Hulk && u.Patrol == "" && new Hex(u.Q, u.R).Distance(new(unit.Q, unit.R)) == 1 && (unit.Cargo > 0 || u.Cargo > 0)).ToArray();
        if (neighbors.Length == 0) return;
        Text(parent, "ПЕРЕДАЧА РЕСУРСОВ", 14, _colors.Highlight);
        if (!neighbors.Any(u => u.Id == _cargoTarget)) _cargoTarget = neighbors[0].Id;
        var other = neighbors.Single(u => u.Id == _cargoTarget);
        var targetsMenu = new OptionButton(); foreach (var target in neighbors) targetsMenu.AddItem(FleetPresentation.UnitName(_content, _state, target) + $" · {target.Cargo} ★");
        targetsMenu.Select(Array.FindIndex(neighbors, u => u.Id == _cargoTarget)); targetsMenu.ItemSelected += i => { _cargoTarget = neighbors[(int)i].Id; DrawBattle(); }; parent.AddChild(targetsMenu);
        var receive = new CheckBox { Text = "Забрать у соседа", ButtonPressed = _cargoReceive };
        receive.Toggled += value => { _cargoReceive = value; DrawBattle(); }; parent.AddChild(receive);
        int maximum = _cargoReceive ? other.Cargo : unit.Cargo; _cargoAmount = Math.Clamp(_cargoAmount, 1, Math.Max(1, maximum));
        var amount = new SpinBox { MinValue = 1, MaxValue = Math.Max(1, maximum), Step = 1, Value = _cargoAmount, Prefix = "Ресурсы: " }; amount.ValueChanged += value => _cargoAmount = (int)value; parent.AddChild(amount);
        Text(parent, "Одна карта и один Play. Активируется выбранный корабль; сосед может уже действовать.", 12, _colors.Muted);
        Button(parent, "ПЕРЕДАТЬ РЕСУРСЫ", () => Submit(new("transfer-stars", unit.Id, _cargoTarget, Amount: _cargoAmount, Receive: _cargoReceive, Repair: _repairChoice)), maximum == 0);
    }
}
