using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private const string RepairInterruptCard = "core:interrupt-repairs#1";
    private int _repairInterruptNetworkStep;
    private void ConfigureRepairInterruptBattle()
    {
        string[] a = ["core:all-ahead-full#1", "core:interrupt-counterintelligence#1"], b = [RepairInterruptCard, "core:all-ahead-full#2", "core:interrupt-counterintelligence#2"];
        _state = _state! with { ActiveSide = 0, Cards = _state.Cards! with { Phase = "orders", Hand0 = a, Hand1 = b, Discard = [],
            Deck = CommandCards.Instances(CommandCards.Deck(_content, _state)).Keys.Except(a).Except(b).ToArray() },
            Units = _state.Units.Select(u => u.Id == "s0-0" ? u with { FocusFire = 4 } : u.Id == "s0-2" ? u with { Models = 2, FocusFire = 4 } : u.Id == "s1-5" ? u with { Models = 2, FocusFire = 2 } : u).ToList() };
        DrawBattle();
    }
    private void RepairInterruptNetworkHost(BattleCommand? command)
    {
        if (command?.Kind != "interrupt-repairs") return;
        string before = _engine.Save(_state!); LoadGame(false);
        if (before != _engine.Save(_state!) || _state!.Cards!.Counter?.Command.Kind != "interrupt-repairs" || _state.Units.Single(u => u.Id == "s1-5").Models != 2) throw new InvalidDataException("Repair proposal did not restore before effect.");
        GD.Print("REPAIR_INTERRUPT_PENDING_NETWORK_RESUMED"); ApplyCommand(0, new("decline-counter"));
        before = _engine.Save(_state!); LoadGame(false);
        if (before != _engine.Save(_state!) || _state!.Units.Single(u => u.Id == "s1-5") is not { Models: 3, FocusFire: 2, Activated: false }
            || _state.Orders1 != 0 || _state.ActiveSide != 0 || InterruptRules.Available(_content, _state, 1, "repairs")) throw new InvalidDataException("Repair result changed turn, tokens or activation.");
        GD.Print("REPAIR_INTERRUPT_RESULT_NETWORK_RESUMED"); Broadcast(); GD.Print("REPAIR_INTERRUPT_NETWORK_OK"); GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit();
    }
    private void RepairInterruptNetworkClient()
    {
        if (_repairInterruptNetworkStep == 0)
        { _repairInterruptNetworkStep = 1; Submit(new("interrupt-repairs", "s1-5", Card: RepairInterruptCard, Repair: "fighter")); }
        else if (_repairInterruptNetworkStep == 1 && _state!.Cards!.Counter != null) _repairInterruptNetworkStep = 2;
        else if (_repairInterruptNetworkStep == 2 && _state!.Cards!.Counter == null)
        {
            if (_state.Units.Single(u => u.Id == "s1-5") is not { Models: 3, FocusFire: 2, Activated: false } || _state.Orders1 != 0 || _state.ActiveSide != 0) throw new InvalidDataException("Remote out-of-turn repair did not synchronize.");
            _repairInterruptNetworkStep = 3; GD.Print("REPAIR_INTERRUPT_NETWORK_OK"); GetTree().Quit();
        }
    }
    private async Task CheckRepairInterrupt()
    {
        try
        {
            void Require(bool ok, string why) { if (!ok) throw new Exception(why); GD.Print("REPAIR_INTERRUPT_UI_PASS " + why); }
            async Task Frames() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            IEnumerable<Node> Nodes(Node root) => root.GetChildren().Where(n => !n.IsQueuedForDeletion()).SelectMany(n => new[] { n }.Concat(Nodes(n)));
            void Press(string text) { var b = Nodes(_ui).OfType<Button>().Single(b => b.Text == text); Require(!b.Disabled, "Enabled: " + text); b.EmitSignal(BaseButton.SignalName.Pressed); }
            async Task Setup(string target)
            {
                StartBattle(BasicCardBattle.OrdersMapId, false, persist: false); ConfigureRepairInterruptBattle();
                _state = _state! with { Cards = _state.Cards! with { Hand0 = _state.Cards.Hand0.Append(RepairInterruptCard).ToArray(), Hand1 = _state.Cards.Hand1.Where(id => id != RepairInterruptCard).ToArray() } };
                _selectedCard = "core:all-ahead-full#1"; _repairInterruptUnit = target; DrawBattle(); await Frames();
            }
            await Setup("s0-2");
            Require(Nodes(_ui).OfType<OptionButton>().Single(o => o.ItemCount > 0 && o.GetItemText(0).StartsWith("Unit #")).GetCombinedMinimumSize().X < 240, "Repair target fits the command panel");
            _locale.SetLanguage("ru"); LocalizeInterface(); await Frames();
            Require(Nodes(_ui).OfType<Button>().Any(b => b.Text == "ВОССТАНОВИТЬ ИСТРЕБИТЕЛЬ"), "Repairs controls are localized"); _locale.SetLanguage("en"); LocalizeInterface(); await Frames();
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "interrupt-repairs.png"));
            Press("RESTORE ONE FIGHTER"); await Frames(); Require(_state!.Cards!.Counter?.Command.Card == RepairInterruptCard, "Repair button preserves its card despite unrelated hand selection");
            string before = _engine.Save(_state); SaveGame(); LoadGame(false); await Frames(); Require(before == _engine.Save(_state!), "Repair counter decision restores");
            Press("LET INTERRUPT PROCEED"); await Frames(); Require(_state!.Units.Single(u => u.Id == "s0-2") is { Models: 3, FocusFire: 4, Activated: false } && _state.Orders0 == 0, "Fighter restored without changing other state");
            await Setup("s0-0"); Press("REMOVE FOCUS TOKEN"); await Frames(); Press("CANCEL INTERRUPT"); await Frames();
            Require(_state!.Units.Single(u => u.Id == "s0-0").FocusFire == 4 && _state.Cards!.Discard.Contains(RepairInterruptCard), "Counter cancels repair before token removal");
            GD.Print("REPAIR_INTERRUPT_UI_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
