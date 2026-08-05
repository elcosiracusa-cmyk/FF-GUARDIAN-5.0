using System.Runtime.CompilerServices;
using FFGuardian.Security.Core;
using Microsoft.Extensions.Options;

internal static class UnifiedScanEngineTests
{
    [ModuleInitializer]
    public static void Initialize() => RunAsync().GetAwaiter().GetResult();

    private static async Task RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "FFGuardian-UnifiedScanTests-" + Guid.NewGuid().ToString("N"));
        string app = Path.Combine(root, "App");
        string data = Path.Combine(root, "Data");
        string quick = Path.Combine(root, "Quick Area");
        string full = Path.Combine(root, "Full Area");
        string custom = Path.Combine(root, "Custom Area With Spaces");
        Directory.CreateDirectory(app);
        Directory.CreateDirectory(data);
        Directory.CreateDirectory(quick);
        Directory.CreateDirectory(full);
        Directory.CreateDirectory(custom);
        try
        {
            await CreateFixturesAsync(quick, 3);
            await CreateFixturesAsync(full, 4);
            await CreateFixturesAsync(custom, 5);
            string empty = Path.Combine(root, "Empty");
            Directory.CreateDirectory(empty);

            SecurityCoreOptions options = new()
            {
                BaseDirectory = app,
                DataDirectory = data,
                EnableIncrementalScanCache = false,
                MaximumScanConcurrency = 2,
                IncludeDefaultQuickScanLocations = false,
                IncludeRunningProcessesInQuickScan = false,
                QuickScanDirectories = [quick],
                FullScanRootDirectories = [full]
            };
            PathExclusionService exclusions = new(Options.Create(options));
            FileHashService hashes = new();
            TestQuarantineService quarantine = new();
            TestLogger logger = new();
            ScanCacheService cache = new(Options.Create(options));
            try
            {
                DelayedYaraService yara = new(TimeSpan.Zero);
                DelayedClamService clam = new(TimeSpan.Zero);
                ScanService scanner = new(yara, clam, exclusions, quarantine, logger, hashes, cache, Options.Create(options));
                using UnifiedScanService unified = new(scanner, exclusions, logger, Options.Create(options));
                CapturingProgress progress = new();

                ScanResult quickResult = await unified.ScanQuickAsync(progress, CancellationToken.None);
                Assert(quickResult.FilesScanned == 3, "quick scan files");
                Assert(unified.GetStatus().Mode == ScanMode.Quick && unified.GetStatus().State == ScanState.Completed, "quick scan status");
                Assert(progress.Last is not null && progress.Last.TotalFiles == 3 && progress.Last.Percentage == 100, "quick scan real progress");

                progress.Reset();
                ScanResult fullResult = await unified.ScanFullAsync(progress, CancellationToken.None);
                Assert(fullResult.FilesScanned == 4, "full scan configured root");
                Assert(unified.GetStatus().Mode == ScanMode.Full && unified.GetStatus().State == ScanState.Completed, "full scan status");

                progress.Reset();
                ScanResult customResult = await unified.ScanCustomAsync(custom, progress, CancellationToken.None);
                Assert(customResult.FilesScanned == 5, "custom scan path with spaces");
                Assert(progress.Last is not null && progress.Last.TotalFiles == 5, "custom scan total");

                ScanResult emptyResult = await unified.ScanCustomAsync(empty, null, CancellationToken.None);
                Assert(emptyResult.FilesScanned == 0 && emptyResult.FilesFailed == 0, "empty directory scan");

                string protectedFile = Path.Combine(data, "Quarantine", "isolated.qdat");
                Directory.CreateDirectory(Path.GetDirectoryName(protectedFile)!);
                await File.WriteAllTextAsync(protectedFile, "fixture");
                ScanResult protectedResult = await unified.ScanCustomAsync(protectedFile, null, CancellationToken.None);
                Assert(protectedResult.FilesSkipped == 1 && protectedResult.FilesScanned == 0, "protected internal file exclusion");
            }
            finally
            {
                cache.Dispose();
            }

            string cancellationRoot = Path.Combine(root, "Cancellation");
            Directory.CreateDirectory(cancellationRoot);
            await CreateFixturesAsync(cancellationRoot, 200);
            SecurityCoreOptions cancellationOptions = new()
            {
                BaseDirectory = app,
                DataDirectory = Path.Combine(root, "CancellationData"),
                EnableIncrementalScanCache = false,
                MaximumScanConcurrency = 1,
                IncludeDefaultQuickScanLocations = false,
                IncludeRunningProcessesInQuickScan = false,
                QuickScanDirectories = [cancellationRoot]
            };
            PathExclusionService cancellationExclusions = new(Options.Create(cancellationOptions));
            ScanCacheService cancellationCache = new(Options.Create(cancellationOptions));
            try
            {
                ScanService slowScanner = new(new DelayedYaraService(TimeSpan.FromMilliseconds(15)), new DelayedClamService(TimeSpan.FromMilliseconds(15)), cancellationExclusions, new TestQuarantineService(), new TestLogger(), new FileHashService(), cancellationCache, Options.Create(cancellationOptions));
                using UnifiedScanService cancellable = new(slowScanner, cancellationExclusions, new TestLogger(), Options.Create(cancellationOptions));
                Task<ScanResult> running = cancellable.ScanQuickAsync(null, CancellationToken.None);
                await Task.Delay(75);
                await cancellable.CancelAsync();
                ScanResult cancelled = await running;
                Assert(cancelled.WasCancelled, "unified cancellation result");
                Assert(cancellable.GetStatus().State == ScanState.Cancelled, "unified cancellation status");
            }
            finally
            {
                cancellationCache.Dispose();
            }

            Console.WriteLine("PASS unified scan engine tests");
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static async Task CreateFixturesAsync(string directory, int count)
    {
        for (int index = 0; index < count; index++)
            await File.WriteAllTextAsync(Path.Combine(directory, $"file-{index:D4}.txt"), "innocuous fixture " + index);
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + name);
        Console.WriteLine("PASS " + name);
    }

    private sealed class CapturingProgress : IProgress<ScanProgress>
    {
        public ScanProgress? Last { get; private set; }
        public void Report(ScanProgress value) => Last = value;
        public void Reset() => Last = null;
    }

    private sealed class DelayedYaraService(TimeSpan delay) : IYaraService
    {
        public Task<EngineVersionInfo> GetVersionAsync(CancellationToken cancellationToken) => Task.FromResult(new EngineVersionInfo("YARA", "fixture", "test", true, "OK"));
        public async Task<IReadOnlyList<YaraMatch>> ScanFileAsync(string path, CancellationToken cancellationToken)
        {
            if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken);
            return [];
        }
        public Task<EngineHealthResult> RunSelfTestAsync(CancellationToken cancellationToken) => Task.FromResult(new EngineHealthResult("YARA", true, "test", "OK", DateTimeOffset.UtcNow, TimeSpan.Zero));
        public Task<YaraDiagnostics> GetDiagnosticsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class DelayedClamService(TimeSpan delay) : IClamAvService
    {
        public Task<EngineVersionInfo> GetVersionAsync(CancellationToken cancellationToken) => Task.FromResult(new EngineVersionInfo("ClamAV", "fixture", "test", true, "OK"));
        public async Task<IReadOnlyList<ClamAvDetection>> ScanFileAsync(string path, CancellationToken cancellationToken)
        {
            if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken);
            return [];
        }
        public Task<EngineHealthResult> RunSelfTestAsync(CancellationToken cancellationToken) => Task.FromResult(new EngineHealthResult("ClamAV", true, "test", "OK", DateTimeOffset.UtcNow, TimeSpan.Zero));
    }

    private sealed class TestQuarantineService : IQuarantineService
    {
        public Task<QuarantineResult> QuarantineAsync(string path, string engine, string detection, string risk, CancellationToken cancellationToken) => Task.FromResult(new QuarantineResult(true, null, "OK"));
        public Task<QuarantineResult> RestoreAsync(Guid id, string destinationPath, bool overwrite, CancellationToken cancellationToken) => Task.FromResult(new QuarantineResult(false, null, "Not used"));
        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<IReadOnlyList<QuarantineEntry>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<QuarantineEntry>>([]);
    }

    private sealed class TestLogger : ISecurityEventLogger
    {
        public Task LogAsync(string componentName, string outcome, string message, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
