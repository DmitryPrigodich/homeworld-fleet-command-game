using Godot;
using FleetCommand.Core;

namespace FleetCommand.Game;

public partial class Main
{
    private void MainMenu()
    {
        StopNetwork(); EndTutorial(); _state = null; _campaign = null; _generation = null; ClearScreen();
        Heading("ТАКТИЧЕСКАЯ СИМУЛЯЦИЯ ФЛОТА");
        var menu = Panel(70, 245, 385, 420);
        Text(menu, "ГЛАВНОЕ МЕНЮ", 15, _colors.Highlight);
        Button(menu, "ОБУЧЕНИЕ", StartTutorial);
        Button(menu, "ОДИНОЧНАЯ ИГРА", SinglePlayerMenu);
        Button(menu, "СОВМЕСТНАЯ ИГРА", NetworkMenu);
        Button(menu, "ОПЦИИ", OptionsMenu);
        Button(menu, "МОДЫ", ModsMenu);
        Button(menu, "ВЫХОД", () => GetTree().Quit());
        var note = new VBoxContainer { Position = new(930, 570), Size = new(390, 210) }; _ui.AddChild(note);
        Text(note, "COMMAND INTERFACE", 20, _colors.Accent);
        Text(note, "Корабли, флоты, карты, темы и кампании задаются данными.\n\nСейчас — проверка игрового цикла на условных кубиках.", 17, _colors.Muted);
        MenuScene();
    }
    private void MenuScene()
    {
        _camera = new Camera3D { Position = new(10, 9, 13), Current = true }; _world.AddChild(_camera); _camera.LookAt(Vector3.Zero);
        var light = new DirectionalLight3D { RotationDegrees = new(-40, -30, 0), LightEnergy = 1.7f }; _world.AddChild(light);
        for (int i = 0; i < 7; i++)
        {
            float s = i == 0 ? 2 : i < 3 ? 0.8f : 0.35f;
            var cube = new MeshInstance3D { Mesh = new BoxMesh { Size = new(s, s * 0.6f, s * 1.6f) },
                Position = new(2 + i * 0.45f, i % 2 * 0.6f, i * 1.0f - 3),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = C(i < 3 ? _colors.Accent : _colors.Enemy), Metallic = 0.3f, Roughness = 0.45f } };
            _world.AddChild(cube);
        }
    }
    private void SinglePlayerMenu()
    {
        ClearScreen(); Heading("Одиночная игра"); var box = Panel(70, 230, 420, 380);
        Button(box, "КАМПАНИИ", CampaignMenu);
        Button(box, "СКИРМИШ / ГЕНЕРАТОР", GeneratorMenu);
        Button(box, "ПРОДОЛЖИТЬ СКИРМИШ", () => LoadGame(false), !System.IO.File.Exists(SavePath(false)));
        Button(box, "НАЗАД", MainMenu);
        Text(box, "Противник в прототипе использует простую эвристику. Полные карты приказов и прерывания ещё в работе.", 15, _colors.Muted);
        MenuScene();
    }
    private void CampaignMenu()
    {
        ClearScreen(); Heading("Кампании"); var box = Panel(70, 205, 600, 470);
        Text(box, "КАМПАНИИ ИЗ МОДОВ", 16, _colors.Highlight);
        foreach (var campaign in _content.Campaigns.Values)
            Button(box, campaign.Name, () =>
            {
                _campaign = FleetCommand.Core.Campaign.Start(_content, campaign.Id, ["local"]);
                var mission = campaign.Missions.Single(m => m.Id == campaign.Start);
                StartBattle(mission.Map, true, preserveCampaign: true);
            });
        Button(box, "Продолжить сохранённую кампанию", () => LoadGame(true), !System.IO.File.Exists(SavePath(true)));
        Button(box, "НАЗАД", SinglePlayerMenu);
        Text(box, "Сохранение включает текущий бой и прогресс кампании. Демокампания содержит два тестовых боя.", 15, _colors.Muted);
        MenuScene();
    }
    private void GeneratorMenu()
    {
        ClearScreen(); Heading("Скирмиш / Генератор поля"); var box = Panel(70, 180, 550, 650);
        box.AddThemeConstantOverride("separation", 6);
        SpinBox Number(string name, int min, int max, int value)
        {
            var row = new HBoxContainer(); box.AddChild(row); var label = Text(row, name, 16); label.CustomMinimumSize = new(300, 0);
            var spin = new SpinBox { MinValue = min, MaxValue = max, Step = 1, Value = value, CustomMinimumSize = new(145, 34) }; row.AddChild(spin); return spin;
        }
        var width = Number("Ширина поля", 8, 40, 12); var height = Number("Высота поля", 4, 30, 8);
        var friendly = Number("Наших подразделений", 1, 30, 5); var enemy = Number("Вражеских подразделений", 1, 30, 5);
        var objects = Number("Объектов на поле", 0, 200, 10); var seed = Number("Seed карты", 1, 999999, 12345);
        var background = new OptionButton();
        foreach (var name in new[] { "Фон: глубокий космос", "Фон: синий сектор", "Фон: янтарный сектор" }) background.AddItem(name);
        box.AddChild(background);
        var ai = new CheckBox { Text = "Компьютерный противник", ButtonPressed = true }; box.AddChild(ai);
        Text(box, "Зоны размещения: мы слева, противник справа.\nИмпорт HWRM отложен: сейчас условные кубики.", 15, _colors.Muted);
        Button(box, "НОВАЯ СЛУЧАЙНАЯ КАРТА", () => seed.Value = Random.Shared.Next(1, 999999));
        Button(box, "НАЧАТЬ", () =>
        {
            var colors = new[] { "#07121f", "#102239", "#21180e" };
            _generation = new((int)width.Value, (int)height.Value, (int)friendly.Value, (int)enemy.Value, (int)objects.Value, (int)seed.Value, colors[background.Selected]);
            var map = MapGenerator.Generate(_content, _generation); StartBattle(map, ai.ButtonPressed, preserveGeneration: true);
        });
        Button(box, "НАЗАД", SinglePlayerMenu); MenuScene();
    }
    private void OptionsMenu()
    {
        ClearScreen(); Heading("Опции"); var box = Panel(70, 230, 550, 330);
        Text(box, "ТЕМА ИНТЕРФЕЙСА", 16, _colors.Highlight);
        var themes = _content.Themes.Values.ToArray(); var dropdown = new OptionButton();
        foreach (var theme in themes) dropdown.AddItem(theme.Name);
        dropdown.Select(Math.Max(0, Array.FindIndex(themes, t => t.Id == _colors.Id))); box.AddChild(dropdown);
        dropdown.ItemSelected += i => { _settings = _settings with { Theme = themes[(int)i].Id }; SaveSettings(); LoadContent(); OptionsMenu(); };
        Text(box, "Все цвета загружаются из theme-определения мода. Для альтернативной палитры включи example в разделе «Моды».", 16, _colors.Muted);
        Button(box, "НАЗАД", MainMenu); MenuScene();
    }
    private void ModsMenu()
    {
        ClearScreen(); Heading("Моды"); var box = Panel(70, 190, 750, 570);
        Text(box, "МОДЫ В ПАПКАХ / JSON", 18, _colors.Highlight);
        Text(box, "core — базовые определения\nКорабли · флоты · способности · объекты · карты · правила · темы · кампании", 16, _colors.Muted);
        var selected = (_settings.EnabledMods ?? []).ToHashSet(StringComparer.Ordinal);
        foreach (var dir in ModDirectories())
        {
            var manifest = JsonData.Read<ModManifest>(System.IO.Path.Combine(dir, "mod.json"));
            var toggle = new CheckBox { Text = manifest.Id + "  /  " + manifest.Version, ButtonPressed = selected.Contains(manifest.Id) };
            toggle.Toggled += enabled => { if (enabled) selected.Add(manifest.Id); else selected.Remove(manifest.Id); }; box.AddChild(toggle);
        }
        Button(box, "ПРИМЕНИТЬ МОДЫ", () =>
        {
            var previous = _settings; _settings = _settings with { EnabledMods = selected.Order(StringComparer.Ordinal).ToArray() };
            try { LoadContent(); SaveSettings(); ModsMenu(); } catch { _settings = previous; throw; }
        });
        Text(box, "Пользовательские моды: " + System.IO.Path.Combine(_data, "mods"), 14, _colors.Muted);
        Text(box, "Изменение правил: параметры JSON и интерфейс IRuleModule для программных расширений. Загрузка стороннего исполняемого кода пока не включена.", 15, _colors.Muted);
        Button(box, "НАЗАД", MainMenu); MenuScene();
    }
}
