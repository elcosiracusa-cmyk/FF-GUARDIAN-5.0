using System.Diagnostics;
using System.Text;
using FFGuardian.Engine10;

namespace FFGuardian;

internal sealed class IndependentMainForm100 : Form
{
    private static readonly Color Background = Color.FromArgb(3, 8, 12);
    private static readonly Color Surface = Color.FromArgb(12, 22, 28);
    private static readonly Color SurfaceRaised = Color.FromArgb(17, 31, 39);
    private static readonly Color Neon = Color.FromArgb(160, 255, 0);
    private static readonly Color Muted = Color.FromArgb(188, 200, 207);

    private readonly FFGuardianEngine10 _engine;
    private readonly Label _status = CreateStatusLabel();
    private readonly Label _score = CreateMetricLabel("--/100");
    private readonly Label _signatureVersion = CreateMetricLabel("--");
    private readonly Label _agentState = CreateMetricLabel("IN AVVIO");
    private readonly Label _lastOperation = CreateMetricLabel("NESSUNA");
    private readonly DataGridView _scanGrid = CreateGrid();
    private readonly DataGridView _auditGrid = CreateGrid();
    private readonly TextBox _activityLog = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        BackColor = Background,
        ForeColor = Color.Gainsboro,
        BorderStyle = BorderStyle.None,
        Font = new Font("Consolas", 9.5F)
    };

    private CancellationTokenSource? _operationCancellation;
    private FileScanResult10? _selectedScanResult;
    private EngineAuditResult10? _lastAudit;
    private bool _ownsEngine;

    public IndependentMainForm100() : this(new FFGuardianEngine10(), ownsEngine: true)
    {
    }

    internal IndependentMainForm100(FFGuardianEngine10 engine, bool ownsEngine = false)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _ownsEngine = ownsEngine;

        Text = "FF GUARDIAN 10 — Autonomous Security Center";
        Icon = DobermannIconFactory.CreateIcon();
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1220, 780);
        BackColor = Background;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10F);
        AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true;

        ConfigureScanGrid();
        ConfigureAuditGrid();
        Controls.Add(BuildLayout());
        Controls.Add(_status);
        RefreshDashboard();
    }

    internal void SetAgentStatus(bool running, int monitoredFolders)
    {
        _agentState.Text = running ? $"ATTIVO · {monitoredFolders}" : "ARRESTATO";
        _agentState.ForeColor = running ? Neon : Color.OrangeRed;
    }

    internal void RecordAgentActivity(ProtectionAgentEvent10 e)
    {
        AppendLog($"AGENTE {e.EventType}: {e.Status} {e.Path}".Trim());
        if (e.ScanResult?.Verdict is ThreatVerdict10.Suspicious or ThreatVerdict10.Malicious)
            _lastOperation.Text = e.ScanResult.Verdict.ToString().ToUpperInvariant();
    }

    private Control BuildLayout()
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            BackColor = Background,
            Padding = new Padding(18)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildTabs(), 0, 1);
        return root;
    }

    private Control BuildHeader()
    {
        TableLayoutPanel header = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = Background
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));

        Label title = new()
        {
            Dock = DockStyle.Fill,
            Text = "FF GUARDIAN 10  ·  AUTONOMOUS SECURITY CENTER",
            Font = new Font("Segoe UI", 21F, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft
        };
        Label edition = new()
        {
            Dock = DockStyle.Fill,
            Text = "ENGINE10 DEFINITIVE",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Neon,
            TextAlign = ContentAlignment.MiddleRight
        };
        header.Controls.Add(title, 0, 0);
        header.Controls.Add(edition, 1, 0);
        return header;
    }

    private Control BuildTabs()
    {
        TabControl tabs = new()
        {
            Dock = DockStyle.Fill,
            Appearance = TabAppearance.FlatButtons,
            ItemSize = new Size(145, 38),
            SizeMode = TabSizeMode.Fixed,
            Padding = new Point(12, 6)
        };
        tabs.TabPages.Add(BuildDashboardPage());
        tabs.TabPages.Add(BuildScannerPage());
        tabs.TabPages.Add(BuildAuditPage());
        tabs.TabPages.Add(BuildRecoveryPage());
        tabs.TabPages.Add(BuildUpdatesPage());
        tabs.TabPages.Add(BuildActivityPage());
        return tabs;
    }

    private TabPage BuildDashboardPage()
    {
        TabPage page = CreatePage("DASHBOARD");
        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 2,
            Padding = new Padding(18)
        };
        for (int i = 0; i < 4; i++)
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(CreateMetricCard("PUNTEGGIO SICUREZZA", _score), 0, 0);
        layout.Controls.Add(CreateMetricCard("AGENTE AUTONOMO", _agentState), 1, 0);
        layout.Controls.Add(CreateMetricCard("DATABASE FIRME", _signatureVersion), 2, 0);
        layout.Controls.Add(CreateMetricCard("ULTIMA OPERAZIONE", _lastOperation), 3, 0);

        Panel info = CreateCard();
        Label description = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(22),
            ForeColor = Muted,
            Font = new Font("Segoe UI", 11F),
            Text = "Protezione autonoma user-mode attiva. Il motore controlla file, persistenza, servizi, attività pianificate, firme digitali e archivi senza utilizzare Microsoft Defender. Le correzioni distruttive richiedono sempre conferma e rollback.",
            TextAlign = ContentAlignment.MiddleLeft
        };
        info.Controls.Add(description);
        layout.SetColumnSpan(info, 4);
        layout.Controls.Add(info, 0, 1);
        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildScannerPage()
    {
        TabPage page = CreatePage("SCANSIONE");
        TableLayoutPanel layout = CreatePageLayout();
        FlowLayoutPanel commands = CreateCommandBar();

        Button fileButton = CreateButton("SCANSIONA FILE");
        fileButton.Click += async (_, _) => await SelectAndScanFileAsync();
        Button folderButton = CreateButton("SCANSIONA CARTELLA");
        folderButton.Click += async (_, _) => await SelectAndScanFolderAsync();
        Button quarantineButton = CreateButton("METTI IN QUARANTENA");
        quarantineButton.Click += async (_, _) => await QuarantineSelectedAsync();
        Button cancelButton = CreateButton("ANNULLA");
        cancelButton.Click += (_, _) => _operationCancellation?.Cancel();

        commands.Controls.AddRange([fileButton, folderButton, quarantineButton, cancelButton]);
        layout.Controls.Add(commands, 0, 0);
        layout.Controls.Add(WrapCard(_scanGrid), 0, 1);
        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildAuditPage()
    {
        TabPage page = CreatePage("AUDIT");
        TableLayoutPanel layout = CreatePageLayout();
        FlowLayoutPanel commands = CreateCommandBar();

        Button auditButton = CreateButton("ESEGUI AUDIT COMPLETO");
        auditButton.Click += async (_, _) => await RunAuditAsync();
        Button reportButton = CreateButton("ESPORTA RAPPORTO");
        reportButton.Click += async (_, _) => await CreateAuditReportAsync();
        Button cancelButton = CreateButton("ANNULLA");
        cancelButton.Click += (_, _) => _operationCancellation?.Cancel();

        commands.Controls.AddRange([auditButton, reportButton, cancelButton]);
        layout.Controls.Add(commands, 0, 0);
        layout.Controls.Add(WrapCard(_auditGrid), 0, 1);
        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildRecoveryPage()
    {
        TabPage page = CreatePage("RECUPERO");
        FlowLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(24)
        };

        layout.Controls.Add(CreateSectionTitle("QUARANTENA CIFRATA E ROLLBACK"));
        layout.Controls.Add(CreateInformationBox(
            "I file rilevati possono essere trasferiti in un contenitore AES-256 autenticato con HMAC-SHA256. " +
            "Prima di ogni correzione viene creato un backup verificato. Il ripristino controlla nuovamente SHA-256 e integrità."));

        Button quarantineFolder = CreateButton("APRI ARCHIVIO QUARANTENA");
        quarantineFolder.Click += (_, _) => OpenEngineFolder("Quarantine");
        Button rollbackFolder = CreateButton("APRI ARCHIVIO ROLLBACK");
        rollbackFolder.Click += (_, _) => OpenEngineFolder("Rollback");
        layout.Controls.Add(quarantineFolder);
        layout.Controls.Add(rollbackFolder);
        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildUpdatesPage()
    {
        TabPage page = CreatePage("AGGIORNAMENTI");
        FlowLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(24)
        };
        layout.Controls.Add(CreateSectionTitle("AGGIORNAMENTI SICURI"));
        layout.Controls.Add(CreateInformationBox(
            "Il motore supporta manifesti firmati RSA-PSS, download HTTPS, SHA-256, verifica Authenticode, staging atomico e protezione anti-downgrade. " +
            "L’installazione automatica resta disabilitata fino ai test finali del Punto 10."));

        Button reload = CreateButton("RICARICA DATABASE FIRME");
        reload.Click += async (_, _) => await ReloadSignaturesAsync();
        layout.Controls.Add(reload);
        page.Controls.Add(layout);
        return page;
    }

    private TabPage BuildActivityPage()
    {
        TabPage page = CreatePage("ATTIVITÀ");
        TableLayoutPanel layout = CreatePageLayout();
        FlowLayoutPanel commands = CreateCommandBar();
        Button openReports = CreateButton("APRI RAPPORTI");
        openReports.Click += (_, _) => OpenReportsFolder();
        Button clear = CreateButton("PULISCI VISUALIZZAZIONE");
        clear.Click += (_, _) => _activityLog.Clear();
        commands.Controls.AddRange([openReports, clear]);
        layout.Controls.Add(commands, 0, 0);
        layout.Controls.Add(WrapCard(_activityLog), 0, 1);
        page.Controls.Add(layout);
        return page;
    }

    private async Task SelectAndScanFileAsync()
    {
        using OpenFileDialog dialog = new()
        {
            Title = "Seleziona il file da analizzare",
            Filter = "File analizzabili|*.exe;*.dll;*.scr;*.com;*.sys;*.msi;*.msix;*.ps1;*.bat;*.cmd;*.vbs;*.js;*.jse;*.hta;*.wsf;*.lnk;*.zip|Tutti i file|*.*"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        await RunExclusiveUiOperationAsync("Scansione file", async token =>
        {
            FileScanResult10 result = await _engine.ScanFileAsync(dialog.FileName, token);
            _selectedScanResult = result;
            ShowScanResults([result]);
            _lastOperation.Text = result.Verdict.ToString().ToUpperInvariant();
            AppendLog($"SCANSIONE FILE: {result.Path} — {result.Verdict} — {result.DetectionName}");
        });
    }

    private async Task SelectAndScanFolderAsync()
    {
        using FolderBrowserDialog dialog = new() { Description = "Seleziona la cartella da analizzare" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        await RunExclusiveUiOperationAsync("Scansione cartella", async token =>
        {
            Progress<string> progress = new(message => _status.Text = message);
            FolderScanSummary10 summary = await _engine.ScanFolderAsync(dialog.SelectedPath, progress, token);
            _selectedScanResult = summary.Results.FirstOrDefault(result =>
                result.Verdict is ThreatVerdict10.Malicious or ThreatVerdict10.Suspicious);
            ShowScanResults(summary.Results);
            _lastOperation.Text = $"{summary.FilesScanned:N0} FILE";
            AppendLog($"SCANSIONE CARTELLA: {summary.RootPath} — {summary.FilesScanned} analizzati — {summary.SuspiciousFiles} sospetti — {summary.MaliciousFiles} malevoli");
        });
    }

    private async Task RunAuditAsync()
    {
        await RunExclusiveUiOperationAsync("Audit completo", async token =>
        {
            Progress<string> progress = new(message => _status.Text = message);
            _lastAudit = await _engine.RunAuditAsync(progress, token);
            ShowAuditResults(_lastAudit);
            _score.Text = $"{_lastAudit.SecurityScore}/100";
            _score.ForeColor = _lastAudit.SecurityScore switch
            {
                >= 85 => Neon,
                >= 65 => Color.Gold,
                _ => Color.OrangeRed
            };
            _lastOperation.Text = "AUDIT";
            AppendLog($"AUDIT: punteggio {_lastAudit.SecurityScore}/100 — {_lastAudit.Findings.Count} evidenze");
        });
    }

    private async Task QuarantineSelectedAsync()
    {
        FileScanResult10? result = _selectedScanResult;
        if (result is null || result.Verdict is not (ThreatVerdict10.Suspicious or ThreatVerdict10.Malicious))
        {
            MessageBox.Show("Seleziona o analizza prima un file sospetto o malevolo.", "FF GUARDIAN 10", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        DialogResult confirmation = MessageBox.Show(
            $"Mettere in quarantena il file?\n\n{result.Path}\n\nVerrà creato un backup e il contenuto sarà cifrato.",
            "Conferma quarantena",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (confirmation != DialogResult.Yes)
            return;

        await RunExclusiveUiOperationAsync("Quarantena", async token =>
        {
            AuditFinding10 finding = new(
                Guid.NewGuid().ToString("N"),
                "File",
                Path.GetFileName(result.Path),
                result.Path,
                result.Verdict == ThreatVerdict10.Malicious ? AuditSeverity10.Critical : AuditSeverity10.High,
                Math.Clamp(result.Confidence, 1, 100),
                string.Join("; ", result.Reasons),
                result.Sha256,
                result.DetectionName,
                true);
            RemediationPlan10 plan = _engine.CreateQuarantinePlan(finding);
            QuarantineRecord10 record = await _engine.ExecuteQuarantineAsync(plan, result, confirmed: true, token);
            AppendLog($"QUARANTENA: {record.OriginalPath} — ID {record.Id}");
            _status.Text = $"File protetto in quarantena. ID: {record.Id}";
            _selectedScanResult = null;
        });
    }

    private async Task ReloadSignaturesAsync()
    {
        await RunExclusiveUiOperationAsync("Aggiornamento firme", async token =>
        {
            await _engine.ReloadSignaturesAsync(token);
            RefreshDashboard();
            AppendLog($"DATABASE FIRME RICARICATO: {_engine.SignatureDatabaseVersion}");
        });
    }

    private async Task RunExclusiveUiOperationAsync(string name, Func<CancellationToken, Task> operation)
    {
        if (_operationCancellation is not null)
        {
            MessageBox.Show("Un’operazione è già in esecuzione.", "FF GUARDIAN 10", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _operationCancellation = new CancellationTokenSource();
        UseWaitCursor = true;
        _status.Text = $"{name} in corso…";
        try
        {
            await operation(_operationCancellation.Token);
            _status.Text = $"{name} completata.";
        }
        catch (OperationCanceledException)
        {
            _status.Text = $"{name} annullata.";
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            AppendLog($"ERRORE {name.ToUpperInvariant()}: {ex.Message}");
            _status.Text = $"{name} non completata.";
            MessageBox.Show(ex.Message, "FF GUARDIAN 10 — Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            _operationCancellation.Dispose();
            _operationCancellation = null;
        }
    }

    private void ShowScanResults(IEnumerable<FileScanResult10> results)
    {
        _scanGrid.Rows.Clear();
        foreach (FileScanResult10 result in results)
        {
            string hash = result.Sha256.Length > 24 ? result.Sha256[..24] + "…" : result.Sha256;
            _scanGrid.Rows.Add(result.Verdict, result.Confidence, result.DetectionName,
                string.Join("; ", result.Reasons), hash, result.Path);
        }
    }

    private void ShowAuditResults(EngineAuditResult10 audit)
    {
        _auditGrid.Rows.Clear();
        foreach (AuditFinding10 finding in audit.Findings)
        {
            string hash = finding.Sha256.Length > 24 ? finding.Sha256[..24] + "…" : finding.Sha256;
            _auditGrid.Rows.Add(finding.Severity, finding.RiskScore, finding.Category,
                finding.Name, finding.Evidence, finding.SignatureStatus, hash, finding.Target);
        }
    }

    private async Task CreateAuditReportAsync()
    {
        EngineAuditResult10? audit = _lastAudit;
        if (audit is null)
        {
            MessageBox.Show("Esegui prima un audit completo.", "FF GUARDIAN 10", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string folder = GetReportsFolder();
        string path = Path.Combine(folder, $"FFGuardian-Audit-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        string temp = path + ".tmp";
        StringBuilder report = new();
        report.AppendLine("FF GUARDIAN 10 — RAPPORTO AUTONOMO");
        report.AppendLine($"Data: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        report.AppendLine($"Computer: {Environment.MachineName}");
        report.AppendLine($"Punteggio: {audit.SecurityScore}/100");
        report.AppendLine($"Persistenza: {audit.PersistenceItems}");
        report.AppendLine($"Servizi: {audit.ServiceItems}");
        report.AppendLine($"Attività pianificate: {audit.ScheduledTaskItems}");
        report.AppendLine();
        foreach (AuditFinding10 finding in audit.Findings)
        {
            report.AppendLine($"[{finding.Severity}] {finding.Category} — {finding.Name}");
            report.AppendLine($"Rischio: {finding.RiskScore}/100");
            report.AppendLine($"Target: {finding.Target}");
            report.AppendLine($"Evidenza: {finding.Evidence}");
            report.AppendLine($"Firma: {finding.SignatureStatus}");
            if (!string.IsNullOrWhiteSpace(finding.Sha256)) report.AppendLine($"SHA-256: {finding.Sha256}");
            report.AppendLine();
        }
        await File.WriteAllTextAsync(temp, report.ToString());
        File.Move(temp, path, true);
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
        AppendLog($"RAPPORTO CREATO: {path}");
    }

    private void RefreshDashboard()
    {
        _signatureVersion.Text = _engine.SignatureDatabaseVersion;
        _signatureVersion.ForeColor = Neon;
    }

    private void AppendLog(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new MethodInvoker(() => AppendLog(message)));
            return;
        }
        _activityLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private static void OpenEngineFolder(string name)
    {
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FFGuardian", name);
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
    }

    private static string GetReportsFolder()
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FF Guardian Reports");
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static void OpenReportsFolder() =>
        Process.Start(new ProcessStartInfo("explorer.exe", GetReportsFolder()) { UseShellExecute = true });

    private void ConfigureScanGrid()
    {
        ConfigureGridStyle(_scanGrid);
        _scanGrid.Columns.Add("Verdict", "Esito");
        _scanGrid.Columns.Add("Confidence", "Confidenza");
        _scanGrid.Columns.Add("Detection", "Rilevamento");
        _scanGrid.Columns.Add("Reasons", "Motivazioni");
        _scanGrid.Columns.Add("Hash", "SHA-256");
        _scanGrid.Columns.Add("Path", "Percorso");
    }

    private void ConfigureAuditGrid()
    {
        ConfigureGridStyle(_auditGrid);
        _auditGrid.Columns.Add("Severity", "Gravità");
        _auditGrid.Columns.Add("Risk", "Rischio");
        _auditGrid.Columns.Add("Category", "Categoria");
        _auditGrid.Columns.Add("Name", "Nome");
        _auditGrid.Columns.Add("Evidence", "Evidenza");
        _auditGrid.Columns.Add("Signature", "Firma");
        _auditGrid.Columns.Add("Hash", "SHA-256");
        _auditGrid.Columns.Add("Target", "Obiettivo");
    }

    private static void ConfigureGridStyle(DataGridView grid)
    {
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(5, 12, 17);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        grid.DefaultCellStyle.BackColor = Surface;
        grid.DefaultCellStyle.ForeColor = Color.White;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(35, 68, 4);
        grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.GridColor = Color.FromArgb(58, 76, 84);
    }

    private static DataGridView CreateGrid() => new()
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

    private static Label CreateStatusLabel() => new()
    {
        Dock = DockStyle.Bottom,
        Height = 38,
        BackColor = Color.FromArgb(5, 12, 17),
        ForeColor = Muted,
        Padding = new Padding(18, 0, 0, 0),
        TextAlign = ContentAlignment.MiddleLeft,
        Text = "Engine10 pronto."
    };

    private static Label CreateMetricLabel(string text) => new()
    {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        Font = new Font("Segoe UI", 20F, FontStyle.Bold),
        ForeColor = Neon,
        Text = text
    };

    private static Panel CreateMetricCard(string title, Control metric)
    {
        Panel card = CreateCard();
        Label label = new()
        {
            Dock = DockStyle.Top,
            Height = 45,
            Text = title,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        card.Controls.Add(metric);
        card.Controls.Add(label);
        return card;
    }

    private static Panel CreateCard() => new()
    {
        Dock = DockStyle.Fill,
        BackColor = SurfaceRaised,
        Margin = new Padding(8),
        Padding = new Padding(8)
    };

    private static Panel WrapCard(Control control)
    {
        Panel card = CreateCard();
        card.Controls.Add(control);
        return card;
    }

    private static TableLayoutPanel CreatePageLayout()
    {
        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Padding = new Padding(12)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        return layout;
    }

    private static FlowLayoutPanel CreateCommandBar() => new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = false,
        BackColor = Surface,
        Padding = new Padding(8)
    };

    private static TabPage CreatePage(string title) => new(title)
    {
        BackColor = Background,
        ForeColor = Color.White,
        Padding = new Padding(6)
    };

    private static Label CreateSectionTitle(string text) => new()
    {
        AutoSize = false,
        Width = 900,
        Height = 54,
        Text = text,
        Font = new Font("Segoe UI", 18F, FontStyle.Bold),
        ForeColor = Color.White,
        TextAlign = ContentAlignment.MiddleLeft
    };

    private static Label CreateInformationBox(string text) => new()
    {
        AutoSize = false,
        Width = 900,
        Height = 130,
        Padding = new Padding(18),
        BackColor = SurfaceRaised,
        ForeColor = Muted,
        Font = new Font("Segoe UI", 10.5F),
        Text = text,
        TextAlign = ContentAlignment.MiddleLeft
    };

    private static Button CreateButton(string text)
    {
        Button button = new()
        {
            Width = 220,
            Height = 42,
            Margin = new Padding(6),
            Text = text,
            BackColor = SurfaceRaised,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = Neon;
        button.FlatAppearance.BorderSize = 1;
        return button;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _operationCancellation?.Cancel();
            _operationCancellation?.Dispose();
            if (_ownsEngine)
                _engine.Dispose();
        }
        base.Dispose(disposing);
    }
}
