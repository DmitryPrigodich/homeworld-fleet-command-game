using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private void ConfigureLogisticsBattle()
    {
        string[] a = ["order-01", "order-09"], b = ["order-21", "order-22", "order-23"];
        _state = _state! with { ActiveSide = 1, RandomState = 1, Cards = new(Enumerable.Range(1, 24).Select(i => $"order-{i:00}").Except(a).Except(b).ToArray(), a, b, [], Phase: "orders", HandLimit1: 3, PlayLimit1: 3) };
    }
    private async Task CheckLogistics()
    {
        try
        {
            void Require(bool ok, string text) { if (!ok) throw new Exception(text); GD.Print("LOGISTICS_UI_PASS " + text); }
            async Task Frames() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            IEnumerable<Node> Nodes(Node root) => root.GetChildren().SelectMany(n => new[] { n }.Concat(Nodes(n))).Where(n => !n.IsQueuedForDeletion());
            void Press(string text) { var b = Nodes(_ui).OfType<Button>().Single(b => b.Text == text); Require(!b.Disabled, "Enabled: " + text); b.EmitSignal(BaseButton.SignalName.Pressed); }
            void Position(string label)
            {
                var dropdown = Nodes(_ui).OfType<OptionButton>().Single(o => o.ItemCount > 0 && o.GetItemText(0).StartsWith("Гекс "));
                int index = Enumerable.Range(0, dropdown.ItemCount).Single(i => dropdown.GetItemText(i) == label); dropdown.Select(index); dropdown.EmitSignal(OptionButton.SignalName.ItemSelected, index);
            }
            SinglePlayerMenu(); await Frames(); Press("АНГАРЫ И ПОДКРЕПЛЕНИЯ"); _bot = false; await Frames();
            Require(_state!.Map == BasicCardBattle.LogisticsMapId && _state.Units.Count == 4 && _state.Reserves!.Count == 6, "Logistics map keeps reserves off the battlefield");
            ConfigureLogisticsBattle(); SelectUnit("s1-5"); await Frames(); Press("ВЫПУСТИТЬ ИЗ АНГАРА"); await Frames();
            string snapshot = _engine.Save(_state!); SaveGame(); LoadGame(false); await Frames(); Require(_engine.Save(_state!) == snapshot, "Paid launch order restores before placement");
            Require(_world.GetChildren().OfType<MeshInstance3D>().Any(n => n.Name == "ReinforceHighlights"), "Legal placement cells are highlighted on the field");
            Position("Гекс 9, 3"); Press("РАЗМЕСТИТЬ НА ПОЛЕ"); await Frames();
            Require(_state!.Units.Any(u => u.Id == "s1-6" && u.Activated) && _state.Units.Single(u => u.Id == "s1-5") is { Activated: false, Launches: 1 }, "Launch activates only the arriving squadron");
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "logistics-launch.png"));
            Press("ЗАВЕРШИТЬ ПРИКАЗ"); ApplyCommand(0, new("pass")); SelectUnit("s1-5"); await Frames(); Press("ПРОИЗВОДСТВО"); await Frames();
            Nodes(_ui).OfType<SpinBox>().Single(s => s.Prefix == "Из резерва: ").Value = 1;
            Nodes(_ui).OfType<CheckBox>().Single(b => b.Text == "Построить в ангар").ButtonPressed = true; await Frames(); Press("ПОСТРОИТЬ В АНГАР"); await Frames();
            Require(_state!.Reserves!.Any(u => u.Id == "s1-10" && u.Activated && u.Embarked == "s1-5") && _state.Funds1 == 2 && _state.Units.Single(u => u.Id == "s1-5").Cargo == 5, "Factory pays exact cargo/reserve cost and stores the built unit in a bay");
            SelectUnit("s1-8"); await Frames(); Press("ВВЕСТИ ИЗ РЕЗЕРВА"); await Frames(); Position("Гекс 10, 1"); Press("РАЗМЕСТИТЬ НА ПОЛЕ"); await Frames();
            Require(_state!.Units.Any(u => u.Id == "s1-8" && u.Q == 9 && u.R == 0) && _state.Round == 2 && _state.Units.Single(u => u.Id == "s1-5").Launches == 0, "Reserve placement finishes the round and resets the launch limit");
            snapshot = _engine.Save(_state); SaveGame(); LoadGame(false); await Frames(); Require(_engine.Save(_state!) == snapshot, "Off-board units, costs and identities survive save/load");
            StartBattle(BasicCardBattle.LogisticsMapId, false, persist: false); ConfigureLogisticsBattle(); SelectUnit("s1-9"); await Frames();
            Press("В АНГАР: Учебный носитель №1"); await Frames(); Require(!_state!.Units.Any(u => u.Id == "s1-9") && _state.Reserves!.Any(u => u.Id == "s1-9" && u.Embarked == "s1-5"), "Embark button moves a strike unit into the chosen bay");
            GD.Print("LOGISTICS_UI_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
