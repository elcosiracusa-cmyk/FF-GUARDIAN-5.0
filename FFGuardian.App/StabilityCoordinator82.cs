namespace FFGuardian;

internal static class StabilityCoordinator82
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, DateTime> RecentErrors = new(StringComparer.Ordinal);
    private const string LogName = "stability-9.0.log";

    public static void WriteStabilityLog(Exception ex)
    {
        try
        {
            string key = ex.GetType().FullName + "|" + ex.Message;
            lock (Sync)
            {
                if (RecentErrors.TryGetValue(key, out DateTime last) && DateTime.UtcNow - last < TimeSpan.FromMinutes(2))
                    return;
                RecentErrors[key] = DateTime.UtcNow;
                RemoveExpiredKeys();
            }

            string folder = GetLogFolder();
            Directory.CreateDirectory(folder);
            RotateLogIfNeeded(folder);
            string message = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\tFF GUARDIAN 9.0\t{ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}";
            File.AppendAllText(Path.Combine(folder, LogName), message);
        }
        catch
        {
            // Logging must never interrupt the security application.
        }
    }

    private static void RotateLogIfNeeded(string folder)
    {
        string current = Path.Combine(folder, LogName);
        if (!File.Exists(current) || new FileInfo(current).Length < 2 * 1024 * 1024)
            return;

        string archive = Path.Combine(folder, $"stability-9.0-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        File.Move(current, archive, true);
        foreach (string oldFile in Directory.GetFiles(folder, "stability-9.0-*.log").OrderByDescending(File.GetLastWriteTimeUtc).Skip(5))
        {
            try { File.Delete(oldFile); }
            catch { }
        }
    }

    private static void RemoveExpiredKeys()
    {
        DateTime threshold = DateTime.UtcNow.AddMinutes(-10);
        foreach (string key in RecentErrors.Where(pair => pair.Value < threshold).Select(pair => pair.Key).ToArray())
            RecentErrors.Remove(key);
    }

    private static string GetLogFolder() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "FF Guardian",
        "Logs");
}
