namespace FleetCommand.Game;

internal static class ErrorReport
{
    public static string? Write(string dataDirectory, string context, Exception error)
    {
        // Reporting must never replace the original failure with a log-writing failure.
        try
        {
            string directory = System.IO.Path.Combine(dataDirectory, "logs");
            System.IO.Directory.CreateDirectory(directory);
            string path = System.IO.Path.Combine(directory, $"error-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}.log");
            System.IO.File.WriteAllText(path, $"{DateTimeOffset.Now:O}\n{context}\n{error}\n");
            return path;
        }
        catch { return null; }
    }
}
