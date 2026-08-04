using System.Runtime.CompilerServices;
using System.Text;

namespace FFGuardian;

/// <summary>
/// Rifinitura finale non distruttiva della UI commerciale.
/// Non ricrea pagine e non sostituisce comandi: dimensiona soltanto i controlli
/// prodotti da CommercialPages18 e registra eventuali anomalie di layout.
/// </summary>
internal static class FinalLayoutGuard21
{
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

    private static System.Windows.Forms.Timer? _startupTimer;
    private static System.Windows.Forms.Timer? _resizeTimer;
    private static TabControl? _tabs;
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

        _startupTimer = new System.Windows.Forms.Timer { Interval = 1400 };
        _startupTimer.Tick += (_, _) =>
        {
            DisposeStartupTimer();
            ApplyAndAuditAll();
        };
        _startupTimer.Start();

        tabs.SelectedIndexChanged += (_, _) => ScheduleSelectedPage();
        form.Resize += (_, _) => ScheduleSelectedPage();
        form.FormClosed += (_, _) => DisposeTimers();
    }

    private static void ScheduleSelectedPage()
    {
        _resizeTimer?.Stop();
        _resizeTimer?.Dispose();
        _resizeTimer = new System.Windows.Forms.Timer { Interval = 120 };
        _resizeTimer.Tick += (_, _) =>
        {
            _resizeTimer?.Stop();
            _resizeTimer?.Dispose();
            _resizeTimer = null;
            if (_tabs?.SelectedTab is TabPage selected)
            {
                FitPage(selected);
                WriteAudit([AuditPage(selected, inspectScreenGeometry: true)]);
            }
        };
        _resizeTimer.Start();
    }

    private static void ApplyAndAuditAll()
    {
        if (_tabs is null || _tabs.IsDisposed)
            return;

        List<string> report =
        [
            "FFGUARDIAN FINAL UI LAYOUT AUDIT",
            DateTime.Now.ToString("O"),
            new string('=', 76)
        ];

        foreach (TabPage page in _tabs.TabPages)
        {
            FitPage(page);
            bool selected = ReferenceEquals(_tabs.SelectedTab, page);
            report.Add(AuditPage(page, inspectScreenGeometry: selected));
        }

        WriteAudit(report);
        StabilityCoordinator82.WriteInformationLog(
            "Controllo finale UI completato: dimensionamento responsive e audit dei comandi applicati.");
    }

    private static void FitPage(TabPage page)
    {
        if (page.IsDisposed)
            return;

        page.AutoScroll = false;
        page.AutoScrollMinSize = Size.Empty;

        Control? root = page.Controls.Cast<Control>().FirstOrDefault(control =>
            control.Name is "CommercialPageRoot18" or "CommercialDashboard18");
        if (root is null)
            return;

        root.Dock = DockStyle.Fill;
        root.Margin = Padding.Empty;

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

        List<Button> buttons = FindControls<Button>(commands).ToList();
        int availableWidth = Math.Max(360, commands.ClientSize.Width - commands.Padding.Horizontal);
        int columns = availableWidth >= 1040 ? 4 : availableWidth >= 760 ? 3 : 2;
        columns = Math.Min(columns, Math.Max(1, buttons.Count));
        int horizontalGap = 10;
        int buttonWidth = Math.Max(150,
            (availableWidth - horizontalGap * Math.Max(0, columns - 1)) / Math.Max(1, columns));

        commands.FlowDirection = FlowDirection.LeftToRight;
        commands.WrapContents = true;
        commands.AutoScroll = false;
        commands.Padding = new Padding(0, 6, 0, 4);

        foreach (Button button in buttons)
        {
            button.Width = buttonWidth;
            button.Height = 48;
            button.MinimumSize = new Size(140, 44);
            button.MaximumSize = Size.Empty;
            button.Margin = new Padding(0, 0, horizontalGap, 10);
            button.AutoEllipsis = true;
            button.TextAlign = ContentAlignment.MiddleCenter;
        }

        int rows = Math.Max(1, (int)Math.Ceiling(buttons.Count / (double)Math.Max(1, columns)));
        int desiredHeight = rows * 58 + 12;
        int maximumHeight = Math.Max(78, page.ClientSize.Height / 3);
        table.RowStyles[1].SizeType = SizeType.Absolute;
        table.RowStyles[1].Height = Math.Clamp(desiredHeight, 78, maximumHeight);
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
            button.MinimumSize = new Size(90, 34);
            button.AutoEllipsis = true;
            button.TextAlign = ContentAlignment.MiddleCenter;
        }

        foreach (Label label in FindControls<Label>(root))
            label.AutoEllipsis = true;
    }

    private static string AuditPage(TabPage page, bool inspectScreenGeometry)
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

            if (inspectScreenGeometry)
            {
                if (!button.Visible)
                    defects.Add($"nascosto: {Normalize(button.Text)}");
                else if (IsClipped(button))
                    defects.Add($"tagliato: {Normalize(button.Text)}");
            }
        }

        if (inspectScreenGeometry)
        {
            List<Button> visibleButtons = buttons.Where(button => button.Visible).ToList();
            for (int first = 0; first < visibleButtons.Count; first++)
            {
                for (int second = first + 1; second < visibleButtons.Count; second++)
                {
                    if (ScreenRectangle(visibleButtons[first]).IntersectsWith(ScreenRectangle(visibleButtons[second])))
                    {
                        defects.Add(
                            $"sovrapposizione: {Normalize(visibleButtons[first].Text)} / {Normalize(visibleButtons[second].Text)}");
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
            foreach (string expectedCommand in expected)
            {
                if (!labels.Contains(Normalize(expectedCommand)))
                    defects.Add($"comando mancante: {expectedCommand}");
            }
        }

        return defects.Count == 0
            ? $"[{key}] OK — nessun comando mancante, tagliato o sovrapposto."
            : $"[{key}] {string.Join("; ", defects.Distinct(StringComparer.OrdinalIgnoreCase))}";
    }

    private static bool IsClipped(Control control)
    {
        for (Control? current = control; current?.Parent is not null; current = current.Parent)
        {
            Rectangle childOnScreen = ScreenRectangle(current);
            Rectangle parentOnScreen = ScreenRectangle(current.Parent);
            if (!parentOnScreen.Contains(childOnScreen))
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
            string path = Path.Combine(folder, "ui-layout-audit.log");
            File.WriteAllLines(path, lines, Encoding.UTF8);
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
        _resizeTimer?.Stop();
        _resizeTimer?.Dispose();
        _resizeTimer = null;
        _tabs = null;
    }
}
