using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private int _publishedOrderStep;
    private void ConfigurePublishedOrderContent()
    {
        foreach (string fleet in _content.Maps[BasicCardBattle.OrdersMapId].Fleets)
        foreach (var entry in _content.Fleets[fleet].Units.Take(2))
        {
            var ship = _content.Ships[entry.Ship];
            _content.Ships[ship.Id] = ship with { Armor = 10, Success = 2, Evasive = 0, Abilities = [],
                Weapons = [new("gun", "Test gun", 2, 5, PowerDice: 2)] };
        }
    }
    private void ConfigurePublishedOrderBattle(int side)
    {
        string[] a = ["core:all-ahead-full#2"], b = ["core:brace-for-impact#1"];
        if (side == 0) (a, b) = (b, a);
        _state = _state! with { ActiveSide = side, Cards = _state.Cards! with { Hand0 = a, Hand1 = b, Discard = [], Phase = "orders",
            Deck = CommandCards.Instances(CommandCards.Deck(_content, _state)).Keys.Except(a).Except(b).ToArray() },
            Units = _state.Units.Select(u => u.Id == "s0-0" ? u with { Q = 2, R = 2 } : u.Id == "s1-3" ? u with { Q = 3, R = 2 } : u).ToList() };
        DrawBattle();
    }
    private void PublishedOrderNetworkHost(BattleCommand? command)
    {
        string before = _engine.Save(_state!); LoadGame(false);
        if (_engine.Save(_state!) != before) throw new InvalidDataException("Published order save diverged.");
        if (command?.Kind == "special")
        {
            if (!FleetRuleTools.Has(_state!.Units.Single(u => u.Id == "s1-3"), "brace")) throw new InvalidDataException("Remote buff was not restored.");
            GD.Print("ORDER_BUFF_NETWORK_RESUMED");
        }
        if (command?.Kind == "attack")
        {
            if (_state!.Cards?.Pending?.Choice != "attack" || _state.Cards.Response == null) throw new InvalidDataException("Chosen action was not restored.");
            GD.Print("ORDER_CHOICE_NETWORK_RESUMED"); ApplyCommand(0, new("decline"));
        }
        if (command?.Kind == "resolve" && _state!.Cards!.Pending == null)
        { GD.Print("PUBLISHED_ORDER_NETWORK_OK"); Broadcast(); GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit(); }
    }
    private void PublishedOrderNetworkClient()
    {
        var cards = _state!.Cards!;
        if (CommandCards.Instances(CommandCards.Deck(_content, _state)).Count != 47) throw new InvalidDataException("Incorrect published command deck.");
        if (_publishedOrderStep == 0)
        { _publishedOrderStep = 1; Submit(new("special", "s1-3", Card: "core:brace-for-impact#1")); }
        else if (_publishedOrderStep == 1 && cards.Pending is { Choice: "" })
        {
            if (!FleetRuleTools.Has(_state.Units.Single(u => u.Id == "s1-3"), "brace")) throw new InvalidDataException("Public card buff missing.");
            if (!_engine.GetActions(_state, 1, "s1-3").AttackTargets.Contains("s0-0")) throw new InvalidDataException("Network fixture has no attack target.");
            _publishedOrderStep = 2; Submit(new("attack", "s1-3", "s0-0"));
        }
        else if (_publishedOrderStep == 2 && _state.Combat != null)
        { _publishedOrderStep = 3; Submit(new("resolve")); }
        else if (_publishedOrderStep == 3 && cards.Pending == null)
        {
            if (_state.Orders1 != 1 || !cards.Discard.Contains("core:brace-for-impact#1")) throw new InvalidDataException("Card or Play conservation failed.");
            _publishedOrderStep = 4; GD.Print("PUBLISHED_ORDER_NETWORK_OK"); GetTree().Quit();
        }
    }
    private async Task CheckPublishedOrders()
    {
        try
        {
            void Require(bool ok, string why) { if (!ok) throw new Exception(why); GD.Print("PUBLISHED_ORDER_UI_PASS " + why); }
            async Task Frames() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            IEnumerable<Node> Nodes(Node root) => root.GetChildren().Where(n => !n.IsQueuedForDeletion()).SelectMany(n => new[] { n }.Concat(Nodes(n)));
            void Press(string text) { var button = Nodes(_ui).OfType<Godot.Button>().Single(b => b.Text == text); Require(!button.Disabled, "Enabled: " + text); button.EmitSignal(BaseButton.SignalName.Pressed); }
            SinglePlayerMenu(); await Frames(); Press("COMMAND ORDERS AND EFFECTS"); _bot = false; await Frames();
            ConfigurePublishedOrderContent(); ConfigurePublishedOrderBattle(0);
            var hand = new[] { "core:deploy-engineers#1" };
            _state = _state! with { Cards = _state.Cards! with { Hand0 = hand, Deck = CommandCards.Instances(CommandCards.Deck(_content, _state)).Keys.Except(hand).Except(_state.Cards.Hand1).ToArray() },
                Units = _state.Units.Select(u => u.Id == "s0-0" ? u with { FocusFire = 4, Effects = ["weapons", "engines"] } : u).ToList() };
            _selectedCard = hand[0]; _repairChoice = ""; SelectUnit("s0-0"); await Frames();
            var picker = Nodes(_ui).OfType<OptionButton>().Single(o => o.ItemCount == 6 && o.GetItemText(0) == "Card repair: first available token");
            picker.Select(4); picker.EmitSignal(OptionButton.SignalName.ItemSelected, 4); Press("SPECIAL ORDER"); await Frames();
            var unit = _state!.Units.Single(u => u.Id == "s0-0"); Require(!FleetRuleTools.Has(unit, "weapons") && FleetRuleTools.Has(unit, "engines") && unit.FocusFire == 4, "Engineers removes only the selected token");
            Require(Nodes(_ui).OfType<Label>().Any(l => l.Text.Contains("Choose MOVE or ATTACK")), "Choice is explained in the command panel");
            string choiceState = _engine.Save(_state);
            _locale.SetLanguage("ru"); LocalizeInterface(); await Frames();
            Require(Nodes(_ui).OfType<Label>().Any(l => l.Text.Contains("Выберите ДВИЖЕНИЕ или АТАКУ"))
                && _locale.Text("Deploy Engineers") == "Высадить инженеров" && _engine.Save(_state) == choiceState,
                "Russian command text preserves the pending choice");
            _locale.SetLanguage("en"); LocalizeInterface(); await Frames();
            Press("SKIP ACTION"); await Frames();
            StartBattle(BasicCardBattle.OrdersMapId, false, persist: false); ConfigurePublishedOrderBattle(0); _selectedCard = "core:brace-for-impact#1"; SelectUnit("s0-0"); await Frames();
            Press("SPECIAL ORDER"); await Frames(); Require(FleetRuleTools.Has(_state!.Units.Single(u => u.Id == "s0-0"), "brace"), "Braced is applied before the chosen action");
            Press("ATTACK"); Submit(new("attack", "s0-0", "s1-3")); await Frames();
            Require(_state.Cards!.Pending?.Choice == "attack" && _state.Cards.Response != null, "Chosen attack is suspended for a response");
            string before = _engine.Save(_state); SaveGame(); LoadGame(false); await Frames(); Require(before == _engine.Save(_state!), "Card effect and choice restore exactly");
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "published-orders.png"));
            Press("SKIP WINDOW"); await Frames();
            Require(_state!.Cards!.Pending == null && _state.Orders0 == 1, "Chosen action completes with one card and Play");
            NetworkMenu(); await Frames(); Require(Nodes(_ui).OfType<CheckBox>().Any(c => c.Text == "Command orders and effects"), "Command deck can be hosted for PvP and co-op");
            GD.Print("PUBLISHED_ORDER_UI_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
