using System.Diagnostics;
using System.Reflection;

namespace FFGuardian;

internal static class DefinitiveReports832
{
    private const string PanelName = "FFG832_DEFINITIVE_REPORTS";
    private static readonly SemaphoreSlim ReportGate = new(1, 1);
    private static readonly Color Surface = Color.FromArgb(11, 20, 24);
    private static readonly Color Surface2 = Color.FromArgb(20, 38, 43);
    private static readonly Color Neon = Color.FromArgb(142, 255, 0);

    public static void Apply(object? sender, EventArgs e)
    {
        foreach (Form form in Application.OpenForms.Cast<Form>().Where(f => !f.IsDisposed))
        {
            if (!form.Text.Contains("FF GUARDIAN", StringComparison.OrdinalIgnoreCase))
                continue;

            Button? reportsButton = Descendants(form).OfType<Button>()
                .FirstOrDefault(b => b.Text.Contains("Rapporti", StringComparison.OrdinalIgnoreCase));
            if (reportsButton is not null && reportsButton.Tag?.ToString() != "FFG832_REPORT_HOOK")
            {
                reportsButton.Tag = "FFG832_REPORT_HOOK";
                reportsButton.Click += (_, _) => form.BeginInvoke(() => EnsureReportsPanel(form));
            }

            EnsureReportsPanel(form);
        }
    }

    private static void EnsureReportsPanel(Form form)
    {
        Label? title = Descendants(form).OfType<Label>()
            .FirstOrDefault(l => string.Equals(l.Text.Trim(), "Rapporti", StringComparison.OrdinalIgnoreCase));
        if (title is null)
            return;

        Panel? pageBody = title.Parent?.Parent?.Controls.OfType<Panel>()
            .FirstOrDefault(p => p.Dock == DockStyle.Fill);
        if (pageBody is null || Descendants(pageBody).Any(c => c.Name == PanelName))
            return;

        Panel card = new()
        {
            Name = PanelName,
            Dock = DockStyle.Bottom,
            Height = 190,
            BackColor = Surface,
            Padding = new Padding(18),
            Margin = new Padding(8)
        };

        Label heading = new()
        {
            Dock = DockStyle.Top,
            Height = 36,
            Text = "CENTRO RAPPORTI DEFINITIVO 8.3.2",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold)
        };
        Label status = new()
        {
            Dock = DockStyle.Fill,
            Text = "Crea un rapporto completo e verificato per assistenza e diagnostica.",
            ForeColor = Color.Gainsboro,
            Font = new Font("Segoe UI", 10F),
            Padding = new Padding(0, 8, 0, 0)
        };
        FlowLayoutPanel actions = new()
        {
            Dock = DockStyle.Bottom,
            Height = 58,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        Button generate = ActionButton("GENERA RAPPORTO COMPLETO");
        Button openFolder = ActionButton("APRI CARTELLA RAPPORTI");
        generate.Click += async (_, _) =>
        {
            generate.Enabled = false;
            status.Text = "Creazione e verifica del rapporto in corso…";
            try
            {
                string path = await GenerateAsync();
                status.ForeColor = Neon;
                status.Text = $"✓ Rapporto creato e verificato: {Path.GetFileName(path)}";
                MessageBox.Show(form, $"Rapporto creato correttamente:\n{path}", "FF GUARDIAN 8.3.2", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                StabilityCoordinator82.WriteStabilityLog(ex);
                status.ForeColor = Color.OrangeRed;
                status.Text = "Rapporto non creato. Controlla il registro stabilità.";
            }
            finally
            {
                generate.Enabled = true;
            }
        };
        openFolder.Click += (_, _) => OpenReportsFolder();
        actions.Controls.Add(generate);
        actions.Controls.Add(openFolder);
        card.Controls.Add(status);
        card.Controls.Add(actions);
        card.Controls.Add(heading);
        pageBody.Controls.Add(card);
        card.BringToFront();
    }

    private static async Task<string> GenerateAsync()
    {
        await ReportGate.WaitAsync();
        try
        {
            string folder = ReportsFolder();
            Directory.CreateDirectory(folder);
            CoreHealthSnapshot health = await CoreHealth83.CheckAsync(TimeSpan.FromSeconds(8));
            string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "8.3.2.0";
            string logsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FF Guardian", "Logs");
            string recentLogs = Directory.Exists(logsFolder)
                ? string.Join(Environment.NewLine, Directory.GetFiles(logsFolder).OrderByDescending(File.GetLastWriteTimeUtc).Take(10).Select(Path.GetFileName))
                : "Nessun registro disponibile";

            string path = Path.Combine(folder, $"FFGuardian-Rapporto-8.3.2-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            string content = $"FF GUARDIAN 8.3.2 - RAPPORTO DEFINITIVO\r\n" +
                             $"Data: {DateTime.Now:dd/MM/yyyy HH:mm:ss}\r\n" +
                             $"Versione assembly: {version}\r\n" +
                             $"Computer: {Environment.MachineName}\r\n" +
                             $"Utente: {Environment.UserName}\r\n" +
                             $"Windows: {Environment.OSVersion}\r\n" +
                             $".NET: {Environment.Version}\r\n\r\n" +
                             $"STATO PROTEZIONE: {health.State}\r\n" +
                             $"Defender Service: {(health.DefenderServiceRunning ? "ATTIVO" : "NON ATTIVO")}\r\n" +
                             $"Firewall Service: {(health.FirewallServiceRunning ? "ATTIVO" : "NON ATTIVO")}\r\n" +
                             $"Rete: {(health.NetworkAvailable ? "DISPONIBILE" : "NON DISPONIBILE")}\r\n" +
                             $"Spazio libero sistema: {health.FreeSystemDriveBytes / 1024d / 1024d / 1024d:0.0} GB\r\n" +
                             $"Sintesi: {health.Summary}\r\n\r\n" +
                             $"REGISTRI RECENTI:\r\n{recentLogs}\r\n";

            string temp = path + ".tmp";
            await File.WriteAllTextAsync(temp, content);
            if (!File.Exists(temp) || new FileInfo(temp).Length < 200)
                throw new IOException("Il rapporto temporaneo non è valido.");
            File.Move(temp, path, true);
            if (!File.Exists(path) || new FileInfo(path).Length < 200)
                throw new IOException("La verifica finale del rapporto non è riuscita.");
            return path;
        }
        finally
        {
            ReportGate.Release();
        }
    }

    private static Button ActionButton(string text)
    {
        Button button = new()
        {
            Text = text,
            Width = 270,
            Height = 46,
            Margin = new Padding(0, 4, 10, 4),
            BackColor = Surface2,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = Neon;
        return button;
    }

    private static void OpenReportsFolder()
    {
        string folder = ReportsFolder();
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
    }

    private static string ReportsFolder() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FF Guardian Reports");

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control nested in Descendants(child))
                yield return nested;
        }
    }
}
