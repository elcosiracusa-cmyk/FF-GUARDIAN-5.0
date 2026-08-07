using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using FFGuardian.Security.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

return await CoreTests.RunAsync();

internal static class CoreTests
{
    private static readonly TimeSpan PhaseTimeout = TimeSpan.FromMinutes(5);

    public static async Task<int> RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "FFGuardian-CoreTests-" + Guid.NewGuid().ToString("N"));
        string app = Path.Combine(root, "App");
        string data = Path.Combine(root, "Data");
        Directory.CreateDirectory(app);
        Directory.CreateDirectory(data);
        try
        {
            await RunPhaseAsync("unified scan engine tests", UnifiedScanEngineTests.RunAsync);

            Assert(YaraService.Parse("RuleOne C:\\sample.txt\r\n", "C:\\sample.txt").Single().Rule == "RuleOne", "YARA parser");
            ClamAvDetection detection = ClamAvService.Parse("C:\\sample.txt: Win.Test FOUND\r\n").Single();
            Assert(detection.Signature == "Win.Test" && detection.TargetPath == "C:\\sample.txt", "ClamAV parser");

            ServiceCollection services = new();
            services.AddFFGuardianSecurityServices(options =>
            {
                options.BaseDirectory = app;
                options.DataDirectory = data;
                options.MaximumScanConcurrency = 4;
            });
            await using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
            IFileHashService hashes = provider.GetRequiredService<IFileHashService>();
            IPathExclusionService exclusions = provider.GetRequiredService<IPathExclusionService>();
            IQuarantineService quarantine = provider.GetRequiredService<IQuarantineService>();

            string hashFile = Path.Combine(root, "hash.txt");
            await File.WriteAllTextAsync(hashFile, "abc", Encoding.ASCII);
            string expected = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes("abc")));
            Assert(await hashes.ComputeSha256Async(hashFile, CancellationToken.None) == expected, "SHA-256");

            string engineFile = Path.Combine(app, "Engine", "Yara", "yara64.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(engineFile)!);
            await File.WriteAllTextAsync(engineFile, "test");
            Assert(exclusions.ShouldExclude(engineFile), "internal engine exclusion");
            Assert(!exclusions.ShouldExclude(Path.Combine(root, "FFGuardian-Malware.exe")), "external same-name remains scannable");
            Assert(!exclusions.IsInside(Path.Combine(app + "-malware", "x.exe"), app), "directory boundary");
            Assert(exclusions.IsInside(Path.Combine(app, "sub", "..", "file.dll"), app), "normalized path");

            string source = Path.Combine(root, "suspicious.exe");
            await File.WriteAllTextAsync(source, "harmless fixture");
            QuarantineResult stored = await quarantine.QuarantineAsync(source, "Fixture", "Test.Rule", "Low", CancellationToken.None);
            Assert(stored.Success && stored.Entry is not null, "quarantine store");
            QuarantineEntry entry = stored.Entry ?? throw new InvalidOperationException("Quarantine entry missing.");
            Assert(!File.Exists(source), "original removed only after verified copy");
            Assert(entry.StoredPath.EndsWith(".qdat", StringComparison.OrdinalIgnoreCase), "non executable stored extension");

            string duplicateSource = Path.Combine(root, "duplicate.exe");
            await File.WriteAllTextAsync(duplicateSource, "harmless fixture");
            QuarantineResult duplicateStored = await quarantine.QuarantineAsync(duplicateSource, "Fixture", "Test.Rule", "Low", CancellationToken.None);
            Assert(!duplicateStored.Success && duplicateStored.Message.Contains("già presente", StringComparison.OrdinalIgnoreCase), "quarantine duplicate prevention");
            Assert(File.Exists(duplicateSource), "duplicate source preserved");

            string restore = Path.Combine(root, "restored.bin");
            QuarantineResult restored = await quarantine.RestoreAsync(entry.Id, restore, false, CancellationToken.None);
            Assert(restored.Success && File.Exists(restore), "quarantine restore");
            Assert(await hashes.ComputeSha256Async(restore, CancellationToken.None) == entry.Sha256, "restored hash");
            QuarantineResult duplicateRestore = await quarantine.RestoreAsync(entry.Id, restore, false, CancellationToken.None);
            Assert(!duplicateRestore.Success, "existing destination blocked");
            Assert(await quarantine.DeleteAsync(entry.Id, CancellationToken.None), "quarantine delete");

            await RunPhaseAsync("incremental parallel scan", () => TestIncrementalParallelScanAsync(root, app, data, hashes, exclusions, quarantine));

            Console.WriteLine("PASS shared security core tests");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static async Task TestIncrementalParallelScanAsync(string root, string app, string data, IFileHashService hashes, IPathExclusionService exclusions, IQuarantineService quarantine)
    {
        string loadRoot = Path.Combine(root, "Load Test With Spaces");
        Directory.CreateDirectory(loadRoot);

        await RunPhaseAsync("create 1000 harmless fixtures", async () =>
        {
            ParallelOptions options = new() { MaxDegreeOfParallelism = 16 };
            await Parallel.ForEachAsync(Enumerable.Range(0, 1000), options, async (index, token) =>
            {
                string path = Path.Combine(loadRoot, $"fixture-{index:D4}.txt");
                await File.WriteAllTextAsync(path, "safe fixture " + index, Encoding.UTF8, token);
            });
        });

        SecurityCoreOptions settings = new()
        {
            BaseDirectory = app,
            DataDirectory = data,
            MaximumScanConcurrency = 4,
            MaximumFileSizeBytes = 1024 * 1024,
            EnableIncrementalScanCache = true
        };
        ScanCacheService cache = new(Options.Create(settings));
        try
        {
            FakeYaraService yara = new();
            FakeClamAvService clam = new();
            ScanService scanner = new(yara, clam, exclusions, quarantine, new NullSecurityLogger(), hashes, cache, Options.Create(settings));

            ScanResult first = await RunPhaseAsync("first scan 1000 files", () => scanner.ScanAsync(new([loadRoot]), null, CancellationToken.None));
            Assert(first.FilesScanned == 1000 && first.FilesSkipped == 0 && first.FilesFailed == 0, "parallel load scan 1000 files");
            Assert(yara.ScanCount == 1000 && clam.ScanCount == 1000, "both engines invoked");

            ScanResult incremental = await RunPhaseAsync("incremental cache scan 1000 files", () => scanner.ScanAsync(new([loadRoot]), null, CancellationToken.None));
            Assert(incremental.FilesScanned == 0 && incremental.FilesSkipped == 1000, "incremental scan cache");
            Assert(yara.ScanCount == 1000 && clam.ScanCount == 1000, "cached files avoid engine calls");

            string changed = Path.Combine(loadRoot, "fixture-0500.txt");
            await File.AppendAllTextAsync(changed, " changed", Encoding.UTF8);
            ScanResult differential = await RunPhaseAsync("differential scan", () => scanner.ScanAsync(new([loadRoot]), null, CancellationToken.None));
            Assert(differential.FilesScanned == 1 && differential.FilesSkipped == 999, "differential scan changed file only");

            string forcedRoot = Path.Combine(loadRoot, "forced-subset");
            Directory.CreateDirectory(forcedRoot);
            for (int index = 0; index < 20; index++)
                await File.WriteAllTextAsync(Path.Combine(forcedRoot, $"forced-{index:D2}.txt"), "safe forced fixture " + index, Encoding.UTF8);
            ScanResult forced = await RunPhaseAsync("forced rescan subset", () => scanner.ScanAsync(new([forcedRoot], ForceRescan: true), null, CancellationToken.None));
            Assert(forced.FilesScanned == 20, "forced rescan bypasses cache");

            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();
            ScanResult cancelled = await RunPhaseAsync("cancelled scan", () => scanner.ScanAsync(new([loadRoot], ForceRescan: true), null, cancellation.Token));
            Assert(cancelled.WasCancelled, "scan cancellation");
        }
        finally
        {
            cache.Dispose();
        }
    }

    private static async Task RunPhaseAsync(string name, Func<Task> action)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        Console.WriteLine($"START {name}");
        await action().WaitAsync(PhaseTimeout);
        Console.WriteLine($"PASS {name} ({stopwatch.Elapsed})");
    }

    private static async Task<T> RunPhaseAsync<T>(string name, Func<Task<T>> action)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        Console.WriteLine($"START {name}");
        T result = await action().WaitAsync(PhaseTimeout);
        Console.WriteLine($"PASS {name} ({stopwatch.Elapsed})");
        return result;
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + name);
        Console.WriteLine("PASS " + name);
    }
}

internal sealed class FakeYaraService : IYaraService
{
    private int _scanCount;
    public int ScanCount => Volatile.Read(ref _scanCount);
    public Task<EngineVersionInfo> GetVersionAsync(CancellationToken cancellationToken) => Task.FromResult(new EngineVersionInfo("YARA", "fixture", "test", true, "OK"));
    public Task<IReadOnlyList<YaraMatch>> ScanFileAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _scanCount);
        return Task.FromResult<IReadOnlyList<YaraMatch>>([]);
    }
    public Task<EngineHealthResult> RunSelfTestAsync(CancellationToken cancellationToken) => Task.FromResult(new EngineHealthResult("YARA", true, "test", "OK", DateTimeOffset.UtcNow, TimeSpan.Zero));
    public Task<YaraDiagnostics> GetDiagnosticsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
}

internal sealed class FakeClamAvService : IClamAvService
{
    private int _scanCount;
    public int ScanCount => Volatile.Read(ref _scanCount);
    public Task<EngineVersionInfo> GetVersionAsync(CancellationToken cancellationToken) => Task.FromResult(new EngineVersionInfo("ClamAV", "fixture", "test", true, "OK"));
    public Task<IReadOnlyList<ClamAvDetection>> ScanFileAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _scanCount);
        return Task.FromResult<IReadOnlyList<ClamAvDetection>>([]);
    }
    public Task<EngineHealthResult> RunSelfTestAsync(CancellationToken cancellationToken) => Task.FromResult(new EngineHealthResult("ClamAV", true, "test", "OK", DateTimeOffset.UtcNow, TimeSpan.Zero));
}

internal sealed class NullSecurityLogger : ISecurityEventLogger
{
    public Task LogAsync(string componentName, string outcome, string message, CancellationToken cancellationToken) => Task.CompletedTask;
}
