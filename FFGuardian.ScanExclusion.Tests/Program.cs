using FFGuardian;

static class Contract
{
    private static int _failures;
    public static void True(bool condition, string name)
    {
        if (condition) Console.WriteLine("PASS " + name);
        else { Console.Error.WriteLine("FAIL " + name); _failures++; }
    }
    public static int ExitCode => _failures == 0 ? 0 : 1;
}

string root = Path.Combine(Path.GetTempPath(), "FF Guardian Test " + Guid.NewGuid().ToString("N"));
string trusted = Path.Combine(root, "FFGuardian");
Directory.CreateDirectory(trusted);
string inside = Path.Combine(trusted, "Engine", "file.dll");
string sibling = Path.Combine(root, "FFGuardian-Malware.exe");
string prefixSibling = Path.Combine(root, "FFGuardian-Evil", "file.dll");

Contract.True(FFGuardianScanExclusionService.IsPathInsideDirectory(inside, trusted), "inside directory");
Contract.True(!FFGuardianScanExclusionService.IsPathInsideDirectory(sibling, trusted), "external same name");
Contract.True(!FFGuardianScanExclusionService.IsPathInsideDirectory(prefixSibling, trusted), "directory boundary");
Contract.True(!FFGuardianScanExclusionService.IsPathInsideDirectory(
    Path.Combine(trusted, "..", "outside.exe"), trusted), "path traversal");
Contract.True(FFGuardianScanExclusionService.IsPathInsideDirectory(
    Path.Combine(trusted, "folder with spaces", "à-speciale.dll"), trusted), "spaces and special characters");

FFGuardianScanExclusionService service = FFGuardianScanExclusionService.Current;
string unknownInstallFile = Path.Combine(AppContext.BaseDirectory, "unknown-manual-plugin.bin");
File.WriteAllText(unknownInstallFile, "unknown");
try
{
    Contract.True(!service.ShouldExcludeFromNormalScan(unknownInstallFile), "unknown install file remains scannable");
    Contract.True(service.ShouldExcludeFromNormalScan(Environment.ProcessPath!), "current executable excluded");
    string externalNamed = Path.Combine(root, "FFGuardian.exe");
    File.WriteAllText(externalNamed, "external");
    Contract.True(!service.ShouldExcludeFromNormalScan(externalNamed), "external FFGuardian.exe scanned");

    Directory.CreateDirectory(service.Layout.LogsDirectory);
    string log = Path.Combine(service.Layout.LogsDirectory, "runtime.log");
    File.WriteAllText(log, "log");
    Contract.True(service.ShouldExcludeFromNormalScan(log), "internal log excluded");

    Directory.CreateDirectory(service.Layout.QuarantineDirectory);
    string quarantine = Path.Combine(service.Layout.QuarantineDirectory, Guid.NewGuid().ToString("N") + ".ffq");
    File.WriteAllText(quarantine, "non executable container");
    Contract.True(service.ShouldExcludeFromNormalScan(quarantine), "quarantine excluded");
}
finally
{
    try { File.Delete(unknownInstallFile); } catch { }
    try { Directory.Delete(root, true); } catch { }
}

Environment.ExitCode = Contract.ExitCode;
