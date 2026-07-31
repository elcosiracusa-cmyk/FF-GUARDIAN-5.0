using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FFGuardian;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Manteniamo la richiesta di elevazione all'avvio (come richiesto dall'utente)
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

        using Mutex singleInstance = new(true, @"Local\FFGuardian.ELCO.SingleInstance", out bool firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show(
                "FF GUARDIAN è già in esecuzione. Controlla la barra delle applicazioni o l'area di notifica.",
                "FF GUARDIAN",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        // Setup DI & Logging
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().AddDebug());
        services.AddSingleton<IDefenderService, DefenderService>();
        // Registrare altri servizi qui quando necessari: IQuarantineService, ISettingsService, IHashService...
        var provider = services.BuildServiceProvider();

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("FFGuardian");

        Application.ThreadException += (_, e) =>
        {
            StabilityCoordinator82.WriteStabilityLog(e.Exception);
            (string message, MessageBoxIcon icon) = ErrorMessageFormatter.Format(e.Exception);
            string title = icon == MessageBoxIcon.Information
                ? "FF GUARDIAN - Operazione già in corso"
                : "FF GUARDIAN - Avviso controllato";
            MessageBox.Show(message, title, MessageBoxButtons.OK, icon);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Exception exception = e.ExceptionObject as Exception
                ?? new Exception(e.ExceptionObject?.ToString() ?? "Errore non identificato");
            StabilityCoordinator82.WriteStabilityLog(exception);
            logger.LogError(exception, "Unhandled exception in AppDomain");
        };

        try
        {
            AutonomousSecurityEngine.Start();
            StabilityCoordinator82.Start();

            // Passiamo il provider alla MainForm in modo che possa risolvere servizi tramite DI
            Application.Run(new MainForm(provider));
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            logger.LogCritical(ex, "Fatal error in main loop");
            MessageBox.Show(
                "FF GUARDIAN ha intercettato un errore imprevisto e lo ha registrato nella diagnostica.",
                "FF GUARDIAN",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static bool IsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
