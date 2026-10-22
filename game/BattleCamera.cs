using Godot;

namespace FleetCommand.Game;

public partial class Main
{
    private Vector3 _cameraTarget;
    private float _cameraYaw, _cameraPitch, _cameraZoom = 1, _cameraBaseSize;
    private bool _orbitDragging;

    private void ResetBattleCamera(bool update = true)
    {
        _cameraYaw = 0.0624f;
        _cameraPitch = 0.982f;
        _cameraZoom = 1;
        _orbitDragging = false;
        if (update) UpdateBattleCamera();
    }

    private void UpdateBattleCamera()
    {
        if (_state == null || !GodotObject.IsInstanceValid(_camera)) return;
        var flat = Mathf.Cos(_cameraPitch);
        _camera.Position = _cameraTarget + new Vector3(Mathf.Sin(_cameraYaw) * flat, Mathf.Sin(_cameraPitch), Mathf.Cos(_cameraYaw) * flat) * 32;
        _camera.Size = _cameraBaseSize * _cameraZoom;
        _camera.LookAt(_cameraTarget);
    }

    public override void _Input(InputEvent input)
    {
        if (!_orbitDragging) return;
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: false })
        {
            _orbitDragging = false;
            GetViewport().SetInputAsHandled();
        }
        else if (input is InputEventMouseMotion motion && _state != null)
        {
            _cameraYaw -= motion.Relative.X * 0.008f;
            _cameraPitch = Mathf.Clamp(_cameraPitch + motion.Relative.Y * 0.006f, 0.35f, 1.35f);
            UpdateBattleCamera();
            if (motion.Relative.Length() > 0) TutorialCameraMoved();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMWindowFocusOut) _orbitDragging = false;
    }

    private bool HandleBattleCamera(InputEvent input)
    {
        if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F })
        {
            ResetBattleCamera(); GetViewport().SetInputAsHandled(); return true;
        }
        if (input is not InputEventMouseButton { Pressed: true } click) return false;
        if (click.ButtonIndex == MouseButton.Right) _orbitDragging = true;
        else if (click.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown)
        {
            _cameraZoom = Mathf.Clamp(_cameraZoom * (click.ButtonIndex == MouseButton.WheelUp ? 0.9f : 1.1f), 0.4f, 2);
            UpdateBattleCamera();
        }
        else return _orbitDragging;
        GetViewport().SetInputAsHandled(); return true;
    }
}
