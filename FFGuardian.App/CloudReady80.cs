using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;

namespace FFGuardian;

internal static class CloudReady80
{
    private const string ButtonName = "FFG80_CLOUD_READY";
    private static readonly HashSet<Form> ConfiguredForms = new();
    private static readonly Color Bg = Color.FromArgb(5, 10, 13);
    private static readonly Color Surface = Color.FromArgb(11, 20, 24);
    private static readonly Color Surface2 = Color.FromArgb(20, 38, 43);
    private static readonly Color Neon = Color.FromArgb(142, 255, 0);
    private static readonly Color Secondary = Color.FromArgb(205, 215, 220);

    public static void Apply(object? sender, EventArgs e)
    {
        foreach (Form form in Application.OpenForms)
        {
            if (!form.Text.Contains("FF GUARDIAN", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!ConfiguredForms.Add(form))
                continue;

            AddCloudReadyButton(form);
            form.FormClosed += (_, _) => ConfiguredForms.Remove(form);
        }
    }

    private static void AddCloudReadyButton(Form owner)
    {
        FlowLayoutPanel? menu = FindControls<FlowLayoutPanel>(owner)
            .FirstOrDefault(flow => flow.Controls.OfType<Button>()
                .Any(button => button.Text.Contains("Dashboard", StringComparison.OrdinalIgnoreCase)));

        if (menu is null || menu.Controls.Find(ButtonName, false).Length > 0)
            return;

        Button button = new()
        {
            Name = ButtonName,
            Text = "☁   Cloud Ready 8.0",
            Width = Math.Max(248, menu.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 16),
            Height = 47,
            Margin = new Padding(0, 3, 0, 3),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(14, 0, 0, 0),
            BackColor = Surface2,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = Neon;
        button.FlatAppearance.BorderSize = 1;
        button.MouseEnter += (_, _) => button.BackColor = Color.FromArgb(35, 68, 25);
        button.MouseLeave += (_, _) => button.BackColor = Surface2;
        button.Click += (_, _) => ShowCloudCenter(owner);

        menu.Controls.Add(button);
        menu.Controls.SetChildIndex(button, Math.Max(0, menu.Controls.Count - 4));
    }

    private static void ShowCloudCenter(Form owner)
    {
        using Form center = new()
        {
            Text = "FF GUARDIAN 8.0 — Cloud Ready Edition",
            Icon = owner.Icon,
            StartPosition = FormStartPosition.CenterParent,
            MinimumSize = new Size(1040, 700),
            Size = new Size(1180, 780),
            BackColor = Bg,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F)
        };

        TabControl tabs = new()
        {
            Dock = DockStyle.Fill,
            Appearance = TabAppearance.FlatButtons,
            ItemSize = new Size(190, 42),
            SizeMode = TabSizeMode.Fixed,
            Padding = new Point(16, 8)
        };
        tabs.TabPages.Add(BuildStatusPage());
        tabs.TabPages.Add(BuildIntegrityPage(center));
        tabs.TabPages.Add(BuildExportPage(center));
        tabs.TabPages.Add(BuildOnlinePage());
        center.Controls.Add(tabs);
        center.ShowDialog(owner);
    }

    private static TabPage BuildStatusPage()
    {
        TabPage page = NewPage("Stato Cloud Ready");
        AutonomousSnapshot snapshot = AutonomousSecurityEngine.GetSnapshot();
        bool online = NetworkInterface.GetIsNetworkAvailable();

        TableLayoutPanel layout = Grid2x2();
        layout.Controls.Add(InfoCard("CONNETTIVITÀ", online ? "ONLINE\nConnessione disponibile per aggiornamenti e servizi futuri." : "OFFLINE\nLe protezioni locali continuano a funzionare.", online ? Neon : Color.Orange), 0, 0);
        layout.Controls.Add(InfoCard("PROTEZIONE LOCALE", $"{snapshot.Score}/100 — {snapshot.Status}\nProfilo: {ProfileName(snapshot.Profile)}", snapshot.Score >= 90 ? Neon : Color.Orange), 1, 0);
        layout.Controls.Add(InfoCard("PRIVACY", "Modalità locale attiva.\nNessun file viene inviato online automaticamente."), 0, 1);
        layout.Controls.Add(InfoCard("BASE CLOUD", "Pronta per aggiornamenti firmati, reputazione file e servizi remoti autorizzati.\nNessun servizio cloud proprietario è ancora attivo."), 1, 1);
        page.Controls.Add(layout);
        return page;
    }

    private static TabPage BuildIntegrityPage(Form owner)
    {
        TabPage page = NewPage("Integrità");
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(22) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        Label result = new() { Dock = DockStyle.Fill, ForeColor = Secondary, Font = new Font("Segoe UI", 11F), Padding = new Padding(16), Text = "Calcola l'impronta SHA-256 dell'eseguibile installato per verificarne l'identità." };
        Button verify = ActionButton("CALCOLA SHA-256");
        verify.Click += (_, _) =>
        {
            try
            {
                string path = Environment.ProcessPath ?? Application.ExecutablePath;
                using SHA256 sha = SHA256.Create();
                using FileStream stream = File.OpenRead(path);
                string hash = Convert.ToHexString(sha.ComputeHash(stream));
                result.Text = $"File: {Path.GetFileName(path)}\n\nSHA-256:\n{hash}";
            }
            catch (Exception ex)
            {
                result.Text = "Verifica non riuscita: " + ex.Message;
            }
        };
        layout.Controls.Add(InfoCard("CONTROLLO INTEGRITÀ", "Verifica locale dell'eseguibile FF GUARDIAN. Il risultato non viene inviato online."), 0, 0);
        layout.Controls.Add(verify, 0, 1);
        layout.Controls.Add(result, 0, 2);
        page.Controls.Add(layout);
        return page;
    }

    private static TabPage BuildExportPage(Form owner)
    {
        TabPage page = NewPage("Esportazione");
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(22) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Label status = new() { Dock = DockStyle.Fill, ForeColor = Secondary, Padding = new Padding(16), Text = "Il rapporto contiene soltanto dati diagnostici locali e non include password o contenuti personali." };
        Button export = ActionButton("ESPORTA RAPPORTO DIAGNOSTICO");
        export.Click += (_, _) => ExportReport(owner, status);
        layout.Controls.Add(InfoCard("RAPPORTO ASSISTENZA", "Esporta versione, sistema operativo, punteggio, profilo, controlli recenti e stato connettività."), 0, 0);
        layout.Controls.Add(export, 0, 1);
        layout.Controls.Add(status, 0, 2);
        page.Controls.Add(layout);
        return page;
    }

    private static TabPage BuildOnlinePage()
    {
        TabPage page = NewPage("Aggiornamenti");
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(22) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Label note = new() { Dock = DockStyle.Fill, ForeColor = Secondary, Padding = new Padding(16), Text = "Versione installata: 8.0\n\nLa pagina GitHub consente di controllare manualmente le build ufficiali. L'aggiornamento automatico firmato verrà attivato solo con un canale di distribuzione stabile." };
        Button open = ActionButton("APRI AGGIORNAMENTI GITHUB");
        open.Click += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://github.com/elcosiracusa-cmyk/FF-GUARDIAN-5.0/actions") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "FF GUARDIAN", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        layout.Controls.Add(InfoCard("CANALE AGGIORNAMENTI", "Controllo manuale e trasparente delle build ufficiali FF GUARDIAN."), 0, 0);
        layout.Controls.Add(open, 0, 1);
        layout.Controls.Add(note, 0, 2);
        page.Controls.Add(layout);
        return page;
    }

    private static void ExportReport(Form owner, Label status)
    {
        try
        {
            AutonomousSnapshot snapshot = AutonomousSecurityEngine.GetSnapshot();
            using SaveFileDialog dialog = new()
            {
                Title = "Salva rapporto FF GUARDIAN",
                Filter = "File di testo (*.txt)|*.txt",
                FileName = $"FFGuardian-8.0-Rapporto-{DateTime.Now:yyyyMMdd-HHmm}.txt"
            };
            if (dialog.ShowDialog(owner) != DialogResult.OK) return;

            StringBuilder report = new();
            report.AppendLine("FF GUARDIAN 8.0 — Cloud Ready Edition");
            report.AppendLine("EL.CO di Francesco Fazzina");
            report.AppendLine(new string('-', 60));
            report.AppendLine($"Data: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            report.AppendLine($"Computer: {Environment.MachineName}");
            report.AppendLine($"Utente: {Environment.UserName}");
            report.AppendLine($"Windows: {Environment.OSVersion}");
            report.AppendLine($"Connettività: {(NetworkInterface.GetIsNetworkAvailable() ? "Online" : "Offline")}");
            report.AppendLine($"Punteggio: {snapshot.Score}/100");
            report.AppendLine($"Stato: {snapshot.Status}");
            report.AppendLine($"Profilo: {ProfileName(snapshot.Profile)}");
            report.AppendLine($"Ultimo controllo: {FormatDate(snapshot.LastProtectionCheck)}");
            report.AppendLine($"Ultimo aggiornamento firme: {FormatDate(snapshot.LastSignatureUpdate)}");
            report.AppendLine($"Ultima scansione rapida: {FormatDate(snapshot.LastQuickScan)}");
            report.AppendLine($"Ultima scansione completa: {FormatDate(snapshot.LastFullScan)}");
            report.AppendLine($"File Download controllati: {snapshot.DownloadFilesChecked}");
            report.AppendLine($"Ultimo errore: {snapshot.LastError ?? "Nessuno"}");
            File.WriteAllText(dialog.FileName, report.ToString(), Encoding.UTF8);
            status.Text = "Rapporto esportato correttamente:\n" + dialog.FileName;
        }
        catch (Exception ex)
        {
            status.Text = "Esportazione non riuscita: " + ex.Message;
        }
    }

    private static TabPage NewPage(string text) => new() { Text = text, BackColor = Bg, ForeColor = Color.White, Padding = new Padding(8) };

    private static TableLayoutPanel Grid2x2()
    {
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(18) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        return layout;
    }

    private static Panel InfoCard(string title, string body, Color? accent = null)
    {
        Panel panel = new() { Dock = DockStyle.Fill, BackColor = Surface, Margin = new Padding(10), Padding = new Padding(18) };
        panel.Paint += (_, e) =>
        {
            using Pen pen = new(Color.FromArgb(50, 90, 100), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, panel.Width - 1), Math.Max(0, panel.Height - 1));
        };
        panel.Controls.Add(new Label { Dock = DockStyle.Fill, Text = body, ForeColor = accent ?? Secondary, Font = new Font("Segoe UI", 11F), Padding = new Padding(0, 10, 0, 0) });
        panel.Controls.Add(new Label { Dock = DockStyle.Top, Height = 42, Text = title, ForeColor = Color.White, Font = new Font("Segoe UI", 12F, FontStyle.Bold) });
        return panel;
    }

    private static Button ActionButton(string text)
    {
        Button button = new() { Dock = DockStyle.Fill, Text = text, BackColor = Surface2, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10F, FontStyle.Bold), Cursor = Cursors.Hand, Margin = new Padding(10) };
        button.FlatAppearance.BorderColor = Neon;
        button.FlatAppearance.BorderSize = 1;
        button.MouseEnter += (_, _) => button.BackColor = Color.FromArgb(35, 68, 25);
        button.MouseLeave += (_, _) => button.BackColor = Surface2;
        return button;
    }

    private static string ProfileName(ProtectionProfile profile) => profile switch
    {
        ProtectionProfile.Casa => "Casa",
        ProtectionProfile.Ufficio => "Ufficio",
        _ => "Massima protezione"
    };

    private static string FormatDate(DateTime? value) => value?.ToString("dd/MM/yyyy HH:mm") ?? "Non disponibile";

    private static IEnumerable<T> FindControls<T>(Control parent) where T : Control
    {
        foreach (Control control in parent.Controls)
        {
            if (control is T match) yield return match;
            if (control.HasChildren)
                foreach (T child in FindControls<T>(control))
                    yield return child;
        }
    }
}
