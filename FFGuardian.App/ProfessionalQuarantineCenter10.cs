using System.Text;
using System.Text.Json;
using FFGuardian.Engine10;

namespace FFGuardian;

internal sealed class QuarantineProfessionalSettings10
{
    public int RetentionDays { get; set; } = 90;
    public bool AutoCleanupRestoredItems { get; set; } = true;

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FF Guardian", "Engine10", "quarantine-professional.json");

    public static QuarantineProfessionalSettings10 Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new();
            QuarantineProfessionalSettings10? value = JsonSerializer.Deserialize<QuarantineProfessionalSettings10>(File.ReadAllText(SettingsPath));
            if (value is null) return new();
            value.RetentionDays = Math.Clamp(value.RetentionDays, 30, 365);
            return value;
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            return new();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}

internal static class ProfessionalQuarantineCenter10
{
    private static readonly Color Background = Color.FromArgb(3, 8, 12);
    private static readonly Color Surface = Color.FromArgb(17, 31, 39);
    private static readonly Color Neon = Color.FromArgb(160, 255, 0);

    private static string QuarantineRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "FF Guardian", "Engine10", "Quarantine");

    private static string ReportsRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FF Guardian Reports");

    public static void Attach(IndependentMainForm100 form, FFGuardianEngine10 engine)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(engine);
        TabControl? tabs = FindControl<TabControl>(form);
        TabPage? page = tabs?.TabPages.Cast<TabPage>().FirstOrDefault(p =>
            string.Equals(p.Text, "RECUPERO", StringComparison.OrdinalIgnoreCase));
        FlowLayoutPanel? panel = page is null ? null : FindControl<FlowLayoutPanel>(page);
        if (panel is null || FindButtons(page!).Any(b => b.Text == "QUARANTENA PROFESSIONALE")) return;

        Button open = CreateButton("QUARANTENA PROFESSIONALE");
        open.Click += async (_, _) => await ShowManagerAsync(form, engine);
        panel.Controls.Add(open);
    }

    public static void RunRetentionCleanup()
    {
        try
        {
            QuarantineProfessionalSettings10 settings = QuarantineProfessionalSettings10.Load();
            DateTime cutoff = DateTime.UtcNow.AddDays(-settings.RetentionDays);
            foreach (QuarantineItem10 item in LoadItems())
            {
                if (item.QuarantinedUtc >= cutoff) continue;
                if (!item.Restored || !settings.AutoCleanupRestoredItems) continue;
                DeleteItemFolder(item);
            }
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
        }
    }

    private static async Task ShowManagerAsync(IWin32Window owner, FFGuardianEngine10 engine)
    {
        Directory.CreateDirectory(QuarantineRoot);
        QuarantineProfessionalSettings10 settings = QuarantineProfessionalSettings10.Load();
        List<QuarantineItem10> items = LoadItems();

        using Form dialog = new()
        {
            Text = "FF GUARDIAN — QUARANTENA PROFESSIONALE",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(1280, 760),
            MinimumSize = new Size(940, 600),
            BackColor = Background,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F)
        };

        TableLayoutPanel root = new() { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(12), BackColor = Background };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));

        FlowLayoutPanel filters = new() { Dock = DockStyle.Fill, BackColor = Surface, Padding = new Padding(10), WrapContents = false };
        TextBox search = new() { Width = 350, PlaceholderText = "Cerca percorso, rilevamento, hash o ID...", BackColor = Background, ForeColor = Color.White };
        ComboBox state = new() { Width = 170, DropDownStyle = ComboBoxStyle.DropDownList, BackColor = Background, ForeColor = Color.White };
        state.Items.AddRange(["TUTTI", "ATTIVI", "RIPRISTINATI"]);
        state.SelectedIndex = 0;
        NumericUpDown retention = new() { Minimum = 30, Maximum = 365, Value = settings.RetentionDays, Width = 80, BackColor = Background, ForeColor = Color.White };
        CheckBox cleanup = new() { AutoSize = true, Text = "Pulizia automatica ripristinati", Checked = settings.AutoCleanupRestoredItems, ForeColor = Color.White, BackColor = Surface };
        filters.Controls.AddRange([search, state, new Label { AutoSize = true, Text = "Conservazione giorni:", ForeColor = Color.White, Margin = new Padding(14, 6, 4, 0) }, retention, cleanup]);

        DataGridView grid = CreateGrid();
        grid.Columns.Add("Id", "ID");
        grid.Columns.Add("Status", "STATO");
        grid.Columns.Add("Detection", "RILEVAMENTO");
        grid.Columns.Add("Created", "DATA");
        grid.Columns.Add("Hash", "SHA-256");
        grid.Columns.Add("Path", "PERCORSO ORIGINALE");

        void RefreshGrid()
        {
            string query = search.Text.Trim();
            string selected = Convert.ToString(state.SelectedItem) ?? "TUTTI";
            grid.Rows.Clear();
            foreach (QuarantineItem10 item in items
                .Where(i => selected == "TUTTI" || selected == "ATTIVI" && !i.Restored || selected == "RIPRISTINATI" && i.Restored)
                .Where(i => string.IsNullOrWhiteSpace(query) || i.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    i.OriginalPath.Contains(query, StringComparison.OrdinalIgnoreCase) || i.DetectionName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    i.Sha256.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(i => i.QuarantinedUtc))
            {
                int row = grid.Rows.Add(item.Id, item.Restored ? "RIPRISTINATO" : "IN QUARANTENA", item.DetectionName,
                    item.QuarantinedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"), item.Sha256, item.OriginalPath);
                grid.Rows[row].Tag = item;
            }
        }

        search.TextChanged += (_, _) => RefreshGrid();
        state.SelectedIndexChanged += (_, _) => RefreshGrid();
        RefreshGrid();

        FlowLayoutPanel commands = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, BackColor = Surface, Padding = new Padding(8) };
        Button close = CreateButton("CHIUDI");
        Button delete = CreateButton("ELIMINA DEFINITIVAMENTE");
        Button restore = CreateButton("RIPRISTINA E RIANALIZZA");
        Button falsePositive = CreateButton("SEGNALA FALSO POSITIVO");
        Button export = CreateButton("ESPORTA REPORT");
        Button save = CreateButton("SALVA CONSERVAZIONE");

        close.Click += (_, _) => dialog.Close();
        save.Click += (_, _) =>
        {
            settings.RetentionDays = decimal.ToInt32(retention.Value);
            settings.AutoCleanupRestoredItems = cleanup.Checked;
            settings.Save();
            RunRetentionCleanup();
            MessageBox.Show(dialog, "Politica di conservazione salvata.", "FF GUARDIAN 10", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        restore.Click += async (_, _) =>
        {
            QuarantineItem10? item = SelectedItem(grid);
            if (item is null) { ShowSelectMessage(dialog); return; }
            if (item.Restored) { MessageBox.Show(dialog, "Elemento già ripristinato."); return; }
            if (MessageBox.Show(dialog, "Ripristinare e analizzare immediatamente il file?", "Conferma", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            restore.Enabled = false;
            try
            {
                await engine.RestoreQuarantineAsync(item.Id);
                FileScanResult10 result = await engine.ScanFileAsync(item.OriginalPath);
                item.Restored = true;
                RefreshGrid();
                MessageBox.Show(dialog, $"Ripristino completato.\nEsito: {result.Verdict}\nRilevamento: {result.DetectionName}",
                    "FF GUARDIAN 10", MessageBoxButtons.OK,
                    result.Verdict is ThreatVerdict10.Malicious or ThreatVerdict10.Suspicious ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
            finally { restore.Enabled = true; }
        };

        delete.Click += (_, _) =>
        {
            QuarantineItem10? item = SelectedItem(grid);
            if (item is null) { ShowSelectMessage(dialog); return; }
            if (MessageBox.Show(dialog, $"Eliminare definitivamente?\n{item.OriginalPath}", "Operazione irreversibile", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            DeleteItemFolder(item);
            items.Remove(item);
            RefreshGrid();
        };

        falsePositive.Click += (_, _) =>
        {
            QuarantineItem10? item = SelectedItem(grid);
            if (item is null) { ShowSelectMessage(dialog); return; }
            WriteFalsePositiveReport(item);
            MessageBox.Show(dialog, "Segnalazione falso positivo salvata.", "FF GUARDIAN 10", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        export.Click += (_, _) =>
        {
            Directory.CreateDirectory(ReportsRoot);
            string path = Path.Combine(ReportsRoot, $"FFGuardian-Quarantena-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
            ExportCsv(path, items);
            MessageBox.Show(dialog, $"Rapporto esportato in:\n{path}");
        };

        commands.Controls.AddRange([close, delete, restore, falsePositive, export, save]);
        root.Controls.Add(filters, 0, 0);
        root.Controls.Add(grid, 0, 1);
        root.Controls.Add(commands, 0, 2);
        dialog.Controls.Add(root);
        dialog.ShowDialog(owner);
    }

    private static List<QuarantineItem10> LoadItems()
    {
        List<QuarantineItem10> items = [];
        if (!Directory.Exists(QuarantineRoot)) return items;
        foreach (string metadataPath in Directory.EnumerateFiles(QuarantineRoot, "metadata.json", SearchOption.AllDirectories))
        {
            try
            {
                QuarantineRecord10? record = JsonSerializer.Deserialize<QuarantineRecord10>(File.ReadAllText(metadataPath));
                if (record is null) continue;
                items.Add(new(record.Id, record.OriginalPath, record.Sha256, record.DetectionName,
                    record.QuarantinedUtc, record.Restored, Path.GetDirectoryName(metadataPath)!));
            }
            catch (Exception ex) { StabilityCoordinator82.WriteStabilityLog(ex); }
        }
        return items;
    }

    private static void DeleteItemFolder(QuarantineItem10 item)
    {
        string root = Path.GetFullPath(QuarantineRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string folder = Path.GetFullPath(item.FolderPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!folder.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Percorso quarantena non valido.");
        if (File.Exists(Path.Combine(item.FolderPath, "metadata.json"))) Directory.Delete(item.FolderPath, true);
    }

    private static void WriteFalsePositiveReport(QuarantineItem10 item)
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FF Guardian", "Engine10", "FalsePositives");
        Directory.CreateDirectory(folder);
        object report = new
        {
            ReportedUtc = DateTime.UtcNow,
            item.Id,
            item.OriginalPath,
            item.Sha256,
            item.DetectionName,
            QuarantinedUtc = item.QuarantinedUtc
        };
        File.AppendAllText(Path.Combine(folder, "reports.jsonl"), JsonSerializer.Serialize(report) + Environment.NewLine);
    }

    private static void ExportCsv(string path, IEnumerable<QuarantineItem10> items)
    {
        StringBuilder csv = new();
        csv.AppendLine("ID;Stato;Rilevamento;Data UTC;SHA-256;Percorso originale");
        foreach (QuarantineItem10 item in items.OrderByDescending(i => i.QuarantinedUtc))
            csv.AppendLine(string.Join(';', Escape(item.Id), Escape(item.Restored ? "RIPRISTINATO" : "IN QUARANTENA"),
                Escape(item.DetectionName), Escape(item.QuarantinedUtc.ToString("O")), Escape(item.Sha256), Escape(item.OriginalPath)));
        File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true));
    }

    private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
    private static QuarantineItem10? SelectedItem(DataGridView grid) => grid.CurrentRow?.Tag as QuarantineItem10;
    private static void ShowSelectMessage(IWin32Window owner) => MessageBox.Show(owner, "Seleziona prima un elemento.", "FF GUARDIAN 10");

    private static DataGridView CreateGrid() => new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Background, ForeColor = Color.White,
        GridColor = Color.FromArgb(58, 76, 84), RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false
    };

    private static Button CreateButton(string text)
    {
        Button button = new() { Width = 220, Height = 42, Margin = new Padding(6), Text = text, BackColor = Surface,
            ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9F, FontStyle.Bold), Cursor = Cursors.Hand };
        button.FlatAppearance.BorderColor = Neon;
        return button;
    }

    private static IEnumerable<Button> FindButtons(Control root)
    {
        if (root is Button button) yield return button;
        foreach (Control child in root.Controls) foreach (Button found in FindButtons(child)) yield return found;
    }

    private static T? FindControl<T>(Control root) where T : Control
    {
        if (root is T match) return match;
        foreach (Control child in root.Controls) { T? found = FindControl<T>(child); if (found is not null) return found; }
        return null;
    }

    private sealed class QuarantineItem10(string id, string originalPath, string sha256, string detectionName,
        DateTime quarantinedUtc, bool restored, string folderPath)
    {
        public string Id { get; } = id;
        public string OriginalPath { get; } = originalPath;
        public string Sha256 { get; } = sha256;
        public string DetectionName { get; } = detectionName;
        public DateTime QuarantinedUtc { get; } = quarantinedUtc;
        public bool Restored { get; set; } = restored;
        public string FolderPath { get; } = folderPath;
    }
}
