using FFGuardian.Engine10;

internal static class Program
{
    private static async Task<int> Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "FFGuardian-Remediation-" + Guid.NewGuid().ToString("N"));
        string startupRoot = Path.Combine(root, "Startup");
        string quarantineRoot = Path.Combine(root, "Quarantine");
        string rollbackRoot = Path.Combine(root, "Rollback");
        Directory.CreateDirectory(startupRoot);

        try
        {
            QuarantineStore10 quarantine = new(quarantineRoot);
            RollbackManager10 rollback = new(rollbackRoot);
            RemediationEngine10 remediation = new(quarantine, rollback, new[] { startupRoot });

            string startupFile = Path.Combine(startupRoot, "sample.cmd");
            await File.WriteAllTextAsync(startupFile, "@echo off\necho FF Guardian remediation test");

            AuditFinding10 finding = new(
                "TEST-STARTUP-001",
                "Persistenza",
                "sample.cmd",
                startupFile,
                AuditSeverity10.Medium,
                30,
                "Elemento Startup artificiale di test.",
                string.Empty,
                "Firma digitale assente",
                true);

            RemediationPlan10 plan = remediation.CreateDisableStartupFilePlan(finding);
            Ensure(plan.RequiresConfirmation, "La remediation deve richiedere conferma esplicita.");
            Ensure(plan.RollbackSupported, "La remediation deve dichiarare il supporto rollback.");

            bool confirmationBlocked = false;
            try
            {
                await remediation.ExecuteDisableStartupFileAsync(plan, confirmed: false);
            }
            catch (InvalidOperationException)
            {
                confirmationBlocked = true;
            }
            Ensure(confirmationBlocked, "La remediation senza conferma non è stata bloccata.");
            Ensure(File.Exists(startupFile), "Il file Startup è stato modificato senza conferma.");

            RemediationExecutionResult10 result = await remediation.ExecuteDisableStartupFileAsync(plan, confirmed: true);
            string disabledPath = startupFile + ".ffguardian-disabled";
            Ensure(result.Succeeded, "La remediation confermata non risulta completata.");
            Ensure(!string.IsNullOrWhiteSpace(result.RollbackId), "Identificativo rollback mancante.");
            Ensure(!File.Exists(startupFile), "L'elemento Startup originale è ancora attivo.");
            Ensure(File.Exists(disabledPath), "L'elemento Startup disabilitato non è stato creato.");
            Ensure(Directory.EnumerateFiles(rollbackRoot, "payload.bin", SearchOption.AllDirectories).Any(),
                "Il backup rollback non è stato creato.");

            string externalFile = Path.Combine(root, "outside.cmd");
            await File.WriteAllTextAsync(externalFile, "@echo off");
            AuditFinding10 externalFinding = finding with
            {
                Id = "TEST-OUTSIDE-001",
                Name = "outside.cmd",
                Target = externalFile
            };

            bool externalBlocked = false;
            try
            {
                remediation.CreateDisableStartupFilePlan(externalFinding);
            }
            catch (InvalidOperationException)
            {
                externalBlocked = true;
            }
            Ensure(externalBlocked, "Un percorso esterno alle cartelle Startup non è stato bloccato.");

            Console.WriteLine("FFGuardian.Engine10 remediation tests: PASSED");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FFGuardian.Engine10 remediation tests: FAILED");
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static void Ensure(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
