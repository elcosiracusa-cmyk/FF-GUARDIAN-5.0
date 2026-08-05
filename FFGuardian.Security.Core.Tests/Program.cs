using System.Security.Cryptography;
using System.Text;
using FFGuardian.Security.Core;
using Microsoft.Extensions.DependencyInjection;

return await CoreTests.RunAsync();

internal static class CoreTests
{
    public static async Task<int> RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "FFGuardian-CoreTests-" + Guid.NewGuid().ToString("N"));
        string app = Path.Combine(root, "App");
        string data = Path.Combine(root, "Data");
        Directory.CreateDirectory(app);
        Directory.CreateDirectory(data);
        try
        {
            Assert(YaraService.Parse("RuleOne C:\\sample.txt\r\n", "C:\\sample.txt").Single().Rule == "RuleOne", "YARA parser");
            ClamAvDetection detection = ClamAvService.Parse("C:\\sample.txt: Win.Test FOUND\r\n").Single();
            Assert(detection.Signature == "Win.Test" && detection.TargetPath == "C:\\sample.txt", "ClamAV parser");

            ServiceCollection services = new();
            services.AddFFGuardianSecurityServices(options =>
            {
                options.BaseDirectory = app;
                options.DataDirectory = data;
            });
            await using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
            IFileHashService hashes = provider.GetRequiredService<IFileHashService>();
            IPathExclusionService exclusions = provider.GetRequiredService<IPathExclusionService>();
            IQuarantineService quarantine = provider.GetRequiredService<IQuarantineService>();

            string hashFile = Path.Combine(root, "hash.txt");
            await File.WriteAllTextAsync(hashFile, "abc", Encoding.ASCII);
            string expected = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes("abc")));
            Assert(await hashes.ComputeSha256Async(hashFile, CancellationToken.None) == expected, "SHA-256");

            string engineFile = Path.Combine(app, "Engine", "Yara", "yara64.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(engineFile)!);
            await File.WriteAllTextAsync(engineFile, "test");
            Assert(exclusions.ShouldExclude(engineFile), "internal engine exclusion");
            Assert(!exclusions.ShouldExclude(Path.Combine(root, "FFGuardian-Malware.exe")), "external same-name remains scannable");
            Assert(!exclusions.IsInside(Path.Combine(app + "-malware", "x.exe"), app), "directory boundary");
            Assert(exclusions.IsInside(Path.Combine(app, "sub", "..", "file.dll"), app), "normalized path");

            string source = Path.Combine(root, "suspicious.exe");
            await File.WriteAllTextAsync(source, "harmless fixture");
            QuarantineResult stored = await quarantine.QuarantineAsync(source, "Fixture", "Test.Rule", "Low", CancellationToken.None);
            Assert(stored.Success && stored.Entry is not null, "quarantine store");
            Assert(!File.Exists(source), "original removed only after verified copy");
            Assert(stored.Entry.StoredPath.EndsWith(".qdat", StringComparison.OrdinalIgnoreCase), "non executable stored extension");
            string restore = Path.Combine(root, "restored.bin");
            QuarantineResult restored = await quarantine.RestoreAsync(stored.Entry.Id, restore, false, CancellationToken.None);
            Assert(restored.Success && File.Exists(restore), "quarantine restore");
            Assert(await hashes.ComputeSha256Async(restore, CancellationToken.None) == stored.Entry.Sha256, "restored hash");
            Assert(!await quarantine.RestoreAsync(stored.Entry.Id, restore, false, CancellationToken.None).ConfigureAwait(false) is { Success: true }, "existing destination blocked");
            Assert(await quarantine.DeleteAsync(stored.Entry.Id, CancellationToken.None), "quarantine delete");

            Console.WriteLine("PASS shared security core tests");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + name);
        Console.WriteLine("PASS " + name);
    }
}
