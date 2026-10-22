using Godot;
using FleetCommand.Core;

namespace FleetCommand.Game;

public partial class Main
{
    private MapObject? _inspectedObject;
    private BattleAnnotations _annotations = null!;

    private void BeginAnnotations()
    {
        _annotations = new BattleAnnotations { Camera = _camera, ShowNames = _settings.ShowLabels,
            FieldBounds = new Rect2(300, 100, _tutorial == null ? 835 : 745, 680) };
        _annotations.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _ui.AddChild(_annotations);
    }

    private void AddFootprint(Vector3 position, Vector2 size, float rotation, Color color)
    {
        _world.AddChild(new MeshInstance3D { Name = "OwnershipProjection", Mesh = new PlaneMesh { Size = size },
            Position = position + Vector3.Up * 0.025f, RotationDegrees = new(0, rotation, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(color, 0.5f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled } });
        var lines = new ImmediateMesh(); lines.SurfaceBegin(Mesh.PrimitiveType.Lines);
        Vector3[] corners = [new(-size.X / 2, 0, -size.Y / 2), new(size.X / 2, 0, -size.Y / 2), new(size.X / 2, 0, size.Y / 2), new(-size.X / 2, 0, size.Y / 2)];
        for (int i = 0; i < 4; i++) { lines.SurfaceAddVertex(corners[i]); lines.SurfaceAddVertex(corners[(i + 1) % 4]); }
        lines.SurfaceEnd();
        _world.AddChild(new MeshInstance3D { Mesh = lines, Position = position + Vector3.Up * 0.035f, RotationDegrees = new(0, rotation, 0),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = color, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } });
    }

    private void ObjectAnnotation(MapObject placement, ObjectDefinition obj, float height)
    {
        var color = C(FleetPresentation.NeutralColor);
        string effects = FleetPresentation.ObjectEffects(obj);
        _annotations.Marker(World(new(placement.Q, placement.R)) + Vector3.Up * (height + 0.3f),
            FleetPresentation.ObjectName(obj), "НЕЙТРАЛЬНЫЙ ОБЪЕКТ", TacticalTextures.Icon(obj.BlocksMovement ? "circle" : "hexagon", color), null,
            color, effects + $"\nКлетка {placement.Q}, {placement.R}. Нажми для подробностей.", () => InspectObject(placement));
    }

    private void UnitAnnotation(BattleUnit unit, ShipDefinition ship, float height)
    {
        var owner = Commander(unit.Side);
        var color = C(owner.Color);
        string details = $"{ship.Name}\n{owner.Name} · {unit.Id}\nХод {ship.Move} · Дальность {ship.Range} · Броня {ship.Armor}\n" +
            (unit.Activated ? "Приказ в этом раунде уже выполнен." : "Подразделение готово к приказу.");
        _annotations.Marker(World(new(unit.Q, unit.R)) + Vector3.Up * height,
            $"{FleetPresentation.ShipName(ship)} ×{unit.Models}", $"{owner.Name} · {unit.Id}" + (unit.Activated ? " · ✓" : ""),
            TacticalTextures.Icon(FleetPresentation.ShipIcon(ship), color), EmblemTexture(owner.Emblem), color, details, () => ClickUnit(unit.Id));
    }

    private void ClickUnit(string id)
    {
        var unit = _state?.Units.Find(u => u.Id == id);
        if (unit == null) return;
        if (_action == "attack" && _selected != "" && unit.Side != _state!.ActiveSide) Submit(new("attack", _selected, id));
        else SelectUnit(id);
    }
    private void InspectObject(MapObject obj)
    {
        if (_tutorial != null) { _status = FleetPresentation.ObjectName(_content.Objects[obj.Definition]) + ": " + FleetPresentation.ObjectEffects(_content.Objects[obj.Definition]); DrawBattle(); return; }
        _inspectedObject = obj; _selected = ""; DrawBattle();
    }

    private void OwnerLegend(Control parent)
    {
        foreach (var owner in _commanders)
        {
            var row = new HBoxContainer(); parent.AddChild(row);
            row.AddChild(new TextureRect { Texture = EmblemTexture(owner.Emblem), CustomMinimumSize = new(26, 26),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
            row.AddChild(new ColorRect { Color = C(owner.Color), CustomMinimumSize = new(5, 26) });
            var name = Text(row, owner.Name + (owner.IsNpc ? " · компьютер" : " · игрок"), 14, owner.Color);
            name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        }
    }
}

public partial class BattleAnnotations : Control
{
    private sealed record Annotation(PanelContainer Panel, Vector3 Point, Color Color);
    private readonly List<Annotation> _markers = [];
    private readonly List<(Vector2 From, Vector2 To, Color Color)> _links = [];
    public Camera3D Camera { get; set; } = null!;
    public bool ShowNames { get; set; } = true;
    public Rect2 FieldBounds { get; set; }

    public BattleAnnotations() { MouseFilter = MouseFilterEnum.Ignore; }

    public void Marker(Vector3 point, string name, string owner, Texture2D symbol, Texture2D? emblem, Color color, string tooltip, Action click)
    {
        var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Pass, TooltipText = tooltip };
        var style = new StyleBoxFlat { BgColor = new Color("#07121feb"), BorderColor = new Color(color, 0.65f),
            BorderWidthLeft = 2, ContentMarginLeft = 4, ContentMarginRight = 5, ContentMarginTop = 3, ContentMarginBottom = 3 };
        panel.AddThemeStyleboxOverride("panel", style); AddChild(panel);
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; row.AddThemeConstantOverride("separation", 4); panel.AddChild(row);
        row.AddChild(new TextureRect { Texture = symbol, CustomMinimumSize = new(23, 23), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore });
        if (ShowNames)
        {
            var text = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore }; text.AddThemeConstantOverride("separation", 0); row.AddChild(text);
            var title = new Label { Text = name, MouseFilter = MouseFilterEnum.Ignore }; title.AddThemeFontSizeOverride("font_size", 13); text.AddChild(title);
            var subtitle = new Label { Text = owner, MouseFilter = MouseFilterEnum.Ignore, Modulate = color }; subtitle.AddThemeFontSizeOverride("font_size", 11); text.AddChild(subtitle);
        }
        if (emblem != null) row.AddChild(new TextureRect { Texture = emblem, CustomMinimumSize = new(22, 22),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore });
        panel.GuiInput += input =>
        {
            if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) { panel.AcceptEvent(); click(); }
        };
        _markers.Add(new(panel, point, color));
    }

    public override void _Process(double delta)
    {
        if (!GodotObject.IsInstanceValid(Camera)) return;
        _links.Clear(); var occupied = new List<Rect2>();
        foreach (var marker in _markers.OrderBy(m => Camera.UnprojectPosition(m.Point).Y))
        {
            var anchor = Camera.UnprojectPosition(marker.Point);
            marker.Panel.Visible = !Camera.IsPositionBehind(marker.Point) && FieldBounds.HasPoint(anchor);
            if (!marker.Panel.Visible) continue;
            var size = marker.Panel.GetCombinedMinimumSize();
            var pos = anchor - new Vector2(size.X * 0.5f, size.Y + 8);
            pos.X = Mathf.Clamp(pos.X, FieldBounds.Position.X, Math.Max(FieldBounds.Position.X, FieldBounds.End.X - size.X));
            for (int attempt = 0; attempt < 8 && occupied.Any(r => r.Intersects(new Rect2(pos, size).Grow(2))); attempt++) pos.Y -= size.Y + 4;
            pos.Y = Math.Max(FieldBounds.Position.Y, pos.Y);
            marker.Panel.Position = pos;
            occupied.Add(new(pos, size));
            _links.Add((anchor, pos + new Vector2(size.X * 0.5f, size.Y), new Color(marker.Color, 0.45f)));
        }
        QueueRedraw();
    }
    public override void _Draw()
    {
        foreach (var link in _links) DrawLine(link.From, link.To, link.Color, 1, true);
    }
}
