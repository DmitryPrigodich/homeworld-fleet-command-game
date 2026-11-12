namespace FleetCommand.Core;

public sealed record BattleLogEntry(int Number, int Round, int Side, string Text);

public static class BattleLog
{
    public const int Capacity = 200;

    public static List<BattleLogEntry> AfterCommand(BattleState before, BattleState after, int side, string message)
    {
        var entries = new List<BattleLogEntry>(before.Log ?? []);
        void Add(int round, int actor, string text) => entries.Add(new((entries.LastOrDefault()?.Number ?? -1) + 1, round, actor, text));
        Add(before.Round, side, message);
        if (after.Finished)
            Add(after.Round, after.Winner ?? -1, after.EndReason != "" ? FleetObjectives.ResultText(after) + (after.Winner == null ? " Draw." : " Victory.") : after.Winner == null ? "Battle complete: round limit reached, draw." : "Victory. Battle complete.");
        else
        {
            if (after.Round != before.Round) Add(after.Round, -1, $"Round started {after.Round}. Units are ready for orders again.");
            if (after.ActiveSide != before.ActiveSide || after.Round != before.Round) Add(after.Round, after.ActiveSide, "Your turn to issue an order.");
        }
        return entries.TakeLast(Capacity).ToList();
    }
}
