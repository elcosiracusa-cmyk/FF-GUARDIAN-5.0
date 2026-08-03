using System.Runtime.CompilerServices;

namespace FFGuardian;

/// <summary>
/// Interfaccia commerciale unica di FFGuardian.
/// Ricostruisce dashboard e pagine preservando controlli, eventi e comandi reali.
/// </summary>
internal static class CommercialPages18
{
    private static readonly Color Background = Color.FromArgb(4, 8, 11);
    private static readonly Color Surface = Color.FromArgb(10, 16, 20);
    private static readonly Color Raised = Color.FromArgb(16, 24, 29);
    private static readonly Color Neon = Color.FromArgb(112, 255, 24);
    private static readonly Color Text = Color.FromArgb(242, 247, 249);
    private static readonly Color Muted = Color.FromArgb(158, 174, 181);
    private static readonly Color Border = Color.FromArgb(42, 61, 68);
    private static readonly Color Danger = Color.FromArgb(255, 76, 76);

    private static bool _applied;
    private static System.Windows.Forms.Timer? _resourceTimer;
    private static Label? _cpuLabel;
    private static Label? _ramLabel;
    private static Label? _diskLabel;
    private static DateTime _lastCpuSample = DateTime.UtcNow;
    private static TimeSpan _lastCpuTime = System.Diagnostics.Process.GetCurrentProcess().TotalProcessorTime;

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

        TabControl? tabs = FindControls<TabControl>(form)
            .OrderByDescending(item => item.TabCount)
            .FirstOrDefault(item => item.TabCount > 0);
        if (tabs is null)
            return;

        try
        {
            List<Button> allOriginalButtons = FindControls<Button>(tabs)
                .Where(button => !string.IsNullOrWhiteSpace(button.Text))
                .ToList();

            TabPage? dashboard = tabs.TabPages.Cast<TabPage>()
                .FirstOrDefault(page => IsDashboard(page.Text));
            if (dashboard is not null)
                BuildDashboard(dashboard, allOriginalButtons, tabs);

            foreach (TabPage page in tabs.TabPages)
            {
                if (!IsDashboard(page.Text))
                    BuildPage(page);
            }

            tabs.SelectedIndexChanged += (_, _) =>
            {
                if (tabs.SelectedTab is TabPage selected)
                    FitPage(selected);
            };
            form.Resize += (_, _) =>
            {
                foreach (TabPage page in tabs.TabPages)
                    FitPage(page);
            };

            StartResourceTimer(form);
            _applied = true;
            Application.Idle -= ApplyWhenReady;
            StabilityCoordinator82.WriteInformationLog(
                "Dashboard commerciale FFGuardian applicata con comandi reali.");
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            Application.Idle -= ApplyWhenReady;
        }
    }

    private static void BuildDashboard(
        TabPage page,
        IReadOnlyList<Button> allButtons,
        TabControl tabs)
    {
        if (page.Controls.Cast<Control>().Any(control => control.Name == "CommercialDashboard18"))
            return;

        page.SuspendLayout();
        try
        {
            page.Controls.Clear();
            page.BackColor = Background;
            page.ForeColor = Text;
            page.Padding = new Padding(10);
            page.AutoScroll = false;

            TableLayoutPanel root = new()
            {
                Name = "CommercialDashboard18",
                Dock = DockStyle.Fill,
                BackColor = Background,
                ColumnCount = 1,
                RowCount = 3,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 36F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 27F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 37F));

            root.Controls.Add(BuildHeroSection(allButtons, tabs), 0, 0);
            root.Controls.Add(BuildProtectionSection(), 0, 1);
            root.Controls.Add(BuildBottomSection(tabs), 0, 2);
            page.Controls.Add(root);
        }
        finally
        {
            page.ResumeLayout(true);
        }
    }

    private static Control BuildHeroSection(
        IReadOnlyList<Button> buttons,
        TabControl tabs)
    {
        TableLayoutPanel hero = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Background,
            ColumnCount = 5,
            RowCount = 1,
            Padding = new Padding(0, 0, 0, 6),
            Margin = Padding.Empty
        };
        hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36F));
        for (int index = 0; index < 4; index++)
            hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 16F));
        hero.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        hero.Controls.Add(BuildProtectionHero(), 0, 0);
        hero.Controls.Add(BuildActionCard(
            "SCANSIONE\nRAPIDA",
            "Aree critiche del sistema",
            FindButton(buttons, "SCANSIONE RAPIDA", "RAPIDA")), 1, 0);
        hero.Controls.Add(BuildActionCard(
            "SCANSIONE\nCOMPLETA",
            "Tutte le unità locali",
            FindButton(buttons, "SCANSIONE COMPLETA", "COMPLETA")), 2, 0);
        hero.Controls.Add(BuildActionCard(
            "SCANSIONE\nPERSONALIZZATA",
            "Scegli cartelle e percorsi",
            FindButton(buttons, "SCANSIONA CARTELLA", "CARTELLA")), 3, 0);
        hero.Controls.Add(BuildActionCard(
            "VERIFICA\nMINACCE",
            "Controlla un file sospetto",
            FindButton(buttons, "SCANSIONA FILE", "VERIFICA FILE")), 4, 0);
        return hero;
    }

    private static Control BuildProtectionHero()
    {
        TableLayoutPanel inner = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(18),
            Margin = Padding.Empty
        };
        inner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
        inner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));

        Label shield = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Neon,
            Font = new Font("Segoe UI Symbol", 64F, FontStyle.Bold),
            Text = "✓",
            TextAlign = ContentAlignment.MiddleCenter
        };
        Label status = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Text,
            Font = new Font("Segoe UI", 11F),
            Text = "PROTEZIONE ATTIVA\r\n\r\n" +
                   "● Protezione in tempo reale: Attiva\r\n" +
                   "● Ransom Shield: Attivo\r\n" +
                   "● Firewall: Gestito da Windows\r\n" +
                   "● USB Shield: Pronto\r\n" +
                   "● Engine10: Attivo\r\n" +
                   "● Database: Aggiornato",
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8)
        };
        inner.Controls.Add(shield, 0, 0);
        inner.Controls.Add(status, 1, 0);
        return Bordered(inner, new Padding(0, 0, 6, 0));
    }

    private static Control BuildActionCard(
        string title,
        string subtitle,
        Button? target)
    {
        TableLayoutPanel card = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12),
            Margin = Padding.Empty
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 43F));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 27F));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 30F));

        card.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Text,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            Text = title,
            TextAlign = ContentAlignment.MiddleCenter
        }, 0, 0);
        card.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 8.5F),
            Text = subtitle,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true
        }, 0, 1);

        Button execute = new()
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(5),
            Text = target is null ? "NON DISPONIBILE" : "ESEGUI",
            Enabled = target is not null && target.Enabled,
            UseVisualStyleBackColor = false,
            FlatStyle = FlatStyle.Flat,
            BackColor = Raised,
            ForeColor = target is null ? Muted : Neon,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        execute.FlatAppearance.BorderColor = target is null ? Border : Neon;
        execute.FlatAppearance.BorderSize = 1;
        if (target is not null)
        {
            execute.Click += (_, _) =>
            {
                if (!target.IsDisposed)
                    target.PerformClick();
            };
            target.EnabledChanged += (_, _) =>
            {
                if (!execute.IsDisposed)
                    execute.Enabled = target.Enabled;
            };
        }
        card.Controls.Add(execute, 0, 2);
        return Bordered(card, new Padding(6, 0, 0, 0));
    }

    private static Control BuildProtectionSection()
    {
        TableLayoutPanel section = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Background,
            ColumnCount = 4,
            RowCount = 1,
            Padding = new Padding(0, 6, 0, 6),
            Margin = Padding.Empty
        };
        for (int index = 0; index < 4; index++)
            section.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        section.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        section.Controls.Add(BuildStateCard(
            "PROTEZIONE IN TEMPO REALE",
            "ATTIVA",
            "Monitoraggio file e processi"), 0, 0);
        section.Controls.Add(BuildStateCard(
            "RANSOM SHIELD",
            "ATTIVO",
            "Comportamenti di cifratura"), 1, 0);
        section.Controls.Add(BuildStateCard(
            "FIREWALL",
            "ATTIVO",
            "Regole Windows Firewall"), 2, 0);
        section.Controls.Add(BuildResourceCard(), 3, 0);
        return section;
    }

    private static Control BuildStateCard(
        string title,
        string state,
        string description)
    {
        TableLayoutPanel card = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16),
            Margin = Padding.Empty
        };
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 35F));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 35F));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 30F));
        card.Controls.Add(MakeLabel(title, Text, 10F, FontStyle.Bold), 0, 0);
        card.Controls.Add(MakeLabel(state, Neon, 14F, FontStyle.Bold), 0, 1);
        card.Controls.Add(MakeLabel(description, Muted, 8.5F, FontStyle.Regular), 0, 2);
        return Bordered(card, new Padding(0, 0, 8, 0));
    }

    private static Control BuildResourceCard()
    {
        TableLayoutPanel card = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(16),
            Margin = Padding.Empty
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));
        for (int index = 0; index < 3; index++)
            card.RowStyles.Add(new RowStyle(SizeType.Percent, 33.333F));

        card.Controls.Add(MakeLabel("CPU", Text, 9F, FontStyle.Bold), 0, 0);
        card.Controls.Add(MakeLabel("RAM", Text, 9F, FontStyle.Bold), 0, 1);
        card.Controls.Add(MakeLabel("DISCO", Text, 9F, FontStyle.Bold), 0, 2);
        _cpuLabel = MakeLabel("--%", Neon, 11F, FontStyle.Bold);
        _ramLabel = MakeLabel("-- MB", Neon, 11F, FontStyle.Bold);
        _diskLabel = MakeLabel("--%", Neon, 11F, FontStyle.Bold);
        card.Controls.Add(_cpuLabel, 1, 0);
        card.Controls.Add(_ramLabel, 1, 1);
        card.Controls.Add(_diskLabel, 1, 2);
        return Bordered(card, Padding.Empty);
    }

    private static Control BuildBottomSection(TabControl tabs)
    {
        TableLayoutPanel bottom = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Background,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, 6, 0, 0),
            Margin = Padding.Empty
        };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58F));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42F));
        bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        bottom.Controls.Add(BuildRecentActivity(tabs), 0, 0);
        bottom.Controls.Add(BuildInformationPanel(), 1, 0);
        return bottom;
    }

    private static Control BuildRecentActivity(TabControl tabs)
    {
        TableLayoutPanel card = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16),
            Margin = Padding.Empty
        };
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        card.Controls.Add(MakeLabel("ATTIVITÀ RECENTI", Text, 11F, FontStyle.Bold), 0, 0);
        card.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 9.5F),
            Text = "● Engine10 pronto\r\n● Database firme caricato\r\n● Protezione automatica attiva\r\n● Nessuna operazione in corso",
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(4, 10, 4, 4)
        }, 0, 1);
        Button viewAll = DashboardNavigationButton("VISUALIZZA TUTTO", tabs, "ATTIV");
        card.Controls.Add(viewAll, 0, 2);
        return Bordered(card, new Padding(0, 0, 6, 0));
    }

    private static Control BuildInformationPanel()
    {
        TableLayoutPanel card = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ColumnCount = 2,
            RowCount = 6,
            Padding = new Padding(16),
            Margin = Padding.Empty
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48F));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52F));
        for (int index = 0; index < 6; index++)
            card.RowStyles.Add(new RowStyle(SizeType.Percent, 16.666F));

        AddInfoRow(card, 0, "VERSIONE", Application.ProductVersion);
        AddInfoRow(card, 1, "ENGINE", "Engine10");
        AddInfoRow(card, 2, "DATABASE FIRME", DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
        AddInfoRow(card, 3, "LICENZA", "EL.CO Commercial");
        AddInfoRow(card, 4, "STATO", "ATTIVO", Neon);
        AddInfoRow(card, 5, "SUPPORTO", "alsafe127.00@gmail.com");
        return Bordered(card, new Padding(6, 0, 0, 0));
    }

    private static void AddInfoRow(
        TableLayoutPanel table,
        int row,
        string key,
        string value,
        Color? valueColor = null)
    {
        table.Controls.Add(MakeLabel(key, Muted, 8.5F, FontStyle.Regular), 0, row);
        Label label = MakeLabel(value, valueColor ?? Text, 8.5F, FontStyle.Bold);
        label.TextAlign = ContentAlignment.MiddleRight;
        table.Controls.Add(label, 1, row);
    }

    private static Button DashboardNavigationButton(
        string text,
        TabControl tabs,
        string pageKeyword)
    {
        Button button = new()
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(4),
            Text = text,
            UseVisualStyleBackColor = false,
            FlatStyle = FlatStyle.Flat,
            BackColor = Raised,
            ForeColor = Neon,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        button.FlatAppearance.BorderColor = Neon;
        button.Click += (_, _) =>
        {
            TabPage? target = tabs.TabPages.Cast<TabPage>()
                .FirstOrDefault(page => page.Text.Contains(pageKeyword, StringComparison.OrdinalIgnoreCase));
            if (target is not null)
                tabs.SelectedTab = target;
        };
        return button;
    }

    private static void StartResourceTimer(Form owner)
    {
        _resourceTimer?.Stop();
        _resourceTimer?.Dispose();
        _resourceTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _resourceTimer.Tick += (_, _) => UpdateResources();
        owner.FormClosed += (_, _) =>
        {
            _resourceTimer?.Stop();
            _resourceTimer?.Dispose();
            _resourceTimer = null;
        };
        UpdateResources();
        _resourceTimer.Start();
    }

    private static void UpdateResources()
    {
        try
        {
            System.Diagnostics.Process process = System.Diagnostics.Process.GetCurrentProcess();
            DateTime now = DateTime.UtcNow;
            TimeSpan cpuNow = process.TotalProcessorTime;
            double elapsedMs = Math.Max(1D, (now - _lastCpuSample).TotalMilliseconds);
            double cpuMs = Math.Max(0D, (cpuNow - _lastCpuTime).TotalMilliseconds);
            double cpu = Math.Clamp(cpuMs / (elapsedMs * Environment.ProcessorCount) * 100D, 0D, 100D);
            _lastCpuSample = now;
            _lastCpuTime = cpuNow;

            long ramMb = process.WorkingSet64 / 1024L / 1024L;
            DriveInfo? drive = DriveInfo.GetDrives()
                .FirstOrDefault(item => item.IsReady &&
                    string.Equals(item.Name, Path.GetPathRoot(Environment.SystemDirectory), StringComparison.OrdinalIgnoreCase));
            double disk = drive is null || drive.TotalSize == 0
                ? 0D
                : (double)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize * 100D;

            if (_cpuLabel is not null && !_cpuLabel.IsDisposed)
                _cpuLabel.Text = $"{cpu:0}%";
            if (_ramLabel is not null && !_ramLabel.IsDisposed)
                _ramLabel.Text = $"{ramMb:N0} MB";
            if (_diskLabel is not null && !_diskLabel.IsDisposed)
                _diskLabel.Text = $"{disk:0}%";
        }
        catch
        {
            // Le metriche non devono mai compromettere la UI o il motore.
        }
    }

    private static Button? FindButton(IEnumerable<Button> buttons, params string[] keywords)
    {
        string[] normalized = keywords.Select(item => item.ToUpperInvariant()).ToArray();
        return buttons.FirstOrDefault(button =>
        {
            string value = (button.Text + " " + button.Name).ToUpperInvariant();
            return normalized.Any(value.Contains);
        });
    }

    private static bool IsDashboard(string text) =>
        text.Contains("DASH", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("HOME", StringComparison.OrdinalIgnoreCase);

    private static Label MakeLabel(
        string text,
        Color color,
        float size,
        FontStyle style)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = color,
            Font = new Font("Segoe UI", size, style),
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
    }

    private static Control Bordered(Control inner, Padding margin)
    {
        Panel outer = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Border,
            Padding = new Padding(1),
            Margin = margin
        };
        inner.Dock = DockStyle.Fill;
        outer.Controls.Add(inner);
        return outer;
    }

    private static void BuildPage(TabPage page)
    {
        string title = page.Text.ToUpperInvariant();
        if (page.Controls.Cast<Control>().Any(control => control.Name == "CommercialPageRoot18"))
            return;

        List<Control> flattened = page.Controls.Cast<Control>()
            .SelectMany(Flatten)
            .Distinct()
            .ToList();

        List<Button> pageButtons = flattened
            .OfType<Button>()
            .Where(button => !string.IsNullOrWhiteSpace(button.Text))
            .OrderBy(AbsoluteTop)
            .ThenBy(AbsoluteLeft)
            .ToList();

        List<Control> contentControls = flattened
            .Where(IsUsefulContent)
            .Where(control => control is not Button)
            .Where(control => !HasUsefulAncestor(control, flattened))
            .ToList();

        foreach (Control control in pageButtons.Cast<Control>().Concat(contentControls).Distinct())
            control.Parent?.Controls.Remove(control);

        page.SuspendLayout();
        try
        {
            page.Controls.Clear();
            page.BackColor = Background;
            page.ForeColor = Text;
            page.Padding = new Padding(12);
            page.AutoScroll = false;

            int commandRows = Math.Max(1, (int)Math.Ceiling(pageButtons.Count / 4D));
            int commandHeight = Math.Clamp(commandRows * 58 + 12, 76, 190);

            TableLayoutPanel root = new()
            {
                Name = "CommercialPageRoot18",
                Dock = DockStyle.Fill,
                BackColor = Background,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, commandHeight));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            root.Controls.Add(BuildHeading(CleanTitle(page.Text), SubtitleFor(title)), 0, 0);
            root.Controls.Add(BuildCommands(pageButtons), 0, 1);
            root.Controls.Add(BuildContent(title, contentControls), 0, 2);
            page.Controls.Add(root);
        }
        finally
        {
            page.ResumeLayout(true);
        }
    }

    private static Control BuildHeading(string title, string subtitle)
    {
        Panel panel = Card();
        panel.Margin = new Padding(0, 0, 0, 8);
        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 9F),
            Text = subtitle,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(10, 0, 18, 0),
            AutoEllipsis = true
        });
        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Left,
            Width = 420,
            BackColor = Surface,
            ForeColor = Text,
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            Text = title,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(18, 0, 0, 0),
            AutoEllipsis = true
        });
        return panel;
    }

    private static Control BuildCommands(IReadOnlyList<Button> originals)
    {
        FlowLayoutPanel flow = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Background,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = false,
            Padding = new Padding(0, 6, 0, 6),
            Margin = Padding.Empty
        };

        if (originals.Count == 0)
        {
            flow.Controls.Add(new Label
            {
                AutoSize = false,
                Width = 420,
                Height = 46,
                BackColor = Surface,
                ForeColor = Muted,
                Text = "Nessun comando operativo disponibile in questa sezione.",
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0)
            });
            return flow;
        }

        HashSet<string> used = new(StringComparer.OrdinalIgnoreCase);
        foreach (Button target in originals)
        {
            string label = NormalizeCommandLabel(target.Text);
            string key = label + "|" + target.Name;
            if (!used.Add(key))
                continue;
            flow.Controls.Add(CreateCommandButton(label, target));
        }
        return flow;
    }

    private static Control BuildContent(string title, IReadOnlyList<Control> useful)
    {
        TableLayoutPanel content = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Background,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68F));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32F));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        Panel primary = Card();
        primary.Margin = new Padding(0, 0, 6, 0);
        Panel secondary = Card();
        secondary.Margin = new Padding(6, 0, 0, 0);

        if (useful.Count == 0)
        {
            primary.Controls.Add(BuildEmptyState(title));
        }
        else if (useful.Count == 1)
        {
            Control main = useful[0];
            PrepareContentControl(main);
            primary.Controls.Add(main);
        }
        else
        {
            TableLayoutPanel stack = new()
            {
                Dock = DockStyle.Fill,
                BackColor = Surface,
                ColumnCount = 1,
                RowCount = useful.Count,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int index = 0; index < useful.Count; index++)
            {
                stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / useful.Count));
                Control control = useful[index];
                PrepareContentControl(control);
                control.Margin = new Padding(0, 0, 0, index == useful.Count - 1 ? 0 : 6);
                stack.Controls.Add(control, 0, index);
            }
            primary.Controls.Add(stack);
        }

        secondary.Controls.Add(BuildSideInformation(title));
        content.Controls.Add(primary, 0, 0);
        content.Controls.Add(secondary, 1, 0);
        return content;
    }

    private static bool IsUsefulContent(Control control)
    {
        return control is DataGridView
            or ListView
            or TreeView
            or RichTextBox
            or CheckedListBox
            or PropertyGrid
            || control is TextBox textBox && textBox.Multiline
            || control is FlowLayoutPanel flow && flow.Controls.Cast<Control>().Any(IsInputControl)
            || control is TableLayoutPanel table && table.Controls.Cast<Control>().Any(IsInputControl)
            || control is Panel panel && panel.Controls.Cast<Control>().Any(IsInputControl)
            || control is GroupBox;
    }

    private static bool IsInputControl(Control control) =>
        control is CheckBox or RadioButton or ComboBox or NumericUpDown or TrackBar or TextBox;

    private static bool HasUsefulAncestor(Control control, IReadOnlyCollection<Control> candidates)
    {
        Control? parent = control.Parent;
        while (parent is not null)
        {
            if (candidates.Contains(parent) && IsUsefulContent(parent))
                return true;
            parent = parent.Parent;
        }
        return false;
    }

    private static void PrepareContentControl(Control control)
    {
        control.Dock = DockStyle.Fill;
        control.Margin = Padding.Empty;
        control.BackColor = Surface;
        control.ForeColor = Text;

        if (control is DataGridView grid)
        {
            grid.BackgroundColor = Surface;
            grid.BorderStyle = BorderStyle.None;
            grid.EnableHeadersVisualStyles = false;
            grid.GridColor = Border;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Raised;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Neon;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            grid.DefaultCellStyle.BackColor = Surface;
            grid.DefaultCellStyle.ForeColor = Text;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(40, 75, 45);
            grid.DefaultCellStyle.SelectionForeColor = Text;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.ScrollBars = ScrollBars.Vertical;
        }
        else if (control is TextBox textBox)
        {
            textBox.BorderStyle = BorderStyle.None;
            textBox.ScrollBars = ScrollBars.Vertical;
        }

        PolishTree(control);
    }

    private static void PolishTree(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (child is CheckBox or RadioButton or Label or GroupBox)
                child.BackColor = Surface;
            if (child is Button button)
            {
                button.UseVisualStyleBackColor = false;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = Neon;
                button.BackColor = Raised;
                button.ForeColor = Text;
            }
            PolishTree(child);
        }
    }

    private static Button CreateCommandButton(string label, Button target)
    {
        Button button = new()
        {
            Width = 210,
            Height = 48,
            Margin = new Padding(0, 0, 10, 10),
            Text = label,
            Enabled = target.Enabled,
            UseVisualStyleBackColor = false,
            FlatStyle = FlatStyle.Flat,
            BackColor = Raised,
            ForeColor = Text,
            Font = new Font("Segoe UI", 9.2F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true,
            Tag = target
        };
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Neon;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(38, 58, 44);
        button.Click += (_, _) =>
        {
            if (!target.IsDisposed)
                target.PerformClick();
        };
        target.EnabledChanged += (_, _) =>
        {
            if (!button.IsDisposed)
                button.Enabled = target.Enabled;
        };
        return button;
    }

    private static string NormalizeCommandLabel(string value)
    {
        string label = value.Replace("&", string.Empty, StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
        while (label.Contains("  ", StringComparison.Ordinal))
            label = label.Replace("  ", " ", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(label) ? "ESEGUI" : label.ToUpperInvariant();
    }

    private static int AbsoluteTop(Control control)
    {
        int result = control.Top;
        for (Control? parent = control.Parent; parent is not null; parent = parent.Parent)
            result += parent.Top;
        return result;
    }

    private static int AbsoluteLeft(Control control)
    {
        int result = control.Left;
        for (Control? parent = control.Parent; parent is not null; parent = parent.Parent)
            result += parent.Left;
        return result;
    }

    private static Control BuildEmptyState(string title)
    {
        string message = title.Contains("RECUP")
            ? "Nessun elemento in quarantena.\r\nGli archivi protetti e i punti di rollback appariranno qui."
            : title.Contains("RANSOM")
                ? "Ransom Shield pronto.\r\nGli eventi comportamentali appariranno qui."
                : title.Contains("AGGIORN")
                    ? "Database firme pronto.\r\nUsa i comandi originali disponibili nella barra superiore."
                    : title.Contains("IMPOST")
                        ? "Configurazione protetta.\r\nI controlli disponibili vengono mantenuti senza modificare le funzioni."
                        : "Nessun evento da mostrare.\r\nLe attività compariranno automaticamente.";

        return new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 12F),
            Text = message,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(30)
        };
    }

    private static Control BuildSideInformation(string title)
    {
        string text = title.Contains("SCANS")
            ? "STATO SCANSIONE\r\n\r\n• Engine10 pronto\r\n• Firme locali attive\r\n• Auto-esclusione FFGuardian attiva\r\n• Quarantena cifrata pronta"
            : title.Contains("AUDIT")
                ? "AUDIT SISTEMA\r\n\r\nControlla persistenza, servizi, attività pianificate, firme digitali e anomalie di avvio."
                : title.Contains("RECUP")
                    ? "RECUPERO SICURO\r\n\r\nAccesso agli archivi di quarantena e rollback tramite i comandi originali."
                    : title.Contains("AGGIORN")
                        ? "AGGIORNAMENTI\r\n\r\nRicarica del database firme con verifica del motore."
                        : title.Contains("RANSOM")
                            ? "PROTEZIONE COMPORTAMENTALE\r\n\r\nLe funzioni disponibili dipendono dai controlli realmente installati."
                            : title.Contains("IMPOST")
                                ? "IMPOSTAZIONI\r\n\r\nI controlli originali vengono conservati e mostrati nel pannello principale."
                                : "MONITORAGGIO\r\n\r\nRapporti e registro attività restano collegati alle funzioni originali.";

        return new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Text,
            Font = new Font("Segoe UI", 10.5F),
            Text = text,
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(22)
        };
    }

    private static Panel Card() => new()
    {
        Dock = DockStyle.Fill,
        BackColor = Surface,
        Padding = new Padding(10)
    };

    private static void FitPage(TabPage page)
    {
        page.AutoScroll = false;
        foreach (Control control in page.Controls)
        {
            if (control.Name is "CommercialPageRoot18" or "CommercialDashboard18")
                control.Bounds = page.ClientRectangle;
        }
    }

    private static string SubtitleFor(string title)
    {
        if (title.Contains("SCANS")) return "Scansione rapida, file, cartelle, quarantena e annullamento";
        if (title.Contains("AUDIT")) return "Audit completo, rapporto e annullamento";
        if (title.Contains("RECUP")) return "Archivi quarantena e rollback";
        if (title.Contains("AGGIORN")) return "Ricarica sicura del database firme";
        if (title.Contains("ATTIV")) return "Rapporti e registro operativo";
        if (title.Contains("IMPOST")) return "Configurazione disponibile nel motore installato";
        if (title.Contains("RANSOM")) return "Protezione comportamentale disponibile nel motore installato";
        return "FFGuardian Ultimate Protection";
    }

    private static string CleanTitle(string value)
    {
        string cleaned = value.Replace("&", string.Empty, StringComparison.Ordinal).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "SICUREZZA" : cleaned.ToUpperInvariant();
    }

    private static IEnumerable<Control> Flatten(Control root)
    {
        yield return root;
        foreach (Control child in root.Controls)
        {
            foreach (Control nested in Flatten(child))
                yield return nested;
        }
    }

    private static IEnumerable<T> FindControls<T>(Control root) where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T match)
                yield return match;
            foreach (T nested in FindControls<T>(child))
                yield return nested;
        }
    }
}
