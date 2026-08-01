using System.Diagnostics;
using System.Security.Principal;
using FFGuardian.Engine10;

namespace FFGuardian;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Any(argument => string.Equals(argument, "--health-check", StringComparison.OrdinalIgnoreCase)))
            return RunHealthCheck();

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
                    "FF GUARDIAN 10.0.1 RC1",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
            return 0;
        }

        using Mutex singleInstance = new(
            initiallyOwned: true,
            name: @"Local\FFGuardian.ELCO.SingleInstance",
            createdNew: out bool firstInstance);

        if (!firstInstance)
        {
            MessageBox.Show(
                "FF GUARDIAN è già in esecuzione. Controlla la barra delle applicazioni o l’area di notifica.",
                "FF GUARDIAN 10.0.1 RC1",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return 0;
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
            return 0;
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            MessageBox.Show(
                "FF GUARDIAN ha intercettato un errore imprevisto e lo ha registrato.",
                "FF GUARDIAN 10.0.1 RC1",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }
    }

    private static int RunHealthCheck()
    {
        string root = Path.Combine(Path.GetTempPath(), "FFGuardian-Health-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            using FFGuardianEngine10 engine = new(
                Path.Combine(root, "signatures.json"),
                updaterPublicKeyPem: null,
                Path.Combine(root, "Quarantine"),
                Path.Combine(root, "Rollback"));
            engine.ReloadSignaturesAsync().GetAwaiter().GetResult();
            string signatureVersion = engine.SignatureDatabaseVersion;
            if (string.IsNullOrWhiteSpace(signatureVersion))
                throw new InvalidOperationException("Versione del database firme non disponibile.");

            Console.WriteLine($"FF GUARDIAN Engine10 health check passed. Signatures: {signatureVersion}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FF GUARDIAN Engine10 health check failed.");
            Console.Error.WriteLine(ex);
            return 2;
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static bool IsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        WindowsPrincipal principal = new(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
