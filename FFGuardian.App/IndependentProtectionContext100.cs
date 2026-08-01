using Microsoft.Win32;

namespace FFGuardian;

internal sealed class IndependentProtectionContext100 : ApplicationContext
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "FFGuardian";

    private readonly IndependentMainForm100 _mainForm;
    private readonly NotifyIcon _trayIcon;
    private readonly System.Windows.Forms.Timer _auditTimer;
    private bool _allowExit;

    public IndependentProtectionContext100()
    {
        EnsureStartupEnabled();

        _mainForm = new IndependentMainForm100();
        _mainForm.FormClosing += MainForm_FormClosing;
        _mainForm.Resize += (_, _) =>
        {
            if (_mainForm.WindowState == FormWindowState.Minimized)
                HideToTray();
        };

        ContextMenuStrip menu = new();
        menu.Items.Add("Apri FF GUARDIAN", null, (_, _) => ShowMainForm());
        menu.Items.Add("Apri rapporti", null, (_, _) => OpenReportsFolder());
        menu.Items.Add(new ToolStripSeparator());
        ToolStripMenuItem startupItem = new("Avvia con Windows")
        {
            Checked = IsStartupEnabled(),
            CheckOnClick = true
        };
        startupItem.CheckedChanged += (_, _) => SetStartup(startupItem.Checked);
        menu.Items.Add(startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Esci completamente", null, (_, _) => ExitCompletely());

        _trayIcon = new NotifyIcon
        {
            Icon = DobermannIconFactory.CreateIcon(),
            Text = "FF GUARDIAN 10 Core Alpha — Motore indipendente",
            Visible = true,
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) => ShowMainForm();

        _auditTimer = new System.Windows.Forms.Timer { Interval = 30 * 60 * 1000 };
        _auditTimer.Tick += (_, _) => ShowAuditReminder();
        _auditTimer.Start();

        _mainForm.Show();
        StabilityCoordinator82.WriteInformationLog("FF GUARDIAN 10 Core Alpha avviato senza Microsoft Defender.");
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_allowExit)
            return;

        e.Cancel = true;
        HideToTray();
        ShowBalloon(
            "FF GUARDIAN resta attivo",
            "Il motore indipendente rimane disponibile nell’area di notifica.",
            ToolTipIcon.Info);
    }

    private void HideToTray()
    {
        _mainForm.Hide();
        _mainForm.ShowInTaskbar = false;
    }

    private void ShowMainForm()
    {
        _mainForm.ShowInTaskbar = true;
        _mainForm.Show();
        if (_mainForm.WindowState == FormWindowState.Minimized)
            _mainForm.WindowState = FormWindowState.Normal;
        _mainForm.Activate();
    }

    private void ShowAuditReminder()
    {
        ShowBalloon(
            "FF GUARDIAN 10",
            "È disponibile un nuovo controllo indipendente del sistema.",
            ToolTipIcon.Info);
    }

    private void ShowBalloon(string title, string text, ToolTipIcon icon)
    {
        _trayIcon.BalloonTipTitle = title;
        _trayIcon.BalloonTipText = text;
        _trayIcon.BalloonTipIcon = icon;
        _trayIcon.ShowBalloonTip(5000);
    }

    private static void OpenReportsFolder()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "FF Guardian Reports");
        Directory.CreateDirectory(folder);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", folder)
        {
            UseShellExecute = true
        });
    }

    private static bool IsStartupEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(RunValueName) is string;
    }

    private static void EnsureStartupEnabled()
    {
        if (!IsStartupEnabled())
            SetStartup(true);
    }

    private static void SetStartup(bool enabled)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled)
            key.SetValue(RunValueName, $"\"{Environment.ProcessPath}\"");
        else
            key.DeleteValue(RunValueName, false);
    }

    private void ExitCompletely()
    {
        _allowExit = true;
        _auditTimer.Stop();
        _trayIcon.Visible = false;
        _mainForm.Close();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _auditTimer.Dispose();
            _trayIcon.Dispose();
            _mainForm.Dispose();
        }

        base.Dispose(disposing);
    }
}
