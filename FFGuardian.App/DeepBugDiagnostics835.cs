using System.Diagnostics;
using System.Text;

namespace FFGuardian;

internal enum DiagnosticSeverity
{
    Info,
    Warning,
    Critical
}

internal sealed record DiagnosticFinding(
    string Code,
    DiagnosticSeverity Severity,
    string Area,
    string Description,
    bool AutoFixAvailable);

internal static class DeepBugDiagnostics835
{
    private const string ButtonName = "FFG835_DEEP_DIAGNOSTICS";
    private static readonly HashSet<Button> HookedButtons = new();
    private static readonly Color Bg = Color.FromArgb(4, 9, 12);
    private static readonly Color Surface = Color.FromArgb(11, 20, 24);
    private static readonly Color Neon = Color.FromArgb(142, 255, 0);

    public static void Apply(object? sender, EventArgs e)
    {
        foreach (Form form in Application.OpenForms.Cast<Form>().Where(f => !f.IsDisposed))
        {
            if (!form.Text.Contains("FF GUARDIAN", StringComparison.OrdinalIgnoreCase))
                continue;

            FlowLayoutPanel? menu = Descendants(form).OfType<FlowLayoutPanel>()
                .FirstOrDefault(flow => flow.Controls.OfType<Button>()
                    .Any(button => button.Text.Contains("Dashboard", StringComparison.OrdinalIgnoreCase)));
            if (menu is null)
                continue;

            Button? button = menu.Controls.OfType<Button>().FirstOrDefault(b => b.Name == ButtonName);
            if (button is null)
            {
                button = BuildButton(menu);
                int infoIndex = menu.Controls.OfType<Button>().ToList()
                    .FindIndex(b => b.Text.Contains("Informazioni", StringComparison.OrdinalIgnoreCase));
                menu.Controls.Add(button);
                if (infoIndex >= 0)
                    menu.Controls.SetChildIndex(button, infoIndex);
            }

            button.Text = "⌁   Diagnostica avanzata 8.3.6";
            button.Width = Math.Max(220, menu.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 6);

            if (HookedButtons.Add(button))
            {
                Form owner = form;
                button.Click += (_, _) => ShowDiagnostics(owner);
                button.Disposed += (_, _) => HookedButtons.Remove(button);
            }
        }
    }

    private static Button BuildButton(FlowLayoutPanel menu)
    {
        Button button = new()
        {
            Name = ButtonName,
            Text = "⌁   Diagnostica avanzata 8.3.6",
            Width = Math.Max(220, menu.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 6),
            Height = 39,
            Margin = new Padding(0, 1, 0, 1),
            Padding = new Padding(12, 0, 0, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Surface,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.2F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = Neon;
        return button;
    }

    private static void ShowDiagnostics(Form owner)
    {
        using Form dialog = new()
        {
            Text = "FF GUARDIAN 8.3.6 — Diagnostic Reliability Fix",
            Icon = owner.Icon,
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(980, 700),
            MinimumSize = new Size(760, 560),
            BackColor = Bg,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F)
        };

        Label summary = new()
        {
            Dock = DockStyle.Top,
            Height = 72,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(18, 0, 12, 0),
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            Text = "Premi ESEGUI CONTROLLO per analizzare la sessione corrente senza avviare scansioni antivirus."
        };

        DataGridView grid = BuildGrid();
        BindingSource source = new();
        grid.DataSource = source;

        TableLayoutPanel actions = new()
        {
            Dock = DockStyle.Bottom,
            Height = 64,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(10, 8, 10, 8),
            BackColor = Surface
        };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32));

        Button run = ActionButton("ESEGUI CONTROLLO");
        Button fix = ActionButton("CORREGGI GRAFICA");
        Button export = ActionButton("ESPORTA RAPPORTO");
        fix.Enabled = false;
        export.Enabled = false;
        List<DiagnosticFinding> lastFindings = [];

        run.Click += (_, _) =>
        {
            Stopwatch watch = Stopwatch.StartNew();
            run.Enabled = false;
            dialog.UseWaitCursor = true;
            summary.Text = "Controllo strutturale in corso…";
            try
            {
                lastFindings = RunChecks(owner);
                source.DataSource = lastFindings.Select(f => new
                {
                    Codice = f.Code,
                    Gravità = SeverityText(f.Severity),
                    Area = f.Area,
                    Problema = f.Description,
                    Correzione = f.AutoFixAvailable ? "Disponibile" : "Manuale"
                }).ToList();

                int critical = lastFindings.Count(f => f.Severity == DiagnosticSeverity.Critical);
                int warnings = lastFindings.Count(f => f.Severity == DiagnosticSeverity.Warning);
                summary.Text = $"Completato in {watch.ElapsedMilliseconds} ms — Critici: {critical}  Avvisi: {warnings}  Risultati: {lastFindings.Count}";
                summary.ForeColor = critical > 0 ? Color.OrangeRed : warnings > 0 ? Color.Gold : Neon;
                fix.Enabled = lastFindings.Any(f => f.AutoFixAvailable);
                export.Enabled = true;
            }
            catch (Exception ex)
            {
                StabilityCoordinator82.WriteStabilityLog(ex);
                summary.Text = "Controllo non completato. L'errore è stato registrato.";
                summary.ForeColor = Color.OrangeRed;
            }
            finally
            {
                dialog.UseWaitCursor = false;
                run.Enabled = true;
            }
        };

        fix.Click += (_, _) =>
        {
            try
            {
                InterfaceRecovery831.Apply(null, EventArgs.Empty);
                FinalUiAudit834.Apply(null, EventArgs.Empty);
                owner.PerformLayout();
                owner.Invalidate(true);
                summary.Text = "Correzioni grafiche applicate. Ripeti il controllo per verificare il risultato.";
                summary.ForeColor = Neon;
            }
            catch (Exception ex)
            {
                StabilityCoordinator82.WriteStabilityLog(ex);
                summary.Text = "Correzione automatica non completata. Consulta il registro stabilità.";
                summary.ForeColor = Color.OrangeRed;
            }
        };

        export.Click += async (_, _) =>
        {
            try
            {
                string path = await ExportAsync(lastFindings, owner);
                summary.Text = $"Rapporto verificato: {Path.GetFileName(path)}";
                summary.ForeColor = Neon;
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                StabilityCoordinator82.WriteStabilityLog(ex);
                summary.Text = "Esportazione non riuscita. Consulta il registro stabilità.";
                summary.ForeColor = Color.OrangeRed;
            }
        };

        actions.Controls.Add(run, 0, 0);
        actions.Controls.Add(fix, 1, 0);
        actions.Controls.Add(export, 2, 0);
        dialog.Controls.Add(grid);
        dialog.Controls.Add(actions);
        dialog.Controls.Add(summary);
        dialog.ShowDialog(owner);
    }

    private static List<DiagnosticFinding> RunChecks(Form form)
    {
        List<DiagnosticFinding> findings = [];
        Control[] controls = Descendants(form).Where(c => !c.IsDisposed).ToArray();
        string activeArea = ActivePageName(form);

        if (form.ClientSize.Width < 1000 || form.ClientSize.Height < 650)
            findings.Add(new("UI-001", DiagnosticSeverity.Info, "Finestra", "Area ridotta: lo scorrimento verticale può essere normale.", false));

        FlowLayoutPanel? menu = controls.OfType<FlowLayoutPanel>()
            .FirstOrDefault(flow => flow.Controls.OfType<Button>().Any(b => b.Text.Contains("Dashboard", StringComparison.OrdinalIgnoreCase)));
        if (menu is null)
            findings.Add(new("NAV-001", DiagnosticSeverity.Critical, "Sidebar", "Menu principale non individuato.", false));
        else
        {
            int navButtons = menu.Controls.OfType<Button>().Count();
            if (navButtons < 13)
                findings.Add(new("NAV-002", DiagnosticSeverity.Warning, "Sidebar", $"Voci menu rilevate: {navButtons}; una o più sezioni potrebbero mancare.", false));
            if (menu.HorizontalScroll.Visible)
                findings.Add(new("NAV-003", DiagnosticSeverity.Warning, "Sidebar", "Scrollbar orizzontale visibile.", true));
            if (menu.Controls.OfType<Button>().Any(b => b.Width > menu.ClientSize.Width + 2))
                findings.Add(new("NAV-004", DiagnosticSeverity.Warning, "Sidebar", "Uno o più pulsanti superano la larghezza disponibile.", true));
        }

        foreach (Control control in controls.Where(c => c.Visible))
        {
            if (control.Width <= 1 || control.Height <= 1)
                findings.Add(new("UI-002", DiagnosticSeverity.Critical, ControlArea(control, activeArea), $"Controllo con dimensione non valida: {ControlName(control)}.", true));

            if (control is Panel panel && IsUnexpectedEmptyPanel(panel))
                findings.Add(new("UI-003", DiagnosticSeverity.Warning, activeArea, $"Pannello operativo vuoto: {ControlName(panel)}.", true));

            if (control is Button button && string.IsNullOrWhiteSpace(button.Text))
                findings.Add(new("BTN-001", DiagnosticSeverity.Warning, ControlArea(button, activeArea), "Pulsante senza testo.", false));

            if (control.Parent is not null && control.Dock == DockStyle.None && control.Width > 20 && control.Height > 20 &&
                !control.Parent.ClientRectangle.IntersectsWith(control.Bounds))
                findings.Add(new("UI-004", DiagnosticSeverity.Warning, ControlArea(control, activeArea), $"Controllo fuori dall'area del contenitore: {ControlName(control)}.", true));
        }

        foreach (Control parent in controls.Where(IsOverlapCandidateParent))
        {
            Control[] children = parent.Controls.Cast<Control>()
                .Where(c => c.Visible && c.Width > 30 && c.Height > 30 && c.Dock == DockStyle.None)
                .ToArray();
            bool reported = false;
            for (int i = 0; i < children.Length && !reported; i++)
            {
                for (int j = i + 1; j < children.Length; j++)
                {
                    Rectangle intersection = Rectangle.Intersect(children[i].Bounds, children[j].Bounds);
                    int overlapArea = intersection.Width * intersection.Height;
                    int smallerArea = Math.Min(children[i].Width * children[i].Height, children[j].Width * children[j].Height);
                    if (smallerArea > 0 && overlapArea > smallerArea * 0.35)
                    {
                        findings.Add(new("UI-005", DiagnosticSeverity.Warning, ControlArea(parent, activeArea), $"Sovrapposizione significativa tra {ControlName(children[i])} e {ControlName(children[j])}.", true));
                        reported = true;
                        break;
                    }
                }
            }
        }

        string programData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FF Guardian");
        string settings = Path.Combine(programData, "advanced-settings-v81.json");
        string logs = Path.Combine(programData, "Logs");
        CheckFolder(findings, "CFG-001", "Configurazione", programData);
        CheckFolder(findings, "LOG-001", "Registri", logs);

        if (File.Exists(settings) && new FileInfo(settings).Length == 0)
            findings.Add(new("CFG-002", DiagnosticSeverity.Critical, "Configurazione", "File impostazioni vuoto.", false));

        if (!findings.Any(f => f.Severity != DiagnosticSeverity.Info))
            findings.Add(new("OK-000", DiagnosticSeverity.Info, activeArea, "Nessun problema strutturale rilevato nella sessione corrente.", false));

        return findings
            .GroupBy(f => (f.Code, f.Area, f.Description))
            .Select(group => group.First())
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.Code)
            .ToList();
    }

    private static bool IsUnexpectedEmptyPanel(Panel panel)
    {
        if (!panel.Visible || panel.Controls.Count != 0 || panel.Dock != DockStyle.Fill)
            return false;
        if (panel.Width < 200 || panel.Height < 120)
            return false;
        if (panel.Parent is Form)
            return false;
        return panel.Parent?.Visible == true;
    }

    private static bool IsOverlapCandidateParent(Control control)
    {
        if (!control.Visible || control.Controls.Count is <= 1 or >= 40)
            return false;
        if (control is FlowLayoutPanel or TableLayoutPanel)
            return false;
        return control.Controls.Cast<Control>().Count(c => c.Visible && c.Dock == DockStyle.None) > 1;
    }

    private static string ActivePageName(Form form)
    {
        Label? pageTitle = Descendants(form).OfType<Label>()
            .Where(label => label.Visible && label.Font.Bold && label.Font.Size >= 16F)
            .Where(label => !label.Text.Contains("FF GUARDIAN Personal", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(label => label.Font.Size)
            .ThenBy(label => label.Top)
            .FirstOrDefault();
        return string.IsNullOrWhiteSpace(pageTitle?.Text) ? "Interfaccia" : pageTitle.Text.Trim();
    }

    private static void CheckFolder(List<DiagnosticFinding> findings, string code, string area, string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            string probe = Path.Combine(path, $".ffg-probe-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
        }
        catch (Exception ex)
        {
            findings.Add(new(code, DiagnosticSeverity.Critical, area, $"Cartella non scrivibile: {ex.Message}", false));
        }
    }

    private static async Task<string> ExportAsync(IReadOnlyCollection<DiagnosticFinding> findings, Form form)
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FF Guardian Reports");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, $"FFGuardian-Bug-Diagnostics-8.3.6-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        StringBuilder text = new();
        text.AppendLine("FF GUARDIAN 8.3.6 - DIAGNOSTIC RELIABILITY REPORT");
        text.AppendLine($"Data: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        text.AppendLine($"Computer: {Environment.MachineName}");
        text.AppendLine($"Windows: {Environment.OSVersion}");
        text.AppendLine($"Finestra: {form.ClientSize.Width}x{form.ClientSize.Height}");
        text.AppendLine($"Pagina attiva: {ActivePageName(form)}");
        text.AppendLine($"Risultati: {findings.Count}");
        text.AppendLine(new string('-', 72));
        foreach (DiagnosticFinding finding in findings)
            text.AppendLine($"[{finding.Code}] {SeverityText(finding.Severity)} | {finding.Area} | {finding.Description} | Auto-fix: {(finding.AutoFixAvailable ? "Sì" : "No")}");

        string temp = path + ".tmp";
        await File.WriteAllTextAsync(temp, text.ToString());
        if (!File.Exists(temp) || new FileInfo(temp).Length < 100)
            throw new IOException("Il rapporto temporaneo non è valido.");
        File.Move(temp, path, true);
        if (!File.Exists(path) || new FileInfo(path).Length < 100)
            throw new IOException("La verifica finale del rapporto non è riuscita.");
        return path;
    }

    private static DataGridView BuildGrid()
    {
        DataGridView grid = new()
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Bg,
            ForeColor = Color.White,
            RowHeadersVisible = false,
            BorderStyle = BorderStyle.None
        };
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(35, 80, 0);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.DefaultCellStyle.BackColor = Surface;
        grid.DefaultCellStyle.ForeColor = Color.White;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(45, 100, 0);
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
        return grid;
    }

    private static Button ActionButton(string text)
    {
        Button button = new()
        {
            Text = text,
            Dock = DockStyle.Fill,
            Margin = new Padding(4, 0, 4, 0),
            BackColor = Surface,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = Neon;
        return button;
    }

    private static string SeverityText(DiagnosticSeverity severity) => severity switch
    {
        DiagnosticSeverity.Critical => "CRITICO",
        DiagnosticSeverity.Warning => "AVVISO",
        _ => "INFO"
    };

    private static string ControlArea(Control control, string activeArea) =>
        control.FindForm() is null ? control.Parent?.Name ?? activeArea : activeArea;

    private static string ControlName(Control control) =>
        !string.IsNullOrWhiteSpace(control.Name) ? control.Name : control.GetType().Name;

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control nested in Descendants(child))
                yield return nested;
        }
    }
}
