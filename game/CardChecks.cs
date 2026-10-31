using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private async Task CheckCards()
    {
        try
        {
            StartBattle(BasicCardBattle.MapId, false, persist: false);
            for (int attempt = 0; _state!.Cards!.Phase == "initiative" && attempt < 30; attempt++)
            {
                int side = _state.Cards.Ready0 ? 1 : 0;
                if (!ApplyCommand(side, new("initiative"))) throw new Exception("Initiative rejected");
            }
            if (_state!.Cards!.Phase != "orders") throw new Exception("Initiative never resolved");
            var unit = _state.Units.First(u => u.Side == _state.ActiveSide);
            SelectUnit(unit.Id); await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            string card = (_state.ActiveSide == 0 ? _state.Cards.Hand0 : _state.Cards.Hand1)[0];
            var actions = _engine.GetActions(_state, _state.ActiveSide, unit.Id);
            var hex = actions.MoveCells.First();
            if (!ApplyCommand(_state.ActiveSide, new("move", unit.Id, Q: hex.Q, R: hex.R, Card: card))) throw new Exception("Card order rejected");
            string before = _engine.Save(_state!); SaveGame(); LoadGame(false);
            if (_engine.Save(_state!) != before) throw new Exception("UI save/resume changed the card battle");
            GD.Print("CARD_UI_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError(e.ToString()); GetTree().Quit(1); }
    }
}
