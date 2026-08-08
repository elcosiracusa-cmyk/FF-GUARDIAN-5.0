using System.Runtime.CompilerServices;
using System.Text.Json;

namespace FFGuardian;

internal static class InternalProtectionUi30
{
    private static bool _started;
    private static Label? _summary;

    [ModuleInitializer]
    internal static void Initialize() => Application.Idle += StartWhenReady;

    private static void StartWhenReady(object? sender, EventArgs e)
    {
        if (_started) return;
        Form? form = Application.OpenForms.Cast<Form>().FirstOrDefault(x => x.IsHandleCreated && !x.IsDisposed);
        if (form is null) return;
        TabControl? tabs = FindControls<TabControl>(form).OrderByDescending(x => x.TabCount).FirstOrDefault();
        if (tabs is null) return;
        TabPage? settings = tabs.TabPages.Cast<TabPage>().FirstOrDefault(x =>
            x.Text.Contains("IMPOST", StringComparison.OrdinalIgnoreCase) ||
            x.Text.Contains("SETTINGS", StringComparison.OrdinalIgnoreCase));
        if (settings is null) return;
        _started = true; Application.Idle -= StartWhenReady;
        Install(settings, form);
    }

    private static void Install(TabPage page, Form owner)
    {
        if (FindControls<Control>(page).Any(x => x.Name == "ProtectedInternalExclusions30")) return;
        GroupBox box = new()
        {
            Name = "ProtectedInternalExclusions30", Text = "Esclusioni interne protette",
            Dock = DockStyle.Top, Height = 330, Padding = new Padding(16),
            ForeColor = Color.White, BackColor = Color.FromArgb(18, 23, 27)
        };
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));

        FFGuardianInternalPathLayout paths = FFGuardianScanExclusionService.Current.Layout;
        TextBox protectedPaths = new()
        {
            Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
            BackColor = Color.FromArgb(10, 15, 18), ForeColor = Color.Gainsboro,
            Text = string.Join(Environment.NewLine, new[]
            {
                "Installazione: " + paths.InstallationDirectory,
                "Motori: " + paths.EngineDirectory,
                "Firme: " + paths.DatabaseDirectory,
                "Regole: " + paths.RulesDirectory,
                "Quarantena: " + paths.QuarantineDirectory,
                "Log: " + paths.LogsDirectory,
                "Temp: " + paths.TempDirectory,
                "Aggiornamenti: " + paths.UpdatesDirectory,
                "Backup: " + paths.BackupDirectory,
                "Cache: " + paths.CacheDirectory,
                "Report: " + paths.ReportsDirectory
            })
        };
        Button verify = CreateButton("VERIFICA INTEGRITÀ FFGUARDIAN");
        Button export = CreateButton("ESPORTA REPORT INTEGRITÀ");
        _summary = new Label { Dock = DockStyle.Fill, ForeColor = Color.FromArgb(157, 255, 0),
            TextAlign = ContentAlignment.MiddleLeft, Text = "Stato integrità FFGuardian: NON VERIFICATO" };
        verify.Click += async (_, _) => await VerifyAsync(owner, exportOnly: false);
        export.Click += async (_, _) => await VerifyAsync(owner, exportOnly: true);
        layout.Controls.Add(protectedPaths, 0, 0);
        layout.Controls.Add(verify, 0, 1);
        layout.Controls.Add(export, 0, 2);
        layout.Controls.Add(_summary, 0, 3);
        box.Controls.Add(layout);
        page.Controls.Add(box);
        box.BringToFront();
    }

    private static Button CreateButton(string text) => new()
    {
        Dock = DockStyle.Fill, Text = text, FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(24, 31, 36), ForeColor = Color.FromArgb(157, 255, 0),
        Font = new Font("Segoe UI", 9F, FontStyle.Bold), UseVisualStyleBackColor = false
    };

    private static async Task VerifyAsync(Form owner, bool exportOnly)
    {
        FFGuardianIntegrityService service = new();
        IntegrityReport report = await service.VerifyAsync(CancellationToken.None);
        if (_summary is not null)
            _summary.Text = $"File ufficiali verificati: {report.Intact} | Modificati: {report.Modified} | " +
                $"Mancanti: {report.Missing} | Sconosciuti: {report.Unknown} | Stato: {report.OverallState}";
        Directory.CreateDirectory(FFGuardianScanExclusionService.Current.Layout.ReportsDirectory);
        string output = Path.Combine(FFGuardianScanExclusionService.Current.Layout.ReportsDirectory,
            $"integrity-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(report,
            new JsonSerializerOptions { WriteIndented = true }));
        if (!exportOnly && report.Modified + report.Missing + report.Unknown > 0)
            MessageBox.Show(owner, "Il controllo integrità ha trovato anomalie. Nessun file è stato eliminato. " +
                "Consulta il report e valuta riparazione o reinstallazione.", "FFGuardian — Integrità",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        else
            MessageBox.Show(owner, "Report integrità salvato in:\n" + output, "FFGuardian — Integrità",
                MessageBoxButtons.OK, report.OverallState == "INTEGRO" ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    private static IEnumerable<T> FindControls<T>(Control root) where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T match) yield return match;
            foreach (T nested in FindControls<T>(child)) yield return nested;
        }
    }
}
