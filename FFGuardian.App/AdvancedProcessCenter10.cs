using System.Diagnostics;
using System.Text;
using FFGuardian.Engine10;

namespace FFGuardian;

internal static class AdvancedProcessCenter10
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
            .FirstOrDefault(item => string.Equals(item.Text, "ATTIVITÀ", StringComparison.OrdinalIgnoreCase));
        page ??= tabs?.TabPages.Cast<TabPage>()
            .FirstOrDefault(item => string.Equals(item.Text, "SCANSIONE", StringComparison.OrdinalIgnoreCase));
        if (page is null || FindButtons(page).Any(button => button.Text == "MONITOR PROCESSI"))
            return;

        FlowLayoutPanel? panel = FindControl<FlowLayoutPanel>(page);
        if (panel is null)
            return;

        Button open = CreateButton("MONITOR PROCESSI");
        open.Click += async (_, _) => await ShowAsync(form, engine);
        panel.Controls.Add(open);
    }

    private static async Task ShowAsync(IWin32Window owner, FFGuardianEngine10 engine)
    {
        using Form dialog = new()
        {
            Text = "FF GUARDIAN — MONITOR PROCESSI",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(1380, 780),
            MinimumSize = new Size(980, 620),
            BackColor = Background,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F)
        };

        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(12),
            BackColor = Background
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));

        FlowLayoutPanel filters = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(10),
            WrapContents = false
        };
        TextBox search = new()
        {
            Width = 360,
            PlaceholderText = "Cerca nome, PID o percorso...",
            BackColor = Background,
            ForeColor = Color.White
        };
        CheckBox suspiciousOnly = new()
        {
            AutoSize = true,
            Text = "Mostra solo sospetti",
            ForeColor = Color.White,
            BackColor = Surface,
            Margin = new Padding(18, 7, 8, 0)
        };
        Label status = new()
        {
            AutoSize = true,
            Text = "Caricamento processi...",
            ForeColor = Color.Gainsboro,
            Margin = new Padding(18, 7, 8, 0)
        };
        filters.Controls.Add(search);
        filters.Controls.Add(suspiciousOnly);
        filters.Controls.Add(status);

        DataGridView grid = CreateGrid();
        grid.Columns.Add("Risk", "RISCHIO");
        grid.Columns.Add("Name", "PROCESSO");
        grid.Columns.Add("Pid", "PID");
        grid.Columns.Add("Parent", "PADRE");
        grid.Columns.Add("Cpu", "CPU");
        grid.Columns.Add("Ram", "RAM MB");
        grid.Columns.Add("Connections", "CONNESSIONI");
        grid.Columns.Add("Verdict", "ESITO");
        grid.Columns.Add("Hash", "SHA-256");
        grid.Columns.Add("Path", "PERCORSO");
        grid.Columns[0].FillWeight = 45;
        grid.Columns[1].FillWeight = 80;
        grid.Columns[2].FillWeight = 45;
        grid.Columns[3].FillWeight = 55;
        grid.Columns[4].FillWeight = 45;
        grid.Columns[5].FillWeight = 55;
        grid.Columns[6].FillWeight = 60;
        grid.Columns[7].FillWeight = 75;
        grid.Columns[8].FillWeight = 130;
        grid.Columns[9].FillWeight = 210;

        List<ProcessRow10> rows = [];

        void RefreshGrid()
        {
            string query = search.Text.Trim();
            grid.Rows.Clear();
            foreach (ProcessRow10 row in rows
                .Where(row => !suspiciousOnly.Checked || row.RiskScore >= 40)
                .Where(row => string.IsNullOrWhiteSpace(query) ||
                    row.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    row.ProcessId.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    row.Path.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(row => row.RiskScore)
                .ThenBy(row => row.Name))
            {
                int index = grid.Rows.Add(
                    row.RiskScore,
                    row.Name,
                    row.ProcessId,
                    row.ParentProcessId,
                    row.CpuSeconds.ToString("N1"),
                    row.WorkingSetMb.ToString("N1"),
                    row.ConnectionCount,
                    row.Verdict,
                    row.Sha256,
                    row.Path);
                grid.Rows[index].Tag = row;
            }
            status.Text = $"Processi: {rows.Count} — sospetti: {rows.Count(row => row.RiskScore >= 40)}";
        }

        search.TextChanged += (_, _) => RefreshGrid();
        suspiciousOnly.CheckedChanged += (_, _) => RefreshGrid();

        FlowLayoutPanel commands = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Surface,
            Padding = new Padding(8)
        };
        Button close = CreateButton("CHIUDI");
        Button terminate = CreateButton("TERMINA PROCESSO");
        Button analyze = CreateButton("ANALIZZA SELEZIONATO");
        Button refresh = CreateButton("AGGIORNA ELENCO");
        Button export = CreateButton("ESPORTA REPORT");
        commands.Controls.Add(close);
        commands.Controls.Add(terminate);
        commands.Controls.Add(analyze);
        commands.Controls.Add(refresh);
        commands.Controls.Add(export);

        async Task LoadAsync()
        {
            refresh.Enabled = false;
            status.Text = "Analisi processi in corso...";
            try
            {
                rows = await BuildRowsAsync(engine);
                RefreshGrid();
            }
            finally
            {
                refresh.Enabled = true;
            }
        }

        close.Click += (_, _) => dialog.Close();
        refresh.Click += async (_, _) => await LoadAsync();
        analyze.Click += async (_, _) =>
        {
            ProcessRow10? row = grid.CurrentRow?.Tag as ProcessRow10;
            if (row is null || string.IsNullOrWhiteSpace(row.Path) || !File.Exists(row.Path))
            {
                MessageBox.Show(dialog, "Seleziona un processo con un file eseguibile accessibile.",
                    "FF GUARDIAN 10", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            FileScanResult10 result = await engine.ScanFileAsync(row.Path);
            MessageBox.Show(dialog,
                $"Processo: {row.Name} ({row.ProcessId})\nPercorso: {row.Path}\n\nEsito: {result.Verdict}\nRilevamento: {result.DetectionName}\nSHA-256: {result.Sha256}\nConfidenza: {result.Confidence}",
                "FF GUARDIAN 10 — Analisi processo", MessageBoxButtons.OK,
                result.Verdict is ThreatVerdict10.Malicious or ThreatVerdict10.Suspicious
                    ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        };
        terminate.Click += (_, _) =>
        {
            ProcessRow10? row = grid.CurrentRow?.Tag as ProcessRow10;
            if (row is null)
                return;
            if (row.ProcessId is 0 or 4 || row.ProcessId == Environment.ProcessId)
            {
                MessageBox.Show(dialog, "Questo processo non può essere terminato da FFGuardian.",
                    "Operazione bloccata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (MessageBox.Show(dialog,
                $"Terminare il processo {row.Name} (PID {row.ProcessId})?\n\nQuesta operazione può causare perdita di dati non salvati.",
                "Conferma terminazione", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            try
            {
                using Process process = Process.GetProcessById(row.ProcessId);
                process.Kill(entireProcessTree: false);
                process.WaitForExit(5000);
                StabilityCoordinator82.WriteInformationLog($"Processo terminato con conferma: {row.Name} PID {row.ProcessId}");
                _ = LoadAsync();
            }
            catch (Exception ex)
            {
                StabilityCoordinator82.WriteStabilityLog(ex);
                MessageBox.Show(dialog, ex.Message, "Terminazione non riuscita",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
        export.Click += (_, _) =>
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FF Guardian Reports");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, $"FFGuardian-Processi-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
            StringBuilder csv = new("Rischio;Processo;PID;Padre;CPU sec;RAM MB;Connessioni;Esito;SHA-256;Percorso\r\n");
            foreach (ProcessRow10 row in rows.OrderByDescending(item => item.RiskScore))
                csv.AppendLine(string.Join(';', row.RiskScore, Escape(row.Name), row.ProcessId, row.ParentProcessId,
                    row.CpuSeconds.ToString("F1"), row.WorkingSetMb.ToString("F1"), row.ConnectionCount,
                    Escape(row.Verdict), Escape(row.Sha256), Escape(row.Path)));
            File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true));
            MessageBox.Show(dialog, $"Report esportato in:\n{path}", "FF GUARDIAN 10",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        root.Controls.Add(filters, 0, 0);
        root.Controls.Add(grid, 0, 1);
        root.Controls.Add(commands, 0, 2);
        dialog.Controls.Add(root);
        dialog.Shown += async (_, _) => await LoadAsync();
        dialog.ShowDialog(owner);
    }

    private static async Task<List<ProcessRow10>> BuildRowsAsync(FFGuardianEngine10 engine)
    {
        Dictionary<int, int> connections = GetConnectionCounts();
        List<ProcessRow10> rows = [];
        foreach (Process process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    string path = process.MainModule?.FileName ?? string.Empty;
                    string verdict = "NON ANALIZZATO";
                    string sha256 = string.Empty;
                    int risk = IsSuspiciousPath(path) ? 35 : 0;
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    {
                        FileScanResult10 scan = await engine.ScanFileAsync(path);
                        verdict = scan.Verdict.ToString().ToUpperInvariant();
                        sha256 = scan.Sha256;
                        risk += scan.Verdict switch
                        {
                            ThreatVerdict10.Malicious => 80,
                            ThreatVerdict10.Suspicious => 50,
                            ThreatVerdict10.Unknown => 10,
                            _ => 0
                        };
                    }
                    int connectionCount = connections.GetValueOrDefault(process.Id);
                    if (connectionCount > 20) risk += 10;
                    rows.Add(new ProcessRow10(
                        process.Id,
                        GetParentProcessId(process.Id),
                        process.ProcessName,
                        path,
                        process.TotalProcessorTime.TotalSeconds,
                        process.WorkingSet64 / 1024d / 1024d,
                        connectionCount,
                        Math.Clamp(risk, 0, 100),
                        verdict,
                        sha256));
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
                {
                }
            }
        }
        return rows;
    }

    private static bool IsSuspiciousPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        string full = path.ToLowerInvariant();
        return full.Contains("\\temp\\") || full.Contains("\\appdata\\local\\temp\\") ||
               full.Contains("\\downloads\\") || full.EndsWith(".scr", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetParentProcessId(int processId)
    {
        try
        {
            using Process query = Process.Start(new ProcessStartInfo("powershell.exe",
                $"-NoProfile -NonInteractive -Command \"(Get-CimInstance Win32_Process -Filter 'ProcessId={processId}').ParentProcessId\"")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            })!;
            string output = query.StandardOutput.ReadToEnd().Trim();
            query.WaitForExit(2000);
            return int.TryParse(output, out int value) ? value : 0;
        }
        catch { return 0; }
    }

    private static Dictionary<int, int> GetConnectionCounts()
    {
        Dictionary<int, int> result = [];
        try
        {
            using Process netstat = Process.Start(new ProcessStartInfo("netstat.exe", "-ano")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            })!;
            while (!netstat.StandardOutput.EndOfStream)
            {
                string? line = netstat.StandardOutput.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;
                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 4 && int.TryParse(parts[^1], out int pid))
                    result[pid] = result.GetValueOrDefault(pid) + 1;
            }
            netstat.WaitForExit(3000);
        }
        catch { }
        return result;
    }

    private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

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

    private static Button CreateButton(string text)
    {
        Button button = new()
        {
            Width = 220,
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

    private sealed record ProcessRow10(
        int ProcessId,
        int ParentProcessId,
        string Name,
        string Path,
        double CpuSeconds,
        double WorkingSetMb,
        int ConnectionCount,
        int RiskScore,
        string Verdict,
        string Sha256);
}
