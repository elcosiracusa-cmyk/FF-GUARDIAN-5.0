using System.Globalization;
using System.IO;
using System.Text;

namespace FFGuardian.PremiumWpf;

internal static class StartupDiagnostics
{
    private static readonly object Sync = new();
    private static readonly string ResolvedLogPath = ResolveLogPath();

    public static string LogPath => ResolvedLogPath;

    public static void Write(string stage, Exception? exception = null, string? message = null)
    {
        StringBuilder entry = new();
        entry.Append(DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture));
        entry.Append(" | ").Append(stage);
        if (!string.IsNullOrWhiteSpace(message)) entry.Append(" | ").Append(message.ReplaceLineEndings(" "));
        entry.AppendLine();
        if (exception is not null) entry.AppendLine(exception.ToString());

        try
        {
            lock (Sync)
            {
                File.AppendAllText(ResolvedLogPath, entry.ToString(), Encoding.UTF8);
            }
        }
        catch (Exception writeException)
        {
            DebugFallback(entry.ToString(), writeException);
        }
    }

    private static string ResolveLogPath()
    {
        string[] candidateRoots =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Path.GetTempPath()
        ];

        foreach (string root in candidateRoots)
        {
            try
            {
                string directory = Path.Combine(root, "FFGuardian", "Logs");
                Directory.CreateDirectory(directory);
                string candidate = Path.Combine(directory, "startup.log");
                using FileStream probe = new(candidate, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                return candidate;
            }
            catch (Exception exception)
            {
                DebugFallback($"Impossibile usare il percorso log: {root}", exception);
            }
        }

        return Path.Combine(AppContext.BaseDirectory, "startup-fallback.log");
    }

    private static void DebugFallback(string message, Exception exception)
    {
        System.Diagnostics.Debug.WriteLine(message);
        System.Diagnostics.Debug.WriteLine(exception);
    }
}
