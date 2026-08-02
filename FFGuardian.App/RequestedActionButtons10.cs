using System.Diagnostics;
using FFGuardian.Engine10;

namespace FFGuardian;

internal static class RequestedActionButtons10
{
    private static readonly Color Background = Color.FromArgb(3, 8, 12);
    private static readonly Color Surface = Color.FromArgb(17, 31, 39);
    private static readonly Color Neon = Color.FromArgb(160, 255, 0);

    public static void Attach(IndependentMainForm100 form, FFGuardianEngine10 engine)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(engine);

        RenameButton(form, "GESTISCI QUARANTENA", "QUARANTENA");
        RenameButton(form, "RICARICA DATABASE FIRME", "AGGIORNA FIRME");

        TabControl? tabs = FindControl<TabControl>(form);
        if (tabs is null)
            return;

        TabPage? scanPage = tabs.TabPages.Cast<TabPage>()
            .FirstOrDefault(page => string.Equals(page.Text, "SCANSIONE", StringComparison.OrdinalIgnoreCase));
        FlowLayoutPanel? scanBar = scanPage is null ? null : FindControl<FlowLayoutPanel>(scanPage);
        if (scanBar is not null && !ContainsButton(scanBar, "PROTEGGI ORA"))
        {
            Button protectNow = CreateButton("PROTEGGI ORA", emphasized: true);
            protectNow.Click += async (_, _) => await ExecuteButtonAsync(
                protectNow, () => RunProtectNowAsync(form, engine));
            scanBar.Controls.Add(protectNow);
            scanBar.Controls.SetChildIndex(protectNow, 0);
        }

        TabPage? auditPage = tabs.TabPages.Cast<TabPage>()
            .FirstOrDefault(page => string.Equals(page.Text, "AUDIT", StringComparison.OrdinalIgnoreCase));
        FlowLayoutPanel? commandBar = auditPage is null ? null : FindControl<FlowLayoutPanel>(auditPage);
        if (commandBar is not null && !ContainsButton(commandBar, "CONTROLLO AVVIO"))
        {
            Button startupButton = CreateButton("CONTROLLO AVVIO");
            startupButton.Click += async (_, _) => await ExecuteButtonAsync(
                startupButton, () => RunStartupCheckAsync(form, engine));
            commandBar.Controls.Add(startupButton);
            commandBar.Controls.SetChildIndex(startupButton, 1);
        }
    }

    private static async Task RunProtectNowAsync(IWin32Window owner, FFGuardianEngine10 engine)
    {
        using ProgressDialog10 progress = new("PROTEGGI ORA");
        progress.Show(owner);

        DateTime started = DateTime.UtcNow;
        int filesScanned = 0;
        int suspicious = 0;
        int malicious = 0;
        int errors = 0;
        int processCount = 0;
        int suspiciousProcesses = 0;
        EngineAuditResult10? audit = null;
        string signatureStatus = "NON AGGIORNATO";

        try
        {
            progress.SetStep(1, 5, "Aggiornamento database firme…");
            await engine.ReloadSignaturesAsync(progress.Token);
            signatureStatus = engine.SignatureDatabaseVersion;

            progress.SetStep(2, 5, "Controllo programmi in avvio…");
            Progress<string> auditProgress = new(message => progress.SetStatus(message));
            audit = await engine.RunAuditAsync(auditProgress, progress.Token);

            progress.SetStep(3, 5, "Analisi processi attivi…");
            string[] processPaths = GetProcessPaths();
            processCount = processPaths.Length;
            for (int index = 0; index < processPaths.Length; index++)
            {
                progress.Token.ThrowIfCancellationRequested();
                progress.SetStatus($"Processo {index + 1}/{processPaths.Length}: {Path.GetFileName(processPaths[index])}");
                FileScanResult10 result = await engine.ScanFileAsync(processPaths[index], progress.Token);
                if (result.Verdict == ThreatVerdict10.Suspicious)
                    suspiciousProcesses++;
                else if (result.Verdict == ThreatVerdict10.Malicious)
                {
                    suspiciousProcesses++;
                    malicious++;
                }
            }

            progress.SetStep(4, 5, "Scansione rapida delle aree sensibili…");
            foreach (string root in GetQuickScanRoots())
            {
                progress.Token.ThrowIfCancellationRequested();
                Progress<string> scanProgress = new(message => progress.SetStatus(message));
                FolderScanSummary10 summary = await engine.ScanFolderAsync(root, scanProgress, progress.Token);
                filesScanned += summary.FilesScanned;
                suspicious += summary.SuspiciousFiles;
                malicious += summary.MaliciousFiles;
                errors += summary.ErrorFiles;
            }

            progress.SetStep(5, 5, "Calcolo stato di sicurezza…");
            await Task.Delay(250, progress.Token);
        }
        finally
        {
            progress.Close();
        }

        int score = audit?.SecurityScore ?? 100;
        score = Math.Clamp(score - (malicious * 20) - (suspicious * 5) - (suspiciousProcesses * 3), 0, 100);
        TimeSpan elapsed = DateTime.UtcNow - started;

        string verdict = malicious > 0
            ? "MINACCE RILEVATE"
            : suspicious + suspiciousProcesses > 0
                ? "ATTENZIONE RICHIESTA"
                : "SISTEMA PROTETTO";

        MessageBox.Show(owner,
            $"PROTEGGI ORA COMPLETATO\n\n" +
            $"Stato: {verdict}\n" +
            $"Database firme: {signatureStatus}\n" +
            $"File analizzati: {filesScanned:N0}\n" +
            $"Processi controllati: {processCount:N0}\n" +
            $"Processi sospetti: {suspiciousProcesses}\n" +
            $"Elementi di avvio: {audit?.PersistenceItems ?? 0}\n" +
            $"File sospetti: {suspicious}\n" +
            $"Minacce: {malicious}\n" +
            $"Errori di accesso: {errors}\n" +
            $"Punteggio sicurezza: {score}/100\n" +
            $"Durata: {elapsed:mm\\:ss}",
            "FF GUARDIAN 10 — Proteggi Ora",
            MessageBoxButtons.OK,
            malicious > 0 ? MessageBoxIcon.Error :
                suspicious + suspiciousProcesses > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
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
        }
        .Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
        .Select(Path.GetFullPath)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    }

    private static string[] GetProcessPaths()
    {
        List<string> paths = [];
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

        return paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static async Task RunStartupCheckAsync(IWin32Window owner, FFGuardianEngine10 engine)
    {
        using ProgressDialog10 progress = new("CONTROLLO AVVIO");
        progress.Show(owner);

        EngineAuditResult10 result;
        try
        {
            Progress<string> status = new(progress.SetStatus);
            result = await engine.RunAuditAsync(status, progress.Token);
        }
        finally
        {
            progress.Close();
        }

        AuditFinding10[] startupFindings = result.Findings
            .Where(finding =>
                finding.Category.Contains("Persist", StringComparison.OrdinalIgnoreCase) ||
                finding.Category.Contains("Startup", StringComparison.OrdinalIgnoreCase) ||
                finding.Category.Contains("Avvio", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(finding => finding.RiskScore)
            .ToArray();

        using Form dialog = new()
        {
            Text = "CONTROLLO AVVIO",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(1180, 680),
            MinimumSize = new Size(800, 500),
            BackColor = Background,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F)
        };

        DataGridView grid = new()
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Background,
            ForeColor = Color.White,
            GridColor = Color.FromArgb(58, 76, 84),
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        grid.Columns.Add("Risk", "RISCHIO");
        grid.Columns.Add("Name", "ELEMENTO");
        grid.Columns.Add("Target", "PERCORSO / COMANDO");
        grid.Columns.Add("Signature", "FIRMA");
        grid.Columns.Add("Evidence", "DETTAGLI");

        foreach (AuditFinding10 finding in startupFindings)
            grid.Rows.Add(finding.RiskScore, finding.Name, finding.Target, finding.SignatureStatus, finding.Evidence);

        dialog.Controls.Add(grid);
        dialog.ShowDialog(owner);

        MessageBox.Show(owner,
            $"Elementi di avvio controllati: {result.PersistenceItems}\nSegnalazioni mostrate: {startupFindings.Length}\nPunteggio sicurezza: {result.SecurityScore}/100",
            "FF GUARDIAN 10 — Controllo avvio completato",
            MessageBoxButtons.OK,
            startupFindings.Any(finding => finding.RiskScore >= 60) ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
    }

    private static void RenameButton(Control root, string currentText, string requestedText)
    {
        foreach (Button button in FindControls<Button>(root))
        {
            if (string.Equals(button.Text, currentText, StringComparison.OrdinalIgnoreCase))
                button.Text = requestedText;
        }
    }

    private static bool ContainsButton(Control root, string text) =>
        FindControls<Button>(root).Any(button => string.Equals(button.Text, text, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<T> FindControls<T>(Control root) where T : Control
    {
        if (root is T match)
            yield return match;
        foreach (Control child in root.Controls)
        {
            foreach (T found in FindControls<T>(child))
                yield return found;
        }
    }

    private static T? FindControl<T>(Control root) where T : Control => FindControls<T>(root).FirstOrDefault();

    private static Button CreateButton(string text, bool emphasized = false)
    {
        Button button = new()
        {
            Width = emphasized ? 230 : 210,
            Height = emphasized ? 48 : 42,
            Margin = new Padding(6),
            Text = text,
            BackColor = emphasized ? Neon : Surface,
            ForeColor = emphasized ? Color.Black : Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", emphasized ? 10F : 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = Neon;
        button.FlatAppearance.BorderSize = emphasized ? 2 : 1;
        return button;
    }

    private static async Task ExecuteButtonAsync(Button button, Func<Task> action)
    {
        button.Enabled = false;
        try
        {
            await action();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            MessageBox.Show(ex.Message, "FF GUARDIAN 10 — Operazione non completata",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            button.Enabled = true;
        }
    }

    private sealed class ProgressDialog10 : Form
    {
        private readonly Label _label;
        private readonly ProgressBar _progress;
        private readonly CancellationTokenSource _cancellation = new();

        public ProgressDialog10(string title)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(680, 230);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Background;
            ForeColor = Color.White;

            _label = new Label
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                Text = "Preparazione…",
                TextAlign = ContentAlignment.MiddleLeft
            };
            _progress = new ProgressBar
            {
                Dock = DockStyle.Bottom,
                Height = 24,
                Minimum = 0,
                Maximum = 100,
                Style = ProgressBarStyle.Continuous
            };
            Button cancel = CreateButton("ANNULLA");
            cancel.Dock = DockStyle.Bottom;
            cancel.Click += (_, _) => _cancellation.Cancel();
            Controls.Add(_label);
            Controls.Add(_progress);
            Controls.Add(cancel);
        }

        public CancellationToken Token => _cancellation.Token;

        public void SetStep(int current, int total, string status)
        {
            int percentage = total <= 0 ? 0 : Math.Clamp(current * 100 / total, 0, 100);
            SetStatus(status);
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(() => _progress.Value = percentage));
                return;
            }
            _progress.Value = percentage;
        }

        public void SetStatus(string status)
        {
            if (IsDisposed)
                return;
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(() => SetStatus(status)));
                return;
            }
            _label.Text = status;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _cancellation.Dispose();
            base.Dispose(disposing);
        }
    }
}
