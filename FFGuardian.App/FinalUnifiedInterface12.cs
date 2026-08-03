using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace FFGuardian;

/// <summary>
/// Interfaccia finale, unica e responsiva. Riutilizza le pagine e i comandi
/// esistenti senza modificare il motore antivirus.
/// </summary>
internal static class FinalUnifiedInterface12
{
    private static readonly Color Background = Color.FromArgb(5, 9, 13);
    private static readonly Color Surface = Color.FromArgb(14, 22, 28);
    private static readonly Color Raised = Color.FromArgb(25, 37, 45);
    private static readonly Color Neon = Color.FromArgb(108, 255, 36);
    private static readonly Color Text = Color.FromArgb(244, 248, 250);
    private static readonly Color Muted = Color.FromArgb(184, 198, 207);
    private static readonly Color Border = Color.FromArgb(66, 91, 102);
    private static bool _applied;
    private static System.Windows.Forms.Timer? _timer;

    [ModuleInitializer]
    internal static void Initialize() => Application.Idle += ApplyWhenReady;

    private static void ApplyWhenReady(object? sender, EventArgs e)
    {
        if (_applied)
            return;

        IndependentMainForm100? form = Application.OpenForms.OfType<IndependentMainForm100>().FirstOrDefault();
        if (form is null || form.IsDisposed || !form.IsHandleCreated)
            return;

        TabControl? tabs = FindControls<TabControl>(form)
            .OrderByDescending(control => control.TabCount)
            .FirstOrDefault(control => control.TabCount > 0);
        if (tabs is null)
            return;

        try
        {
            Build(form, tabs);
            _applied = true;
            Application.Idle -= ApplyWhenReady;
            StabilityCoordinator82.WriteInformationLog("Interfaccia finale responsive 12 applicata.");
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            Application.Idle -= ApplyWhenReady;
        }
    }

    private static void Build(IndependentMainForm100 form, TabControl tabs)
    {
        form.SuspendLayout();
        try
        {
            form.MinimumSize = new Size(1024, 680);
            form.BackColor = Background;
            form.Font = new Font("Segoe UI", 10F);

            tabs.Parent?.Controls.Remove(tabs);

            Panel shell = new()
            {
                Name = "FinalUnifiedShell12",
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
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220F));
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            FlowLayoutPanel nav = BuildNavigation(tabs);
            Panel host = new()
            {
                Dock = DockStyle.Fill,
                BackColor = Background,
                Padding = new Padding(10)
            };

            ConfigureTabs(tabs);
            host.Controls.Add(tabs);
            body.Controls.Add(nav, 0, 0);
            body.Controls.Add(host, 1, 0);

            shell.Controls.Add(body);
            shell.Controls.Add(footer);
            shell.Controls.Add(header);
            form.Controls.Add(shell);
            shell.BringToFront();

            foreach (TabPage page in tabs.TabPages)
                NormalizePage(page);

            tabs.SelectedIndexChanged += (_, _) =>
            {
                UpdateNavigation(nav, tabs.SelectedIndex);
                if (tabs.SelectedTab is not null)
                    NormalizePage(tabs.SelectedTab);
            };

            form.Resize += (_, _) =>
            {
                body.ColumnStyles[0].Width = form.ClientSize.Width < 1180 ? 180F : 220F;
                ResizeNavigation(nav, body.ColumnStyles[0].Width);
                if (tabs.SelectedTab is not null)
                    NormalizePage(tabs.SelectedTab);
            };

            StartMetrics(form, footer);
            UpdateNavigation(nav, tabs.SelectedIndex);
            ResizeNavigation(nav, body.ColumnStyles[0].Width);
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
            Height = 72,
            BackColor = Surface,
            Padding = new Padding(16, 8, 16, 8)
        };

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ColumnCount = 3,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 54F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210F));

        Label logo = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Raised,
            ForeColor = Neon,
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            Text = "FG",
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0, 0, 10, 0)
        };

        Panel brand = new() { Dock = DockStyle.Fill, BackColor = Surface };
        brand.Controls.Add(new Label
        {
            Dock = DockStyle.Bottom,
            Height = 22,
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
            Font = new Font("Segoe UI", 19F, FontStyle.Bold),
            Text = "FFGUARDIAN",
            TextAlign = ContentAlignment.MiddleLeft
        });

        Label state = new()
        {
            Name = "FinalProtectionState12",
            Dock = DockStyle.Fill,
            BackColor = Raised,
            ForeColor = Neon,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            Text = "● PROTETTO",
            TextAlign = ContentAlignment.MiddleCenter
        };

        layout.Controls.Add(logo, 0, 0);
        layout.Controls.Add(brand, 1, 0);
        layout.Controls.Add(state, 2, 0);
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
            Name = "FinalMetrics12",
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 8.5F),
            Text = "Engine10 pronto",
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        });
        return footer;
    }

    private static FlowLayoutPanel BuildNavigation(TabControl tabs)
    {
        FlowLayoutPanel nav = new()
        {
            Name = "FinalNavigation12",
            Dock = DockStyle.Fill,
            BackColor = Surface,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(10, 12, 10, 12),
            Margin = Padding.Empty
        };

        for (int i = 0; i < tabs.TabCount; i++)
        {
            int index = i;
            string title = CleanTitle(tabs.TabPages[i].Text);
            Button button = new()
            {
                Name = $"FinalNav12_{i}",
                Tag = i,
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
                AutoEllipsis = true
            };
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Border;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(42, 62, 50);
            button.Click += (_, _) => tabs.SelectedIndex = index;
            nav.Controls.Add(button);
        }
        return nav;
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

    private static void NormalizePage(TabPage page)
    {
        page.SuspendLayout();
        try
        {
            page.BackColor = Background;
            page.ForeColor = Text;
            page.Padding = new Padding(12);
            page.AutoScroll = true;
            PolishTree(page);

            foreach (Control child in page.Controls)
            {
                if (child is DataGridView grid)
                {
                    ConfigureGrid(grid);
                    continue;
                }

                AnchorStyles defaultAnchor = AnchorStyles.Top | AnchorStyles.Left;
                if (child.Dock == DockStyle.None && child.Anchor == defaultAnchor)
                {
                    child.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                    child.Width = Math.Max(520, page.ClientSize.Width - 28);
                }
            }
        }
        finally
        {
            page.ResumeLayout(true);
        }
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
                    label.ForeColor = label.ForeColor == Neon ? Neon : Text;
                    label.BackColor = label.Parent is null ? Background : label.Parent.BackColor;
                    label.AutoEllipsis = true;
                    if (label.Font.Size > 16F)
                        label.Font = new Font("Segoe UI", 14F, label.Font.Style);
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
        grid.Dock = DockStyle.Fill;
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
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        grid.AllowUserToResizeColumns = true;
        grid.AllowUserToOrderColumns = true;
    }

    private static void UpdateNavigation(FlowLayoutPanel nav, int selectedIndex)
    {
        foreach (Button button in nav.Controls.OfType<Button>())
        {
            bool selected = button.Tag is int index && index == selectedIndex;
            button.BackColor = selected ? Neon : Raised;
            button.ForeColor = selected ? Background : Text;
            button.FlatAppearance.BorderColor = selected ? Neon : Border;
        }
    }

    private static void ResizeNavigation(FlowLayoutPanel nav, float width)
    {
        int buttonWidth = Math.Max(150, (int)width - nav.Padding.Horizontal - 2);
        foreach (Button button in nav.Controls.OfType<Button>())
            button.Width = buttonWidth;
    }

    private static void StartMetrics(Form form, Panel footer)
    {
        Label? label = footer.Controls.OfType<Label>().FirstOrDefault(control => control.Name == "FinalMetrics12");
        _timer?.Dispose();
        _timer = new System.Windows.Forms.Timer { Interval = 3000 };
        _timer.Tick += (_, _) =>
        {
            try
            {
                Process process = Process.GetCurrentProcess();
                process.Refresh();
                double ram = process.WorkingSet64 / 1024D / 1024D;
                if (label is not null)
                    label.Text = $"FFGuardian 10.0.1 Stable • Engine10 pronto • RAM {ram:F1} MB • {DateTime.Now:dd/MM/yyyy HH:mm:ss}";
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
