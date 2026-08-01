using System.Diagnostics;

namespace FFGuardian;

internal sealed class ProfessionalMainForm92 : Form
{
    private const string VersionText = "9.2";
    private const string SupportEmail = "alsafe127.00@gmail.com";

    private static readonly Color Background = Color.FromArgb(3, 8, 12);
    private static readonly Color Sidebar = Color.FromArgb(5, 12, 17);
    private static readonly Color Surface = Color.FromArgb(12, 22, 28);
    private static readonly Color SurfaceDark = Color.FromArgb(8, 17, 22);
    private static readonly Color Border = Color.FromArgb(58, 76, 84);
    private static readonly Color Neon = Color.FromArgb(160, 255, 0);
    private static readonly Color Muted = Color.FromArgb(198, 205, 210);

    private readonly DefenderService _defender = new();
    private readonly Panel _pageHost = new() { Dock = DockStyle.Fill, BackColor = Background };
    private readonly Label _status = new()
    {
        Dock = DockStyle.Bottom,
        Height = 34,
        BackColor = Sidebar,
        ForeColor = Muted,
        Padding = new Padding(18, 0, 0, 0),
        TextAlign = ContentAlignment.MiddleLeft,
        Text = "FF GUARDIAN pronto."
    };
    private readonly List<Button> _navigation = [];
    private Button? _selectedNavigation;
    private bool _navigationBusy;

    public ProfessionalMainForm92()
    {
        Text = "FF GUARDIAN 9.2 — Triple-Checked Professional Edition by EL.CO";
        Icon = DobermannIconFactory.CreateIcon();
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1180, 760);
        BackColor = Background;
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
        TableLayoutPanel bar = new()
        {
            Dock = DockStyle.Top,
            Height = 74,
            BackColor = Sidebar,
            Padding = new Padding(24, 12, 20, 12),
            ColumnCount = 3,
            RowCount = 1
        };
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 164));
        bar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 194));

        Label brand = new()
        {
            Dock = DockStyle.Fill,
            Text = "FF GUARDIAN  |  PERSONAL SECURITY",
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        Button support = CreateButton("ASSISTENZA");
        support.Dock = DockStyle.Fill;
        support.Margin = new Padding(6, 0, 6, 0);
        support.Click += (_, _) => OpenSupportEmail();
        Button refresh = CreateButton("AGGIORNA STATO");
        refresh.Dock = DockStyle.Fill;
        refresh.Margin = new Padding(6, 0, 0, 0);
        refresh.BackColor = Color.FromArgb(38, 92, 0);
        refresh.Click += async (_, _) => await NavigateAsync(_navigation[0], ShowDashboardAsync);

        bar.Controls.Add(brand, 0, 0);
        bar.Controls.Add(support, 1, 0);
        bar.Controls.Add(refresh, 2, 0);
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
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 170));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));

        TableLayoutPanel identity = new() { Dock = DockStyle.Fill, BackColor = Sidebar, RowCount = 2, ColumnCount = 1 };
        identity.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        identity.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        identity.Controls.Add(new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = DobermannIconFactory.CreateBitmap(230)
        }, 0, 0);
        identity.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "FF GUARDIAN\nPERSONAL SECURITY • EL.CO",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold)
        }, 0, 1);

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
        AddNavigation(menu, "⌕   Scansioni", ShowScansAsync);
        AddNavigation(menu, "▦   Firewall e rete", ShowFirewallAsync);
        AddNavigation(menu, "⚙   Automazione", ShowAutomationAsync);
        AddNavigation(menu, "☣   Quarantena", ShowQuarantineAsync);
        AddNavigation(menu, "▥   Rapporti", ShowReportsAsync);
        AddNavigation(menu, "☏   Assistenza", ShowSupportAsync);
        AddNavigation(menu, "●   Informazioni", ShowInfoAsync);

        Panel protection = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(8, 26, 14), Padding = new Padding(14) };
        protection.Paint += (_, e) => DrawBorder(e.Graphics, protection.ClientRectangle, Neon);
        protection.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "🛡  PROTEZIONE ATTIVA\nMicrosoft Defender integrato\n\nVersione 9.2",
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

    private void AddNavigation(Control parent, string text, Func<Task> action)
    {
        Button button = CreateButton(text);
        button.Width = 232;
        button.Height = 42;
        button.Margin = new Padding(0, 2, 0, 2);
        button.Padding = new Padding(14, 0, 0, 0);
        button.TextAlign = ContentAlignment.MiddleLeft;
        button.Click += async (_, _) => await NavigateAsync(button, action);
        _navigation.Add(button);
        parent.Controls.Add(button);
        if (_selectedNavigation is null) SelectNavigation(button);
    }

    private async Task NavigateAsync(Button button, Func<Task> action)
    {
        if (_navigationBusy) return;
        _navigationBusy = true;
        try
        {
            await action();
            SelectNavigation(button);
        }
        catch (Exception ex)
        {
            HandleError(ex, "Impossibile aprire la pagina richiesta.");
        }
        finally
        {
            _navigationBusy = false;
        }
    }

    private void SelectNavigation(Button selected)
    {
        _selectedNavigation = selected;
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
        grid.Controls.Add(CreateCard("PROTEZIONE DEL DISPOSITIVO", BuildProtectionContent(state)), 0, 0);
        grid.Controls.Add(CreateCard("AZIONI RAPIDE", BuildQuickActions()), 1, 0);
        grid.Controls.Add(CreateCard("STATO PROTEZIONE", BuildSecurityState(state)), 0, 1);
        grid.Controls.Add(CreateCard("SICUREZZA E CONSIGLI", BuildAdvice(state)), 1, 1);
        body.Controls.Add(grid);
        _status.Text = $"Controllo completato alle {DateTime.Now:HH:mm:ss}. Firme: {state.SignatureVersion}";
    }

    private Control BuildProtectionContent(SecurityState state)
    {
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
        return layout;
    }

    private Control BuildQuickActions()
    {
        FlowLayoutPanel actions = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(8)
        };
        actions.Controls.Add(ActionButton("SCANSIONE RAPIDA", () => RunSecurityOperationAsync(_defender.QuickScanAsync, "Scansione rapida avviata.")));
        actions.Controls.Add(ActionButton("SCANSIONE COMPLETA", () => RunSecurityOperationAsync(_defender.FullScanAsync, "Scansione completa avviata.")));
        actions.Controls.Add(ActionButton("AGGIORNA FIRME", () => RunSecurityOperationAsync(_defender.UpdateAsync, "Firme aggiornate.")));
        return actions;
    }

    private static Control BuildSecurityState(SecurityState state)
    {
        TableLayoutPanel table = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(8) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (int row = 0; row < 3; row++) table.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 3f));
        string[] names = ["Defender", "Tempo reale", "Firewall", "Firme", "Ransomware", "Rete"];
        bool[] values = [state.Antivirus, state.Realtime, state.Firewall, state.Signatures, state.Ransomware, state.Network];
        for (int i = 0; i < names.Length; i++) table.Controls.Add(StateTile(names[i], values[i]), i % 2, i / 2);
        return table;
    }

    private static Control BuildAdvice(SecurityState state) => new Label
    {
        Dock = DockStyle.Fill,
        Padding = new Padding(12),
        Font = new Font("Segoe UI", 11F),
        ForeColor = state.Issues.Count == 0 ? Neon : Color.Orange,
        Text = state.Issues.Count == 0
            ? "✓ Nessuna azione urgente.\n\nIl sistema risulta protetto e monitorato.\n\nEsegui periodicamente una scansione completa."
            : string.Join("\n\n", state.Issues.Select(issue => "• " + issue))
    };

    private Task ShowScansAsync()
    {
        Panel body = CreatePage("Scansione malware", "Analizza il dispositivo con Microsoft Defender");
        FlowLayoutPanel flow = TileFlow();
        flow.Controls.Add(ActionTile("Scansione rapida", "Controlla le aree più critiche del sistema in pochi minuti.", "AVVIA", () => RunSecurityOperationAsync(_defender.QuickScanAsync, "Scansione rapida avviata.")));
        flow.Controls.Add(ActionTile("Scansione completa", "Analizza l’intero sistema alla ricerca di minacce e malware.", "AVVIA", () => RunSecurityOperationAsync(_defender.FullScanAsync, "Scansione completa avviata.")));
        flow.Controls.Add(ActionTile("Scansione cartella", "Seleziona una cartella specifica da controllare.", "SELEZIONA", ScanFolderAsync));
        flow.Controls.Add(ActionTile("Aggiorna firme", "Scarica le definizioni di sicurezza più recenti.", "AGGIORNA", () => RunSecurityOperationAsync(_defender.UpdateAsync, "Firme aggiornate.")));
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
        Label description = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            Font = new Font("Segoe UI", 12F),
            Text = "• Controllo dello stato ogni 15 minuti\n\n• Aggiornamento firme ogni 24 ore\n\n• Scansione rapida ogni 7 giorni\n\n• Avvisi tramite area di notifica Windows"
        };
        body.Controls.Add(CreateCard("PROTEZIONE AUTONOMA", description));
        return Task.CompletedTask;
    }

    private Task ShowQuarantineAsync()
    {
        Panel body = CreatePage("Quarantena", "Gestione ufficiale tramite Microsoft Defender");
        TableLayoutPanel content = new() { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(18) };
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        content.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 12F),
            Text = "Controlla, ripristina o elimina gli elementi isolati dalla cronologia ufficiale di Microsoft Defender."
        }, 0, 0);
        Button open = CreateButton("APRI QUARANTENA");
        open.Dock = DockStyle.Fill;
        open.Click += (_, _) => _defender.OpenWindowsSecurity();
        content.Controls.Add(open, 0, 1);
        body.Controls.Add(CreateCard("CRONOLOGIA PROTEZIONE", content));
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
        TableLayoutPanel content = new() { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(18) };
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        content.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        content.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 12F),
            Text = $"Email: {SupportEmail}\n\nVersione: FF GUARDIAN {VersionText}\n\nAllega un rapporto diagnostico e descrivi il problema riscontrato."
        }, 0, 0);
        Button mail = CreateButton("APRI EMAIL SUPPORTO");
        mail.Dock = DockStyle.Fill;
        mail.Click += (_, _) => OpenSupportEmail();
        content.Controls.Add(mail, 0, 1);
        body.Controls.Add(CreateCard("SUPPORTO CLIENTI", content));
        return Task.CompletedTask;
    }

    private Task ShowInfoAsync()
    {
        Panel body = CreatePage("Informazioni", "FF GUARDIAN Professional Security by EL.CO");
        body.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 14F),
            Text = "FF GUARDIAN 9.2\nTriple-Checked Professional Edition\n\nInterfaccia nativa senza patch runtime\nCoordinamento unico delle operazioni\nMicrosoft Defender integrato\n\nEL.CO di Francesco Fazzina"
        });
        return Task.CompletedTask;
    }

    private Panel CreatePage(string title, string subtitle)
    {
        _pageHost.SuspendLayout();
        try
        {
            _pageHost.Controls.Clear();
            TableLayoutPanel page = new() { Dock = DockStyle.Fill, BackColor = Background, RowCount = 2, ColumnCount = 1 };
            page.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
            page.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            TableLayoutPanel heading = new() { Dock = DockStyle.Fill, BackColor = Background, Padding = new Padding(28, 12, 20, 8), RowCount = 2, ColumnCount = 1 };
            heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            heading.Controls.Add(new Label { Dock = DockStyle.Fill, Text = title, Font = new Font("Segoe UI", 24F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            heading.Controls.Add(new Label { Dock = DockStyle.Fill, Text = subtitle, ForeColor = Muted, Font = new Font("Segoe UI", 10F), TextAlign = ContentAlignment.MiddleLeft }, 0, 1);

            Panel body = new() { Dock = DockStyle.Fill, BackColor = Background, Padding = new Padding(22), AutoScroll = true };
            page.Controls.Add(heading, 0, 0);
            page.Controls.Add(body, 0, 1);
            _pageHost.Controls.Add(page);
            return body;
        }
        finally
        {
            _pageHost.ResumeLayout(true);
        }
    }

    private static Panel CreateCard(string title, Control content)
    {
        Panel outer = new() { Dock = DockStyle.Fill, BackColor = Surface, Padding = new Padding(18), Margin = new Padding(8) };
        outer.Paint += (_, e) => DrawBorder(e.Graphics, outer.ClientRectangle, Border);
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = title,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        content.Dock = DockStyle.Fill;
        layout.Controls.Add(content, 0, 1);
        outer.Controls.Add(layout);
        return outer;
    }

    private static Control StateTile(string name, bool active)
    {
        Panel tile = new() { Dock = DockStyle.Fill, BackColor = SurfaceDark, Margin = new Padding(6), Padding = new Padding(14) };
        tile.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = active ? Neon : Color.Orange,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            Text = $"{name}\n{(active ? "ATTIVO" : "VERIFICARE")}"
        });
        return tile;
    }

    private static FlowLayoutPanel TileFlow() => new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.LeftToRight,
        WrapContents = true,
        AutoScroll = true,
        BackColor = Background,
        Padding = new Padding(2)
    };

    private Control ActionTile(string title, string description, string actionText, Func<Task> action)
    {
        Panel tile = new() { Width = 310, Height = 260, BackColor = Surface, Margin = new Padding(10), Padding = new Padding(22) };
        tile.Paint += (_, e) => DrawBorder(e.Graphics, tile.ClientRectangle, Border);
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            Text = title,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 8, 0, 8),
            ForeColor = Muted,
            Font = new Font("Segoe UI", 10.5F),
            Text = description
        }, 0, 1);
        Button actionButton = CreateButton(actionText);
        actionButton.Dock = DockStyle.Fill;
        actionButton.Click += async (_, _) => await RunUiActionAsync(action);
        layout.Controls.Add(actionButton, 0, 2);
        tile.Controls.Add(layout);
        return tile;
    }

    private Button ActionButton(string text, Func<Task> action)
    {
        Button button = CreateButton(text);
        button.Width = 360;
        button.Margin = new Padding(0, 5, 0, 5);
        button.Click += async (_, _) => await RunUiActionAsync(action);
        return button;
    }

    private static Button CreateButton(string text)
    {
        Button button = new()
        {
            Text = text,
            Height = 46,
            BackColor = Surface,
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
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        string selectedPath = dialog.SelectedPath;
        await RunSecurityOperationAsync(() => _defender.CustomScanAsync(selectedPath), "Scansione della cartella avviata.");
    }

    private async Task RunSecurityOperationAsync(Func<Task> operation, string success)
    {
        using IDisposable? lease = await SecurityOperationGate92.TryEnterAsync();
        if (lease is null)
        {
            _status.Text = "Un’altra operazione di sicurezza è già in corso.";
            return;
        }

        _status.Text = "Operazione di sicurezza in corso…";
        await operation();
        _status.Text = success;
    }

    private async Task GenerateReportAsync()
    {
        string folder = ReportsFolder();
        Directory.CreateDirectory(folder);
        SecurityState state = await _defender.GetStateAsync();
        string path = Path.Combine(folder, $"FFGuardian-Report-9.2-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        string temp = path + ".tmp";
        string content = $"FF GUARDIAN 9.2 - RAPPORTO DIAGNOSTICO{Environment.NewLine}" +
                         $"Data: {DateTime.Now:dd/MM/yyyy HH:mm:ss}{Environment.NewLine}" +
                         $"Computer: {Environment.MachineName}{Environment.NewLine}" +
                         $"Windows: {Environment.OSVersion}{Environment.NewLine}" +
                         $"Punteggio: {state.Score}/100{Environment.NewLine}" +
                         $"Defender: {state.Antivirus}{Environment.NewLine}" +
                         $"Tempo reale: {state.Realtime}{Environment.NewLine}" +
                         $"Firewall: {state.Firewall}{Environment.NewLine}" +
                         $"Firme: {state.SignatureVersion}{Environment.NewLine}" +
                         $"Problemi: {(state.Issues.Count == 0 ? "Nessuno" : string.Join(" | ", state.Issues))}{Environment.NewLine}";

        await File.WriteAllTextAsync(temp, content);
        File.Move(temp, path, true);
        FileInfo report = new(path);
        if (!report.Exists || report.Length == 0) throw new IOException("Il rapporto non è stato creato correttamente.");
        _status.Text = $"Rapporto creato: {report.Name}";
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
        string subject = Uri.EscapeDataString("Supporto FF GUARDIAN 9.2");
        string body = Uri.EscapeDataString($"Descrizione problema:\r\n\r\nVersione: FF GUARDIAN 9.2\r\nComputer: {Environment.MachineName}\r\nWindows: {Environment.OSVersion}\r\nData: {DateTime.Now:dd/MM/yyyy HH:mm}");
        Process.Start(new ProcessStartInfo($"mailto:{SupportEmail}?subject={subject}&body={body}") { UseShellExecute = true });
    }

    private async Task RunUiActionAsync(Func<Task> action)
    {
        UseWaitCursor = true;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            HandleError(ex, "Operazione non completata.");
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void HandleError(Exception ex, string status)
    {
        StabilityCoordinator82.WriteStabilityLog(ex);
        _status.Text = status;
        (string message, MessageBoxIcon icon) = ErrorMessageFormatter.Format(ex);
        MessageBox.Show(message, "FF GUARDIAN 9.2", MessageBoxButtons.OK, icon);
    }

    private static void DrawBorder(Graphics graphics, Rectangle bounds, Color color)
    {
        if (bounds.Width <= 1 || bounds.Height <= 1) return;
        using Pen pen = new(color, 1F);
        graphics.DrawRectangle(pen, 0, 0, bounds.Width - 1, bounds.Height - 1);
    }
}
