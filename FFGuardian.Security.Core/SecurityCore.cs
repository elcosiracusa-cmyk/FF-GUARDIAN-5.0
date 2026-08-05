using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FFGuardian.Security.Core;

public sealed class SecurityCoreOptions
{
    public string BaseDirectory { get; set; } = AppContext.BaseDirectory;
    public string DataDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FF Guardian");
    public TimeSpan ProcessTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

public sealed record ProcessRequest(string FileName, IReadOnlyList<string> Arguments, string WorkingDirectory, TimeSpan Timeout);
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError, bool TimedOut, TimeSpan Duration);
public sealed record EngineVersionInfo(string Name, string Path, string Version, bool Operational, string Message);
public sealed record YaraMatch(string Rule, string TargetPath, string RawOutput);
public sealed record ClamAvDetection(string Signature, string TargetPath, string RawOutput);
public sealed record ScanDetection(string Engine, string Name, string FilePath, string Details);
public sealed record ScanProgress(int FilesScanned, int FilesSkipped, string CurrentPath);
public sealed record ScanRequest(IReadOnlyCollection<string> Paths, bool Recursive = true, bool QuarantineDetections = false);
public sealed record ScanResult(DateTimeOffset StartTime, DateTimeOffset EndTime, int FilesScanned, int FilesSkipped, int FilesFailed, IReadOnlyList<ScanDetection> Detections, IReadOnlyList<string> EnginesUsed, bool WasCancelled, IReadOnlyList<string> Errors);
public sealed record EngineHealthResult(string Name, bool Operational, string Version, string Message, DateTimeOffset CheckedAt, TimeSpan Duration);
public sealed record QuarantineEntry(Guid Id, string OriginalName, string OriginalPath, string StoredPath, string Sha256, long Size, string Engine, string Detection, DateTimeOffset CreatedAt, string Risk);
public sealed record QuarantineResult(bool Success, QuarantineEntry? Entry, string Message);

public interface IProcessRunner { Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken); }
public interface IFileHashService { Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken); }
public interface IEngineLocatorService { Task<string?> LocateYaraAsync(CancellationToken cancellationToken); Task<string?> LocateClamAvAsync(CancellationToken cancellationToken); }
public interface IPathExclusionService { bool ShouldExclude(string path); bool IsInside(string candidate, string trustedDirectory); }
public interface ISecurityEventLogger { Task LogAsync(string module, string outcome, string message, CancellationToken cancellationToken); }
public interface IYaraService { Task<EngineVersionInfo> GetVersionAsync(CancellationToken cancellationToken); Task<IReadOnlyList<YaraMatch>> ScanFileAsync(string path, CancellationToken cancellationToken); Task<EngineHealthResult> RunSelfTestAsync(CancellationToken cancellationToken); }
public interface IClamAvService { Task<EngineVersionInfo> GetVersionAsync(CancellationToken cancellationToken); Task<IReadOnlyList<ClamAvDetection>> ScanFileAsync(string path, CancellationToken cancellationToken); Task<EngineHealthResult> RunSelfTestAsync(CancellationToken cancellationToken); }
public interface IFreshClamService { Task<EngineHealthResult> GetHealthAsync(CancellationToken cancellationToken); }
public interface IQuarantineService { Task<QuarantineResult> QuarantineAsync(string path, string engine, string detection, string risk, CancellationToken cancellationToken); Task<QuarantineResult> RestoreAsync(Guid id, string destinationPath, bool overwrite, CancellationToken cancellationToken); Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken); Task<IReadOnlyList<QuarantineEntry>> ListAsync(CancellationToken cancellationToken); }
public interface IScanService { Task<ScanResult> ScanAsync(ScanRequest request, IProgress<ScanProgress>? progress, CancellationToken cancellationToken); }
public interface IAntivirusHealthService { Task<IReadOnlyList<EngineHealthResult>> CheckAsync(CancellationToken cancellationToken); }

public sealed class SecureProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        ProcessStartInfo start = new()
        {
            FileName = request.FileName,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (string argument in request.Arguments) start.ArgumentList.Add(argument);
        using Process process = new() { StartInfo = start };
        Stopwatch stopwatch = Stopwatch.StartNew();
        if (!process.Start()) throw new InvalidOperationException($"Impossibile avviare {request.FileName}.");
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(request.Timeout);
        bool timedOut = false;
        try { await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            timedOut = true;
            TryKill(process);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
        string stdout = await stdoutTask.ConfigureAwait(false);
        string stderr = await stderrTask.ConfigureAwait(false);
        stopwatch.Stop();
        return new(timedOut ? -1 : process.ExitCode, stdout, stderr, timedOut, stopwatch.Elapsed);
    }

    private static void TryKill(Process process)
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }
}

public sealed class FileHashService : IFileHashService
{
    public async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
    }
}

public sealed class EngineLocatorService(IOptions<SecurityCoreOptions> options) : IEngineLocatorService
{
    private readonly string _root = Path.GetFullPath(options.Value.BaseDirectory);
    public Task<string?> LocateYaraAsync(CancellationToken cancellationToken) => Task.FromResult(Find("Engine/Yara/yara64.exe", "Engine/Yara/yara.exe", "Tools/Yara/yara64.exe", "Tools/Yara/yara.exe"));
    public Task<string?> LocateClamAvAsync(CancellationToken cancellationToken) => Task.FromResult(Find("Engine/ClamAV/clamscan.exe", "ClamAV/clamscan.exe"));
    private string? Find(params string[] relatives)
    {
        foreach (string relative in relatives)
        {
            string candidate = Path.GetFullPath(Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (candidate.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) && File.Exists(candidate)) return candidate;
        }
        return null;
    }
}

public sealed class PathExclusionService(IOptions<SecurityCoreOptions> options) : IPathExclusionService
{
    private readonly string[] _protected = Build(options.Value);
    public bool ShouldExclude(string path)
    {
        string full;
        try { full = Path.GetFullPath(path); } catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
        return _protected.Any(directory => IsInside(full, directory));
    }
    public bool IsInside(string candidate, string trustedDirectory)
    {
        try
        {
            string file = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
            string directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(trustedDirectory));
            return file.Equals(directory, StringComparison.OrdinalIgnoreCase) || file.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
    }
    private static string[] Build(SecurityCoreOptions value)
    {
        string root = Path.GetFullPath(value.BaseDirectory);
        string data = Path.GetFullPath(value.DataDirectory);
        return [Path.Combine(root, "Engine"), Path.Combine(root, "Rules"), Path.Combine(root, "Database"), Path.Combine(data, "Quarantine"), Path.Combine(data, "Logs"), Path.Combine(data, "Temp"), Path.Combine(data, "Updates"), Path.Combine(data, "Backup")];
    }
}

public sealed class SecurityEventLogger(IOptions<SecurityCoreOptions> options) : ISecurityEventLogger
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _path = Path.Combine(options.Value.DataDirectory, "Logs", "security-core.jsonl");
    private readonly SemaphoreSlim _gate = new(1, 1);
    public async Task LogAsync(string module, string outcome, string message, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        string line = JsonSerializer.Serialize(new { timestamp = DateTimeOffset.UtcNow, module, outcome, message }, JsonOptions);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await File.AppendAllTextAsync(_path, line + Environment.NewLine, Encoding.UTF8, cancellationToken).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }
}

public sealed class YaraService(IEngineLocatorService locator, IProcessRunner runner, IOptions<SecurityCoreOptions> options) : IYaraService
{
    private const string RuleName = "FFGuardian_Runtime_SelfTest";
    private const string Marker = "FFGUARDIAN_YARA_TEST_STRING";
    public async Task<EngineVersionInfo> GetVersionAsync(CancellationToken cancellationToken)
    {
        string? executable = await locator.LocateYaraAsync(cancellationToken).ConfigureAwait(false);
        if (executable is null) return new("YARA", string.Empty, "--", false, "Eseguibile non trovato.");
        ProcessResult result = await runner.RunAsync(new(executable, ["--version"], Path.GetDirectoryName(executable)!, options.Value.ProcessTimeout), cancellationToken).ConfigureAwait(false);
        string version = FirstLine(result.StandardOutput, result.StandardError);
        return new("YARA", executable, version, !result.TimedOut && result.ExitCode == 0, result.TimedOut ? "Timeout." : result.ExitCode == 0 ? "Versione verificata." : result.StandardError);
    }
    public async Task<IReadOnlyList<YaraMatch>> ScanFileAsync(string path, CancellationToken cancellationToken)
    {
        string? executable = await locator.LocateYaraAsync(cancellationToken).ConfigureAwait(false) ?? throw new FileNotFoundException("YARA non trovato.");
        string rules = Path.Combine(options.Value.BaseDirectory, "Rules");
        string? rule = Directory.Exists(rules) ? Directory.EnumerateFiles(rules, "*.yar", SearchOption.AllDirectories).Concat(Directory.EnumerateFiles(rules, "*.yara", SearchOption.AllDirectories)).FirstOrDefault() : null;
        if (rule is null) throw new FileNotFoundException("Nessuna regola YARA disponibile.");
        ProcessResult result = await runner.RunAsync(new(executable, [rule, Path.GetFullPath(path)], Path.GetDirectoryName(executable)!, options.Value.ProcessTimeout), cancellationToken).ConfigureAwait(false);
        if (result.TimedOut || result.ExitCode is not 0 and not 1) throw new InvalidOperationException(result.StandardError);
        return Parse(result.StandardOutput, path);
    }
    public async Task<EngineHealthResult> RunSelfTestAsync(CancellationToken cancellationToken)
    {
        Stopwatch sw = Stopwatch.StartNew();
        string? executable = await locator.LocateYaraAsync(cancellationToken).ConfigureAwait(false);
        if (executable is null) return new("YARA", false, "--", "Eseguibile non trovato.", DateTimeOffset.UtcNow, sw.Elapsed);
        string root = Path.Combine(Path.GetTempPath(), "FFGuardian-Yara-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string rule = Path.Combine(root, "selftest.yar");
            string sample = Path.Combine(root, "sample.txt");
            await File.WriteAllTextAsync(rule, $"rule {RuleName} {{ strings: $a = \"{Marker}\" ascii condition: $a }}", cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(sample, Marker, cancellationToken).ConfigureAwait(false);
            ProcessResult result = await runner.RunAsync(new(executable, [rule, sample], Path.GetDirectoryName(executable)!, options.Value.ProcessTimeout), cancellationToken).ConfigureAwait(false);
            bool ok = !result.TimedOut && result.ExitCode == 0 && result.StandardOutput.Contains(RuleName, StringComparison.Ordinal);
            sw.Stop();
            return new("YARA", ok, (await GetVersionAsync(cancellationToken).ConfigureAwait(false)).Version, ok ? "Regola innocua rilevata realmente." : result.StandardError + result.StandardOutput, DateTimeOffset.UtcNow, sw.Elapsed);
        }
        finally { TryDelete(root); }
    }
    internal static IReadOnlyList<YaraMatch> Parse(string output, string target) => output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim()).Where(line => line.Length > 0).Select(line => new YaraMatch(line.Split(' ', 2)[0], target, line)).ToArray();
    private static string FirstLine(params string[] values) => values.SelectMany(v => v.Split(['\r','\n'], StringSplitOptions.RemoveEmptyEntries)).Select(v => v.Trim()).FirstOrDefault(v => v.Length > 0) ?? "--";
    private static void TryDelete(string root) { try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
}

public sealed class ClamAvService(IEngineLocatorService locator, IProcessRunner runner, IOptions<SecurityCoreOptions> options) : IClamAvService
{
    private const string Eicar = "X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*";
    public async Task<EngineVersionInfo> GetVersionAsync(CancellationToken cancellationToken)
    {
        string? executable = await locator.LocateClamAvAsync(cancellationToken).ConfigureAwait(false);
        if (executable is null) return new("ClamAV", string.Empty, "--", false, "Eseguibile non trovato.");
        ProcessResult result = await runner.RunAsync(new(executable, ["--version"], Path.GetDirectoryName(executable)!, options.Value.ProcessTimeout), cancellationToken).ConfigureAwait(false);
        string version = result.StandardOutput.Split(['\r','\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "--";
        return new("ClamAV", executable, version, !result.TimedOut && result.ExitCode == 0, result.ExitCode == 0 ? "Versione verificata." : result.StandardError);
    }
    public async Task<IReadOnlyList<ClamAvDetection>> ScanFileAsync(string path, CancellationToken cancellationToken)
    {
        string? executable = await locator.LocateClamAvAsync(cancellationToken).ConfigureAwait(false) ?? throw new FileNotFoundException("ClamAV non trovato.");
        ProcessResult result = await runner.RunAsync(new(executable, ["--no-summary", Path.GetFullPath(path)], Path.GetDirectoryName(executable)!, options.Value.ProcessTimeout), cancellationToken).ConfigureAwait(false);
        if (result.TimedOut || result.ExitCode is not 0 and not 1) throw new InvalidOperationException(result.StandardError);
        return Parse(result.StandardOutput);
    }
    public async Task<EngineHealthResult> RunSelfTestAsync(CancellationToken cancellationToken)
    {
        Stopwatch sw = Stopwatch.StartNew();
        EngineVersionInfo version = await GetVersionAsync(cancellationToken).ConfigureAwait(false);
        if (!version.Operational) return new("ClamAV", false, version.Version, version.Message, DateTimeOffset.UtcNow, sw.Elapsed);
        string root = Path.Combine(Path.GetTempPath(), "FFGuardian-ClamAV-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string clean = Path.Combine(root, "clean.txt");
            string eicar = Path.Combine(root, "eicar.txt");
            await File.WriteAllTextAsync(clean, "FFGuardian harmless test", cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(eicar, Eicar, Encoding.ASCII, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<ClamAvDetection> cleanResult = await ScanFileAsync(clean, cancellationToken).ConfigureAwait(false);
            IReadOnlyList<ClamAvDetection> eicarResult = await ScanFileAsync(eicar, cancellationToken).ConfigureAwait(false);
            bool ok = cleanResult.Count == 0 && eicarResult.Any(x => x.Signature.Contains("Eicar", StringComparison.OrdinalIgnoreCase));
            sw.Stop();
            return new("ClamAV", ok, version.Version, ok ? "File innocuo pulito ed EICAR rilevato realmente." : "Self-test ClamAV non superato.", DateTimeOffset.UtcNow, sw.Elapsed);
        }
        finally { TryDelete(root); }
    }
    internal static IReadOnlyList<ClamAvDetection> Parse(string output)
    {
        List<ClamAvDetection> detections = [];
        foreach (string line in output.Split(['\r','\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            int found = line.LastIndexOf(" FOUND", StringComparison.OrdinalIgnoreCase);
            int colon = line.IndexOf(':');
            if (found > colon && colon >= 0) detections.Add(new(line[(colon + 1)..found].Trim(), line[..colon].Trim(), line));
        }
        return detections;
    }
    private static void TryDelete(string root) { try { Directory.Delete(root, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
}

public sealed class FreshClamService(IOptions<SecurityCoreOptions> options, IProcessRunner runner) : IFreshClamService
{
    public async Task<EngineHealthResult> GetHealthAsync(CancellationToken cancellationToken)
    {
        Stopwatch sw = Stopwatch.StartNew();
        string executable = Path.Combine(options.Value.BaseDirectory, "Engine", "ClamAV", "freshclam.exe");
        if (!File.Exists(executable)) return new("FreshClam", false, "--", "Eseguibile non trovato.", DateTimeOffset.UtcNow, sw.Elapsed);
        ProcessResult result = await runner.RunAsync(new(executable, ["--version"], Path.GetDirectoryName(executable)!, options.Value.ProcessTimeout), cancellationToken).ConfigureAwait(false);
        sw.Stop();
        return new("FreshClam", !result.TimedOut && result.ExitCode == 0, result.StandardOutput.Trim(), result.ExitCode == 0 ? "Versione verificata; aggiornamento rete non eseguito." : result.StandardError, DateTimeOffset.UtcNow, sw.Elapsed);
    }
}

public sealed class QuarantineService(IOptions<SecurityCoreOptions> options, IFileHashService hashes, ISecurityEventLogger logger) : IQuarantineService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _root = Path.Combine(options.Value.DataDirectory, "Quarantine");
    private readonly SemaphoreSlim _gate = new(1, 1);
    public async Task<QuarantineResult> QuarantineAsync(string path, string engine, string detection, string risk, CancellationToken cancellationToken)
    {
        string source = Path.GetFullPath(path);
        if (!File.Exists(source)) return new(false, null, "File sorgente assente.");
        Directory.CreateDirectory(_root);
        Guid id = Guid.NewGuid();
        string stored = Path.Combine(_root, id.ToString("N") + ".qdat");
        string metadata = Path.Combine(_root, id.ToString("N") + ".json");
        string hash = await hashes.ComputeSha256Async(source, cancellationToken).ConfigureAwait(false);
        FileInfo info = new(source);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using (FileStream input = new(source, FileMode.Open, FileAccess.Read, FileShare.Read))
            await using (FileStream output = new(stored, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
                await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
            string copiedHash = await hashes.ComputeSha256Async(stored, cancellationToken).ConfigureAwait(false);
            if (!hash.Equals(copiedHash, StringComparison.OrdinalIgnoreCase)) { File.Delete(stored); return new(false, null, "Verifica copia quarantena fallita."); }
            QuarantineEntry entry = new(id, info.Name, source, stored, hash, info.Length, engine, detection, DateTimeOffset.UtcNow, risk);
            await File.WriteAllTextAsync(metadata, JsonSerializer.Serialize(entry, JsonOptions), cancellationToken).ConfigureAwait(false);
            File.Delete(source);
            await logger.LogAsync("Quarantine", "Stored", id.ToString(), cancellationToken).ConfigureAwait(false);
            return new(true, entry, "File isolato.");
        }
        catch { TryDelete(stored); TryDelete(metadata); throw; }
        finally { _gate.Release(); }
    }
    public async Task<QuarantineResult> RestoreAsync(Guid id, string destinationPath, bool overwrite, CancellationToken cancellationToken)
    {
        QuarantineEntry? entry = await GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (entry is null || !File.Exists(entry.StoredPath)) return new(false, entry, "Elemento non trovato.");
        string destination = Path.GetFullPath(destinationPath);
        if (File.Exists(destination) && !overwrite) return new(false, entry, "Destinazione già esistente.");
        if (!entry.Sha256.Equals(await hashes.ComputeSha256Async(entry.StoredPath, cancellationToken).ConfigureAwait(false), StringComparison.OrdinalIgnoreCase)) return new(false, entry, "Hash quarantena non valido.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(entry.StoredPath, destination, overwrite);
        if (!entry.Sha256.Equals(await hashes.ComputeSha256Async(destination, cancellationToken).ConfigureAwait(false), StringComparison.OrdinalIgnoreCase)) { TryDelete(destination); return new(false, entry, "Verifica ripristino fallita."); }
        await logger.LogAsync("Quarantine", "Restored", id.ToString(), cancellationToken).ConfigureAwait(false);
        return new(true, entry, "File ripristinato; la UI deve avere acquisito conferma esplicita.");
    }
    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        QuarantineEntry? entry = await GetAsync(id, cancellationToken).ConfigureAwait(false);
        if (entry is null) return false;
        TryDelete(entry.StoredPath); TryDelete(Path.Combine(_root, id.ToString("N") + ".json"));
        await logger.LogAsync("Quarantine", "Deleted", id.ToString(), cancellationToken).ConfigureAwait(false);
        return true;
    }
    public async Task<IReadOnlyList<QuarantineEntry>> ListAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_root)) return [];
        List<QuarantineEntry> entries = [];
        foreach (string file in Directory.EnumerateFiles(_root, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { QuarantineEntry? entry = JsonSerializer.Deserialize<QuarantineEntry>(await File.ReadAllTextAsync(file, cancellationToken).ConfigureAwait(false), JsonOptions); if (entry is not null) entries.Add(entry); }
            catch (JsonException) { }
        }
        return entries.OrderByDescending(x => x.CreatedAt).ToArray();
    }
    private async Task<QuarantineEntry?> GetAsync(Guid id, CancellationToken token)
    {
        string metadata = Path.Combine(_root, id.ToString("N") + ".json");
        if (!File.Exists(metadata)) return null;
        return JsonSerializer.Deserialize<QuarantineEntry>(await File.ReadAllTextAsync(metadata, token).ConfigureAwait(false), JsonOptions);
    }
    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
}

public sealed class ScanService(IYaraService yara, IClamAvService clam, IPathExclusionService exclusions, IQuarantineService quarantine, ISecurityEventLogger logger) : IScanService
{
    public async Task<ScanResult> ScanAsync(ScanRequest request, IProgress<ScanProgress>? progress, CancellationToken cancellationToken)
    {
        DateTimeOffset start = DateTimeOffset.UtcNow;
        int scanned = 0, skipped = 0, failed = 0;
        List<ScanDetection> detections = [];
        List<string> errors = [];
        HashSet<string> engines = new(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (string file in EnumerateFiles(request))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (exclusions.ShouldExclude(file)) { skipped++; continue; }
                try
                {
                    IReadOnlyList<YaraMatch> ym = await yara.ScanFileAsync(file, cancellationToken).ConfigureAwait(false); engines.Add("YARA");
                    detections.AddRange(ym.Select(x => new ScanDetection("YARA", x.Rule, file, x.RawOutput)));
                    IReadOnlyList<ClamAvDetection> cm = await clam.ScanFileAsync(file, cancellationToken).ConfigureAwait(false); engines.Add("ClamAV");
                    detections.AddRange(cm.Select(x => new ScanDetection("ClamAV", x.Signature, file, x.RawOutput)));
                    scanned++;
                    progress?.Report(new(scanned, skipped, file));
                    if (request.QuarantineDetections)
                        foreach (ScanDetection detection in detections.Where(x => x.FilePath.Equals(file, StringComparison.OrdinalIgnoreCase)).Take(1))
                            await quarantine.QuarantineAsync(file, detection.Engine, detection.Name, "High", cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { failed++; errors.Add($"{file}: {ex.Message}"); }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new(start, DateTimeOffset.UtcNow, scanned, skipped, failed, detections, engines.ToArray(), true, errors);
        }
        await logger.LogAsync("Scan", "Completed", $"scanned={scanned}; detections={detections.Count}", cancellationToken).ConfigureAwait(false);
        return new(start, DateTimeOffset.UtcNow, scanned, skipped, failed, detections, engines.ToArray(), false, errors);
    }
    private static IEnumerable<string> EnumerateFiles(ScanRequest request)
    {
        foreach (string path in request.Paths)
        {
            string full = Path.GetFullPath(path);
            if (File.Exists(full)) yield return full;
            else if (Directory.Exists(full))
                foreach (string file in Directory.EnumerateFiles(full, "*", request.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)) yield return file;
        }
    }
}

public sealed class AntivirusHealthService(IYaraService yara, IClamAvService clam, IFreshClamService freshClam) : IAntivirusHealthService
{
    public async Task<IReadOnlyList<EngineHealthResult>> CheckAsync(CancellationToken cancellationToken) =>
        [await yara.RunSelfTestAsync(cancellationToken).ConfigureAwait(false), await clam.RunSelfTestAsync(cancellationToken).ConfigureAwait(false), await freshClam.GetHealthAsync(cancellationToken).ConfigureAwait(false)];
}

public static class SecurityServiceCollectionExtensions
{
    public static IServiceCollection AddFFGuardianSecurityServices(this IServiceCollection services, Action<SecurityCoreOptions>? configure = null)
    {
        services.AddOptions<SecurityCoreOptions>();
        if (configure is not null) services.Configure(configure);
        services.AddSingleton<IProcessRunner, SecureProcessRunner>();
        services.AddSingleton<IFileHashService, FileHashService>();
        services.AddSingleton<IEngineLocatorService, EngineLocatorService>();
        services.AddSingleton<IPathExclusionService, PathExclusionService>();
        services.AddSingleton<ISecurityEventLogger, SecurityEventLogger>();
        services.AddSingleton<IYaraService, YaraService>();
        services.AddSingleton<IClamAvService, ClamAvService>();
        services.AddSingleton<IFreshClamService, FreshClamService>();
        services.AddSingleton<IQuarantineService, QuarantineService>();
        services.AddSingleton<IScanService, ScanService>();
        services.AddSingleton<IAntivirusHealthService, AntivirusHealthService>();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        return services;
    }
}
