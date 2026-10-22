using Godot;
using FleetCommand.Core;
using System.Text.Json;

namespace FleetCommand.Game;

public partial class Main
{
    private async Task CheckPresentation()
    {
        var original = _settings;
        try
        {
            void Check(bool condition, string description)
            {
                if (!condition) throw new InvalidOperationException(description);
                GD.Print("PRESENTATION_PASS " + description);
            }
            async Task Frame() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            IEnumerable<T> Nodes<T>(Node root) where T : Node => root.GetChildren().SelectMany(child =>
                (child is T target ? new[] { target } : Array.Empty<T>()).Concat(Nodes<T>(child)));
            void Press(string title) => Nodes<Button>(_ui).Single(b => b.Text == title && !b.IsQueuedForDeletion()).EmitSignal(BaseButton.SignalName.Pressed);
            PlayerMenu(); await Frame(); await Frame();
            var picker = Nodes<ColorPickerButton>(_ui).Single();
            var name = Nodes<LineEdit>(_ui).First();
            var emblem = Nodes<OptionButton>(_ui).Single();
            picker.Color = C(FleetPresentation.NeutralColor); Press("СОХРАНИТЬ ПРОФИЛЬ");
            Check(_settings == original, "profile UI rejects the NPC/neutral gray");
            name.Text = "Пилот"; picker.Color = C("#68ca77"); emblem.Select(Array.IndexOf(FleetPresentation.Emblems, "kiith-sjet"));
            Press("СОХРАНИТЬ ПРОФИЛЬ"); await Frame(); await Frame();
            var saved = JsonData.Read<AppSettings>(System.IO.Path.Combine(_data, "settings.json"));
            Check(saved.Player is { Name: "Пилот", Color: "#68ca77", Emblem: "kiith-sjet" }, "profile UI saves selected identity");
            StartBattle("core:training", true, persist: false); await Frame(); await Frame();
            Check(Commander(0) is { Name: "Пилот", Color: "#68ca77", Emblem: "kiith-sjet", IsNpc: false }
                && Commander(1).IsNpc && Commander(1).Color == FleetPresentation.NeutralColor && Commander(1).Emblem == "taiidan",
                "player and NPC receive separate colors and faction badges");
            Check(_world.GetChildren().OfType<MeshInstance3D>().Count(n => n.Mesh is PlaneMesh) >= _state!.Units.Count,
                "ship and object footprints are rendered on the board");
            Check(Nodes<Label>(_annotations).Any(l => l.Text == "Астероиды") && Nodes<Label>(_annotations).Any(l => l.Text.Contains("Перехватчики")), "ship and terrain names are visible annotations");
            GetViewport().PushInput(new InputEventKey { Keycode = Key.Alt, Pressed = true }, true); await Frame();
            var annotation = Nodes<PanelContainer>(_annotations).First(p => p.TooltipText.Contains("Стоимость движения"));
            annotation.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Left });
            await Frame(); await Frame();
            Check(_inspectedObject != null && Nodes<Label>(_ui).Any(l => l.Text.Contains("Штраф к кубикам атаки")), "clicking an object annotation opens its effects");
            var loadedIdentity = ValidateCommanders(JsonSerializer.Deserialize<CommanderAppearance[]>(JsonSerializer.Serialize(_commanders, JsonData.Options), JsonData.Options));
            Check(loadedIdentity.SequenceEqual(_commanders), "appearance survives save/snapshot serialization");
            Check(Nodes<Label>(_ui).Where(l => l.Text == "Пилот · игрок" || l.Text == "НПЦ · компьютер").All(l => l.Size.X > 100 && l.Size.Y < 60), "owner legend fits without character-by-character wrapping");
            GetViewport().PushInput(new InputEventKey { Keycode = Key.Alt, Pressed = false }, true);
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "identification.png"));
            MainMenu();
            _settings = original; SaveSettings();
            GD.Print("PRESENTATION_CHECKS_OK"); GetTree().Quit();
        }
        catch (Exception e) { _settings = original; SaveSettings(); GD.PushError("PRESENTATION_CHECKS_FAILED " + e); GetTree().Quit(1); }
    }
}
