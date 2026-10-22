using Godot;
using FleetCommand.Core;

namespace FleetCommand.Game;

public partial class Main
{
    // Opt-in integration check: real menu signals, viewport input and battle snapshots.
    private async Task CheckControls()
    {
        try
        {
            void Check(bool condition, string description)
            {
                if (!condition) throw new InvalidOperationException(description);
                GD.Print("CONTROL_PASS " + description);
            }
            async Task Frame() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            IEnumerable<Button> Buttons(Node node) => node.GetChildren().SelectMany(child =>
                (child is Button button ? new[] { button } : Array.Empty<Button>()).Concat(Buttons(child)));
            void Press(string title)
            {
                var button = Buttons(_ui).Single(b => b.Text == title && !b.IsQueuedForDeletion());
                Check(!button.Disabled, "button enabled: " + title);
                button.EmitSignal(BaseButton.SignalName.Pressed);
            }
            void InputEvent(InputEvent input) => GetViewport().PushInput(input, true);
            async Task ClickCell(Hex hex)
            {
                var position = _camera.UnprojectPosition(World(hex));
                InputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = position });
                InputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = position });
                await Frame(); await Frame();
            }
            string ReadSave(bool campaign) => System.IO.File.Exists(SavePath(campaign)) ? System.IO.File.ReadAllText(SavePath(campaign)) : "";
            string skirmish = ReadSave(false), campaign = ReadSave(true);
            string objects = string.Join(',', _content.Objects.Keys.Order());
            Press("ОБУЧЕНИЕ"); await Frame(); await Frame();
            Check(_tutorial?.Step == TutorialStep.Welcome, "main menu opens dedicated tutorial");
            Press("НАЧАТЬ ОБУЧЕНИЕ"); await Frame(); await Frame();
            var before = _camera.Transform;
            InputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true, Position = new(650, 450) });
            InputEvent(new InputEventMouseMotion { Position = new(700, 470), Relative = new(50, 20), ButtonMask = MouseButtonMask.Right });
            await Frame(); await Frame();
            Check(_tutorial?.Step == TutorialStep.Select && _camera.Transform != before && _state?.Sequence == 0, "right drag orbits camera without issuing an order");
            InputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = false, Position = new(1200, 300) });
            Check(!_orbitDragging, "right release over HUD ends drag");
            float zoom = _camera.Size;
            InputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.WheelUp, Pressed = true, Position = new(650, 450) });
            Check(_camera.Size < zoom, "wheel zooms battlefield");
            InputEvent(new InputEventKey { Keycode = Key.F, Pressed = true }); await Frame();
            Check(Mathf.IsEqualApprox(_cameraZoom, 1), "F resets view");
            InputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true, Position = new(650, 450) });
            InputEvent(new InputEventMouseMotion { Position = new(665, 442), Relative = new(15, -8), ButtonMask = MouseButtonMask.Right });
            InputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = false, Position = new(665, 442) });
            await Frame();
            before = _camera.Transform;
            await ClickCell(new(1, 2));
            Check(_tutorial?.Step == TutorialStep.Move && _selected == TutorialLesson.ScoutId, "left click selects highlighted unit");
            Check(_camera.Transform == before, "camera survives selection redraw");
            Submit(new("pass")); await Frame(); await Frame();
            Check(_tutorial?.Step == TutorialStep.Move && _state?.Sequence == 0, "wrong tutorial action does not advance battle");
            // Save the move-stage image for visual inspection.
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "tutorial-move.png"));
            await ClickCell(TutorialLesson.Destination);
            Check(_tutorial?.Step == TutorialStep.EndTurn && _state?.ActiveSide == 0, "highlighted move and passive opponent advance lesson");
            Check(_camera.Transform == before, "camera survives an accepted order");
            Press("ЗАВЕРШИТЬ ХОД"); await Frame(); await Frame();
            Check(_tutorial?.Step == TutorialStep.Attack && _state?.Round == 2, "end turn refreshes units for shooting");
            Press("АТАКА"); await Frame(); await Frame();
            int sequence = _state!.Sequence;
            await ClickCell(new(2, 2));
            Check(_state.Sequence == sequence, "attack mode does not move to an empty hex");
            await ClickCell(new(4, 2));
            Check(_tutorial?.Step == TutorialStep.Complete, "target click completes the guided lesson");
            Check(skirmish == ReadSave(false) && campaign == ReadSave(true), "tutorial leaves existing saves untouched");
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "tutorial-complete.png"));
            Press("ГЛАВНОЕ МЕНЮ"); await Frame(); await Frame();
            Check(_tutorial == null && _state == null && !_orbitDragging, "leaving tutorial resets control state");
            Check(string.Join(',', _content.Objects.Keys.Order()) == objects && !_content.Maps.ContainsKey(TutorialLesson.MapId), "tutorial content cannot change later generated maps");
            GD.Print("CONTROLS_CHECKS_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("CONTROLS_CHECKS_FAILED " + e); GetTree().Quit(1); }
    }
}
