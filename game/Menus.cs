using Godot;
using FleetCommand.Core;

namespace FleetCommand.Game;

public partial class Main
{
    private void MainMenu()
    {
        StopNetwork(); EndTutorial(); _state = null; _campaign = null; _generation = null; _preparation = null; _namesHeld = false; ClearScreen();
        Heading("FLEET TACTICAL SIMULATION");
        var menu = Panel(70, 245, 385, 420);
        Text(menu, "MAIN MENU", 15, _colors.Highlight);
        Button(menu, "TUTORIAL", StartTutorial);
        Button(menu, "SINGLE PLAYER", SinglePlayerMenu);
        Button(menu, "MULTIPLAYER", NetworkMenu);
        Button(menu, "OPTIONS", OptionsMenu);
        Button(menu, "MODS", ModsMenu);
        Button(menu, "EXIT", () => GetTree().Quit());
        var note = new VBoxContainer { Position = new(930, 570), Size = new(390, 210) }; _ui.AddChild(note);
        Text(note, "COMMAND INTERFACE", 20, _colors.Accent);
        Text(note, "Ships, fleets, maps, themes and campaigns are defined by data.\n\nThe current build tests the game loop with placeholder cubes.", 17, _colors.Muted);
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
        ClearScreen(); Heading("Single player"); var outer = Panel(70, 170, 540, 650);
        var scroll = new ScrollContainer { CustomMinimumSize = new(0, 618), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; outer.AddChild(scroll);
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; box.AddThemeConstantOverride("separation", 8); scroll.AddChild(box);
        Button(box, "CAMPAIGNS", CampaignMenu);
        Button(box, "RULEBOOK SCENARIOS", ScenarioMenu);
        Button(box, "TWO-HEX SHIPS", () => { StartBattle("practice:large", true); QueueBot(); });
        Button(box, "FLEET PREPARATION", () => ForceMenu());
        Button(box, "TRAINING CARD BATTLE", StartCardBattle);
        Button(box, "RULES / DEVELOPMENT MODE", () => { StartBattle(BasicCardBattle.TabletopMapId, true); QueueBot(); });
        Button(box, "OBJECTIVES AND MEDALS", () => { StartBattle(BasicCardBattle.ObjectivesMapId, true); QueueBot(); });
        Button(box, "BAYS AND REINFORCEMENTS", () => { StartBattle(BasicCardBattle.LogisticsMapId, true); QueueBot(); });
        Button(box, "SURVEY AND HARVEST", () => { StartBattle(BasicCardBattle.EconomyMapId, true); QueueBot(); });
        Button(box, "MINES AND ASTEROIDS", () => { StartBattle(BasicCardBattle.TerrainMapId, true); QueueBot(); });
        Button(box, "SKIRMISH / GENERATOR", GeneratorMenu);
        Button(box, "RESUME SKIRMISH", () => LoadGame(false), !System.IO.File.Exists(SavePath(false)));
        Button(box, "BACK", MainMenu);
        Text(box, "The prototype opponent uses simple heuristics. Full command cards and interrupts are still in progress.", 15, _colors.Muted);
        MenuScene();
    }
    private void CampaignMenu()
    {
        ClearScreen(); Heading("Campaigns"); var box = Panel(70, 205, 600, 470);
        Text(box, "CAMPAIGNS FROM MODS", 16, _colors.Highlight);
        foreach (var campaign in _content.Campaigns.Values)
            Button(box, campaign.Name, () =>
            {
                _campaign = FleetCommand.Core.Campaign.Start(_content, campaign.Id, ["local"]);
                var mission = campaign.Missions.Single(m => m.Id == campaign.Start);
                StartBattle(mission.Map, true, preserveCampaign: true);
            });
        Button(box, "Resume saved campaign", () => LoadGame(true), !System.IO.File.Exists(SavePath(true)));
        Button(box, "BACK", SinglePlayerMenu);
        Text(box, "The save includes the current battle and campaign progress. The demo campaign contains two test battles.", 15, _colors.Muted);
        MenuScene();
    }
    private void GeneratorMenu()
    {
        ClearScreen(); Heading("Skirmish / Battlefield generator"); var box = Panel(70, 180, 550, 650);
        box.AddThemeConstantOverride("separation", 6);
        SpinBox Number(string name, int min, int max, int value)
        {
            var row = new HBoxContainer(); box.AddChild(row); var label = Text(row, name, 16); label.CustomMinimumSize = new(300, 0);
            var spin = new SpinBox { MinValue = min, MaxValue = max, Step = 1, Value = value, CustomMinimumSize = new(145, 34) }; row.AddChild(spin); return spin;
        }
        var width = Number("Field width", 8, 40, 12); var height = Number("Field height", 4, 30, 8);
        var friendly = Number("Our units", 1, 30, 5); var enemy = Number("Enemy units", 1, 30, 5);
        var objects = Number("Battlefield objects", 0, 200, 10); var seed = Number("Map seed", 1, 999999, 12345);
        var background = new OptionButton();
        foreach (var name in new[] { "Background: deep space", "Background: blue sector", "Background: amber sector" }) background.AddItem(name);
        box.AddChild(background);
        var ai = new CheckBox { Text = "Computer opponent", ButtonPressed = true }; box.AddChild(ai);
        Text(box, "Deployment: our side on the left, opponent on the right.\nHWRM import is deferred; ships currently use placeholder cubes.", 15, _colors.Muted);
        Button(box, "NEW RANDOM MAP", () => seed.Value = Random.Shared.Next(1, 999999));
        Button(box, "START", () =>
        {
            var colors = new[] { "#07121f", "#102239", "#21180e" };
            _generation = new((int)width.Value, (int)height.Value, (int)friendly.Value, (int)enemy.Value, (int)objects.Value, (int)seed.Value, colors[background.Selected]);
            var map = MapGenerator.Generate(_content, _generation); StartBattle(map, ai.ButtonPressed, preserveGeneration: true);
        });
        Button(box, "BACK", SinglePlayerMenu); MenuScene();
    }
    private void OptionsMenu()
    {
        ClearScreen(); Heading("Options"); var box = Panel(70, 190, 550, 520);
        Button(box, "LANGUAGE", LanguageMenu);
        Button(box, "PLAYER: NAME, COLOR, BADGE", PlayerMenu);
        Button(box, "CARD APPEARANCE", CardStylesMenu);
        Text(box, "Current player: " + FleetPresentation.Normalize(_settings.Player).Name, 16, _colors.Muted);
        Text(box, "INTERFACE THEME", 16, _colors.Highlight);
        var themes = _content.Themes.Values.ToArray(); var dropdown = new OptionButton();
        foreach (var theme in themes) dropdown.AddItem(theme.Name);
        dropdown.Select(Math.Max(0, Array.FindIndex(themes, t => t.Id == _colors.Id))); box.AddChild(dropdown);
        dropdown.ItemSelected += i => { _settings = _settings with { Theme = themes[(int)i].Id }; SaveSettings(); LoadContent(); OptionsMenu(); };
        Text(box, "All colors come from a mod's theme definition. Enable example in Mods for an alternate palette.", 16, _colors.Muted);
        Text(box, "Hold Alt to show names on the field. Tactical symbols and badges are always visible.", 16, _colors.Muted);
        Button(box, "BACK", MainMenu); MenuScene();
    }
    private void ModsMenu()
    {
        ClearScreen(); Heading("Mods"); var box = Panel(70, 190, 750, 570);
        Text(box, "FOLDER MODS / JSON", 18, _colors.Highlight);
        Text(box, "core — base definitions\nShips · fleets · abilities · objects · maps · rules · themes · campaigns", 16, _colors.Muted);
        var selected = (_settings.EnabledMods ?? []).ToHashSet(StringComparer.Ordinal);
        foreach (var dir in ModDirectories())
        {
            var manifest = JsonData.Read<ModManifest>(System.IO.Path.Combine(dir, "mod.json"));
            var toggle = new CheckBox { Text = manifest.Id + "  /  " + manifest.Version, ButtonPressed = selected.Contains(manifest.Id) };
            toggle.Toggled += enabled => { if (enabled) selected.Add(manifest.Id); else selected.Remove(manifest.Id); }; box.AddChild(toggle);
        }
        Button(box, "APPLY MODS", () =>
        {
            var previous = _settings; _settings = _settings with { EnabledMods = selected.Order(StringComparer.Ordinal).ToArray() };
            try { LoadContent(); SaveSettings(); ModsMenu(); } catch { _settings = previous; throw; }
        });
        Text(box, "User mods: " + System.IO.Path.Combine(_data, "mods"), 14, _colors.Muted);
        Text(box, "Rule changes: JSON parameters and the IRuleModule interface for code extensions. Loading third-party executable code is not enabled yet.", 15, _colors.Muted);
        Button(box, "BACK", MainMenu); MenuScene();
    }
}
