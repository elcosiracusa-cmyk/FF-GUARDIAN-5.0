using System.Reflection;

internal static class SmokeEntryPoint
{
    private static int Main()
    {
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

        Console.WriteLine("ENGINE10_ENTRYPOINT_ENTER");
        Console.Out.Flush();

        try
        {
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
}
