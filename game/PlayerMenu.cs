using Godot;
using FleetCommand.Core;

namespace FleetCommand.Game;

public partial class Main
{
    private void PlayerMenu()
    {
        ClearScreen(); Heading("Player settings");
        var profile = FleetPresentation.Normalize(_settings.Player);
        var box = Panel(70, 180, 560, 650);
        Text(box, "COMMANDER PROFILE", 19, _colors.Highlight);
        Text(box, "Player name", 15, _colors.Muted);
        var name = new LineEdit { Text = profile.Name, MaxLength = 24, CustomMinimumSize = new(0, 42) }; box.AddChild(name);
        Text(box, "Battlefield projection color", 15, _colors.Muted);
        var picker = new ColorPickerButton { Color = C(profile.Color), EditAlpha = false, CustomMinimumSize = new(0, 44) }; box.AddChild(picker);
        var palette = new HBoxContainer(); box.AddChild(palette);
        foreach (string color in FleetPresentation.PlayerColors)
        {
            var swatch = new Button { CustomMinimumSize = new(52, 30), TooltipText = color };
            swatch.AddThemeStyleboxOverride("normal", Box(color, _colors.Grid));
            swatch.AddThemeStyleboxOverride("hover", Box(color, _colors.Text));
            swatch.Pressed += () => { picker.Color = C(color); picker.EmitSignal(ColorPickerButton.SignalName.ColorChanged, picker.Color); };
            palette.AddChild(swatch);
        }
        Text(box, "Player badge", 15, _colors.Muted);
        var emblems = new OptionButton { CustomMinimumSize = new(0, 48) };
        emblems.AddThemeConstantOverride("icon_max_width", 32);
        foreach (string id in FleetPresentation.Emblems)
            emblems.AddIconItem(EmblemTexture(id == "auto" ? "kushan" : id), FleetPresentation.EmblemName(id));
        emblems.Select(Array.IndexOf(FleetPresentation.Emblems, profile.Emblem)); box.AddChild(emblems);
        Text(box, "By faction: Kushan badge for Kushan ships, Taiidan badge for Taiidan ships. You can also choose a kiith badge.", 16, _colors.Muted);
        var feedback = Text(box, "Grey is reserved for NPCs and neutral objects. Player colors must be bright and saturated enough.", 15, _colors.Muted);
        var previewBox = Panel(710, 180, 660, 650);
        Text(previewBox, "BATTLEFIELD PREVIEW", 19, _colors.Highlight);
        var preview = new IdentityPreview { CustomMinimumSize = new(0, 280), Tint = C(profile.Color), Emblem = EmblemTexture(profile.Emblem == "auto" ? "kushan" : profile.Emblem) };
        previewBox.AddChild(preview);
        var previewName = Text(previewBox, profile.Name, 23, _colors.Text);
        Text(previewBox, "The flat colored projection follows the cube hulls. A tactical symbol identifies the ship class; a badge and name identify its owner.", 17);
        Text(previewBox, _emblems.Count > 0 ? $"Cached local HWRM badges: {_emblems.Count}. They are not included in the repository." : "Placeholder symbols are active. Original badges can be read from your HWRM installation.", 14, _colors.Muted);
        var installation = new LineEdit { Text = _settings.HwrmInstallation, PlaceholderText = "Homeworld Remastered folder", CustomMinimumSize = new(0, 40) }; previewBox.AddChild(installation);
        Button(previewBox, "IMPORT BADGES FROM HWRM", () =>
        {
            if (!System.IO.File.Exists(System.IO.Path.Combine(installation.Text, "HomeworldRM", "Data", "Homeworld2.big")))
            { feedback.Text = "HomeworldRM/Data/Homeworld2.big was not found in this folder."; return; }
            _settings = _settings with { HwrmInstallation = installation.Text }; SaveSettings(); LoadEmblems(import: true);
            for (int i = 0; i < FleetPresentation.Emblems.Length; i++)
                emblems.SetItemIcon(i, EmblemTexture(i == 0 ? "kushan" : FleetPresentation.Emblems[i]));
            string current = FleetPresentation.Emblems[emblems.Selected];
            preview.Emblem = EmblemTexture(current == "auto" ? "kushan" : current); preview.QueueRedraw();
            feedback.Text = $"Badges imported: {_emblems.Count}. Your current name and color remain in the form.";
        });
        picker.ColorChanged += color =>
        {
            preview.Tint = color; preview.QueueRedraw();
            feedback.Text = FleetPresentation.IsPlayerColor("#" + color.ToHtml(false)) ? "This color is available." : "Choose a brighter, more saturated color: grey and dark shades are reserved.";
        };
        name.TextChanged += value => previewName.Text = value;
        emblems.ItemSelected += i => { string id = FleetPresentation.Emblems[(int)i]; preview.Emblem = EmblemTexture(id == "auto" ? "kushan" : id); preview.QueueRedraw(); };
        Button(box, "SAVE PROFILE", () =>
        {
            string color = "#" + picker.Color.ToHtml(false);
            if (!FleetPresentation.IsPlayerColor(color)) { feedback.Text = "This color is too close to neutral or too dark. Choose a bright color."; return; }
            _settings = _settings with { Player = FleetPresentation.Normalize(new(name.Text, color, FleetPresentation.Emblems[emblems.Selected])) };
            SaveSettings(); OptionsMenu();
        });
        Button(box, "BACK", OptionsMenu);
    }
}

public partial class IdentityPreview : Control
{
    public Color Tint { get; set; }
    public Texture2D? Emblem { get; set; }
    public override void _Draw()
    {
        var center = new Vector2(Size.X * 0.5f, 135);
        Vector2[] Shadow(float x, float y) => [center + new Vector2(x - 36, y), center + new Vector2(x, y - 18), center + new Vector2(x + 36, y), center + new Vector2(x, y + 18)];
        for (int i = 0; i < 3; i++)
        {
            float x = (i - 1) * 75;
            var shadow = Shadow(x, 68);
            DrawColoredPolygon(shadow, new Color(Tint, 0.55f));
            DrawPolyline(shadow.Append(shadow[0]).ToArray(), Tint, 2, true);
            var top = Shadow(x, 0);
            DrawColoredPolygon(top, new Color("#55b9d9"));
            DrawColoredPolygon([top[0], top[3], top[3] + new Vector2(0, 38), top[0] + new Vector2(0, 38)], new Color("#226c89"));
            DrawColoredPolygon([top[3], top[2], top[2] + new Vector2(0, 38), top[3] + new Vector2(0, 38)], new Color("#3596b5"));
        }
        DrawTextureRect(TacticalTextures.Icon("triangle", Tint), new Rect2(center + new Vector2(-40, -108), new(40, 40)), false);
        if (Emblem != null) DrawTextureRect(Emblem, new Rect2(center + new Vector2(15, -110), new(44, 44)), false);
    }
}
