using Godot;
using FleetCommand.Core;

namespace FleetCommand.Game;

public partial class Main
{
    private UnitActions _unitActions = UnitActions.Empty;

    private void DrawActionHighlights()
    {
        if (_state?.Reinforce is { } frame && frame.Side == LocalCommandSide && _state.Hazard == null && _state.Combat == null && _state.Defense == null && _state.Repairs is not { Length: > 0 })
        { _unitActions = UnitActions.Empty; if (!_productionToBay || frame.Mode != "build") HighlightHexes(ReinforceCells(), C("#3bdb7b"), "ReinforceHighlights"); return; }
        _unitActions = _state == null || _selected == "" ? UnitActions.Empty : _engine.GetActions(_state, LocalCommandSide, _selected);
        if (FleetBattle && _state!.Units.Find(u => u.Id == _selected) is { } selected)
        {
            if (_content.Ships[selected.Definition].Footprint == 2 && _facingChoice >= 0)
                _unitActions = _unitActions with { MoveCells = _unitActions.MoveCells.Where(h => FleetMovement.Path(_content, _state, selected, h, _facingChoice) != null).ToArray() };
            string weapon = FleetWeapons.Weapons(_content, _content.Ships[selected.Definition]).Any(w => w.Id == _weaponChoice) ? _weaponChoice : "";
            _unitActions = _unitActions with { AttackTargets = _unitActions.AttackTargets.Where(id => FleetCombat.Allowed(_content, _state, selected, _state.Units.Find(u => u.Id == id), new("attack", Weapon: weapon, AttackKind: _attackKindChoice))).ToArray() };
        }
        if (_tutorial != null)
            _unitActions = new(_unitActions.MoveCells.Where(h => _tutorial.Allows(new("move", _selected, Q: h.Q, R: h.R))).ToArray(),
                _unitActions.AttackTargets.Where(id => _tutorial.Allows(new("attack", _selected, id))).ToArray());
        HighlightHexes(_unitActions.MoveCells, C("#3bdb7b"), "MoveHighlights");
        var targets = _state?.Units.Where(u => _unitActions.AttackTargets.Contains(u.Id)).SelectMany(u => FleetFootprint.Cells(_content, u)) ?? [];
        HighlightHexes(targets, C("#f36d68"), "AttackHighlights");
    }

    private void HighlightHexes(IEnumerable<Hex> cells, Color color, string name)
    {
        var hexes = cells.ToArray();
        if (hexes.Length == 0) return;
        var fill = new ImmediateMesh(); fill.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        var border = new ImmediateMesh(); border.SurfaceBegin(Mesh.PrimitiveType.Lines);
        foreach (var hex in hexes)
        {
            var center = World(hex);
            for (int side = 0; side < 6; side++)
            {
                float a = Mathf.DegToRad(side * 60), b = Mathf.DegToRad((side + 1) * 60);
                var first = center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 0.94f;
                var second = center + new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * 0.94f;
                fill.SurfaceAddVertex(center); fill.SurfaceAddVertex(first); fill.SurfaceAddVertex(second);
                border.SurfaceAddVertex(first); border.SurfaceAddVertex(second);
            }
        }
        fill.SurfaceEnd(); border.SurfaceEnd();
        _world.AddChild(new MeshInstance3D { Name = name, Mesh = fill, Position = Vector3.Up * 0.012f,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(color, 0.2f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled } });
        _world.AddChild(new MeshInstance3D { Name = name + "Borders", Mesh = border, Position = Vector3.Up * 0.045f,
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } });
    }
}
