namespace FFGuardian;

internal static class StabilityCoordinator82
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, DateTime> RecentErrors = new(StringComparer.Ordinal);
    private static readonly HashSet<Form> ConfiguredForms = new();
    private static System.Windows.Forms.Timer? _startupTimer;
    private static System.Windows.Forms.Timer? _maintenanceTimer;
    private static bool _running;
    private static int _startupAttempts;

    public static void Start()
    {
        lock (Sync)
        {
            if (_running) return;
            _running = true;

            // The old 500 ms UI loop repeatedly rebuilt the interface and caused visible flicker.
            // This timer exists only long enough to catch the main form after Application.Run starts.
            _startupTimer = new System.Windows.Forms.Timer { Interval = 250 };
            _startupTimer.Tick += (_, _) => ConfigureNewFormsDuringStartup();
            _startupTimer.Start();

            _maintenanceTimer = new System.Windows.Forms.Timer { Interval = 60000 };
            _maintenanceTimer.Tick += (_, _) => RunMaintenance();
            _maintenanceTimer.Start();

            RunMaintenance();
        }
    }

    private static void ConfigureNewFormsDuringStartup()
    {
        _startupAttempts++;
        ConfigureNewForms();

        // Five seconds is ample for the main window and startup dialogs to be created.
        // Afterwards no timer is allowed to touch layout, text or control order.
        if (_startupAttempts >= 20)
        {
            _startupTimer?.Stop();
            _startupTimer?.Dispose();
            _startupTimer = null;
        }
    }

    public static void ConfigureNewForms()
    {
        Form[] forms = Application.OpenForms.Cast<Form>()
            .Where(form => !form.IsDisposed && form.IsHandleCreated)
            .ToArray();

        foreach (Form form in forms)
        {
            if (!ConfiguredForms.Add(form))
                continue;

            ConfigureFormOnce(form);
            form.FormClosed += (_, _) => ConfiguredForms.Remove(form);
        }
    }

    private static void ConfigureFormOnce(Form form)
    {
        // Historical modules are kept for compatibility but are executed once only.
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
        SafeRun(CoreHealth83.Apply);
        SafeRun(InterfaceRecovery831.Apply);
        SafeRun(DefinitiveReports832.Apply);
        SafeRun(FinalUiAudit834.Apply);
        SafeRun(DeepBugDiagnostics835.Apply);
        SafeRun(VersionConsistency836.Apply);

        form.Invalidate(true);
        form.Update();
    }

    private static void SafeRun(EventHandler handler)
    {
        try { handler(null, EventArgs.Empty); }
        catch (Exception ex) { WriteStabilityLog(ex); }
    }

    private static void RunMaintenance()
    {
        try
        {
            RotateLogIfNeeded();
            RemoveExpiredErrorKeys();
            ConfiguredForms.RemoveWhere(form => form.IsDisposed);
        }
        catch { }
    }

    public static void WriteStabilityLog(Exception ex)
    {
        try
        {
            string key = ex.GetType().FullName + "|" + ex.Message;
            lock (Sync)
            {
                if (RecentErrors.TryGetValue(key, out DateTime last) && DateTime.UtcNow - last < TimeSpan.FromMinutes(2)) return;
                RecentErrors[key] = DateTime.UtcNow;
            }

            string folder = GetLogFolder();
            Directory.CreateDirectory(folder);
            RotateLogIfNeeded();
            string message = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\tSTABILITY 8.4.0\t{ex.GetType().Name}: {ex.Message}{Environment.NewLine}";
            File.AppendAllText(Path.Combine(folder, "stability-8.4.log"), message);
        }
        catch { }
    }

    private static void RotateLogIfNeeded()
    {
        string folder = GetLogFolder();
        Directory.CreateDirectory(folder);
        string current = Path.Combine(folder, "stability-8.4.log");
        if (!File.Exists(current) || new FileInfo(current).Length < 2 * 1024 * 1024) return;

        string archive = Path.Combine(folder, $"stability-8.4-{DateTime.Now:yyyyMMdd-HHmmss}.log");
        File.Move(current, archive, true);
        foreach (string oldFile in Directory.GetFiles(folder, "stability-8.4-*.log").OrderByDescending(File.GetLastWriteTimeUtc).Skip(5))
            try { File.Delete(oldFile); } catch { }
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

    private static string GetLogFolder() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FF Guardian", "Logs");
}