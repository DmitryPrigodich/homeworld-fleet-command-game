using Godot;
using System.Text.Json;
using FleetCommand.Core;

namespace FleetCommand.Game;

public partial class Main
{
    private Label? _battleStatus;
    private static Vector3 World(Hex h) => new(h.Q * 1.5f, 0, (h.R + h.Q * 0.5f) * 1.73205f);
    private void StartBattle(string mapId, bool bot, bool preserveCampaign = false, bool preserveGeneration = false, bool persist = true, bool preserveTutorial = false)
    {
        if (!preserveTutorial) EndTutorial();
        if (!preserveCampaign) _campaign = null;
        if (!preserveGeneration) _generation = null;
        _state = _engine.Create(mapId, (uint)(_generation?.Seed ?? 12345));
        _bot = bot; _selected = ""; _action = "move"; _status = "Выбери подразделение, затем клетку назначения или цель.";
        _inspectedObject = null; InitializeCommanders();
        ResetBattleCamera(update: false);
        DrawBattle(); if (persist) SaveGame();
    }
    private void DrawBattle()
    {
        if (_state == null) return;
        ClearScreen(); var map = _content.Maps[_state.Map];
        _cameraTarget = World(new(map.Width - 1, map.Height - 1)) * 0.5f;
        _cameraBaseSize = Math.Max(map.Width * 2.35f, map.Height * 2.9f);
        _camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Current = true };
        _world.AddChild(_camera); UpdateBattleCamera();
        BeginAnnotations();
        _world.AddChild(new DirectionalLight3D { RotationDegrees = new(-50, -25, 0), LightEnergy = 1.4f });
        var environment = new WorldEnvironment { Environment = new Godot.Environment { BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = C(map.Background.StartsWith('#') && Color.HtmlIsValid(map.Background) ? map.Background : _colors.Background),
            AmbientLightSource = Godot.Environment.AmbientSource.Color, AmbientLightColor = C(_colors.Text), AmbientLightEnergy = 0.5f } };
        _world.AddChild(environment);
        var lines = new ImmediateMesh(); lines.SurfaceBegin(Mesh.PrimitiveType.Lines);
        for (int q = 0; q < map.Width; q++) for (int r = 0; r < map.Height; r++)
        {
            var point = World(new(q, r));
            for (int i = 0; i < 6; i++)
            {
                float a = Mathf.DegToRad(i * 60), b = Mathf.DegToRad((i + 1) * 60);
                lines.SurfaceAddVertex(point + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)));
                lines.SurfaceAddVertex(point + new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)));
            }
        }
        lines.SurfaceEnd();
        _world.AddChild(new MeshInstance3D { Mesh = lines, MaterialOverride = new StandardMaterial3D { AlbedoColor = C(_colors.Grid), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } });
        foreach (var placement in map.Objects)
        {
            var def = _content.Objects[placement.Definition];
            var size = def.BlocksMovement ? new Vector3(1.1f, 1.2f, 1.1f) : new Vector3(0.75f, 0.35f, 0.75f);
            var position = World(new(placement.Q, placement.R));
            AddFootprint(position, new(size.X + 0.12f, size.Z + 0.12f), 0, C(FleetPresentation.NeutralColor));
            _world.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size },
                Position = position + Vector3.Up * (size.Y * 0.5f + 0.25f),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = C(def.Color), Roughness = 0.9f } });
            ObjectAnnotation(placement, def, size.Y);
        }
        foreach (var unit in _state.Units)
        {
            var def = _content.Ships[unit.Definition]; var point = World(new(unit.Q, unit.R));
            float size = def.VisualSize;
            var color = C(string.IsNullOrEmpty(def.Color) ? unit.Side == 0 ? _colors.Accent : _colors.Enemy : def.Color);
            if (unit.Activated) color = color.Darkened(0.45f);
            for (int i = 0; i < Math.Min(unit.Models, 6); i++)
            {
                var offset = unit.Models > 1 ? new Vector3((i % 3 - 1) * size * 1.3f, 0, (i / 3 - 0.5f) * size * 1.3f) : Vector3.Zero;
                AddFootprint(point + offset, new(size + 0.1f, size * 1.25f + 0.1f), -60 * unit.Facing, C(Commander(unit.Side).Color));
                _world.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new(size, size, size * 1.25f) }, Position = point + offset + Vector3.Up * (size * 0.5f + 0.3f),
                    RotationDegrees = new(0, -60 * unit.Facing, 0), MaterialOverride = new StandardMaterial3D { AlbedoColor = color, Metallic = 0.2f, Roughness = 0.5f } });
            }
            UnitAnnotation(unit, def, size + 0.9f);
            if (unit.Id == _selected)
                _world.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.8f, OuterRadius = 0.86f, Rings = 6, RingSegments = 24 },
                    Position = point + Vector3.Up * 0.05f, MaterialOverride = new StandardMaterial3D { AlbedoColor = C(_colors.Highlight), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } });
        }
        DrawTutorialMarker();
        var top = Panel(30, 22, 1380, 68);
        Text(top, $"{map.Name.ToUpperInvariant()}     /     РАУНД {_state.Round}     /     ХОД: {SideName(_state.ActiveSide)}", 22, _colors.Accent);
        var left = Panel(30, 112, 260, 650);
        Text(left, _tutorial != null ? "ОБУЧЕНИЕ" : _campaign == null ? "СКИРМИШ" : "КАМПАНИЯ", 18, _colors.Highlight);
        Text(left, _tutorial != null ? "СИНИЕ — МЫ / КРАСНЫЕ — ВРАГ" : _generation == null ? "Сценарий из данных" : $"Seed: {_generation.Seed}", 13, _colors.Muted);
        OwnerLegend(left);
        var scroll = new ScrollContainer { CustomMinimumSize = new(0, 320), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        left.AddChild(scroll); var roster = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; scroll.AddChild(roster);
        foreach (var unit in _state.Units.OrderBy(u => u.Side).ThenBy(u => u.Id))
        {
            var ship = _content.Ships[unit.Definition]; var owner = Commander(unit.Side);
            var b = Button(roster, $"{FleetPresentation.ShipName(ship)} ×{unit.Models}\n{owner.Name} · {unit.Id}" + (unit.Activated ? " · ✓" : ""), () => SelectUnit(unit.Id));
            b.Icon = TacticalTextures.Icon(FleetPresentation.ShipIcon(ship), C(owner.Color)); b.ExpandIcon = true;
            b.AddThemeConstantOverride("icon_max_width", 20); b.AddThemeFontSizeOverride("font_size", 13);
            b.CustomMinimumSize = new(0, 54);
            b.TooltipText = _content.Ships[unit.Definition].Name + (unit.Activated ? " — приказ уже выполнен" : " — готово к приказу");
        }
        Text(left, "ЛКМ — выбор / приказ\nПКМ + мышь — обзор\nКолёсико — масштаб\nF — исходный ракурс", 15, _colors.Muted);
        var right = Panel(_tutorial == null ? 1150 : 1060, 112, _tutorial == null ? 260 : 350, 650);
        if (_tutorial != null) right.AddThemeConstantOverride("separation", 7);
        DrawTutorial(right);
        var selected = _state.Units.Find(u => u.Id == _selected);
        if (_tutorial == null || _tutorial.Step is TutorialStep.Move or TutorialStep.EndTurn or TutorialStep.Attack)
            Text(right, _inspectedObject != null ? FleetPresentation.ObjectName(_content.Objects[_inspectedObject.Definition]) : selected == null ? "ПРИКАЗЫ" : FleetPresentation.ShipName(_content.Ships[selected.Definition]), 19, _colors.Highlight);
        if (_inspectedObject != null && _tutorial == null)
        {
            Text(right, "НЕЙТРАЛЬНЫЙ ОБЪЕКТ", 13, FleetPresentation.NeutralColor);
            Text(right, FleetPresentation.ObjectEffects(_content.Objects[_inspectedObject.Definition]), 16);
        }
        if (selected != null && _tutorial == null)
        {
            var def = _content.Ships[selected.Definition];
            Text(right, $"Ход {def.Move} / Дальность {def.Range}\nБроня {def.Armor} / Кубики {def.DicePerModel} × {selected.Models}\nFocus Fire: {selected.FocusFire}", 15);
        }
        if (_tutorial == null || _tutorial.Step is TutorialStep.Move or TutorialStep.EndTurn or TutorialStep.Attack)
        {
            Button(right, _action == "move" ? "▶ ПЕРЕМЕЩЕНИЕ" : "ПЕРЕМЕЩЕНИЕ", () => SetBattleAction("move"), _tutorial != null && _tutorial.Step != TutorialStep.Move);
            Button(right, _action == "attack" ? "▶ АТАКА" : "АТАКА", () => SetBattleAction("attack"), _tutorial != null && _tutorial.Step != TutorialStep.Attack);
            Button(right, "ЗАВЕРШИТЬ ХОД", () => Submit(new("pass")), _tutorial != null && _tutorial.Step != TutorialStep.EndTurn);
        }
        if (_tutorial == null) Button(right, "СОХРАНИТЬ", () => { SaveGame(); _status = "Сохранено."; DrawBattle(); }, _networkActive && !_isHost);
        if (_state.Finished)
        {
            Text(right, _state.Winner == null ? "Лимит раундов. Ничья." : $"Победа: {SideName(_state.Winner.Value)}", 18, _colors.Highlight);
            if (_campaign != null)
                Button(right, "ДАЛЕЕ ПО КАМПАНИИ", AdvanceCampaign, _networkActive && !_isHost);
        }
        Button(right, "ГЛАВНОЕ МЕНЮ", () => { SaveGame(); MainMenu(); });
        if (_tutorial == null) Text(right, _networkActive ? (_cooperative ? "CO-OP / ОБЩИЙ ФЛОТ" : "PVP") : _bot ? "ОДИНОЧНАЯ / AI" : "ЛОКАЛЬНАЯ / HOTSEAT", 13, _colors.Muted);
        var bottom = Panel(30, 795, 1380, 76); _battleStatus = Text(bottom, _status, 16, _colors.Text);
    }
    public override void _UnhandledInput(InputEvent input)
    {
        if (_state == null || HandleBattleCamera(input) || input is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click) return;
        var origin = _camera.ProjectRayOrigin(click.Position); var ray = _camera.ProjectRayNormal(click.Position);
        var hit = new Plane(Vector3.Up, 0).IntersectsRay(origin, ray); if (!hit.HasValue) return;
        var map = _content.Maps[_state.Map];
        Hex? tile = null; float distance = float.MaxValue;
        for (int q = 0; q < map.Width; q++) for (int r = 0; r < map.Height; r++)
        {
            float d = World(new(q, r)).DistanceTo(hit.Value); if (d < distance) { distance = d; tile = new(q, r); }
        }
        if (tile == null || distance > 1.05f) return;
        var target = _state.Units.Find(u => u.Q == tile.Value.Q && u.R == tile.Value.R);
        var obj = map.Objects.FirstOrDefault(o => o.Q == tile.Value.Q && o.R == tile.Value.R);
        if (obj != null && target == null && (_selected == "" || _content.Objects[obj.Definition].BlocksMovement)) { InspectObject(obj); return; }
        if (target != null && (target.Side == _state.ActiveSide || string.IsNullOrEmpty(_selected))) { SelectUnit(target.Id); return; }
        if (string.IsNullOrEmpty(_selected)) return;
        if (_action == "attack" && target != null) Submit(new("attack", _selected, target.Id));
        else if (_action == "move") Submit(new("move", _selected, Q: tile.Value.Q, R: tile.Value.R));
    }
    private void ApplyCommand(int side, BattleCommand command)
    {
        if (_state == null) return;
        var result = _engine.Apply(_state, side, command); _status = result.Message;
        if (result.Accepted) { _state = result.State; SaveGame(); }
        DrawBattle(); if (_networkActive && _isHost) Broadcast(); QueueBot();
    }
    private void QueueBot()
    {
        if (!_bot || _botQueued || _state is null || _state.Finished || _state.ActiveSide != 1 || _networkActive && !_isHost) return;
        _botQueued = true;
        GetTree().CreateTimer(0.4).Timeout += () => { _botQueued = false; BotTurn(); };
    }
    private void BotTurn()
    {
        if (_state == null || !_bot || _state.ActiveSide != 1 || _state.Finished) return;
        foreach (var unit in _state.Units.Where(u => u.Side == 1 && !u.Activated).ToArray())
        {
            var enemies = _state.Units.Where(u => u.Side == 0).ToArray();
            foreach (var target in enemies.OrderBy(u => u.Models))
            {
                var cmd = new BattleCommand("attack", unit.Id, target.Id);
                if (_engine.Apply(_state, 1, cmd).Accepted) { ApplyCommand(1, cmd); return; }
            }
            var map = _content.Maps[_state.Map];
            var cells = Enumerable.Range(0, map.Width).SelectMany(q => Enumerable.Range(0, map.Height).Select(r => new Hex(q, r)))
                .OrderBy(h => enemies.Min(e => h.Distance(new(e.Q, e.R)))).ThenBy(h => h.Distance(new(unit.Q, unit.R)));
            foreach (var h in cells)
            {
                var cmd = new BattleCommand("move", unit.Id, Q: h.Q, R: h.R, Facing: 3);
                if (_engine.Apply(_state, 1, cmd).Accepted) { ApplyCommand(1, cmd); return; }
            }
        }
        ApplyCommand(1, new("pass"));
    }
    private string SavePath(bool campaign) => System.IO.Path.Combine(_data, campaign ? "campaign.json" : "skirmish.json");
    private void SaveGame()
    {
        if (_state == null || _tutorial != null || _networkActive && !_isHost) return;
        var saved = new SavedGame(_state, _generation, _campaign == null ? null : _campaign with { Battle = _state }, _bot, _commanders);
        string path = SavePath(_campaign != null), temp = path + ".pending";
        System.IO.File.WriteAllText(temp, JsonSerializer.Serialize(saved, JsonData.Options));
        System.IO.File.Move(temp, path, true);
    }
    private void LoadGame(bool campaign)
    {
        EndTutorial();
        var saved = JsonData.Read<SavedGame>(SavePath(campaign));
        _generation = saved.Generation;
        if (_generation != null) MapGenerator.Generate(_content, _generation);
        _state = _engine.Load(JsonSerializer.Serialize(saved.State, JsonData.Options));
        _tutorial = null; _campaign = saved.Campaign; _bot = saved.ComputerOpponent; _selected = ""; _action = "move"; _status = "Партия восстановлена.";
        _inspectedObject = null; _commanders = ValidateCommanders(saved.Commanders);
        ResetBattleCamera(update: false);
        if (_campaign?.Completed == true) { ShowCampaignEnd(); return; }
        DrawBattle(); QueueBot();
    }
    private void AdvanceCampaign()
    {
        if (_state == null || _campaign == null) return;
        _campaign = FleetCommand.Core.Campaign.CompleteMission(_content, _campaign, _state);
        if (_campaign.Completed) { SaveGame(); ShowCampaignEnd(); return; }
        var mission = _content.Campaigns[_campaign.Campaign].Missions.Single(m => m.Id == _campaign.CurrentMission);
        StartBattle(mission.Map, _bot, preserveCampaign: true); if (_networkActive && _isHost) Broadcast();
    }
    private void ShowCampaignEnd()
    {
        int medals = _campaign?.Medals ?? 0; _state = null;
        ClearScreen(); Heading("Кампания завершена"); var box = Panel(70, 230, 560, 280);
        Text(box, $"Демонстрационная кампания завершена.\nМедали: {medals}", 22, _colors.Highlight);
        Button(box, "ГЛАВНОЕ МЕНЮ", MainMenu); MenuScene();
    }
}
