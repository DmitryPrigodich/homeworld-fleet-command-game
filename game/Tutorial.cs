using Godot;
using FleetCommand.Core;

namespace FleetCommand.Game;

public partial class Main
{
    private TutorialLesson? _tutorial;
    private bool _interruptLesson;
    private ContentCatalog? _contentBeforeTutorial;

    private void EndTutorial()
    {
        _tutorial = null; _interruptLesson = false; _orbitDragging = false;
        if (_contentBeforeTutorial == null) return;
        _content = _contentBeforeTutorial; _contentBeforeTutorial = null;
        _engine = new(_content);
    }

    private void StartTutorial()
    {
        StopNetwork();
        EndTutorial();
        _contentBeforeTutorial = _content;
        _content = _content.CopyForRuntime();
        _engine = new(_content);
        TutorialLesson.Register(_content);
        _tutorial = new();
        StartBattle(TutorialLesson.MapId, false, persist: false, preserveTutorial: true);
        _status = "Training battle: complete the tasks on the right. You may return to the menu at any time.";
        DrawBattle();
    }

    private void TutorialCameraMoved()
    {
        if (_tutorial?.Step != TutorialStep.Camera) return;
        _tutorial.Continue();
        _status = "Camera rotated. Use the wheel to zoom and F to reset the view.";
        DrawBattle();
    }

    private void SelectUnit(string id)
    {
        if (_tutorial is { Step: < TutorialStep.Select }) return;
        if (_tutorial != null && id != TutorialLesson.ScoutId)
        {
            _status = "In this lesson we command blue Interceptor unit #1. Select it on the field or in the left roster.";
            DrawBattle(); return;
        }
        _selected = id;
        _inspectedObject = null;
        _tutorial?.Select(id);
        DrawBattle();
    }

    private void SetBattleAction(string action)
    {
        _action = action; DrawBattle();
    }

    private void DrawTutorial(Control parent)
    {
        if (_tutorial == null) return;
        var (title, body) = _tutorial.Step switch
        {
            TutorialStep.Welcome => ("1 / 7   THE BATTLEFIELD", "Blue is your fleet; red is the enemy. Grey objects cannot be passed through.\n\nA group of cubes is one unit. ×6 means six ships. Large cubes are frigates.\n\nThe basic battle goal is to destroy the enemy fleet. This lesson introduces the controls in a few actions."),
            TutorialStep.Camera => ("2 / 7   LOOK AROUND", "Point at the battlefield. Hold the RIGHT mouse button and drag to view the field from different angles.\n\nWheel — zoom in or out.\nF — reset the view.\n\nRotate the camera to continue."),
            TutorialStep.Select => ("3 / 7   SELECT YOUR SHIPS", "LEFT-click the blue Interceptors inside the gold ring. You may also select them from the left roster.\n\nThis is unit #1: six small cubes. Hold Alt to see names on the field.\n\nHints and order buttons appear on the right."),
            TutorialStep.Move => ("4 / 7   MOVEMENT", "Interceptor unit #1 selected.\n\nClick MOVE, then LEFT-click the gold hex marked HERE. This is two steps across the grid.\n\nMove 3 is your movement allowance in hexes. After an order the unit darkens and waits for a new round."),
            TutorialStep.EndTurn => ("5 / 7   ORDERS AND ROUNDS", "The ships moved and darkened: they have already received their order this round.\n\nThe sides alternate orders. The training opponent passed.\n\nClick END TURN to pass your remaining orders. A new round will begin and your ships will be ready again."),
            TutorialStep.Attack => ("6 / 7   OPEN FIRE", "Interceptor unit #1 is ready again.\n\nClick ATTACK, then LEFT-click the red cubes inside the gold ring. They are within range 1.\n\nDice determine hits; armor reduces casualties. Even a miss spends the order. Moving and attacking are separate orders.\n\nThe result appears in the bottom log."),
            _ => ("7 / 7   TUTORIAL COMPLETE", "You selected a unit, moved it, ended your turn and attacked. Battle history and rolls are in the bottom log.\n\nIn battle: select a ready blue unit → choose an order → click a hex or enemy.\n\nAlt — names · Right-drag — orbit · Wheel — zoom · F — reset view.\n\nTry a skirmish with small fleets and no objects.")
        };
        Text(parent, title, 17, _colors.Highlight);
        Text(parent, body, 16);
        if (_tutorial.Step == TutorialStep.Welcome)
        {
            Button(parent, "START TUTORIAL", () => { _tutorial.Continue(); DrawBattle(); });
            Button(parent, "CARDS AND SPECIAL ORDERS", CardGuide);
        }
        if (_tutorial.Step == TutorialStep.Camera)
            Button(parent, "SKIP CAMERA LESSON", () => { _tutorial.Continue(); DrawBattle(); });
        if (_tutorial.Step == TutorialStep.Complete)
        {
            Button(parent, "SKIRMISH / GENERATOR", () => { EndTutorial(); _state = null; GeneratorMenu(); });
            Button(parent, "PLAY AGAIN", StartTutorial);
        }
    }

    private bool ApplyTutorialCommand(BattleCommand command)
    {
        if (_tutorial == null) return false;
        if (!_tutorial.Allows(command))
        {
            _status = "Complete the task on the right: the target is highlighted in gold.";
            DrawBattle(); return true;
        }
        var result = _engine.Apply(_state!, 0, command);
        if (!result.Accepted) { _status = result.Message; DrawBattle(); return true; }
        _state = result.State;
        _tutorial.Accepted(command);
        if (command.Kind == "move") _status = "Interceptors moved. The training opponent passes its orders.";
        if (command.Kind == "pass")
        {
            _selected = TutorialLesson.ScoutId;
            _status = "New round: units are ready again. Your Interceptors are selected — try an attack.";
        }
        if (command.Kind == "attack")
        {
            int remaining = _state.Units.FirstOrDefault(u => u.Id == TutorialLesson.TargetId)?.Models ?? 0;
            _status = $"Volley complete. Enemy losses: {6 - remaining}, remaining: {remaining}. Order spent. Tutorial complete!";
        }
        // A passive training opponent keeps the real alternating-order/round rules intact.
        if (_state.ActiveSide == 1 && !_state.Finished) _state = _engine.Apply(_state, 1, new("pass")).State;
        DrawBattle(); return true;
    }

    private void DrawTutorialMarker()
    {
        if (_tutorial == null) return;
        Hex? cell = _tutorial.Step switch
        {
            TutorialStep.Select => new(1, 2),
            TutorialStep.Move => TutorialLesson.Destination,
            TutorialStep.Attack => new(4, 2),
            _ => null
        };
        if (cell == null) return;
        _world.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.88f, OuterRadius = 1, Rings = 6, RingSegments = 36 },
            Position = World(cell.Value) + Vector3.Up * 0.08f,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = C(_colors.Highlight), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } });
        if (_tutorial.Step == TutorialStep.Move)
            _world.AddChild(new Label3D { Text = "HERE", Position = World(cell.Value) + Vector3.Up * 0.9f, FontSize = 34,
                PixelSize = 0.01f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Modulate = C(_colors.Highlight) });
    }
}
