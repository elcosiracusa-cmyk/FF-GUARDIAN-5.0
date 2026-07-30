namespace FFGuardian;

internal static class StabilityCoordinator82
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, DateTime> RecentErrors = new(StringComparer.Ordinal);
    private static System.Windows.Forms.Timer? _uiTimer;
    private static System.Windows.Forms.Timer? _maintenanceTimer;
    private static bool _running;

    public static void Start()
    {
        lock (Sync)
        {
            if (_running) return;
            _running = true;

            _uiTimer = new System.Windows.Forms.Timer { Interval = 500 };
            _uiTimer.Tick += (_, _) => RunUiCycle();
            _uiTimer.Start();

            _maintenanceTimer = new System.Windows.Forms.Timer { Interval = 60000 };
            _maintenanceTimer.Tick += (_, _) => RunMaintenance();
            _maintenanceTimer.Start();

            RunMaintenance();
        }
    }

    private static void RunUiCycle()
    {
        if (Application.OpenForms.Count == 0) return;

        SafeRun(LayoutRepair.ApplyToOpenForms);
        SafeRun(StatusInnovationFix.Apply);
        SafeRun(SupportEmailLayoutFix.Apply);
        SafeRun(Advanced60Ui.Apply);
        SafeRun(Version60Fix.Apply);
        SafeRun(UiReadabilityFix.Apply);
        SafeRun(ProfessionalSecurityCenter63.Apply);
        SafeRun(SidebarFirewallFix631.Apply);
        SafeRun(CloudReady80.Apply);
        SafeRun(AdvancedSettings81.Apply);
    }

    private static void SafeRun(EventHandler handler)
    {
        try
        {
            handler(null, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            WriteStabilityLog(ex);
        }
    }

    private static void RunMaintenance()
    {
        try
        {
            RotateLogIfNeeded();
            RemoveExpiredErrorKeys();
        }
        catch
        {
            // La manutenzione non deve mai interrompere l'applicazione.
        }
    }

    public static void WriteStabilityLog(Exception ex)
    {
        try
        {
            string key = ex.GetType().FullName + "|" + ex.Message;
            lock (Sync)
            {
                if (RecentErrors.TryGetValue(key, out DateTime last) && DateTime.UtcNow - last < TimeSpan.FromMinutes(2))
                    return;
                RecentErrors[key] = DateTime.UtcNow;
            }

            string folder = GetLogFolder();
            Directory.CreateDirectory(folder);
            RotateLogIfNeeded();
            string message = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\tSTABILITY 8.2.1\t{ex.GetType().Name}: {ex.Message}{Environment.NewLine}";
            File.AppendAllText(Path.Combine(folder, "stability-8.2.log"), message);
        }
        catch
        {
            // La diagnostica non deve mai interrompere l'applicazione.
        }
    }

    private static void RotateLogIfNeeded()
    {
        string folder = GetLogFolder();
        Directory.CreateDirectory(folder);
        string current = Path.Combine(folder, "stability-8.2.log");
        if (!File.Exists(current) || new FileInfo(current).Length < 2 * 1024 * 1024)
            return;

        string archive = Path.Combine(folder, $"stability-8.2-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        File.Move(current, archive, true);

        foreach (string oldFile in Directory.GetFiles(folder, "stability-8.2-*.log")
                     .OrderByDescending(File.GetLastWriteTimeUtc)
                     .Skip(5))
        {
            try { File.Delete(oldFile); } catch { }
        }
    }

    private static void RemoveExpiredErrorKeys()
    {
        lock (Sync)
        {
            DateTime threshold = DateTime.UtcNow.AddMinutes(-10);
            foreach (string key in RecentErrors.Where(pair => pair.Value < threshold).Select(pair => pair.Key).ToArray())
                RecentErrors.Remove(key);
        }
    }

    private static string GetLogFolder() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "FF Guardian", "Logs");
}
