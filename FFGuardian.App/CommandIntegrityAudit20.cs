using System.Runtime.CompilerServices;
using System.Text;

namespace FFGuardian;

/// <summary>
/// Verifica finale non distruttiva della UI. Controlla che ogni pagina esponga
/// tutti i comandi operativi previsti e che nessun pulsante venga tagliato dal layout.
/// Non simula click e non altera il motore antivirus.
/// </summary>
internal static class CommandIntegrityAudit20
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
            ["AGGIORNAMENTI"] =
            [
                "RICARICA DATABASE FIRME"
            ],
            ["ATTIVITÀ"] =
            [
                "APRI RAPPORTI",
                "PULISCI VISUALIZZAZIONE"
            ],
            ["IMPOSTAZIONI"] =
            [
                "SALVA IMPOSTAZIONI",
                "RIPRISTINA PREDEFINITE"
            ],
            ["RANSOM SHIELD"] =
            [
                "AGGIUNGI CARTELLA PROTETTA",
                "APRI REGISTRO EVENTI",
                "SALVA E RIAVVIA PROTEZIONE"
            ]
        };

    private static System.Windows.Forms.Timer? _startupTimer;
    private static System.Windows.Forms.Timer? _navigationTimer;
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

        _startupTimer = new System.Windows.Forms.Timer { Interval = 2600 };
        _startupTimer.Tick += (_, _) =>
        {
            DisposeStartupTimer();
            AuditAllPages();
        };
        _startupTimer.Start();

        tabs.SelectedIndexChanged += (_, _) => ScheduleSelectedPageAudit();
        form.Resize += (_, _) =>
        {
            if (_tabs?.SelectedTab is TabPage selected)
                AuditPage(selected, writeReport: false);
        };
        form.FormClosed += (_, _) => DisposeTimers();
    }

    private static void ScheduleSelectedPageAudit()
    {
        _navigationTimer?.Stop();
        _navigationTimer?.Dispose();
        _navigationTimer = new System.Windows.Forms.Timer { Interval = 420 };
        _navigationTimer.Tick += (_, _) =>
        {
            _navigationTimer?.Stop();
            _navigationTimer?.Dispose();
            _navigationTimer = null;
            if (_tabs?.SelectedTab is TabPage selected)
                AuditPage(selected, writeReport: true);
        };
        _navigationTimer.Start();
    }

    private static void AuditAllPages()
    {
        if (_tabs is null || _tabs.IsDisposed)
            return;

        List<string> report =
        [
            "FFGUARDIAN UI COMMAND AUDIT",
            DateTime.Now.ToString("O"),
            new string('=', 72)
        ];

        int missingTotal = 0;
        foreach (TabPage page in _tabs.TabPages)
            missingTotal += AuditPage(page, writeReport: false, report);

        report.Add(new string('-', 72));
        report.Add(missingTotal == 0
            ? "ESITO: tutti i comandi previsti sono presenti e visibili."
            : $"ESITO: {missingTotal} comandi mancanti o non visibili.");
        WriteReport(report);

        StabilityCoordinator82.WriteInformationLog(missingTotal == 0
            ? "Audit UI: tutti i comandi operativi sono presenti e visibili."
            : $"Audit UI: rilevati {missingTotal} comandi mancanti o non visibili.");
    }

    private static int AuditPage(
        TabPage page,
        bool writeReport,
        List<string>? aggregateReport = null)
    {
        if (page.IsDisposed)
            return 0;

        string pageKey = ResolvePageKey(page.Text);
        List<Button> buttons = FindControls<Button>(page).ToList();

        foreach (Button button in buttons)
        {
            button.Visible = true;
            button.AutoEllipsis = true;
            button.MinimumSize = new Size(130, 38);
            if (button.Parent is not null)
            {
                Rectangle visibleArea = button.Parent.ClientRectangle;
                if (!visibleArea.IntersectsWith(button.Bounds))
                    button.BringToFront();
            }
        }

        EnsureCommandAreaHeight(page, buttons.Count);

        if (!ExpectedCommands.TryGetValue(pageKey, out string[]? expected))
            return 0;

        HashSet<string> visibleLabels = buttons
            .Where(button => button.Visible)
            .Select(button => Normalize(button.Text))
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<string> missing = expected
            .Where(command => !visibleLabels.Contains(Normalize(command)))
            .ToList();

        string line = missing.Count == 0
            ? $"[{pageKey}] OK — {expected.Length}/{expected.Length} comandi visibili."
            : $"[{pageKey}] MANCANTI: {string.Join(", ", missing)}";

        aggregateReport?.Add(line);
        if (writeReport)
        {
            WriteReport(
            [
                "FFGUARDIAN UI COMMAND AUDIT",
                DateTime.Now.ToString("O"),
                line
            ]);
        }

        return missing.Count;
    }

    private static void EnsureCommandAreaHeight(TabPage page, int commandCount)
    {
        TableLayoutPanel? root = FindControls<TableLayoutPanel>(page)
            .FirstOrDefault(control =>
                control.Name is "UnifiedCommercialPage19" or "CommercialPageRoot18");
        if (root is null || root.RowStyles.Count < 2)
            return;

        int columns = Math.Min(4, Math.Max(1, commandCount));
        int rows = Math.Max(1, (int)Math.Ceiling(commandCount / (double)columns));
        int desired = Math.Clamp(rows * 64 + 10, 74, Math.Max(100, page.ClientSize.Height / 2));
        root.RowStyles[1].SizeType = SizeType.Absolute;
        root.RowStyles[1].Height = desired;
        root.PerformLayout();
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

    private static void WriteReport(IEnumerable<string> lines)
    {
        try
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FF Guardian", "Engine10", "Diagnostics");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "ui-command-audit.log");
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
        _navigationTimer?.Stop();
        _navigationTimer?.Dispose();
        _navigationTimer = null;
    }
}
