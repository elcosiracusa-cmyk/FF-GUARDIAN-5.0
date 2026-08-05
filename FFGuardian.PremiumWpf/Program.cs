using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Windows;

namespace FFGuardian.PremiumWpf;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        string logPath = StartupDiagnostics.LogPath;
        RegisterEarlyExceptionHandlers();

        try
        {
            WriteEnvironmentSnapshot(args);
            bool safeMode = HasArgument(args, "--safe-mode");
            bool smokeTest = HasArgument(args, "--smoke-test");

            StartupDiagnostics.Write("Bootstrap.App.Create", message: $"SafeMode={safeMode}; SmokeTest={smokeTest}");
            App app = new();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            if (!safeMode)
            {
                StartupDiagnostics.Write("Bootstrap.Resources.Load.Begin");
                app.InitializeComponent();
                StartupDiagnostics.Write("Bootstrap.Resources.Load.Success");
            }
            else
            {
                StartupDiagnostics.Write("Bootstrap.Resources.Skipped", message: "Safe mode");
            }

            StartupDiagnostics.Write("Bootstrap.Application.Run.Begin");
            int exitCode = app.Run();
            StartupDiagnostics.Write("Bootstrap.Application.Run.End", message: $"ExitCode={exitCode.ToString(CultureInfo.InvariantCulture)}");
            return exitCode;
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Write("Bootstrap.Fatal", exception);
            ShowFatalStartupMessage(logPath, exception);
            return 100;
        }
    }

    private static void RegisterEarlyExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            StartupDiagnostics.Write(
                "Bootstrap.AppDomainUnhandledException",
                eventArgs.ExceptionObject as Exception,
                eventArgs.IsTerminating ? "Processo in terminazione" : "Eccezione non gestita");

        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            StartupDiagnostics.Write("Bootstrap.UnobservedTaskException", eventArgs.Exception);
            eventArgs.SetObserved();
        };
    }

    private static void WriteEnvironmentSnapshot(string[] args)
    {
        Assembly assembly = typeof(Program).Assembly;
        string version = assembly.GetName().Version?.ToString() ?? "--";
        string architecture = RuntimeInformation.ProcessArchitecture.ToString();
        string framework = RuntimeInformation.FrameworkDescription;
        string currentDirectory = Environment.CurrentDirectory;
        bool elevated = IsElevated();
        string arguments = string.Join(' ', args.Select(SanitizeArgument));

        StartupDiagnostics.Write("Process.Start", message: $"PID={Environment.ProcessId.ToString(CultureInfo.InvariantCulture)}");
        StartupDiagnostics.Write("Process.Version", message: version);
        StartupDiagnostics.Write("Process.Architecture", message: architecture);
        StartupDiagnostics.Write("Process.Framework", message: framework);
        StartupDiagnostics.Write("Process.BaseDirectory", message: AppContext.BaseDirectory);
        StartupDiagnostics.Write("Process.CurrentDirectory", message: currentDirectory);
        StartupDiagnostics.Write("Process.Elevated", message: elevated.ToString(CultureInfo.InvariantCulture));
        StartupDiagnostics.Write("Process.Arguments", message: arguments);
    }

    private static bool IsElevated()
    {
        try
        {
            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Write("Process.PrivilegeCheck.Failed", exception);
            return false;
        }
    }

    private static bool HasArgument(IEnumerable<string> arguments, string expected) =>
        arguments.Any(argument => string.Equals(argument, expected, StringComparison.OrdinalIgnoreCase));

    private static string SanitizeArgument(string argument)
    {
        if (argument.Length <= 256) return argument;
        return string.Concat(argument.AsSpan(0, 256), "…");
    }

    private static void ShowFatalStartupMessage(string logPath, Exception exception)
    {
        try
        {
            MessageBox.Show(
                $"FFGuardian non è riuscito ad avviarsi. È stato creato un report diagnostico.\n\n{logPath}\n\n{exception.GetType().Name}: {exception.Message}",
                "FFGuardian — errore di avvio",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        catch (Exception messageException)
        {
            StartupDiagnostics.Write("Bootstrap.ErrorDialog.Failed", messageException);
        }
    }
}
