using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace FFGuardian.PremiumWpf;

public partial class ToolsView : UserControl
{
    public ToolsView()
    {
        InitializeComponent();
    }

    private void OpenLogs_Click(object sender, RoutedEventArgs e)
    {
        string logsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FFGuardian",
            "Logs");

        try
        {
            Directory.CreateDirectory(logsDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = true,
                ArgumentList = { logsDirectory }
            });
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            StartupDiagnostics.Write("Tools.OpenLogs.Failed", exception, logsDirectory);
            MessageBox.Show(
                $"Impossibile aprire la cartella log.\n\nPercorso: {logsDirectory}",
                "FFGuardian",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
