using System.Runtime.CompilerServices;
using FFGuardian.Engine10;

internal static class AuditSmokeTests10
{
    [ModuleInitializer]
    internal static void Run()
    {
        string quoted = AuditTargetInspector10.ExtractExecutablePath(
            "\"C:\\Program Files\\Example\\example.exe\" --background");
        Ensure(quoted.Equals(@"C:\Program Files\Example\example.exe", StringComparison.OrdinalIgnoreCase),
            "Estrazione percorso quotato dell'audit non corretta.");

        string unquoted = AuditTargetInspector10.ExtractExecutablePath(
            @"C:\Windows\System32\cmd.exe /c echo test");
        Ensure(unquoted.EndsWith(@"System32\cmd.exe", StringComparison.OrdinalIgnoreCase),
            "Estrazione percorso non quotato dell'audit non corretta.");

        AuditTargetInspection10 suspicious = AuditTargetInspector10.Inspect(
            @"powershell.exe -EncodedCommand ZgBmAGcA");
        Ensure(suspicious.RiskScore >= 30,
            "Un comando PowerShell codificato e non risolvibile deve produrre un rischio significativo.");
        Ensure(suspicious.Evidence.Any(item => item.Contains("script", StringComparison.OrdinalIgnoreCase)),
            "L'audit non ha registrato l'evidenza relativa allo script.");

        AuditTargetInspection10 missing = AuditTargetInspector10.Inspect(
            @"C:\FFGuardian-Smoke-Does-Not-Exist\missing.exe");
        Ensure(!missing.Exists && missing.RiskScore >= 20,
            "Un target di persistenza inesistente deve essere rilevato.");

        Ensure(AuditTargetInspector10.Severity(55) == AuditSeverity10.Critical,
            "Classificazione Critical dell'audit non corretta.");
        Ensure(AuditTargetInspector10.Severity(20) == AuditSeverity10.Medium,
            "Classificazione Medium dell'audit non corretta.");
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
