using Godot;
using FleetCommand.Core;
namespace FleetCommand.Game;
public partial class Main
{
    private void ScenarioMenu()
    {
        ClearScreen(); Heading("Сценарии из книги"); var box = Panel(70, 180, 700, 620);
        var map = _content.Maps["core:first-contact"];
        Text(box, map.Name, 24, _colors.Highlight); Text(box, map.Briefing, 18);
        Text(box, "Разведданные отмечаются после завершения приказа. Уничтожение носителя данных уничтожает и данные. Красные гексы обозначают зону эвакуации таиидан.", 17, _colors.Muted);
        Button(box, "ИГРАТЬ ПРОТИВ AI", () => { StartBattle(map.Id, true); QueueBot(); });
        Button(box, "ИГРАТЬ ВДВОЁМ ЛОКАЛЬНО", () => StartBattle(map.Id, false));
        Button(box, "НАЗАД", SinglePlayerMenu); MenuScene();
    }
    private bool TryScenarioBotMove(BattleUnit unit)
    {
        if (_state!.Scenario?.Fired.Contains("delivered") == true) return false;
        var events = _content.Maps[_state.Map].Events!;
        var target = ScenarioEvents.HasToken(_state, unit.Id, "data") ? events.Single(e => e.Effect == "deliver") : events.Single(e => e.Effect == "mark");
        int Distance(Hex h) => target.Zone != null ? target.Zone.Min(h.Distance) : h.Distance(new(_content.Maps[_state.Map].Objects[target.Object].Q, _content.Maps[_state.Map].Objects[target.Object].R));
        var moves = _engine.GetActions(_state, 1, unit.Id).MoveCells.OrderBy(Distance).ThenBy(h => h.Q).ThenBy(h => h.R).ToArray();
        if (moves.Length == 0 || Distance(moves[0]) >= Distance(new(unit.Q, unit.R)) && !(target.Effect == "mark" && Distance(moves[0]) <= target.Radius)) return false;
        return ApplyCommand(1, new("move", unit.Id, Q: moves[0].Q, R: moves[0].R, Card: _state.Cards!.Hand1.FirstOrDefault() ?? ""));
    }
    private void DrawScenarioZones()
    {
        if (_state == null) return;
        foreach (var e in _content.Maps[_state.Map].Events ?? []) if (e.Effect == "deliver" && e.Zone != null) HighlightHexes(e.Zone, C(_colors.Enemy), "EscapeZone");
    }
}
