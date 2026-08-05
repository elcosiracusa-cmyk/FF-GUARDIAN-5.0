using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace FFGuardian.Security.Core;

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
    private static readonly string[] YaraCandidates =
    [
        "Engine/Yara/yara64.exe", "Engine/Yara/yara.exe",
        "Engines/Yara/yara64.exe", "Engines/Yara/yara.exe",
        "Tools/Yara/yara64.exe", "Tools/Yara/yara.exe"
    ];

    private static readonly string[] YaracCandidates =
    [
        "Engine/Yara/yarac64.exe", "Engine/Yara/yarac.exe",
        "Engines/Yara/yarac64.exe", "Engines/Yara/yarac.exe",
        "Tools/Yara/yarac64.exe", "Tools/Yara/yarac.exe"
    ];

    private readonly string _root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(options.Value.BaseDirectory));

    public Task<string?> LocateYaraAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Find(YaraCandidates));
    }

    public Task<string?> LocateYaraCompilerAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Find(YaracCandidates));
    }

    public Task<string?> LocateClamAvAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Find(["Engine/ClamAV/clamscan.exe", "ClamAV/clamscan.exe"]));
    }

    private string? Find(IEnumerable<string> relatives)
    {
        foreach (string relative in relatives)
        {
            string candidate = Path.GetFullPath(Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (IsInsideRoot(candidate) && File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private bool IsInsideRoot(string candidate) =>
        candidate.Equals(_root, StringComparison.OrdinalIgnoreCase) ||
        candidate.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}

public sealed class PathExclusionService(IOptions<SecurityCoreOptions> options) : IPathExclusionService
{
    private readonly string[] _protected = Build(options.Value);
    public bool ShouldExclude(string path)
    {
        try { string full = Path.GetFullPath(path); return _protected.Any(directory => IsInside(full, directory)); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
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
        return [Path.Combine(root, "Engine"), Path.Combine(root, "Engines"), Path.Combine(root, "Rules"), Path.Combine(root, "Database"), Path.Combine(data, "Quarantine"), Path.Combine(data, "Logs"), Path.Combine(data, "Temp"), Path.Combine(data, "Updates"), Path.Combine(data, "Backup")];
    }
}

public sealed class SecurityEventLogger(IOptions<SecurityCoreOptions> options) : ISecurityEventLogger, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _path = Path.Combine(options.Value.DataDirectory, "Logs", "security-core.jsonl");
    private readonly SemaphoreSlim _gate = new(1, 1);
    public async Task LogAsync(string componentName, string outcome, string message, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        string line = JsonSerializer.Serialize(new { timestamp = DateTimeOffset.UtcNow, componentName, outcome, message }, JsonOptions);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await File.AppendAllTextAsync(_path, line + Environment.NewLine, Encoding.UTF8, cancellationToken).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }
    public void Dispose() => _gate.Dispose();
}
