namespace FFGuardian;

internal static class StabilityCoordinator82
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, DateTime> RecentErrors = new(StringComparer.Ordinal);
    private const string LogName = "stability-10.0.log";

    public static void WriteStabilityLog(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        try
        {
            string key = $"{ex.GetType().FullName}|{ex.Message}";
            lock (Sync)
            {
                if (RecentErrors.TryGetValue(key, out DateTime last) &&
                    DateTime.UtcNow - last < TimeSpan.FromMinutes(2))
                    return;

                RecentErrors[key] = DateTime.UtcNow;
                RemoveExpiredKeys();
            }

            WriteLine(
                "ERROR",
                $"{ex.GetType().Name}: {ex.Message}{Environment.NewLine}" +
                (string.IsNullOrWhiteSpace(ex.StackTrace) ? "Stack trace non disponibile." : ex.StackTrace));
        }
        catch
        {
            // Il logging non deve interrompere l'applicazione.
        }
    }

    public static void WriteInformationLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        try
        {
            WriteLine("INFO", message.Replace('\r', ' ').Replace('\n', ' '));
        }
        catch
        {
            // Il logging non deve interrompere l'applicazione.
        }
    }

    private static void WriteLine(string level, string message)
    {
        string folder = GetLogFolder();
        Directory.CreateDirectory(folder);
        RotateLogIfNeeded(folder);
        string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\tFF GUARDIAN 10.0\t{level}\t{message}{Environment.NewLine}";
        File.AppendAllText(Path.Combine(folder, LogName), line);
    }

    private static void RotateLogIfNeeded(string folder)
    {
        string current = Path.Combine(folder, LogName);
        if (!File.Exists(current) || new FileInfo(current).Length < 2 * 1024 * 1024)
            return;

        string archive = Path.Combine(folder, $"stability-10.0-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        File.Move(current, archive, true);

        foreach (string oldFile in Directory
                     .GetFiles(folder, "stability-10.0-*.log")
                     .OrderByDescending(File.GetLastWriteTimeUtc)
                     .Skip(5))
        {
            try { File.Delete(oldFile); }
            catch { }
        }
    }

    private static void RemoveExpiredKeys()
    {
        DateTime threshold = DateTime.UtcNow.AddMinutes(-10);
        foreach (string key in RecentErrors
                     .Where(pair => pair.Value < threshold)
                     .Select(pair => pair.Key)
                     .ToArray())
            RecentErrors.Remove(key);
    }

    private static string GetLogFolder() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "FF Guardian",
        "Logs");
}
