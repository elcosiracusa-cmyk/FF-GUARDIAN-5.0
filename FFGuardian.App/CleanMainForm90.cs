using System.Diagnostics;

namespace FFGuardian;

internal sealed class CleanMainForm90 : Form
{
    private const string VersionText = "9.0";
    private const string SupportEmail = "alsafe127.00@gmail.com";

    private static readonly Color Background = Color.FromArgb(3, 8, 12);
    private static readonly Color Sidebar = Color.FromArgb(5, 12, 17);
    private static readonly Color Header = Color.FromArgb(4, 11, 16);
    private static readonly Color Card = Color.FromArgb(12, 22, 28);
    private static readonly Color CardHover = Color.FromArgb(17, 32, 39);
    private static readonly Color Border = Color.FromArgb(54, 72, 79);
    private static readonly Color Neon = Color.FromArgb(160, 255, 0);
    private static readonly Color Muted = Color.FromArgb(194, 202, 207);

    private readonly DefenderService _defender = new();
    private readonly Panel _content = new() { Dock = DockStyle.Fill, BackColor = Background };
    private readonly Label _status = new()
    {
        Dock = DockStyle.Bottom,
        Height = 34,
        BackColor = Header,
        ForeColor = Muted,
        Padding = new Padding(18, 0, 0, 0),
        TextAlign = ContentAlignment.MiddleLeft,
        Text = "FF GUARDIAN pronto."
    };
    private readonly List<Button> _navigation = [];
    private Button? _selectedNavigation;
    private bool _busy;

    public CleanMainForm90()
    {
        Text = "FF GUARDIAN 9.0 — Professional Clean Architecture by EL.CO";
        Icon = DobermannIconFactory.CreateIcon();
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1180, 760);
        BackColor = Background;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10F);
        AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true;

        Controls.Add(_content);
        Controls.Add(BuildSidebar());
        Controls.Add(BuildTopBar());
        Controls.Add(_status);

        Shown += async (_, _) => await ShowDashboardAsync();
    }

    private Control BuildTopBar()
    {
        Panel bar = new()
        {
            Dock = DockStyle.Top,
            Height = 74,
            BackColor = Header,
            Padding = new Padding(24, 12, 20, 12)
        };

        Label brand = new()
        {
            Dock = DockStyle.Left,
            Width = 590,
            Text = "FF GUARDIAN  |  PERSONAL SECURITY",
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft
        };

        Button refresh = CreateButton("AGGIORNA STATO", 180);
        refresh.Dock = DockStyle.Right;
        refresh.BackColor = Color.FromArgb(37, 93, 0);
        refresh.Click += async (_, _) => await ShowDashboardAsync();

        Button support = CreateButton("ASSISTENZA", 155);
        support.Dock = DockStyle.Right;
        support.Margin = new Padding(0, 0, 12, 0);
        support.Click += (_, _) => OpenSupportEmail();

        bar.Controls.Add(refresh);
        bar.Controls.Add(support);
        bar.Controls.Add(brand);
        return bar;
    }

    private Control BuildSidebar()
    {
        Panel side = new() { Dock = DockStyle.Left, Width = 270, BackColor = Sidebar };
        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(12)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 174));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));

        Panel identity = new() { Dock = DockStyle.Fill, BackColor = Sidebar };
        PictureBox logo = new()
        {
            Dock = DockStyle.Top,
            Height = 112,
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = DobermannIconFactory.CreateBitmap(240)
        };
        Label name = new()
        {
            Dock = DockStyle.Fill,
            Text = "FF GUARDIAN\nPERSONAL SECURITY\nby EL.CO",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.White
        };
        identity.Controls.Add(name);
        identity.Controls.Add(logo);

        FlowLayoutPanel menu = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Sidebar,
            Padding = new Padding(0, 2, 0, 2)
        };

        AddNavigation(menu, "⌂   Dashboard", ShowDashboardAsync);
        AddNavigation(menu, "⌕   Scansioni", () => ShowScansPage());
        AddNavigation(menu, "▦   Firewall e rete", () => ShowFirewallPage());
        AddNavigation(menu, "⚙   Automazione", () => ShowAutomationPage());
        AddNavigation(menu, "☣   Quarantena", () => ShowQuarantinePage());
        AddNavigation(menu, "▥   Rapporti", () => ShowReportsPage());
        AddNavigation(menu, "☏   Assistenza", () => ShowSupportPage());
        AddNavigation(menu, "●   Informazioni", () => ShowInformationPage());

        Panel protection = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(8, 26, 14),
            Padding = new Padding(14)
        };
        protection.Paint += (_, e) =>
        {
            using Pen pen = new(Neon, 1F);
            e.Graphics.DrawRectangle(pen, 0, 0, protection.ClientSize.Width - 1, protection.ClientSize.Height - 1);
        };
        protection.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "🛡  PROTEZIONE ATTIVA\nMicrosoft Defender integrato\n\nVersione 9.0",
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Neon,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        });

        layout.Controls.Add(identity, 0, 0);
        layout.Controls.Add(menu, 0, 1);
        layout.Controls.Add(protection, 0, 2);
        side.Controls.Add(layout);
        return side;
    }

    private void AddNavigation(Control parent, string text, Func<Task> action)
    {
        Button button = CreateButton(text, 232);
        button.Height = 42;
        button.Margin = new Padding(0, 2, 0, 2);
        button.TextAlign = ContentAlignment.MiddleLeft;
        button.Padding = new Padding(14, 0, 0, 0);
        button.Click += async (_, _) =>
        {
            SelectNavigation(button);
            await RunSafeAsync(action);
        };
        _navigation.Add(button);
        parent.Controls.Add(button);
        if (_selectedNavigation is null) SelectNavigation(button);
    }

    private void SelectNavigation(Button selected)
    {
        _selectedNavigation = selected;
        foreach (Button button in _navigation)
        {
            bool active = ReferenceEquals(button, selected);
            button.BackColor = active ? Color.FromArgb(30, 67, 4) : Card;
            button.ForeColor = active ? Neon : Color.White;
            button.FlatAppearance.BorderColor = active ? Neon : Border;
        }
    }

    private async Task ShowDashboardAsync()
    {
        Panel body = CreatePage("Dashboard", "Protezione chiara, stabile e integrata con Microsoft Defender");
        _status.Text = "Controllo dello stato di sicurezza in corso…";
        SecurityState state = await _defender.GetStateAsync();

        TableLayoutPanel grid = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(2)
        };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 44));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 56));

        grid.Controls.Add(BuildProtectionSummary(state), 0, 0);
        grid.Controls.Add(BuildQuickActions(), 1, 0);
        grid.Controls.Add(BuildSecurityState(state), 0, 1);
        grid.Controls.Add(BuildAdvice(state), 1, 1);
        body.Controls.Add(grid);
        _status.Text = $"Controllo completato alle {DateTime.Now:HH:mm:ss}. Firme: {state.SignatureVersion}";
    }

    private Control BuildProtectionSummary(SecurityState state)
    {
        Panel card = CreateCard();
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(20) };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        PictureBox dog = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, Image = DobermannIconFactory.CreateBitmap(300) };
        Label text = new()
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12F),
            Text = $"PUNTEGGIO SICUREZZA\n\n{state.Score}/100\n\n{(state.Issues.Count == 0 ? "SISTEMA PROTETTO" : "VERIFICA RICHIESTA")}" 
        };
        layout.Controls.Add(dog, 0, 0);
        layout.Controls.Add(text, 1, 0);
        card.Controls.Add(layout);
        return card;
    }

    private Control BuildQuickActions()
    {
        Panel card = CreateCard("AZIONI RAPIDE");
        FlowLayoutPanel actions = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(18, 12, 18, 18)
        };
        actions.Controls.Add(ActionButton("SCANSIONE RAPIDA", () => RunDefenderActionAsync(_defender.QuickScanAsync, "Scansione rapida avviata.")));
        actions.Controls.Add(ActionButton("SCANSIONE COMPLETA", () => RunDefenderActionAsync(_defender.FullScanAsync, "Scansione completa avviata.")));
        actions.Controls.Add(ActionButton("AGGIORNA FIRME", () => RunDefenderActionAsync(_defender.UpdateAsync, "Firme aggiornate.")));
        card.Controls.Add(actions);
        return card;
    }

    private Control BuildSecurityState(SecurityState state)
    {
        Panel card = CreateCard("STATO PROTEZIONE");
        TableLayoutPanel table = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(20) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        string[] names = ["Defender", "Tempo reale", "Firewall", "Firme", "Ransomware", "Rete"];
        bool[] values = [state.Antivirus, state.Realtime, state.Firewall, state.Signatures, state.Ransomware, state.Network];
        for (int i = 0; i < names.Length; i++)
            table.Controls.Add(StateBox(names[i], values[i]), i % 2, i / 2);
        card.Controls.Add(table);
        return card;
    }

    private static Control BuildAdvice(SecurityState state)
    {
        Panel card = CreateCard("SICUREZZA E CONSIGLI");
        Label advice = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            Font = new Font("Segoe UI", 11F),
            ForeColor = state.Issues.Count == 0 ? Neon : Color.Orange,
            Text = state.Issues.Count == 0
                ? "✓ Nessuna azione urgente.\n\nIl sistema risulta protetto e monitorato.\n\nEsegui periodicamente una scansione completa."
                : string.Join("\n\n", state.Issues.Select(issue => "• " + issue))
        };
        card.Controls.Add(advice);
        return card;
    }

    private Task ShowScansPage()
    {
        Panel body = CreatePage("Scansione malware", "Analizza il dispositivo con Microsoft Defender");
        FlowLayoutPanel flow = CreateTileFlow();
        flow.Controls.Add(ScanTile("Scansione rapida", "Controlla le aree più critiche del sistema in pochi minuti.", "AVVIA", () => RunDefenderActionAsync(_defender.QuickScanAsync, "Scansione rapida avviata.")));
        flow.Controls.Add(ScanTile("Scansione completa", "Analizza l’intero sistema alla ricerca di minacce e malware.", "AVVIA", () => RunDefenderActionAsync(_defender.FullScanAsync, "Scansione completa avviata.")));
        flow.Controls.Add(ScanTile("Scansione cartella", "Seleziona una cartella specifica da controllare.", "SELEZIONA", ScanFolderAsync));
        flow.Controls.Add(ScanTile("Aggiorna firme", "Scarica le definizioni di sicurezza più recenti.", "AGGIORNA", () => RunDefenderActionAsync(_defender.UpdateAsync, "Firme aggiornate.")));
        body.Controls.Add(flow);
        return Task.CompletedTask;
    }

    private Task ShowFirewallPage()
    {
        Panel body = CreatePage("Firewall e rete", "Strumenti Windows per protezione e diagnostica della rete");
        FlowLayoutPanel flow = CreateTileFlow();
        flow.Controls.Add(ScanTile("Firewall avanzato", "Apri la console di gestione del firewall Windows.", "APRI", () => OpenToolAsync("wf.msc")));
        flow.Controls.Add(ScanTile("Connessioni attive", "Controlla processi, porte e traffico di rete.", "ANALIZZA", () => OpenToolAsync("resmon.exe")));
        flow.Controls.Add(ScanTile("Configurazione IP", "Visualizza la configurazione completa degli adattatori.", "VISUALIZZA", () => OpenConsoleAsync("ipconfig /all & pause")));
        body.Controls.Add(flow);
        return Task.CompletedTask;
    }

    private Task ShowAutomationPage()
    {
        Panel body = CreatePage("Automazione", "La protezione continua anche quando la finestra è chiusa");
        Panel card = CreateCard("PROTEZIONE AUTONOMA");
        card.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(26),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12F),
            Text = "• Controllo dello stato ogni 15 minuti\n\n• Aggiornamento firme ogni 24 ore\n\n• Scansione rapida programmata ogni 7 giorni\n\n• Avvisi nell’area di notifica Windows"
        });
        body.Controls.Add(card);
        return Task.CompletedTask;
    }

    private Task ShowQuarantinePage()
    {
        Panel body = CreatePage("Quarantena", "Gestione ufficiale degli elementi isolati da Microsoft Defender");
        Panel card = CreateCard("QUARANTENA MICROSOFT DEFENDER");
        Button open = CreateButton("APRI CRONOLOGIA PROTEZIONE", 330);
        open.Dock = DockStyle.Bottom;
        open.Height = 52;
        open.Click += (_, _) => _defender.OpenWindowsSecurity();
        card.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12F),
            Text = "FF GUARDIAN usa la quarantena ufficiale di Microsoft Defender.\n\nApri Cronologia protezione per controllare, ripristinare o eliminare gli elementi isolati."
        });
        card.Controls.Add(open);
        body.Controls.Add(card);
        return Task.CompletedTask;
    }

    private Task ShowReportsPage()
    {
        Panel body = CreatePage("Rapporti", "Esporta informazioni utili per diagnostica e assistenza");
        FlowLayoutPanel flow = CreateTileFlow();
        flow.Controls.Add(ScanTile("Rapporto diagnostico", "Crea un rapporto TXT verificato con lo stato del sistema.", "GENERA", GenerateReportAsync));
        flow.Controls.Add(ScanTile("Cartella rapporti", "Apri la cartella che contiene i rapporti esportati.", "APRI", OpenReportsFolderAsync));
        body.Controls.Add(flow);
        return Task.CompletedTask;
    }

    private Task ShowSupportPage()
    {
        Panel body = CreatePage("Assistenza", "Contatta il supporto FF GUARDIAN");
        Panel card = CreateCard("SUPPORTO CLIENTI");
        Button mail = CreateButton("APRI EMAIL SUPPORTO", 300);
        mail.Dock = DockStyle.Bottom;
        mail.Height = 52;
        mail.Click += (_, _) => OpenSupportEmail();
        card.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12F),
            Text = $"Email: {SupportEmail}\n\nVersione: FF GUARDIAN {VersionText}\n\nAllega un rapporto diagnostico e descrivi il problema riscontrato."
        });
        card.Controls.Add(mail);
        body.Controls.Add(card);
        return Task.CompletedTask;
    }

    private Task ShowInformationPage()
    {
        Panel body = CreatePage("Informazioni", "FF GUARDIAN Professional Security by EL.CO");
        body.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 14F),
            Text = "FF GUARDIAN 9.0\nProfessional Clean Architecture\n\nProtezione e gestione Microsoft Defender\nInterfaccia nativa senza patch runtime\n\nEL.CO di Francesco Fazzina"
        });
        return Task.CompletedTask;
    }

    private Panel CreatePage(string title, string subtitle)
    {
        _content.SuspendLayout();
        _content.Controls.Clear();
        Panel page = new() { Dock = DockStyle.Fill, BackColor = Background };
        Panel heading = new() { Dock = DockStyle.Top, Height = 94, BackColor = Background, Padding = new Padding(28, 14, 20, 8) };
        heading.Controls.Add(new Label { Dock = DockStyle.Bottom, Height = 28, Text = subtitle, ForeColor = Muted, Font = new Font("Segoe UI", 10F) });
        heading.Controls.Add(new Label { Dock = DockStyle.Top, Height = 46, Text = title, ForeColor = Color.White, Font = new Font("Segoe UI", 24F, FontStyle.Bold) });
        Panel body = new() { Dock = DockStyle.Fill, BackColor = Background, Padding = new Padding(22), AutoScroll = true };
        page.Controls.Add(body);
        page.Controls.Add(heading);
        _content.Controls.Add(page);
        _content.ResumeLayout(true);
        return body;
    }

    private static Panel CreateCard(string? title = null)
    {
        Panel card = new() { Dock = DockStyle.Fill, BackColor = Card, Padding = new Padding(18), Margin = new Padding(8) };
        card.Paint += (_, e) =>
        {
            using Pen pen = new(Border, 1F);
            e.Graphics.DrawRectangle(pen, 0, 0, card.ClientSize.Width - 1, card.ClientSize.Height - 1);
        };
        if (!string.IsNullOrWhiteSpace(title))
        {
            card.Controls.Add(new Label
            {
                Dock = DockStyle.Top,
                Height = 38,
                Text = title,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold)
            });
        }
        return card;
    }

    private static Control StateBox(string name, bool active)
    {
        Panel box = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(8, 17, 22), Margin = new Padding(6), Padding = new Padding(14) };
        box.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = active ? Neon : Color.Orange,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            Text = $"{name}\n{(active ? "ATTIVO" : "VERIFICARE")}"
        });
        return box;
    }

    private static FlowLayoutPanel CreateTileFlow() => new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = true,
        AutoScroll = true,
        BackColor = Background,
        Padding = new Padding(2)
    };

    private Control ScanTile(string title, string description, string actionText, Func<Task> action)
    {
        Panel tile = new()
        {
            Width = 310,
            Height = 260,
            BackColor = Card,
            Margin = new Padding(10),
            Padding = new Padding(22)
        };
        tile.Paint += (_, e) =>
        {
            using Pen pen = new(Border, 1F);
            e.Graphics.DrawRectangle(pen, 0, 0, tile.ClientSize.Width - 1, tile.ClientSize.Height - 1);
        };
        tile.MouseEnter += (_, _) => tile.BackColor = CardHover;
        tile.MouseLeave += (_, _) => tile.BackColor = Card;

        Button actionButton = CreateButton(actionText, 266);
        actionButton.Dock = DockStyle.Bottom;
        actionButton.Height = 50;
        actionButton.Click += async (_, _) => await RunSafeAsync(action);

        Label descriptionLabel = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 10, 0, 10),
            ForeColor = Muted,
            Font = new Font("Segoe UI", 10.5F),
            Text = description,
            AutoEllipsis = false
        };
        Label titleLabel = new()
        {
            Dock = DockStyle.Top,
            Height = 58,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            Text = title,
            TextAlign = ContentAlignment.MiddleLeft
        };

        tile.Controls.Add(descriptionLabel);
        tile.Controls.Add(actionButton);
        tile.Controls.Add(titleLabel);
        return tile;
    }

    private Button ActionButton(string text, Func<Task> action)
    {
        Button button = CreateButton(text, 360);
        button.Height = 48;
        button.Margin = new Padding(0, 5, 0, 5);
        button.Click += async (_, _) => await RunSafeAsync(action);
        return button;
    }

    private static Button CreateButton(string text, int width)
    {
        Button button = new()
        {
            Text = text,
            Width = width,
            Height = 46,
            BackColor = Card,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = Neon;
        button.FlatAppearance.BorderSize = 1;
        return button;
    }

    private async Task ScanFolderAsync()
    {
        using FolderBrowserDialog dialog = new() { Description = "Seleziona la cartella da analizzare" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            await RunDefenderActionAsync(() => _defender.CustomScanAsync(dialog.SelectedPath), "Scansione cartella avviata.");
    }

    private async Task RunDefenderActionAsync(Func<Task> operation, string success)
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
        string path = Path.Combine(folder, $"FFGuardian-Report-9.0-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        string content = $"FF GUARDIAN 9.0 - RAPPORTO DIAGNOSTICO\r\n" +
                         $"Data: {DateTime.Now:dd/MM/yyyy HH:mm:ss}\r\n" +
                         $"Computer: {Environment.MachineName}\r\n" +
                         $"Windows: {Environment.OSVersion}\r\n" +
                         $"Punteggio: {state.Score}/100\r\n" +
                         $"Defender: {state.Antivirus}\r\n" +
                         $"Tempo reale: {state.Realtime}\r\n" +
                         $"Firewall: {state.Firewall}\r\n" +
                         $"Firme: {state.SignatureVersion}\r\n" +
                         $"Problemi: {string.Join(" | ", state.Issues)}\r\n";
        string temp = path + ".tmp";
        await File.WriteAllTextAsync(temp, content);
        File.Move(temp, path, true);
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
        Process.Start(new ProcessStartInfo("cmd.exe", $"/k {command}") { UseShellExecute = true, Verb = "runas" });
        return Task.CompletedTask;
    }

    private static void OpenSupportEmail()
    {
        string subject = Uri.EscapeDataString("Supporto FF GUARDIAN 9.0");
        string body = Uri.EscapeDataString($"Descrizione problema:\r\n\r\nVersione: FF GUARDIAN 9.0\r\nComputer: {Environment.MachineName}\r\nWindows: {Environment.OSVersion}\r\nData: {DateTime.Now:dd/MM/yyyy HH:mm}");
        Process.Start(new ProcessStartInfo($"mailto:{SupportEmail}?subject={subject}&body={body}") { UseShellExecute = true });
    }

    private async Task RunSafeAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            UseWaitCursor = true;
            await action();
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            _status.Text = "Operazione non completata.";
            MessageBox.Show(this, ex.Message, "FF GUARDIAN — Errore controllato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            UseWaitCursor = false;
            _busy = false;
        }
    }
}
