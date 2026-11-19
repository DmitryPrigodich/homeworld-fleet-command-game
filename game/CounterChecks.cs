using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private const string CounterCard = "core:interrupt-counterintelligence#1", CounterAttack = "core:interrupt-attack#1";
    private int _counterNetworkStep;
    private void ConfigureCounterContent()
    {
        foreach (string id in new[] { "practice:orders-kushan-boarder", "practice:orders-taiidan-boarder" })
            _content.Ships[id] = _content.Ships[id] with { Armor = 50, Move = 3, Weapons = [new("gun", "Counter test gun", 2, 5)] };
        foreach (var ship in _content.Ships.Values.Where(d => d.Id.StartsWith("practice:orders-") && d.Faction == "taiidan").ToArray()) _content.Ships[ship.Id] = ship with { Move = 0 };
    }
    private void ConfigureCounterBattle()
    {
        string[] a = [CounterCard, "core:all-ahead-full#1"], b = [CounterAttack, "core:interrupt-attack#2"];
        _state = _state! with { ActiveSide = 0, Cards = _state.Cards! with { Phase = "orders", Hand0 = a, Hand1 = b, Discard = [],
            Deck = CommandCards.Instances(CommandCards.Deck(_content, _state)).Keys.Except(a).Except(b).ToArray() },
            Units = _state.Units.Select(u => u.Id == "s0-0" ? u with { Q = 1, R = 1 } : u.Id == "s1-3" ? u with { Q = 2, R = 2, Activated = true } : u).ToList() };
        DrawBattle();
    }
    private void CounterNetworkHost(BattleCommand? command)
    {
        if (command?.Kind != "interrupt") return;
        string before = _engine.Save(_state!); LoadGame(false);
        if (before != _engine.Save(_state!) || _state!.Cards!.Counter == null || _state.Combat != null) throw new InvalidDataException("Counter decision did not restore before effect.");
        GD.Print("COUNTER_PENDING_NETWORK_RESUMED"); ApplyCommand(0, new("counter", Card: CounterCard));
        before = _engine.Save(_state!); LoadGame(false);
        if (before != _engine.Save(_state!) || _state!.Cards!.Response != null || _state.Units.Single(u => u.Id == "s0-0").Q != 3
            || InterruptRules.Available(_content, _state, 1) || InterruptRules.Available(_content, _state, 0, "counter")) throw new InvalidDataException("Counter cancellation continuation did not restore.");
        GD.Print("COUNTER_CANCEL_NETWORK_RESUMED"); Broadcast(); GD.Print("COUNTER_NETWORK_OK"); GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit();
    }
    private void CounterNetworkClient()
    {
        if (_counterNetworkStep == 0 && _state!.Cards!.Response != null)
        { _counterNetworkStep = 1; Submit(new("interrupt", "s1-3", "s0-0", Card: CounterAttack)); }
        else if (_counterNetworkStep == 1 && _state!.Cards!.Counter != null)
        { _counterNetworkStep = 2; GD.Print("COUNTER_PUBLIC_PENDING_OK"); }
        else if (_counterNetworkStep == 2 && _state!.Cards!.Counter == null && _state.Cards.Discard.Contains(CounterCard))
        {
            if (!_state.Cards.Discard.Contains(CounterAttack) || _state.Cards.Response != null || _state.Units.Single(u => u.Id == "s0-0").Q != 3 || _state.Orders1 != 0) throw new InvalidDataException("Counter result was not synchronized.");
            _counterNetworkStep = 3; GD.Print("COUNTER_NETWORK_OK"); GetTree().Quit();
        }
    }
    private async Task CheckCounter()
    {
        try
        {
            void Require(bool ok, string why) { if (!ok) throw new Exception(why); GD.Print("COUNTER_UI_PASS " + why); }
            async Task Frames() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            IEnumerable<Node> Nodes(Node root) => root.GetChildren().Where(n => !n.IsQueuedForDeletion()).SelectMany(n => new[] { n }.Concat(Nodes(n)));
            void Press(string text) { var b = Nodes(_ui).OfType<Button>().Single(b => b.Text == text); Require(!b.Disabled, "Enabled: " + text); b.EmitSignal(BaseButton.SignalName.Pressed); }
            async Task Offer()
            {
                StartBattle(BasicCardBattle.OrdersMapId, false, persist: false); ConfigureCounterContent(); ConfigureCounterBattle();
                _state = _state! with { Cards = _state.Cards! with { Hand0 = _state.Cards.Hand0.Append("core:interrupt-counterintelligence#2").ToArray(), Deck = _state.Cards.Deck.Where(id => id != "core:interrupt-counterintelligence#2").ToArray() } };
                ApplyCommand(0, new("move", "s0-0", Q: 3, R: 1, Card: "core:all-ahead-full#1")); _selectedCard = CounterAttack; SelectUnit("s1-3"); await Frames();
                var target = _state!.Units.Single(u => u.Id == "s0-0"); var screen = _camera.UnprojectPosition(World(new(target.Q, target.R)));
                _UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = screen }); await Frames();
                Require(_state.Cards!.Counter != null && _state.Combat == null, "Field click declares interrupt before effect");
                Require(Nodes(_ui).OfType<Label>().Any(l => l.Text.Contains("COUNTER: " + SideName(0))), "Header identifies the counter decision owner");
            }
            await Offer(); string before = _engine.Save(_state!); SaveGame(); LoadGame(false); await Frames(); Require(before == _engine.Save(_state!), "Counter window restores exactly");
            _locale.SetLanguage("ru"); LocalizeInterface(); await Frames(); Require(Nodes(_ui).OfType<Button>().Any(b => b.Text == "ОТМЕНИТЬ ПРЕРЫВАНИЕ"), "Counter action is localized");
            _locale.SetLanguage("en"); LocalizeInterface(); await Frames();
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "counterintelligence.png"));
            Press("CANCEL INTERRUPT"); await Frames(); Require(_state!.Cards!.Counter == null && _state.Cards.Response == null && _state.Cards.Discard.Contains(CounterCard) && _state.Units.Single(u => u.Id == "s0-0").Q == 3, "Cancellation resumes original movement");
            await Offer(); Press("LET INTERRUPT PROCEED"); await Frames(); Require(_state!.Cards!.Counter == null && InterruptRules.Played(_state.Cards, 1).Contains(CounterAttack), "Declining counter executes interrupt");
            GD.Print("COUNTER_UI_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
