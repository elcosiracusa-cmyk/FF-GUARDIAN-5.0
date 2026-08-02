using System.Runtime.CompilerServices;
using FFGuardian;

internal static class RansomShieldMaximumSmokeTests10
{
    [ModuleInitializer]
    internal static void Run()
    {
        int harmless = RansomShieldMaximum10.CalculateTestScore(
            events: 3,
            renames: 0,
            deletes: 0,
            highEntropy: 0,
            suspiciousExtensions: 0,
            canary: false,
            ransomNote: false,
            threshold: 35);
        Ensure(harmless < 40, "Attività ordinaria classificata con rischio eccessivo.");

        int burst = RansomShieldMaximum10.CalculateTestScore(
            events: 45,
            renames: 12,
            deletes: 8,
            highEntropy: 6,
            suspiciousExtensions: 4,
            canary: false,
            ransomNote: true,
            threshold: 35);
        Ensure(burst >= 90, "Il comportamento ransomware massivo non raggiunge il livello critico.");

        int canary = RansomShieldMaximum10.CalculateTestScore(
            events: 1,
            renames: 0,
            deletes: 1,
            highEntropy: 0,
            suspiciousExtensions: 0,
            canary: true,
            ransomNote: false,
            threshold: 35);
        Ensure(canary == 100, "La modifica di un file-esca deve produrre rischio massimo.");

        int entropy = RansomShieldMaximum10.CalculateTestScore(
            events: 10,
            renames: 2,
            deletes: 0,
            highEntropy: 6,
            suspiciousExtensions: 0,
            canary: false,
            ransomNote: false,
            threshold: 35);
        Ensure(entropy >= 40, "Una serie di file ad alta entropia non produce rischio sufficiente.");
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
