using Godot;
using FleetCommand.Core;

namespace FleetCommand.Game;

public partial class Main
{
    private async Task CheckActionHighlights()
    {
        try
        {
            void Check(bool condition, string description)
            {
                if (!condition) throw new InvalidOperationException(description);
                GD.Print("ACTIONS_PASS " + description);
            }
            async Task Frame() { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
            StartBattle("core:training", false, persist: false);
            var ship = _state!.Units.First(u => u.Definition == "core:kushan-frigate");
            var enemy = _state.Units.First(u => u.Side == 1);
            _state = _state with { Units = _state.Units.Select(u => u.Id == ship.Id ? u with { Q = 3, R = 3 } : u.Id == enemy.Id ? u with { Q = 6, R = 3 } : u).ToList() };
            string snapshot = _engine.Save(_state);
            SelectUnit(ship.Id); await Frame(); await Frame();
            Check(_unitActions.MoveCells.Length > 0 && _unitActions.AttackTargets.Contains(enemy.Id), "selecting a unit displays green moves and a red target together");
            Check(_world.GetNodeOrNull<MeshInstance3D>("MoveHighlights") != null && _world.GetNodeOrNull<MeshInstance3D>("AttackHighlights") != null,
                "movement and attack hex meshes are present on the board");
            var actions = _unitActions;
            SetBattleAction("attack"); await Frame(); await Frame();
            Check(_unitActions.MoveCells.SequenceEqual(actions.MoveCells) && _unitActions.AttackTargets.SequenceEqual(actions.AttackTargets),
                "both previews remain visible regardless of the selected order button");
            Check(snapshot == _engine.Save(_state), "selection and range display do not change the battle");
            if (DisplayServer.GetName() != "headless") GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(_root, "tmp", "action-highlights.png"));
            SelectUnit(enemy.Id); await Frame(); await Frame();
            Check(_unitActions.MoveCells.Length == 0 && _unitActions.AttackTargets.Length == 0, "inspecting an enemy does not advertise orders the local player cannot give");
            SelectUnit(ship.Id);
            var goal = _unitActions.MoveCells.First();
            ApplyCommand(0, new("move", ship.Id, Q: goal.Q, R: goal.R)); await Frame(); await Frame();
            Check(_unitActions.MoveCells.Length == 0 && _unitActions.AttackTargets.Length == 0, "highlights clear after the selected unit spends its action");
            _state = _state! with { Finished = true, Winner = 0 }; DrawBattle(); await Frame(); await Frame();
            Check(_world.GetNodeOrNull<MeshInstance3D>("MoveHighlights") == null && _world.GetNodeOrNull<MeshInstance3D>("AttackHighlights") == null,
                "a finished battle has no stale action highlights");
            GD.Print("ACTION_HIGHLIGHTS_OK"); GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("ACTION_HIGHLIGHTS_FAILED " + e); GetTree().Quit(1); }
    }
}
