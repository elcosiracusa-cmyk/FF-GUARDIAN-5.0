using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;
using System.Text;
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
                string reportPath = CrashReporter10.Write(ex, "Elevazione amministratore");
                MessageBox.Show(
                    $"FF GUARDIAN richiede i privilegi di amministratore.\n\nReport diagnostico:\n{reportPath}",
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
            string reportPath = CrashReporter10.Write(e.Exception, "Eccezione interfaccia");
            ShowCrashDialog(e.Exception, reportPath);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Exception exception = e.ExceptionObject as Exception
                ?? new Exception(e.ExceptionObject?.ToString() ?? "Errore non identificato");
            CrashReporter10.Write(exception, "Eccezione dominio applicazione");
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            CrashReporter10.Write(e.Exception, "Eccezione attività asincrona");
            e.SetObserved();
        };

        try
        {
            Application.Run(new IndependentProtectionContext100());
            return 0;
        }
        catch (Exception ex)
        {
            string reportPath = CrashReporter10.Write(ex, "Avvio applicazione");
            ShowCrashDialog(ex, reportPath);
            return 1;
        }
    }

    private static void ShowCrashDialog(Exception exception, string reportPath)
    {
        string type = exception.GetType().FullName ?? exception.GetType().Name;
        string message = string.IsNullOrWhiteSpace(exception.Message)
            ? "Messaggio non disponibile"
            : exception.Message;

        MessageBox.Show(
            $"FF GUARDIAN non ha potuto completare l’avvio.\n\n" +
            $"Errore: {type}\n" +
            $"Dettagli: {message}\n\n" +
            $"Il report è stato salvato qui:\n{reportPath}\n\n" +
            "Invia il file FFGuardian-CRASH più recente per la correzione.",
            "FF GUARDIAN 10 — Diagnostica avvio",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
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

internal static class CrashReporter10
{
    public static string Write(Exception exception, string phase)
    {
        ArgumentNullException.ThrowIfNull(exception);
        string fileName = $"FFGuardian-CRASH-{DateTime.Now:yyyyMMdd-HHmmssfff}.txt";
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        string fallback = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FF Guardian", "Engine10", "CrashReports");

        string report = BuildReport(exception, phase);
        foreach (string folder in new[] { desktop, fallback, Path.GetTempPath() })
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folder))
                    continue;
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, fileName);
                File.WriteAllText(path, report, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                try { StabilityCoordinator82.WriteStabilityLog(exception); } catch { }
                return path;
            }
            catch
            {
            }
        }

        return "Impossibile salvare il report diagnostico.";
    }

    private static string BuildReport(Exception exception, string phase)
    {
        AssemblyName assembly = Assembly.GetExecutingAssembly().GetName();
        StringBuilder report = new();
        report.AppendLine("FF GUARDIAN 10 — CRASH REPORT");
        report.AppendLine(new string('=', 72));
        report.AppendLine($"Data locale: {DateTime.Now:O}");
        report.AppendLine($"Data UTC: {DateTime.UtcNow:O}");
        report.AppendLine($"Fase: {phase}");
        report.AppendLine($"Versione: {assembly.Version}");
        report.AppendLine($"Sistema operativo: {Environment.OSVersion}");
        report.AppendLine($"Processo 64 bit: {Environment.Is64BitProcess}");
        report.AppendLine($"Amministratore: {IsCurrentAdministrator()}");
        report.AppendLine($"Cartella applicazione: {AppContext.BaseDirectory}");
        report.AppendLine($"Eseguibile: {Environment.ProcessPath}");
        report.AppendLine();
        report.AppendLine("ECCEZIONE COMPLETA");
        report.AppendLine(new string('-', 72));
        report.AppendLine(exception.ToString());
        return report.ToString();
    }

    private static bool IsCurrentAdministrator()
    {
        try
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
