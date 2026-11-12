using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private async Task CheckSpecialCards()
    {
        try
        {
            void Require(bool condition, string message) { if (!condition) throw new Exception(message); GD.Print("SPECIAL_UI_PASS " + message); }
            async Task Frames() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            IEnumerable<Button> Buttons(Node root) => root.GetChildren().SelectMany(child => (child is Button b ? new[] { b } : Array.Empty<Button>()).Concat(Buttons(child)));
            void Press(string text)
            {
                var button = Buttons(_ui).Single(b => b.Text == text && !b.IsQueuedForDeletion());
                Require(!button.Disabled, "Enabled button: " + text); button.EmitSignal(BaseButton.SignalName.Pressed);
            }
            void Fixture()
            {
                StartBattle(BasicCardBattle.MapId, false, persist: false);
                var cards = _state!.Cards!; var deck = cards.Deck.Concat(cards.Hand0).Concat(cards.Hand1).ToArray();
                string[] hand = ["order-01", "order-09"]; var rest = deck.Except(hand).ToArray();
                _state = _state with { ActiveSide = 0, Cards = new(rest.Skip(2).ToArray(), hand, rest.Take(2).ToArray(), [], Phase: "orders") };
            }
            CardGuide(); await Frames();
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "card-guide.png"));
            Press("START CARD BATTLE"); await Frames();
            Require(_state?.Cards != null, "Card guide opens the playable card battle");
            Fixture(); _selectedCard = "order-01"; SelectUnit("s0-0"); await Frames();
            Press("SPECIAL ORDER"); await Frames();
            Require(_state!.Cards!.Pending is { Step: 0 } && _selected == "s0-0", "UI starts the selected special card");
            var position = _camera.UnprojectPosition(World(new(2, 2)));
            GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = position }, true);
            GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = position }, true);
            await Frames();
            Require(_state.Cards!.Pending is { Step: 1 }, "Board click advances one action");
            string snapshot = _engine.Save(_state); SaveGame(); LoadGame(false); await Frames();
            Require(_engine.Save(_state!) == snapshot && _state!.Cards!.Pending is { Step: 1 }, "Save/load resumes the second action");
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "special-order.png"));
            Press("SKIP ACTION"); await Frames();
            Require(_state.Cards!.Pending == null && _state.Orders0 == 1, "Skip completes the card without extra activation");
            Fixture(); _selectedCard = "order-09"; SelectUnit("s0-0"); await Frames();
            Press("SPECIAL ORDER"); await Frames();
            Require(_state!.Cards!.Pending!.Units.SequenceEqual(new[] { "s0-0", "s0-1" }), "Formation preassigns two units");
            Press("SKIP ACTION"); await Frames(); Press("SKIP ACTION"); await Frames();
            Require(_selected == "s0-1" && _action == "move", "UI switches actor and order mode for the second unit");
            Press("FINISH ORDER"); await Frames();
            Require(_state!.Cards!.Pending == null, "Finish button skips remaining steps");
            GD.Print("SPECIAL_UI_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
