using Godot;
using System.Text.Json;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private void ConfigureObjectiveContent()
    {
        _content.Maps[BasicCardBattle.ObjectivesMapId] = _content.Maps[BasicCardBattle.ObjectivesMapId] with { Objectives = [new("station", "Capture the station", 1, "capture", 3, Target: "s1-6")] };
        _content.Ships["practice:taiidan-boarder"] = _content.Ships["practice:taiidan-boarder"] with { Success = 2, Crew = 6, Armor = 20, Weapons = [] };
        _content.Ships["practice:station"] = _content.Ships["practice:station"] with { Armor = 0, Crew = 0 };
    }
    private void ConfigureObjectiveBattle()
    {
        string[] a = ["order-01", "order-09"], b = ["order-21", "order-22"];
        _state = _state! with { ActiveSide = 1, ValorOwner = 1, RandomState = 1, Units = _state.Units.Select(u => u.Id == "s1-3" ? u with { Q = 4, R = 3 } : u.Id == "s1-6" ? u with { Q = 3, R = 3, Side = 0 } : u).ToList(),
            Cards = new(Enumerable.Range(1, 24).Select(i => $"order-{i:00}").Except(a).Except(b).ToArray(), a, b, [], Phase: "orders") };
    }
    private async Task CheckObjectives()
    {
        try
        {
            void Require(bool ok, string text) { if (!ok) throw new Exception(text); GD.Print("OBJECTIVE_UI_PASS " + text); }
            async Task Frames() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            IEnumerable<Node> Nodes(Node root) => root.GetChildren().SelectMany(n => new[] { n }.Concat(Nodes(n))).Where(n => !n.IsQueuedForDeletion());
            void Press(string text) { var b = Nodes(_ui).OfType<Button>().Single(b => b.Text == text); Require(!b.Disabled, "Enabled: " + text); b.EmitSignal(BaseButton.SignalName.Pressed); }
            SinglePlayerMenu(); await Frames(); Press("OBJECTIVES AND MEDALS"); _bot = false; await Frames();
            Require(_state!.Map == BasicCardBattle.ObjectivesMapId && Nodes(_ui).OfType<Label>().Any(l => l.Text.Contains("Capture the station")), "Published-rule objective types are reachable through the training menu");
            ConfigureObjectiveContent(); StartBattle(BasicCardBattle.ObjectivesMapId, false, persist: false); ConfigureObjectiveBattle(); DrawBattle(); await Frames();
            Require(Nodes(_ui).OfType<Label>().Any(l => l.Text == "MEDALS: 0 : 0 / 3"), "Initial public medal score is visible");
            _attackKindChoice = "board"; Submit(new("attack", "s1-3", "s1-6", Card: "order-21", AttackKind: "board")); await Frames();
            if (_state!.Combat?.Kind != "board") throw new Exception("Expected boarding capture choice.");
            string snapshot = _engine.Save(_state); SaveGame(); LoadGame(false); await Frames(); Require(_engine.Save(_state!) == snapshot, "Capture decision and objective state restore before the winning result");
            Press("CAPTURE TARGET"); await Frames();
            Require(_state!.Finished && _state.Winner == 1 && _state.Medals1 == 3 && _state.Cards!.Response == null && Nodes(_ui).OfType<Label>().Any(l => l.Text.Contains("Medal threshold reached")), "Third medal ends the battle and explains the result");
            Require(_state.Log!.Any(e => e.Text.Contains("Medals: 0:0 → 0:3")), "Medal changes are recorded in the event log");
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "objectives-result.png"));
            snapshot = _engine.Save(_state); SaveGame(); LoadGame(false); await Frames(); Require(_engine.Save(_state!) == snapshot, "Final medal result survives save/load");
            _campaign = FleetCommand.Core.Campaign.Start(_content, "practice:objectives", ["host", "friend"]); DrawBattle(); await Frames(); Press("CONTINUE CAMPAIGN"); await Frames();
            Require(_campaign!.Completed && _campaign.OpponentMedals == 3 && _campaign.Results is { Length: 1 } && Nodes(_ui).OfType<Label>().Any(l => l.Text.Contains("Opponent's medals: 3")), "Campaign records actual mission medals and shows both fleets' totals");
            var saved = JsonData.Read<SavedGame>(SavePath(true)); Require(saved.Campaign!.Results![0].Medals1 == 3 && saved.Campaign.Participants!.Length == 2, "Campaign result history and participants are saved");
            LoadGame(true); await Frames(); Require(_campaign!.OpponentMedals == 3 && _campaign.Results!.Length == 1, "Campaign end screen reloads without awarding the result again");
            NetworkMenu(); await Frames(); Require(Nodes(_ui).OfType<OptionButton>().Any(o => Enumerable.Range(0, o.ItemCount).Any(i => o.GetItemText(i).Contains("Medals — training campaign"))), "Co-op hosting can select the objective campaign");
            GD.Print("OBJECTIVE_UI_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
