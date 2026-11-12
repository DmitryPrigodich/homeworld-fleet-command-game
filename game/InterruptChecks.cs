using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private async Task CheckInterrupts()
    {
        try
        {
            void Require(bool value, string text) { if (!value) throw new Exception(text); GD.Print("INTERRUPT_UI_PASS " + text); }
            async Task Frames() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            IEnumerable<Button> Buttons(Node root) => root.GetChildren().SelectMany(child => (child is Button b ? new[] { b } : Array.Empty<Button>()).Concat(Buttons(child)));
            void Press(string text) { var b = Buttons(_ui).Single(b => b.Text == text && !b.IsQueuedForDeletion()); Require(!b.Disabled, "Enabled: " + text); b.EmitSignal(BaseButton.SignalName.Pressed); }
            System.IO.File.WriteAllText(SavePath(false), "lesson-save-marker");
            CardGuide(); await Frames(); Press("TRAINING: INTERRUPT"); await Frames();
            Require(_interruptLesson && _state!.Cards!.Response != null && LocalCommandSide == 0, "Guide opens an actual enemy attack response");
            Require(_selected == "s0-0" && _state!.Units.Single(u => u.Id == _selected).Activated && _unitActions.AttackTargets.SequenceEqual(new[] { "s1-3" }), "Activated unit has one red response target");
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "interrupt-window.png"));
            var target = _state!.Units.Single(u => u.Id == "s1-3"); var point = _camera.UnprojectPosition(World(new(target.Q, target.R)));
            GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = point }, true);
            GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = point }, true); await Frames();
            Require(_state!.Cards!.Response == null && InterruptRules.Played(_state.Cards, 0).Contains("order-17") && _state.Orders0 == 0, "Board click fires interrupt without spending Play");
            Require(System.IO.File.ReadAllText(SavePath(false)) == "lesson-save-marker", "Lesson preserves the production save");
            Press("REPEAT LESSON"); await Frames(); Require(_state!.Cards!.Response != null, "Lesson can be replayed");
            Press("TUTORIAL MENU"); await Frames(); Require(!_interruptLesson, "Lesson exits cleanly");
            StartBattle(BasicCardBattle.MapId, false, persist: false);
            string[] a = ["order-01", "order-09"], b = ["order-17", "order-18"];
            _state = _state! with { ActiveSide = 0, Cards = new(Enumerable.Range(1, 24).Select(i => $"order-{i:00}").Except(a).Except(b).ToArray(), a, b, [], Phase: "orders"),
                Units = _state.Units.Select(u => u.Id == "s1-3" ? u with { Q = 2, R = 3 } : u).ToList() };
            Require(ApplyCommand(0, new("move", "s0-0", Q: 3, R: 2, Card: "order-01")), "Actual match starts a movement pause"); await Frames();
            Require(_state!.Cards!.Response is { RemainingPath.Length: > 0 } && LocalCommandSide == 1, "Hotseat switches to responding player midway through a move");
            string snapshot = _engine.Save(_state); SaveGame(); LoadGame(false); await Frames();
            Require(_engine.Save(_state!) == snapshot && _state!.Cards!.Response != null, "UI save/load preserves the paused movement");
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "interrupt-move.png"));
            for (int i = 0; _state!.Cards!.Response != null && i < 5; i++) { Press("SKIP WINDOW"); await Frames(); }
            Require(_state!.Cards!.Response == null && _state.Units.Single(u => u.Id == "s0-0") is { Q: 3, R: 2 }, "Declining resumes the saved movement to its destination");
            GD.Print("INTERRUPT_UI_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
