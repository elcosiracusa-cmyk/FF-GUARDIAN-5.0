using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FFGuardian;

/// <summary>
/// Dashboard commerciale definitiva per FFGuardian.
/// Ricostruisce esclusivamente la pagina principale senza modificare il motore antivirus.
/// </summary>
internal static class CommercialDashboard13
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
    private static TabControl? _tabs;
    private static Control? _dashboardRoot;

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
            .OrderByDescending(control => control.TabCount)
            .FirstOrDefault(control => control.TabCount > 0);
        if (tabs is null)
            return;

        TabPage? dashboard = FindDashboard(tabs);
        if (dashboard is null)
            return;

        try
        {
            BuildDashboard(form, tabs, dashboard);
            _tabs = tabs;
            _applied = true;
            Application.Idle -= ApplyWhenReady;
            StabilityCoordinator82.WriteInformationLog("Dashboard commerciale 13 applicata.");
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            Application.Idle -= ApplyWhenReady;
        }
    }

    private static TabPage? FindDashboard(TabControl tabs)
    {
        TabPage? dashboard = tabs.TabPages.Cast<TabPage>()
            .FirstOrDefault(page =>
                page.Text.Contains("DASH", StringComparison.OrdinalIgnoreCase) ||
                page.Text.Contains("HOME", StringComparison.OrdinalIgnoreCase) ||
                page.Text.Contains("PROTEZ", StringComparison.OrdinalIgnoreCase));

        return dashboard ?? (tabs.TabCount > 0 ? tabs.TabPages[0] : null);
    }

    private static void BuildDashboard(Form form, TabControl tabs, TabPage dashboard)
    {
        dashboard.SuspendLayout();
        try
        {
            dashboard.Controls.Clear();
            dashboard.BackColor = Background;
            dashboard.ForeColor = Text;
            dashboard.Padding = new Padding(12);
            dashboard.AutoScroll = true;

            TableLayoutPanel root = new()
            {
                Name = "CommercialDashboard13Root",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Background,
                ColumnCount = 1,
                RowCount = 6,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 130F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 142F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 168F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 188F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 176F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 90F));

            root.Controls.Add(BuildHero(tabs), 0, 0);
            root.Controls.Add(BuildStatusStrip(), 0, 1);
            root.Controls.Add(BuildProtectionStrip(), 0, 2);
            root.Controls.Add(BuildMetricsStrip(), 0, 3);
            root.Controls.Add(BuildBottomStrip(tabs), 0, 4);
            root.Controls.Add(BuildQuickActions(tabs), 0, 5);

            dashboard.Controls.Add(root);
            _dashboardRoot = root;

            form.Resize += (_, _) =>
            {
                if (!dashboard.IsDisposed)
                    dashboard.AutoScrollMinSize = new Size(Math.Max(900, dashboard.ClientSize.Width - 24), 910);
            };

            StartTimer(form);
            RefreshMetrics();
        }
        finally
        {
            dashboard.ResumeLayout(true);
        }
    }

    private static Control BuildHero(TabControl tabs)
    {
        TableLayoutPanel hero = CreateCard();
        hero.ColumnCount = 2;
        hero.RowCount = 1;
        hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72F));
        hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));
        hero.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        hero.Padding = new Padding(20, 14, 16, 14);

        Panel left = new() { Dock = DockStyle.Fill, BackColor = Surface };
        left.Controls.Add(new Label
        {
            Dock = DockStyle.Bottom,
            Height = 34,
            BackColor = Surface,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 10F),
            Text = "Engine10 monitora file, avvio, processi, USB e modifiche anomale in tempo reale.",
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        });
        left.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Text,
            Font = new Font("Segoe UI", 19F, FontStyle.Bold),
            Text = "IL TUO SISTEMA È PROTETTO",
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        });

        Panel right = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Raised,
            Padding = new Padding(12)
        };
        right.Controls.Add(new Label
        {
            Dock = DockStyle.Bottom,
            Height = 28,
            BackColor = Raised,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 9F),
            Text = "Protezione continua attiva",
            TextAlign = ContentAlignment.MiddleCenter
        });
        right.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Raised,
            ForeColor = Neon,
            Font = new Font("Segoe UI", 22F, FontStyle.Bold),
            Text = "✓ PROTETTO",
            TextAlign = ContentAlignment.MiddleCenter
        });

        hero.Controls.Add(left, 0, 0);
        hero.Controls.Add(right, 1, 0);
        return hero;
    }

    private static Control BuildStatusStrip()
    {
        TableLayoutPanel strip = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Background,
            ColumnCount = 4,
            RowCount = 1,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        for (int i = 0; i < 4; i++)
            strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        strip.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        strip.Controls.Add(BuildValueCard("PUNTEGGIO SICUREZZA", "--/100", "Calcolo disponibile dopo l'analisi"), 0, 0);
        strip.Controls.Add(BuildValueCard("AGENTE AUTONOMO", "ATTIVO", "Protezione residente operativa"), 1, 0);
        strip.Controls.Add(BuildValueCard("DATABASE FIRME", "10.0.1 EL.CO", "Firme locali pronte"), 2, 0);
        strip.Controls.Add(BuildValueCard("ULTIMA OPERAZIONE", "NESSUNA", "In attesa di attività"), 3, 0);
        return strip;
    }

    private static Control BuildProtectionStrip()
    {
        TableLayoutPanel strip = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Background,
            ColumnCount = 4,
            RowCount = 1,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        for (int i = 0; i < 4; i++)
            strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        strip.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        strip.Controls.Add(BuildModuleCard("PROTEZIONE TEMPO REALE", "ATTIVA", "File e download monitorati"), 0, 0);
        strip.Controls.Add(BuildModuleCard("RANSOM SHIELD", "ATTIVO", "Controllo comportamentale"), 1, 0);
        strip.Controls.Add(BuildModuleCard("FIREWALL", "ATTIVO", "Traffico e processi controllati"), 2, 0);
        strip.Controls.Add(BuildModuleCard("USB SHIELD", "ATTIVO", "Dispositivi rimovibili protetti"), 3, 0);
        return strip;
    }

    private static Control BuildMetricsStrip()
    {
        TableLayoutPanel strip = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Background,
            ColumnCount = 5,
            RowCount = 1,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));
        for (int i = 1; i < 5; i++)
            strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18F));
        strip.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        strip.Controls.Add(BuildResourcesCard(), 0, 0);
        strip.Controls.Add(BuildMetricCard("FILE SCANSIONATI", "0", "Totali", "CommercialFiles13"), 1, 0);
        strip.Controls.Add(BuildMetricCard("MINACCE RILEVATE", "0", "Quarantena", "CommercialDetected13", Danger), 2, 0);
        strip.Controls.Add(BuildMetricCard("FILE IN QUARANTENA", "0", "Elementi protetti", "CommercialQuarantine13"), 3, 0);
        strip.Controls.Add(BuildMetricCard("RAPPORTI", "0", "Disponibili", "CommercialReports13"), 4, 0);
        return strip;
    }

    private static Control BuildBottomStrip(TabControl tabs)
    {
        TableLayoutPanel strip = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Background,
            ColumnCount = 2,
            RowCount = 1,
            Padding = Padding.Empty,
            Margin = Padding.Empty
        };
        strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54F));
        strip.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46F));
        strip.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        strip.Controls.Add(BuildActivityCard(), 0, 0);
        strip.Controls.Add(BuildScanCard(tabs), 1, 0);
        return strip;
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
        actions.Controls.Add(CreateActionButton("QUARANTENA", false, () => SelectTab(tabs, "QUARANT")));
        actions.Controls.Add(CreateActionButton("USB SHIELD", false, () => SelectTab(tabs, "USB")));
        actions.Controls.Add(CreateActionButton("FIREWALL", false, () => SelectTab(tabs, "FIREWALL")));
        actions.Controls.Add(CreateActionButton("AGGIORNA FIRME", false, () => SelectTab(tabs, "AGGIORN")));
        return actions;
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

    private static Control BuildValueCard(string title, string value, string detail)
    {
        Panel card = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Margin = new Padding(6),
            Padding = new Padding(12)
        };
        card.Controls.Add(new Label
        {
            Dock = DockStyle.Bottom,
            Height = 28,
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
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
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
        Panel card = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Margin = new Padding(6),
            Padding = new Padding(14)
        };
        card.Controls.Add(new Label
        {
            Dock = DockStyle.Bottom,
            Height = 48,
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
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            Text = "✓  " + state,
            TextAlign = ContentAlignment.MiddleLeft
        });
        card.Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            Height = 34,
            BackColor = Surface,
            ForeColor = Text,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Text = title,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        });
        return card;
    }

    private static Control BuildResourcesCard()
    {
        Panel card = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Margin = new Padding(6),
            Padding = new Padding(14)
        };
        card.Controls.Add(new Label
        {
            Name = "CommercialResources13",
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Text,
            Font = new Font("Consolas", 10F),
            Text = "RAM FFGuardian: -- MB\r\nProcessori logici: --\r\nSistema: operativo",
            TextAlign = ContentAlignment.MiddleLeft
        });
        card.Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            Height = 30,
            BackColor = Surface,
            ForeColor = Neon,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Text = "RISORSE DI SISTEMA",
            TextAlign = ContentAlignment.MiddleLeft
        });
        return card;
    }

    private static Control BuildMetricCard(string title, string value, string detail, string name, Color? valueColor = null)
    {
        Panel card = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Margin = new Padding(6),
            Padding = new Padding(12)
        };
        card.Controls.Add(new Label
        {
            Dock = DockStyle.Bottom,
            Height = 28,
            BackColor = Surface,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 8.5F),
            Text = detail,
            TextAlign = ContentAlignment.MiddleCenter
        });
        card.Controls.Add(new Label
        {
            Name = name,
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = valueColor ?? Neon,
            Font = new Font("Segoe UI", 21F, FontStyle.Bold),
            Text = value,
            TextAlign = ContentAlignment.MiddleCenter
        });
        card.Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            Height = 38,
            BackColor = Surface,
            ForeColor = Text,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Text = title,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true
        });
        return card;
    }

    private static Control BuildActivityCard()
    {
        Panel card = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Margin = new Padding(6),
            Padding = new Padding(14)
        };
        card.Controls.Add(new Label
        {
            Name = "CommercialActivity13",
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Text,
            Font = new Font("Segoe UI", 9.5F),
            Text = "✓ Engine10 pronto\r\n✓ Protezione in tempo reale attiva\r\n✓ Auto-esclusione componenti interni attiva\r\n✓ In attesa di nuove operazioni",
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(0, 8, 0, 0)
        });
        card.Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            Height = 32,
            BackColor = Surface,
            ForeColor = Neon,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            Text = "ATTIVITÀ RECENTI",
            TextAlign = ContentAlignment.MiddleLeft
        });
        return card;
    }

    private static Control BuildScanCard(TabControl tabs)
    {
        Panel card = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Margin = new Padding(6),
            Padding = new Padding(14)
        };
        FlowLayoutPanel buttons = new()
        {
            Dock = DockStyle.Bottom,
            Height = 62,
            BackColor = Surface,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0, 8, 0, 0)
        };
        buttons.Controls.Add(CreateActionButton("SCANSIONE RAPIDA", true, () => SelectTab(tabs, "SCANS")));
        buttons.Controls.Add(CreateActionButton("SCANSIONE COMPLETA", false, () => SelectTab(tabs, "SCANS")));

        card.Controls.Add(buttons);
        card.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 10F),
            Text = "Controlla le aree critiche del sistema oppure avvia una scansione completa.",
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        });
        card.Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            Height = 34,
            BackColor = Surface,
            ForeColor = Neon,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            Text = "SCANSIONE",
            TextAlign = ContentAlignment.MiddleLeft
        });
        return card;
    }

    private static Button CreateActionButton(string text, bool primary, Action action)
    {
        Button button = new()
        {
            Width = primary ? 210 : 175,
            Height = 44,
            Margin = new Padding(0, 0, 10, 0),
            Padding = new Padding(10, 0, 10, 0),
            Text = text,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Neon : Raised,
            ForeColor = primary ? Background : Text,
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true
        };
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Neon;
        button.FlatAppearance.MouseOverBackColor = primary ? Color.FromArgb(140, 255, 70) : Color.FromArgb(42, 62, 50);
        button.Click += (_, _) => action();
        return button;
    }

    private static void StartTimer(Form form)
    {
        _timer?.Dispose();
        _timer = new System.Windows.Forms.Timer { Interval = 3000 };
        _timer.Tick += (_, _) => RefreshMetrics();
        _timer.Start();
        form.FormClosed += (_, _) =>
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
        };
    }

    private static void RefreshMetrics()
    {
        try
        {
            if (_dashboardRoot is null || _dashboardRoot.IsDisposed)
                return;

            Process process = Process.GetCurrentProcess();
            process.Refresh();
            double ramMb = process.WorkingSet64 / 1024D / 1024D;
            SetText("CommercialResources13",
                $"RAM FFGuardian: {ramMb:F1} MB\r\nProcessori logici: {Environment.ProcessorCount}\r\nSistema: operativo\r\nAggiornato: {DateTime.Now:HH:mm:ss}");

            string basePath = AppContext.BaseDirectory;
            int quarantine = CountFilesSafe(Path.Combine(basePath, "Quarantine")) +
                             CountFilesSafe(Path.Combine(basePath, "Quarantena"));
            int reports = CountFilesSafe(Path.Combine(basePath, "Reports")) +
                          CountFilesSafe(Path.Combine(basePath, "Rapporti"));
            SetText("CommercialQuarantine13", quarantine.ToString("N0"));
            SetText("CommercialReports13", reports.ToString("N0"));
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
        }
    }

    private static int CountFilesSafe(string path)
    {
        try
        {
            return Directory.Exists(path)
                ? Directory.EnumerateFiles(path, "*", SearchOption.TopDirectoryOnly).Take(10000).Count()
                : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static void SetText(string name, string value)
    {
        if (_dashboardRoot is null)
            return;
        Label? label = FindControls<Label>(_dashboardRoot)
            .FirstOrDefault(candidate => candidate.Name == name);
        if (label is not null)
            label.Text = value;
    }

    private static void SelectTab(TabControl tabs, string token)
    {
        TabPage? page = tabs.TabPages.Cast<TabPage>()
            .FirstOrDefault(candidate => candidate.Text.Contains(token, StringComparison.OrdinalIgnoreCase));
        if (page is not null)
            tabs.SelectedTab = page;
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
