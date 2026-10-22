using Godot;
using System.Text.Json;
using FleetCommand.Core;

namespace FleetCommand.Game;

public sealed record AppSettings(string Theme = "core:hw1", string[]? EnabledMods = null, PlayerProfile? Player = null,
    string HwrmInstallation = "C:\\Program Files (x86)\\Steam\\steamapps\\common\\Homeworld", bool ShowLabels = true);
public sealed record SavedGame(BattleState State, GenerationOptions? Generation = null, CampaignProgress? Campaign = null,
    bool ComputerOpponent = true, CommanderAppearance[]? Commanders = null);

public partial class Main : Node3D
{
    private ContentCatalog _content = null!;
    private BattleEngine _engine = null!;
    private BattleState? _state;
    private CampaignProgress? _campaign;
    private GenerationOptions? _generation;
    private AppSettings _settings = new();
    private ThemeDefinition _colors = new("core:hw1", "Deep Space", "#07121f", "#102436", "#b4d9e7", "#6c96aa", "#39a4c5", "#e2b361", "#d07858", "#234355");
    private Control _ui = null!;
    private Node3D _world = null!;
    private Camera3D _camera = null!;
    private Starfield _stars = null!;
    private string _root = "", _data = "", _status = "";
    private string _selected = "", _action = "move";
    private bool _bot = true, _botQueued;
    private int _frames;
    private string[] _args = [];

    public override void _Ready()
    {
        _root = ProjectSettings.GlobalizePath("res://");
        _data = System.Environment.GetEnvironmentVariable("FLEET_COMMAND_DATA_DIR") ?? System.IO.Path.Combine(_root, ".local");
        System.IO.Directory.CreateDirectory(_data);
        _args = OS.GetCmdlineUserArgs();
        var canvas = new CanvasLayer(); AddChild(canvas);
        _ui = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _ui.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect); canvas.AddChild(_ui);
        var background = new CanvasLayer { Layer = -10 }; AddChild(background);
        _stars = new Starfield(); background.AddChild(_stars);
        try
        {
            var settings = System.IO.Path.Combine(_data, "settings.json");
            if (System.IO.File.Exists(settings)) _settings = JsonData.Read<AppSettings>(settings);
            _settings = _settings with { Player = FleetPresentation.Normalize(_settings.Player) };
            LoadContent();
            LoadEmblems();
            MainMenu();
            if (_args.Contains("--capture-battle")) StartBattle("core:training", false);
            if (_args.Contains("--capture-generator")) GeneratorMenu();
            if (_args.Contains("--capture-tutorial")) StartTutorial();
            if (_args.Contains("--capture-player")) PlayerMenu();
            if (_args.Contains("--capture-identification")) StartBattle("core:training", true, persist: false);
            if (_args.Contains("--smoke-host"))
            {
                bool coop = _args.Contains("--smoke-coop");
                if (coop) _campaign = FleetCommand.Core.Campaign.Start(_content, "core:patrol", ["host"]);
                StartBattle("core:training", false, preserveCampaign: coop); StartNetwork(true, "127.0.0.1", 25990, coop);
            }
            if (_args.Contains("--smoke-client")) { StartBattle("core:training", false, persist: false); StartNetwork(false, "127.0.0.1", 25990, _args.Contains("--smoke-coop")); }
            GD.Print($"FLEET_COMMAND_READY mods={_content.Mods.Count} ships={_content.Ships.Count}");
        }
        catch (Exception e) { ShowError(e.Message); GD.PushError(e.ToString()); }
    }

    private IEnumerable<string> ModDirectories()
    {
        foreach (var parent in new[] { System.IO.Path.Combine(_root, "mods"), System.IO.Path.Combine(_data, "mods") })
            if (System.IO.Directory.Exists(parent))
                foreach (var dir in System.IO.Directory.GetDirectories(parent))
                    if (System.IO.File.Exists(System.IO.Path.Combine(dir, "mod.json"))) yield return dir;
    }
    private void LoadContent()
    {
        var dirs = new List<string> { System.IO.Path.Combine(_root, "content", "base") };
        dirs.AddRange(ModDirectories().Where(d => (_settings.EnabledMods ?? []).Contains(JsonData.Read<ModManifest>(System.IO.Path.Combine(d, "mod.json")).Id)));
        var content = ContentCatalog.Load(dirs);
        _content = content; _engine = new(content);
        _colors = _content.Themes.GetValueOrDefault(_settings.Theme) ?? _content.Themes["core:hw1"];
        RenderingServer.SetDefaultClearColor(C(_colors.Background));
        _stars.Palette = _colors; _stars.QueueRedraw();
        _ui.Theme = MakeTheme();
    }
    private void SaveSettings() => System.IO.File.WriteAllText(System.IO.Path.Combine(_data, "settings.json"), JsonSerializer.Serialize(_settings, JsonData.Options));
    private Color C(string color) => new(color);
    private Theme MakeTheme()
    {
        var theme = new Theme { DefaultFontSize = 18 };
        foreach (var type in new[] { "Label", "Button", "CheckBox", "LineEdit", "OptionButton", "SpinBox" })
            theme.SetColor("font_color", type, C(_colors.Text));
        theme.SetColor("font_hover_color", "Button", C(_colors.Highlight));
        theme.SetColor("font_disabled_color", "Button", C(_colors.Muted));
        foreach (var type in new[] { "Button", "OptionButton", "LineEdit" })
        {
            theme.SetStylebox("normal", type, Box(_colors.Panel, _colors.Grid));
            theme.SetStylebox("hover", type, Box(_colors.Panel, _colors.Accent));
            theme.SetStylebox("pressed", type, Box(_colors.Grid, _colors.Highlight));
            theme.SetStylebox("focus", type, Box("#00000000", _colors.Highlight));
        }
        return theme;
    }
    private StyleBoxFlat Box(string fill, string border) => new()
    {
        BgColor = C(fill), BorderColor = C(border), BorderWidthBottom = 1, BorderWidthTop = 1,
        BorderWidthLeft = 1, BorderWidthRight = 1, ContentMarginLeft = 16, ContentMarginRight = 16,
        ContentMarginTop = 8, ContentMarginBottom = 8
    };
    private void ClearScreen()
    {
        foreach (var child in _ui.GetChildren()) child.QueueFree();
        _world?.QueueFree(); _world = new Node3D(); AddChild(_world);
    }
    private Label Text(Control parent, string text, int size = 18, string? color = null)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontSizeOverride("font_size", size);
        if (color != null) label.AddThemeColorOverride("font_color", C(color));
        parent.AddChild(label); return label;
    }
    private VBoxContainer Panel(float x, float y, float width, float height)
    {
        var panel = new PanelContainer { Position = new(x, y), Size = new(width, height) };
        panel.AddThemeStyleboxOverride("panel", Box(_colors.Panel + ( _colors.Panel.Length == 7 ? "eb" : ""), _colors.Grid));
        _ui.AddChild(panel);
        var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", 10); panel.AddChild(box); return box;
    }
    private Button Button(Control parent, string title, Action action, bool disabled = false)
    {
        var b = new Button { Text = title, CustomMinimumSize = new(0, 44), Alignment = HorizontalAlignment.Left, Disabled = disabled };
        b.Pressed += () => { try { action(); } catch (Exception e) { _status = e.Message; ShowError(e.Message); GD.PushError(e.ToString()); } };
        parent.AddChild(b); return b;
    }
    private void Heading(string subtitle)
    {
        var title = new VBoxContainer { Position = new(70, 50), Size = new(1200, 100) }; _ui.AddChild(title);
        Text(title, "FLEET COMMAND", 42, _colors.Accent);
        Text(title, subtitle.ToUpperInvariant(), 16, _colors.Muted);
        var footer = new Label { Text = "DEVELOPMENT PROTOTYPE   /   PLACEHOLDER CUBES   /   MODDABLE DATA", Position = new(72, 855) };
        footer.AddThemeFontSizeOverride("font_size", 13); footer.AddThemeColorOverride("font_color", C(_colors.Muted)); _ui.AddChild(footer);
    }
    private void ShowError(string message)
    {
        ClearScreen(); Heading("Ошибка загрузки"); var box = Panel(70, 220, 750, 400);
        Text(box, message, 18, _colors.Enemy); Button(box, "В главное меню", MainMenu);
    }
    public override void _Process(double delta)
    {
        _frames++;
        if (_frames == 5 && _args.Contains("--check-controls")) _ = CheckControls();
        if (_frames == 5 && _args.Contains("--check-presentation")) _ = CheckPresentation();
        if (_world != null && _state == null) _world.RotateY((float)delta * 0.08f);
        if (_frames == 40)
        {
            var capture = _args.FirstOrDefault(a => a.StartsWith("--capture="));
            if (capture != null)
            {
                string path = capture["--capture=".Length..];
                var image = GetViewport().GetTexture().GetImage(); image.SavePng(path);
                GD.Print("CAPTURE_SAVED " + path); GetTree().Quit();
            }
            else if (_args.Contains("--smoke")) GetTree().Quit();
        }
    }
}

public partial class Starfield : Node2D
{
    public ThemeDefinition? Palette { get; set; }
    public override void _Draw()
    {
        if (Palette == null) return;
        var size = GetViewportRect().Size;
        var random = new Random(79);
        for (int i = 0; i < 380; i++)
        {
            var p = new Vector2((float)random.NextDouble() * size.X, (float)random.NextDouble() * size.Y);
            var color = new Color(Palette.Text) { A = (float)random.NextDouble() * 0.42f + 0.05f };
            DrawCircle(p, i % 31 == 0 ? 1.4f : 0.6f, color);
        }
        DrawLine(new(0, size.Y * 0.74f), new(size.X, size.Y * 0.36f), new Color(Palette.Grid, 0.35f), 1);
    }
}
