using System.Diagnostics;
using System.Text;

namespace FFGuardian;

internal sealed class IndependentMainForm100 : Form
{
    private static readonly Color Background = Color.FromArgb(3, 8, 12);
    private static readonly Color Surface = Color.FromArgb(12, 22, 28);
    private static readonly Color Neon = Color.FromArgb(160, 255, 0);
    private static readonly Color Muted = Color.FromArgb(198, 205, 210);

    private readonly IndependentSecurityEngine100 _engine = new();
    private readonly Label _status = new()
    {
        Dock = DockStyle.Bottom,
        Height = 36,
        BackColor = Color.FromArgb(5, 12, 17),
        ForeColor = Muted,
        Padding = new Padding(16, 0, 0, 0),
        TextAlign = ContentAlignment.MiddleLeft,
        Text = "Motore indipendente pronto."
    };
    private readonly Label _score = new()
    {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        Font = new Font("Segoe UI", 26F, FontStyle.Bold),
        ForeColor = Neon,
        Text = "--/100"
    };
    private readonly DataGridView _results = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = Background,
        BorderStyle = BorderStyle.None,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false
    };

    private CancellationTokenSource? _scanCancellation;
    private IndependentAuditResult? _lastResult;

    public IndependentMainForm100()
    {
        Text = "FF GUARDIAN 10.0 Core Alpha — Independent Security Engine";
        Icon = DobermannIconFactory.CreateIcon();
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1180, 760);
        BackColor = Background;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10F);
        AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true;

        ConfigureGrid();
        Controls.Add(BuildMainLayout());
        Controls.Add(_status);
    }

    private Control BuildMainLayout()
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Background,
            Padding = new Padding(20)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        Label title = new()
        {
            Dock = DockStyle.Fill,
            Text = "FF GUARDIAN 10 — INDEPENDENT SECURITY CORE",
            Font = new Font("Segoe UI", 22F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };

        TableLayoutPanel controls = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            BackColor = Surface,
            Padding = new Padding(16)
        };
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        Button audit = CreateButton("AUDIT DEL SISTEMA");
        audit.Click += async (_, _) => await RunAuditAsync(null);
        Button folder = CreateButton("SCANSIONA CARTELLA");
        folder.Click += async (_, _) => await SelectAndScanFolderAsync();
        Button cancel = CreateButton("ANNULLA");
        cancel.Click += (_, _) => _scanCancellation?.Cancel();
        Button report = CreateButton("CREA RAPPORTO");
        report.Click += async (_, _) => await CreateReportAsync();

        controls.Controls.Add(audit, 0, 0);
        controls.Controls.Add(folder, 1, 0);
        controls.Controls.Add(cancel, 2, 0);
        controls.Controls.Add(report, 3, 0);
        controls.Controls.Add(_score, 4, 0);

        Panel resultCard = new() { Dock = DockStyle.Fill, BackColor = Surface, Padding = new Padding(14) };
        resultCard.Controls.Add(_results);

        root.Controls.Add(title, 0, 0);
        root.Controls.Add(controls, 0, 1);
        root.Controls.Add(resultCard, 0, 2);
        return root;
    }

    private void ConfigureGrid()
    {
        _results.EnableHeadersVisualStyles = false;
        _results.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(5, 12, 17);
        _results.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _results.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        _results.DefaultCellStyle.BackColor = Surface;
        _results.DefaultCellStyle.ForeColor = Color.White;
        _results.DefaultCellStyle.SelectionBackColor = Color.FromArgb(35, 68, 4);
        _results.DefaultCellStyle.SelectionForeColor = Color.White;
        _results.GridColor = Color.FromArgb(58, 76, 84);
        _results.Columns.Add("Risk", "Rischio");
        _results.Columns.Add("Category", "Categoria");
        _results.Columns.Add("Name", "Nome");
        _results.Columns.Add("Evidence", "Evidenza");
        _results.Columns.Add("Signature", "Firma");
        _results.Columns.Add("Hash", "SHA-256");
        _results.Columns.Add("Target", "Percorso/obiettivo");
        _results.Columns[0].FillWeight = 35;
        _results.Columns[1].FillWeight = 55;
        _results.Columns[2].FillWeight = 80;
        _results.Columns[3].FillWeight = 150;
        _results.Columns[4].FillWeight = 110;
        _results.Columns[5].FillWeight = 120;
        _results.Columns[6].FillWeight = 160;
    }

    private async Task SelectAndScanFolderAsync()
    {
        using FolderBrowserDialog dialog = new() { Description = "Seleziona la cartella da analizzare con il motore indipendente" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            await RunAuditAsync(dialog.SelectedPath);
    }

    private async Task RunAuditAsync(string? scanRoot)
    {
        if (_scanCancellation is not null)
        {
            MessageBox.Show("Un controllo è già in esecuzione.", "FF GUARDIAN 10", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _scanCancellation = new CancellationTokenSource();
        UseWaitCursor = true;
        _results.Rows.Clear();
        _score.Text = "…";

        Progress<string> progress = new(message => _status.Text = message);
        try
        {
            _lastResult = await _engine.RunAuditAsync(scanRoot, progress, _scanCancellation.Token);
            ShowResult(_lastResult);
            _status.Text = $"Audit completato. {_lastResult.Findings.Count} elementi registrati; {_lastResult.FilesExamined:N0} file esaminati.";
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Operazione annullata.";
            _score.Text = "--/100";
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            _status.Text = "Audit non completato.";
            MessageBox.Show(ex.Message, "FF GUARDIAN 10 — Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            _scanCancellation.Dispose();
            _scanCancellation = null;
        }
    }

    private void ShowResult(IndependentAuditResult result)
    {
        _score.Text = $"{result.SecurityScore}/100";
        _score.ForeColor = result.SecurityScore switch
        {
            >= 85 => Neon,
            >= 65 => Color.Gold,
            _ => Color.OrangeRed
        };

        foreach (IndependentFinding finding in result.Findings)
        {
            string shortHash = finding.Sha256.Length > 24 ? finding.Sha256[..24] + "…" : finding.Sha256;
            _results.Rows.Add(
                finding.Risk,
                finding.Category,
                finding.Name,
                finding.Evidence,
                finding.SignatureStatus,
                shortHash,
                finding.Target);
        }
    }

    private async Task CreateReportAsync()
    {
        if (_lastResult is null)
        {
            MessageBox.Show("Esegui prima un audit.", "FF GUARDIAN 10", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FF Guardian Reports");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, $"FFGuardian-Independent-Audit-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        string temp = path + ".tmp";

        StringBuilder report = new();
        report.AppendLine("FF GUARDIAN 10.0 CORE ALPHA — RAPPORTO INDIPENDENTE");
        report.AppendLine($"Data: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        report.AppendLine($"Computer: {Environment.MachineName}");
        report.AppendLine($"Punteggio: {_lastResult.SecurityScore}/100");
        report.AppendLine($"File esaminati: {_lastResult.FilesExamined}");
        report.AppendLine($"Voci di avvio: {_lastResult.StartupEntries}");
        report.AppendLine($"Servizi: {_lastResult.ServicesExamined}");
        report.AppendLine($"Attività pianificate: {_lastResult.ScheduledTasksExamined}");
        report.AppendLine();

        foreach (IndependentFinding finding in _lastResult.Findings)
        {
            report.AppendLine($"[{finding.Risk}] {finding.Category} — {finding.Name}");
            report.AppendLine($"Obiettivo: {finding.Target}");
            report.AppendLine($"Evidenza: {finding.Evidence}");
            report.AppendLine($"Firma: {finding.SignatureStatus}");
            if (!string.IsNullOrWhiteSpace(finding.Sha256)) report.AppendLine($"SHA-256: {finding.Sha256}");
            report.AppendLine();
        }

        await File.WriteAllTextAsync(temp, report.ToString());
        File.Move(temp, path, true);
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        _status.Text = $"Rapporto creato: {Path.GetFileName(path)}";
    }

    private static Button CreateButton(string text)
    {
        Button button = new()
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(6),
            Text = text,
            BackColor = Surface,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = Neon;
        button.FlatAppearance.BorderSize = 1;
        return button;
    }
}
