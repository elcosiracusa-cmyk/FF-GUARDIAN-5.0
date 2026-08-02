using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FFGuardian.Engine10;

namespace FFGuardian;

internal sealed record ProtectNowReport20(
    DateTime StartedUtc,
    DateTime CompletedUtc,
    string SignatureVersion,
    bool RansomShieldEnabled,
    bool UsbShieldEnabled,
    string UsbShieldMode,
    int RemovableDrives,
    bool FirewallDomain,
    bool FirewallPrivate,
    bool FirewallPublic,
    int ProcessesChecked,
    int SuspiciousProcesses,
    int FilesScanned,
    int SuspiciousFiles,
    int MaliciousFiles,
    int ScanErrors,
    int PersistenceItems,
    int AuditFindings,
    int SecurityScore,
    string Verdict);

internal static class ProtectNowCenter20
{
    private static readonly Color Background = Color.FromArgb(3, 8, 12);
    private static readonly Color Surface = Color.FromArgb(17, 31, 39);
    private static readonly Color Neon = Color.FromArgb(160, 255, 0);

    public static void Attach(IndependentMainForm100 form, FFGuardianEngine10 engine)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(engine);

        TabControl? tabs = FindControl<TabControl>(form);
        TabPage? page = tabs?.TabPages.Cast<TabPage>()
            .FirstOrDefault(item => string.Equals(item.Text, "SCANSIONE", StringComparison.OrdinalIgnoreCase));
        FlowLayoutPanel? panel = page is null ? null : FindControl<FlowLayoutPanel>(page);
        if (panel is null || FindButtons(panel).Any(button => button.Text == "PROTEGGI ORA 2.0"))
            return;

        foreach (Button oldButton in FindButtons(panel)
                     .Where(button => string.Equals(button.Text, "PROTEGGI ORA", StringComparison.OrdinalIgnoreCase)))
            oldButton.Visible = false;

        Button protect = CreateButton("PROTEGGI ORA 2.0");
        protect.Click += async (_, _) =>
        {
            protect.Enabled = false;
            try
            {
                await RunAsync(form, engine);
            }
            catch (OperationCanceledException)
            {
                MessageBox.Show(form, "Controllo annullato.", "FF GUARDIAN 10",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                StabilityCoordinator82.WriteStabilityLog(ex);
                MessageBox.Show(form, ex.Message, "FF GUARDIAN — Proteggi Ora 2.0",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                protect.Enabled = true;
            }
        };
        panel.Controls.Add(protect);
        panel.Controls.SetChildIndex(protect, 0);
    }

    private static async Task RunAsync(IWin32Window owner, FFGuardianEngine10 engine)
    {
        using ProgressForm20 progress = new();
        progress.Show(owner);
        DateTime started = DateTime.UtcNow;

        string signatureVersion = engine.SignatureDatabaseVersion;
        RansomShieldSettings10 ransom = RansomShieldSettings10.Load();
        UsbShieldSettings10 usb = UsbShieldSettings10.Load();
        bool firewallDomain = false;
        bool firewallPrivate = false;
        bool firewallPublic = false;
        int processCount = 0;
        int suspiciousProcesses = 0;
        int filesScanned = 0;
        int suspiciousFiles = 0;
        int maliciousFiles = 0;
        int scanErrors = 0;
        EngineAuditResult10? audit = null;

        try
        {
            progress.SetStep(1, 9, "Caricamento e verifica database firme…");
            await engine.ReloadSignaturesAsync(progress.Token);
            signatureVersion = engine.SignatureDatabaseVersion;

            progress.SetStep(2, 9, "Verifica Ransom Shield…");
            ransom = RansomShieldSettings10.Load();
            await Task.Delay(100, progress.Token);

            progress.SetStep(3, 9, "Analisi processi attivi…");
            string[] processPaths = GetProcessPaths().Take(250).ToArray();
            processCount = processPaths.Length;
            for (int index = 0; index < processPaths.Length; index++)
            {
                progress.Token.ThrowIfCancellationRequested();
                progress.SetStatus($"Processo {index + 1}/{processPaths.Length}: {Path.GetFileName(processPaths[index])}");
                FileScanResult10 result = await engine.ScanFileAsync(processPaths[index], progress.Token);
                if (result.Verdict is ThreatVerdict10.Suspicious or ThreatVerdict10.Malicious)
                    suspiciousProcesses++;
                if (result.Verdict == ThreatVerdict10.Malicious)
                    maliciousFiles++;
            }

            progress.SetStep(4, 9, "Controllo avvio, servizi e attività pianificate…");
            audit = await engine.RunAuditAsync(new Progress<string>(progress.SetStatus), progress.Token);

            progress.SetStep(5, 9, "Verifica profili Windows Firewall…");
            Dictionary<string, bool> firewall = await ReadFirewallProfilesAsync(progress.Token);
            firewallDomain = firewall.GetValueOrDefault("Domain");
            firewallPrivate = firewall.GetValueOrDefault("Private");
            firewallPublic = firewall.GetValueOrDefault("Public");

            progress.SetStep(6, 9, "Verifica USB Shield e dispositivi rimovibili…");
            usb = UsbShieldSettings10.Load();
            int removableDrives = GetRemovableDriveCount();
            progress.SetStatus($"USB Shield: {usb.Mode}; dispositivi collegati: {removableDrives}");
            await Task.Delay(100, progress.Token);

            progress.SetStep(7, 9, "Scansione rapida delle aree sensibili…");
            foreach (string root in GetQuickScanRoots())
            {
                progress.Token.ThrowIfCancellationRequested();
                FolderScanSummary10 summary = await engine.ScanFolderAsync(
                    root, new Progress<string>(progress.SetStatus), progress.Token);
                filesScanned += summary.FilesScanned;
                suspiciousFiles += summary.SuspiciousFiles;
                maliciousFiles += summary.MaliciousFiles;
                scanErrors += summary.ErrorFiles;
            }

            progress.SetStep(8, 9, "Calcolo punteggio di sicurezza…");
            int score = audit?.SecurityScore ?? 100;
            if (!ransom.Enabled) score -= 10;
            if (!usb.Enabled) score -= 5;
            if (!firewallDomain || !firewallPrivate || !firewallPublic) score -= 10;
            score -= maliciousFiles * 20;
            score -= suspiciousFiles * 5;
            score -= suspiciousProcesses * 3;
            score = Math.Clamp(score, 0, 100);

            string verdict = maliciousFiles > 0
                ? "MINACCE RILEVATE"
                : suspiciousFiles + suspiciousProcesses > 0
                    ? "ATTENZIONE RICHIESTA"
                    : score < 80 ? "PROTEZIONE DA MIGLIORARE" : "SISTEMA PROTETTO";

            progress.SetStep(9, 9, "Generazione rapporto finale…");
            ProtectNowReport20 report = new(
                started,
                DateTime.UtcNow,
                signatureVersion,
                ransom.Enabled,
                usb.Enabled,
                usb.Mode.ToString(),
                GetRemovableDriveCount(),
                firewallDomain,
                firewallPrivate,
                firewallPublic,
                processCount,
                suspiciousProcesses,
                filesScanned,
                suspiciousFiles,
                maliciousFiles,
                scanErrors,
                audit?.PersistenceItems ?? 0,
                audit?.Findings.Count ?? 0,
                score,
                verdict);

            string reportPath = SaveReport(report);
            progress.Close();
            ShowResult(owner, report, reportPath);
        }
        finally
        {
            if (!progress.IsDisposed)
                progress.Close();
        }
    }

    private static async Task<Dictionary<string, bool>> ReadFirewallProfilesAsync(CancellationToken cancellationToken)
    {
        Dictionary<string, bool> result = new(StringComparer.OrdinalIgnoreCase);
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo("powershell.exe",
                "-NoProfile -NonInteractive -Command \"Get-NetFirewallProfile | ForEach-Object { Write-Output ($_.Name + '|' + $_.Enabled) }\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.Start();
        string output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
            return result;

        foreach (string line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = line.Split('|', 2);
            if (parts.Length == 2 && bool.TryParse(parts[1].Trim(), out bool enabled))
                result[parts[0].Trim()] = enabled;
        }
        return result;
    }

    private static string SaveReport(ProtectNowReport20 report)
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "FF Guardian Reports", "ProtectNow");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, $"ProtectNow-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(report,
            new JsonSerializerOptions { WriteIndented = true }));
        StabilityCoordinator82.WriteInformationLog($"Proteggi Ora 2.0 completato: {report.Verdict}, punteggio {report.SecurityScore}/100.");
        return path;
    }

    private static void ShowResult(IWin32Window owner, ProtectNowReport20 report, string reportPath)
    {
        MessageBoxIcon icon = report.MaliciousFiles > 0 ? MessageBoxIcon.Error :
            report.SuspiciousFiles + report.SuspiciousProcesses > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information;
        MessageBox.Show(owner,
            $"PROTEGGI ORA 2.0 COMPLETATO\n\n" +
            $"Stato: {report.Verdict}\n" +
            $"Punteggio sicurezza: {report.SecurityScore}/100\n" +
            $"Database firme: {report.SignatureVersion}\n" +
            $"Ransom Shield: {(report.RansomShieldEnabled ? "ATTIVO" : "DISATTIVATO")}\n" +
            $"USB Shield: {(report.UsbShieldEnabled ? report.UsbShieldMode : "DISATTIVATO")}\n" +
            $"Firewall: dominio {(report.FirewallDomain ? "ON" : "OFF")}, privato {(report.FirewallPrivate ? "ON" : "OFF")}, pubblico {(report.FirewallPublic ? "ON" : "OFF")}\n" +
            $"Processi controllati: {report.ProcessesChecked}\n" +
            $"Processi sospetti: {report.SuspiciousProcesses}\n" +
            $"Elementi di avvio: {report.PersistenceItems}\n" +
            $"Segnalazioni audit: {report.AuditFindings}\n" +
            $"File analizzati: {report.FilesScanned:N0}\n" +
            $"File sospetti: {report.SuspiciousFiles}\n" +
            $"Minacce: {report.MaliciousFiles}\n" +
            $"Errori: {report.ScanErrors}\n\n" +
            $"Rapporto salvato in:\n{reportPath}",
            "FF GUARDIAN — Proteggi Ora 2.0", MessageBoxButtons.OK, icon);
    }

    private static IEnumerable<string> GetProcessPaths()
    {
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        foreach (Process process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    string? path = process.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                        paths.Add(Path.GetFullPath(path));
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
                {
                }
            }
        }
        return paths;
    }

    private static string[] GetQuickScanRoots()
    {
        string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new[]
        {
            Path.Combine(profile, "Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
            Path.GetTempPath()
        }.Where(Directory.Exists).Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static int GetRemovableDriveCount() => DriveInfo.GetDrives().Count(drive =>
    {
        try { return drive.DriveType == DriveType.Removable && drive.IsReady; }
        catch { return false; }
    });

    private static Button CreateButton(string text)
    {
        Button button = new()
        {
            Width = 250,
            Height = 50,
            Margin = new Padding(6),
            Text = text,
            BackColor = Neon,
            ForeColor = Color.Black,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = Color.White;
        button.FlatAppearance.BorderSize = 2;
        return button;
    }

    private static IEnumerable<Button> FindButtons(Control root)
    {
        if (root is Button button) yield return button;
        foreach (Control child in root.Controls)
            foreach (Button found in FindButtons(child)) yield return found;
    }

    private static T? FindControl<T>(Control root) where T : Control
    {
        if (root is T match) return match;
        foreach (Control child in root.Controls)
        {
            T? found = FindControl<T>(child);
            if (found is not null) return found;
        }
        return null;
    }

    private sealed class ProgressForm20 : Form
    {
        private readonly Label _status;
        private readonly ProgressBar _bar;
        private readonly CancellationTokenSource _cancellation = new();

        public ProgressForm20()
        {
            Text = "PROTEGGI ORA 2.0";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(720, 250);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Background;
            ForeColor = Color.White;
            _status = new Label { Dock = DockStyle.Fill, Padding = new Padding(20),
                Text = "Preparazione controllo completo…", TextAlign = ContentAlignment.MiddleLeft };
            _bar = new ProgressBar { Dock = DockStyle.Bottom, Height = 26, Minimum = 0, Maximum = 100 };
            Button cancel = new() { Dock = DockStyle.Bottom, Height = 42, Text = "ANNULLA",
                BackColor = Surface, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            cancel.Click += (_, _) => _cancellation.Cancel();
            Controls.Add(_status);
            Controls.Add(_bar);
            Controls.Add(cancel);
        }

        public CancellationToken Token => _cancellation.Token;

        public void SetStep(int current, int total, string status)
        {
            SetStatus(status);
            int value = Math.Clamp(current * 100 / Math.Max(total, 1), 0, 100);
            if (InvokeRequired) BeginInvoke(new MethodInvoker(() => _bar.Value = value));
            else _bar.Value = value;
        }

        public void SetStatus(string status)
        {
            if (IsDisposed) return;
            if (InvokeRequired) { BeginInvoke(new MethodInvoker(() => SetStatus(status))); return; }
            _status.Text = status;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _cancellation.Dispose();
            base.Dispose(disposing);
        }
    }
}

internal static class ProtectNowBootstrap20
{
    private static bool _attached;

    [ModuleInitializer]
    internal static void Initialize()
    {
        Application.Idle += AttachWhenReady;
    }

    private static void AttachWhenReady(object? sender, EventArgs e)
    {
        if (_attached) return;
        IndependentMainForm100? form = Application.OpenForms.OfType<IndependentMainForm100>().FirstOrDefault();
        if (form is null || form.IsDisposed || !form.IsHandleCreated) return;

        FFGuardianEngine10? engine = FindEngine(form);
        if (engine is null) return;
        ProtectNowCenter20.Attach(form, engine);
        _attached = true;
        Application.Idle -= AttachWhenReady;
    }

    private static FFGuardianEngine10? FindEngine(IndependentMainForm100 form)
    {
        var field = typeof(IndependentMainForm100).GetField("_engine",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return field?.GetValue(form) as FFGuardianEngine10;
    }
}
