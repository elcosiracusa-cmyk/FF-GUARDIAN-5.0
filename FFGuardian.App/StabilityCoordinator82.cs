namespace FFGuardian;

internal static class StabilityCoordinator82
{
    private static readonly object Sync = new();
    private static System.Windows.Forms.Timer? _uiTimer;
    private static bool _running;

    public static void Start()
    {
        lock (Sync)
        {
            if (_running) return;
            _running = true;

            _uiTimer = new System.Windows.Forms.Timer
            {
                Interval = 500
            };
            _uiTimer.Tick += (_, _) => RunUiCycle();
            _uiTimer.Start();
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

    public static void WriteStabilityLog(Exception ex)
    {
        try
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "FF Guardian", "Logs");
            Directory.CreateDirectory(folder);
            string message = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\tSTABILITY 8.2\t{ex.GetType().Name}: {ex.Message}{Environment.NewLine}";
            File.AppendAllText(Path.Combine(folder, "stability-8.2.log"), message);
        }
        catch
        {
            // La diagnostica non deve mai interrompere l'applicazione.
        }
    }
}
