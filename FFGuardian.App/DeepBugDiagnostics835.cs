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
                menu.Controls.Add(button);
            }

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
            Text = "⌁   Diagnostica avanzata 8.3.5",
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
            Text = "FF GUARDIAN 8.3.5 — Deep Bug Diagnostics",
            Icon = owner.Icon,
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(960, 680),
            MinimumSize = new Size(820, 580),
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
            Text = "Premi ESEGUI CONTROLLO COMPLETO per analizzare interfaccia, cartelle e configurazione."
        };

        DataGridView grid = BuildGrid();
        BindingSource source = new();
        grid.DataSource = source;

        FlowLayoutPanel actions = new()
        {
            Dock = DockStyle.Bottom,
            Height = 64,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Padding = new Padding(12, 8, 8, 8),
            BackColor = Surface
        };

        Button run = ActionButton("ESEGUI CONTROLLO COMPLETO", 250);
        Button fix = ActionButton("CORREGGI PROBLEMI RILEVATI", 260);
        Button export = ActionButton("ESPORTA RAPPORTO BUG", 220);
        fix.Enabled = false;
        export.Enabled = false;
        List<DiagnosticFinding> lastFindings = [];

        run.Click += (_, _) =>
        {
            Stopwatch watch = Stopwatch.StartNew();
            run.Enabled = false;
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
                summary.Text = $"Controllo completato in {watch.ElapsedMilliseconds} ms — Critici: {critical}  Avvisi: {warnings}  Totale: {lastFindings.Count}";
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
                summary.Text = "Correzioni grafiche sicure applicate. Esegui nuovamente il controllo.";
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
                summary.Text = $"Rapporto esportato: {Path.GetFileName(path)}";
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

        actions.Controls.Add(run);
        actions.Controls.Add(fix);
        actions.Controls.Add(export);
        dialog.Controls.Add(grid);
        dialog.Controls.Add(actions);
        dialog.Controls.Add(summary);
        dialog.ShowDialog(owner);
    }

    private static List<DiagnosticFinding> RunChecks(Form form)
    {
        List<DiagnosticFinding> findings = [];
        Control[] controls = Descendants(form).ToArray();

        if (form.ClientSize.Width < 1000 || form.ClientSize.Height < 650)
            findings.Add(new("UI-001", DiagnosticSeverity.Warning, "Finestra", "Area disponibile ridotta: alcuni contenuti potrebbero richiedere scorrimento.", false));

        FlowLayoutPanel? menu = controls.OfType<FlowLayoutPanel>()
            .FirstOrDefault(flow => flow.Controls.OfType<Button>().Any(b => b.Text.Contains("Dashboard", StringComparison.OrdinalIgnoreCase)));
        if (menu is null)
            findings.Add(new("NAV-001", DiagnosticSeverity.Critical, "Sidebar", "Menu principale non individuato.", false));
        else
        {
            int navButtons = menu.Controls.OfType<Button>().Count();
            if (navButtons < 12)
                findings.Add(new("NAV-002", DiagnosticSeverity.Warning, "Sidebar", $"Numero ridotto di voci menu rilevate: {navButtons}.", false));
            if (menu.HorizontalScroll.Visible)
                findings.Add(new("NAV-003", DiagnosticSeverity.Warning, "Sidebar", "Scrollbar orizzontale visibile.", true));
        }

        foreach (Control control in controls.Where(c => !c.IsDisposed))
        {
            if (control.Visible && (control.Width <= 1 || control.Height <= 1))
                findings.Add(new("UI-002", DiagnosticSeverity.Critical, ControlArea(control), $"Controllo visibile con dimensione non valida: {ControlName(control)}.", true));

            if (control is Panel panel && panel.Visible && panel.Controls.Count == 0 && panel.Dock == DockStyle.Fill)
                findings.Add(new("UI-003", DiagnosticSeverity.Warning, ControlArea(panel), $"Pannello centrale vuoto: {ControlName(panel)}.", true));

            if (control is Button button && string.IsNullOrWhiteSpace(button.Text))
                findings.Add(new("BTN-001", DiagnosticSeverity.Warning, ControlArea(button), "Pulsante senza testo.", false));

            if (control.Right < 0 || control.Bottom < 0)
                findings.Add(new("UI-004", DiagnosticSeverity.Warning, ControlArea(control), $"Controllo fuori dall'area visibile: {ControlName(control)}.", true));
        }

        foreach (Control parent in controls.Where(c => c.Visible && c.Controls.Count is > 1 and < 40))
        {
            Control[] children = parent.Controls.Cast<Control>()
                .Where(c => c.Visible && c.Width > 20 && c.Height > 20 && c.Dock == DockStyle.None)
                .ToArray();
            for (int i = 0; i < children.Length; i++)
            {
                for (int j = i + 1; j < children.Length; j++)
                {
                    Rectangle intersection = Rectangle.Intersect(children[i].Bounds, children[j].Bounds);
                    if (intersection.Width > 20 && intersection.Height > 20)
                    {
                        findings.Add(new("UI-005", DiagnosticSeverity.Warning, ControlArea(parent), $"Possibile sovrapposizione tra {ControlName(children[i])} e {ControlName(children[j])}.", true));
                        i = children.Length;
                        break;
                    }
                }
            }
        }

        string settings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FF Guardian", "advanced-settings-v81.json");
        string logs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FF Guardian", "Logs");
        CheckFolder(findings, "CFG-001", "Configurazione", Path.GetDirectoryName(settings) ?? string.Empty);
        CheckFolder(findings, "LOG-001", "Registri", logs);

        if (File.Exists(settings) && new FileInfo(settings).Length == 0)
            findings.Add(new("CFG-002", DiagnosticSeverity.Critical, "Configurazione", "File impostazioni vuoto.", false));

        if (!findings.Any())
            findings.Add(new("OK-000", DiagnosticSeverity.Info, "Sistema", "Nessun problema strutturale rilevato nella sessione corrente.", false));

        return findings
            .GroupBy(f => (f.Code, f.Area, f.Description))
            .Select(group => group.First())
            .OrderByDescending(f => f.Severity)
            .ThenBy(f => f.Code)
            .ToList();
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
        string path = Path.Combine(folder, $"FFGuardian-Bug-Diagnostics-8.3.5-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        StringBuilder text = new();
        text.AppendLine("FF GUARDIAN 8.3.5 - DEEP BUG DIAGNOSTICS");
        text.AppendLine($"Data: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        text.AppendLine($"Computer: {Environment.MachineName}");
        text.AppendLine($"Windows: {Environment.OSVersion}");
        text.AppendLine($"Finestra: {form.ClientSize.Width}x{form.ClientSize.Height}");
        text.AppendLine($"Risultati: {findings.Count}");
        text.AppendLine(new string('-', 72));
        foreach (DiagnosticFinding finding in findings)
            text.AppendLine($"[{finding.Code}] {SeverityText(finding.Severity)} | {finding.Area} | {finding.Description} | Auto-fix: {(finding.AutoFixAvailable ? "Sì" : "No")}");

        string temp = path + ".tmp";
        await File.WriteAllTextAsync(temp, text.ToString());
        File.Move(temp, path, true);
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
        return grid;
    }

    private static Button ActionButton(string text, int width)
    {
        Button button = new()
        {
            Text = text,
            Width = width,
            Height = 44,
            Margin = new Padding(0, 0, 10, 0),
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

    private static string ControlArea(Control control)
    {
        Form? form = control.FindForm();
        Label? title = form is null ? null : Descendants(form).OfType<Label>()
            .FirstOrDefault(label => label.Visible && label.Font.Bold && label.Font.Size >= 16F);
        return title?.Text.Trim() ?? control.Parent?.Name ?? "Interfaccia";
    }

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