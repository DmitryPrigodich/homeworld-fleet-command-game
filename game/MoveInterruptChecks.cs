using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private const string MoveInterruptCard = "core:interrupt-move#2";
    private int _moveInterruptNetworkStep;
    private bool _moveInterruptContextSeen;
    private void ConfigureMoveInterruptContent()
    {
        foreach (var ship in _content.Ships.Values.Where(d => d.Id.StartsWith("practice:orders-")).ToArray())
            _content.Ships[ship.Id] = ship with { Weapons = [], Armor = 50, Abilities = [] };
        foreach (string id in new[] { "practice:orders-kushan-boarder", "practice:orders-taiidan-boarder" }) _content.Ships[id] = _content.Ships[id] with { Move = 3 };
    }
    private void ConfigureMoveInterruptBattle()
    {
        // Isolate Move from further Attack opportunities; core scenarios exercise both types at one pause.
        string[] a = ["core:all-ahead-full#1", "core:interrupt-counterintelligence#1"], b = [MoveInterruptCard], spent = ["core:interrupt-attack#1", "core:interrupt-attack#2"];
        _state = _state! with { ActiveSide = 0, Cards = _state.Cards! with { Phase = "orders", Hand0 = a, Hand1 = b, Discard = spent, UsedInterrupts0 = ["attack"], UsedInterrupts1 = ["attack"],
            Deck = CommandCards.Instances(CommandCards.Deck(_content, _state)).Keys.Except(a).Except(b).Except(spent).ToArray() },
            Units = _state.Units.Select(u => u.Id == "s0-0" ? u with { Q = 1, R = 1 } : u.Id == "s1-3" ? u with { Q = 2, R = 2 } : u).ToList() };
        DrawBattle();
    }
    private void MoveInterruptNetworkHost(BattleCommand? command)
    {
        if (command?.Kind != "interrupt-move") return;
        void Restore(string marker) { string before = _engine.Save(_state!); LoadGame(false); if (before != _engine.Save(_state!)) throw new InvalidDataException("Move interrupt disk restore diverged."); GD.Print(marker); }
        Restore("MOVE_INTERRUPT_PENDING_NETWORK_RESUMED"); if (_state!.Cards!.Counter == null) throw new InvalidDataException("Missing movement counter opportunity.");
        ApplyCommand(0, new("decline-counter"));
        for (int n = 0; n < 10 && _state.Cards.Response is { Replan: false }; n++)
        { if (_state.Cards.Response == null || InterruptRules.Responder(_state) != 0) throw new InvalidDataException("Unexpected movement reaction owner."); ApplyCommand(0, new("decline")); }
        Restore("MOVE_INTERRUPT_REPLAN_NETWORK_RESUMED");
        if (_state.Cards.Response is not { Replan: true, MoveSpent: 1, MoveBudget: 3 } || _state.Units.Single(u => u.Id == "s1-3") is not { Q: 3, R: 1, Activated: false }) throw new InvalidDataException("Remaining movement or reactor activation was lost.");
        ApplyCommand(0, new("resume-move", "s0-0", Q: 4, R: 0)); Restore("MOVE_INTERRUPT_FINISH_NETWORK_RESUMED");
        if (_state.Cards.Response != null || _state.Orders0 != 1 || _state.Orders1 != 0) throw new InvalidDataException("Original order did not finish once.");
        Broadcast(); GD.Print("MOVE_INTERRUPT_NETWORK_OK"); GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit();
    }
    private void MoveInterruptNetworkClient()
    {
        if (_moveInterruptNetworkStep == 0 && _state!.Cards!.Response is { Replan: false })
        { _moveInterruptNetworkStep = 1; Submit(new("interrupt-move", "s1-3", Q: 3, R: 1, Card: MoveInterruptCard)); }
        else if (_moveInterruptNetworkStep == 1 && _state!.Cards!.Counter != null) _moveInterruptNetworkStep = 2;
        if (_moveInterruptNetworkStep >= 2 && _state!.Cards!.MoveInterrupt is { } frame)
        { if (frame.Response.Command.Unit != "s0-0" || frame.ParentSide != 0) throw new InvalidDataException("Public movement context differs."); _moveInterruptContextSeen = true; }
        if (_moveInterruptNetworkStep == 2 && _state!.Cards!.Response?.Replan == true)
        { _moveInterruptNetworkStep = 3; GD.Print("MOVE_INTERRUPT_PUBLIC_REPLAN_OK"); }
        else if (_moveInterruptNetworkStep == 3 && _state!.Cards!.Response == null)
        {
            if (!_moveInterruptContextSeen || _state.Units.Single(u => u.Id == "s0-0") is not { Q: 4, R: 0 } || _state.Orders0 != 1 || _state.Orders1 != 0 || !InterruptRules.Played(_state.Cards, 1).Contains(MoveInterruptCard)) throw new InvalidDataException("Movement result or hidden-state boundary differs.");
            _moveInterruptNetworkStep = 4; GD.Print("MOVE_INTERRUPT_NETWORK_OK"); GetTree().Quit();
        }
    }
    private async Task CheckMoveInterrupt()
    {
        try
        {
            void Require(bool ok, string why) { if (!ok) throw new Exception(why); GD.Print("MOVE_INTERRUPT_UI_PASS " + why); }
            async Task Frames() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            IEnumerable<Node> Nodes(Node root) => root.GetChildren().Where(n => !n.IsQueuedForDeletion()).SelectMany(n => new[] { n }.Concat(Nodes(n)));
            void Press(string text) { var b = Nodes(_ui).OfType<Button>().Single(b => b.Text == text); Require(!b.Disabled, "Enabled: " + text); b.EmitSignal(BaseButton.SignalName.Pressed); }
            void Click(Hex cell) { var screen = _camera.UnprojectPosition(World(cell)); _UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = screen }); }
            async Task Setup()
            {
                StartBattle(BasicCardBattle.OrdersMapId, false, persist: false); ConfigureMoveInterruptContent(); ConfigureMoveInterruptBattle();
                ApplyCommand(0, new("move", "s0-0", Q: 4, R: 1, Card: "core:all-ahead-full#1")); _selectedCard = MoveInterruptCard; SelectUnit("s1-3"); await Frames();
                Require(_action == "move" && _unitActions.MoveCells.Contains(new(3, 1)), "Move card selects green reaction destinations"); Click(new(3, 1)); await Frames(); Require(_state!.Cards!.Counter?.Command.Kind == "interrupt-move", "Field click proposes movement before effect");
            }
            await Setup(); string before = _engine.Save(_state!); SaveGame(); LoadGame(false); await Frames(); Require(before == _engine.Save(_state!), "Proposed movement restores");
            Press("LET INTERRUPT PROCEED"); await Frames();
            for (int n = 0; n < 10 && _state!.Cards!.Response is { Replan: false }; n++) { Press("SKIP WINDOW"); await Frames(); }
            Require(_state!.Cards!.Response is { Replan: true, MoveSpent: 1, MoveBudget: 3 } && LocalCommandSide == 0 && _state.Units.Single(u => u.Id == "s1-3") is { Q: 3, R: 1, Activated: false }, "Reactor moves without activation and returns route choice to its owner");
            before = _engine.Save(_state); SaveGame(); LoadGame(false); await Frames(); Require(before == _engine.Save(_state!), "Remaining route choice restores exactly");
            _locale.SetLanguage("ru"); LocalizeInterface(); await Frames(); Require(Nodes(_ui).OfType<Button>().Any(b => b.Text == "ОСТАНОВИТЬСЯ"), "Route choice is localized");
            _locale.SetLanguage("en"); LocalizeInterface(); await Frames();
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "interrupt-move-replan.png"));
            Require(_unitActions.MoveCells.Contains(new(4, 0)) && !_unitActions.MoveCells.Contains(new(3, 1)), "Green replan cells respect remaining allowance and new blocker");
            Click(new(4, 0)); await Frames(); Require(_state.Cards.Response == null && _state.Units.Single(u => u.Id == "s0-0") is { Q: 4, R: 0 } && _state.Orders0 == 1 && _state.Orders1 == 0, "Viewport click finishes revised movement without extra Play");
            await Setup(); Press("CANCEL INTERRUPT"); await Frames(); Require(_state!.Cards!.Response == null && _state.Units.Single(u => u.Id == "s1-3") is { Q: 2, R: 2 } && _state.Units.Single(u => u.Id == "s0-0").Q == 4, "Counter cancels movement and preserves the original route");
            GD.Print("MOVE_INTERRUPT_UI_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
