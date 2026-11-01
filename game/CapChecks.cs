using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private void ConfigureCapContent()
    {
        foreach (string id in new[] { "core:kushan-interceptor", "core:taiidan-interceptor" })
            _content.Ships[id] = _content.Ships[id] with { Class = "fighter", Faction = "taiidan", Success = 2, Armor = 0, Stars = 1, Abilities = [], Weapons = [new("gun", "Fighter guns", 1, 4)] };
        _content.Ships["core:taiidan-frigate"] = _content.Ships["core:taiidan-frigate"] with { Class = "capital", Success = 2, Armor = 2, Abilities = [], Weapons = [new("flak", "Hull flak", 1, 0, DefensiveFire: true)] };
    }
    private void ConfigureCapBattle()
    {
        string[] a = ["order-01", "order-09"], b = ["order-21", "order-22"];
        _state = _state! with { ActiveSide = 0, ValorOwner = 1, RandomState = 1,
            Cards = new(Enumerable.Range(1, 24).Select(i => $"order-{i:00}").Except(a).Except(b).ToArray(), a, b, [], Phase: "orders"),
            Units = _state.Units.Select(u => u.Id == "s0-0" ? u with { Q = 2, R = 2, Models = 2 } : u.Id == "s1-5" ? u with { Q = 4, R = 2, Models = 1 } : u.Id == "s1-3" ? u with { Q = 4, R = 2, Models = 2, Patrol = "s1-5" } : u).ToList() };
    }
    private async Task CheckCap()
    {
        try
        {
            void Require(bool value, string text) { if (!value) throw new Exception(text); GD.Print("CAP_UI_PASS " + text); }
            async Task Frames() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            IEnumerable<Node> Nodes(Node root) => root.GetChildren().SelectMany(child => new[] { child }.Concat(Nodes(child))).Where(n => !n.IsQueuedForDeletion());
            void Press(string text) { var b = Nodes(_ui).OfType<Button>().Single(b => b.Text == text); Require(!b.Disabled, "Enabled: " + text); b.EmitSignal(BaseButton.SignalName.Pressed); }
            void Fixture() { StartBattle(BasicCardBattle.TabletopMapId, false, persist: false); ConfigureCapBattle(); }
            ConfigureCapContent(); Fixture();
            _state = _state! with { ActiveSide = 1, Units = _state.Units.Select(u => u.Id == "s1-3" ? u with { Patrol = "", Q = 2, R = 3 } : u).ToList() };
            _action = "move"; SelectUnit("s1-3"); await Frames(); Press("▶ ПЕРЕМЕЩЕНИЕ"); await Frames(); Submit(new("move", "s1-3", Q: 3, R: 2)); await Frames();
            for (int i = 0; _state!.CapEntry == null && i < 10; i++) { Press("ПРОПУСТИТЬ ОКНО"); await Frames(); }
            Require(_state!.CapEntry != null, "Movement offers CAP entry through the UI");
            string snapshot = _engine.Save(_state); SaveGame(); LoadGame(false); await Frames(); Require(_engine.Save(_state!) == snapshot, "CAP entry decision survives disk save/load");
            Press(Nodes(_ui).OfType<Button>().Single(b => b.Text.StartsWith("CAP: ")).Text); await Frames();
            Require(_state!.Units.Single(u => u.Id == "s1-3").Patrol == "s1-5", "CAP button attaches the fighter to its host");
            Fixture(); _state = _state! with { ActiveSide = 1 }; _action = "move"; SelectUnit("s1-3"); await Frames();
            Require(Nodes(_ui).OfType<OptionButton>().Any(d => d.GetItemText(0).StartsWith("Выход:")), "Ready CAP has an exit-hex selector");
            Press("▶ ПЕРЕМЕЩЕНИЕ"); await Frames(); Require(_state!.Units.Single(u => u.Id == "s1-3").Patrol == "" && _state.Cards!.Pending?.BasicAction == "move", "UI leaves CAP before beginning the paid order");
            Fixture(); SelectUnit("s0-0"); await Frames(); Press("АТАКА"); await Frames(); Submit(new("attack", "s0-0", "s1-5")); await Frames();
            if (_state!.Defense == null) { Press("ПРОПУСТИТЬ ОКНО"); await Frames(); }
            Press("ПЕРЕХВАТ CAP: Fighter guns"); await Frames();
            Require(_state!.Combat?.Kind == "cap" && _state.Defense?.PreparedIncoming != null && _state.Orders1 == 0, "CAP rolls both dogfight pools before casualties and spends no Play");
            string capDice = string.Join(", ", _state.Combat!.Rolls);
            Press("ПРИНЯТЬ БРОСОК"); await Frames();
            Require(_state!.Combat?.Kind == "dogfight" && _state.Units.Single(u => u.Id == "s0-0").Models == 2, "CAP casualties wait for the incoming fighter's volley");
            Require(Nodes(_ui).OfType<Label>().Any(l => l.Text == "Одновременный бросок противника: [" + capDice + "]"), "Incoming fighter sees the CAP volley rather than its own dice twice");
            snapshot = _engine.Save(_state); SaveGame(); LoadGame(false); await Frames(); Require(_engine.Save(_state!) == snapshot, "Both simultaneous volleys restore from disk");
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "cap-dogfight.png"));
            Press("ПРИНЯТЬ БРОСОК"); await Frames();
            Require(!_state!.Units.Any(u => u.Id is "s0-0" or "s1-3") && _state.Cards!.Response == null && _state.Defense == null, "Mutual dogfight kills cancel the original attack and finish its order");
            GD.Print("CAP_UI_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
