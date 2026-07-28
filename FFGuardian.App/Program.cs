using System.Diagnostics;
using System.Security.Principal;

namespace FFGuardian;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        if (!IsAdministrator())
        {
            try
            {
                Process.Start(new ProcessStartInfo(Environment.ProcessPath!)
                {
                    UseShellExecute = true,
                    Verb = "runas"
                });
            }
            catch { }
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) =>
            MessageBox.Show(e.Exception.Message, "FF GUARDIAN - Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FF Guardian", "Logs");
                Directory.CreateDirectory(folder);
                File.AppendAllText(Path.Combine(folder, "fatal-errors.log"), $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\t{e.ExceptionObject}{Environment.NewLine}");
            }
            catch { }
        };

        Application.Run(new AutonomousProtectionContext());
    }

    private static bool IsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
