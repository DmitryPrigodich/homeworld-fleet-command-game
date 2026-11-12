using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private void ConfigureEconomyBattle()
    {
        string[] a = ["order-01", "order-09"], b = ["order-21", "order-22"];
        _state = _state! with { ActiveSide = 1, RandomState = 1,
            Cards = new(Enumerable.Range(1, 24).Select(i => $"order-{i:00}").Except(a).Except(b).ToArray(), a, b, [], Phase: "orders", PlayLimit1: 2),
            Units = _state.Units.Select(u => u.Id == "s1-3" ? u with { Q = 7, R = 1 } : u.Id == "s1-4" ? u with { Q = 5, R = 1 } : u).ToList(),
            Deposits = new() { [0] = new(0), [1] = new(2), [2] = new(3) } };
    }
    private async Task CheckEconomy()
    {
        try
        {
            void Require(bool value, string text) { if (!value) throw new Exception(text); GD.Print("ECONOMY_UI_PASS " + text); }
            async Task Frames() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            IEnumerable<Node> Nodes(Node root) => root.GetChildren().SelectMany(n => new[] { n }.Concat(Nodes(n))).Where(n => !n.IsQueuedForDeletion());
            void Press(string text) { var button = Nodes(_ui).OfType<Button>().Single(b => b.Text == text); Require(!button.Disabled, "Enabled: " + text); button.EmitSignal(BaseButton.SignalName.Pressed); }
            SinglePlayerMenu(); await Frames(); Press("SURVEY AND HARVEST"); _bot = false; await Frames(); Require(_state!.Map == BasicCardBattle.EconomyMapId, "Economy map is reachable from the menu");
            ConfigureEconomyBattle(); SelectUnit("s1-3"); await Frames();
            Require(DepositDescription(2).Contains("unknown"), "Unscouted deposits stay hidden even on the authority UI");
            Press("RESOURCE HARVESTING"); await Frames();
            Submit(new("move", "s1-3", Q: 6, R: 1)); await Frames();
            Require(_state!.Cards!.Pending is { BasicAction: "harvest", Step: 1 } && DepositDescription(2).Contains("3 ★") && DepositDescription(1).Contains("unknown"), "Free movement surveys only adjacent fields for the moving fleet");
            Require(_ui.GetChildren().OfType<PanelContainer>().Where(p => p.Position.X > 1000).All(p => p.GetGlobalRect().End.X <= GetViewport().GetVisibleRect().End.X), "Economy controls stay within the viewport width");
            string snapshot = _engine.Save(_state); SaveGame(); LoadGame(false); await Frames(); Require(_engine.Save(_state!) == snapshot, "Private knowledge and pending harvest restore from disk");
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "economy-survey.png"));
            Press("HARVEST: 7, 3 · 3 ★"); await Frames();
            Require(_state!.Units.Single(u => u.Id == "s1-3").Cargo == 3 && _state.Harvested1 == 3 && _state.Stars1 == 0 && _state.Deposits![2].Harvested && DepositDescription(2).Contains("Harvested"), "Harvested stars become public cargo rather than destruction score");
            ApplyCommand(0, new("pass")); SelectUnit("s1-4"); await Frames();
            var receive = Nodes(_ui).OfType<CheckBox>().Single(b => b.Text == "Take from neighbor"); receive.ButtonPressed = true; await Frames();
            var amount = Nodes(_ui).OfType<SpinBox>().Single(b => b.Prefix == "Resources: "); amount.Value = 2;
            Press("TRANSFER RESOURCES"); await Frames();
            Require(_state!.Units.Single(u => u.Id == "s1-3").Cargo == 1 && _state.Units.Single(u => u.Id == "s1-4").Cargo == 2 && _state.Round == 2 && _state.Cards!.Phase == "cleanup", "Ready recipient can retrieve cargo from an activated neighbor and finish the round");
            snapshot = _engine.Save(_state); SaveGame(); LoadGame(false); await Frames(); Require(_engine.Save(_state!) == snapshot, "Cargo and depleted deposits survive round-end save/load");
            GD.Print("ECONOMY_UI_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
