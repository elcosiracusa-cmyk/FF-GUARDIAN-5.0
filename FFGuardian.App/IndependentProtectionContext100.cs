using FFGuardian.Engine10;

namespace FFGuardian;

internal sealed class IndependentProtectionContext100 : ApplicationContext
{
    private readonly FFGuardianEngine10 _engine;
    private readonly IndependentMainForm100 _mainForm;
    private readonly NotifyIcon _trayIcon;
    private readonly System.Windows.Forms.Timer _auditTimer;
    private readonly AutonomousProtectionAgent10 _protectionAgent;
    private readonly AppSettings10 _settings;
    private readonly RansomShieldSettings10 _ransomSettings;
    private readonly RansomShieldMonitor10 _ransomShield;
    private readonly RansomShieldIntelligence10 _ransomIntelligence;
    private bool _allowExit;

    public IndependentProtectionContext100()
    {
        _settings = AppSettings10.Load();
        _ransomSettings = RansomShieldSettings10.Load();
        AppSettings10.ApplyStartup(_settings.StartWithWindows);

        _engine = new FFGuardianEngine10();
        _mainForm = new IndependentMainForm100(_engine);
        AdvancedActionButtons10.Attach(_mainForm, _engine);
        RequestedActionButtons10.Attach(_mainForm, _engine);
        SettingsCenter10.Attach(_mainForm, _settings, ApplyRuntimeSettings);
        RansomShieldCenter10.Attach(_mainForm, _ransomSettings, ApplyRansomShieldSettings);
        SignatureUpdateCenter10.Attach(_mainForm, _engine);
        ProfessionalQuarantineCenter10.Attach(_mainForm, _engine);
        ProfessionalQuarantineCenter10.RunRetentionCleanup();
        _mainForm.FormClosing += MainForm_FormClosing;
        _mainForm.Resize += (_, _) =>
        {
            if (_settings.MinimizeToTray && _mainForm.WindowState == FormWindowState.Minimized)
                HideToTray();
        };
        _mainForm.Shown += async (_, _) => await ApplyStartupActionsAsync();

        ContextMenuStrip menu = new();
        menu.Items.Add("Apri FF GUARDIAN", null, (_, _) => ShowMainForm());
        menu.Items.Add("Apri rapporti", null, (_, _) => OpenReportsFolder());
        menu.Items.Add(new ToolStripSeparator());
        ToolStripMenuItem startupItem = new("Avvia con Windows")
        {
            Checked = _settings.StartWithWindows,
            CheckOnClick = true
        };
        startupItem.CheckedChanged += (_, _) =>
        {
            _settings.StartWithWindows = startupItem.Checked;
            _settings.Save();
        };
        menu.Items.Add(startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Esci completamente", null, (_, _) => ExitCompletely());

        _trayIcon = new NotifyIcon
        {
            Icon = DobermannIconFactory.CreateIcon(),
            Text = "FF GUARDIAN 10 — Protezione autonoma attiva",
            Visible = true,
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) => ShowMainForm();

        _protectionAgent = new AutonomousProtectionAgent10(_engine);
        _protectionAgent.Activity += ProtectionAgent_Activity;
        _protectionAgent.Start();
        _mainForm.SetAgentStatus(_protectionAgent.IsRunning, _protectionAgent.MonitoredFolderCount);

        _ransomShield = new RansomShieldMonitor10(_ransomSettings);
        _ransomShield.Alert += RansomShield_Alert;
        _ransomShield.Start();

        _ransomIntelligence = new RansomShieldIntelligence10(_ransomSettings);
        _ransomIntelligence.Alert += RansomIntelligence_Alert;
        _ransomIntelligence.Start();

        _auditTimer = new System.Windows.Forms.Timer();
        _auditTimer.Tick += (_, _) => ShowAuditReminder();
        ApplyRuntimeSettings();

        _mainForm.Show();
        StabilityCoordinator82.WriteInformationLog(
            $"FF GUARDIAN 10 avviato: monitoraggio autonomo su {_protectionAgent.MonitoredFolderCount} cartelle; Ransom Shield su {_ransomShield.ProtectedFolderCount} cartelle; Intelligence {(_ransomIntelligence.IsRunning ? "attiva" : "disattivata")}.");
    }

    private async Task ApplyStartupActionsAsync()
    {
        if (!_settings.UpdateSignaturesAtStartup)
            return;

        try
        {
            await _engine.ReloadSignaturesAsync();
            StabilityCoordinator82.WriteInformationLog(
                $"Database firme caricato all’avvio: {_engine.SignatureDatabaseVersion}");
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            if (_settings.ShowSecurityNotifications)
                ShowBalloon("FF GUARDIAN — Aggiornamento firme", ex.Message, ToolTipIcon.Warning);
        }
    }

    private void ApplyRuntimeSettings()
    {
        int hours = Math.Clamp(_settings.AuditReminderHours, 1, 24);
        _auditTimer.Interval = checked(hours * 60 * 60 * 1000);
        _auditTimer.Enabled = _settings.EnableAuditReminders;
        AppSettings10.ApplyStartup(_settings.StartWithWindows);
    }

    private void ApplyRansomShieldSettings()
    {
        _ransomShield.Restart();
        _ransomIntelligence.Restart();
        StabilityCoordinator82.WriteInformationLog(
            $"Ransom Shield {(_ransomShield.IsRunning ? "attivo" : "disattivato")} su {_ransomShield.ProtectedFolderCount} cartelle; Intelligence {(_ransomIntelligence.IsRunning ? "attiva" : "disattivata")}.");
    }

    private void RansomShield_Alert(object? sender, RansomShieldAlert10 alert)
    {
        if (_mainForm.IsDisposed || _mainForm.Disposing)
            return;

        if (_mainForm.InvokeRequired)
        {
            try { _mainForm.BeginInvoke(new MethodInvoker(() => RansomShield_Alert(sender, alert))); }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
            return;
        }

        if (_ransomSettings.ShowAlerts)
        {
            ShowBalloon(
                "FF GUARDIAN — Ransom Shield",
                $"{alert.Status}. {alert.Changes} modifiche in {alert.Folder}",
                ToolTipIcon.Error);
        }
    }

    private void RansomIntelligence_Alert(object? sender, RansomIntelligenceAlert10 alert)
    {
        if (_mainForm.IsDisposed || _mainForm.Disposing)
            return;

        if (_mainForm.InvokeRequired)
        {
            try { _mainForm.BeginInvoke(new MethodInvoker(() => RansomIntelligence_Alert(sender, alert))); }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
            return;
        }

        StabilityCoordinator82.WriteInformationLog(
            $"Ransom Shield 2.0: {alert.Severity} {alert.Score}/100 — {string.Join("; ", alert.Reasons)}");
        if (_ransomSettings.ShowAlerts)
        {
            ShowBalloon(
                $"FF GUARDIAN — Ransom Shield {alert.Severity}",
                $"{alert.Status}. Punteggio {alert.Score}/100. {Path.GetFileName(alert.TriggerPath)}",
                ToolTipIcon.Error);
        }
    }

    private void ProtectionAgent_Activity(object? sender, ProtectionAgentEvent10 e)
    {
        if (_mainForm.IsDisposed || _mainForm.Disposing)
            return;

        if (_mainForm.InvokeRequired)
        {
            try { _mainForm.BeginInvoke(new MethodInvoker(() => ProtectionAgent_Activity(sender, e))); }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
            return;
        }

        _mainForm.RecordAgentActivity(e);
        _mainForm.SetAgentStatus(_protectionAgent.IsRunning, _protectionAgent.MonitoredFolderCount);

        if (e.ScanResult?.Verdict == ThreatVerdict10.Malicious)
        {
            if (_settings.ShowSecurityNotifications)
                ShowBalloon("FF GUARDIAN — Minaccia rilevata", Path.GetFileName(e.Path), ToolTipIcon.Error);
            StabilityCoordinator82.WriteInformationLog($"Minaccia rilevata: {e.Path} — {e.ScanResult.DetectionName}");
        }
        else if (e.ScanResult?.Verdict == ThreatVerdict10.Suspicious)
        {
            if (_settings.ShowSecurityNotifications)
                ShowBalloon("FF GUARDIAN — File sospetto", Path.GetFileName(e.Path), ToolTipIcon.Warning);
            StabilityCoordinator82.WriteInformationLog($"File sospetto: {e.Path} — {e.ScanResult.DetectionName}");
        }
        else if (e.EventType is "WatcherError" or "ScanError")
        {
            StabilityCoordinator82.WriteInformationLog($"Agente autonomo: {e.Status}");
        }
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_allowExit)
            return;

        if (!_settings.MinimizeToTray)
        {
            _allowExit = true;
            _trayIcon.Visible = false;
            return;
        }

        e.Cancel = true;
        HideToTray();
        if (_settings.ShowSecurityNotifications)
        {
            ShowBalloon(
                "FF GUARDIAN resta attivo",
                "Il monitoraggio autonomo continua nell’area di notifica.",
                ToolTipIcon.Info);
        }
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
        if (!_settings.EnableAuditReminders || !_settings.ShowSecurityNotifications)
            return;

        _mainForm.SetAgentStatus(_protectionAgent.IsRunning, _protectionAgent.MonitoredFolderCount);
        ShowBalloon(
            "FF GUARDIAN 10",
            $"Protezione autonoma attiva su {_protectionAgent.MonitoredFolderCount} cartelle.",
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
            _protectionAgent.Activity -= ProtectionAgent_Activity;
            _ransomShield.Alert -= RansomShield_Alert;
            _ransomIntelligence.Alert -= RansomIntelligence_Alert;
            try { _protectionAgent.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
            catch { }
            _ransomIntelligence.Dispose();
            _ransomShield.Dispose();
            _auditTimer.Dispose();
            _trayIcon.Dispose();
            _mainForm.Dispose();
            _engine.Dispose();
        }

        base.Dispose(disposing);
    }
}
