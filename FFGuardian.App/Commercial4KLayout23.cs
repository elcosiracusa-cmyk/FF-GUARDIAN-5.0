using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Text;

namespace FFGuardian;

/// <summary>
/// Rifinitura finale 4K della sola interfaccia commerciale attiva.
/// Non ricrea le pagine e non sostituisce i comandi originali.
/// </summary>
internal static class Commercial4KLayout23
{
    private const int SafeHeaderGap = 34;
    private const int SafeRightGap = 22;
    private static readonly Color Background = Color.FromArgb(4, 8, 11);
    private static readonly Color Surface = Color.FromArgb(10, 16, 20);
    private static readonly Color Neon = Color.FromArgb(112, 255, 24);
    private static readonly Color Text = Color.FromArgb(242, 247, 249);

    private static System.Windows.Forms.Timer? _startupTimer;
    private static System.Windows.Forms.Timer? _layoutTimer;
    private static TabControl? _tabs;
    private static Form? _form;
    private static bool _started;
    private static readonly Dictionary<Control, Padding> OriginalPadding = new();

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
        if (tabs is null)
            return;

        _started = true;
        _form = form;
        _tabs = tabs;
        Application.Idle -= StartWhenReady;

        form.AutoScaleMode = AutoScaleMode.Dpi;
        form.MinimumSize = new Size(1180, 720);
        form.DoubleBuffered(true);

        _startupTimer = new System.Windows.Forms.Timer { Interval = 1850 };
        _startupTimer.Tick += (_, _) =>
        {
            DisposeStartupTimer();
            ApplyAll();
        };
        _startupTimer.Start();

        tabs.SelectedIndexChanged += (_, _) => ScheduleLayout();
        form.Resize += (_, _) => ScheduleLayout();
        form.DpiChanged += (_, _) => ScheduleLayout();
        form.FormClosed += (_, _) => DisposeTimers();
    }

    private static void ScheduleLayout()
    {
        _layoutTimer?.Stop();
        _layoutTimer?.Dispose();
        _layoutTimer = new System.Windows.Forms.Timer { Interval = 140 };
        _layoutTimer.Tick += (_, _) =>
        {
            _layoutTimer?.Stop();
            _layoutTimer?.Dispose();
            _layoutTimer = null;
            ApplyAll();
        };
        _layoutTimer.Start();
    }

    private static void ApplyAll()
    {
        if (_tabs is null || _tabs.IsDisposed || _form is null || _form.IsDisposed)
            return;

        LowerNavigationBelowHeader(_tabs);
        foreach (TabPage page in _tabs.TabPages)
            FitPage(page);

        InstallDobermannMark(_tabs);
        WriteAudit();
    }

    private static void LowerNavigationBelowHeader(TabControl tabs)
    {
        Control? host = tabs.Parent;
        if (host is null || host is Form)
            return;

        if (!OriginalPadding.TryGetValue(host, out Padding original))
        {
            original = host.Padding;
            OriginalPadding[host] = original;
        }

        int scaledGap = Scale(SafeHeaderGap);
        host.Padding = new Padding(
            original.Left,
            Math.Max(original.Top, scaledGap),
            Math.Max(original.Right, Scale(SafeRightGap)),
            original.Bottom);
        host.PerformLayout();
    }

    private static void FitPage(TabPage page)
    {
        page.BackColor = Background;
        page.ForeColor = Text;
        page.AutoScroll = false;
        page.AutoScrollMinSize = Size.Empty;

        Control? root = page.Controls.Cast<Control>().FirstOrDefault(control =>
            control.Name is "CommercialPageRoot18" or "CommercialDashboard18");
        if (root is null)
            return;

        root.Dock = DockStyle.Fill;
        root.Margin = Padding.Empty;
        root.Padding = new Padding(Scale(12), Scale(18), Scale(SafeRightGap), Scale(12));

        if (root.Name == "CommercialDashboard18")
        {
            FitDashboard(root);
            return;
        }

        if (root is not TableLayoutPanel table || table.RowCount < 3)
            return;

        FlowLayoutPanel? commands = FindControls<FlowLayoutPanel>(table)
            .FirstOrDefault(flow => FindControls<Button>(flow).Any());
        if (commands is null)
            return;

        List<Button> buttons = FindControls<Button>(commands)
            .Where(button => !button.IsDisposed)
            .ToList();
        if (buttons.Count == 0)
            return;

        int safeWidth = Math.Max(520,
            page.ClientSize.Width - root.Padding.Horizontal - Scale(18));
        int columns = safeWidth >= Scale(1800) ? 4
            : safeWidth >= Scale(1250) ? 3
            : 2;
        columns = Math.Min(columns, buttons.Count);

        int gap = Scale(12);
        int buttonWidth = Math.Max(Scale(180),
            (safeWidth - gap * Math.Max(0, columns - 1)) / Math.Max(1, columns));

        commands.Dock = DockStyle.Fill;
        commands.FlowDirection = FlowDirection.LeftToRight;
        commands.WrapContents = true;
        commands.AutoScroll = false;
        commands.Padding = new Padding(0, Scale(8), 0, Scale(5));
        commands.Margin = Padding.Empty;

        foreach (Button button in buttons)
        {
            button.Width = buttonWidth;
            button.Height = Scale(50);
            button.MinimumSize = new Size(Scale(150), Scale(44));
            button.MaximumSize = Size.Empty;
            button.Margin = new Padding(0, 0, gap, gap);
            button.AutoEllipsis = true;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.UseVisualStyleBackColor = false;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = Neon;
            button.ForeColor = Text;
        }

        int rows = Math.Max(1, (int)Math.Ceiling(buttons.Count / (double)columns));
        int desiredHeight = Scale(20) + rows * (Scale(50) + gap);
        int maximumHeight = Math.Max(Scale(110), page.ClientSize.Height / 2);
        table.RowStyles[1].SizeType = SizeType.Absolute;
        table.RowStyles[1].Height = Math.Clamp(desiredHeight, Scale(92), maximumHeight);
        table.PerformLayout();
    }

    private static void FitDashboard(Control root)
    {
        foreach (TableLayoutPanel table in FindControls<TableLayoutPanel>(root))
        {
            table.AutoScroll = false;
            table.Margin = Padding.Empty;
        }

        foreach (Button button in FindControls<Button>(root))
        {
            button.MinimumSize = new Size(Scale(96), Scale(36));
            button.AutoEllipsis = true;
            button.TextAlign = ContentAlignment.MiddleCenter;
        }

        foreach (Label label in FindControls<Label>(root))
            label.AutoEllipsis = true;
    }

    private static void InstallDobermannMark(TabControl tabs)
    {
        TabPage? dashboard = tabs.TabPages.Cast<TabPage>()
            .FirstOrDefault(page => Normalize(page.Text).Contains("DASH", StringComparison.Ordinal));
        if (dashboard is null)
            dashboard = tabs.TabPages.Cast<TabPage>()
                .FirstOrDefault(page => FindControls<Control>(page)
                    .Any(control => control.Name == "CommercialDashboard18"));
        if (dashboard is null || FindControls<DobermannShieldControl23>(dashboard).Any())
            return;

        Label? oldShield = FindControls<Label>(dashboard)
            .FirstOrDefault(label => label.Text.Trim() == "✓" && label.Font.Size >= 40F);
        if (oldShield?.Parent is not TableLayoutPanel parent)
            return;

        TableLayoutPanelCellPosition position = parent.GetPositionFromControl(oldShield);
        parent.Controls.Remove(oldShield);
        oldShield.Dispose();

        DobermannShieldControl23 mark = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Margin = Padding.Empty
        };
        parent.Controls.Add(mark, position.Column, position.Row);
        mark.BringToFront();
    }

    private static void WriteAudit()
    {
        if (_tabs is null)
            return;

        List<string> lines =
        [
            "FFGUARDIAN 4K UI AUDIT",
            DateTime.Now.ToString("O"),
            new string('=', 72)
        ];

        foreach (TabPage page in _tabs.TabPages)
        {
            List<string> defects = [];
            foreach (Button button in FindControls<Button>(page))
            {
                if (button.Width < Scale(120) || button.Height < Scale(34))
                    defects.Add($"dimensioni insufficienti: {Normalize(button.Text)}");
                if (ReferenceEquals(_tabs.SelectedTab, page) && button.Visible && IsClipped(button))
                    defects.Add($"tagliato: {Normalize(button.Text)}");
            }

            lines.Add(defects.Count == 0
                ? $"[{Normalize(page.Text)}] OK"
                : $"[{Normalize(page.Text)}] {string.Join("; ", defects.Distinct())}");
        }

        try
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FF Guardian", "Engine10", "Diagnostics");
            Directory.CreateDirectory(folder);
            File.WriteAllLines(Path.Combine(folder, "ui-4k-audit.log"), lines, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
        }
    }

    private static bool IsClipped(Control control)
    {
        Rectangle controlRect = new(control.PointToScreen(Point.Empty), control.Size);
        for (Control? parent = control.Parent; parent is not null; parent = parent.Parent)
        {
            Rectangle parentRect = new(parent.PointToScreen(Point.Empty), parent.ClientSize);
            if (!parentRect.Contains(controlRect))
                return true;
            if (parent is TabPage)
                break;
        }
        return false;
    }

    private static int Scale(int value)
    {
        int dpi = _form?.DeviceDpi ?? 96;
        return Math.Max(1, (int)Math.Round(value * dpi / 96D));
    }

    private static string Normalize(string value)
    {
        string text = value.Replace("&", string.Empty, StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
        while (text.Contains("  ", StringComparison.Ordinal))
            text = text.Replace("  ", " ", StringComparison.Ordinal);
        return text.ToUpperInvariant();
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

    private static void DisposeStartupTimer()
    {
        _startupTimer?.Stop();
        _startupTimer?.Dispose();
        _startupTimer = null;
    }

    private static void DisposeTimers()
    {
        DisposeStartupTimer();
        _layoutTimer?.Stop();
        _layoutTimer?.Dispose();
        _layoutTimer = null;
        _tabs = null;
        _form = null;
    }
}

internal sealed class DobermannShieldControl23 : Control
{
    private static readonly Color Neon = Color.FromArgb(112, 255, 24);
    private static readonly Color Dark = Color.FromArgb(4, 8, 11);
    private static readonly Color Copper = Color.FromArgb(176, 104, 48);

    public DobermannShieldControl23()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.UserPaint |
                 ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.SupportsTransparentBackColor, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        RectangleF bounds = ClientRectangle;
        float size = Math.Max(20F, Math.Min(bounds.Width, bounds.Height) * 0.82F);
        float left = bounds.Left + (bounds.Width - size) / 2F;
        float top = bounds.Top + (bounds.Height - size) / 2F;
        RectangleF box = new(left, top, size, size);

        using GraphicsPath shield = new();
        shield.AddPolygon(
        [
            new PointF(box.Left + box.Width * 0.50F, box.Top),
            new PointF(box.Right - box.Width * 0.08F, box.Top + box.Height * 0.14F),
            new PointF(box.Right - box.Width * 0.14F, box.Top + box.Height * 0.64F),
            new PointF(box.Left + box.Width * 0.50F, box.Bottom),
            new PointF(box.Left + box.Width * 0.14F, box.Top + box.Height * 0.64F),
            new PointF(box.Left + box.Width * 0.08F, box.Top + box.Height * 0.14F)
        ]);

        using SolidBrush glow = new(Color.FromArgb(28, Neon));
        graphics.FillPath(glow, shield);
        using Pen shieldPen = new(Neon, Math.Max(2F, size * 0.024F));
        shieldPen.LineJoin = LineJoin.Round;
        graphics.DrawPath(shieldPen, shield);

        PointF center = new(box.Left + box.Width * 0.50F, box.Top + box.Height * 0.48F);
        using GraphicsPath head = new();
        head.AddPolygon(
        [
            new PointF(center.X - size * 0.22F, center.Y - size * 0.13F),
            new PointF(center.X - size * 0.28F, center.Y - size * 0.36F),
            new PointF(center.X - size * 0.10F, center.Y - size * 0.25F),
            new PointF(center.X, center.Y - size * 0.30F),
            new PointF(center.X + size * 0.10F, center.Y - size * 0.25F),
            new PointF(center.X + size * 0.28F, center.Y - size * 0.36F),
            new PointF(center.X + size * 0.22F, center.Y - size * 0.13F),
            new PointF(center.X + size * 0.16F, center.Y + size * 0.20F),
            new PointF(center.X, center.Y + size * 0.32F),
            new PointF(center.X - size * 0.16F, center.Y + size * 0.20F)
        ]);

        using SolidBrush headBrush = new(Dark);
        graphics.FillPath(headBrush, head);
        using Pen headPen = new(Neon, Math.Max(1.5F, size * 0.015F));
        graphics.DrawPath(headPen, head);

        float eyeY = center.Y - size * 0.035F;
        float eyeW = size * 0.055F;
        float eyeH = size * 0.025F;
        using SolidBrush eyeBrush = new(Neon);
        graphics.FillEllipse(eyeBrush, center.X - size * 0.12F, eyeY, eyeW, eyeH);
        graphics.FillEllipse(eyeBrush, center.X + size * 0.065F, eyeY, eyeW, eyeH);

        using SolidBrush copperBrush = new(Copper);
        graphics.FillEllipse(copperBrush, center.X - size * 0.13F, center.Y + size * 0.08F,
            size * 0.26F, size * 0.13F);
        using SolidBrush noseBrush = new(Neon);
        graphics.FillEllipse(noseBrush, center.X - size * 0.035F, center.Y + size * 0.13F,
            size * 0.07F, size * 0.045F);
    }
}

internal static class ControlDoubleBufferExtensions23
{
    public static void DoubleBuffered(this Control control, bool enabled)
    {
        typeof(Control).GetProperty(
            "DoubleBuffered",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(control, enabled);
    }
}
