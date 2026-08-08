using FFGuardian.AI.Security;
using Microsoft.Extensions.DependencyInjection;

List<string> failures = [];
await RunAsync("score bounds and levels", async () =>
{
    IThreatScoreCalculator calculator = new ThreatScoreCalculator();
    ThreatScore low = calculator.Calculate([]);
    ThreatScore critical = calculator.Calculate([new("x", "x", 150, EvidenceDirection.Risk, "test", 1)]);
    Assert(low.Value == 0 && low.Level == AiRiskLevel.Low, "Low score invalid");
    Assert(critical.Value == 100 && critical.Level == AiRiskLevel.Critical, "Critical score invalid");
    await Task.CompletedTask;
});
await RunAsync("contrasting evidence", async () =>
{
    ThreatScore score = new ThreatScoreCalculator().Calculate([
        new("clamav-detection", "Detected", 50, EvidenceDirection.Risk, "ClamAV", .9),
        new("trusted-signature", "Trusted", 30, EvidenceDirection.Trust, "Authenticode", .9)]);
    Assert(score.Value == 20, "Contrasting evidence calculation failed");
    await Task.CompletedTask;
});
await RunAsync("behavior correlation", async () =>
{
    IReadOnlyCollection<ThreatEvidence> evidence = new BehaviorCorrelationService().Correlate(new(2, 1, 500, 200, true, false, false, false, true, true, false, false));
    Assert(evidence.Count >= 4, "Correlated evidence missing");
    await Task.CompletedTask;
});
await RunAsync("model missing remains unavailable", async () =>
{
    string root = Path.Combine(Path.GetTempPath(), "FFGuardian AI Tests", Guid.NewGuid().ToString("N"));
    ServiceProvider provider = new ServiceCollection().AddLogging().AddFFGuardianAiSecurity(o => { o.ModelPath = Path.Combine(root, "missing.onnx"); o.ModelLockPath = Path.Combine(root, "missing.json"); o.DataDirectory = root; }).BuildServiceProvider();
    ModelVersionInfo status = await provider.GetRequiredService<IAiModelProvider>().GetStatusAsync(CancellationToken.None);
    Assert(!status.IsVerified, "Missing model must not be verified");
    await provider.DisposeAsync();
});
await RunAsync("allowlist hash only and revocation", async () =>
{
    string root = Path.Combine(Path.GetTempPath(), "FFGuardian AI Tests", Guid.NewGuid().ToString("N"));
    ServiceProvider provider = new ServiceCollection().AddLogging().AddFFGuardianAiSecurity(o => o.DataDirectory = root).BuildServiceProvider();
    ILocalHashAllowlist list = provider.GetRequiredService<ILocalHashAllowlist>();
    string hash = new string('a', 64);
    await list.AddAsync(hash, "false positive fixture", DateTimeOffset.UtcNow.AddMinutes(5), CancellationToken.None);
    Assert(await list.ContainsAsync(hash, CancellationToken.None), "Hash not found");
    await list.RevokeAsync(hash, CancellationToken.None);
    Assert(!await list.ContainsAsync(hash, CancellationToken.None), "Hash not revoked");
    await provider.DisposeAsync();
    Directory.Delete(root, true);
});
await RunAsync("static extraction and privacy", async () =>
{
    string root = Path.Combine(Path.GetTempPath(), "FFGuardian AI Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    string file = Path.Combine(root, "invoice.pdf.exe.txt");
    await File.WriteAllTextAsync(file, "harmless fixture powershell");
    ServiceProvider provider = new ServiceCollection().AddLogging().AddFFGuardianAiSecurity(o => o.DataDirectory = root).BuildServiceProvider();
    FileSecurityFeatures features = await provider.GetRequiredService<IFeatureExtractor>().ExtractAsync(file, CancellationToken.None);
    Assert(features.Sha256.Length == 64, "SHA-256 missing");
    Assert(!features.Sha256.Contains(Environment.UserName, StringComparison.OrdinalIgnoreCase), "User data leaked into hash");
    await provider.DisposeAsync();
    Directory.Delete(root, true);
});
await RunAsync("cancellation", async () =>
{
    using CancellationTokenSource cts = new(); cts.Cancel();
    ServiceProvider provider = new ServiceCollection().AddLogging().AddFFGuardianAiSecurity().BuildServiceProvider();
    AiAnalysisResult result = await provider.GetRequiredService<IAiThreatAnalyzer>().AnalyzeAsync(new("missing.file"), cts.Token);
    Assert(result.IsCancelled || result.Score.Level == AiRiskLevel.Unavailable, "Cancellation not handled");
    await provider.DisposeAsync();
});

if (failures.Count > 0) { Console.Error.WriteLine(string.Join(Environment.NewLine, failures)); return 1; }
Console.WriteLine("FFGuardian AI Security tests passed.");
return 0;

async Task RunAsync(string name, Func<Task> test)
{
    try { await test(); Console.WriteLine($"PASS: {name}"); }
    catch (Exception ex) { failures.Add($"FAIL: {name}: {ex}"); }
}
static void Assert(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
