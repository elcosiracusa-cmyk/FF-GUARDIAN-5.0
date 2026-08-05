using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;

namespace FFGuardian.PremiumWpf;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        string logPath = GetBootstrapLogPath();
        try
        {
            WriteLog(logPath, "BOOTSTRAP_START", null);
            App app = new();
            app.InitializeComponent();
            WriteLog(logPath, "APP_RESOURCES_LOADED", null);
            int exitCode = app.Run();
            WriteLog(logPath, $"BOOTSTRAP_EXIT code={exitCode.ToString(CultureInfo.InvariantCulture)}", null);
            return exitCode;
        }
        catch (Exception exception)
        {
            WriteLog(logPath, "BOOTSTRAP_FATAL", exception);
            try
            {
                MessageBox.Show(
                    $"FFGuardian non può essere avviato.\n\nDettagli salvati in:\n{logPath}\n\nErrore: {exception.GetType().Name}: {exception.Message}",
                    "FFGuardian — errore di avvio",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch
            {
                // Il log su disco rimane la sorgente diagnostica anche se USER32 non mostra la finestra.
            }
            return 100;
        }
    }

    private static string GetBootstrapLogPath()
    {
        string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string directory = Path.Combine(localData, "FFGuardian", "Logs");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "bootstrap.log");
    }

    private static void WriteLog(string path, string message, Exception? exception)
    {
        try
        {
            StringBuilder entry = new();
            entry.Append(DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture))
                .Append(' ')
                .Append(message)
                .AppendLine();
            if (exception is not null)
            {
                entry.AppendLine(exception.ToString());
            }
            File.AppendAllText(path, entry.ToString(), Encoding.UTF8);
        }
        catch
        {
            // Non mascherare l'eccezione originale per un errore secondario di logging.
        }
    }
}
