using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private string _launchUnit = "", _productionShip = "";
    private Hex _reinforceCell = new(-1, -1);
    private bool _productionToBay;
    private int _reserveSpend;
    private bool DrawReinforcePhase(Control parent)
    {
        if (_state?.Reinforce is not { } frame) return false;
        Text(parent, "ПОДКРЕПЛЕНИЯ / REINFORCE", 15, _colors.Highlight);
        Text(parent, frame.Mode switch { "deploy" => "Ввод из резерва в свою зону развёртывания.", "build" => "Производство одного нового отряда.", _ => "Выпуск готовых отрядов из ангаров до лимита Launch Tubes." }, 14);
        Text(parent, "Карта и Play уже потрачены. Отряды прибывают активированными. Выпуск не активирует носитель.", 13);
        if (LocalCommandSide == frame.Side) Button(parent, "ЗАВЕРШИТЬ ПРИКАЗ", () => Submit(new("finish-reinforce")));
        else Text(parent, "Ожидается размещение противника.", 14);
        return true;
    }
    private Hex[] ReinforceCells()
    {
        var frame = _state!.Reinforce!; var parent = _state.Units.Find(u => u.Id == frame.Source);
        var cells = frame.Mode == "deploy" ? FleetLogistics.DeployCells(_content, _state, frame.Side) : parent == null ? [] : FleetLogistics.Adjacent(_content, _state, parent);
        var arriving = frame.Mode == "deploy" ? _state.Reserves?.Find(u => u.Id == frame.Source) : null;
        if (arriving == null) return cells;
        int facing = Math.Max(0, _facingChoice);
        return cells.Where(h => FleetFootprint.CanPlace(_content, _state, arriving, h, facing) && FleetFootprint.Cells(_content.Ships[arriving.Definition], h, facing).All(cells.Contains)).ToArray();
    }
    private void PlaceReinforcement(Hex cell)
    {
        var frame = _state!.Reinforce!;
        Submit(frame.Mode switch
        {
            "deploy" => new("deploy", Q: cell.Q, R: cell.R, Facing: Math.Max(0, _facingChoice), Repair: _repairChoice),
            "launch" => new("launch", _launchUnit, Q: cell.Q, R: cell.R, Facing: Math.Max(0, _facingChoice), Repair: _repairChoice),
            _ => new("build", Definition: _productionShip, Q: cell.Q, R: cell.R, ToBay: _productionToBay, ReserveSpend: _reserveSpend, Facing: Math.Max(0, _facingChoice), Repair: _repairChoice)
        });
    }
    private void DrawLogistics(Control parent, BattleUnit? unit)
    {
        if (!FleetBattle) return;
        Text(parent, $"Резерв ресурсов: {(_state!.ActiveSide == 0 ? _state.Funds0 : _state.Funds1)} ★", 13);
        if (_state.Reinforce is { } frame && _state.Hazard == null && _state.Defense == null && _state.Combat == null && _state.Repairs is not { Length: > 0 })
        {
            if (LocalCommandSide != frame.Side) return;
            var source = _state.Units.Find(u => u.Id == frame.Source);
            if (source != null) Text(parent, $"Ангары: {FleetLogistics.Children(_state, source.Id).Length}/{_content.Ships[source.Definition].Bays} · Выпуск: {source.Launches}/{_content.Ships[source.Definition].LaunchTubes}", 13);
            if (frame.Mode == "launch")
            {
                var ready = FleetLogistics.Children(_state, frame.Source).Where(u => !u.Activated).ToArray();
                if (!ready.Any(u => u.Id == _launchUnit)) _launchUnit = ready.FirstOrDefault()?.Id ?? "";
                var dropdown = new OptionButton(); foreach (var child in ready) dropdown.AddItem(FleetPresentation.UnitName(_content, _state, child));
                if (ready.Length > 0) dropdown.Select(Array.FindIndex(ready, u => u.Id == _launchUnit)); dropdown.ItemSelected += i => _launchUnit = ready[(int)i].Id; parent.AddChild(dropdown);
            }
            if (frame.Mode == "build")
            {
                var factory = _state.Units.Single(u => u.Id == frame.Source); var buildable = FleetLogistics.Buildable(_content, _state, factory);
                if (!buildable.Contains(_productionShip)) _productionShip = buildable.FirstOrDefault() ?? "";
                var dropdown = new OptionButton(); foreach (string id in buildable) dropdown.AddItem(FleetPresentation.ShipName(_content.Ships[id]) + $" · {_content.Ships[id].Stars} ★");
                if (buildable.Length == 0) { Text(parent, "Нет доступных профилей производства.", 13); return; }
                dropdown.Select(Array.IndexOf(buildable, _productionShip)); dropdown.ItemSelected += i => { _productionShip = buildable[(int)i]; DrawBattle(); }; parent.AddChild(dropdown);
                int cost = _content.Ships[_productionShip].Stars, funds = frame.Side == 0 ? _state.Funds0 : _state.Funds1;
                _reserveSpend = Math.Clamp(Math.Max(_reserveSpend, cost - factory.Cargo), 0, Math.Min(cost, funds));
                Text(parent, $"Цена: {cost} ★. Груз фабрики: {factory.Cargo} ★. Остаток цены оплачивается из груза.", 13);
                var reserve = new SpinBox { MinValue = 0, MaxValue = Math.Min(cost, funds), Step = 1, Value = _reserveSpend, Prefix = "Из резерва: " }; reserve.ValueChanged += value => _reserveSpend = (int)value; parent.AddChild(reserve);
                bool bay = FleetRuleTools.Strike(_content.Ships[_productionShip]) && FleetLogistics.Children(_state, factory.Id).Length < _content.Ships[factory.Definition].Bays;
                if (!bay) _productionToBay = false;
                var docked = new CheckBox { Text = "Построить в ангар", ButtonPressed = _productionToBay, Disabled = !bay }; docked.Toggled += value => { _productionToBay = value; DrawBattle(); }; parent.AddChild(docked);
                if (_productionToBay) { Button(parent, "ПОСТРОИТЬ В АНГАР", () => PlaceReinforcement(new(-1, -1))); return; }
            }
            var cells = ReinforceCells();
            if (cells.Length == 0) { Text(parent, "Нет свободных гексов. Можно завершить приказ.", 13); return; }
            if (!cells.Contains(_reinforceCell)) _reinforceCell = cells[0];
            Text(parent, "Выбери свободный гекс кнопкой или нажми на поле.", 13);
            var positions = new OptionButton(); foreach (var h in cells) positions.AddItem($"Гекс {h.Q + 1}, {h.R + 1}"); positions.Select(Array.IndexOf(cells, _reinforceCell)); positions.ItemSelected += i => _reinforceCell = cells[(int)i]; parent.AddChild(positions);
            var facing = new OptionButton(); for (int i = 0; i < 6; i++) facing.AddItem($"Курс {i * 60}°"); facing.Select(Math.Max(0, _facingChoice)); facing.ItemSelected += i => _facingChoice = (int)i; parent.AddChild(facing);
            Button(parent, "РАЗМЕСТИТЬ НА ПОЛЕ", () => PlaceReinforcement(_reinforceCell), frame.Mode == "launch" && (source == null || _launchUnit == "" || !FleetLogistics.CanLaunch(_content, _state, source))); return;
        }
        if (unit == null) return;
        var ship = _content.Ships[unit.Definition];
        if (ship.Bays > 0) Text(parent, $"Ангары: {FleetLogistics.Children(_state, unit.Id).Length}/{ship.Bays}\nВыпуск за раунд: {unit.Launches}/{ship.LaunchTubes}", 14);
        foreach (var carrier in FleetLogistics.EmbarkHosts(_content, _state, LocalCommandSide, unit.Id))
        { string target = carrier.Id; Button(parent, "В АНГАР: " + FleetPresentation.UnitName(_content, _state, carrier), () => Submit(new("embark", unit.Id, target))); }
        bool available = !FleetDecision && unit.Side == LocalCommandSide && _state.ActiveSide == LocalCommandSide && _state.Cards is { Phase: "orders", Pending: null, Response: null };
        if (!available) return;
        if ((_state.Reserves ?? []).Any(u => u.Id == unit.Id))
        {
            if (unit.Embarked == "") Button(parent, "ВВЕСТИ ИЗ РЕЗЕРВА", () => Submit(new("begin-reinforce", unit.Id, "deploy")), unit.Activated || FleetLogistics.DeployCells(_content, _state, unit.Side).Length == 0);
            else Text(parent, "В ангаре. Выбери носитель для выпуска этого отряда.", 13);
            return;
        }
        if (FleetLogistics.Children(_state, unit.Id).Any(u => !u.Activated)) Button(parent, "ВЫПУСТИТЬ ИЗ АНГАРА", () => Submit(new("begin-reinforce", unit.Id, "launch")), !FleetLogistics.CanLaunch(_content, _state, unit));
        if (FleetLogistics.Buildable(_content, _state, unit).Length > 0) Button(parent, "ПРОИЗВОДСТВО", () => Submit(new("begin-reinforce", unit.Id, "build")));

    }
}
