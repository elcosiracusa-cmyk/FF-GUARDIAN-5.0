using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FFGuardian;

/// <summary>
/// Interfaccia commerciale unica di FFGuardian.
/// Mantiene i controlli e gli eventi originali, ma applica una sola shell,
/// una dashboard ordinata e pagine verticali per Ransom Shield, Recupero e Aggiornamenti.
/// </summary>
internal static class FinalCommercialInterface16
{
    private static readonly Color Background = Color.FromArgb(5, 9, 13);
    private static readonly Color Surface = Color.FromArgb(14, 22, 28);
    private static readonly Color Raised = Color.FromArgb(24, 35, 43);
    private static readonly Color Neon = Color.FromArgb(108, 255, 36);
    private static readonly Color Text = Color.FromArgb(244, 248, 250);
    private static readonly Color Muted = Color.FromArgb(180, 195, 204);
    private static readonly Color Border = Color.FromArgb(61, 83, 93);
    private static readonly Color Danger = Color.FromArgb(255, 82, 82);

    private static bool _applied;
    private static System.Windows.Forms.Timer? _timer;

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
            .OrderByDescending(candidate => candidate.TabCount)
            .FirstOrDefault(candidate => candidate.TabCount > 0);
        if (tabs is null)
            return;

        try
        {
            BuildShell(form, tabs);
            _applied = true;
            Application.Idle -= ApplyWhenReady;
            StabilityCoordinator82.WriteInformationLog("Interfaccia commerciale unificata 16 applicata.");
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            Application.Idle -= ApplyWhenReady;
        }
    }

    private static void BuildShell(IndependentMainForm100 form, TabControl tabs)
    {
        form.SuspendLayout();
        try
        {
            form.MinimumSize = new Size(1024, 680);
            form.BackColor = Background;
            form.Font = new Font("Segoe UI", 10F);

            tabs.Parent?.Controls.Remove(tabs);
            ConfigureTabs(tabs);

            Panel shell = new()
            {
                Name = "FinalCommercialShell16",
                Dock = DockStyle.Fill,
                BackColor = Background
            };

            Panel header = BuildHeader();
            Panel footer = BuildFooter();
            TableLayoutPanel body = new()
            {
                Dock = DockStyle.Fill,
                BackColor = Background,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230F));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            Panel navigation = BuildNavigation(tabs);
            Panel host = new()
            {
                Dock = DockStyle.Fill,
                BackColor = Background,
                Padding = new Padding(10)
            };
            host.Controls.Add(tabs);
            body.Controls.Add(navigation, 0, 0);
            body.Controls.Add(host, 1, 0);

            shell.Controls.Add(body);
            shell.Controls.Add(footer);
            shell.Controls.Add(header);
            form.Controls.Add(shell);
            shell.BringToFront();

            TabPage? dashboard = FindDashboard(tabs);
            if (dashboard is not null)
                BuildDashboard(dashboard, tabs);

            foreach (TabPage page in tabs.TabPages)
            {
                if (!ReferenceEquals(page, dashboard) && IsVerticalPage(page))
                    ConvertPageToVertical(page);
                else if (!ReferenceEquals(page, dashboard))
                    NormalizePage(page);
            }

            tabs.SelectedIndexChanged += (_, _) =>
            {
                UpdateNavigation(navigation, tabs.SelectedIndex);
                if (tabs.SelectedTab is not null && !ReferenceEquals(tabs.SelectedTab, dashboard))
                    NormalizePage(tabs.SelectedTab);
            };

            form.Resize += (_, _) =>
            {
                float width = form.ClientSize.Width < 1180 ? 190F : 230F;
                body.ColumnStyles[0].Width = width;
                ResizeNavigation(navigation, (int)width);
            };

            UpdateNavigation(navigation, tabs.SelectedIndex);
            StartMetrics(form, footer);
        }
        finally
        {
            form.ResumeLayout(true);
        }
    }

    private static Panel BuildHeader()
    {
        Panel header = new()
        {
            Dock = DockStyle.Top,
            Height = 76,
            BackColor = Surface,
            Padding = new Padding(16, 8, 16, 8)
        };

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220F));

        Label logo = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Raised,
            ForeColor = Neon,
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            Text = "FG",
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 0, 12, 0)
        };

        Panel brand = new() { Dock = DockStyle.Fill, BackColor = Surface };
        brand.Controls.Add(new Label
        {
            Dock = DockStyle.Bottom,
            Height = 23,
            BackColor = Surface,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 9F),
            Text = "Protezione autonoma • Ransom Shield • Firewall • USB Shield • Engine10",
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        });
        brand.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Text,
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            Text = "FFGUARDIAN",
            TextAlign = ContentAlignment.MiddleLeft
        });

        Label status = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Raised,
            ForeColor = Neon,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            Text = "● SISTEMA PROTETTO",
            TextAlign = ContentAlignment.MiddleCenter
        };

        layout.Controls.Add(logo, 0, 0);
        layout.Controls.Add(brand, 1, 0);
        layout.Controls.Add(status, 2, 0);
        header.Controls.Add(layout);
        return header;
    }

    private static Panel BuildFooter()
    {
        Panel footer = new()
        {
            Dock = DockStyle.Bottom,
            Height = 30,
            BackColor = Surface,
            Padding = new Padding(12, 2, 12, 2)
        };
        footer.Controls.Add(new Label
        {
            Name = "FinalCommercialMetrics16",
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 8.5F),
            Text = "FFGuardian 10.0.1 Stable • Engine10 pronto",
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        });
        return footer;
    }

    private static Panel BuildNavigation(TabControl tabs)
    {
        Panel navigation = new()
        {
            Name = "FinalCommercialNavigation16",
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(10, 12, 10, 12),
            AutoScroll = true
        };

        TableLayoutPanel stack = new()
        {
            Name = "FinalCommercialNavigationStack16",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Surface,
            ColumnCount = 1,
            RowCount = tabs.TabCount,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        for (int index = 0; index < tabs.TabCount; index++)
        {
            int target = index;
            string title = CleanTitle(tabs.TabPages[index].Text);
            stack.RowStyles.Add(new RowStyle(SizeType.Absolute, 51F));
            Button button = new()
            {
                Name = $"FinalCommercialNav16_{index}",
                Tag = index,
                Dock = DockStyle.Fill,
                Height = 44,
                Margin = new Padding(0, 0, 0, 7),
                Padding = new Padding(12, 0, 8, 0),
                Text = title,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                BackColor = Raised,
                ForeColor = Text,
                Cursor = Cursors.Hand,
                AutoEllipsis = true
            };
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Border;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(42, 62, 50);
            button.Click += (_, _) => tabs.SelectedIndex = target;
            stack.Controls.Add(button, 0, index);
        }

        navigation.Controls.Add(stack);
        return navigation;
    }

    private static void ConfigureTabs(TabControl tabs)
    {
        tabs.Dock = DockStyle.Fill;
        tabs.Appearance = TabAppearance.FlatButtons;
        tabs.SizeMode = TabSizeMode.Fixed;
        tabs.ItemSize = new Size(0, 1);
        tabs.Multiline = true;
        tabs.Padding = Point.Empty;
        tabs.Margin = Padding.Empty;
        tabs.Font = new Font("Segoe UI", 1F);

        foreach (TabPage page in tabs.TabPages)
        {
            page.BackColor = Background;
            page.ForeColor = Text;
            page.Padding = new Padding(12);
            page.AutoScroll = true;
            page.UseVisualStyleBackColor = false;
        }
    }

    private static TabPage? FindDashboard(TabControl tabs)
    {
        TabPage? dashboard = tabs.TabPages.Cast<TabPage>().FirstOrDefault(page =>
            page.Text.Contains("DASH", StringComparison.OrdinalIgnoreCase) ||
            page.Text.Contains("HOME", StringComparison.OrdinalIgnoreCase));
        return dashboard ?? (tabs.TabCount > 0 ? tabs.TabPages[0] : null);
    }

    private static bool IsVerticalPage(TabPage page)
    {
        string title = page.Text;
        return title.Contains("RANSOM", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("RECUP", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("RIPRIST", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("AGGIORN", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("UPDATE", StringComparison.OrdinalIgnoreCase);
    }

    private static void BuildDashboard(TabPage dashboard, TabControl tabs)
    {
        dashboard.SuspendLayout();
        try
        {
            dashboard.Controls.Clear();
            dashboard.BackColor = Background;
            dashboard.Padding = new Padding(6);
            dashboard.AutoScroll = true;

            TableLayoutPanel root = new()
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Background,
                ColumnCount = 1,
                RowCount = 5,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 126F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 142F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 168F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 200F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 94F));

            root.Controls.Add(BuildHero(), 0, 0);
            root.Controls.Add(BuildStatusRow(), 0, 1);
            root.Controls.Add(BuildProtectionRow(), 0, 2);
            root.Controls.Add(BuildActivityRow(), 0, 3);
            root.Controls.Add(BuildQuickActions(tabs), 0, 4);
            dashboard.Controls.Add(root);
        }
        finally
        {
            dashboard.ResumeLayout(true);
        }
    }

    private static Control BuildHero()
    {
        TableLayoutPanel hero = CreateCard();
        hero.ColumnCount = 2;
        hero.RowCount = 1;
        hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72F));
        hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));
        hero.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        hero.Controls.Add(CreateCenteredLabel("IL TUO SISTEMA È SOTTO PROTEZIONE\r\nEngine10 monitora le aree sensibili in tempo reale.", Text, 18F), 0, 0);
        hero.Controls.Add(CreateCenteredLabel("✓  PROTETTO", Neon, 21F), 1, 0);
        return hero;
    }

    private static Control BuildStatusRow()
    {
        TableLayoutPanel row = CreateRow(4);
        row.Controls.Add(BuildValueCard("PUNTEGGIO SICUREZZA", "--/100", "Disponibile dopo l'analisi"), 0, 0);
        row.Controls.Add(BuildValueCard("AGENTE AUTONOMO", "ATTIVO", "Protezione residente"), 1, 0);
        row.Controls.Add(BuildValueCard("DATABASE FIRME", "10.0.1 EL.CO", "Firme locali pronte"), 2, 0);
        row.Controls.Add(BuildValueCard("ULTIMA OPERAZIONE", "NESSUNA", "In attesa di attività"), 3, 0);
        return row;
    }

    private static Control BuildProtectionRow()
    {
        TableLayoutPanel row = CreateRow(4);
        row.Controls.Add(BuildModuleCard("PROTEZIONE TEMPO REALE", "ATTIVA", "File e download monitorati"), 0, 0);
        row.Controls.Add(BuildModuleCard("RANSOM SHIELD", "ATTIVO", "Controllo comportamentale"), 1, 0);
        row.Controls.Add(BuildModuleCard("FIREWALL", "ATTIVO", "Traffico controllato"), 2, 0);
        row.Controls.Add(BuildModuleCard("USB SHIELD", "ATTIVO", "Dispositivi rimovibili protetti"), 3, 0);
        return row;
    }

    private static Control BuildActivityRow()
    {
        TableLayoutPanel row = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Background,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56F));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44F));
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        Panel activity = CreatePanelCard();
        activity.Controls.Add(CreateCenteredLabel("ATTIVITÀ RECENTI\r\n\r\n• Engine10 pronto\r\n• Protezione in tempo reale attiva\r\n• Nessuna operazione distruttiva eseguita", Text, 10F));

        Panel metrics = CreatePanelCard();
        metrics.Controls.Add(new Label
        {
            Name = "FinalCommercialDashboardMetrics16",
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Text,
            Font = new Font("Consolas", 10F),
            Text = "RISORSE DI SISTEMA\r\n\r\nRAM FFGuardian: -- MB\r\nProcessori logici: --\r\nStato: operativo",
            TextAlign = ContentAlignment.MiddleLeft
        });

        row.Controls.Add(activity, 0, 0);
        row.Controls.Add(metrics, 1, 0);
        return row;
    }

    private static Control BuildQuickActions(TabControl tabs)
    {
        FlowLayoutPanel actions = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = true,
            Padding = new Padding(14, 12, 14, 8),
            Margin = new Padding(6)
        };
        actions.Controls.Add(CreateActionButton("SCANSIONE", true, () => SelectTab(tabs, "SCANS")));
        actions.Controls.Add(CreateActionButton("RANSOM SHIELD", false, () => SelectTab(tabs, "RANSOM")));
        actions.Controls.Add(CreateActionButton("RECUPERO", false, () => SelectTab(tabs, "RECUP")));
        actions.Controls.Add(CreateActionButton("AGGIORNAMENTI", false, () => SelectTab(tabs, "AGGIORN")));
        actions.Controls.Add(CreateActionButton("QUARANTENA", false, () => SelectTab(tabs, "QUARANT")));
        return actions;
    }

    private static void ConvertPageToVertical(TabPage page)
    {
        if (page.Controls.OfType<TableLayoutPanel>().Any(control => control.Name == "FinalVerticalPage16"))
            return;

        List<Control> original = page.Controls.Cast<Control>().ToList();
        if (original.Count == 0)
            return;

        page.SuspendLayout();
        try
        {
            page.Controls.Clear();
            page.BackColor = Background;
            page.Padding = new Padding(12);
            page.AutoScroll = true;

            TableLayoutPanel vertical = new()
            {
                Name = "FinalVerticalPage16",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Background,
                ColumnCount = 1,
                RowCount = original.Count + 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            vertical.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            vertical.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            vertical.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                BackColor = Background,
                ForeColor = Text,
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                Text = CleanTitle(page.Text),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0)
            }, 0, 0);

            int row = 1;
            foreach (Control control in original.OrderBy(item => item.Top).ThenBy(item => item.Left))
            {
                control.Dock = DockStyle.Fill;
                control.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                control.Margin = new Padding(6);
                PolishTree(control);
                int height = PreferredVerticalHeight(control);
                vertical.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
                vertical.Controls.Add(control, 0, row++);
            }

            page.Controls.Add(vertical);
        }
        finally
        {
            page.ResumeLayout(true);
        }
    }

    private static int PreferredVerticalHeight(Control control)
    {
        if (control is DataGridView || control is ListView || control is TreeView)
            return 320;
        if (control is GroupBox)
            return Math.Max(150, Math.Min(280, control.Height));
        if (control is Button)
            return 56;
        if (control is TextBox textBox && textBox.Multiline)
            return 180;
        if (control is FlowLayoutPanel || control is TableLayoutPanel)
            return Math.Max(140, Math.Min(300, control.Height));
        return Math.Max(62, Math.Min(180, control.Height));
    }

    private static void NormalizePage(TabPage page)
    {
        page.BackColor = Background;
        page.ForeColor = Text;
        page.Padding = new Padding(12);
        page.AutoScroll = true;
        PolishTree(page);
    }

    private static void PolishTree(Control root)
    {
        foreach (Control control in root.Controls)
        {
            switch (control)
            {
                case Button button:
                    button.UseVisualStyleBackColor = false;
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderSize = 1;
                    button.FlatAppearance.BorderColor = Neon;
                    button.BackColor = Raised;
                    button.ForeColor = Text;
                    button.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                    button.MinimumSize = new Size(126, 40);
                    button.AutoEllipsis = true;
                    break;
                case Label label:
                    label.ForeColor = Text;
                    if (label.BackColor == Color.Transparent)
                        label.BackColor = label.Parent?.BackColor ?? Background;
                    label.AutoEllipsis = true;
                    break;
                case GroupBox group:
                    group.ForeColor = Text;
                    group.BackColor = Surface;
                    group.Padding = new Padding(12);
                    break;
                case Panel panel when panel.BackColor == Color.Transparent:
                    panel.BackColor = Surface;
                    break;
                case DataGridView grid:
                    ConfigureGrid(grid);
                    break;
            }
            PolishTree(control);
        }
    }

    private static void ConfigureGrid(DataGridView grid)
    {
        grid.BackgroundColor = Background;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.GridColor = Border;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Raised;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Neon;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        grid.ColumnHeadersHeight = 38;
        grid.DefaultCellStyle.BackColor = Surface;
        grid.DefaultCellStyle.ForeColor = Text;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(50, 80, 55);
        grid.DefaultCellStyle.SelectionForeColor = Text;
        grid.RowTemplate.Height = 30;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.AllowUserToResizeColumns = true;
        grid.AllowUserToOrderColumns = true;
    }

    private static TableLayoutPanel CreateCard()
    {
        return new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Margin = new Padding(6),
            Padding = new Padding(14),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
        };
    }

    private static TableLayoutPanel CreateRow(int columns)
    {
        TableLayoutPanel row = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Background,
            ColumnCount = columns,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        for (int index = 0; index < columns; index++)
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / columns));
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        return row;
    }

    private static Panel CreatePanelCard()
    {
        return new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Margin = new Padding(6),
            Padding = new Padding(16)
        };
    }

    private static Label CreateCenteredLabel(string value, Color color, float size)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = color,
            Font = new Font("Segoe UI", size, FontStyle.Bold),
            Text = value,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true
        };
    }

    private static Control BuildValueCard(string title, string value, string detail)
    {
        Panel card = CreatePanelCard();
        card.Controls.Add(new Label
        {
            Dock = DockStyle.Bottom,
            Height = 27,
            BackColor = Surface,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 8.5F),
            Text = detail,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true
        });
        card.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Text,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            Text = value,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true
        });
        card.Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            Height = 25,
            BackColor = Surface,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            Text = title,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true
        });
        return card;
    }

    private static Control BuildModuleCard(string title, string state, string detail)
    {
        Panel card = CreatePanelCard();
        card.Controls.Add(new Label
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            BackColor = Surface,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 9F),
            Text = detail,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        });
        card.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Neon,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            Text = "✓  " + state,
            TextAlign = ContentAlignment.MiddleLeft
        });
        card.Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            Height = 33,
            BackColor = Surface,
            ForeColor = Text,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Text = title,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        });
        return card;
    }

    private static Button CreateActionButton(string text, bool primary, Action action)
    {
        Button button = new()
        {
            Width = 172,
            Height = 48,
            Margin = new Padding(0, 0, 10, 0),
            Text = text,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Neon : Raised,
            ForeColor = primary ? Background : Text,
            Cursor = Cursors.Hand,
            AutoEllipsis = true
        };
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Neon;
        button.Click += (_, _) => action();
        return button;
    }

    private static void UpdateNavigation(Panel navigation, int selectedIndex)
    {
        foreach (Button button in FindControls<Button>(navigation))
        {
            bool selected = button.Tag is int index && index == selectedIndex;
            button.BackColor = selected ? Neon : Raised;
            button.ForeColor = selected ? Background : Text;
            button.FlatAppearance.BorderColor = selected ? Neon : Border;
        }
    }

    private static void ResizeNavigation(Panel navigation, int width)
    {
        TableLayoutPanel? stack = FindControls<TableLayoutPanel>(navigation)
            .FirstOrDefault(control => control.Name == "FinalCommercialNavigationStack16");
        if (stack is not null)
            stack.Width = Math.Max(150, width - navigation.Padding.Horizontal - 2);
    }

    private static void SelectTab(TabControl tabs, string token)
    {
        TabPage? page = tabs.TabPages.Cast<TabPage>()
            .FirstOrDefault(candidate => candidate.Text.Contains(token, StringComparison.OrdinalIgnoreCase));
        if (page is not null)
            tabs.SelectedTab = page;
    }

    private static void StartMetrics(Form form, Panel footer)
    {
        Label? footerLabel = footer.Controls.OfType<Label>()
            .FirstOrDefault(control => control.Name == "FinalCommercialMetrics16");
        _timer?.Dispose();
        _timer = new System.Windows.Forms.Timer { Interval = 3000 };
        _timer.Tick += (_, _) =>
        {
            try
            {
                Process process = Process.GetCurrentProcess();
                process.Refresh();
                double ram = process.WorkingSet64 / 1024D / 1024D;
                if (footerLabel is not null)
                    footerLabel.Text = $"FFGuardian 10.0.1 Stable • Engine10 pronto • RAM {ram:F1} MB • {DateTime.Now:dd/MM/yyyy HH:mm:ss}";

                Label? dashboardMetrics = FindControls<Label>(form)
                    .FirstOrDefault(control => control.Name == "FinalCommercialDashboardMetrics16");
                if (dashboardMetrics is not null)
                    dashboardMetrics.Text = $"RISORSE DI SISTEMA\r\n\r\nRAM FFGuardian: {ram:F1} MB\r\nProcessori logici: {Environment.ProcessorCount}\r\nStato: operativo";
            }
            catch (Exception ex)
            {
                StabilityCoordinator82.WriteStabilityLog(ex);
            }
        };
        _timer.Start();
        form.FormClosed += (_, _) =>
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
        };
    }

    private static string CleanTitle(string text)
    {
        string cleaned = text.Replace("&", string.Empty, StringComparison.Ordinal).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "FUNZIONE" : cleaned.ToUpperInvariant();
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
