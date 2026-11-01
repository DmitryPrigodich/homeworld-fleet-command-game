using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private async Task CheckCloseCombat()
    {
        try
        {
            void Require(bool value, string text) { if (!value) throw new Exception(text); GD.Print("CLOSE_UI_PASS " + text); }
            async Task Frames() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            IEnumerable<Node> Nodes(Node root) => root.GetChildren().SelectMany(child => new[] { child }.Concat(Nodes(child))).Where(n => !n.IsQueuedForDeletion());
            void Press(string text) { var b = Nodes(_ui).OfType<Button>().Single(b => b.Text == text); Require(!b.Disabled, "Enabled: " + text); b.EmitSignal(BaseButton.SignalName.Pressed); }
            void Fixture()
            {
                StartBattle(BasicCardBattle.TabletopMapId, false, persist: false);
                _content.Ships["core:kushan-interceptor"] = _content.Ships["core:kushan-interceptor"] with { Class = "frigate", Ram = 6, Crew = 6, Armor = 2, Success = 2, Abilities = [] };
                _content.Ships["core:taiidan-interceptor"] = _content.Ships["core:taiidan-interceptor"] with { Class = "capital", Armor = 1, Stars = 4, Success = 2, Abilities = [] };
                string[] a = ["order-01", "order-09"], b = ["order-21", "order-22"];
                _state = _state! with { ActiveSide = 0, ValorOwner = 0, Cards = new(Enumerable.Range(1, 24).Select(i => $"order-{i:00}").Except(a).Except(b).ToArray(), a, b, [], Phase: "orders"),
                    Units = _state.Units.Select(u => u.Id == "s0-0" ? u with { Q = 2, R = 2, Facing = 0, Models = 2 } : u.Id == "s1-3" ? u with { Q = 3, R = 2, Facing = 3, Models = 1 } : u).ToList() };
                _selectedCard = "order-01"; _attackKindChoice = "ranged"; SelectUnit("s0-0");
            }
            Fixture(); await Frames();
            var kind = Nodes(_ui).OfType<OptionButton>().Single(d => d.ItemCount == 3 && d.GetItemText(0) == "Атака: стрельба");
            kind.EmitSignal(OptionButton.SignalName.ItemSelected, 1); await Frames();
            Require(_attackKindChoice == "ram" && _unitActions.AttackTargets.Contains("s1-3"), "Attack selector shows a legal ram target");
            Press("▶ АТАКА"); await Frames(); Submit(new("attack", "s0-0", "s1-3")); await Frames(); Press("ПРОПУСТИТЬ ОКНО"); await Frames();
            Require(_state!.Combat?.Kind == "ram", "UI submits a ram rather than ranged fire");
            Press("ПЕРЕБРОС: VALOR"); await Frames();
            Press("ПРИНЯТЬ БРОСОК"); await Frames();
            Require(_state!.Combat is { Kind: "collision", Ghost: not null } && !_state.Units.Any(u => u.Id == "s1-3"), "Destroyed ship still rolls its collision through the UI");
            string snapshot = _engine.Save(_state); SaveGame(); LoadGame(false); await Frames(); Require(_engine.Save(_state!) == snapshot, "Collision ghost and rolled dice survive disk save/load");
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "close-collision.png"));
            Press("ПРИНЯТЬ БРОСОК"); await Frames(); Require(_state!.Overrun != null, "UI offers post-combat advancement");
            Press("ЗАНЯТЬ ГЕКС"); await Frames(); Require(_state!.Overrun == null && _state.Units.Single(u => u.Id == "s0-0") is { Q: 3, R: 2 }, "Overrun completes in the target's old hex");
            Fixture(); _content.Maps[_state!.Map] = _content.Maps[_state.Map] with { CaptureTargets = ["s1-3"] };
            _content.Ships["core:taiidan-interceptor"] = _content.Ships["core:taiidan-interceptor"] with { Armor = 0 };
            _attackKindChoice = "board"; _action = "attack"; DrawBattle(); await Frames(); Press("▶ АТАКА"); await Frames();
            Submit(new("attack", "s0-0", "s1-3")); await Frames(); Press("ПРОПУСТИТЬ ОКНО"); await Frames();
            Require(_state!.Combat?.Kind == "board" && FleetCombat.CanCapture(_content, _state), "Boarding capture is offered only for the allowed scenario target");
            Press("ЗАХВАТИТЬ ЦЕЛЬ"); await Frames(); Require(_state!.Units.Single(u => u.Id == "s1-3").Side == 0 && _state.Units.Single(u => u.Id == "s0-0").CrewDamage == 1, "UI capture transfers control and pays crew cost");
            GD.Print("CLOSE_UI_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
