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
            if (!File.Exists(SettingsPath))
                return new QuarantineProfessionalSettings10();

            QuarantineProfessionalSettings10? value = JsonSerializer.Deserialize<QuarantineProfessionalSettings10>(
                File.ReadAllText(SettingsPath));
            if (value is null)
                return new QuarantineProfessionalSettings10();

            value.RetentionDays = Math.Clamp(value.RetentionDays, 30, 365);
            return value;
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            return new QuarantineProfessionalSettings10();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this,
            new JsonSerializerOptions { WriteIndented = true }));
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
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "FF Guardian Reports");

    public static void Attach(IndependentMainForm100 form, FFGuardianEngine10 engine)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(engine);

        TabControl? tabs = FindControl<TabControl>(form);
        TabPage? page = tabs?.TabPages.Cast<TabPage>()
            .FirstOrDefault(item => string.Equals(item.Text, "RECUPERO", StringComparison.OrdinalIgnoreCase));
        if (page is null || FindButtons(page).Any(button => button.Text == "QUARANTENA PROFESSIONALE"))
            return;

        FlowLayoutPanel? panel = FindControl<FlowLayoutPanel>(page);
        if (panel is null)
            return;

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
                if (item.CreatedUtc >= cutoff)
                    continue;
                if (!item.Restored && !settings.AutoCleanupRestoredItems)
                    continue;
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

        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            BackColor = Background,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));

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
            PlaceholderText = "Cerca percorso, rilevamento, hash o ID...",
            BackColor = Background,
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        ComboBox state = new()
        {
            Width = 180,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Background,
            ForeColor = Color.White
        };
        state.Items.AddRange(["TUTTI", "ATTIVI", "RIPRISTINATI"]);
        state.SelectedIndex = 0;
        NumericUpDown retention = new()
        {
            Minimum = 30,
            Maximum = 365,
            Value = settings.RetentionDays,
            Width = 80,
            BackColor = Background,
            ForeColor = Color.White
        };
        CheckBox cleanupRestored = new()
        {
            AutoSize = true,
            Text = "Pulizia automatica ripristinati",
            Checked = settings.AutoCleanupRestoredItems,
            ForeColor = Color.White,
            BackColor = Surface,
            Margin = new Padding(14, 6, 8, 0)
        };
        filters.Controls.Add(search);
        filters.Controls.Add(state);
        filters.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "Conservazione giorni:",
            ForeColor = Color.White,
            Margin = new Padding(14, 6, 4, 0)
        });
        filters.Controls.Add(retention);
        filters.Controls.Add(cleanupRestored);

        DataGridView grid = CreateGrid();
        grid.Columns.Add("Id", "ID");
        grid.Columns.Add("Status", "STATO");
        grid.Columns.Add("Detection", "RILEVAMENTO");
        grid.Columns.Add("Created", "DATA");
        grid.Columns.Add("Hash", "SHA-256");
        grid.Columns.Add("Path", "PERCORSO ORIGINALE");
        grid.Columns[0].FillWeight = 60;
        grid.Columns[1].FillWeight = 55;
        grid.Columns[2].FillWeight = 95;
        grid.Columns[3].FillWeight = 70;
        grid.Columns[4].FillWeight = 115;
        grid.Columns[5].FillWeight = 180;

        List<QuarantineItem10> items = LoadItems();

        void RefreshGrid()
        {
            string query = search.Text.Trim();
            string selectedState = Convert.ToString(state.SelectedItem) ?? "TUTTI";
            grid.Rows.Clear();

            foreach (QuarantineItem10 item in items
                .Where(item => selectedState == "TUTTI" ||
                    selectedState == "ATTIVI" && !item.Restored ||
                    selectedState == "RIPRISTINATI" && item.Restored)
                .Where(item => string.IsNullOrWhiteSpace(query) ||
                    item.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    item.OriginalPath.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    item.DetectionName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    item.Sha256.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.CreatedUtc))
            {
                int rowIndex = grid.Rows.Add(
                    item.Id,
                    item.Restored ? "RIPRISTINATO" : "IN QUARANTENA",
                    item.DetectionName,
                    item.CreatedUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                    item.Sha256,
                    item.OriginalPath);
                grid.Rows[rowIndex].Tag = item;
            }
        }

        search.TextChanged += (_, _) => RefreshGrid();
        state.SelectedIndexChanged += (_, _) => RefreshGrid();
        RefreshGrid();

        FlowLayoutPanel commands = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Surface,
            Padding = new Padding(8)
        };
        Button close = CreateButton("CHIUDI");
        Button delete = CreateButton("ELIMINA DEFINITIVAMENTE");
        Button restore = CreateButton("RIPRISTINA E RIANALIZZA");
        Button falsePositive = CreateButton("SEGNALA FALSO POSITIVO");
        Button export = CreateButton("ESPORTA REPORT");
        Button savePolicy = CreateButton("SALVA CONSERVAZIONE");

        close.Click += (_, _) => dialog.Close();
        savePolicy.Click += (_, _) =>
        {
            settings.RetentionDays = decimal.ToInt32(retention.Value);
            settings.AutoCleanupRestoredItems = cleanupRestored.Checked;
            settings.Save();
            RunRetentionCleanup();
            MessageBox.Show(dialog, "Politica di conservazione salvata.", "FF GUARDIAN 10",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        restore.Click += async (_, _) =>
        {
            QuarantineItem10? item = SelectedItem(grid);
            if (item is null)
            {
                ShowSelectMessage(dialog);
                return;
            }
            if (item.Restored)
            {
                MessageBox.Show(dialog, "Questo elemento risulta già ripristinato.", "FF GUARDIAN 10",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(dialog,
                "Ripristinare il file nel percorso originale e analizzarlo immediatamente?",
                "Conferma ripristino", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            restore.Enabled = false;
            try
            {
                await engine.RestoreQuarantineAsync(item.Id);
                FileScanResult10 scan = await engine.ScanFileAsync(item.OriginalPath);
                item.Restored = true;
                RefreshGrid();
                MessageBox.Show(dialog,
                    $"Ripristino completato.\n\nEsito nuova scansione: {scan.Verdict}\nRilevamento: {scan.DetectionName}\nConfidenza: {scan.Confidence}",
                    "FF GUARDIAN 10 — Ripristino verificato",
                    MessageBoxButtons.OK,
                    scan.Verdict is ThreatVerdict10.Malicious or ThreatVerdict10.Suspicious
                        ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
            finally
            {
                restore.Enabled = true;
            }
        };

        delete.Click += (_, _) =>
        {
            QuarantineItem10? item = SelectedItem(grid);
            if (item is null)
            {
                ShowSelectMessage(dialog);
                return;
            }

            using Form confirm = CreateDeleteConfirmation(item);
            if (confirm.ShowDialog(dialog) != DialogResult.OK)
                return;

            DeleteItemFolder(item);
            items.Remove(item);
            RefreshGrid();
            StabilityCoordinator82.WriteInformationLog(
                $"Elemento eliminato definitivamente dalla quarantena: {item.Id} — {item.OriginalPath}");
        };

        falsePositive.Click += (_, _) =>
        {
            QuarantineItem10? item = SelectedItem(grid);
            if (item is null)
            {
                ShowSelectMessage(dialog);
                return;
            }
            WriteFalsePositiveReport(item);
            MessageBox.Show(dialog,
                "Segnalazione salvata nel registro locale dei falsi positivi.",
                "FF GUARDIAN 10", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        export.Click += (_, _) =>
        {
            Directory.CreateDirectory(ReportsRoot);
            string path = Path.Combine(ReportsRoot,
                $"FFGuardian-Quarantena-{DateTime.Now:yyyyMMdd-HHmmss}.csv");
            ExportCsv(path, items);
            MessageBox.Show(dialog, $"Rapporto esportato in:\n{path}", "FF GUARDIAN 10",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        commands.Controls.Add(close);
        commands.Controls.Add(delete);
        commands.Controls.Add(restore);
        commands.Controls.Add(falsePositive);
        commands.Controls.Add(export);
        commands.Controls.Add(savePolicy);

        root.Controls.Add(filters, 0, 0);
        root.Controls.Add(grid, 0, 1);
        root.Controls.Add(commands, 0, 2);
        dialog.Controls.Add(root);
        dialog.ShowDialog(owner);
    }

    private static List<QuarantineItem10> LoadItems()
    {
        List<QuarantineItem10> items = [];
        if (!Directory.Exists(QuarantineRoot))
            return items;

        foreach (string metadataPath in Directory.EnumerateFiles(
            QuarantineRoot, "metadata.json", SearchOption.AllDirectories))
        {
            try
            {
                QuarantineRecord10? record = JsonSerializer.Deserialize<QuarantineRecord10>(
                    File.ReadAllText(metadataPath));
                if (record is null)
                    continue;
                string folder = Path.GetDirectoryName(metadataPath)!;
                items.Add(new QuarantineItem10(
                    record.Id,
                    record.OriginalPath,
                    record.Sha256,
                    record.DetectionName,
                    record.CreatedUtc,
                    record.Restored,
                    folder));
            }
            catch (Exception ex)
            {
                StabilityCoordinator82.WriteStabilityLog(ex);
            }
        }
        return items;
    }

    private static void DeleteItemFolder(QuarantineItem10 item)
    {
        string fullRoot = Path.GetFullPath(QuarantineRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string fullFolder = Path.GetFullPath(item.FolderPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullFolder.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Percorso quarantena non valido.");
        if (File.Exists(Path.Combine(item.FolderPath, "metadata.json")))
            Directory.Delete(item.FolderPath, recursive: true);
    }

    private static void WriteFalsePositiveReport(QuarantineItem10 item)
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FF Guardian", "Engine10", "FalsePositives");
        Directory.CreateDirectory(folder);
        object report = new
        {
            CreatedUtc = DateTime.UtcNow,
            item.Id,
            item.OriginalPath,
            item.Sha256,
            item.DetectionName,
            item.CreatedUtc
        };
        File.AppendAllText(Path.Combine(folder, "reports.jsonl"),
            JsonSerializer.Serialize(report) + Environment.NewLine);
    }

    private static void ExportCsv(string path, IEnumerable<QuarantineItem10> items)
    {
        StringBuilder csv = new();
        csv.AppendLine("ID;Stato;Rilevamento;Data UTC;SHA-256;Percorso originale");
        foreach (QuarantineItem10 item in items.OrderByDescending(item => item.CreatedUtc))
        {
            csv.Append(Escape(item.Id)).Append(';')
                .Append(Escape(item.Restored ? "RIPRISTINATO" : "IN QUARANTENA")).Append(';')
                .Append(Escape(item.DetectionName)).Append(';')
                .Append(Escape(item.CreatedUtc.ToString("O"))).Append(';')
                .Append(Escape(item.Sha256)).Append(';')
                .AppendLine(Escape(item.OriginalPath));
        }
        File.WriteAllText(path, csv.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }

    private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";

    private static Form CreateDeleteConfirmation(QuarantineItem10 item)
    {
        Form dialog = new()
        {
            Text = "Conferma eliminazione definitiva",
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(620, 260),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            BackColor = Background,
            ForeColor = Color.White
        };
        Label label = new()
        {
            Dock = DockStyle.Top,
            Height = 110,
            Padding = new Padding(18),
            Text = $"L'operazione è irreversibile.\n{item.OriginalPath}\n\nScrivi ELIMINA per confermare:",
            ForeColor = Color.White
        };
        TextBox text = new()
        {
            Dock = DockStyle.Top,
            Margin = new Padding(18),
            BackColor = Surface,
            ForeColor = Color.White
        };
        FlowLayoutPanel buttons = new()
        {
            Dock = DockStyle.Bottom,
            Height = 58,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
            BackColor = Surface
        };
        Button confirm = CreateButton("ELIMINA");
        confirm.Enabled = false;
        Button cancel = CreateButton("ANNULLA");
        text.TextChanged += (_, _) => confirm.Enabled =
            string.Equals(text.Text.Trim(), "ELIMINA", StringComparison.OrdinalIgnoreCase);
        confirm.Click += (_, _) => { dialog.DialogResult = DialogResult.OK; dialog.Close(); };
        cancel.Click += (_, _) => { dialog.DialogResult = DialogResult.Cancel; dialog.Close(); };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(confirm);
        dialog.Controls.Add(buttons);
        dialog.Controls.Add(text);
        dialog.Controls.Add(label);
        return dialog;
    }

    private static QuarantineItem10? SelectedItem(DataGridView grid) => grid.CurrentRow?.Tag as QuarantineItem10;

    private static void ShowSelectMessage(IWin32Window owner) => MessageBox.Show(owner,
        "Seleziona prima un elemento della quarantena.", "FF GUARDIAN 10",
        MessageBoxButtons.OK, MessageBoxIcon.Information);

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
        if (root is Button button)
            yield return button;
        foreach (Control child in root.Controls)
            foreach (Button found in FindButtons(child))
                yield return found;
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

    private sealed class QuarantineItem10(
        string id,
        string originalPath,
        string sha256,
        string detectionName,
        DateTime createdUtc,
        bool restored,
        string folderPath)
    {
        public string Id { get; } = id;
        public string OriginalPath { get; } = originalPath;
        public string Sha256 { get; } = sha256;
        public string DetectionName { get; } = detectionName;
        public DateTime CreatedUtc { get; } = createdUtc;
        public bool Restored { get; set; } = restored;
        public string FolderPath { get; } = folderPath;
    }
}
