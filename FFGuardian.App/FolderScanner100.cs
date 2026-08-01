namespace FFGuardian;

internal sealed record FolderScanProgress100(
    int FilesVisited,
    int ExecutablesAnalyzed,
    int Findings,
    string CurrentPath);

internal sealed record FolderScanSummary100(
    string Root,
    DateTime StartedUtc,
    DateTime CompletedUtc,
    int FilesVisited,
    int ExecutablesAnalyzed,
    int AccessErrors,
    IReadOnlyList<IndependentFinding> Findings);

internal sealed class FolderScanner100
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".com", ".scr", ".msi", ".ps1", ".bat", ".cmd", ".vbs", ".js", ".hta"
    };

    private readonly IndependentSecurityEngine100 _engine;

    public FolderScanner100(IndependentSecurityEngine100 engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    public async Task<FolderScanSummary100> ScanAsync(
        string root,
        IProgress<FolderScanProgress100>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        string fullRoot = Path.GetFullPath(root);
        if (!Directory.Exists(fullRoot))
            throw new DirectoryNotFoundException($"Cartella non trovata: {fullRoot}");

        DateTime startedUtc = DateTime.UtcNow;
        List<IndependentFinding> findings = [];
        int visited = 0;
        int analyzed = 0;
        int accessErrors = 0;
        Stack<string> pending = new();
        HashSet<string> visitedDirectories = new(StringComparer.OrdinalIgnoreCase);
        pending.Push(fullRoot);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string current = pending.Pop();
            if (!visitedDirectories.Add(current))
                continue;

            string[] files;
            try
            {
                files = Directory.GetFiles(current);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                accessErrors++;
                continue;
            }

            foreach (string file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                visited++;

                if (!SupportedExtensions.Contains(Path.GetExtension(file)))
                {
                    if (visited % 100 == 0)
                        progress?.Report(new(visited, analyzed, findings.Count, file));
                    continue;
                }

                analyzed++;
                IndependentFinding? finding = await _engine.AnalyzeFileAsync(file, cancellationToken).ConfigureAwait(false);
                if (finding is not null)
                    findings.Add(finding);

                progress?.Report(new(visited, analyzed, findings.Count, file));
            }

            string[] directories;
            try
            {
                directories = Directory.GetDirectories(current);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                accessErrors++;
                continue;
            }

            foreach (string directory in directories)
            {
                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(directory);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    accessErrors++;
                    continue;
                }

                if ((attributes & FileAttributes.ReparsePoint) == 0)
                    pending.Push(directory);
            }
        }

        return new FolderScanSummary100(
            fullRoot,
            startedUtc,
            DateTime.UtcNow,
            visited,
            analyzed,
            accessErrors,
            findings.OrderByDescending(item => item.Score).ToArray());
    }
}
