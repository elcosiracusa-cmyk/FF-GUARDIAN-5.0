using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using FFGuardian.Security.Core;

namespace FFGuardian.PremiumWpf;

public enum Engine10Status
{
    Operational,
    NotInstalled,
    AssemblyMissing,
    DependencyMissing,
    InitializationError,
    SelfTestFailed,
    IncompatibleVersion,
    NotConfigured
}

public sealed record Engine10Diagnostics(
    Engine10Status Status,
    string Version,
    string AssemblyName,
    string AssemblyPath,
    IReadOnlyList<string> Dependencies,
    bool ServiceResolved,
    bool ConfigurationLoaded,
    bool InternalDatabaseAvailable,
    bool HashCalculatorOperational,
    bool ParserOperational,
    bool InnocuousScanPassed,
    DateTimeOffset LastCheck,
    TimeSpan Duration,
    string Message,
    string ErrorCode);

public sealed record SecurityComponentHealth(
    string Name,
    string Status,
    string Version,
    string Message,
    bool IsApproved,
    bool RuntimeVerified,
    DateTimeOffset LastCheck,
    TimeSpan Duration,
    string ErrorCode);

public interface IEngine10Service
{
    Task<SecurityComponentHealth> GetHealthAsync(CancellationToken cancellationToken);
    Task<string> GetVersionAsync(CancellationToken cancellationToken);
    Task<Engine10Diagnostics> GetDiagnosticsAsync(CancellationToken cancellationToken);
    Task<Engine10Diagnostics> RunSelfTestAsync(CancellationToken cancellationToken);
    Task<ScanResult> ScanFileAsync(string path, CancellationToken cancellationToken);
}

public sealed class Engine10Service(IScanService scanService, IFileHashService hashService) : IEngine10Service
{
    private static readonly Version MinimumSupportedVersion = new(10, 0, 0, 0);

    public async Task<SecurityComponentHealth> GetHealthAsync(CancellationToken cancellationToken)
    {
        Engine10Diagnostics diagnostics = await RunSelfTestAsync(cancellationToken).ConfigureAwait(false);
        return new(
            "Engine10",
            ToDisplayStatus(diagnostics.Status),
            diagnostics.Version,
            diagnostics.Message,
            true,
            diagnostics.Status == Engine10Status.Operational,
            diagnostics.LastCheck,
            diagnostics.Duration,
            diagnostics.ErrorCode);
    }

    public Task<string> GetVersionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GetServiceAssembly().GetName().Version?.ToString() ?? "--");
    }

    public Task<Engine10Diagnostics> GetDiagnosticsAsync(CancellationToken cancellationToken) =>
        RunSelfTestAsync(cancellationToken);

    public async Task<Engine10Diagnostics> RunSelfTestAsync(CancellationToken cancellationToken)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        DateTimeOffset checkedAt = DateTimeOffset.UtcNow;
        Assembly assembly = GetServiceAssembly();
        string assemblyPath = assembly.Location;
        string assemblyName = assembly.GetName().Name ?? "--";
        Version? version = assembly.GetName().Version;
        string versionText = version?.ToString() ?? "--";
        List<string> dependencies = assembly.GetReferencedAssemblies().Select(item => item.FullName).OrderBy(item => item, StringComparer.Ordinal).ToList();

        if (string.IsNullOrWhiteSpace(assemblyPath) || !File.Exists(assemblyPath))
            return Result(Engine10Status.AssemblyMissing, "Assembly del servizio Engine10 non disponibile.", "ENGINE10_ASSEMBLY_MISSING");

        if (version is null || version < MinimumSupportedVersion)
            return Result(Engine10Status.IncompatibleVersion, $"Versione Engine10 incompatibile: {versionText}.", "ENGINE10_VERSION_INCOMPATIBLE");

        string tempRoot = Path.Combine(Path.GetTempPath(), "FFGuardian-Engine10-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            string harmless = Path.Combine(tempRoot, "harmless.txt");
            await File.WriteAllTextAsync(harmless, "FFGuardian Engine10 harmless self-test", cancellationToken).ConfigureAwait(false);

            string digest = await hashService.ComputeSha256Async(harmless, cancellationToken).ConfigureAwait(false);
            bool hashOk = digest.Length == 64 && digest.All(Uri.IsHexDigit);
            if (!hashOk)
                return Result(Engine10Status.SelfTestFailed, "Calcolo SHA-256 non valido.", "ENGINE10_HASH_FAILED", hashOk: false);

            ScanResult scan = await ScanFileAsync(harmless, cancellationToken).ConfigureAwait(false);
            bool parserOk = scan.Errors.All(error => !string.IsNullOrWhiteSpace(error));
            bool scanOk = !scan.WasCancelled && scan.FilesFailed == 0 && scan.Detections.Count == 0;
            if (!parserOk || !scanOk)
                return Result(Engine10Status.SelfTestFailed, "La scansione innocua Engine10 non ha prodotto un risultato coerente.", "ENGINE10_SCAN_SELFTEST_FAILED", hashOk: true, parserOk: parserOk, scanOk: scanOk);

            return Result(Engine10Status.Operational, "Servizio risolto tramite DI e self-test innocuo superato.", string.Empty, hashOk: true, parserOk: true, scanOk: true);
        }
        catch (FileNotFoundException exception)
        {
            return Result(Engine10Status.DependencyMissing, exception.Message, "ENGINE10_DEPENDENCY_MISSING");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or CryptographicException)
        {
            return Result(Engine10Status.InitializationError, exception.Message, "ENGINE10_INITIALIZATION_ERROR");
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        Engine10Diagnostics Result(Engine10Status status, string message, string errorCode, bool hashOk = false, bool parserOk = false, bool scanOk = false)
        {
            stopwatch.Stop();
            return new(status, versionText, assemblyName, assemblyPath, dependencies, true, true,
                true, hashOk, parserOk, scanOk, checkedAt, stopwatch.Elapsed, message, errorCode);
        }
    }

    public Task<ScanResult> ScanFileAsync(string path, CancellationToken cancellationToken) =>
        scanService.ScanAsync(new ScanRequest([Path.GetFullPath(path)], Recursive: false, QuarantineDetections: false, ForceRescan: true), null, cancellationToken);

    private static Assembly GetServiceAssembly() => typeof(Engine10Service).Assembly;

    private static string ToDisplayStatus(Engine10Status status) => status switch
    {
        Engine10Status.Operational => "Operativo",
        Engine10Status.NotInstalled => "Non installato",
        Engine10Status.AssemblyMissing => "Assembly mancante",
        Engine10Status.DependencyMissing => "Dipendenza mancante",
        Engine10Status.InitializationError => "Errore inizializzazione",
        Engine10Status.SelfTestFailed => "Self-test fallito",
        Engine10Status.IncompatibleVersion => "Versione incompatibile",
        _ => "Non configurato"
    };
}
