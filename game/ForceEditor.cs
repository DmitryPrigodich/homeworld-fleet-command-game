using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;

public partial class Main
{
    private ForcePlan[] _forcePlans = [ForceBattle.PracticeForce("kushan"), ForceBattle.PracticeForce("taiidan")];
    private int _forceSide, _forceWidth = 12, _forceHeight = 8, _forceObjects = 8, _forceSeed = 12345, _forceMode;
    private string _forceUnit = "u1", _forceNotice = "", _forceAddress = "127.0.0.1", _forceBackground = "";
    private bool _forceNetwork;
    private VBoxContainer? _forceReport;
    private Button? _forceLaunch;
    private string ForceDirectory => System.IO.Path.Combine(_data, "fleets");
    private ForcePlan CurrentForce => _forcePlans[_forceSide];
    private void ChangeForce(Func<ForcePlan, ForcePlan> change) { _forcePlans[_forceSide] = change(CurrentForce); RefreshForceReport(); }
    private static string ForceClass(string cls) => cls switch { "fighter" => "истребители", "corvette" => "корветы", "frigate" => "фрегаты", "capital" => "капитальные", "platform" => "платформы", "station" => "станции", _ => cls };
    private void StartPrepared(PreparedBattle setup, bool ai, bool persist = true)
    {
        string map = ForceBattle.Register(_content, setup); _preparation = setup;
        StartBattle(map, ai, persist: persist, preservePreparation: true);
    }
    private void RestorePreparation(SavedGame saved)
    {
        if ((saved.State.Map == ForceBattle.MapId) != (saved.Preparation != null) || saved.Preparation != null && (saved.Generation != null || saved.Campaign != null)) throw new InvalidDataException("Неверные данные подготовки флотов.");
        if (saved.Preparation != null) ForceBattle.Register(_content, saved.Preparation);
        _preparation = saved.Preparation;
    }
    private PreparedBattle EditorPreparation() => new(_forcePlans.ToArray(), _forceWidth, _forceHeight, _forceObjects, _forceSeed, _forceBackground == "" ? _colors.Background : _forceBackground);
    private void ForceMenu(bool network = false)
    {
        StopNetwork(); _state = null; _forceNetwork = network; ClearScreen(); Heading("Подготовка флотов / цветные вкладки");
        VBoxContainer Column(float x)
        {
            var panel = Panel(x, 160, 422, 635); panel.AddThemeConstantOverride("separation", 5);
            var scroll = new ScrollContainer { CustomMinimumSize = new(0, 603), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; panel.AddChild(scroll);
            var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; box.AddThemeConstantOverride("separation", 7); scroll.AddChild(box); return box;
        }
        var left = Column(70); var middle = Column(509); var right = Column(948);
        OptionButton Picker(Control box, string label, string[] names, int selected, Action<int> change)
        {
            Text(box, label, 15, _colors.Muted); var select = new OptionButton { CustomMinimumSize = new(0, 36), ClipText = true };
            foreach (var name in names) select.AddItem(name); if (names.Length > 0) select.Select(Math.Clamp(selected, 0, names.Length - 1)); box.AddChild(select);
            select.ItemSelected += i => change((int)i); return select;
        }
        SpinBox Number(Control box, string label, int min, int max, int value, Action<int> change)
        {
            var row = new HBoxContainer(); box.AddChild(row); var text = Text(row, label, 15); text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            var input = new SpinBox { MinValue = min, MaxValue = max, Value = value, Step = 1, CustomMinimumSize = new(135, 36) }; row.AddChild(input); input.ValueChanged += v => change((int)v); return input;
        }
        LineEdit Field(Control box, string label, string value, Action<string> change)
        {
            Text(box, label, 15, _colors.Muted); var input = new LineEdit { Text = value, CustomMinimumSize = new(0, 36), MaxLength = 60 }; box.AddChild(input); input.TextChanged += value => change(value); return input;
        }
        Picker(left, "РЕДАКТИРУЕМ СТОРОНУ", ["1 · Наш флот", "2 · Противник"], _forceSide, i => { _forceSide = i; _forceUnit = CurrentForce.Units.FirstOrDefault()?.Id ?? ""; ForceMenu(network); });
        var factions = _content.Ships.Values.Where(s => s.Stars > 0).Select(s => s.Faction).Distinct().Order().ToArray();
        Picker(left, "Фракция (смена создаёт новый состав)", factions, Array.IndexOf(factions, CurrentForce.Faction), i =>
        {
            string faction = factions[i]; _forcePlans[_forceSide] = faction is "kushan" or "taiidan" ? ForceBattle.PracticeForce(faction) : new("fleet-" + _forceSide, faction, faction, 12, [], []);
            _forceUnit = CurrentForce.Units.FirstOrDefault()?.Id ?? ""; _forceNotice = "Создан новый состав."; ForceMenu(network);
        });
        Field(left, "Название", CurrentForce.Name, value => ChangeForce(p => p with { Name = value }));
        Field(left, "Имя файла (латиница, цифры, _ и -)", CurrentForce.Id, value => ChangeForce(p => p with { Id = value }));
        Number(left, "Бюджет ★", 1, 1000000, CurrentForce.Budget, v => ChangeForce(p => p with { Budget = v }));
        Number(left, "Ресурсы в резерве ★", 0, 1000000, CurrentForce.ReserveStars, v => ChangeForce(p => p with { ReserveStars = v }));
        var tabs = new CheckBox { Text = "Проверять цветные вкладки", ButtonPressed = CurrentForce.CheckTabs }; left.AddChild(tabs);
        tabs.Toggled += v => ChangeForce(p => p with { CheckTabs = v });
        Text(left, "Без проверки вкладок — свободный состав. Стоимость, фракция и ангары проверяются всегда.", 14, _colors.Muted);
        Button(left, "СОХРАНИТЬ СОСТАВ", () =>
        {
            if (!ForceBuilder.SafeId(CurrentForce.Id)) _forceNotice = "Для сохранения нужно имя файла: 1–48 букв латиницы, цифр, _ или -.";
            else { ForceLibrary.Save(ForceDirectory, CurrentForce); _forceNotice = "Состав сохранён. Черновики проверяются при загрузке."; }
            ForceMenu(network);
        });
        ForcePlan[] library;
        try { library = ForceLibrary.Read(ForceDirectory); }
        catch (Exception e) { library = []; Text(left, "Ошибка библиотеки: " + e.Message, 14, _colors.Enemy); }
        int libraryIndex = 0;
        Picker(left, "Библиотека составов", library.Select(p => p.Name + " · " + p.Id).ToArray(), 0, i => libraryIndex = i);
        Button(left, "ЗАГРУЗИТЬ СОСТАВ", () => { _forcePlans[_forceSide] = library[libraryIndex]; _forceUnit = CurrentForce.Units.FirstOrDefault()?.Id ?? ""; _forceNotice = "Состав загружен и проверен с текущими модами."; ForceMenu(network); }, library.Length == 0);
        Text(left, "ПОЛЕ ДЛЯ ОБОИХ ФЛОТОВ", 16, _colors.Highlight);
        Number(left, "Ширина", 8, 40, _forceWidth, v => { _forceWidth = v; RefreshForceReport(); });
        Number(left, "Высота", 4, 30, _forceHeight, v => { _forceHeight = v; RefreshForceReport(); });
        Number(left, "Объектов", 0, 1080, _forceObjects, v => { _forceObjects = v; RefreshForceReport(); });
        Number(left, "Seed поля", 1, 999999, _forceSeed, v => _forceSeed = v);
        string[] backgrounds = ["", "#07121f", "#102239", "#21180e"];
        Picker(left, "Фон", ["Из текущей темы", "Глубокий космос", "Синий сектор", "Янтарный сектор"], Array.IndexOf(backgrounds, _forceBackground), i => _forceBackground = backgrounds[i]);
        Button(left, "НОВЫЙ SEED", () => { _forceSeed = Random.Shared.Next(1, 999999); ForceMenu(network); });
        Picker(left, "Режим боя", network ? ["По сети: друг против друга", "По сети: общий флот против AI"] : ["Против компьютера", "Вдвоём за одним компьютером"], _forceMode, i => _forceMode = i);
        if (network) Field(left, "Адрес хоста / UDP 24560", _forceAddress, v => _forceAddress = v);
        Text(middle, "ПОДРАЗДЕЛЕНИЯ", 16, _colors.Highlight);
        var ships = _content.Ships.Values.Where(s => s.Faction == CurrentForce.Faction && s.Stars > 0).OrderBy(s => s.Name).ThenBy(s => s.Id).ToArray(); int shipIndex = 0;
        Picker(middle, "Купить подразделение", ships.Select(s => $"{s.Name} · {s.Stars} ★ · {ForceClass(FleetRuleTools.Class(s))}").ToArray(), 0, i => shipIndex = i);
        Button(middle, "ДОБАВИТЬ КОРАБЛЬ", () =>
        {
            int index = 1; while (CurrentForce.Units.Any(u => u.Id == "u" + index)) index++;
            var cell = Enumerable.Range(0, _forceHeight).SelectMany(r => new[] { (q: 0, r), (q: 1, r) }).FirstOrDefault(h => !CurrentForce.Units.Any(u => u.Placement == "board" && u.Column == h.q && u.Row == h.r));
            var entry = new ForceEntry("u" + index, ships[shipIndex].Id, Column: cell.q, Row: cell.r); ChangeForce(p => p with { Units = p.Units.Append(entry).ToArray() }); _forceUnit = entry.Id; ForceMenu(network);
        }, ships.Length == 0 || CurrentForce.Units.Length >= 64);
        foreach (var unit in CurrentForce.Units)
        {
            string name = _content.Ships.GetValueOrDefault(unit.Ship)?.Name ?? unit.Ship;
            var b = Button(middle, $"{(unit.Id == _forceUnit ? "▸ " : "")}{unit.Id} · {name}\n{unit.Placement switch { "bay" => "Ангар " + unit.Parent, "reserve" => "Резерв", _ => $"Поле: {unit.Column + 1}, {unit.Row + 1}" }}", () => { _forceUnit = unit.Id; ForceMenu(network); }); b.AddThemeFontSizeOverride("font_size", 15);
        }
        var selected = CurrentForce.Units.FirstOrDefault(u => u.Id == _forceUnit);
        if (selected != null)
        {
            Text(middle, "РАЗМЕЩЕНИЕ · " + selected.Id, 15, _colors.Highlight);
            void Edit(Func<ForceEntry, ForceEntry> change) => ChangeForce(p => p with { Units = p.Units.Select(u => u.Id == selected.Id ? change(u) : u).ToArray() });
            string[] placements = ["board", "reserve", "bay"];
            Picker(middle, "Место старта", ["На поле", "В резерве", "В ангаре"], Array.IndexOf(placements, selected.Placement), i => { Edit(u => u with { Placement = placements[i] }); ForceMenu(network); });
            if (selected.Placement == "board")
            {
                Number(middle, "Колонка своей зоны (1–2)", 1, 2, selected.Column + 1, v => Edit(u => u with { Column = v - 1 }));
                Number(middle, "Ряд (сверху вниз)", 1, 30, selected.Row + 1, v => Edit(u => u with { Row = v - 1 }));
                Picker(middle, "Курс", ["К противнику", "0", "1", "2", "3", "4", "5"], selected.Facing + 1, i => Edit(u => u with { Facing = i - 1 }));
            }
            if (selected.Placement == "bay")
            {
                var parents = CurrentForce.Units.Where(u => u.Id != selected.Id && _content.Ships.GetValueOrDefault(u.Ship)?.Bays > 0).ToArray();
                Picker(middle, "Носитель (включая корабли в резерве)", new[] { "Выбери носитель" }.Concat(parents.Select(u => u.Id + " · " + _content.Ships[u.Ship].Name)).ToArray(), Array.FindIndex(parents, u => u.Id == selected.Parent) + 1, i => Edit(u => u with { Parent = i == 0 ? "" : parents[i - 1].Id }));
            }
            Button(middle, "УБРАТЬ ВЫБРАННЫЙ КОРАБЛЬ", () => { ChangeForce(p => p with { Units = p.Units.Where(u => u.Id != selected.Id).ToArray(), Leaders = p.Leaders.Where(l => l.Unit != selected.Id && !l.Unit.StartsWith(selected.Id + "~")).ToArray() }); _forceUnit = ""; ForceMenu(network); });
        }
        Text(middle, "ЛИДЕРЫ", 16, _colors.Highlight);
        var leaders = _content.Leaders.Values.Where(l => l.Faction == CurrentForce.Faction && !CurrentForce.Leaders.Any(x => x.Leader == l.Id)).OrderBy(l => l.Name).ToArray(); int leaderIndex = 0;
        Picker(middle, "Нанять лидера", leaders.Select(l => $"{l.Name} · {l.Stars} ★").ToArray(), 0, i => leaderIndex = i);
        Button(middle, "ДОБАВИТЬ ЛИДЕРА", () => { ChangeForce(p => p with { Leaders = p.Leaders.Append(new(leaders[leaderIndex].Id, _forceUnit)).ToArray() }); ForceMenu(network); }, leaders.Length == 0 || CurrentForce.Leaders.Length >= 16);
        var expanded = ForceBuilder.Validate(_content, CurrentForce).Units;
        foreach (var choice in CurrentForce.Leaders)
        {
            if (!_content.Leaders.TryGetValue(choice.Leader, out var leader))
            {
                Text(middle, choice.Leader + " недоступен", 15, _colors.Enemy);
                Button(middle, "УБРАТЬ: " + choice.Leader, () => { ChangeForce(p => p with { Leaders = p.Leaders.Where(l => l.Leader != choice.Leader).ToArray() }); ForceMenu(network); }); continue;
            }
            Text(middle, $"{leader.Name} · {leader.Stars} ★\nРука +{leader.Hand}, приказы +{leader.Play}, ремонт +{leader.CoolUnderPressure}, атака +{leader.AttackDice}", 15);
            var targets = expanded.Where(u => leader.Classes.Contains(FleetRuleTools.Class(_content.Ships[u.Entry.Ship]))).ToArray();
            Picker(middle, "Назначить на отряд", new[] { "Не назначен" }.Concat(targets.Select(u => u.Entry.Id + (u.Free ? " · приписан" : ""))).ToArray(), Array.FindIndex(targets, u => u.Entry.Id == choice.Unit) + 1,
                i => ChangeForce(p => p with { Leaders = p.Leaders.Select(l => l.Leader == choice.Leader ? l with { Unit = i == 0 ? "" : targets[i - 1].Entry.Id } : l).ToArray() }));
            Button(middle, "УБРАТЬ: " + leader.Name, () => { ChangeForce(p => p with { Leaders = p.Leaders.Where(l => l.Leader != choice.Leader).ToArray() }); ForceMenu(network); });
        }
        Text(right, "ПРОВЕРКА СОСТАВА", 16, _colors.Highlight); _forceReport = new VBoxContainer(); right.AddChild(_forceReport);
        Text(right, "Цвета вкладок задаются темой. Полная — обязательная покупка; половинная — по желанию. Серая занята бесплатным приписанным отрядом. Синие вкладки носителя требуют старта в его ангаре.", 14, _colors.Muted);
        Text(right, "Пока используются явно учебные профили. Точные вкладки и стоимости настоящих карт добавим по фотографиям. Правила сборки и оформление независимы.", 14, _colors.Muted);
        if (_forceNotice != "") Text(right, _forceNotice, 15, _colors.Highlight);
        var actions = new HBoxContainer { Position = new(70, 805), Size = new(1300, 44) }; _ui.AddChild(actions);
        Button(actions, "НАЗАД", network ? NetworkMenu : SinglePlayerMenu);
        _forceLaunch = Button(actions, network ? "СОЗДАТЬ БОЙ С ЭТИМИ ФЛОТАМИ" : "НАЧАТЬ БОЙ С ЭТИМИ ФЛОТАМИ", () =>
        {
            StartPrepared(EditorPreparation(), network ? _forceMode == 1 : _forceMode == 0);
            if (network) StartNetwork(true, _forceAddress, 24560, _forceMode == 1); QueueBot();
        });
        RefreshForceReport(); MenuScene();
    }
    private void RefreshForceReport()
    {
        if (_forceReport == null || !GodotObject.IsInstanceValid(_forceReport) || _forceReport.IsQueuedForDeletion()) return;
        foreach (var child in _forceReport.GetChildren()) { _forceReport.RemoveChild(child); child.QueueFree(); }
        var reports = _forcePlans.Select(p => ForceBuilder.Validate(_content, p)).ToArray(); var report = reports[_forceSide];
        Text(_forceReport, $"Потрачено: {report.Spent} ★ / {CurrentForce.Budget} ★\nОстаток: {report.Remaining} ★ (не идёт в бой)\nРезерв в бою: {CurrentForce.ReserveStars} ★", 19, report.Valid ? _colors.Highlight : _colors.Enemy);
        foreach (var slot in report.Slots)
        {
            bool grey = slot.Tab.GrantedUnit != ""; string color = grey ? _colors.TabGrey : slot.Tab.Color == "blue" ? _colors.TabBlue : _colors.TabYellow;
            string unit = slot.Unit == "" ? "свободна" : slot.Unit; string classes = slot.Tab.Classes is { Length: > 0 } ? string.Join(", ", slot.Tab.Classes.Select(ForceClass)) : slot.Tab.Color == "blue" ? "истребители / корветы / платформы" : "фрегаты / капитальные / станции";
            Text(_forceReport, $"{(grey || slot.Tab.Required ? "■" : "◧")} {slot.Source} / {slot.Tab.Id}\n{classes} → {unit}{(grey ? " · бесплатно" : slot.Tab.Required ? " · обязательно" : " · по желанию")}", 15, color);
        }
        foreach (var carrier in report.Units.Where(u => _content.Ships[u.Entry.Ship].Bays > 0)) Text(_forceReport, $"Ангары {carrier.Entry.Id}: {report.Units.Count(u => u.Entry.Placement == "bay" && u.Entry.Parent == carrier.Entry.Id)} / {_content.Ships[carrier.Entry.Ship].Bays}", 15);
        var mapErrors = new List<string>();
        for (int side = 0; side < 2; side++)
        {
            if (reports[side].Units.Any(u => u.Entry.Placement == "board" && u.Entry.Row >= _forceHeight)) mapErrors.Add($"Флот {side + 1}: корабли за высотой поля.");
            foreach (string error in reports[side].Errors) Text(_forceReport, $"Флот {side + 1}: {error}", 14, _colors.Enemy);
        }
        if (_forceObjects > (_forceWidth - 4) * _forceHeight) mapErrors.Add("Слишком много объектов для центральной части поля.");
        foreach (string error in mapErrors) Text(_forceReport, error, 14, _colors.Enemy);
        if (reports.All(r => r.Valid) && mapErrors.Count == 0)
        {
            try { ForceBattle.Register(_content.CopyForRuntime(), EditorPreparation()); }
            catch (InvalidDataException e) { mapErrors.Add(e.Message); Text(_forceReport, e.Message, 14, _colors.Enemy); }
        }
        bool valid = reports.All(r => r.Valid) && mapErrors.Count == 0;
        Text(_forceReport, valid ? "Оба флота готовы к бою." : "Исправь ошибки перед запуском.", 16, valid ? _colors.Highlight : _colors.Enemy);
        if (_forceLaunch != null && GodotObject.IsInstanceValid(_forceLaunch)) _forceLaunch.Disabled = !valid;
    }
}
