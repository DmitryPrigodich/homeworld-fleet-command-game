using Godot;
using FleetCommand.Core;

namespace FleetCommand.Game;
public partial class Main
{
    private void ConfigureDefenseContent()
    {
        // Test-only stats; the playable ship/card catalogue is not changed by this fixture.
        _content.Ships["core:kushan-interceptor"] = _content.Ships["core:kushan-interceptor"] with { Class = "frigate", Faction = "taiidan", Armor = 0, Success = 2, Abilities = [], Weapons = [new("incoming", "Missiles", 3, 4, Kind: "missile")] };
        _content.Ships["core:taiidan-interceptor"] = _content.Ships["core:taiidan-interceptor"] with { Class = "capital", Armor = 2, Success = 2, Abilities = [], Weapons = [new("flak", "Flak", 1, 0, DefensiveFire: true)] };
    }
    private void ConfigureDefenseBattle()
    {
        string[] a = ["order-01", "order-09"], b = ["order-21", "order-22"];
        _state = _state! with { ActiveSide = 0, ValorOwner = 1, RandomState = 1,
            Cards = new(Enumerable.Range(1, 24).Select(i => $"order-{i:00}").Except(a).Except(b).ToArray(), a, b, [], Phase: "orders"),
            Units = _state.Units.Select(u => u.Id == "s0-0" ? u with { Q = 1, R = 1, Models = 2 } : u.Id == "s1-3" ? u with { Q = 3, R = 1, Models = 3, Activated = true } : u).ToList() };
    }
    private async Task CheckDefense()
    {
        try
        {
            void Require(bool value, string text) { if (!value) throw new Exception(text); GD.Print("DEFENSE_UI_PASS " + text); }
            async Task Frames() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            IEnumerable<Node> Nodes(Node root) => root.GetChildren().SelectMany(child => new[] { child }.Concat(Nodes(child))).Where(n => !n.IsQueuedForDeletion());
            void Press(string text) { var b = Nodes(_ui).OfType<Button>().Single(b => b.Text == text); Require(!b.Disabled, "Enabled: " + text); b.EmitSignal(BaseButton.SignalName.Pressed); }
            ConfigureDefenseContent(); StartBattle(BasicCardBattle.TabletopMapId, false, persist: false); ConfigureDefenseBattle();
            SelectUnit("s0-0"); await Frames(); Press("ATTACK"); await Frames(); Submit(new("attack", "s0-0", "s1-3")); await Frames();
            if (_state!.Defense == null) { Press("SKIP WINDOW"); await Frames(); }
            Require(_state.Defense != null && _state.Combat == null && LocalCommandSide == 1, "UI gives defense to the defender before any attack rolls");
            string snapshot = _engine.Save(_state); SaveGame(); LoadGame(false); await Frames(); Require(_engine.Save(_state!) == snapshot, "Unrolled defense window survives disk save/load");
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "defense-choice.png"));
            Press("DEFENSE: Flak · 3D"); await Frames();
            Require(_state!.Combat is { Kind: "defense", Side: 1 } && _state.Orders1 == 0 && _state.Cards!.Hand1.Length == 2, "Defense rolls without a card or Play");
            snapshot = _engine.Save(_state); SaveGame(); LoadGame(false); await Frames(); Require(_engine.Save(_state!) == snapshot, "Rolled defense restores without rolling again");
            Require(!Nodes(_ui).OfType<Button>().Any(b => b.Text == "REROLL: TAIIDAN"), "Taiidan ability is unavailable for defensive fire");
            Press("ACCEPT ROLL"); await Frames();
            Require(_state!.Defense == null && _state.Combat is { Kind: "ranged", Rolls.Length: 3 } && LocalCommandSide == 0, "Defense reduces the incoming missiles and returns the decision to the attacker");
            Press("ACCEPT ROLL"); await Frames();
            Require(_state!.Cards!.Response == null && _state.Combat == null && _state.Orders0 == 1, "Original order finishes exactly once");
            GD.Print("DEFENSE_UI_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
