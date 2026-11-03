using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private async Task CheckForces()
    {
        try
        {
            void Require(bool ok, string text) { if (!ok) throw new Exception(text); GD.Print("FORCE_UI_PASS " + text); }
            async Task Frames() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            IEnumerable<Node> Nodes(Node root) => root.GetChildren().Where(n => !n.IsQueuedForDeletion()).SelectMany(n => new[] { n }.Concat(Nodes(n)));
            void Press(string text) { var b = Nodes(_ui).OfType<Button>().Single(b => b.Text == text); Require(!b.Disabled, "Enabled: " + text); b.EmitSignal(BaseButton.SignalName.Pressed); }
            SinglePlayerMenu(); await Frames(); Press("ПОДГОТОВКА ФЛОТОВ"); await Frames();
            Require(_forceLaunch is { Disabled: false } && ForceBuilder.Validate(_content, CurrentForce).Valid, "Both default forces are ready");
            Require(Nodes(_forceReport!).OfType<Label>().Any(l => l.Text.Contains("бесплатно")) && Nodes(_forceReport!).OfType<Label>().Any(l => l.Text.Contains("обязательно")), "Grey assigned and compulsory tabs are explained");
            var budget = Nodes(_ui).OfType<SpinBox>().First(); budget.Value = 1; await Frames(); Require(_forceLaunch!.Disabled, "Over-budget force blocks battle launch"); budget.Value = 12;
            Press("ДОБАВИТЬ КОРАБЛЬ"); await Frames(); Require(CurrentForce.Units.Length == 3 && _forceLaunch!.Disabled, "Buying a ship recalculates costs and tabs");
            Press("УБРАТЬ ВЫБРАННЫЙ КОРАБЛЬ"); await Frames(); Require(CurrentForce.Units.Length == 2 && !_forceLaunch!.Disabled, "Removing the ship restores validity");
            Press("СОХРАНИТЬ СОСТАВ"); await Frames(); Require(ForceLibrary.Read(ForceDirectory).Any(p => p.Id == CurrentForce.Id), "Personal fleet saved");
            _forcePlans[0] = CurrentForce with { Budget = 1 }; ForceMenu(); await Frames(); Press("ЗАГРУЗИТЬ СОСТАВ"); await Frames(); Require(CurrentForce.Budget == 12 && !_forceLaunch!.Disabled, "Library reload revalidates current definitions");
            var side = Nodes(_ui).OfType<OptionButton>().First(); side.Select(1); side.EmitSignal(OptionButton.SignalName.ItemSelected, 1L); await Frames(); Require(CurrentForce.Faction == "taiidan", "Opposing fleet is edited independently");
            side = Nodes(_ui).OfType<OptionButton>().First(); side.Select(0); side.EmitSignal(OptionButton.SignalName.ItemSelected, 0L); await Frames();
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "forces-editor.png"));
            _forceMode = 1; Press("НАЧАТЬ БОЙ С ЭТИМИ ФЛОТАМИ"); await Frames();
            Require(_state!.Map == ForceBattle.MapId && _preparation != null && _state.Reserves!.Count == 2 && _state.Funds0 == 3, "Prepared placements, free craft and reserve resources reach the battle");
            string before = _engine.Save(_state); SaveGame(); _content.Maps.Remove(ForceBattle.MapId); _preparation = null; LoadGame(false); await Frames();
            Require(_preparation != null && _engine.Save(_state!) == before && !_bot, "Preparation restores before loading battle state");
            StartBattle("core:training", false, persist: false); Require(_preparation == null, "Another battle clears preparation metadata");
            NetworkMenu(); await Frames(); Press("ПОДГОТОВИТЬ ФЛОТЫ ДЛЯ СЕТИ"); await Frames(); Require(_forceNetwork && _forceLaunch!.Text.Contains("СОЗДАТЬ БОЙ"), "PvP and co-op hosting use the same editor");
            GD.Print("FORCE_UI_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
