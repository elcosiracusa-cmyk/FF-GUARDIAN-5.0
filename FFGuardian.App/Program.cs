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
        try
        {
            return MainCore(args);
        }
        catch (Exception ex)
        {
            string reportPath = CrashReporter10.Write(ex, "Avvio preliminare");
            ShowCrashDialog(ex, reportPath);
            return 1;
        }
    }

    private static int MainCore(string[] args)
    {
        if (args.Any(argument => string.Equals(argument, "--health-check", StringComparison.OrdinalIgnoreCase)))
            return RunHealthCheck();

        if (!IsAdministrator())
        {
            try
            {
                string? executable = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(executable))
                    throw new InvalidOperationException("Percorso dell'eseguibile non disponibile.");

                Process.Start(new ProcessStartInfo(executable)
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
            CrashReporter10.WriteStartupMarker("Inizializzazione contesto di protezione");
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
        string method = exception.TargetSite?.ToString() ?? "Metodo non disponibile";
        string stack = exception.StackTrace ?? "Stack trace non disponibile";
        if (stack.Length > 1800)
            stack = stack[..1800] + "…";

        string details =
            $"Errore: {type}\n" +
            $"Messaggio: {message}\n" +
            $"Metodo: {method}\n\n" +
            $"Stack trace:\n{stack}\n\n" +
            $"Report:\n{reportPath}";

        try { Clipboard.SetText(details); } catch { }

        MessageBox.Show(
            "FF GUARDIAN non ha potuto completare l’avvio.\n\n" +
            details +
            "\n\nI dettagli sono stati copiati negli appunti.",
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
    private static readonly object Sync = new();

    public static string Write(Exception exception, string phase)
    {
        ArgumentNullException.ThrowIfNull(exception);
        string report = BuildReport(exception, phase);
        return WriteReport(report, "CRASH");
    }

    public static string WriteStartupMarker(string phase)
    {
        string report =
            "FF GUARDIAN 10 — STARTUP MARKER\r\n" +
            new string('=', 72) + "\r\n" +
            $"Data locale: {DateTime.Now:O}\r\n" +
            $"Fase: {phase}\r\n" +
            $"Processo: {Environment.ProcessId}\r\n" +
            $"Eseguibile: {Environment.ProcessPath}\r\n";
        return WriteReport(report, "STARTUP");
    }

    private static string WriteReport(string report, string kind)
    {
        lock (Sync)
        {
            string fileName = $"FFGuardian-{kind}-{DateTime.Now:yyyyMMdd-HHmmssfff}-{Environment.ProcessId}.txt";
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string localReports = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FF Guardian", "Engine10", "CrashReports");

            foreach (string folder in new[] { desktop, localReports, Path.GetTempPath() })
            {
                if (string.IsNullOrWhiteSpace(folder))
                    continue;

                try
                {
                    Directory.CreateDirectory(folder);
                    string finalPath = Path.Combine(folder, fileName);
                    string temporaryPath = finalPath + ".tmp";
                    byte[] data = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(report);

                    using (FileStream stream = new(
                        temporaryPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.Read,
                        bufferSize: 4096,
                        options: FileOptions.WriteThrough))
                    {
                        stream.Write(data, 0, data.Length);
                        stream.Flush(flushToDisk: true);
                    }

                    File.Move(temporaryPath, finalPath, overwrite: true);
                    if (!File.Exists(finalPath) || new FileInfo(finalPath).Length == 0)
                        throw new IOException("Il report diagnostico è stato creato ma risulta vuoto.");

                    return finalPath;
                }
                catch
                {
                    // Prova automaticamente il percorso successivo.
                }
            }

            return "Impossibile salvare il report diagnostico.";
        }
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
        report.AppendLine($"Processo: {Environment.ProcessId}");
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
