using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace FFGuardian;

internal enum InternalPathCategory
{
    None, ApplicationBinary, Engine, Rules, Database, Quarantine, Logs, Temp,
    Updates, Backup, Cache, Reports
}

internal enum IntegrityState { Intact, Modified, Missing, Unknown, InvalidSignature, ManifestUnavailable }

internal sealed record InternalFileManifestEntry(
    string RelativePath, string Sha256, long Size, string Component, string Version,
    bool Required, bool DigitalSignatureRequired);

internal sealed record InternalFileManifest(string Product, string Version,
    DateTime GeneratedUtc, IReadOnlyList<InternalFileManifestEntry> Files);

internal sealed record IntegrityFileResult(string RelativePath, IntegrityState State,
    string Component, string Detail, long? ActualSize, string? ActualSha256);

internal sealed record IntegrityReport(DateTime CheckedUtc, bool ManifestVerified,
    IReadOnlyList<IntegrityFileResult> Files)
{
    public int Intact => Files.Count(x => x.State == IntegrityState.Intact);
    public int Modified => Files.Count(x => x.State is IntegrityState.Modified or IntegrityState.InvalidSignature);
    public int Missing => Files.Count(x => x.State == IntegrityState.Missing);
    public int Unknown => Files.Count(x => x.State == IntegrityState.Unknown);
    public string OverallState => !ManifestVerified ? "MANIFESTO NON VERIFICATO" :
        Modified > 0 ? "MODIFICATO" : Missing > 0 ? "INCOMPLETO" : Unknown > 0 ? "DA VERIFICARE" : "INTEGRO";
}

internal sealed class FFGuardianInternalPathLayout
{
    public string InstallationDirectory { get; }
    public string EngineDirectory { get; }
    public string ClamAvDirectory { get; }
    public string YaraDirectory { get; }
    public string RulesDirectory { get; }
    public string DatabaseDirectory { get; }
    public string QuarantineDirectory { get; }
    public string LogsDirectory { get; }
    public string TempDirectory { get; }
    public string UpdatesDirectory { get; }
    public string BackupDirectory { get; }
    public string CacheDirectory { get; }
    public string ReportsDirectory { get; }
    public string ManifestPath { get; }
    public string ManifestSignaturePath { get; }

    private FFGuardianInternalPathLayout(string installation, string dataRoot)
    {
        InstallationDirectory = NormalizeDirectory(installation);
        EngineDirectory = NormalizeDirectory(Path.Combine(installation, "Engine"));
        ClamAvDirectory = NormalizeDirectory(Path.Combine(EngineDirectory, "ClamAV"));
        YaraDirectory = NormalizeDirectory(Path.Combine(EngineDirectory, "Yara"));
        RulesDirectory = NormalizeDirectory(Path.Combine(installation, "Rules"));
        DatabaseDirectory = NormalizeDirectory(Path.Combine(installation, "Database"));
        QuarantineDirectory = NormalizeDirectory(Path.Combine(dataRoot, "Quarantine"));
        LogsDirectory = NormalizeDirectory(Path.Combine(dataRoot, "Logs"));
        TempDirectory = NormalizeDirectory(Path.Combine(dataRoot, "Temp"));
        UpdatesDirectory = NormalizeDirectory(Path.Combine(dataRoot, "Updates"));
        BackupDirectory = NormalizeDirectory(Path.Combine(dataRoot, "Backup"));
        CacheDirectory = NormalizeDirectory(Path.Combine(dataRoot, "Cache"));
        ReportsDirectory = NormalizeDirectory(Path.Combine(dataRoot, "Reports"));
        ManifestPath = Path.Combine(installation, "Assets", "ffguardian-files-manifest.json");
        ManifestSignaturePath = Path.Combine(installation, "Assets", "ffguardian-files-manifest.sig");
    }

    public static FFGuardianInternalPathLayout CreateDefault()
    {
        string installation = AppContext.BaseDirectory;
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string dataRoot = Path.Combine(local, "FF Guardian");
        return new FFGuardianInternalPathLayout(installation, dataRoot);
    }

    public IReadOnlyCollection<string> ProtectedOperationalDirectories =>
    [EngineDirectory, RulesDirectory, DatabaseDirectory, QuarantineDirectory, LogsDirectory,
     TempDirectory, UpdatesDirectory, BackupDirectory, CacheDirectory, ReportsDirectory];

    private static string NormalizeDirectory(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
}

internal sealed class FFGuardianManifestVerifier
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly FFGuardianInternalPathLayout _layout;
    private readonly string? _publicKeyPem;

    public FFGuardianManifestVerifier(FFGuardianInternalPathLayout layout, string? publicKeyPem = null)
    {
        _layout = layout;
        _publicKeyPem = publicKeyPem ?? Environment.GetEnvironmentVariable("FFGUARDIAN_FILES_MANIFEST_PUBLIC_KEY");
    }

    public bool TryLoadVerified(out InternalFileManifest? manifest, out string detail)
    {
        manifest = null;
        try
        {
            if (!File.Exists(_layout.ManifestPath) || !File.Exists(_layout.ManifestSignaturePath))
            { detail = "Manifesto o firma assente."; return false; }
            if (string.IsNullOrWhiteSpace(_publicKeyPem))
            { detail = "Chiave pubblica del manifesto non configurata."; return false; }
            byte[] data = File.ReadAllBytes(_layout.ManifestPath);
            byte[] signature = Convert.FromBase64String(File.ReadAllText(_layout.ManifestSignaturePath).Trim());
            using RSA rsa = RSA.Create();
            rsa.ImportFromPem(_publicKeyPem);
            if (!rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
            { detail = "Firma RSA-PSS del manifesto non valida."; return false; }
            manifest = JsonSerializer.Deserialize<InternalFileManifest>(data, ManifestJsonOptions);
            if (manifest is null || manifest.Files is null)
            { detail = "Manifesto non leggibile."; return false; }
            foreach (InternalFileManifestEntry entry in manifest.Files)
            {
                if (!TryResolveManifestPath(entry.RelativePath, out _))
                { detail = $"Percorso non valido nel manifesto: {entry.RelativePath}"; manifest = null; return false; }
                if (entry.Size < 0 || entry.Sha256.Length != 64 || !entry.Sha256.All(Uri.IsHexDigit))
                { detail = $"Metadati non validi nel manifesto: {entry.RelativePath}"; manifest = null; return false; }
            }
            detail = "Manifesto verificato.";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or CryptographicException or JsonException or FormatException)
        { detail = ex.Message; manifest = null; return false; }
    }

    public bool TryResolveManifestPath(string relativePath, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath)) return false;
        try
        {
            string candidate = Path.GetFullPath(Path.Combine(_layout.InstallationDirectory,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!FFGuardianScanExclusionService.IsPathInsideDirectory(candidate, _layout.InstallationDirectory)) return false;
            fullPath = candidate; return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        { return false; }
    }
}

internal sealed class FFGuardianScanExclusionService
{
    private static readonly StringComparer PathComparer = StringComparer.OrdinalIgnoreCase;
    private readonly FFGuardianInternalPathLayout _layout;
    private readonly FFGuardianManifestVerifier _manifestVerifier;
    private readonly HashSet<string> _loadedOfficialFiles;
    private readonly Dictionary<string, InternalFileManifestEntry> _verifiedManifestFiles;
    private readonly ConcurrentDictionary<string, long> _reasonCounters = new(StringComparer.OrdinalIgnoreCase);

    public FFGuardianScanExclusionService(FFGuardianInternalPathLayout? layout = null)
    {
        _layout = layout ?? FFGuardianInternalPathLayout.CreateDefault();
        _manifestVerifier = new FFGuardianManifestVerifier(_layout);
        _loadedOfficialFiles = GetLoadedOfficialFiles();
        _verifiedManifestFiles = LoadVerifiedManifestEntries();
    }

    public static FFGuardianScanExclusionService Current { get; } = new();
    public FFGuardianInternalPathLayout Layout => _layout;

    public bool ShouldExcludeFromNormalScan(string path) =>
        GetCategory(path, requireTrustedFile: true) != InternalPathCategory.None;

    public bool IsApplicationBinary(string path) => GetCategory(path, true) == InternalPathCategory.ApplicationBinary;
    public bool IsEngineComponent(string path) => GetCategory(path, true) == InternalPathCategory.Engine;
    public bool IsDatabaseFile(string path) => GetCategory(path, true) == InternalPathCategory.Database;
    public bool IsQuarantineFile(string path) => GetCategory(path, false) == InternalPathCategory.Quarantine;
    public bool IsTemporaryInternalFile(string path) => GetCategory(path, false) == InternalPathCategory.Temp;
    public bool IsUpdateFile(string path) => GetCategory(path, false) == InternalPathCategory.Updates;
    public bool IsLogFile(string path) => GetCategory(path, false) == InternalPathCategory.Logs;
    public IReadOnlyCollection<string> GetProtectedInternalDirectories() => _layout.ProtectedOperationalDirectories;

    public string GetExclusionReason(string path)
    {
        InternalPathCategory category = GetCategory(path, true);
        string reason = category switch
        {
            InternalPathCategory.ApplicationBinary => "Componente applicativo ufficiale verificato",
            InternalPathCategory.Engine => "Motore interno verificato",
            InternalPathCategory.Rules => "Regola interna verificata",
            InternalPathCategory.Database => "Database firme interno verificato",
            InternalPathCategory.Quarantine => "Contenuto quarantena non eseguibile",
            InternalPathCategory.Logs => "Log interno",
            InternalPathCategory.Temp => "File temporaneo interno autorizzato",
            InternalPathCategory.Updates => "Aggiornamento interno in elaborazione",
            InternalPathCategory.Backup => "Backup/rollback interno",
            InternalPathCategory.Cache => "Cache interna",
            InternalPathCategory.Reports => "Report generato dal programma",
            _ => string.Empty
        };
        if (reason.Length > 0) _reasonCounters.AddOrUpdate(reason, 1, (_, value) => value + 1);
        return reason;
    }

    public IReadOnlyDictionary<string, long> GetExclusionCounters() =>
        new Dictionary<string, long>(_reasonCounters, StringComparer.OrdinalIgnoreCase);

    public InternalPathCategory GetCategory(string path, bool requireTrustedFile)
    {
        if (!TryNormalizeCandidate(path, out string candidate)) return InternalPathCategory.None;
        if (ContainsReparsePoint(candidate)) return InternalPathCategory.None;

        if (IsPathInsideDirectory(candidate, _layout.QuarantineDirectory)) return InternalPathCategory.Quarantine;
        if (IsPathInsideDirectory(candidate, _layout.LogsDirectory)) return InternalPathCategory.Logs;
        if (IsPathInsideDirectory(candidate, _layout.TempDirectory)) return InternalPathCategory.Temp;
        if (IsPathInsideDirectory(candidate, _layout.UpdatesDirectory)) return InternalPathCategory.Updates;
        if (IsPathInsideDirectory(candidate, _layout.BackupDirectory)) return InternalPathCategory.Backup;
        if (IsPathInsideDirectory(candidate, _layout.CacheDirectory)) return InternalPathCategory.Cache;
        if (IsPathInsideDirectory(candidate, _layout.ReportsDirectory)) return InternalPathCategory.Reports;

        if (IsPathInsideDirectory(candidate, _layout.InstallationDirectory))
        {
            if (requireTrustedFile && !IsTrustedInstalledFile(candidate)) return InternalPathCategory.None;
            if (PathComparer.Equals(candidate, NormalizeFile(Environment.ProcessPath ?? string.Empty)) ||
                _loadedOfficialFiles.Contains(candidate)) return InternalPathCategory.ApplicationBinary;
            if (IsPathInsideDirectory(candidate, _layout.EngineDirectory)) return InternalPathCategory.Engine;
            if (IsPathInsideDirectory(candidate, _layout.RulesDirectory)) return InternalPathCategory.Rules;
            if (IsPathInsideDirectory(candidate, _layout.DatabaseDirectory)) return InternalPathCategory.Database;
            if (_verifiedManifestFiles.ContainsKey(candidate)) return InternalPathCategory.ApplicationBinary;
        }
        return InternalPathCategory.None;
    }

    private bool IsTrustedInstalledFile(string candidate) =>
        _loadedOfficialFiles.Contains(candidate) || _verifiedManifestFiles.ContainsKey(candidate) ||
        PathComparer.Equals(candidate, NormalizeFile(Environment.ProcessPath ?? string.Empty));

    private Dictionary<string, InternalFileManifestEntry> LoadVerifiedManifestEntries()
    {
        Dictionary<string, InternalFileManifestEntry> entries = new(PathComparer);
        if (!_manifestVerifier.TryLoadVerified(out InternalFileManifest? manifest, out _) || manifest is null) return entries;
        foreach (InternalFileManifestEntry entry in manifest.Files)
            if (_manifestVerifier.TryResolveManifestPath(entry.RelativePath, out string full)) entries[NormalizeFile(full)] = entry;
        return entries;
    }

    private static HashSet<string> GetLoadedOfficialFiles()
    {
        HashSet<string> files = new(PathComparer);
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try { if (!string.IsNullOrWhiteSpace(assembly.Location)) files.Add(NormalizeFile(assembly.Location)); }
            catch (NotSupportedException) { }
        }
        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath)) files.Add(NormalizeFile(Environment.ProcessPath));
        return files;
    }

    public static bool IsPathInsideDirectory(string candidatePath, string trustedDirectory)
    {
        if (!TryNormalize(candidatePath, out string candidate) || !TryNormalize(trustedDirectory, out string directory)) return false;
        if (PathComparer.Equals(candidate, directory)) return true;
        string prefix = directory + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryNormalizeCandidate(string path, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(path)) return false;
        try { normalized = NormalizeFile(path); return true; }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        { return false; }
    }

    private static bool TryNormalize(string path, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(path)) return false;
        try { normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)); return true; }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        { return false; }
    }

    private static string NormalizeFile(string path) => Path.GetFullPath(path);

    private static bool ContainsReparsePoint(string candidate)
    {
        try
        {
            string? current = File.Exists(candidate) ? Path.GetDirectoryName(candidate) : candidate;
            while (!string.IsNullOrEmpty(current))
            {
                if (Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
                string? parent = Path.GetDirectoryName(current);
                if (PathComparer.Equals(parent ?? string.Empty, current)) break;
                current = parent;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { return true; }
        return false;
    }
}

internal sealed class FFGuardianIntegrityService
{
    private readonly FFGuardianInternalPathLayout _layout;
    private readonly FFGuardianManifestVerifier _verifier;
    public FFGuardianIntegrityService(FFGuardianInternalPathLayout? layout = null)
    {
        _layout = layout ?? FFGuardianInternalPathLayout.CreateDefault();
        _verifier = new FFGuardianManifestVerifier(_layout);
    }

    public async Task<IntegrityReport> VerifyAsync(CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        if (!_verifier.TryLoadVerified(out InternalFileManifest? manifest, out string manifestDetail) || manifest is null)
            return new IntegrityReport(now, false,
                [new IntegrityFileResult("Assets/ffguardian-files-manifest.json", IntegrityState.ManifestUnavailable,
                    "Manifest", manifestDetail, null, null)]);

        List<IntegrityFileResult> results = [];
        HashSet<string> expected = new(StringComparer.OrdinalIgnoreCase);
        foreach (InternalFileManifestEntry entry in manifest.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_verifier.TryResolveManifestPath(entry.RelativePath, out string fullPath))
            { results.Add(new(entry.RelativePath, IntegrityState.Modified, entry.Component, "Percorso non valido.", null, null)); continue; }
            expected.Add(Path.GetFullPath(fullPath));
            if (!File.Exists(fullPath))
            {
                results.Add(new(entry.RelativePath, entry.Required ? IntegrityState.Missing : IntegrityState.Unknown,
                    entry.Component, "File assente.", null, null)); continue;
            }
            FileInfo info = new(fullPath);
            string hash;
            await using (FileStream stream = new(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
            IntegrityState state = info.Length != entry.Size || !hash.Equals(entry.Sha256, StringComparison.OrdinalIgnoreCase)
                ? IntegrityState.Modified : IntegrityState.Intact;
            string detail = state == IntegrityState.Intact ? "Dimensione e SHA-256 validi." : "Dimensione o SHA-256 differente.";
            if (state == IntegrityState.Intact && entry.DigitalSignatureRequired && !HasValidAuthenticode(fullPath))
            { state = IntegrityState.InvalidSignature; detail = "Firma Authenticode assente o non valida."; }
            results.Add(new(entry.RelativePath, state, entry.Component, detail, info.Length, hash));
        }

        foreach (string file in EnumerateInstallationFilesSafe(_layout.InstallationDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            string normalized = Path.GetFullPath(file);
            if (expected.Contains(normalized) || normalized.Equals(_layout.ManifestPath, StringComparison.OrdinalIgnoreCase) ||
                normalized.Equals(_layout.ManifestSignaturePath, StringComparison.OrdinalIgnoreCase)) continue;
            string relative = Path.GetRelativePath(_layout.InstallationDirectory, normalized);
            results.Add(new(relative, IntegrityState.Unknown, "Unknown", "File non presente nel manifesto.",
                new FileInfo(file).Length, null));
        }
        return new IntegrityReport(now, true, results);
    }

    private static IEnumerable<string> EnumerateInstallationFilesSafe(string root)
    {
        Stack<string> pending = new(); pending.Push(root);
        while (pending.Count > 0)
        {
            string directory = pending.Pop();
            string[] files = []; string[] directories = [];
            try { files = Directory.GetFiles(directory); directories = Directory.GetDirectories(directory); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            foreach (string file in files) yield return file;
            foreach (string child in directories)
            {
                try { if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) == 0) pending.Push(child); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
        }
    }

    private static bool HasValidAuthenticode(string path)
    {
        try
        {
            using X509Certificate certificate = X509Certificate.CreateFromSignedFile(path);
            using X509Certificate2 certificate2 = new(certificate);
            using X509Chain chain = new();
            return chain.Build(certificate2);
        }
        catch (CryptographicException) { return false; }
    }
}
