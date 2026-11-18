using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private int _launchOrderNetworkStep;
    private void ConfigureLaunchOrderBattle(int side)
    {
        string[] a = ["core:all-ahead-full#1"], b = ["core:ready-to-launch#1"];
        if (side == 0) (a, b) = (b, a);
        _state = _state! with { ActiveSide = side, Cards = _state.Cards! with { Phase = "orders", Hand0 = a, Hand1 = b, Discard = [],
            Deck = CommandCards.Instances(CommandCards.Deck(_content, _state)).Keys.Except(a).Except(b).ToArray() },
            Units = _state.Units.Select(u => u.Id == (side == 0 ? "s0-0" : "s1-5") ? u with { Activated = true, Effects = ["brace"] } : u).ToList() };
        DrawBattle();
    }
    private void LaunchOrderNetworkHost(BattleCommand? command)
    {
        string before = _engine.Save(_state!); LoadGame(false);
        if (_engine.Save(_state!) != before) throw new InvalidDataException("Launch order disk restore diverged.");
        var parent = _state!.Units.Single(u => u.Id == "s1-5");
        if (!parent.Activated || !FleetRuleTools.Has(parent, "brace")) throw new InvalidDataException("Launch reordered its carrier.");
        if (command?.Kind == "special")
        { if (_state.Cards!.Pending?.Step != 0 || !_state.Reserves!.Single(u => u.Id == "s1-6").Activated) throw new InvalidDataException("Offboard assignment lost."); GD.Print("LAUNCH_ASSIGNMENT_NETWORK_RESUMED"); }
        if (command?.Kind == "launch")
        { if (_state.Cards!.Pending?.Step != 1 || parent.Launches != 1 || !_state.Units.Single(u => u.Id == "s1-6").Activated) throw new InvalidDataException("Launch continuation lost."); GD.Print("LAUNCH_PLACEMENT_NETWORK_RESUMED"); }
        if (command?.Kind == "finish-order")
        { GD.Print("LAUNCH_ORDER_NETWORK_OK"); GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit(); }
    }
    private void LaunchOrderNetworkClient()
    {
        if (_state!.Map != BasicCardBattle.LaunchOrdersMapId) throw new InvalidDataException("Wrong launch training map.");
        if (_launchOrderNetworkStep == 0)
        { _launchOrderNetworkStep = 1; Submit(new("special", "s1-6", Card: "core:ready-to-launch#1")); }
        else if (_launchOrderNetworkStep == 1 && _state.Cards!.Pending?.Step == 0)
        { var cell = FleetLaunchOrders.Cells(_content, _state, "s1-6").First(); _launchOrderNetworkStep = 2; Submit(new("launch", "s1-6", Q: cell.Q, R: cell.R, Facing: 3)); }
        else if (_launchOrderNetworkStep == 2 && _state.Cards!.Pending?.Step == 1)
        { _launchOrderNetworkStep = 3; Submit(new("finish-order")); }
        else if (_launchOrderNetworkStep == 3 && _state.Cards!.Pending == null)
        { if (_state.Orders1 != 1) throw new InvalidDataException("Launch spent extra Play."); _launchOrderNetworkStep = 4; GD.Print("LAUNCH_ORDER_NETWORK_OK"); GetTree().Quit(); }
    }
    private async Task CheckLaunchOrder()
    {
        try
        {
            void Require(bool ok, string why) { if (!ok) throw new Exception(why); GD.Print("LAUNCH_ORDER_UI_PASS " + why); }
            async Task Frames() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            IEnumerable<Node> Nodes(Node root) => root.GetChildren().Where(n => !n.IsQueuedForDeletion()).SelectMany(n => new[] { n }.Concat(Nodes(n)));
            void Press(string text) { var b = Nodes(_ui).OfType<Button>().Single(b => b.Text == text); Require(!b.Disabled, "Enabled: " + text); b.EmitSignal(BaseButton.SignalName.Pressed); }
            SinglePlayerMenu(); await Frames(); Press("LAUNCH ORDERS"); _bot = false; ConfigureLaunchOrderBattle(0); _selectedCard = "core:ready-to-launch#1";
            SelectUnit("s0-1"); await Frames(); Press("SPECIAL ORDER"); await Frames();
            string before = _engine.Save(_state!); SaveGame(); LoadGame(false); await Frames(); Require(before == _engine.Save(_state!), "Offboard squadron assignment restores");
            Require(_world.GetChildren().OfType<MeshInstance3D>().Any(n => n.Name == "MoveHighlights"), "Launch exit hexes highlighted");
            var cell = FleetLaunchOrders.Cells(_content, _state!, "s0-1").First(); var screen = _camera.UnprojectPosition(World(cell));
            _UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = screen }); await Frames();
            Require(_state!.Cards!.Pending?.Step == 1 && _state.Units.Any(u => u.Id == "s0-1") && _state.Units.Single(u => u.Id == "s0-0") is { Activated: true, Launches: 1 }, "Field click launches squadron and keeps carrier activated");
            before = _engine.Save(_state); SaveGame(); LoadGame(false); await Frames(); Require(before == _engine.Save(_state!), "Launched action step restores");
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "ready-to-launch.png"));
            _locale.SetLanguage("ru"); LocalizeInterface(); await Frames(); Require(_locale.Text("Ready to Launch") == "Готовность к запуску", "Launch card has Russian text");
            _locale.SetLanguage("en"); LocalizeInterface(); Press("FINISH ORDER"); await Frames(); Require(_state.Cards.Pending == null && _state.Orders0 == 1, "Launch special spends one Play");
            NetworkMenu(); await Frames(); Require(Nodes(_ui).OfType<CheckBox>().Any(c => c.Text == "Launch orders"), "Launch training can be hosted");
            GD.Print("LAUNCH_ORDER_UI_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
