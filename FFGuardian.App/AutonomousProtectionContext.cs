using Microsoft.Win32;
using System.Text.Json;

namespace FFGuardian;

internal sealed class AutonomousProtectionContext : ApplicationContext
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "FFGuardian";
    private const string SupportEmail = "alsafe127.00@gmail.com";
    private static readonly string DataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FF Guardian");
    private static readonly string LogFolder = Path.Combine(DataFolder, "Logs");
    private static readonly string StateFile = Path.Combine(DataFolder, "autonomous-state.json");

    private readonly MainForm _mainForm;
    private readonly DefenderService _defender = new();
    private readonly NotifyIcon _trayIcon;
    private readonly System.Windows.Forms.Timer _timer;
    private AutonomousState _state;
    private bool _allowExit;
    private bool _checkRunning;

    public AutonomousProtectionContext()
    {
        Directory.CreateDirectory(LogFolder);
        _state = LoadState();
        EnsureStartupEnabled();

        _mainForm = new MainForm();
        _mainForm.FormClosing += MainForm_FormClosing;
        _mainForm.Resize += (_, _) => { if (_mainForm.WindowState == FormWindowState.Minimized) HideToTray(); };

        ContextMenuStrip menu = new();
        menu.Items.Add("Apri FF GUARDIAN", null, (_, _) => ShowMainForm());
        menu.Items.Add("Scansione rapida", null, async (_, _) => await RunTrayActionAsync(_defender.QuickScanAsync, "Scansione rapida avviata"));
        menu.Items.Add("Aggiorna firme", null, async (_, _) => await RunTrayActionAsync(_defender.UpdateAsync, "Firme aggiornate"));
        menu.Items.Add("Apri Quarantena", null, (_, _) => _defender.OpenWindowsSecurity());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Assistenza clienti", null, (_, _) => OpenSupportEmail());
        menu.Items.Add("Apri cartella registri", null, (_, _) => OpenLogs());
        ToolStripMenuItem startupItem = new("Avvia con Windows") { Checked = IsStartupEnabled(), CheckOnClick = true };
        startupItem.CheckedChanged += (_, _) => SetStartup(startupItem.Checked);
        menu.Items.Add(startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Esci completamente", null, (_, _) => ExitCompletely());

        _trayIcon = new NotifyIcon
        {
            Icon = DobermannIconFactory.CreateIcon(),
            Text = "FF GUARDIAN 5.2 — Dobermann Protection attiva",
            Visible = true,
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) => ShowMainForm();

        _timer = new System.Windows.Forms.Timer { Interval = 15 * 60 * 1000 };
        _timer.Tick += async (_, _) => await AutonomousCheckAsync();
        _timer.Start();

        _mainForm.Show();
        _ = AutonomousCheckAsync();
        Log("Avvio", "FF GUARDIAN 5.2 Dobermann Support inizializzato.");
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_allowExit) return;
        e.Cancel = true;
        HideToTray();
        ShowBalloon("FF GUARDIAN resta attivo", "La protezione autonoma continua in background.", ToolTipIcon.Info);
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
        _mainForm.WindowState = FormWindowState.Maximized;
        _mainForm.Activate();
    }

    private async Task AutonomousCheckAsync()
    {
        if (_checkRunning) return;
        _checkRunning = true;
        try
        {
            SecurityState security = await _defender.GetStateAsync();
            _state.LastCheckUtc = DateTime.UtcNow;
            _state.LastScore = security.Score;

            if (security.Issues.Count > 0)
            {
                string details = string.Join(" ", security.Issues.Take(3));
                Log("Avviso", details);
                ShowBalloon("FF GUARDIAN — Attenzione", details, ToolTipIcon.Warning);
            }
            else
            {
                Log("Controllo", $"Sistema protetto. Punteggio {security.Score}/100.");
            }

            if (DateTime.UtcNow - _state.LastSignatureUpdateUtc >= TimeSpan.FromHours(24))
            {
                await _defender.UpdateAsync();
                _state.LastSignatureUpdateUtc = DateTime.UtcNow;
                Log("Aggiornamento", "Firme Microsoft Defender aggiornate automaticamente.");
            }

            if (DateTime.UtcNow - _state.LastQuickScanUtc >= TimeSpan.FromDays(7))
            {
                await _defender.QuickScanAsync();
                _state.LastQuickScanUtc = DateTime.UtcNow;
                Log("Scansione", "Scansione rapida settimanale avviata automaticamente.");
                ShowBalloon("Scansione programmata", "FF GUARDIAN ha avviato la scansione rapida settimanale.", ToolTipIcon.Info);
            }

            SaveState();
        }
        catch (Exception ex)
        {
            Log("Errore", ex.ToString());
        }
        finally
        {
            _checkRunning = false;
        }
    }

    private async Task RunTrayActionAsync(Func<Task> action, string success)
    {
        try
        {
            await action();
            Log("Azione manuale", success);
            ShowBalloon("FF GUARDIAN", success, ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            Log("Errore", ex.ToString());
            ShowBalloon("FF GUARDIAN — Errore", ex.Message, ToolTipIcon.Error);
        }
    }

    private static void OpenSupportEmail()
    {
        string subject = Uri.EscapeDataString("Supporto FF GUARDIAN 5.2");
        string body = Uri.EscapeDataString($"Descrizione problema:\r\n\r\nVersione: FF GUARDIAN 5.2\r\nComputer: {Environment.MachineName}\r\nUtente: {Environment.UserName}\r\nWindows: {Environment.OSVersion.Version}\r\nData: {DateTime.Now:dd/MM/yyyy HH:mm}");
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo($"mailto:{SupportEmail}?subject={subject}&body={body}") { UseShellExecute = true });
    }

    private void ShowBalloon(string title, string text, ToolTipIcon icon)
    {
        _trayIcon.BalloonTipTitle = title;
        _trayIcon.BalloonTipText = text.Length > 240 ? text[..240] : text;
        _trayIcon.BalloonTipIcon = icon;
        _trayIcon.ShowBalloonTip(5000);
    }

    private static void OpenLogs()
    {
        Directory.CreateDirectory(LogFolder);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(LogFolder) { UseShellExecute = true });
    }

    private static void Log(string category, string message)
    {
        try
        {
            Directory.CreateDirectory(LogFolder);
            string path = Path.Combine(LogFolder, $"guardian-{DateTime.Now:yyyy-MM-dd}.log");
            File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\t{category}\t{message.Replace("\r", " ").Replace("\n", " ")}{Environment.NewLine}");
        }
        catch { }
    }

    private AutonomousState LoadState()
    {
        try
        {
            if (File.Exists(StateFile)) return JsonSerializer.Deserialize<AutonomousState>(File.ReadAllText(StateFile)) ?? new AutonomousState();
        }
        catch { }
        return new AutonomousState();
    }

    private void SaveState()
    {
        try
        {
            Directory.CreateDirectory(DataFolder);
            File.WriteAllText(StateFile, JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private static bool IsStartupEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(RunValueName) is string;
    }

    private static void EnsureStartupEnabled() { if (!IsStartupEnabled()) SetStartup(true); }

    private static void SetStartup(bool enabled)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enabled) key.SetValue(RunValueName, $"\"{Environment.ProcessPath}\"");
        else key.DeleteValue(RunValueName, false);
    }

    private void ExitCompletely()
    {
        _allowExit = true;
        _timer.Stop();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _mainForm.Close();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _trayIcon.Dispose();
            _mainForm.Dispose();
        }
        base.Dispose(disposing);
    }

    private sealed class AutonomousState
    {
        public DateTime LastCheckUtc { get; set; } = DateTime.MinValue;
        public DateTime LastSignatureUpdateUtc { get; set; } = DateTime.MinValue;
        public DateTime LastQuickScanUtc { get; set; } = DateTime.MinValue;
        public int LastScore { get; set; }
    }
}
