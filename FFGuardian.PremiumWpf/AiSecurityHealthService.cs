using FFGuardian.AI.Security;

namespace FFGuardian.PremiumWpf;

public sealed class AiSecurityHealthService(IAiThreatAnalyzer analyzer, IAiModelProvider modelProvider)
{
    public async Task<ComponentStatus> CheckAsync(CancellationToken cancellationToken)
    {
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), "FFGuardian", "AI-SelfTest", Guid.NewGuid().ToString("N"));
        string temporaryFile = Path.Combine(temporaryDirectory, "benign-ai-self-test.txt");

        try
        {
            Directory.CreateDirectory(temporaryDirectory);
            await File.WriteAllTextAsync(
                temporaryFile,
                "FFGuardian AI benign local self-test. No executable content.",
                cancellationToken).ConfigureAwait(false);

            ModelVersionInfo model = await modelProvider.GetStatusAsync(cancellationToken).ConfigureAwait(false);
            AiAnalysisResult result = await analyzer.AnalyzeAsync(
                new AiAnalysisRequest(temporaryFile, Timeout: TimeSpan.FromSeconds(10)),
                cancellationToken).ConfigureAwait(false);

            bool operational = !result.IsCancelled && string.IsNullOrWhiteSpace(result.Error) && result.FileFeatures is not null;
            string mode = model.IsVerified
                ? $"modello locale verificato {model.Version}"
                : "euristica locale operativa; modello ONNX non verificato o non installato";
            string detail = operational
                ? $"Self-test completato, rischio {result.Score.Value}/100; {mode}. Nessun dato inviato al cloud."
                : $"Self-test AI fallito: {result.Error ?? result.Explanation}";

            return new ComponentStatus("AI Security", operational, detail);
        }
        catch (OperationCanceledException)
        {
            return new ComponentStatus("AI Security", false, "Self-test AI annullato o scaduto.");
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Write("AI.HealthCheck.Failed", exception);
            return new ComponentStatus("AI Security", false, $"Errore runtime AI: {exception.Message}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, true);
            }
            catch (Exception cleanupException)
            {
                StartupDiagnostics.Write("AI.HealthCheck.CleanupFailed", cleanupException);
            }
        }
    }
}
