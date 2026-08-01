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
            catch (Exception ex)
            {
                StabilityCoordinator82.WriteStabilityLog(ex);
                MessageBox.Show(
                    "FF GUARDIAN richiede i privilegi di amministratore per analizzare servizi e configurazioni di sistema.",
                    "FF GUARDIAN 10 Core Alpha",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            return;
        }

        using Mutex singleInstance = new(
            initiallyOwned: true,
            name: @"Local\FFGuardian.ELCO.SingleInstance",
            createdNew: out bool firstInstance);

        if (!firstInstance)
        {
            MessageBox.Show(
                "FF GUARDIAN è già in esecuzione. Controlla la barra delle applicazioni o l’area di notifica.",
                "FF GUARDIAN 10 Core Alpha",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        Application.ThreadException += (_, e) =>
        {
            StabilityCoordinator82.WriteStabilityLog(e.Exception);
            (string message, MessageBoxIcon icon) = ErrorMessageFormatter.Format(e.Exception);
            MessageBox.Show(
                message,
                "FF GUARDIAN 10 — Errore controllato",
                MessageBoxButtons.OK,
                icon);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Exception exception = e.ExceptionObject as Exception
                ?? new Exception(e.ExceptionObject?.ToString() ?? "Errore non identificato");
            StabilityCoordinator82.WriteStabilityLog(exception);
        };

        try
        {
            Application.Run(new IndependentProtectionContext100());
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            MessageBox.Show(
                "FF GUARDIAN ha intercettato un errore imprevisto e lo ha registrato.",
                "FF GUARDIAN 10 Core Alpha",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static bool IsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
