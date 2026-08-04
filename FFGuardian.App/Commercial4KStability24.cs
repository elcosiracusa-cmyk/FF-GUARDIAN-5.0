using System.Runtime.CompilerServices;
using System.Text;

namespace FFGuardian;

/// <summary>
/// Controllo finale non distruttivo per la UI 4K.
/// Non ricrea pagine, non sostituisce pulsanti e non modifica gli eventi del motore.
/// </summary>
internal static class Commercial4KStability24
{
    private static System.Windows.Forms.Timer? _startupTimer;
    private static System.Windows.Forms.Timer? _layoutTimer;
    private static TabControl? _tabs;
    private static Form? _form;
    private static bool _started;

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

        _startupTimer = new System.Windows.Forms.Timer { Interval = 2800 };
        _startupTimer.Tick += (_, _) =>
        {
            DisposeStartupTimer();
            ApplyAndAudit();
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
        _layoutTimer = new System.Windows.Forms.Timer { Interval = 180 };
        _layoutTimer.Tick += (_, _) =>
        {
            _layoutTimer?.Stop();
            _layoutTimer?.Dispose();
            _layoutTimer = null;
            ApplyAndAudit();
        };
        _layoutTimer.Start();
    }

    private static void ApplyAndAudit()
    {
        if (_tabs is null || _tabs.IsDisposed || _form is null || _form.IsDisposed)
            return;

        List<string> report =
        [
            "FFGUARDIAN 4K FINAL STABILITY AUDIT",
            DateTime.Now.ToString("O"),
            new string('=', 76)
        ];

        foreach (TabPage page in _tabs.TabPages)
        {
            StabilizePage(page);
            report.Add(AuditPage(page));
        }

        bool dobermannPresent = FindControls<DobermannShieldControl23>(_tabs).Any();
        report.Add(dobermannPresent
            ? "[DOBERMANN] OK — marchio vettoriale caricato."
            : "[DOBERMANN] ATTENZIONE — marchio non ancora caricato nella Dashboard.");

        WriteReport(report);
    }

    private static void StabilizePage(TabPage page)
    {
        page.BackColor = Color.FromArgb(4, 8, 11);
        page.ForeColor = Color.FromArgb(242, 247, 249);

        Control? root = page.Controls.Cast<Control>().FirstOrDefault(control =>
            control.Name is "CommercialPageRoot18" or "CommercialDashboard18");
        if (root is null)
            return;

        root.Dock = DockStyle.Fill;
        root.Margin = Padding.Empty;

        if (root.Name == "CommercialDashboard18")
        {
            foreach (Button button in FindControls<Button>(root))
            {
                button.Visible = true;
                button.AutoEllipsis = true;
                button.MinimumSize = new Size(Scale(92), Scale(34));
            }
            return;
        }

        if (root is not TableLayoutPanel table || table.RowStyles.Count < 3)
            return;

        // Mantiene il titolo completamente sotto la barra FFGuardian.
        int headingHeight = Scale(72);
        table.RowStyles[0].SizeType = SizeType.Absolute;
        table.RowStyles[0].Height = headingHeight;

        FlowLayoutPanel? commands = FindControls<FlowLayoutPanel>(table)
            .FirstOrDefault(flow => FindControls<Button>(flow).Any());
        if (commands is null)
            return;

        List<Button> buttons = FindControls<Button>(commands)
            .Where(button => !button.IsDisposed)
            .ToList();
        if (buttons.Count == 0)
            return;

        int availableWidth = Math.Max(Scale(520),
            table.ClientSize.Width - table.Padding.Horizontal - Scale(30));
        int columns = availableWidth >= Scale(1680) ? 4
            : availableWidth >= Scale(1080) ? 3
            : 2;
        columns = Math.Clamp(columns, 1, buttons.Count);

        int gap = Scale(12);
        int buttonWidth = Math.Max(Scale(170),
            (availableWidth - gap * Math.Max(0, columns - 1)) / columns);

        commands.Dock = DockStyle.Fill;
        commands.FlowDirection = FlowDirection.LeftToRight;
        commands.WrapContents = true;
        commands.AutoScroll = false;
        commands.Padding = new Padding(0, Scale(10), Scale(8), Scale(6));

        foreach (Button button in buttons)
        {
            button.Visible = true;
            button.Width = buttonWidth;
            button.Height = Scale(52);
            button.MinimumSize = new Size(Scale(150), Scale(44));
            button.MaximumSize = Size.Empty;
            button.Margin = new Padding(0, 0, gap, gap);
            button.AutoEllipsis = true;
            button.TextAlign = ContentAlignment.MiddleCenter;
        }

        int rows = Math.Max(1, (int)Math.Ceiling(buttons.Count / (double)columns));
        int commandHeight = Scale(18) + rows * (Scale(52) + gap);
        table.RowStyles[1].SizeType = SizeType.Absolute;
        table.RowStyles[1].Height = commandHeight;

        int requiredHeight = headingHeight + commandHeight + Scale(170);
        bool needsVerticalFallback = page.ClientSize.Height > 0 && requiredHeight > page.ClientSize.Height;
        page.AutoScroll = needsVerticalFallback;
        page.HorizontalScroll.Enabled = false;
        page.HorizontalScroll.Visible = false;
        page.AutoScrollMinSize = needsVerticalFallback
            ? new Size(0, requiredHeight)
            : Size.Empty;

        table.PerformLayout();
    }

    private static string AuditPage(TabPage page)
    {
        List<string> defects = [];
        bool selected = ReferenceEquals(_tabs?.SelectedTab, page);

        foreach (Button button in FindControls<Button>(page))
        {
            if (button.Width < Scale(120) || button.Height < Scale(34))
                defects.Add($"dimensione insufficiente: {Normalize(button.Text)}");
            if (selected && !button.Visible)
                defects.Add($"nascosto: {Normalize(button.Text)}");
            if (selected && button.Visible && IsClipped(button))
                defects.Add($"tagliato: {Normalize(button.Text)}");
        }

        return defects.Count == 0
            ? $"[{Normalize(page.Text)}] OK — comandi visibili e allineati."
            : $"[{Normalize(page.Text)}] {string.Join("; ", defects.Distinct(StringComparer.OrdinalIgnoreCase))}";
    }

    private static bool IsClipped(Control control)
    {
        Rectangle child = new(control.PointToScreen(Point.Empty), control.Size);
        for (Control? parent = control.Parent; parent is not null; parent = parent.Parent)
        {
            Rectangle parentArea = new(parent.PointToScreen(Point.Empty), parent.ClientSize);
            if (!parentArea.IntersectsWith(child))
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

    private static void WriteReport(IEnumerable<string> lines)
    {
        try
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FF Guardian", "Engine10", "Diagnostics");
            Directory.CreateDirectory(folder);
            File.WriteAllLines(
                Path.Combine(folder, "ui-4k-final-audit.log"),
                lines,
                Encoding.UTF8);
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
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
