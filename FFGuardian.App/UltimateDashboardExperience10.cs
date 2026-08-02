using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;

namespace FFGuardian;

internal static class UltimateDashboardExperience10
{
    private static readonly Color Background = Color.FromArgb(3, 8, 12);
    private static readonly Color Surface = Color.FromArgb(10, 20, 26);
    private static readonly Color Raised = Color.FromArgb(17, 31, 39);
    private static readonly Color Neon = Color.FromArgb(108, 255, 36);
    private static readonly Color Muted = Color.FromArgb(174, 190, 200);
    private static bool _applied;

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
        if (dashboard is null)
            return;

        Apply(form, dashboard, tabs!);
        _applied = true;
        Application.Idle -= ApplyWhenReady;
        StabilityCoordinator82.WriteInformationLog("Ultimate Dashboard Experience 10 applicata.");
    }

    private static void Apply(IndependentMainForm100 form, TabPage dashboard, TabControl tabs)
    {
        form.SuspendLayout();
        dashboard.SuspendLayout();
        try
        {
            dashboard.Padding = new Padding(12);
            dashboard.AutoScroll = true;

            Panel commandDeck = BuildCommandDeck(form, tabs);
            dashboard.Controls.Add(commandDeck);
            commandDeck.BringToFront();

            ToolTip tips = new()
            {
                AutoPopDelay = 8000,
                InitialDelay = 250,
                ReshowDelay = 100,
                ShowAlways = true
            };
            ApplyToolTips(form, tips);

            form.KeyPreview = true;
            form.KeyDown += (_, key) =>
            {
                if (key.Control && key.KeyCode == Keys.Space)
                {
                    FindButton(form, "PROTEGGI ORA")?.PerformClick();
                    key.Handled = true;
                }
                else if (key.Control && key.KeyCode == Keys.F)
                {
                    SelectTab(tabs, "SCANSIONE");
                    key.Handled = true;
                }
                else if (key.KeyCode == Keys.Escape)
                {
                    FindButton(form, "ANNULLA")?.PerformClick();
                }
            };

            form.Resize += (_, _) => ResizeDeck(commandDeck, form.ClientSize.Width);
            ResizeDeck(commandDeck, form.ClientSize.Width);
        }
        finally
        {
            dashboard.ResumeLayout(true);
            form.ResumeLayout(true);
        }
    }

    private static Panel BuildCommandDeck(IndependentMainForm100 form, TabControl tabs)
    {
        Panel host = new()
        {
            Name = "UltimateCommandDeck10",
            Dock = DockStyle.Top,
            Height = 290,
            BackColor = Background,
            Padding = new Padding(8, 8, 8, 14)
        };

        RoundedPanel10 card = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            BorderColor = Color.FromArgb(45, 72, 82),
            GlowColor = Neon,
            CornerRadius = 18,
            Padding = new Padding(22)
        };

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 310));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));

        ShieldPulse10 shield = new() { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        layout.SetRowSpan(shield, 2);
        layout.Controls.Add(shield, 0, 0);

        Panel center = new() { Dock = DockStyle.Fill, BackColor = Color.Transparent, Padding = new Padding(12, 2, 12, 2) };
        center.Controls.Add(new Label
        {
            Dock = DockStyle.Bottom,
            Height = 42,
            Text = "Tre livelli di difesa: motore autonomo, Ransom Shield e controllo integrità.\nTutte le azioni distruttive richiedono conferma e rollback.",
            ForeColor = Muted,
            Font = new Font("Segoe UI", 9.5F),
            AutoEllipsis = true
        });
        center.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "IL TUO SISTEMA È SOTTO PROTEZIONE",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        });
        center.Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            Height = 26,
            Text = "FFGUARDIAN ULTIMATE  •  THREE DOBERMANN DEFENSE",
            ForeColor = Neon,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        });
        layout.Controls.Add(center, 1, 0);

        RoundedPanel10 stateCard = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Raised,
            BorderColor = Color.FromArgb(45, 72, 82),
            GlowColor = Neon,
            CornerRadius = 14,
            Padding = new Padding(18)
        };
        stateCard.Controls.Add(new Label
        {
            Dock = DockStyle.Bottom,
            Height = 38,
            Text = "Monitoraggio in tempo reale\nEngine10 Definitive",
            ForeColor = Muted,
            Font = new Font("Segoe UI", 8.5F),
            TextAlign = ContentAlignment.BottomRight
        });
        stateCard.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "PROTETTO",
            ForeColor = Neon,
            Font = new Font("Segoe UI", 22F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        });
        stateCard.Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            Height = 24,
            Text = "STATO GENERALE",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.TopRight
        });
        layout.Controls.Add(stateCard, 2, 0);

        FlowLayoutPanel commands = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.Transparent,
            Padding = new Padding(8, 10, 8, 6)
        };

        commands.Controls.Add(CreatePrimaryButton("PROTEGGI ORA", () =>
        {
            Button? original = FindButton(form, "PROTEGGI ORA");
            if (original is not null && original.Parent is not null)
                original.PerformClick();
            else
                SelectTab(tabs, "SCANSIONE");
        }));
        commands.Controls.Add(CreateCommandButton("SCANSIONE COMPLETA", () => InvokeExistingOrTab(form, tabs, "SCANSIONE COMPLETA", "SCANSIONE")));
        commands.Controls.Add(CreateCommandButton("PROCESSI ATTIVI", () => InvokeExistingOrTab(form, tabs, "PROCESSI ATTIVI", "PROCESSI")));
        commands.Controls.Add(CreateCommandButton("CONTROLLO AVVIO", () => InvokeExistingOrTab(form, tabs, "CONTROLLO AVVIO", "AUDIT")));
        commands.Controls.Add(CreateCommandButton("QUARANTENA", () => InvokeExistingOrTab(form, tabs, "QUARANTENA", "RECUPERO")));
        commands.Controls.Add(CreateCommandButton("AGGIORNA FIRME", () => InvokeExistingOrTab(form, tabs, "AGGIORNA FIRME", "AGGIORNAMENTI")));
        layout.SetColumnSpan(commands, 2);
        layout.Controls.Add(commands, 1, 1);

        card.Controls.Add(layout);
        host.Controls.Add(card);
        return host;
    }

    private static Button CreatePrimaryButton(string text, Action action)
    {
        Button button = CreateCommandButton(text, action);
        button.Width = 180;
        button.BackColor = Neon;
        button.ForeColor = Color.FromArgb(3, 8, 12);
        button.FlatAppearance.BorderColor = Neon;
        button.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        return button;
    }

    private static Button CreateCommandButton(string text, Action action)
    {
        Button button = new()
        {
            Width = 160,
            Height = 44,
            Margin = new Padding(5),
            Text = text,
            BackColor = Raised,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            AccessibleName = text,
            TabStop = true
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(74, 106, 116);
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 64, 38);
        button.Click += (_, _) => action();
        return button;
    }

    private static void InvokeExistingOrTab(Control root, TabControl tabs, string command, string tab)
    {
        Button? original = FindButton(root, command, excludeParentName: "UltimateCommandDeck10");
        if (original is not null)
        {
            original.PerformClick();
            return;
        }
        SelectTab(tabs, tab);
    }

    private static void SelectTab(TabControl tabs, string text)
    {
        TabPage? page = tabs.TabPages.Cast<TabPage>()
            .FirstOrDefault(candidate => candidate.Text.Contains(text, StringComparison.OrdinalIgnoreCase));
        if (page is not null)
            tabs.SelectedTab = page;
    }

    private static Button? FindButton(Control root, string text, string? excludeParentName = null)
    {
        foreach (Control control in root.Controls)
        {
            if (control is Button button &&
                button.Text.Contains(text, StringComparison.OrdinalIgnoreCase) &&
                (excludeParentName is null || !IsInsideNamedParent(button, excludeParentName)))
                return button;

            Button? nested = FindButton(control, text, excludeParentName);
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

    private static void ApplyToolTips(Control root, ToolTip tips)
    {
        foreach (Control control in root.Controls)
        {
            if (control is Button button)
                tips.SetToolTip(button, $"Esegui: {button.Text}. Le operazioni sensibili richiedono conferma.");
            ApplyToolTips(control, tips);
        }
    }

    private static void ResizeDeck(Control deck, int width)
    {
        deck.Height = width < 1180 ? 350 : 290;
        foreach (TableLayoutPanel table in deck.Controls.OfType<Control>()
                     .SelectMany(AllDescendants).OfType<TableLayoutPanel>())
        {
            if (table.ColumnCount == 3 && table.ColumnStyles.Count == 3)
            {
                table.ColumnStyles[0].Width = width < 1180 ? 140 : 190;
                table.ColumnStyles[2].Width = width < 1180 ? 245 : 310;
            }
        }
    }

    private static IEnumerable<Control> AllDescendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control nested in AllDescendants(child))
                yield return nested;
        }
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

internal sealed class RoundedPanel10 : Panel
{
    public Color BorderColor { get; set; } = Color.DimGray;
    public Color GlowColor { get; set; } = Color.Lime;
    public int CornerRadius { get; set; } = 16;

    public RoundedPanel10()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle bounds = ClientRectangle;
        bounds.Inflate(-2, -2);
        using GraphicsPath path = CreateRoundedRectangle(bounds, CornerRadius);
        using SolidBrush fill = new(BackColor);
        using Pen border = new(BorderColor, 1F);
        using Pen glow = new(Color.FromArgb(70, GlowColor), 3F);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(glow, path);
        e.Graphics.DrawPath(border, path);
        base.OnPaint(e);
    }

    private static GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
    {
        int diameter = Math.Max(2, radius * 2);
        GraphicsPath path = new();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class ShieldPulse10 : Control
{
    private readonly System.Windows.Forms.Timer _timer;
    private float _phase;

    public ShieldPulse10()
    {
        DoubleBuffered = true;
        _timer = new System.Windows.Forms.Timer { Interval = 70 };
        _timer.Tick += (_, _) =>
        {
            _phase += .08F;
            Invalidate();
        };
        _timer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _timer.Dispose();
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        float pulse = (float)((Math.Sin(_phase) + 1D) / 2D);
        RectangleF shield = new(20, 12, Math.Max(80, Width - 40), Math.Max(100, Height - 24));
        using GraphicsPath path = new();
        path.AddPolygon([
            new PointF(shield.Left + shield.Width * .12F, shield.Top + shield.Height * .08F),
            new PointF(shield.Right - shield.Width * .12F, shield.Top + shield.Height * .08F),
            new PointF(shield.Right - shield.Width * .04F, shield.Top + shield.Height * .58F),
            new PointF(shield.Left + shield.Width * .50F, shield.Bottom),
            new PointF(shield.Left + shield.Width * .04F, shield.Top + shield.Height * .58F)
        ]);
        using LinearGradientBrush fill = new(shield, Color.FromArgb(29, 48, 56), Color.FromArgb(5, 11, 15), 90F);
        using Pen outer = new(Color.FromArgb((int)(80 + pulse * 130), 108, 255, 36), 5F);
        using Pen inner = new(Color.FromArgb(108, 255, 36), 1.8F);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(outer, path);
        e.Graphics.DrawPath(inner, path);

        Rectangle emblemBounds = Rectangle.Round(new RectangleF(
            shield.Left + shield.Width * .12F,
            shield.Top + shield.Height * .11F,
            shield.Width * .76F,
            shield.Height * .70F));
        using TripleDobermannEmblem10 emblem = new() { Size = emblemBounds.Size, BackColor = Color.Transparent };
        Bitmap bitmap = new(Math.Max(1, emblemBounds.Width), Math.Max(1, emblemBounds.Height));
        emblem.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
        e.Graphics.DrawImage(bitmap, emblemBounds);
        bitmap.Dispose();
    }
}
