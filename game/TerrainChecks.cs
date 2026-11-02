using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private const string TerrainCheckMap = "check:terrain";
    private void ConfigureTerrainContent()
    {
        _content.Ships["core:taiidan-interceptor"] = _content.Ships["core:taiidan-interceptor"] with { Class = "capital", Armor = 20, Success = 2, Evasive = 1, Move = 5, Abilities = [], Weapons = [new("guard", "Guard", 1, 3, DefensiveFire: true)] };
        _content.Maps[TerrainCheckMap] = _content.Maps[BasicCardBattle.TabletopMapId] with { Id = TerrainCheckMap, Name = "Проверка местности", Objects = [new("practice:mines", 2, 2)] };
    }
    private void ConfigureTerrainBattle()
    {
        string[] a = ["order-01", "order-09"], b = ["order-21", "order-22"];
        _state = _state! with { ActiveSide = 1, ValorOwner = 1, RandomState = 1,
            Cards = new(Enumerable.Range(1, 24).Select(i => $"order-{i:00}").Except(a).Except(b).ToArray(), a, b, [], Phase: "orders"),
            Units = _state.Units.Select(u => u.Id == "s1-3" ? u with { Q = 1, R = 2, Models = 3, Facing = 0 } : u.Id == "s0-0" ? u with { Q = 7, R = 1 } : u).ToList() };
    }
    private async Task CheckTerrain()
    {
        try
        {
            void Require(bool value, string text) { if (!value) throw new Exception(text); GD.Print("TERRAIN_UI_PASS " + text); }
            async Task Frames() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            IEnumerable<Node> Nodes(Node root) => root.GetChildren().SelectMany(child => new[] { child }.Concat(Nodes(child))).Where(n => !n.IsQueuedForDeletion());
            void Press(string text) { var b = Nodes(_ui).OfType<Button>().Single(b => b.Text == text); Require(!b.Disabled, "Enabled: " + text); b.EmitSignal(BaseButton.SignalName.Pressed); }
            SinglePlayerMenu(); await Frames(); Press("МИНЫ И АСТЕРОИДЫ"); _bot = false; await Frames(); Require(_state!.Map == BasicCardBattle.TerrainMapId, "Dangerous terrain map is reachable from the menu");
            ConfigureTerrainContent(); StartBattle(TerrainCheckMap, false, persist: false); ConfigureTerrainBattle(); _action = "move"; SelectUnit("s1-3"); await Frames();
            Press("▶ ПЕРЕМЕЩЕНИЕ"); await Frames(); Submit(new("move", "s1-3", Q: 3, R: 2)); await Frames();
            Require(_state!.Hazard?.Kind == "mine" && _state.Units.Single(u => u.Id == "s1-3").Q == 2, "Movement pauses in the mine hex before finishing its path");
            Require(Nodes(_ui).OfType<Label>().Any(l => l.Text.Contains("Мин осталось: 5")), "Defense UI shows the current mine token");
            int markersBefore = Nodes(_annotations).OfType<PanelContainer>().Count();
            string snapshot = _engine.Save(_state); SaveGame(); LoadGame(false); await Frames(); Require(_engine.Save(_state!) == snapshot, "Terrain source, token and path restore from disk");
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "terrain-defense.png"));
            Press("ЗАЩИТА: Guard · 3D"); await Frames(); Press("ПРИНЯТЬ БРОСОК"); await Frames();
            Require(_state!.Combat is { Kind: "terrain", AbilityRerolls: 1 } && FleetTerrain.MineCount(_state, 0) == 5, "Mines keep their current pool until the attack resolves");
            snapshot = _engine.Save(_state); SaveGame(); LoadGame(false); await Frames(); Require(_engine.Save(_state!) == snapshot, "Actual terrain dice survive disk restore");
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "terrain-evasive.png"));
            Press("ПРИМЕНИТЬ ПЕРЕБРОСЫ"); await Frames();
            for (int i = 0; _state!.Cards!.Response != null && i < 20; i++) { Press("ПРОПУСТИТЬ ОКНО"); await Frames(); }
            Require(FleetTerrain.MineCount(_state!, 0) == 0 && Nodes(_annotations).OfType<PanelContainer>().Count() == markersBefore - 1, "Cleared mines disappear from the battlefield annotations");
            Require(_state!.Units.Single(u => u.Id == "s1-3") is { Q: 3, R: 2, FocusFire: 1 } && _state.Orders1 == 1 && _state.Hazard == null, "Original movement resumes once after defense and mine damage");
            snapshot = _engine.Save(_state); SaveGame(); LoadGame(false); await Frames(); Require(_engine.Save(_state!) == snapshot && FleetTerrain.MineCount(_state!, 0) == 0, "Removed mine token does not respawn after loading");
            GD.Print("TERRAIN_UI_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
