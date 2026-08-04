using System.Runtime.CompilerServices;
using System.Text;

namespace FFGuardian;

/// <summary>
/// Correzione finale del viewport commerciale.
/// Mantiene l'intera pagina sotto l'header, impedisce tagli sul bordo destro
/// e distribuisce tutti i comandi senza sovrapposizioni.
/// </summary>
internal static class FinalSafeViewport22
{
    private const int SafeTopInset = 28;
    private const int SafeRightInset = 22;
    private const int SafeLeftInset = 14;
    private const int SafeBottomInset = 12;
    private const int ButtonHeight = 48;
    private const int HorizontalGap = 10;
    private const int VerticalGap = 10;

    private static readonly IReadOnlyDictionary<string, string[]> ExpectedCommands =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["SCANSIONE"] =
            [
                "SCANSIONE RAPIDA",
                "SCANSIONA FILE",
                "SCANSIONA CARTELLA",
                "METTI IN QUARANTENA",
                "ANNULLA"
            ],
            ["AUDIT"] =
            [
                "ESEGUI AUDIT COMPLETO",
                "ESPORTA RAPPORTO",
                "ANNULLA"
            ],
            ["RECUPERO"] =
            [
                "APRI ARCHIVIO QUARANTENA",
                "APRI ARCHIVIO ROLLBACK"
            ],
            ["AGGIORNAMENTI"] = ["RICARICA DATABASE FIRME"],
            ["ATTIVITÀ"] = ["APRI RAPPORTI", "PULISCI VISUALIZZAZIONE"],
            ["IMPOSTAZIONI"] = ["SALVA IMPOSTAZIONI", "RIPRISTINA PREDEFINITE"],
            ["RANSOM SHIELD"] =
            [
                "AGGIUNGI CARTELLA PROTETTA",
                "APRI REGISTRO EVENTI",
                "SALVA E RIAVVIA PROTEZIONE"
            ]
        };

    private static TabControl? _tabs;
    private static System.Windows.Forms.Timer? _startupTimer;
    private static System.Windows.Forms.Timer? _layoutTimer;
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
        _tabs = tabs;
        Application.Idle -= StartWhenReady;

        _startupTimer = new System.Windows.Forms.Timer { Interval = 2100 };
        _startupTimer.Tick += (_, _) =>
        {
            DisposeStartupTimer();
            ApplyAll();
        };
        _startupTimer.Start();

        tabs.SelectedIndexChanged += (_, _) => ScheduleLayout();
        form.Resize += (_, _) => ScheduleLayout();
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

            if (_tabs?.SelectedTab is TabPage selected)
            {
                FitPage(selected);
                WriteAudit([AuditPage(selected, inspectGeometry: true)]);
            }
        };
        _layoutTimer.Start();
    }

    private static void ApplyAll()
    {
        if (_tabs is null || _tabs.IsDisposed)
            return;

        List<string> report =
        [
            "FFGUARDIAN SAFE VIEWPORT AUDIT",
            DateTime.Now.ToString("O"),
            new string('=', 76)
        ];

        foreach (TabPage page in _tabs.TabPages)
        {
            FitPage(page);
            report.Add(AuditPage(page, ReferenceEquals(_tabs.SelectedTab, page)));
        }

        WriteAudit(report);
        StabilityCoordinator82.WriteInformationLog(
            "Viewport finale applicato: header protetto, margini sicuri e comandi completamente visibili.");
    }

    private static void FitPage(TabPage page)
    {
        if (page.IsDisposed)
            return;

        Control? root = page.Controls.Cast<Control>().FirstOrDefault(control =>
            control.Name is "CommercialPageRoot18" or "CommercialDashboard18");
        if (root is null)
            return;

        page.SuspendLayout();
        try
        {
            page.AutoScroll = false;
            page.AutoScrollMinSize = Size.Empty;
            page.Padding = root.Name == "CommercialDashboard18"
                ? new Padding(10, 14, 16, 10)
                : new Padding(SafeLeftInset, SafeTopInset, SafeRightInset, SafeBottomInset);

            root.Dock = DockStyle.Fill;
            root.Margin = Padding.Empty;
            root.MinimumSize = Size.Empty;

            if (root.Name == "CommercialDashboard18")
            {
                FitDashboard(root);
                return;
            }

            if (root is not TableLayoutPanel table || table.RowCount < 3)
                return;

            table.Padding = Padding.Empty;
            table.Margin = Padding.Empty;
            table.AutoScroll = false;

            FlowLayoutPanel? commandBar = FindControls<FlowLayoutPanel>(table)
                .FirstOrDefault(flow => FindControls<Button>(flow).Any());
            if (commandBar is null)
                return;

            List<Button> buttons = FindControls<Button>(commandBar)
                .Where(button => !button.IsDisposed)
                .ToList();

            commandBar.Dock = DockStyle.Fill;
            commandBar.FlowDirection = FlowDirection.LeftToRight;
            commandBar.WrapContents = true;
            commandBar.AutoScroll = false;
            commandBar.Padding = new Padding(0, 6, 0, 2);
            commandBar.Margin = Padding.Empty;

            int usableWidth = Math.Max(
                320,
                page.ClientSize.Width
                - page.Padding.Horizontal
                - commandBar.Padding.Horizontal
                - 16);

            int columns = ResolveColumns(usableWidth, buttons.Count);
            int widthForButtons = usableWidth - HorizontalGap * Math.Max(0, columns - 1);
            int buttonWidth = Math.Max(145, widthForButtons / Math.Max(1, columns));

            for (int index = 0; index < buttons.Count; index++)
            {
                Button button = buttons[index];
                int column = index % columns;
                int row = index / columns;
                int totalRows = Math.Max(1, (int)Math.Ceiling(buttons.Count / (double)columns));

                button.Width = buttonWidth;
                button.Height = ButtonHeight;
                button.MinimumSize = new Size(135, 44);
                button.MaximumSize = Size.Empty;
                button.Margin = new Padding(
                    0,
                    row == 0 ? 0 : VerticalGap,
                    column == columns - 1 ? 0 : HorizontalGap,
                    row == totalRows - 1 ? 0 : 2);
                button.AutoEllipsis = true;
                button.TextAlign = ContentAlignment.MiddleCenter;
                button.Visible = true;
            }

            int rows = Math.Max(1, (int)Math.Ceiling(buttons.Count / (double)Math.Max(1, columns)));
            int requiredHeight = rows * ButtonHeight
                + Math.Max(0, rows - 1) * VerticalGap
                + commandBar.Padding.Vertical
                + 10;

            int maxCommandHeight = Math.Max(82, page.ClientSize.Height * 42 / 100);
            table.RowStyles[0].SizeType = SizeType.Absolute;
            table.RowStyles[0].Height = 62;
            table.RowStyles[1].SizeType = SizeType.Absolute;
            table.RowStyles[1].Height = Math.Clamp(requiredHeight, 76, maxCommandHeight);
            table.RowStyles[2].SizeType = SizeType.Percent;
            table.RowStyles[2].Height = 100F;

            table.PerformLayout();
            commandBar.PerformLayout();
            root.BringToFront();
        }
        finally
        {
            page.ResumeLayout(true);
        }
    }

    private static int ResolveColumns(int usableWidth, int buttonCount)
    {
        int columns = usableWidth switch
        {
            >= 1180 => 4,
            >= 850 => 3,
            _ => 2
        };
        return Math.Min(columns, Math.Max(1, buttonCount));
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
            button.MinimumSize = new Size(88, 34);
            button.AutoEllipsis = true;
            button.TextAlign = ContentAlignment.MiddleCenter;
        }

        foreach (Label label in FindControls<Label>(root))
            label.AutoEllipsis = true;
    }

    private static string AuditPage(TabPage page, bool inspectGeometry)
    {
        string key = ResolvePageKey(page.Text);
        List<Button> buttons = FindControls<Button>(page)
            .Where(button => !button.IsDisposed)
            .ToList();
        List<string> defects = [];

        foreach (Button button in buttons)
        {
            if (button.Width < 120 || button.Height < 34)
                defects.Add($"dimensioni insufficienti: {Normalize(button.Text)} ({button.Width}x{button.Height})");

            if (inspectGeometry)
            {
                if (!button.Visible)
                    defects.Add($"nascosto: {Normalize(button.Text)}");
                else if (IsClipped(button))
                    defects.Add($"tagliato: {Normalize(button.Text)}");
            }
        }

        if (inspectGeometry)
        {
            List<Button> visible = buttons.Where(button => button.Visible).ToList();
            for (int first = 0; first < visible.Count; first++)
            {
                for (int second = first + 1; second < visible.Count; second++)
                {
                    if (ScreenRectangle(visible[first]).IntersectsWith(ScreenRectangle(visible[second])))
                    {
                        defects.Add(
                            $"sovrapposizione: {Normalize(visible[first].Text)} / {Normalize(visible[second].Text)}");
                    }
                }
            }
        }

        if (ExpectedCommands.TryGetValue(key, out string[]? expected))
        {
            HashSet<string> labels = buttons
                .Select(button => Normalize(button.Text))
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (string command in expected)
            {
                if (!labels.Contains(Normalize(command)))
                    defects.Add($"comando mancante: {command}");
            }
        }

        return defects.Count == 0
            ? $"[{key}] OK — tutti i comandi sono visibili, allineati e non sovrapposti."
            : $"[{key}] {string.Join("; ", defects.Distinct(StringComparer.OrdinalIgnoreCase))}";
    }

    private static bool IsClipped(Control control)
    {
        for (Control? current = control; current?.Parent is not null; current = current.Parent)
        {
            Rectangle child = ScreenRectangle(current);
            Rectangle parent = ScreenRectangle(current.Parent);
            if (!parent.Contains(child))
                return true;
            if (current.Parent is TabPage)
                break;
        }
        return false;
    }

    private static Rectangle ScreenRectangle(Control control)
    {
        Point location = control.PointToScreen(Point.Empty);
        return new Rectangle(location, control.Size);
    }

    private static string ResolvePageKey(string value)
    {
        string title = Normalize(value);
        if (title.Contains("RANSOM", StringComparison.Ordinal)) return "RANSOM SHIELD";
        if (title.Contains("IMPOST", StringComparison.Ordinal)) return "IMPOSTAZIONI";
        if (title.Contains("AGGIORN", StringComparison.Ordinal)) return "AGGIORNAMENTI";
        if (title.Contains("ATTIV", StringComparison.Ordinal)) return "ATTIVITÀ";
        if (title.Contains("RECUP", StringComparison.Ordinal)) return "RECUPERO";
        if (title.Contains("AUDIT", StringComparison.Ordinal)) return "AUDIT";
        if (title.Contains("SCANS", StringComparison.Ordinal)) return "SCANSIONE";
        return title;
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

    private static void WriteAudit(IEnumerable<string> lines)
    {
        try
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FF Guardian", "Engine10", "Diagnostics");
            Directory.CreateDirectory(folder);
            File.WriteAllLines(
                Path.Combine(folder, "ui-safe-viewport-audit.log"),
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
    }
}
