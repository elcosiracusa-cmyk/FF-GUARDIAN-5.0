using System.Diagnostics;

namespace FFGuardian;

internal sealed class ProfessionalMainForm91 : Form
{
    private const string VersionText = "9.1";
    private const string SupportEmail = "alsafe127.00@gmail.com";

    private static readonly Color Bg = Color.FromArgb(3, 8, 12);
    private static readonly Color Side = Color.FromArgb(5, 12, 17);
    private static readonly Color Surface = Color.FromArgb(12, 22, 28);
    private static readonly Color Surface2 = Color.FromArgb(8, 17, 22);
    private static readonly Color Border = Color.FromArgb(58, 76, 84);
    private static readonly Color Neon = Color.FromArgb(160, 255, 0);
    private static readonly Color Muted = Color.FromArgb(198, 205, 210);

    private readonly DefenderService _defender = new();
    private readonly Panel _pageHost = new() { Dock = DockStyle.Fill, BackColor = Bg };
    private readonly Label _status = new()
    {
        Dock = DockStyle.Bottom,
        Height = 34,
        BackColor = Side,
        ForeColor = Muted,
        Padding = new Padding(18, 0, 0, 0),
        TextAlign = ContentAlignment.MiddleLeft,
        Text = "FF GUARDIAN pronto."
    };
    private readonly List<Button> _navigation = [];
    private Button? _selected;
    private bool _busy;

    public ProfessionalMainForm91()
    {
        Text = "FF GUARDIAN 9.1 — Definitive Professional Edition by EL.CO";
        Icon = DobermannIconFactory.CreateIcon();
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1180, 760);
        BackColor = Bg;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10F);
        AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true;

        Controls.Add(_pageHost);
        Controls.Add(BuildSidebar());
        Controls.Add(BuildTopBar());
        Controls.Add(_status);

        Shown += async (_, _) => await NavigateAsync(_navigation[0], ShowDashboardAsync);
    }

    private Control BuildTopBar()
    {
        Panel bar = new() { Dock = DockStyle.Top, Height = 74, BackColor = Side, Padding = new Padding(24, 12, 20, 12) };
        Label brand = new()
        {
            Dock = DockStyle.Left,
            Width = 620,
            Text = "FF GUARDIAN  |  PERSONAL SECURITY",
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        Button refresh = UiButton("AGGIORNA STATO", 180);
        refresh.Dock = DockStyle.Right;
        refresh.BackColor = Color.FromArgb(38, 92, 0);
        refresh.Click += async (_, _) => await RunSafeAsync(ShowDashboardAsync);
        Button support = UiButton("ASSISTENZA", 150);
        support.Dock = DockStyle.Right;
        support.Click += (_, _) => OpenSupportEmail();
        bar.Controls.Add(refresh);
        bar.Controls.Add(support);
        bar.Controls.Add(brand);
        return bar;
    }

    private Control BuildSidebar()
    {
        Panel side = new() { Dock = DockStyle.Left, Width = 270, BackColor = Side };
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(12) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));

        Panel identity = new() { Dock = DockStyle.Fill, BackColor = Side };
        identity.Controls.Add(new Label
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            Text = "FF GUARDIAN\nPERSONAL SECURITY • EL.CO",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold)
        });
        identity.Controls.Add(new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = DobermannIconFactory.CreateBitmap(230)
        });

        FlowLayoutPanel menu = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Side,
            Padding = new Padding(0, 2, 0, 2)
        };
        AddNav(menu, "⌂   Dashboard", ShowDashboardAsync);
        AddNav(menu, "⌕   Scansioni", ShowScansAsync);
        AddNav(menu, "▦   Firewall e rete", ShowFirewallAsync);
        AddNav(menu, "⚙   Automazione", ShowAutomationAsync);
        AddNav(menu, "☣   Quarantena", ShowQuarantineAsync);
        AddNav(menu, "▥   Rapporti", ShowReportsAsync);
        AddNav(menu, "☏   Assistenza", ShowSupportAsync);
        AddNav(menu, "●   Informazioni", ShowInfoAsync);

        Panel protection = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(8, 26, 14), Padding = new Padding(14) };
        protection.Paint += (_, e) => DrawBorder(e.Graphics, protection.ClientRectangle, Neon);
        protection.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "🛡  PROTEZIONE ATTIVA\nMicrosoft Defender integrato\n\nVersione 9.1",
            ForeColor = Neon,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        });

        layout.Controls.Add(identity, 0, 0);
        layout.Controls.Add(menu, 0, 1);
        layout.Controls.Add(protection, 0, 2);
        side.Controls.Add(layout);
        return side;
    }

    private void AddNav(Control parent, string text, Func<Task> action)
    {
        Button button = UiButton(text, 232);
        button.Height = 42;
        button.Margin = new Padding(0, 2, 0, 2);
        button.Padding = new Padding(14, 0, 0, 0);
        button.TextAlign = ContentAlignment.MiddleLeft;
        button.Click += async (_, _) => await NavigateAsync(button, action);
        _navigation.Add(button);
        parent.Controls.Add(button);
        if (_selected is null) SelectNav(button);
    }

    private async Task NavigateAsync(Button button, Func<Task> action)
    {
        SelectNav(button);
        await RunSafeAsync(action);
    }

    private void SelectNav(Button selected)
    {
        _selected = selected;
        foreach (Button button in _navigation)
        {
            bool active = ReferenceEquals(button, selected);
            button.BackColor = active ? Color.FromArgb(30, 67, 4) : Surface;
            button.ForeColor = active ? Neon : Color.White;
            button.FlatAppearance.BorderColor = active ? Neon : Border;
        }
    }

    private async Task ShowDashboardAsync()
    {
        Panel body = CreatePage("Dashboard", "Protezione stabile e integrata con Microsoft Defender");
        _status.Text = "Controllo dello stato di sicurezza in corso…";
        SecurityState state = await _defender.GetStateAsync();

        TableLayoutPanel grid = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(2) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 44));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 56));
        grid.Controls.Add(ProtectionCard(state), 0, 0);
        grid.Controls.Add(QuickActionsCard(), 1, 0);
        grid.Controls.Add(SecurityStateCard(state), 0, 1);
        grid.Controls.Add(AdviceCard(state), 1, 1);
        body.Controls.Add(grid);
        _status.Text = $"Controllo completato alle {DateTime.Now:HH:mm:ss}. Firme: {state.SignatureVersion}";
    }

    private Control ProtectionCard(SecurityState state)
    {
        Panel content = CardContent("PROTEZIONE DEL DISPOSITIVO");
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(8) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.Controls.Add(new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, Image = DobermannIconFactory.CreateBitmap(280) }, 0, 0);
        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = state.Issues.Count == 0 ? Neon : Color.Orange,
            Text = $"PUNTEGGIO {state.Score}/100\n\n{(state.Issues.Count == 0 ? "SISTEMA PROTETTO" : "VERIFICA RICHIESTA")}"
        }, 1, 0);
        content.Controls.Add(layout);
        return content.Parent!;
    }

    private Control QuickActionsCard()
    {
        Panel content = CardContent("AZIONI RAPIDE");
        FlowLayoutPanel actions = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(8) };
        actions.Controls.Add(ActionButton("SCANSIONE RAPIDA", () => DefenderActionAsync(_defender.QuickScanAsync, "Scansione rapida avviata.")));
        actions.Controls.Add(ActionButton("SCANSIONE COMPLETA", () => DefenderActionAsync(_defender.FullScanAsync, "Scansione completa avviata.")));
        actions.Controls.Add(ActionButton("AGGIORNA FIRME", () => DefenderActionAsync(_defender.UpdateAsync, "Firme aggiornate.")));
        content.Controls.Add(actions);
        return content.Parent!;
    }

    private Control SecurityStateCard(SecurityState state)
    {
        Panel content = CardContent("STATO PROTEZIONE");
        TableLayoutPanel table = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(8) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        string[] names = ["Defender", "Tempo reale", "Firewall", "Firme", "Ransomware", "Rete"];
        bool[] values = [state.Antivirus, state.Realtime, state.Firewall, state.Signatures, state.Ransomware, state.Network];
        for (int i = 0; i < names.Length; i++) table.Controls.Add(StateTile(names[i], values[i]), i % 2, i / 2);
        content.Controls.Add(table);
        return content.Parent!;
    }

    private static Control AdviceCard(SecurityState state)
    {
        Panel content = CardContent("SICUREZZA E CONSIGLI");
        content.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            Font = new Font("Segoe UI", 11F),
            ForeColor = state.Issues.Count == 0 ? Neon : Color.Orange,
            Text = state.Issues.Count == 0
                ? "✓ Nessuna azione urgente.\n\nIl sistema risulta protetto e monitorato.\n\nEsegui periodicamente una scansione completa."
                : string.Join("\n\n", state.Issues.Select(issue => "• " + issue))
        });
        return content.Parent!;
    }

    private Task ShowScansAsync()
    {
        Panel body = CreatePage("Scansione malware", "Analizza il dispositivo con Microsoft Defender");
        FlowLayoutPanel flow = TileFlow();
        flow.Controls.Add(ActionTile("Scansione rapida", "Controlla le aree più critiche del sistema in pochi minuti.", "AVVIA", () => DefenderActionAsync(_defender.QuickScanAsync, "Scansione rapida avviata.")));
        flow.Controls.Add(ActionTile("Scansione completa", "Analizza l’intero sistema alla ricerca di minacce e malware.", "AVVIA", () => DefenderActionAsync(_defender.FullScanAsync, "Scansione completa avviata.")));
        flow.Controls.Add(ActionTile("Scansione cartella", "Seleziona una cartella specifica da controllare.", "SELEZIONA", ScanFolderAsync));
        flow.Controls.Add(ActionTile("Aggiorna firme", "Scarica le definizioni di sicurezza più recenti.", "AGGIORNA", () => DefenderActionAsync(_defender.UpdateAsync, "Firme aggiornate.")));
        body.Controls.Add(flow);
        return Task.CompletedTask;
    }

    private Task ShowFirewallAsync()
    {
        Panel body = CreatePage("Firewall e rete", "Strumenti Windows per protezione e diagnostica");
        FlowLayoutPanel flow = TileFlow();
        flow.Controls.Add(ActionTile("Firewall avanzato", "Apri la console ufficiale del firewall Windows.", "APRI", () => OpenToolAsync("wf.msc")));
        flow.Controls.Add(ActionTile("Connessioni attive", "Controlla processi, porte e traffico di rete.", "ANALIZZA", () => OpenToolAsync("resmon.exe")));
        flow.Controls.Add(ActionTile("Configurazione IP", "Visualizza la configurazione degli adattatori.", "VISUALIZZA", () => OpenConsoleAsync("ipconfig /all & pause")));
        body.Controls.Add(flow);
        return Task.CompletedTask;
    }

    private Task ShowAutomationAsync()
    {
        Panel body = CreatePage("Automazione", "Un solo motore automatico, senza operazioni duplicate");
        Panel content = CardContent("PROTEZIONE AUTONOMA");
        content.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            Font = new Font("Segoe UI", 12F),
            Text = "• Controllo stato ogni 15 minuti\n\n• Aggiornamento firme ogni 24 ore\n\n• Scansione rapida ogni 7 giorni\n\n• Avvisi tramite area di notifica Windows"
        });
        body.Controls.Add(content.Parent!);
        return Task.CompletedTask;
    }

    private Task ShowQuarantineAsync()
    {
        Panel body = CreatePage("Quarantena", "Gestione ufficiale tramite Microsoft Defender");
        Panel content = CardContent("CRONOLOGIA PROTEZIONE");
        Button open = UiButton("APRI QUARANTENA", 300);
        open.Dock = DockStyle.Bottom;
        open.Click += (_, _) => _defender.OpenWindowsSecurity();
        content.Controls.Add(new Label { Dock = DockStyle.Fill, Padding = new Padding(18), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 12F), Text = "Controlla, ripristina o elimina gli elementi isolati dalla cronologia ufficiale di Microsoft Defender." });
        content.Controls.Add(open);
        body.Controls.Add(content.Parent!);
        return Task.CompletedTask;
    }

    private Task ShowReportsAsync()
    {
        Panel body = CreatePage("Rapporti", "Esporta informazioni diagnostiche verificate");
        FlowLayoutPanel flow = TileFlow();
        flow.Controls.Add(ActionTile("Rapporto diagnostico", "Crea un file TXT con lo stato corrente del sistema.", "GENERA", GenerateReportAsync));
        flow.Controls.Add(ActionTile("Cartella rapporti", "Apri la cartella dei rapporti esportati.", "APRI", OpenReportsFolderAsync));
        body.Controls.Add(flow);
        return Task.CompletedTask;
    }

    private Task ShowSupportAsync()
    {
        Panel body = CreatePage("Assistenza", "Contatta il supporto FF GUARDIAN");
        Panel content = CardContent("SUPPORTO CLIENTI");
        Button mail = UiButton("APRI EMAIL SUPPORTO", 300);
        mail.Dock = DockStyle.Bottom;
        mail.Click += (_, _) => OpenSupportEmail();
        content.Controls.Add(new Label { Dock = DockStyle.Fill, Padding = new Padding(18), Font = new Font("Segoe UI", 12F), Text = $"Email: {SupportEmail}\n\nVersione: FF GUARDIAN {VersionText}\n\nAllega un rapporto diagnostico e descrivi il problema." });
        content.Controls.Add(mail);
        body.Controls.Add(content.Parent!);
        return Task.CompletedTask;
    }

    private Task ShowInfoAsync()
    {
        Panel body = CreatePage("Informazioni", "FF GUARDIAN Professional Security by EL.CO");
        body.Controls.Add(new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 14F), Text = "FF GUARDIAN 9.1\nDefinitive Professional Edition\n\nInterfaccia nativa senza patch runtime\nUn solo motore automatico\nMicrosoft Defender integrato\n\nEL.CO di Francesco Fazzina" });
        return Task.CompletedTask;
    }

    private Panel CreatePage(string title, string subtitle)
    {
        _pageHost.SuspendLayout();
        _pageHost.Controls.Clear();
        Panel page = new() { Dock = DockStyle.Fill, BackColor = Bg };
        Panel heading = new() { Dock = DockStyle.Top, Height = 94, BackColor = Bg, Padding = new Padding(28, 14, 20, 8) };
        heading.Controls.Add(new Label { Dock = DockStyle.Bottom, Height = 28, Text = subtitle, ForeColor = Muted, Font = new Font("Segoe UI", 10F) });
        heading.Controls.Add(new Label { Dock = DockStyle.Top, Height = 46, Text = title, Font = new Font("Segoe UI", 24F, FontStyle.Bold) });
        Panel body = new() { Dock = DockStyle.Fill, BackColor = Bg, Padding = new Padding(22), AutoScroll = true };
        page.Controls.Add(body);
        page.Controls.Add(heading);
        _pageHost.Controls.Add(page);
        _pageHost.ResumeLayout(true);
        return body;
    }

    private static Panel CardContent(string title)
    {
        Panel outer = new() { Dock = DockStyle.Fill, BackColor = Surface, Padding = new Padding(18), Margin = new Padding(8) };
        outer.Paint += (_, e) => DrawBorder(e.Graphics, outer.ClientRectangle, Border);
        Label heading = new() { Dock = DockStyle.Top, Height = 40, Text = title, Font = new Font("Segoe UI", 12F, FontStyle.Bold) };
        Panel content = new() { Dock = DockStyle.Fill, BackColor = Surface, Padding = new Padding(0, 8, 0, 0) };
        outer.Controls.Add(content);
        outer.Controls.Add(heading);
        return content;
    }

    private static Control StateTile(string name, bool active)
    {
        Panel tile = new() { Dock = DockStyle.Fill, BackColor = Surface2, Margin = new Padding(6), Padding = new Padding(14) };
        tile.Controls.Add(new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, ForeColor = active ? Neon : Color.Orange, Font = new Font("Segoe UI", 11F, FontStyle.Bold), Text = $"{name}\n{(active ? "ATTIVO" : "VERIFICARE")}" });
        return tile;
    }

    private static FlowLayoutPanel TileFlow() => new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, AutoScroll = true, BackColor = Bg, Padding = new Padding(2) };

    private Control ActionTile(string title, string description, string actionText, Func<Task> action)
    {
        Panel tile = new() { Width = 310, Height = 260, BackColor = Surface, Margin = new Padding(10), Padding = new Padding(22) };
        tile.Paint += (_, e) => DrawBorder(e.Graphics, tile.ClientRectangle, Border);
        Button actionButton = UiButton(actionText, 266);
        actionButton.Dock = DockStyle.Bottom;
        actionButton.Height = 50;
        actionButton.Click += async (_, _) => await RunSafeAsync(action);
        Label descriptionLabel = new() { Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 10), ForeColor = Muted, Font = new Font("Segoe UI", 10.5F), Text = description };
        Label titleLabel = new() { Dock = DockStyle.Top, Height = 58, Font = new Font("Segoe UI", 14F, FontStyle.Bold), Text = title, TextAlign = ContentAlignment.MiddleLeft };
        tile.Controls.Add(descriptionLabel);
        tile.Controls.Add(actionButton);
        tile.Controls.Add(titleLabel);
        return tile;
    }

    private Button ActionButton(string text, Func<Task> action)
    {
        Button button = UiButton(text, 360);
        button.Margin = new Padding(0, 5, 0, 5);
        button.Click += async (_, _) => await RunSafeAsync(action);
        return button;
    }

    private static Button UiButton(string text, int width)
    {
        Button button = new() { Text = text, Width = width, Height = 46, BackColor = Surface, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), Cursor = Cursors.Hand };
        button.FlatAppearance.BorderColor = Neon;
        button.FlatAppearance.BorderSize = 1;
        return button;
    }

    private async Task ScanFolderAsync()
    {
        using FolderBrowserDialog dialog = new() { Description = "Seleziona la cartella da analizzare" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            await DefenderActionAsync(() => _defender.CustomScanAsync(dialog.SelectedPath), "Scansione cartella avviata.");
    }

    private async Task DefenderActionAsync(Func<Task> operation, string success)
    {
        _status.Text = "Operazione in corso…";
        await operation();
        _status.Text = success;
    }

    private async Task GenerateReportAsync()
    {
        string folder = ReportsFolder();
        Directory.CreateDirectory(folder);
        SecurityState state = await _defender.GetStateAsync();
        string path = Path.Combine(folder, $"FFGuardian-Report-9.1-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        string content = $"FF GUARDIAN 9.1 - RAPPORTO DIAGNOSTICO{Environment.NewLine}" +
                         $"Data: {DateTime.Now:dd/MM/yyyy HH:mm:ss}{Environment.NewLine}" +
                         $"Computer: {Environment.MachineName}{Environment.NewLine}" +
                         $"Windows: {Environment.OSVersion}{Environment.NewLine}" +
                         $"Punteggio: {state.Score}/100{Environment.NewLine}" +
                         $"Defender: {state.Antivirus}{Environment.NewLine}" +
                         $"Tempo reale: {state.Realtime}{Environment.NewLine}" +
                         $"Firewall: {state.Firewall}{Environment.NewLine}" +
                         $"Firme: {state.SignatureVersion}{Environment.NewLine}" +
                         $"Problemi: {string.Join(" | ", state.Issues)}{Environment.NewLine}";
        string temp = path + ".tmp";
        await File.WriteAllTextAsync(temp, content);
        File.Move(temp, path, true);
        if (!File.Exists(path) || new FileInfo(path).Length == 0) throw new IOException("Il rapporto non è stato creato correttamente.");
        _status.Text = $"Rapporto creato: {Path.GetFileName(path)}";
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
    }

    private static Task OpenReportsFolderAsync()
    {
        string folder = ReportsFolder();
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
        return Task.CompletedTask;
    }

    private static string ReportsFolder() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FF Guardian Reports");

    private static Task OpenToolAsync(string file)
    {
        Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
        return Task.CompletedTask;
    }

    private static Task OpenConsoleAsync(string command)
    {
        Process.Start(new ProcessStartInfo("cmd.exe", $"/k {command}") { UseShellExecute = true });
        return Task.CompletedTask;
    }

    private static void OpenSupportEmail()
    {
        string subject = Uri.EscapeDataString("Supporto FF GUARDIAN 9.1");
        string body = Uri.EscapeDataString($"Descrizione problema:\r\n\r\nVersione: FF GUARDIAN 9.1\r\nComputer: {Environment.MachineName}\r\nWindows: {Environment.OSVersion}\r\nData: {DateTime.Now:dd/MM/yyyy HH:mm}");
        Process.Start(new ProcessStartInfo($"mailto:{SupportEmail}?subject={subject}&body={body}") { UseShellExecute = true });
    }

    private async Task RunSafeAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        UseWaitCursor = true;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            _status.Text = "Operazione non completata.";
            MessageBox.Show(ErrorMessageFormatter.Format(ex).message, "FF GUARDIAN 9.1", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            UseWaitCursor = false;
            _busy = false;
        }
    }

    private static void DrawBorder(Graphics graphics, Rectangle bounds, Color color)
    {
        if (bounds.Width <= 1 || bounds.Height <= 1) return;
        using Pen pen = new(color, 1F);
        graphics.DrawRectangle(pen, 0, 0, bounds.Width - 1, bounds.Height - 1);
    }
}