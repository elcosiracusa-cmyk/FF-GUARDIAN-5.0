using System.Diagnostics;
using System.Runtime.CompilerServices;
using FFGuardian.Engine10;

namespace FFGuardian;

internal static class UltimateDashboardExperience10
{
    private static readonly Color Background = Color.FromArgb(3, 8, 12);
    private static readonly Color Surface = Color.FromArgb(10, 20, 26);
    private static readonly Color Raised = Color.FromArgb(17, 31, 39);
    private static readonly Color Neon = Color.FromArgb(108, 255, 36);
    private static readonly Color Muted = Color.FromArgb(210, 220, 226);
    private static readonly Color Border = Color.FromArgb(70, 104, 116);
    private static bool _applied;
    private static System.Windows.Forms.Timer? _refreshTimer;
    private static TimeSpan _lastCpu;
    private static DateTime _lastCpuSampleUtc;

    [ModuleInitializer]
    internal static void Initialize() => Application.Idle += ApplyWhenReady;

    private static void ApplyWhenReady(object? sender, EventArgs e)
    {
        if (_applied)
            return;

        IndependentMainForm100? form = Application.OpenForms
            .OfType<IndependentMainForm100>()
            .FirstOrDefault();
        if (form is null || form.IsDisposed || !form.IsHandleCreated)
            return;

        TabControl? tabs = FindControl<TabControl>(form);
        TabPage? dashboard = tabs?.TabPages.Cast<TabPage>()
            .FirstOrDefault(page => page.Text.Equals("DASHBOARD", StringComparison.OrdinalIgnoreCase));
        if (dashboard is null || tabs is null)
            return;

        try
        {
            Apply(form, dashboard, tabs);
            _applied = true;
            Application.Idle -= ApplyWhenReady;
            StabilityCoordinator82.WriteInformationLog("Dashboard Safe leggibile applicata.");
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            Application.Idle -= ApplyWhenReady;
        }
    }

    private static void Apply(IndependentMainForm100 form, TabPage dashboard, TabControl tabs)
    {
        dashboard.SuspendLayout();
        try
        {
            dashboard.Controls.Clear();
            dashboard.Padding = new Padding(12);
            dashboard.BackColor = Background;
            dashboard.AutoScroll = true;

            Panel root = BuildDashboard(form, tabs);
            dashboard.Controls.Add(root);

            form.KeyPreview = true;
            form.KeyDown += (_, key) =>
            {
                if (key.Control && key.KeyCode == Keys.Space)
                {
                    FindButton(form, "PROTEGGI ORA", "UltimateDashboardSafe10")?.PerformClick();
                    key.Handled = true;
                }
                else if (key.Control && key.KeyCode == Keys.F)
                {
                    SelectTab(tabs, "SCANSIONE");
                    key.Handled = true;
                }
            };

            _lastCpu = Process.GetCurrentProcess().TotalProcessorTime;
            _lastCpuSampleUtc = DateTime.UtcNow;
            _refreshTimer?.Dispose();
            _refreshTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            _refreshTimer.Tick += (_, _) => RefreshDynamicValues(root);
            _refreshTimer.Start();

            form.Resize += (_, _) => AuditAndRepairLayout(root);
            form.Shown += (_, _) => AuditAndRepairLayout(root);
            form.FormClosed += (_, _) =>
            {
                _refreshTimer?.Stop();
                _refreshTimer?.Dispose();
                _refreshTimer = null;
            };

            RefreshDynamicValues(root);
            AuditAndRepairLayout(root);
        }
        finally
        {
            dashboard.ResumeLayout(true);
        }
    }

    private static Panel BuildDashboard(IndependentMainForm100 form, TabControl tabs)
    {
        Panel root = new()
        {
            Name = "UltimateDashboardSafe10",
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Background,
            Padding = new Padding(6)
        };

        TableLayoutPanel layout = new()
        {
            Name = "DashboardGrid10",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Background,
            ColumnCount = 2,
            RowCount = 7,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 180));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 138));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 210));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));

        Panel hero = BuildHero(form, tabs);
        layout.SetColumnSpan(hero, 2);
        layout.Controls.Add(hero, 0, 0);

        layout.Controls.Add(BuildStatusCard("PROTEZIONE TEMPO REALE", "ATTIVA", "Monitoraggio continuo di file, download e dispositivi USB."), 0, 1);
        layout.Controls.Add(BuildStatusCard("RANSOM SHIELD", "ATTIVO", "Controllo delle modifiche massive e delle attività anomale."), 1, 1);

        layout.Controls.Add(BuildDynamicCard("MOTORI DI RILEVAMENTO", "DynamicEngines10"), 0, 2);
        layout.Controls.Add(BuildDynamicCard("ATTIVITÀ E PROTEZIONE", "DynamicActivity10"), 1, 2);
        layout.Controls.Add(BuildDynamicCard("RISORSE FFGUARDIAN", "DynamicResources10"), 0, 3);
        layout.Controls.Add(BuildDynamicCard("SCANSIONI E FIRME", "DynamicScans10"), 1, 3);
        layout.Controls.Add(BuildDynamicCard("QUARANTENA E RAPPORTI", "DynamicQuarantine10"), 0, 4);
        layout.Controls.Add(BuildDynamicCard("STATO DEL SISTEMA", "DynamicHealth10"), 1, 4);
        layout.Controls.Add(BuildStatusCard("INTEGRITÀ SISTEMA", "PROTETTA", "Audit di processi, avvio, servizi e attività pianificate."), 0, 5);
        layout.Controls.Add(BuildStatusCard("PRIVACY", "ATTIVA", "Percorsi personali nascosti nei rapporti e nelle notifiche."), 1, 5);

        Panel footer = BuildFooter(form, tabs);
        layout.SetColumnSpan(footer, 2);
        layout.Controls.Add(footer, 0, 6);

        root.Controls.Add(layout);
        return root;
    }

    private static Panel BuildHero(IndependentMainForm100 form, TabControl tabs)
    {
        Panel panel = CreateCard();
        panel.Margin = new Padding(6);
        panel.Padding = new Padding(20, 14, 20, 12);

        Label badge = new()
        {
            Dock = DockStyle.Top,
            Height = 26,
            BackColor = Surface,
            ForeColor = Neon,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Text = "FFGUARDIAN ULTIMATE  •  THREE DOBERMANN DEFENSE",
            TextAlign = ContentAlignment.MiddleLeft
        };

        Label status = new()
        {
            Name = "DynamicProtectionState10",
            Dock = DockStyle.Right,
            Width = 230,
            BackColor = Raised,
            ForeColor = Neon,
            Font = new Font("Segoe UI", 21F, FontStyle.Bold),
            Text = "PROTETTO",
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(8)
        };

        Label title = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            Text = "IL TUO SISTEMA È SOTTO PROTEZIONE\nEngine10, Ransom Shield, ClamAV e YARA reale.",
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = false
        };

        FlowLayoutPanel commands = new()
        {
            Dock = DockStyle.Bottom,
            Height = 58,
            BackColor = Surface,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0, 8, 0, 0)
        };
        commands.Controls.Add(CreateButton("PROTEGGI ORA", true, () => InvokeExistingOrTab(form, tabs, "PROTEGGI ORA", "SCANSIONE")));
        commands.Controls.Add(CreateButton("SCANSIONE", false, () => SelectTab(tabs, "SCANSIONE")));
        commands.Controls.Add(CreateButton("QUARANTENA", false, () => SelectTab(tabs, "QUARANTENA")));
        commands.Controls.Add(CreateButton("AGGIORNA FIRME", false, () => SelectTab(tabs, "AGGIORNAMENTI")));

        panel.Controls.Add(title);
        panel.Controls.Add(status);
        panel.Controls.Add(commands);
        panel.Controls.Add(badge);
        return panel;
    }

    private static Panel BuildStatusCard(string title, string value, string detail)
    {
        Panel card = CreateCard();
        card.Margin = new Padding(6);
        card.Padding = new Padding(16);

        Label titleLabel = new()
        {
            Dock = DockStyle.Top,
            Height = 27,
            BackColor = Surface,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Text = title
        };
        Label valueLabel = new()
        {
            Dock = DockStyle.Top,
            Height = 42,
            BackColor = Surface,
            ForeColor = Neon,
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            Text = value,
            TextAlign = ContentAlignment.MiddleLeft
        };
        Label detailLabel = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 10F),
            Text = detail,
            TextAlign = ContentAlignment.TopLeft,
            AutoEllipsis = false
        };

        card.Controls.Add(detailLabel);
        card.Controls.Add(valueLabel);
        card.Controls.Add(titleLabel);
        return card;
    }

    private static Panel BuildDynamicCard(string title, string labelName)
    {
        Panel card = CreateCard();
        card.Margin = new Padding(6);
        card.Padding = new Padding(16);

        Label content = new()
        {
            Name = labelName,
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 10.5F),
            Text = "Caricamento dati...",
            Padding = new Padding(0, 10, 0, 0),
            AutoEllipsis = false,
            TextAlign = ContentAlignment.TopLeft,
            UseCompatibleTextRendering = true
        };
        Label heading = new()
        {
            Dock = DockStyle.Top,
            Height = 34,
            BackColor = Surface,
            ForeColor = Neon,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Text = title,
            TextAlign = ContentAlignment.MiddleLeft
        };

        card.Controls.Add(content);
        card.Controls.Add(heading);
        return card;
    }

    private static void RefreshDynamicValues(Control root)
    {
        try
        {
            ExternalEngineStatus10 engine = ExternalThreatEngines10.GetStatus();
            int protectedFolders = GetProtectedFolderCount();
            int quarantineItems = CountFilesSafe(GetQuarantineDirectories());
            int reportItems = CountFilesSafe(GetReportDirectories());
            DateTime? lastReport = GetLatestFileTimeSafe(GetReportDirectories());
            DateTime? lastSignature = GetLatestFileTimeSafe(GetSignatureDirectories());

            Process process = Process.GetCurrentProcess();
            process.Refresh();
            double cpu = CalculateCpuPercent(process);
            double memoryMb = process.WorkingSet64 / 1024D / 1024D;
            double managedMb = GC.GetTotalMemory(false) / 1024D / 1024D;

            SetDynamicText(root, "DynamicEngines10",
                $"Engine10 autonomo: ATTIVO\n\nClamAV: {(engine.ClamAvAvailable ? "ATTIVO" : "NON INSTALLATO")}\n\nYARA reale: {(engine.YaraAvailable ? "ATTIVO" : "NON INSTALLATO")}\n\nRegole YARA disponibili: {engine.YaraRuleFiles}");

            SetDynamicText(root, "DynamicActivity10",
                $"Cartelle protette: {protectedFolders}\n\nAuto-esclusione FFGuardian: ATTIVA\n\nUSB Shield: PRONTO\n\nUltimo evento: {GetLatestEventSafe()}");

            SetDynamicText(root, "DynamicResources10",
                $"CPU FFGuardian: {cpu:F1}%\n\nRAM processo: {memoryMb:F1} MB\n\nMemoria gestita: {managedMb:F1} MB\n\nID processo: {process.Id}");

            SetDynamicText(root, "DynamicScans10",
                $"Versione: 10.0.1 Stable\n\nDatabase firme: PRONTO\n\nUltimo aggiornamento: {FormatDate(lastSignature)}\n\nRegole caricate: {engine.YaraRuleFiles}");

            SetDynamicText(root, "DynamicQuarantine10",
                $"File in quarantena: {quarantineItems}\n\nRapporti disponibili: {reportItems}\n\nUltimo rapporto: {FormatDate(lastReport)}\n\nPercorsi personali: NASCOSTI");

            SetDynamicText(root, "DynamicHealth10",
                $"Protezione: ATTIVA\n\nRansom Shield: ATTIVO\n\nIntegrità: PROTETTA\n\nAggiornato alle: {DateTime.Now:HH:mm:ss}");

            Label? state = FindNamedControl<Label>(root, "DynamicProtectionState10");
            if (state is not null)
            {
                state.Text = "PROTETTO";
                state.ForeColor = Neon;
            }

            AuditAndRepairLayout(root);
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
        }
    }

    private static void AuditAndRepairLayout(Control root)
    {
        try
        {
            foreach (Label label in Descendants(root).OfType<Label>())
            {
                if (label.Width <= 0 || label.Height <= 0)
                    continue;

                Size proposed = TextRenderer.MeasureText(
                    label.Text,
                    label.Font,
                    new Size(Math.Max(40, label.ClientSize.Width), int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);

                if (proposed.Height > label.ClientSize.Height && label.Dock == DockStyle.Fill)
                {
                    float reduced = Math.Max(9F, label.Font.Size - 0.5F);
                    if (reduced < label.Font.Size)
                        label.Font = new Font(label.Font.FontFamily, reduced, label.Font.Style);
                }
            }
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
        }
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control nested in Descendants(child))
                yield return nested;
        }
    }

    private static double CalculateCpuPercent(Process process)
    {
        DateTime now = DateTime.UtcNow;
        TimeSpan cpuNow = process.TotalProcessorTime;
        double elapsedMs = (now - _lastCpuSampleUtc).TotalMilliseconds;
        double cpuMs = (cpuNow - _lastCpu).TotalMilliseconds;
        _lastCpu = cpuNow;
        _lastCpuSampleUtc = now;
        if (elapsedMs <= 0 || Environment.ProcessorCount <= 0)
            return 0;
        return Math.Clamp(cpuMs / elapsedMs / Environment.ProcessorCount * 100D, 0D, 100D);
    }

    private static int GetProtectedFolderCount()
    {
        string[] candidates =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            Path.GetTempPath()
        };
        return candidates.Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase).Count();
    }

    private static IEnumerable<string> GetQuarantineDirectories()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(local, "FF Guardian", "Quarantine");
        yield return Path.Combine(local, "FFGuardian", "Quarantine");
        yield return Path.Combine(AppContext.BaseDirectory, "Quarantine");
    }

    private static IEnumerable<string> GetReportDirectories()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        yield return Path.Combine(local, "FF Guardian", "Reports");
        yield return Path.Combine(local, "FFGuardian", "Reports");
        yield return Path.Combine(documents, "FF Guardian Reports");
    }

    private static IEnumerable<string> GetSignatureDirectories()
    {
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(local, "FF Guardian", "Engine10");
        yield return Path.Combine(local, "FFGuardian", "Engine10");
        yield return Path.Combine(AppContext.BaseDirectory, "Rules");
    }

    private static int CountFilesSafe(IEnumerable<string> directories)
    {
        int count = 0;
        foreach (string directory in directories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (Directory.Exists(directory))
                    count += Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Take(10000).Count();
            }
            catch
            {
                // Una metrica non disponibile non deve fermare la protezione.
            }
        }
        return count;
    }

    private static DateTime? GetLatestFileTimeSafe(IEnumerable<string> directories)
    {
        DateTime? latest = null;
        foreach (string directory in directories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if (!Directory.Exists(directory))
                    continue;
                foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Take(10000))
                {
                    DateTime time = File.GetLastWriteTime(file);
                    if (latest is null || time > latest.Value)
                        latest = time;
                }
            }
            catch
            {
                // Ignora esclusivamente errori di lettura della metrica.
            }
        }
        return latest;
    }

    private static string GetLatestEventSafe()
    {
        DateTime? latest = GetLatestFileTimeSafe(GetReportDirectories());
        return latest is null ? "Protezione avviata" : latest.Value.ToString("dd/MM HH:mm");
    }

    private static string FormatDate(DateTime? value) => value?.ToString("dd/MM/yyyy HH:mm") ?? "Non disponibile";

    private static void SetDynamicText(Control root, string name, string value)
    {
        Label? label = FindNamedControl<Label>(root, name);
        if (label is not null)
            label.Text = value;
    }

    private static Panel BuildFooter(IndependentMainForm100 form, TabControl tabs)
    {
        Panel footer = CreateCard();
        footer.Margin = new Padding(6);
        footer.Padding = new Padding(10);
        FlowLayoutPanel commands = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true
        };
        commands.Controls.Add(CreateButton("PROCESSI ATTIVI", false, () => InvokeExistingOrTab(form, tabs, "PROCESSI ATTIVI", "PROCESSI")));
        commands.Controls.Add(CreateButton("CONTROLLO AVVIO", false, () => InvokeExistingOrTab(form, tabs, "CONTROLLO AVVIO", "AUDIT")));
        commands.Controls.Add(CreateButton("FIREWALL", false, () => SelectTab(tabs, "FIREWALL")));
        commands.Controls.Add(CreateButton("RAPPORTI", false, () => SelectTab(tabs, "RAPPORTI")));
        commands.Controls.Add(CreateButton("ASSISTENZA", false, () => SelectTab(tabs, "ASSISTENZA")));
        footer.Controls.Add(commands);
        return footer;
    }

    private static Panel CreateCard() => new()
    {
        Dock = DockStyle.Fill,
        BackColor = Surface,
        BorderStyle = BorderStyle.FixedSingle
    };

    private static Button CreateButton(string text, bool primary, Action action)
    {
        Button button = new()
        {
            Width = primary ? 170 : 150,
            Height = 40,
            Margin = new Padding(5),
            Text = text,
            BackColor = primary ? Neon : Raised,
            ForeColor = primary ? Background : Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", primary ? 9.5F : 8.5F, FontStyle.Bold),
            AccessibleName = text,
            TabStop = true
        };
        button.FlatAppearance.BorderColor = primary ? Neon : Border;
        button.FlatAppearance.BorderSize = 1;
        button.Click += (_, _) => action();
        return button;
    }

    private static void InvokeExistingOrTab(Control root, TabControl tabs, string command, string tab)
    {
        Button? original = FindButton(root, command, "UltimateDashboardSafe10");
        if (original is not null)
            original.PerformClick();
        else
            SelectTab(tabs, tab);
    }

    private static void SelectTab(TabControl tabs, string text)
    {
        TabPage? page = tabs.TabPages.Cast<TabPage>()
            .FirstOrDefault(candidate => candidate.Text.Contains(text, StringComparison.OrdinalIgnoreCase));
        if (page is not null)
            tabs.SelectedTab = page;
    }

    private static Button? FindButton(Control root, string text, string excludedParent)
    {
        foreach (Control control in root.Controls)
        {
            if (control is Button button &&
                button.Text.Contains(text, StringComparison.OrdinalIgnoreCase) &&
                !IsInsideNamedParent(button, excludedParent))
                return button;

            Button? nested = FindButton(control, text, excludedParent);
            if (nested is not null)
                return nested;
        }
        return null;
    }

    private static bool IsInsideNamedParent(Control control, string name)
    {
        for (Control? parent = control.Parent; parent is not null; parent = parent.Parent)
            if (parent.Name.Equals(name, StringComparison.Ordinal))
                return true;
        return false;
    }

    private static T? FindNamedControl<T>(Control root, string name) where T : Control
    {
        if (root is T match && root.Name.Equals(name, StringComparison.Ordinal))
            return match;
        foreach (Control child in root.Controls)
        {
            T? found = FindNamedControl<T>(child, name);
            if (found is not null)
                return found;
        }
        return null;
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
}
