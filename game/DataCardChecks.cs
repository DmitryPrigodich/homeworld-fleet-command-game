using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private int _dataCardNetworkStep = -1;
    private void ConfigureDataCardBattle(int side)
    {
        string[] a = ["cardtest:basic#1", "cardtest:basic#2"], b = ["cardtest:maneuver#1", "cardtest:free-course#1"];
        if (side == 0) (a, b) = (b, a);
        _state = _state! with { ActiveSide = side, Cards = new(CommandCards.Instances(CommandCards.Deck(_content, _state)).Keys.Except(a).Except(b).ToArray(), a, b, [], Phase: "orders") };
        DrawBattle();
    }
    private void DataCardsNetworkHost(BattleCommand? command)
    {
        string before = _engine.Save(_state!); LoadGame(false);
        if (_engine.Save(_state!) != before || CommandCards.Deck(_content, _state).Id != "cardtest:deck") throw new InvalidDataException("Mod card save diverged.");
        if (_state!.Cards?.Pending is { Step: 1 } pending)
        {
            if (SpecialOrders.Actor(_content, _state, pending) != "s1-4" || SpecialOrders.Action(_content, _state, pending) != "attack") throw new InvalidDataException("Mod order line changed after restore.");
            GD.Print("DATA_CARD_NETWORK_RESUMED");
        }
        if (command?.Kind == "skip" && _state.Cards!.Pending == null)
        { GD.Print("DATA_CARD_NETWORK_OK"); GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit(); }
    }
    private void DataCardsNetworkClient()
    {
        var cards = _state!.Cards!;
        if (CommandCards.Deck(_content, _state).Id != "cardtest:deck") throw new InvalidDataException("Wrong network command deck.");
        if (_smokeRequestSent && cards.Pending == null && cards.Discard.Contains("cardtest:maneuver#1"))
        { GD.Print("DATA_CARD_NETWORK_OK"); GetTree().Quit(); return; }
        if (cards.Pending is { } pending && pending.Step > _dataCardNetworkStep)
        { _dataCardNetworkStep = pending.Step; Submit(new("skip")); }
        else if (!_smokeRequestSent)
        { _smokeRequestSent = true; Submit(new("special", "s1-3", Card: "cardtest:maneuver#1", OtherUnit: "s1-4")); }
    }
    private async Task CheckDataCards()
    {
        try
        {
            void Require(bool ok, string why) { if (!ok) throw new Exception(why); GD.Print("DATA_CARD_UI_PASS " + why); }
            async Task Frames() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            IEnumerable<Node> Nodes(Node root) => root.GetChildren().Where(n => !n.IsQueuedForDeletion()).SelectMany(n => new[] { n }.Concat(Nodes(n)));
            void Press(string text) { var b = Nodes(_ui).OfType<Godot.Button>().Single(b => b.Text == text); Require(!b.Disabled, "Enabled: " + text); b.EmitSignal(BaseButton.SignalName.Pressed); }
            StartBattle("cardtest:sector", false, persist: false); ConfigureDataCardBattle(0);
            _settings = _settings with { CardStyle = "amber" }; _formationLines = "both"; _selectedCard = "cardtest:maneuver#1"; SelectUnit("s0-0"); await Frames();
            Require(Nodes(_ui).OfType<Label>().Any(l => l.Text.Contains("First unit: Move. Second unit: Attack")), "Preview reads the mod description");
            Require(Nodes(_ui).OfType<OptionButton>().Any(o => o.ItemCount == 3 && o.GetItemText(0) == "Both order lines"), "Mod order exposes line assignment");
            Press("SPECIAL ORDER"); await Frames(); Press("SKIP ACTION"); await Frames();
            Require(_state!.Cards!.Pending!.Step == 1 && _selected == "s0-1" && Nodes(_ui).OfType<Label>().Any(l => l.Text.Contains("Action 2/3: Attack")), "Unequal action lines select the correct actor");
            string before = _engine.Save(_state); SaveGame(); LoadGame(false); await Frames(); Require(before == _engine.Save(_state!), "Mod order restores from disk");
            Press("SKIP ACTION"); await Frames();
            var h = _engine.GetActions(_state!, 0, "s0-1").MoveCells.First(); Submit(new("move", "s0-1", Q: h.Q, R: h.R)); await Frames();
            for (int i = 0; i < 20 && (_state!.CapEntry != null || _state.Cards!.Response != null); i++)
            { Press(_state.CapEntry != null ? "STAY ON THE FIELD" : "SKIP WINDOW"); await Frames(); }
            Require(_state.Cards.Pending == null && _state.Orders0 == 1, "Data-defined final movement completes the order");
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "data-cards.png"));
            GD.Print("DATA_CARD_UI_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
