using System.Runtime.CompilerServices;

namespace FFGuardian;

/// <summary>
/// Shell commerciale non distruttiva per l'interfaccia WinForms esistente.
/// Mantiene le TabPage e tutti i controlli originali, quindi non sostituisce
/// gli handler di scansione, aggiornamento, quarantena, YARA o ClamAV.
/// </summary>
internal static class PremiumCommercialShell28
{
    private static readonly Color Background = ColorTranslator.FromHtml("#111315");
    private static readonly Color Sidebar = ColorTranslator.FromHtml("#15181B");
    private static readonly Color Card = ColorTranslator.FromHtml("#1A1E21");
    private static readonly Color CardHover = ColorTranslator.FromHtml("#20252A");
    private static readonly Color Neon = ColorTranslator.FromHtml("#9DFF00");
    private static readonly Color NeonMuted = ColorTranslator.FromHtml("#73C900");
    private static readonly Color PrimaryText = ColorTranslator.FromHtml("#F4F7F8");
    private static readonly Color SecondaryText = ColorTranslator.FromHtml("#A7B0B5");
    private static readonly Color Border = ColorTranslator.FromHtml("#30363B");
    private static readonly Color Warning = ColorTranslator.FromHtml("#F5B942");
    private static readonly Color Error = ColorTranslator.FromHtml("#FF4D4D");

    private static bool _started;
    private static TableLayoutPanel? _shell;
    private static Panel? _sidebar;
    private static Panel? _header;
    private static FlowLayoutPanel? _nav;
    private static Button? _collapseButton;
    private static TabControl? _tabs;
    private static Label? _pageTitle;
    private static Label? _globalStatus;
    private static readonly Dictionary<TabPage, Button> NavigationButtons = [];

    [ModuleInitializer]
    internal static void Initialize() => Application.Idle += StartWhenReady;

    private static void StartWhenReady(object? sender, EventArgs e)
    {
        if (_started)
            return;

        IndependentMainForm100? form = Application.OpenForms
            .OfType<IndependentMainForm100>()
            .FirstOrDefault();
        if (form is null || form.IsDisposed || !form.IsHandleCreated)
            return;

        TabControl? tabs = FindControls<TabControl>(form)
            .OrderByDescending(candidate => candidate.TabCount)
            .FirstOrDefault(candidate => candidate.TabCount > 0);
        if (tabs is null || tabs.Parent is null)
            return;

        _started = true;
        Application.Idle -= StartWhenReady;
        Apply(form, tabs);
    }

    private static void Apply(Form form, TabControl tabs)
    {
        _tabs = tabs;
        form.SuspendLayout();
        try
        {
            form.BackColor = Background;
            form.ForeColor = PrimaryText;
            form.AutoScaleMode = AutoScaleMode.Dpi;
            form.MinimumSize = new Size(1180, 720);
            form.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);

            Control originalParent = tabs.Parent!;
            originalParent.SuspendLayout();
            originalParent.Controls.Remove(tabs);

            _shell = new TableLayoutPanel
            {
                Name = "PremiumCommercialShell28",
                Dock = DockStyle.Fill,
                BackColor = Background,
                ColumnCount = 2,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            _shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 236F));
            _shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            _shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 76F));
            _shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _sidebar = BuildSidebar(form);
            _header = BuildHeader();
            ConfigureTabs(tabs);

            _shell.Controls.Add(_sidebar, 0, 0);
            _shell.SetRowSpan(_sidebar, 2);
            _shell.Controls.Add(_header, 1, 0);
            _shell.Controls.Add(tabs, 1, 1);
            originalParent.Controls.Add(_shell);
            _shell.BringToFront();

            StyleAllPages(tabs);
            BuildNavigation(tabs);
            UpdateSelection(tabs.SelectedTab);

            tabs.SelectedIndexChanged += (_, _) => UpdateSelection(tabs.SelectedTab);
            form.Resize += (_, _) => ApplyResponsiveSizing(form);
            form.DpiChanged += (_, _) => ApplyResponsiveSizing(form);
            ApplyResponsiveSizing(form);

            originalParent.ResumeLayout(true);
        }
        finally
        {
            form.ResumeLayout(true);
        }
    }

    private static Panel BuildSidebar(Form form)
    {
        Panel panel = new()
        {
            Name = "PremiumSidebar28",
            Dock = DockStyle.Fill,
            BackColor = Sidebar,
            Margin = Padding.Empty,
            Padding = new Padding(16, 18, 16, 16)
        };

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Sidebar,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));

        Panel brand = new() { Dock = DockStyle.Fill, BackColor = Sidebar };
        Label logo = new()
        {
            Name = "CompactBrand28",
            Dock = DockStyle.Left,
            Width = 48,
            Text = "◆",
            ForeColor = Neon,
            Font = new Font("Segoe UI Symbol", 25F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            AccessibleName = "Logo FFGuardian"
        };
        Label title = new()
        {
            Dock = DockStyle.Fill,
            Text = "FFGuardian\nUltimate Protection",
            ForeColor = PrimaryText,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0),
            AutoEllipsis = true
        };
        brand.Controls.Add(title);
        brand.Controls.Add(logo);

        _nav = new FlowLayoutPanel
        {
            Name = "PremiumNavigation28",
            Dock = DockStyle.Fill,
            BackColor = Sidebar,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Margin = Padding.Empty,
            Padding = new Padding(0, 10, 0, 10)
        };

        _collapseButton = CreateNavigationButton("☰", "Riduci barra laterale");
        _collapseButton.Dock = DockStyle.Fill;
        _collapseButton.Click += (_, _) => ToggleSidebar(form, title);

        layout.Controls.Add(brand, 0, 0);
        layout.Controls.Add(_nav, 0, 1);
        layout.Controls.Add(_collapseButton, 0, 2);
        panel.Controls.Add(layout);
        return panel;
    }

    private static Panel BuildHeader()
    {
        Panel panel = new()
        {
            Name = "PremiumHeader28",
            Dock = DockStyle.Fill,
            BackColor = Background,
            Margin = Padding.Empty,
            Padding = new Padding(24, 12, 24, 10)
        };

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Background,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280F));

        _pageTitle = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Dashboard",
            ForeColor = PrimaryText,
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };

        _globalStatus = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Sistema protetto  ●",
            ForeColor = Neon,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight,
            AccessibleName = "Stato globale protezione"
        };

        layout.Controls.Add(_pageTitle, 0, 0);
        layout.Controls.Add(_globalStatus, 1, 0);
        panel.Controls.Add(layout);
        return panel;
    }

    private static void ConfigureTabs(TabControl tabs)
    {
        tabs.Dock = DockStyle.Fill;
        tabs.Margin = new Padding(24, 0, 24, 24);
        tabs.Padding = Padding.Empty;
        tabs.Appearance = TabAppearance.FlatButtons;
        tabs.SizeMode = TabSizeMode.Fixed;
        tabs.ItemSize = new Size(0, 1);
        tabs.Multiline = true;
        tabs.BackColor = Background;
    }

    private static void BuildNavigation(TabControl tabs)
    {
        if (_nav is null)
            return;

        NavigationButtons.Clear();
        _nav.Controls.Clear();
        foreach (TabPage page in tabs.TabPages)
        {
            string label = NormalizePageName(page.Text);
            Button button = CreateNavigationButton(IconFor(label), label);
            button.Tag = page;
            button.Click += (_, _) => tabs.SelectedTab = page;
            NavigationButtons[page] = button;
            _nav.Controls.Add(button);
        }
    }

    private static Button CreateNavigationButton(string icon, string text)
    {
        Button button = new()
        {
            Width = 200,
            Height = 44,
            Margin = new Padding(0, 0, 0, 6),
            Padding = new Padding(12, 0, 10, 0),
            Text = $"{icon}   {text}",
            TextAlign = ContentAlignment.MiddleLeft,
            FlatStyle = FlatStyle.Flat,
            BackColor = Sidebar,
            ForeColor = SecondaryText,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            UseVisualStyleBackColor = false,
            AutoEllipsis = true,
            AccessibleName = text,
            TabStop = true
        };
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = CardHover;
        button.FlatAppearance.MouseDownBackColor = Card;
        return button;
    }

    private static void StyleAllPages(TabControl tabs)
    {
        foreach (TabPage page in tabs.TabPages)
        {
            page.BackColor = Background;
            page.ForeColor = PrimaryText;
            page.Padding = new Padding(0);
            StyleTree(page);
        }
    }

    private static void StyleTree(Control root)
    {
        foreach (Control control in root.Controls)
        {
            switch (control)
            {
                case Button button:
                    StyleButton(button);
                    break;
                case Label label:
                    label.ForeColor = label.ForeColor == Color.Red ? Error : PrimaryText;
                    label.BackColor = Color.Transparent;
                    label.AutoEllipsis = true;
                    if (label.Font.Size < 9F)
                        label.Font = new Font("Segoe UI", 9F, label.Font.Style);
                    break;
                case GroupBox group:
                    group.ForeColor = PrimaryText;
                    group.BackColor = Card;
                    group.Padding = new Padding(20);
                    break;
                case Panel panel:
                    if (panel.BackColor != Color.Transparent)
                        panel.BackColor = Card;
                    break;
                case TableLayoutPanel table:
                    table.BackColor = Background;
                    break;
                case FlowLayoutPanel flow:
                    flow.BackColor = Background;
                    flow.Padding = new Padding(0, 4, 0, 4);
                    break;
                case DataGridView grid:
                    StyleGrid(grid);
                    break;
                case TextBox textBox:
                    textBox.BackColor = Card;
                    textBox.ForeColor = PrimaryText;
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case CheckBox checkBox:
                    checkBox.ForeColor = PrimaryText;
                    checkBox.BackColor = Color.Transparent;
                    checkBox.MinimumSize = new Size(0, 32);
                    break;
            }
            StyleTree(control);
        }
    }

    private static void StyleButton(Button button)
    {
        bool primary = button.Text.Contains("ESEGUI", StringComparison.OrdinalIgnoreCase) ||
                       button.Text.Contains("AVVIA", StringComparison.OrdinalIgnoreCase) ||
                       button.Text.Contains("AGGIORNA", StringComparison.OrdinalIgnoreCase) ||
                       button.Text.Contains("PROTEGGI", StringComparison.OrdinalIgnoreCase);

        button.FlatStyle = FlatStyle.Flat;
        button.UseVisualStyleBackColor = false;
        button.FlatAppearance.BorderSize = primary ? 0 : 1;
        button.FlatAppearance.BorderColor = NeonMuted;
        button.FlatAppearance.MouseOverBackColor = primary ? NeonMuted : CardHover;
        button.FlatAppearance.MouseDownBackColor = NeonMuted;
        button.BackColor = primary ? Neon : Card;
        button.ForeColor = primary ? Background : PrimaryText;
        button.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        button.MinimumSize = new Size(120, 42);
        button.Padding = new Padding(12, 0, 12, 0);
        button.AutoEllipsis = true;
        button.TabStop = true;
    }

    private static void StyleGrid(DataGridView grid)
    {
        grid.BackgroundColor = Background;
        grid.BorderStyle = BorderStyle.None;
        grid.GridColor = Border;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Card;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = PrimaryText;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        grid.DefaultCellStyle.BackColor = Background;
        grid.DefaultCellStyle.ForeColor = PrimaryText;
        grid.DefaultCellStyle.SelectionBackColor = CardHover;
        grid.DefaultCellStyle.SelectionForeColor = PrimaryText;
        grid.RowHeadersVisible = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
    }

    private static void UpdateSelection(TabPage? selected)
    {
        if (selected is null)
            return;

        string title = NormalizePageName(selected.Text);
        if (_pageTitle is not null)
            _pageTitle.Text = title;

        foreach ((TabPage page, Button button) in NavigationButtons)
        {
            bool active = ReferenceEquals(page, selected);
            button.BackColor = active ? CardHover : Sidebar;
            button.ForeColor = active ? Neon : SecondaryText;
            button.FlatAppearance.BorderSize = active ? 1 : 0;
            button.FlatAppearance.BorderColor = active ? Neon : Sidebar;
            button.Font = new Font("Segoe UI", 9.5F, active ? FontStyle.Bold : FontStyle.Regular);
        }
    }

    private static void ToggleSidebar(Form form, Label title)
    {
        if (_shell is null || _nav is null || _collapseButton is null)
            return;

        bool expanded = _shell.ColumnStyles[0].Width > 100F;
        _shell.ColumnStyles[0].Width = expanded ? 76F : 236F;
        title.Visible = !expanded;
        foreach (Button button in _nav.Controls.OfType<Button>())
        {
            string accessible = button.AccessibleName ?? button.Text;
            button.Width = expanded ? 44 : 200;
            button.Text = expanded ? IconFor(accessible) : $"{IconFor(accessible)}   {accessible}";
            button.TextAlign = expanded ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft;
            button.Padding = expanded ? Padding.Empty : new Padding(12, 0, 10, 0);
        }
        _collapseButton.Text = expanded ? "☰" : "☰   Riduci barra laterale";
        ApplyResponsiveSizing(form);
    }

    private static void ApplyResponsiveSizing(Form form)
    {
        if (_shell is null || _nav is null || _header is null || _tabs is null)
            return;

        bool compact = form.ClientSize.Width < 1320;
        if (compact && _shell.ColumnStyles[0].Width > 100F)
            _shell.ColumnStyles[0].Width = 204F;
        else if (!compact && _shell.ColumnStyles[0].Width is > 100F and < 220F)
            _shell.ColumnStyles[0].Width = 236F;

        _shell.RowStyles[0].Height = form.DeviceDpi >= 144 ? 88F : 76F;
        _tabs.Margin = compact
            ? new Padding(16, 0, 16, 16)
            : new Padding(24, 0, 24, 24);

        foreach (Button button in _nav.Controls.OfType<Button>())
            button.Width = Math.Max(44, (int)_shell.ColumnStyles[0].Width - 32);
    }

    private static string NormalizePageName(string value)
    {
        string text = value.Trim();
        if (text.Length == 0)
            return "Dashboard";
        return text.ToLowerInvariant() switch
        {
            "audit" => "Audit",
            "attività" or "attivita" => "Attività",
            "ransom shield" => "Ransom Shield",
            "usb shield" => "USB Shield",
            _ => char.ToUpperInvariant(text[0]) + text[1..].ToLowerInvariant()
        };
    }

    private static string IconFor(string page) => page.ToUpperInvariant() switch
    {
        var name when name.Contains("DASH") => "⌂",
        var name when name.Contains("SCAN") => "⌕",
        var name when name.Contains("PROTE") => "◈",
        var name when name.Contains("RANSOM") => "▣",
        var name when name.Contains("FIRE") => "▦",
        var name when name.Contains("USB") => "▤",
        var name when name.Contains("RECUP") => "↶",
        var name when name.Contains("AGGIORN") => "↓",
        var name when name.Contains("ATTIV") => "⌁",
        var name when name.Contains("SALUTE") => "♡",
        var name when name.Contains("ASSIST") => "?",
        var name when name.Contains("IMPOST") => "⚙",
        var name when name.Contains("AUDIT") => "✓",
        _ => "•"
    };

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
