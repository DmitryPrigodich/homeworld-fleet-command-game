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
            Add(after.Round, after.Winner ?? -1, after.Winner == null ? "Бой завершён: лимит раундов, ничья." : "Победа. Бой завершён.");
        else
        {
            if (after.Round != before.Round) Add(after.Round, -1, $"Начался раунд {after.Round}. Подразделения снова готовы к приказам.");
            if (after.ActiveSide != before.ActiveSide || after.Round != before.Round) Add(after.Round, after.ActiveSide, "Очередь отдавать приказ.");
        }
        return entries.TakeLast(Capacity).ToList();
    }
}
