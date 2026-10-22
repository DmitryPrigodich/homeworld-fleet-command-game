namespace FleetCommand.Core;

public enum TutorialStep { Welcome, Camera, Select, Move, EndTurn, Attack, Complete }

// A small, fixed lesson uses the real battle engine, without touching campaign/skirmish saves.
public sealed class TutorialLesson
{
    public const string MapId = "tutorial:basics";
    public const string ScoutId = "s0-0";
    public const string TargetId = "s1-2";
    public static readonly Hex Destination = new(3, 2);
    public TutorialStep Step { get; private set; }

    public static void Register(ContentCatalog content)
    {
        content.Ships["tutorial:scouts"] = new("tutorial:scouts", "Учебные перехватчики", "training", "", 6, 3, 1, 5, 0, 1, VisualSize: 0.28f);
        content.Ships["tutorial:frigate"] = new("tutorial:frigate", "Учебный фрегат", "training", "", 2, 2, 3, 5, 2, 3, VisualSize: 0.6f);
        content.Fleets["tutorial:blue"] = new("tutorial:blue", "Учебный флот", [new("tutorial:scouts", 1, 2), new("tutorial:frigate", 1, 4)]);
        content.Fleets["tutorial:red"] = new("tutorial:red", "Учебная цель", [new("tutorial:scouts", 4, 2, 3)]);
        content.Objects["tutorial:obstacle"] = new("tutorial:obstacle", "Непроходимый объект", BlocksMovement: true, Color: "#8792a2");
        content.Rules["tutorial:rules"] = new("tutorial:rules", "Учебный бой", RearShots: false);
        content.Maps[MapId] = new(MapId, "Основы командования", 7, 6, "", [new("tutorial:obstacle", 4, 4)],
            ["tutorial:blue", "tutorial:red"], "tutorial:rules");
    }

    public void Continue()
    {
        if (Step is TutorialStep.Welcome or TutorialStep.Camera) Step++;
    }

    public void Select(string id)
    {
        if (Step == TutorialStep.Select && id == ScoutId) Step = TutorialStep.Move;
    }

    public bool Allows(BattleCommand command) => Step switch
    {
        TutorialStep.Move => command is { Kind: "move", Unit: ScoutId } && new Hex(command.Q, command.R) == Destination,
        TutorialStep.EndTurn => command.Kind == "pass",
        TutorialStep.Attack => command is { Kind: "attack", Unit: ScoutId, Target: TargetId },
        _ => false
    };

    public void Accepted(BattleCommand command)
    {
        if (Allows(command)) Step++;
    }
}
