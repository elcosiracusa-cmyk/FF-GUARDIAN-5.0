using System.Diagnostics;
using System.Reflection;

internal static class SmokeEntryPoint
{
    private static readonly TimeSpan ExplicitSmokeTimeout = TimeSpan.FromSeconds(45);

    private static int Main()
    {
        Console.WriteLine("ENGINE10_ENTRYPOINT_ENTER");
        Console.Out.Flush();

        string? workspace = Environment.GetEnvironmentVariable("GITHUB_WORKSPACE");
        string entryLog = !string.IsNullOrWhiteSpace(workspace)
            ? Path.Combine(workspace, "artifacts", "engine10-diagnostics", "entrypoint.log")
            : Path.Combine(Path.GetTempPath(), "ffguardian-engine10-entrypoint.log");

        try
        {
            File.AppendAllText(entryLog,
                $"{DateTime.UtcNow:O}\tentrypoint-enter{Environment.NewLine}");
        }
        catch
        {
            // La diagnostica non deve impedire l'avvio della suite.
        }

        try
        {
            RunExplicitSmoke("audit", AuditSmokeTests10.Run);
            RunExplicitSmoke("baseline-signatures", BaselineSignatureSmokeTests10.Run);
            RunExplicitSmoke("ransom-shield-maximum", RansomShieldMaximumSmokeBootstrap10.Run);
            RunExplicitSmoke("eicar", EicarSmokeTests10.Run);
            RunExplicitSmoke("archive-yara", ArchiveAndYaraSmokeTests10.Run);

            MethodInfo main = typeof(Program).GetMethod(
                "Main",
                BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new MissingMethodException("Program.Main non trovato.");

            object? invocation = main.Invoke(null, null);
            if (invocation is not Task<int> task)
                throw new InvalidOperationException("Program.Main non ha restituito Task<int>.");

            int exitCode = task.GetAwaiter().GetResult();
            try
            {
                File.AppendAllText(entryLog,
                    $"{DateTime.UtcNow:O}\tentrypoint-exit:{exitCode}{Environment.NewLine}");
            }
            catch { }
            return exitCode;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            Console.Error.WriteLine("ENGINE10_ENTRYPOINT_FAILURE");
            Console.Error.WriteLine(ex.InnerException);
            Console.Error.Flush();
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ENGINE10_ENTRYPOINT_FAILURE");
            Console.Error.WriteLine(ex);
            Console.Error.Flush();
            return 1;
        }
    }

    private static void RunExplicitSmoke(string name, Action action)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        Console.WriteLine($"ENGINE10_BOOTSTRAP_START {name}");
        Console.Out.Flush();

        Task task = Task.Run(action);
        Task completed = Task.WhenAny(task, Task.Delay(ExplicitSmokeTimeout)).GetAwaiter().GetResult();
        if (!ReferenceEquals(completed, task))
            throw new TimeoutException($"Engine10 bootstrap '{name}' exceeded {ExplicitSmokeTimeout.TotalSeconds:F0} seconds.");

        task.GetAwaiter().GetResult();
        Console.WriteLine($"ENGINE10_BOOTSTRAP_PASS {name} elapsed_ms={stopwatch.ElapsedMilliseconds}");
        Console.Out.Flush();
    }
}
