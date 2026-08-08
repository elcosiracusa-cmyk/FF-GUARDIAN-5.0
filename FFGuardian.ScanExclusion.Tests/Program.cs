using FFGuardian;

internal static class Program
{
    private static int _failures;

    private static int Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "FF Guardian Test " + Guid.NewGuid().ToString("N"));
        string trusted = Path.Combine(root, "FFGuardian");
        Directory.CreateDirectory(trusted);
        string inside = Path.Combine(trusted, "Engine", "file.dll");
        string sibling = Path.Combine(root, "FFGuardian-Malware.exe");
        string prefixSibling = Path.Combine(root, "FFGuardian-Evil", "file.dll");

        Assert(FFGuardianScanExclusionService.IsPathInsideDirectory(inside, trusted), "inside directory");
        Assert(!FFGuardianScanExclusionService.IsPathInsideDirectory(sibling, trusted), "external same name");
        Assert(!FFGuardianScanExclusionService.IsPathInsideDirectory(prefixSibling, trusted), "directory boundary");
        Assert(!FFGuardianScanExclusionService.IsPathInsideDirectory(
            Path.Combine(trusted, "..", "outside.exe"), trusted), "path traversal");
        Assert(FFGuardianScanExclusionService.IsPathInsideDirectory(
            Path.Combine(trusted, "folder with spaces", "à-speciale.dll"), trusted),
            "spaces and special characters");

        FFGuardianScanExclusionService service = FFGuardianScanExclusionService.Current;
        string unknownInstallFile = Path.Combine(AppContext.BaseDirectory, "unknown-manual-plugin.bin");
        File.WriteAllText(unknownInstallFile, "unknown");
        try
        {
            Assert(!service.ShouldExcludeFromNormalScan(unknownInstallFile),
                "unknown install file remains scannable");
            string? processPath = Environment.ProcessPath;
            Assert(!string.IsNullOrWhiteSpace(processPath) && service.ShouldExcludeFromNormalScan(processPath),
                "current executable excluded");
            string externalNamed = Path.Combine(root, "FFGuardian.exe");
            File.WriteAllText(externalNamed, "external");
            Assert(!service.ShouldExcludeFromNormalScan(externalNamed), "external FFGuardian.exe scanned");

            Directory.CreateDirectory(service.Layout.LogsDirectory);
            string log = Path.Combine(service.Layout.LogsDirectory, "runtime.log");
            File.WriteAllText(log, "log");
            Assert(service.ShouldExcludeFromNormalScan(log), "internal log excluded");

            Directory.CreateDirectory(service.Layout.QuarantineDirectory);
            string quarantine = Path.Combine(service.Layout.QuarantineDirectory,
                Guid.NewGuid().ToString("N") + ".ffq");
            File.WriteAllText(quarantine, "non executable container");
            Assert(service.ShouldExcludeFromNormalScan(quarantine), "quarantine excluded");
        }
        finally
        {
            TryDeleteFile(unknownInstallFile);
            TryDeleteDirectory(root);
        }

        return _failures == 0 ? 0 : 1;
    }

    private static void Assert(bool condition, string name)
    {
        if (condition)
            Console.WriteLine("PASS " + name);
        else
        {
            Console.Error.WriteLine("FAIL " + name);
            _failures++;
        }
    }

    private static void TryDeleteFile(string path)
    {
        try { File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
