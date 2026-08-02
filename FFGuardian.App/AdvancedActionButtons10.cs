using System.Diagnostics;
using System.Text.Json;
using FFGuardian.Engine10;

namespace FFGuardian;

internal static class AdvancedActionButtons10
{
    private static readonly Color Background = Color.FromArgb(3, 8, 12);
    private static readonly Color Surface = Color.FromArgb(17, 31, 39);
    private static readonly Color Neon = Color.FromArgb(160, 255, 0);

    public static void Attach(IndependentMainForm100 form, FFGuardianEngine10 engine)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(engine);

        TabControl? tabs = FindControl<TabControl>(form);
        if (tabs is null)
            throw new InvalidOperationException("Schede dell'interfaccia non trovate.");

        TabPage? scanPage = tabs.TabPages.Cast<TabPage>()
            .FirstOrDefault(page => string.Equals(page.Text, "SCANSIONE", StringComparison.OrdinalIgnoreCase));
        FlowLayoutPanel? scanBar = scanPage is null ? null : FindControl<FlowLayoutPanel>(scanPage);
        if (scanBar is not null)
        {
            Button fullScan = CreateButton("SCANSIONE COMPLETA");
            fullScan.Click += async (_, _) => await ExecuteButtonAsync(
                fullScan, () => RunFullScanAsync(form, engine));

            Button processes = CreateButton("PROCESSI ATTIVI");
            processes.Click += async (_, _) => await ExecuteButtonAsync(
                processes, () => RunProcessAuditAsync(form, engine));

            scanBar.Controls.Add(fullScan);
            scanBar.Controls.SetChildIndex(fullScan, Math.Min(1, scanBar.Controls.Count - 1));
            scanBar.Controls.Add(processes);
            scanBar.Controls.SetChildIndex(processes, Math.Min(2, scanBar.Controls.Count - 1));
        }

        TabPage? recoveryPage = tabs.TabPages.Cast<TabPage>()
            .FirstOrDefault(page => string.Equals(page.Text, "RECUPERO", StringComparison.OrdinalIgnoreCase));
        FlowLayoutPanel? recoveryPanel = recoveryPage is null ? null : FindControl<FlowLayoutPanel>(recoveryPage);
        if (recoveryPanel is not null)
        {
            Button manage = CreateButton("GESTISCI QUARANTENA");
            manage.Click += async (_, _) => await ExecuteButtonAsync(
                manage, () => ShowQuarantineManagerAsync(form, engine));
            recoveryPanel.Controls.Add(manage);
        }
    }

    private static async Task RunFullScanAsync(IWin32Window owner, FFGuardianEngine10 engine)
    {
        string[] roots = DriveInfo.GetDrives()
            .Where(drive => drive.DriveType == DriveType.Fixed && drive.IsReady)
            .Select(drive => drive.RootDirectory.FullName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (roots.Length == 0)
            throw new InvalidOperationException("Nessun disco fisso disponibile.");

        using ProgressDialog10 progress = new("SCANSIONE COMPLETA");
        progress.Show(owner);

        List<FileScanResult10> results = [];
        int scanned = 0;
        int suspicious = 0;
        int malicious = 0;
        int errors = 0;

        try
        {
            for (int index = 0; index < roots.Length; index++)
            {
                progress.Token.ThrowIfCancellationRequested();
                string root = roots[index];
                progress.SetStatus($"Disco {index + 1}/{roots.Length}: {root}");
                Progress<string> engineProgress = new(progress.SetStatus);
                FolderScanSummary10 summary = await engine.ScanFolderAsync(root, engineProgress, progress.Token);
                scanned += summary.FilesScanned;
                suspicious += summary.SuspiciousFiles;
                malicious += summary.MaliciousFiles;
                errors += summary.ErrorFiles;
                results.AddRange(summary.Results.Where(result =>
                    result.Verdict is ThreatVerdict10.Suspicious or ThreatVerdict10.Malicious or ThreatVerdict10.Error));
            }
        }
        finally
        {
            progress.Close();
        }

        ShowScanResults(owner, "RISULTATI SCANSIONE COMPLETA", results);
        MessageBox.Show(owner,
            $"File analizzati: {scanned:N0}\nSospetti: {suspicious}\nMalevoli: {malicious}\nErrori: {errors}",
            "FF GUARDIAN 10", MessageBoxButtons.OK,
            malicious > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
    }

    private static async Task RunProcessAuditAsync(IWin32Window owner, FFGuardianEngine10 engine)
    {
        using ProgressDialog10 progress = new("PROCESSI ATTIVI");
        progress.Show(owner);

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

        string[] distinctPaths = paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        List<FileScanResult10> results = [];
        try
        {
            for (int index = 0; index < distinctPaths.Length; index++)
            {
                progress.Token.ThrowIfCancellationRequested();
                string path = distinctPaths[index];
                progress.SetStatus($"Processo {index + 1}/{distinctPaths.Length}: {Path.GetFileName(path)}");
                results.Add(await engine.ScanFileAsync(path, progress.Token));
            }
        }
        finally
        {
            progress.Close();
        }

        ShowScanResults(owner, "PROCESSI ATTIVI", results);
    }

    private static async Task ShowQuarantineManagerAsync(IWin32Window owner, FFGuardianEngine10 engine)
    {
        string root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "FF Guardian", "Engine10", "Quarantine");
        Directory.CreateDirectory(root);

        using Form dialog = CreateDialog("GESTISCI QUARANTENA", new Size(1050, 620));
        DataGridView grid = CreateGrid();
        grid.Columns.Add("Id", "ID");
        grid.Columns.Add("OriginalPath", "Percorso originale");
        grid.Columns.Add("Detection", "Rilevamento");
        grid.Columns.Add("Created", "Data");
        grid.Columns.Add("Restored", "Ripristinato");

        foreach (string metadataPath in Directory.EnumerateFiles(root, "metadata.json", SearchOption.AllDirectories))
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(metadataPath));
                JsonElement json = document.RootElement;
                string fallbackId = Path.GetFileName(Path.GetDirectoryName(metadataPath)) ?? string.Empty;
                string id = ReadString(json, "Id") ?? fallbackId;
                string original = ReadString(json, "OriginalPath") ?? "Percorso non disponibile";
                string detection = ReadString(json, "DetectionName") ?? "Non specificato";
                string created = ReadString(json, "CreatedUtc") ?? string.Empty;
                bool restored = ReadBoolean(json, "Restored");
                grid.Rows.Add(id, original, detection, created, restored ? "Sì" : "No");
            }
            catch (Exception ex)
            {
                StabilityCoordinator82.WriteStabilityLog(ex);
            }
        }

        Button restore = CreateButton("RIPRISTINA SELEZIONATO");
        restore.Click += async (_, _) =>
        {
            string id = Convert.ToString(grid.CurrentRow?.Cells[0].Value) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(id))
            {
                MessageBox.Show(dialog, "Seleziona un elemento della quarantena.", "FF GUARDIAN 10",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (MessageBox.Show(dialog,
                "Ripristinare il file nella posizione originale? Verranno verificati integrità e SHA-256.",
                "Conferma ripristino", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            restore.Enabled = false;
            try
            {
                await engine.RestoreQuarantineAsync(id);
                if (grid.CurrentRow is not null)
                    grid.CurrentRow.Cells[4].Value = "Sì";
                MessageBox.Show(dialog, "Ripristino completato e verificato.", "FF GUARDIAN 10",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                restore.Enabled = true;
            }
        };

        Button close = CreateButton("CHIUDI");
        close.Click += (_, _) => dialog.Close();

        FlowLayoutPanel commands = new()
        {
            Dock = DockStyle.Bottom,
            Height = 62,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
            BackColor = Surface
        };
        commands.Controls.Add(close);
        commands.Controls.Add(restore);
        dialog.Controls.Add(grid);
        dialog.Controls.Add(commands);
        dialog.ShowDialog(owner);
    }

    private static void ShowScanResults(IWin32Window owner, string title, IEnumerable<FileScanResult10> results)
    {
        using Form dialog = CreateDialog(title, new Size(1180, 680));
        DataGridView grid = CreateGrid();
        grid.Columns.Add("Verdict", "Esito");
        grid.Columns.Add("Confidence", "Confidenza");
        grid.Columns.Add("Detection", "Rilevamento");
        grid.Columns.Add("Reasons", "Motivazioni");
        grid.Columns.Add("Hash", "SHA-256");
        grid.Columns.Add("Path", "Percorso");

        foreach (FileScanResult10 result in results
            .OrderByDescending(result => result.Verdict == ThreatVerdict10.Malicious)
            .ThenByDescending(result => result.Verdict == ThreatVerdict10.Suspicious)
            .ThenByDescending(result => result.Confidence))
        {
            grid.Rows.Add(result.Verdict, result.Confidence, result.DetectionName,
                string.Join("; ", result.Reasons), result.Sha256, result.Path);
        }

        dialog.Controls.Add(grid);
        dialog.ShowDialog(owner);
    }

    private static string? ReadString(JsonElement json, string propertyName) =>
        json.TryGetProperty(propertyName, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool ReadBoolean(JsonElement json, string propertyName)
    {
        if (!json.TryGetProperty(propertyName, out JsonElement value))
            return false;
        return value.ValueKind is JsonValueKind.True or JsonValueKind.False && value.GetBoolean();
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

    private static T? FindControl<T>(Control root) where T : Control
    {
        if (root is T match)
            return match;
        foreach (Control child in root.Controls)
        {
            T? found = FindControl<T>(child);
            if (found is not null)
                return found;
        }
        return null;
    }

    private static Button CreateButton(string text)
    {
        Button button = new()
        {
            Width = 210,
            Height = 42,
            Margin = new Padding(6),
            Text = text,
            BackColor = Surface,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = Neon;
        return button;
    }

    private static Form CreateDialog(string title, Size size) => new()
    {
        Text = title,
        StartPosition = FormStartPosition.CenterParent,
        Size = size,
        MinimumSize = new Size(760, 480),
        BackColor = Background,
        ForeColor = Color.White,
        Font = new Font("Segoe UI", 10F)
    };

    private static DataGridView CreateGrid() => new()
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
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false
    };

    private sealed class ProgressDialog10 : Form
    {
        private readonly Label _label;
        private readonly CancellationTokenSource _cancellation = new();

        public ProgressDialog10(string title)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(660, 190);
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
            Button cancel = CreateButton("ANNULLA");
            cancel.Dock = DockStyle.Bottom;
            cancel.Click += (_, _) => _cancellation.Cancel();
            Controls.Add(_label);
            Controls.Add(cancel);
        }

        public CancellationToken Token => _cancellation.Token;

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
