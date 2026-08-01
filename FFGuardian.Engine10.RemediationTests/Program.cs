using Microsoft.Win32;
using FFGuardian.Engine10;

internal static class Program
{
    private static async Task<int> Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "FFGuardian-Remediation-" + Guid.NewGuid().ToString("N"));
        string startupRoot = Path.Combine(root, "Startup");
        string quarantineRoot = Path.Combine(root, "Quarantine");
        string rollbackRoot = Path.Combine(root, "Rollback");
        string registryPath = @"Software\FFGuardian\Engine10Tests\" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(startupRoot);

        try
        {
            QuarantineStore10 quarantine = new(quarantineRoot);
            RollbackManager10 rollback = new(rollbackRoot);
            RemediationEngine10 remediation = new(quarantine, rollback, new[] { startupRoot });

            string startupFile = Path.Combine(startupRoot, "sample.cmd");
            const string startupContent = "@echo off\necho FF Guardian remediation test";
            await File.WriteAllTextAsync(startupFile, startupContent);

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

            RollbackRecord10 fileRecord = await rollback.GetRecordAsync(result.RollbackId);
            await rollback.RestoreFileAsync(fileRecord);
            Ensure(File.Exists(startupFile), "Il rollback file non ha ricreato l'elemento originale.");
            Ensure(await File.ReadAllTextAsync(startupFile) == startupContent,
                "Il rollback file non ha ripristinato il contenuto esatto.");

            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(registryPath, writable: true))
                key.SetValue("TestValue", "original-value", RegistryValueKind.String);

            RollbackRecord10 registryRecord = await rollback.BackupRegistryValueAsync(
                RegistryHive.CurrentUser,
                RegistryView.Default,
                registryPath,
                "TestValue",
                "TestRegistryRollback");

            using (RegistryKey key = Registry.CurrentUser.CreateSubKey(registryPath, writable: true))
                key.SetValue("TestValue", "changed-value", RegistryValueKind.String);

            await rollback.RestoreRegistryValueAsync(registryRecord);
            using (RegistryKey? restoredKey = Registry.CurrentUser.OpenSubKey(registryPath, writable: false))
            {
                string restoredValue = restoredKey?.GetValue("TestValue")?.ToString() ?? string.Empty;
                Ensure(restoredValue == "original-value", "Il rollback registro non ha ripristinato il valore originale.");
                Ensure(restoredKey?.GetValueKind("TestValue") == RegistryValueKind.String,
                    "Il rollback registro non ha conservato il tipo originale.");
            }

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

            Console.WriteLine("FFGuardian.Engine10 remediation and rollback tests: PASSED");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FFGuardian.Engine10 remediation and rollback tests: FAILED");
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(registryPath, throwOnMissingSubKey: false); } catch { }
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
