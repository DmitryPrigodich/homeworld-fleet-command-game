using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private int _featureNetworkStep;
    private void ConfigureFeatureBattle(bool large)
    {
        string[] a = ["order-01", "order-09"], b = ["order-21", "order-22"];
        _state = _state! with { ActiveSide = 1, Units = _state.Units.Select(u => !large && u.Id == "s1-3" ? u with { Q = 5, R = 2 } : u).ToList(),
            Cards = _state.Cards! with { Phase = "orders", Hand0 = a, Hand1 = b, Deck = Enumerable.Range(1, 24).Select(i => $"order-{i:00}").Except(a).Except(b).ToArray(), Discard = [] } };
    }
    private void FeatureNetworkHost(BattleCommand? command)
    {
        if (command?.Kind != "move") return;
        string snapshot = _engine.Save(_state!); LoadGame(false);
        if (_engine.Save(_state!) != snapshot) throw new InvalidDataException("Feature network save diverged.");
        if (_args.Contains("--smoke-large"))
        {
            if (!FleetFootprint.Cells(_content, _state!.Units.Single(u => u.Id == "s1-2")).SequenceEqual([new Hex(6, 2), new(7, 2)])) throw new InvalidDataException("Wide ship pose was not restored.");
            GD.Print("LARGE_NETWORK_RESUMED"); GD.Print("FEATURE_NETWORK_OK"); Broadcast(); GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit(); return;
        }
        if (ScenarioEvents.HasToken(_state!, "s1-3", "data"))
        {
            GD.Print("SCENARIO_DATA_NETWORK_RESUMED");
            if (_state!.ActiveSide == 0) ApplyCommand(0, new("pass"));
            // Shorten the return trip in this transport fixture; ordinary movement is covered by core scenarios.
            _state = _state! with { Units = _state.Units.Select(u => u.Id == "s1-3" ? u with { Q = 14, R = 4, Activated = false } : u).ToList() }; SaveGame(); Broadcast();
        }
        else if (_state!.Medals1 == 2 && _state.Scenario!.Fired.Contains("delivered"))
        { GD.Print("SCENARIO_DELIVERY_NETWORK_RESUMED"); GD.Print("FEATURE_NETWORK_OK"); Broadcast(); GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit(); }
    }
    private void FeatureNetworkClient()
    {
        if (_args.Contains("--smoke-large"))
        {
            if (_featureNetworkStep == 1 && _state!.Units.Single(u => u.Id == "s1-2").Q == 6) { GD.Print("FEATURE_NETWORK_OK"); GetTree().Quit(); return; }
            if (_featureNetworkStep == 0) { _featureNetworkStep = 1; Submit(new("move", "s1-2", Q: 6, R: 2, Facing: 3, AutoFacing: false, Card: "order-21")); } return;
        }
        if (_locale.Language != "en") throw new InvalidDataException("Client locale was not retained.");
        LocalizeInterface();
        if (_featureNetworkStep == 2 && _state!.Medals1 == 2 && _state.Scenario!.Fired.Contains("delivered")) { GD.Print("SCENARIO_MIXED_LANGUAGE_OK"); GD.Print("FEATURE_NETWORK_OK"); GetTree().Quit(); return; }
        if (_featureNetworkStep == 0) { _featureNetworkStep = 1; Submit(new("move", "s1-3", Q: 4, R: 2, Card: "order-21")); }
        else if (_featureNetworkStep == 1 && ScenarioEvents.HasToken(_state!, "s1-3", "data") && _state!.Units.Single(u => u.Id == "s1-3").Q == 14)
        { _featureNetworkStep = 2; Submit(new("move", "s1-3", Q: 15, R: 4, Card: "order-22")); }
    }
    private async Task CheckScenarioFeatures()
    {
        try
        {
            void Require(bool ok, string message) { if (!ok) throw new Exception(message); GD.Print("SCENARIO_UI_PASS " + message); }
            async Task Frames() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            IEnumerable<Node> Nodes(Node root) => root.GetChildren().Where(n => !n.IsQueuedForDeletion()).SelectMany(n => new[] { n }.Concat(Nodes(n)));
            void Press(string text) { var b = Nodes(_ui).OfType<Godot.Button>().Single(b => b.Text == text); Require(!b.Disabled, "Enabled: " + text); b.EmitSignal(BaseButton.SignalName.Pressed); }
            SinglePlayerMenu(); await Frames(); Press("TWO-HEX SHIPS"); _bot = false; await Frames();
            ConfigureFeatureBattle(true); _state = _state! with { ActiveSide = 0 }; _facingChoice = 1; SelectUnit("s0-0"); await Frames();
            Require(_world.GetNodeOrNull("SelectedFootprint") != null && Nodes(_ui).OfType<Label>().Any(l => l.Text.Contains("hull occupies 2 hexes")), "Two-hex hull and controls are visible");
            Submit(new("move", "s0-0", Q: 2, R: 2, Facing: 1, AutoFacing: false, Card: "order-01")); await Frames();
            if (_state!.Cards?.Response != null) Submit(new("decline"));
            Require(_state.Units.Single(u => u.Id == "s0-0").Facing == 1, "Turn-in-place command pivots stern");
            string before = _engine.Save(_state); SaveGame(); LoadGame(false); Require(before == _engine.Save(_state!), "Wide ship restores exactly");
            MainMenu(); SinglePlayerMenu(); await Frames(); Press("RULEBOOK SCENARIOS"); await Frames(); Press("PLAY LOCAL HOTSEAT"); await Frames();
            Require(_world.GetNodeOrNull("EscapeZone") != null && _state!.Map == "core:first-contact", "Rulebook map and escape zone are reachable");
            ConfigureFeatureBattle(false); _facingChoice = -1; SelectUnit("s1-3"); Submit(new("move", "s1-3", Q: 4, R: 2, Card: "order-21")); await Frames();
            Require(ScenarioEvents.HasToken(_state!, "s1-3", "data") && Nodes(_ui).OfType<Label>().Any(l => l.Text == "ON BOARD: INTELLIGENCE"), "Collected intelligence is visible");
            before = _engine.Save(_state!); SaveGame(); LoadGame(false); await Frames(); Require(before == _engine.Save(_state!), "Scenario token restores");
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "first-contact.png"));
            NetworkMenu(); await Frames(); Require(Nodes(_ui).OfType<CheckBox>().Any(b => b.Text == "Scenario 1: First Contact"), "Scenario can be hosted in PvP or shared co-op");
            GD.Print("SCENARIO_UI_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
