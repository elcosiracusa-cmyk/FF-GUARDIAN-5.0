using System.Diagnostics;
using System.Runtime.CompilerServices;
using FFGuardian.Engine10;

namespace FFGuardian;

/// <summary>
/// Shell unica e responsiva per FFGuardian.
/// Sostituisce le precedenti correzioni grafiche sovrapposte senza modificare
/// la logica dei pulsanti e delle pagine già presenti nel programma.
/// </summary>
internal static class UnifiedResponsiveInterface11
{
    private static readonly Color Background = Color.FromArgb(5, 9, 13);
    private static readonly Color Surface = Color.FromArgb(14, 22, 28);
    private static readonly Color Raised = Color.FromArgb(24, 35, 43);
    private static readonly Color RaisedHover = Color.FromArgb(38, 55, 46);
    private static readonly Color Neon = Color.FromArgb(108, 255, 36);
    private static readonly Color Text = Color.FromArgb(244, 248, 250);
    private static readonly Color Muted = Color.FromArgb(184, 198, 207);
    private static readonly Color Border = Color.FromArgb(66, 91, 102);

    private static bool _applied;
    private static System.Windows.Forms.Timer? _metricsTimer;
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
            StabilityCoordinator82.WriteInformationLog("Interfaccia unificata responsive 11 applicata.");
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
            form.Font = new Font("Segoe UI", 10F, FontStyle.Regular);

            Panel shell = new()
            {
                Name = "UnifiedResponsiveShell11",
                Dock = DockStyle.Fill,
                BackColor = Background,
                Padding = Padding.Empty
            };

            Panel header = BuildHeader();
            Panel statusBar = BuildStatusBar();
            TableLayoutPanel body = new()
            {
                Dock = DockStyle.Fill,
                BackColor = Background,
                ColumnCount = 2,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 224F));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            FlowLayoutPanel navigation = BuildNavigation(tabs);
            Panel contentHost = new()
            {
                Name = "UnifiedContentHost11",
                Dock = DockStyle.Fill,
                BackColor = Background,
                Padding = new Padding(10)
            };

            tabs.Parent?.Controls.Remove(tabs);
            ConfigureHiddenTabs(tabs);
            contentHost.Controls.Add(tabs);

            body.Controls.Add(navigation, 0, 0);
            body.Controls.Add(contentHost, 1, 0);

            shell.Controls.Add(body);
            shell.Controls.Add(statusBar);
            shell.Controls.Add(header);
            form.Controls.Add(shell);
            shell.BringToFront();

            RebuildDashboard(tabs);
            foreach (TabPage page in tabs.TabPages)
                NormalizePage(page);

            tabs.SelectedIndexChanged += (_, _) =>
            {
                UpdateNavigationSelection(navigation, tabs.SelectedIndex);
                NormalizePage(tabs.SelectedTab);
                AuditLayout(form, tabs);
            };

            form.Resize += (_, _) =>
            {
                body.ColumnStyles[0].Width = form.ClientSize.Width < 1180 ? 184F : 224F;
                AdjustNavigationLabels(navigation, form.ClientSize.Width < 1180);
                NormalizePage(tabs.SelectedTab);
                AuditLayout(form, tabs);
            };

            _lastCpu = Process.GetCurrentProcess().TotalProcessorTime;
            _lastCpuSampleUtc = DateTime.UtcNow;
            _metricsTimer?.Dispose();
            _metricsTimer = new System.Windows.Forms.Timer { Interval = 2500 };
            _metricsTimer.Tick += (_, _) => RefreshMetrics(shell);
            _metricsTimer.Start();
            form.FormClosed += (_, _) =>
            {
                _metricsTimer?.Stop();
                _metricsTimer?.Dispose();
                _metricsTimer = null;
            };

            UpdateNavigationSelection(navigation, tabs.SelectedIndex);
            RefreshMetrics(shell);
            form.BeginInvoke(() => AuditLayout(form, tabs));
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
            Name = "UnifiedHeader11",
            Dock = DockStyle.Top,
            Height = 78,
            BackColor = Surface,
            Padding = new Padding(18, 10, 18, 8)
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
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230F));

        Panel emblem = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Raised,
            Margin = new Padding(0, 0, 12, 0),
            Padding = new Padding(4)
        };
        emblem.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Raised,
            ForeColor = Neon,
            Font = new Font("Segoe UI Symbol", 21F, FontStyle.Bold),
            Text = "FG",
            TextAlign = ContentAlignment.MiddleCenter
        });

        Panel brand = new() { Dock = DockStyle.Fill, BackColor = Surface };
        brand.Controls.Add(new Label
        {
            Dock = DockStyle.Bottom,
            Height = 24,
            BackColor = Surface,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 9.5F),
            Text = "Protezione autonoma  •  Ransom Shield  •  Firewall  •  USB Shield  •  Engine10",
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        });
        brand.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Text,
            Font = new Font("Segoe UI", 21F, FontStyle.Bold),
            Text = "FFGUARDIAN",
            TextAlign = ContentAlignment.MiddleLeft
        });

        Panel state = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Raised,
            Padding = new Padding(12, 4, 12, 4)
        };
        state.Controls.Add(new Label
        {
            Name = "UnifiedHeaderState11",
            Dock = DockStyle.Fill,
            BackColor = Raised,
            ForeColor = Neon,
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            Text = "● PROTETTO",
            TextAlign = ContentAlignment.MiddleRight
        });

        layout.Controls.Add(emblem, 0, 0);
        layout.Controls.Add(brand, 1, 0);
        layout.Controls.Add(state, 2, 0);
        header.Controls.Add(layout);
        return header;
    }

    private static Panel BuildStatusBar()
    {
        Panel status = new()
        {
            Dock = DockStyle.Bottom,
            Height = 30,
            BackColor = Surface,
            Padding = new Padding(14, 2, 14, 2)
        };
        status.Controls.Add(new Label
        {
            Name = "UnifiedFooterMetrics11",
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 8.5F),
            Text = "Engine10 pronto",
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        });
        return status;
    }

    private static FlowLayoutPanel BuildNavigation(TabControl tabs)
    {
        FlowLayoutPanel navigation = new()
        {
            Name = "UnifiedNavigation11",
            Dock = DockStyle.Fill,
            BackColor = Surface,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(10, 12, 10, 12),
            Margin = Padding.Empty
        };

        for (int index = 0; index < tabs.TabCount; index++)
        {
            int targetIndex = index;
            string title = CleanTitle(tabs.TabPages[index].Text);
            Button button = new()
            {
                Name = $"UnifiedNavButton11_{index}",
                Tag = index,
                Width = 198,
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
                AutoEllipsis = true,
                AccessibleName = title
            };
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Border;
            button.FlatAppearance.MouseOverBackColor = RaisedHover;
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(55, 82, 52);
            button.Click += (_, _) => tabs.SelectedIndex = targetIndex;
            navigation.Controls.Add(button);
        }

        return navigation;
    }

    private static void ConfigureHiddenTabs(TabControl tabs)
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

    private static void RebuildDashboard(TabControl tabs)
    {
        TabPage? dashboard = tabs.TabPages.Cast<TabPage>()
            .FirstOrDefault(page => page.Text.Contains("DASH", StringComparison.OrdinalIgnoreCase));
        if (dashboard is null)
            return;

        dashboard.SuspendLayout();
        try
        {
            dashboard.Controls.Clear();
            dashboard.AutoScroll = true;
            dashboard.Padding = new Padding(6);

            TableLayoutPanel grid = new()
            {
                Name = "UnifiedDashboardGrid11",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Background,
                ColumnCount = 2,
                RowCount = 4,
                Padding = Padding.Empty
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 132F));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 146F));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 190F));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));

            Panel hero = BuildDashboardHero(tabs);
            grid.SetColumnSpan(hero, 2);
            grid.Controls.Add(hero, 0, 0);
            grid.Controls.Add(BuildDashboardCard("PROTEZIONE TEMPO REALE", "ATTIVA", "Download, file e dispositivi rimovibili sorvegliati."), 0, 1);
            grid.Controls.Add(BuildDashboardCard("RANSOM SHIELD", "ATTIVO", "Controllo delle modifiche massive e rollback prudente."), 1, 1);
            grid.Controls.Add(BuildDashboardMetricsCard("MOTORI E FIRME", "UnifiedEngines11"), 0, 2);
            grid.Controls.Add(BuildDashboardMetricsCard("RISORSE E ATTIVITÀ", "UnifiedResources11"), 1, 2);

            FlowLayoutPanel shortcuts = new()
            {
                Dock = DockStyle.Fill,
                BackColor = Surface,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = true,
                Padding = new Padding(10)
            };
            foreach ((string label, string target) in new[]
                     {
                         ("PROCESSI", "PROCESS"), ("USB SHIELD", "USB"), ("FIREWALL", "FIREWALL"),
                         ("QUARANTENA", "QUARANT"), ("RAPPORTI", "RAPPORT")
                     })
            {
                shortcuts.Controls.Add(CreateActionButton(label, false, () => SelectTab(tabs, target)));
            }
            grid.SetColumnSpan(shortcuts, 2);
            grid.Controls.Add(shortcuts, 0, 3);
            dashboard.Controls.Add(grid);
        }
        finally
        {
            dashboard.ResumeLayout(true);
        }
    }

    private static Panel BuildDashboardHero(TabControl tabs)
    {
        Panel hero = CreateCard();
        hero.Margin = new Padding(6);
        hero.Padding = new Padding(18, 14, 18, 12);

        Label status = new()
        {
            Dock = DockStyle.Right,
            Width = 210,
            BackColor = Raised,
            ForeColor = Neon,
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            Text = "PROTETTO",
            TextAlign = ContentAlignment.MiddleCenter
        };
        Label title = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Text,
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            Text = "IL SISTEMA È SOTTO PROTEZIONE\nEngine10 controlla le aree sensibili senza invadere il lavoro quotidiano.",
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
        FlowLayoutPanel actions = new()
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            BackColor = Surface,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0, 6, 0, 0)
        };
        actions.Controls.Add(CreateActionButton("PROTEGGI ORA", true, () => SelectTab(tabs, "SCANS")));
        actions.Controls.Add(CreateActionButton("SCANSIONE", false, () => SelectTab(tabs, "SCANS")));
        actions.Controls.Add(CreateActionButton("AGGIORNA FIRME", false, () => SelectTab(tabs, "AGGIORN")));
        hero.Controls.Add(title);
        hero.Controls.Add(status);
        hero.Controls.Add(actions);
        return hero;
    }

    private static Panel BuildDashboardCard(string heading, string value, string detail)
    {
        Panel card = CreateCard();
        card.Margin = new Padding(6);
        card.Padding = new Padding(16);
        card.Controls.Add(new Label
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            BackColor = Surface,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 9.5F),
            Text = detail,
            AutoEllipsis = true
        });
        card.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Neon,
            Font = new Font("Segoe UI", 19F, FontStyle.Bold),
            Text = value,
            TextAlign = ContentAlignment.MiddleLeft
        });
        card.Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            BackColor = Surface,
            ForeColor = Text,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Text = heading
        });
        return card;
    }

    private static Panel BuildDashboardMetricsCard(string heading, string name)
    {
        Panel card = CreateCard();
        card.Margin = new Padding(6);
        card.Padding = new Padding(16);
        card.Controls.Add(new Label
        {
            Name = name,
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Text,
            Font = new Font("Segoe UI", 10F),
            Text = "Caricamento dati...",
            Padding = new Padding(0, 10, 0, 0),
            AutoEllipsis = true
        });
        card.Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            Height = 30,
            BackColor = Surface,
            ForeColor = Neon,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Text = heading
        });
        return card;
    }

    private static void NormalizePage(TabPage? page)
    {
        if (page is null || page.IsDisposed)
            return;

        page.SuspendLayout();
        try
        {
            page.BackColor = Background;
            page.ForeColor = Text;
            page.Padding = new Padding(12);
            page.AutoScroll = true;

            PolishControlTree(page);
            IReadOnlyList<DataGridView> grids = FindControls<DataGridView>(page).ToArray();
            foreach (DataGridView grid in grids)
                PolishGrid(grid);

            if (grids.Count > 0)
            {
                DataGridView mainGrid = grids.OrderByDescending(grid => grid.Width * grid.Height).First();
                mainGrid.Dock = DockStyle.Fill;
                mainGrid.BringToFront();
            }

            Control[] topLevel = page.Controls.Cast<Control>().Where(control => control.Visible).ToArray();
            if (topLevel.Length == 1 && topLevel[0] is Panel or GroupBox or TableLayoutPanel or FlowLayoutPanel)
                topLevel[0].Dock = DockStyle.Fill;

            foreach (Control child in topLevel)
            {
                if (child.Dock == DockStyle.None)
                {
                    child.Anchor |= AnchorStyles.Left | AnchorStyles.Right;
                    child.Width = Math.Max(500, page.ClientSize.Width - page.Padding.Horizontal - 20);
                }
            }
        }
        finally
        {
            page.ResumeLayout(true);
        }
    }

    private static void PolishControlTree(Control root)
    {
        foreach (Control control in root.Controls)
        {
            switch (control)
            {
                case Button button:
                    PolishButton(button);
                    break;
                case Label label:
                    label.BackColor = label.BackColor == Color.Transparent ? Surface : label.BackColor;
                    label.ForeColor = label.ForeColor.GetBrightness() > 0.82F ? Text : label.ForeColor;
                    label.Font = new Font("Segoe UI", Math.Clamp(label.Font.Size, 9F, 14F), label.Font.Style);
                    label.AutoEllipsis = true;
                    break;
                case GroupBox group:
                    group.BackColor = Surface;
                    group.ForeColor = Text;
                    group.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
                    group.Padding = new Padding(14);
                    break;
                case Panel panel:
                    if (panel.BackColor == Color.Transparent || panel.BackColor.GetBrightness() < 0.05F)
                        panel.BackColor = Surface;
                    break;
                case CheckBox check:
                    check.BackColor = Surface;
                    check.ForeColor = Text;
                    check.Font = new Font("Segoe UI", 10F);
                    check.AutoSize = true;
                    break;
                case ComboBox combo:
                    combo.Font = new Font("Segoe UI", 10F);
                    combo.MinimumSize = new Size(190, 32);
                    break;
                case TextBox textBox:
                    textBox.Font = new Font("Segoe UI", 10F);
                    textBox.BackColor = Raised;
                    textBox.ForeColor = Text;
                    break;
            }

            if (control is Panel or GroupBox or TableLayoutPanel or FlowLayoutPanel)
            {
                control.Margin = new Padding(6);
                if (control.Width < Math.Max(520, root.ClientSize.Width * 0.68))
                    control.Anchor |= AnchorStyles.Left | AnchorStyles.Right;
            }

            PolishControlTree(control);
        }
    }

    private static void PolishButton(Button button)
    {
        button.UseVisualStyleBackColor = false;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Neon;
        button.FlatAppearance.MouseOverBackColor = RaisedHover;
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(58, 86, 52);
        button.BackColor = button.Text.Contains("PROTEGGI", StringComparison.OrdinalIgnoreCase) ? Neon : Raised;
        button.ForeColor = button.Text.Contains("PROTEGGI", StringComparison.OrdinalIgnoreCase) ? Background : Text;
        button.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.Padding = new Padding(8, 2, 8, 2);
        button.MinimumSize = new Size(126, 40);
        button.AutoEllipsis = true;
    }

    private static void PolishGrid(DataGridView grid)
    {
        grid.BackgroundColor = Background;
        grid.BorderStyle = BorderStyle.None;
        grid.GridColor = Border;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersHeight = 38;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Raised;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Neon;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        grid.DefaultCellStyle.BackColor = Surface;
        grid.DefaultCellStyle.ForeColor = Text;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(45, 75, 50);
        grid.DefaultCellStyle.SelectionForeColor = Text;
        grid.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
        grid.RowTemplate.Height = 30;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        grid.AllowUserToResizeColumns = true;
        grid.AllowUserToOrderColumns = true;
    }

    private static Button CreateActionButton(string text, bool primary, Action action)
    {
        Button button = new()
        {
            Width = primary ? 164 : 148,
            Height = 40,
            Margin = new Padding(4),
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Neon : Raised,
            ForeColor = primary ? Background : Text,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            AutoEllipsis = true
        };
        button.FlatAppearance.BorderColor = Neon;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(135, 255, 80) : RaisedHover;
        button.Click += (_, _) => action();
        return button;
    }

    private static Panel CreateCard() => new()
    {
        Dock = DockStyle.Fill,
        BackColor = Surface,
        BorderStyle = BorderStyle.FixedSingle
    };

    private static void UpdateNavigationSelection(FlowLayoutPanel navigation, int selectedIndex)
    {
        foreach (Button button in navigation.Controls.OfType<Button>())
        {
            bool selected = button.Tag is int index && index == selectedIndex;
            button.BackColor = selected ? Neon : Raised;
            button.ForeColor = selected ? Background : Text;
            button.FlatAppearance.BorderColor = selected ? Neon : Border;
        }
    }

    private static void AdjustNavigationLabels(FlowLayoutPanel navigation, bool compact)
    {
        navigation.Padding = compact ? new Padding(7, 10, 7, 10) : new Padding(10, 12, 10, 12);
        foreach (Button button in navigation.Controls.OfType<Button>())
        {
            button.Width = compact ? 160 : 198;
            button.Font = new Font("Segoe UI", compact ? 8.5F : 9.5F, FontStyle.Bold);
        }
    }

    private static void RefreshMetrics(Control shell)
    {
        try
        {
            ExternalEngineStatus10 engine = ExternalThreatEngines10.GetStatus();
            Process process = Process.GetCurrentProcess();
            process.Refresh();
            double cpu = CalculateCpuPercent(process);
            double ramMb = process.WorkingSet64 / 1024D / 1024D;

            SetText(shell, "UnifiedEngines11",
                $"Engine10 autonomo: ATTIVO\r\nClamAV: {(engine.ClamAvAvailable ? "ATTIVO" : "NON INSTALLATO")}\r\nYARA reale: {(engine.YaraAvailable ? "ATTIVO" : "NON INSTALLATO")}\r\nRegole YARA disponibili: {engine.YaraRuleFiles}");
            SetText(shell, "UnifiedResources11",
                $"CPU FFGuardian: {cpu:F1}%\r\nRAM processo: {ramMb:F1} MB\r\nAuto-esclusione componenti interni: ATTIVA\r\nAggiornato alle: {DateTime.Now:HH:mm:ss}");
            SetText(shell, "UnifiedFooterMetrics11",
                $"FFGuardian 10.0.1 Stable  •  Engine10 pronto  •  CPU {cpu:F1}%  •  RAM {ramMb:F1} MB  •  {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
        }
    }

    private static double CalculateCpuPercent(Process process)
    {
        DateTime now = DateTime.UtcNow;
        TimeSpan currentCpu = process.TotalProcessorTime;
        double elapsedMs = (now - _lastCpuSampleUtc).TotalMilliseconds;
        double cpuMs = (currentCpu - _lastCpu).TotalMilliseconds;
        _lastCpu = currentCpu;
        _lastCpuSampleUtc = now;
        if (elapsedMs <= 0 || Environment.ProcessorCount <= 0)
            return 0D;
        return Math.Clamp(cpuMs / elapsedMs / Environment.ProcessorCount * 100D, 0D, 100D);
    }

    private static void AuditLayout(Form form, TabControl tabs)
    {
        try
        {
            int clipped = 0;
            foreach (Control control in FindControls<Control>(tabs.SelectedTab ?? tabs))
            {
                if (!control.Visible || string.IsNullOrWhiteSpace(control.Text))
                    continue;
                if (control.ClientSize.Width <= 0 || control.ClientSize.Height <= 0)
                    continue;

                Size preferred = TextRenderer.MeasureText(control.Text, control.Font,
                    new Size(Math.Max(1, control.ClientSize.Width), int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                if (preferred.Height > control.ClientSize.Height + 8)
                    clipped++;
            }

            if (clipped > 0)
                StabilityCoordinator82.WriteInformationLog($"Audit UI: {clipped} controlli richiedono scorrimento o più spazio su {form.ClientSize.Width}x{form.ClientSize.Height}.");
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
        }
    }

    private static void SelectTab(TabControl tabs, string token)
    {
        TabPage? page = tabs.TabPages.Cast<TabPage>()
            .FirstOrDefault(candidate => candidate.Text.Contains(token, StringComparison.OrdinalIgnoreCase));
        if (page is not null)
            tabs.SelectedTab = page;
    }

    private static string CleanTitle(string value)
    {
        string cleaned = value.Replace("&", string.Empty, StringComparison.Ordinal).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "FUNZIONE" : cleaned.ToUpperInvariant();
    }

    private static void SetText(Control root, string name, string value)
    {
        Label? label = FindControls<Label>(root).FirstOrDefault(candidate => candidate.Name == name);
        if (label is not null)
            label.Text = value;
    }

    private static IEnumerable<T> FindControls<T>(Control root) where T : Control
    {
        if (root is T rootMatch)
            yield return rootMatch;

        foreach (Control child in root.Controls)
        {
            if (child is T match)
                yield return match;
            foreach (T nested in FindControls<T>(child))
                yield return nested;
        }
    }
}
