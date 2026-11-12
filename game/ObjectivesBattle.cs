using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private bool _showObjectives = true;
    private void DrawObjectives(Control parent)
    {
        if (_state == null || !FleetObjectives.Enabled(_content, _state)) return;
        Text(parent, $"MEDALS: {_state.Medals0} : {_state.Medals1} / {_content.Maps[_state.Map].VictoryMedals}", 15, _colors.Highlight);
        Button(parent, _showObjectives ? "HIDE OBJECTIVES" : "SCENARIO OBJECTIVES", () => { _showObjectives = !_showObjectives; DrawBattle(); });
        if (!_showObjectives) return;
        foreach (int side in new[] { 0, 1 })
        {
            Text(parent, SideName(side), 13, Commander(side).Color);
            foreach (var goal in _content.Maps[_state.Map].Objectives!.Where(o => o.Side == side))
            {
                bool active = FleetObjectives.Active(_state, goal); int progress = active ? goal.Threshold : FleetObjectives.Progress(_content, _state, goal);
                string status = active ? "✓ " : (_state.Claims ?? []).Contains(goal.Id) ? "↺ " : "○ ";
                Text(parent, status + goal.Name + $" · {goal.Medals} med.\n{Math.Min(progress, goal.Threshold)}/{goal.Threshold}" + (goal.Reversible ? " · reversible by recapture" : ""), 12, active ? _colors.Highlight : _colors.Text);
            }
        }
        Text(parent, $"Destroyed ★ {_state.Stars0}:{_state.Stars1}\nHarvested ★ {_state.Harvested0}:{_state.Harvested1}", 12, _colors.Muted);
    }
}
