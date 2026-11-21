using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private const string SupremePilot = "core:hero-taiidan-veteran-commander#hero-1", LaunchAll = "core:hero-taiidan-launch-commander#hero-1";
    private int _pilotLaunchNetworkStep;
    private void ConfigurePilotLaunchContent()
    {
        foreach (string id in _content.Maps[HeroicBattle.PilotLaunchMapId].Fleets.SelectMany(f => _content.Fleets[f].Units).Select(u => u.Ship).Distinct())
            _content.Ships[id] = _content.Ships[id] with { Armor = 50, Success = 2, Evasive = 0, Weapons = [new("gun", "Pilot launch test gun", 2, 6, PowerDice: 2)] };
    }
    private void ConfigurePilotLaunchBattle()
    {
        _state = _state! with { ActiveSide = 1, ValorOwner = 1, RandomState = 3,
            Cards = _state.Cards! with { Phase = "orders", UsedInterrupts0 = ["attack", "move", "counter"], UsedInterrupts1 = ["attack", "move", "counter"] } };
        DrawBattle();
    }
    private void PilotLaunchNetworkHost(BattleCommand? command)
    {
        string before = _engine.Save(_state!); LoadGame(false);
        if (before != _engine.Save(_state!)) throw new InvalidDataException("Pilot/launch disk restore diverged.");
        if (command?.Kind == "move" && command.Unit == "s1-3")
        {
            if (_state!.RandomState != 3 || _state.Hazard != null || FleetTerrain.MineCount(_state, 1) != 5) throw new InvalidDataException("Remote Supreme Pilot lost terrain immunity.");
            GD.Print("PILOT_MOVE_NETWORK_RESUMED");
        }
        if (command?.Kind == "skip" && _state!.Cards!.Pending == null && _state.Cards.HeroicUsed!.Contains(SupremePilot) && !_state.Cards.HeroicUsed!.Contains(LaunchAll))
        { if (!ApplyCommand(0, new("pass"))) throw new InvalidDataException("Host could not pass between Heroic drills."); }
        if (command?.Kind == "special" && command.Card == LaunchAll)
        {
            if (!_state!.Cards!.Pending!.Units.SequenceEqual(["s1-4", "s1-5"]) || _state.Units.Single(u => u.Id == "s1-2").Activated) throw new InvalidDataException("Remote launch assignment reordered carrier.");
            GD.Print("HEROIC_LAUNCH_ASSIGNMENT_NETWORK_RESUMED");
        }
        if (command?.Kind == "launch")
        {
            if (_state!.Cards!.Pending?.Step != (command.Unit == "s1-4" ? 1 : 4)) throw new InvalidDataException("Remote launch reached wrong row.");
            GD.Print("HEROIC_LAUNCH_PLACEMENT_NETWORK_RESUMED");
        }
        if (command?.Kind == "attack") GD.Print("HEROIC_LAUNCH_ROLL_NETWORK_RESUMED");
        if (command?.Kind == "resolve" && _state!.Cards!.Pending == null)
        {
            if (_state.Orders1 != 2 || _state.Units.Single(u => u.Id == "s1-2") is not { Activated: false, Launches: 2 } || !_state.Cards.HeroicUsed!.Contains(LaunchAll)) throw new InvalidDataException("Two Heroic orders diverged.");
            GD.Print("PILOT_LAUNCH_NETWORK_OK"); GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit();
        }
    }
    private void PilotLaunchNetworkClient()
    {
        var cards = _state!.Cards!;
        if (_pilotLaunchNetworkStep == 0)
        {
            if (cards.HeroicCards?.Length != 2 || cards.Hand0.Length != 0) throw new InvalidDataException("Pilot/launch private hand diverged.");
            _pilotLaunchNetworkStep = 1; Submit(new("special", "s1-3", Card: SupremePilot));
        }
        else if (_pilotLaunchNetworkStep == 1 && cards.Pending?.Step == 0) { _pilotLaunchNetworkStep = 2; Submit(new("move", "s1-3", Q: 5, R: 2)); }
        else if (_pilotLaunchNetworkStep == 2 && cards.Pending?.Step == 1) { _pilotLaunchNetworkStep = 3; Submit(new("move", "s1-3", Q: 3, R: 2)); }
        else if (_pilotLaunchNetworkStep == 3 && cards.Pending?.Step == 2) { _pilotLaunchNetworkStep = 4; Submit(new("skip")); }
        else if (_pilotLaunchNetworkStep == 4 && cards.Pending == null && _state.ActiveSide == 1) { _pilotLaunchNetworkStep = 5; Submit(new("special", "s1-4", Card: LaunchAll, OtherUnit: "s1-5")); }
        else if (_pilotLaunchNetworkStep == 5 && cards.Pending?.Step == 0) { _pilotLaunchNetworkStep = 6; Submit(new("launch", "s1-4", Q: 7, R: 5, Facing: 3)); }
        else if (_pilotLaunchNetworkStep == 6 && cards.Pending?.Step == 1) { _pilotLaunchNetworkStep = 7; Submit(new("skip")); }
        else if (_pilotLaunchNetworkStep == 7 && cards.Pending?.Step == 2) { _pilotLaunchNetworkStep = 8; Submit(new("attack", "s1-4", "s0-1", Weapon: "gun")); }
        else if (_pilotLaunchNetworkStep == 8 && _state.Combat != null) { _pilotLaunchNetworkStep = 9; Submit(new("resolve")); }
        else if (_pilotLaunchNetworkStep == 9 && cards.Pending?.Step == 3) { _pilotLaunchNetworkStep = 10; Submit(new("launch", "s1-5", Q: 7, R: 6, Facing: 3)); }
        else if (_pilotLaunchNetworkStep == 10 && cards.Pending?.Step == 4) { _pilotLaunchNetworkStep = 11; Submit(new("skip")); }
        else if (_pilotLaunchNetworkStep == 11 && cards.Pending?.Step == 5) { _pilotLaunchNetworkStep = 12; Submit(new("attack", "s1-5", "s0-1", Weapon: "gun")); }
        else if (_pilotLaunchNetworkStep == 12 && _state.Combat != null) { _pilotLaunchNetworkStep = 13; Submit(new("resolve")); }
        else if (_pilotLaunchNetworkStep == 13 && cards.Pending == null)
        {
            if (_state.Orders1 != 2 || cards.HeroicUsed?.Length != 2 || cards.Discard.Contains(SupremePilot) || cards.Discard.Contains(LaunchAll)) throw new InvalidDataException("Pilot/launch result or one-use spending diverged.");
            _pilotLaunchNetworkStep = 14; GD.Print("PILOT_LAUNCH_NETWORK_OK"); GetTree().Quit();
        }
    }
    private async Task CheckPilotLaunch()
    {
        try
        {
            void Require(bool ok, string why) { if (!ok) throw new Exception(why); GD.Print("PILOT_LAUNCH_UI_PASS " + why); }
            async Task Frames() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            IEnumerable<Node> Nodes(Node root) => root.GetChildren().Where(n => !n.IsQueuedForDeletion()).SelectMany(n => new[] { n }.Concat(Nodes(n)));
            void Press(string text) { var button = Nodes(_ui).OfType<Button>().Single(b => b.Text == text); Require(!button.Disabled, "Enabled: " + text); button.EmitSignal(BaseButton.SignalName.Pressed); }
            void Click(Hex cell) { var point = _camera.UnprojectPosition(World(cell)); _UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = point }); }
            void Restore() { string before = _engine.Save(_state!); SaveGame(); LoadGame(false); Require(before == _engine.Save(_state!), "Pilot/launch continuation restores from disk"); }
            SinglePlayerMenu(); await Frames(); Press("PILOT AND LAUNCH HEROES"); _bot = false; Require(_state!.Units.Any(u => u.Side == 0 && u.Leader == "core:hero-taiidan-veteran-commander") && _state.Cards!.Hand0.Contains("core:hero-taiidan-launch-commander#hero-0"), "Single player receives both Heroes"); StartBattle(HeroicBattle.PilotLaunchMapId, false, persist: false); ConfigurePilotLaunchContent(); ConfigurePilotLaunchBattle(); _selectedCard = SupremePilot; SelectUnit("s1-3"); await Frames();
            Press("SPECIAL ORDER"); await Frames(); Click(new(5, 2)); await Frames(); Require(_state!.Cards!.Pending?.Step == 1 && _state.RandomState == 3 && _state.Units.Single(u => u.Id == "s1-3").Q == 5, "Field click passes an enemy, asteroids and mines without dice"); Restore(); await Frames();
            _locale.SetLanguage("ru"); LocalizeInterface(); await Frames(); Require(_locale.Text("Supreme Pilot") == "Высший пилотаж" && _locale.Text("Lightning Move") == "Молниеносное перемещение" && _locale.Text("fighter, corvette, platform") == "истребитель, корвет, платформа", "Supreme Pilot and its movement/aura labels are localized");
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "supreme-pilot.png"));
            _locale.SetLanguage("en"); LocalizeInterface(); await Frames(); Click(new(3, 2)); await Frames(); Require(_state.Cards!.Pending?.Step == 2 && _state.RandomState == 3, "Lightning Move keeps the scoped protection"); Press("FINISH ORDER"); await Frames(); Press("END TURN"); await Frames();
            _selectedCard = LaunchAll; _formationLines = "both"; SelectUnit("s1-4"); await Frames();
            var second = Nodes(_ui).OfType<OptionButton>().Single(b => b.ItemCount > 0 && b.GetItemText(0).StartsWith("Second: "));
            Require(second.ItemCount == 1 && _secondOrderUnit == "s1-5", "Bay picker selects only the second fighter from Hero's carrier"); Press("SPECIAL ORDER"); await Frames(); Restore(); await Frames();
            Click(new(7, 5)); await Frames(); Require(_state.Cards!.Pending?.Step == 1 && _state.Units.Single(u => u.Id == "s1-2").Launches == 1, "First launch executes from the field"); Press("SKIP ACTION"); await Frames(); Click(new(5, 5)); await Frames(); Require(_state.Combat != null, "Launched fighter gets its printed Attack"); Restore(); await Frames(); Press("ACCEPT ROLL"); await Frames();
            Require(_state.Cards!.Pending?.Step == 3 && _selected == "s1-5", "Second embarked fighter is selected for launch"); Restore(); await Frames();
            _locale.SetLanguage("ru"); LocalizeInterface(); await Frames(); Require(_locale.Text("Launch All Fighters") == "Поднять все истребители", "Launch All Fighters is localized");
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "heroic-second-launch.png"));
            _locale.SetLanguage("en"); LocalizeInterface(); await Frames(); Click(new(7, 6)); await Frames(); Press("SKIP ACTION"); await Frames(); Click(new(5, 5)); await Frames(); Press("ACCEPT ROLL"); await Frames();
            Require(_state.Cards!.Pending == null && _state.Orders1 == 2 && _state.Units.Single(u => u.Id == "s1-2") is { Activated: false, Launches: 2 }, "Both fighters finish for one Play while carrier stays ready");
            StartBattle(HeroicBattle.PilotLaunchMapId, false, persist: false); ConfigurePilotLaunchBattle(); _selectedCard = LaunchAll; _formationLines = "second"; SelectUnit("s1-5"); await Frames(); Press("SPECIAL ORDER"); await Frames(); Require(_state.Cards!.Pending?.Step == 3 && !_state.Reserves!.Single(u => u.Id == "s1-4").Activated, "Second launch line alone works in UI");
            NetworkMenu(); await Frames(); Require(Nodes(_ui).OfType<CheckBox>().Any(b => b.Text == "Pilot and launch Heroes"), "Pilot and launch practice can be hosted"); GD.Print("PILOT_LAUNCH_UI_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
