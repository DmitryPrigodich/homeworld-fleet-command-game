using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private async Task CheckTabletop()
    {
        try
        {
            void Require(bool value, string text) { if (!value) throw new Exception(text); GD.Print("TABLETOP_UI_PASS " + text); }
            async Task Frames() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            IEnumerable<Node> Nodes(Node root) => root.GetChildren().SelectMany(child => new[] { child }.Concat(Nodes(child))).Where(n => !n.IsQueuedForDeletion());
            void Press(string text) { var b = Nodes(_ui).OfType<Button>().Single(b => b.Text == text); Require(!b.Disabled, "Enabled: " + text); b.EmitSignal(BaseButton.SignalName.Pressed); }
            SinglePlayerMenu(); await Frames(); Press("RULES / DEVELOPMENT MODE"); _bot = false; await Frames();
            Require(FleetBattle, "Rulebook mode opens through the menu");
            var bid = Nodes(_ui).OfType<CheckBox>().First(); bid.ButtonPressed = true;
            Press("CONFIRM BID"); await Frames();
            Require(_state!.Cards!.Ready0 && _state.Cards.Bids0 is { Length: 1 } && LocalCommandSide == 1, "UI commits selected initiative cards and switches hotseat side");
            for (int i = 0; _state!.Cards!.Phase == "initiative" && i < 30; i++) { Press("CONFIRM BID"); await Frames(); }
            Require(_state!.Cards!.Phase == "orders", "Initiative ties cannot trap the hotseat UI");
            StartBattle(BasicCardBattle.TabletopMapId, false, persist: false);
            string[] a = ["order-01", "order-09"], b = ["order-21", "order-22"];
            _state = _state! with { ActiveSide = 0, ValorOwner = 0, Cards = new(Enumerable.Range(1, 24).Select(i => $"order-{i:00}").Except(a).Except(b).ToArray(), a, b, [], Phase: "orders"),
                Units = _state.Units.Select(u => u.Id == "s0-0" ? u with { Q = 1, R = 1 } : u.Id == "s1-3" ? u with { Q = 2, R = 1 } : u).ToList() };
            SelectUnit("s0-0"); await Frames(); Press("ATTACK"); await Frames();
            Require(_state!.Cards!.Pending?.BasicAction == "attack" && _state.Orders0 == 1, "Action button reserves exactly one basic order");
            var point = _camera.UnprojectPosition(World(new(2, 1)));
            GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = point }, true);
            GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = point }, true); await Frames();
            Press("SKIP WINDOW"); await Frames(); Require(_state!.Combat != null, "Board attack pauses for a post-roll choice");
            while (_state!.Combat is { AbilityRerolls: > 0 }) { Press("APPLY REROLLS"); await Frames(); }
            string snapshot = _engine.Save(_state); SaveGame(); LoadGame(false); await Frames(); Require(_engine.Save(_state!) == snapshot, "UI save restores the actual rolled dice and decision");
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "tabletop-dice.png"));
            Press("REROLL: VALOR"); await Frames();
            while (_state!.Combat is { AbilityRerolls: > 0 }) { Press("APPLY REROLLS"); await Frames(); }
            Press("ACCEPT ROLL"); await Frames();
            Require(_state!.Combat == null && _state.Cards!.Response == null && _state.Cards.Pending == null && _state.ValorOwner == 1, "Reroll resumes the paid order and transfers Valor");
            ApplyCommand(1, new("pass")); await Frames(); Require(_state!.Cards!.Phase == "cleanup", "Cleanup has an explicit UI phase");
            Press("CONFIRM DISCARD"); await Frames(); Press("CONFIRM DISCARD"); await Frames();
            Require(_state!.Cards!.Phase == "initiative", "Both cleanup commitments start the next round");
            _content.Ships["core:kushan-interceptor"] = _content.Ships["core:kushan-interceptor"] with { Success = 2 };
            _state = _state with { ActiveSide = 0, RandomState = 1, Cards = _state.Cards with { Phase = "orders" },
                Units = _state.Units.Select(u => u.Id == "s0-0" ? u with { FocusFire = 1, Effects = ["weapons"] } : u).ToList() };
            _repairChoice = "ask"; SelectUnit("s0-0"); await Frames(); Press(_action == "attack" ? "▶ ATTACK" : "ATTACK"); await Frames();
            Require(_state.Repairs is { Length: 1 }, "Activation opens a post-roll repair choice");
            snapshot = _engine.Save(_state); SaveGame(); LoadGame(false); await Frames(); Require(_engine.Save(_state!) == snapshot, "Repair successes survive UI save/load");
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "tabletop-repair.png"));
            Press("REMOVE: weapons"); await Frames(); Require(_state!.Repairs == null && !FleetRuleTools.Has(_state.Units.Single(u => u.Id == "s0-0"), "weapons"), "UI spends the chosen repair success");
            Press("SKIP ACTION"); await Frames();
            string fingerprint = _content.Fingerprint, save = _engine.Save(_state); CardStylesMenu(); await Frames();
            var dropdown = Nodes(_ui).OfType<OptionButton>().Single(); int amber = Array.FindIndex(_cardStyles, p => p.Definition.Id == "amber") + 1;
            Require(amber > 0, "Independent cosmetic pack is discovered"); dropdown.EmitSignal(OptionButton.SignalName.ItemSelected, amber); await Frames();
            Require(_settings.CardStyle == "amber" && _content.Fingerprint == fingerprint && _engine.Save(_state!) == save, "Cosmetic selection leaves match hash and state unchanged");
            _state = _state! with { Cards = _state.Cards! with { Phase = "orders" } }; DrawBattle(); await Frames();
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "tabletop-card-style.png"));
            _settings = _settings with { CardStyle = "text" }; DrawBattle(); await Frames();
            GD.Print("TABLETOP_UI_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
