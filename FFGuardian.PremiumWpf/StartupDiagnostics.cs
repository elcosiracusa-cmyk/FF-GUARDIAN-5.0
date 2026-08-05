using System.Globalization;
using System.IO;
using System.Text;

namespace FFGuardian.PremiumWpf;

internal static class StartupDiagnostics
{
    private static readonly object Sync = new();

    public static string LogPath
    {
        get
        {
            string root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FFGuardian",
                "Logs");
            Directory.CreateDirectory(root);
            return Path.Combine(root, "startup.log");
        }
    }

    public static void Write(string stage, Exception? exception = null, string? message = null)
    {
        try
        {
            StringBuilder line = new();
            line.Append(DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture));
            line.Append(" | ").Append(stage);
            if (!string.IsNullOrWhiteSpace(message)) line.Append(" | ").Append(message);
            if (exception is not null)
            {
                line.AppendLine();
                line.Append(exception);
            }
            line.AppendLine();

            lock (Sync)
            {
                File.AppendAllText(LogPath, line.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // La diagnostica non deve provocare un secondo arresto durante lo startup.
        }
    }
}
