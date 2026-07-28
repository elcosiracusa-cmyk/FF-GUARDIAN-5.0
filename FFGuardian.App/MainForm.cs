using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace FFGuardian;

internal sealed class MainForm : Form
{
    private static readonly Color Bg = Color.FromArgb(4, 10, 18);
    private static readonly Color Surface = Color.FromArgb(10, 25, 41);
    private static readonly Color Surface2 = Color.FromArgb(14, 35, 56);
    private static readonly Color Blue = Color.FromArgb(0, 116, 245);
    private static readonly Color Cyan = Color.FromArgb(0, 207, 255);
    private static readonly Color Green = Color.FromArgb(67, 226, 110);
    private static readonly Color Orange = Color.FromArgb(255, 170, 42);
    private static readonly Color Red = Color.FromArgb(245, 70, 75);

    private readonly DefenderService _defender = new();
    private readonly Panel _pageHost = new() { Dock = DockStyle.Fill, BackColor = Bg };
    private readonly Label _status = new()
    {
        Dock = DockStyle.Bottom,
        Height = 32,
        ForeColor = Color.Gainsboro,
        BackColor = Color.FromArgb(5, 15, 26),
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(16, 0, 0, 0)
    };
    private readonly List<Button> _navButtons = [];

    public MainForm()
    {
        Text = "FF GUARDIAN 5.0.2 — Navigation & Tools Fix by EL.CO";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1280, 800);
        BackColor = Bg;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);
        DoubleBuffered = true;

        Controls.Add(_pageHost);
        Controls.Add(BuildSidebar());
        Controls.Add(BuildHeader());
        Controls.Add(_status);
        Shown += async (_, _) => await SafeAsync(ShowDashboardAsync);
    }

    private Control BuildHeader()
    {
        Panel header = new()
        {
            Dock = DockStyle.Top,
            Height = 78,
            BackColor = Color.FromArgb(5, 16, 28),
            Padding = new Padding(300, 0, 18, 0)
        };

        Label title = new()
        {
            Dock = DockStyle.Left,
            Width = 410,
            Text = "FF GUARDIAN 5.0.2",
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft
        };
        Label edition = new()
        {
            Dock = DockStyle.Left,
            Width = 410,
            Text = "NAVIGATION & TOOLS FIX  •  BY EL.CO",
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            ForeColor = Cyan,
            TextAlign = ContentAlignment.MiddleLeft
        };
        Button emergency = CyberButton("⚠  MODALITÀ EMERGENZA", 210, 46, Red);
        emergency.Dock = DockStyle.Right;
        emergency.Click += async (_, _) => await SafeAsync(EmergencyAsync);
        Button refresh = CyberButton("⟳  AGGIORNA SISTEMA", 195, 46, Blue);
        refresh.Dock = DockStyle.Right;
        refresh.Click += async (_, _) => await SafeAsync(ShowDashboardAsync);

        header.Controls.Add(emergency);
        header.Controls.Add(refresh);
        header.Controls.Add(edition);
        header.Controls.Add(title);
        return header;
    }

    private Control BuildSidebar()
    {
        Panel sidebar = new() { Dock = DockStyle.Left, Width = 286, BackColor = Color.FromArgb(5, 17, 29) };
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(14) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));

        Panel brand = new() { Dock = DockStyle.Fill };
        brand.Paint += (_, e) => PaintBrand(e.Graphics);

        FlowLayoutPanel menu = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0, 4, 0, 4)
        };
        AddNav(menu, "⌂   Dashboard", ShowDashboardAsync);
        AddNav(menu, "⌕   Scansioni", () => { ShowScans(); return Task.CompletedTask; });
        AddNav(menu, "⚠   Minacce e Quarantena", ShowThreatsAsync);
        AddNav(menu, "◈   Protezione", ShowProtectionAsync);
        AddNav(menu, "◎   Rete e Firewall", () => { ShowNetwork(); return Task.CompletedTask; });
        AddNav(menu, "⚙   Manutenzione PC", () => { ShowTools(); return Task.CompletedTask; });
        AddNav(menu, "▣   Quarantena", () => { ShowQuarantine(); return Task.CompletedTask; });
        AddNav(menu, "≡   Report e Registro", ShowLogsAsync);
        AddNav(menu, "●   Informazioni", () => { ShowInfo(); return Task.CompletedTask; });

        Panel protectedPanel = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(6, 43, 31), Padding = new Padding(12) };
        protectedPanel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "✓  PROTETTO\nTutte le difese principali sono operative",
            ForeColor = Green,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        });

        layout.Controls.Add(brand, 0, 0);
        layout.Controls.Add(menu, 0, 1);
        layout.Controls.Add(protectedPanel, 0, 2);
        sidebar.Controls.Add(layout);
        return sidebar;
    }

    private static void PaintBrand(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        PointF[] shield = [new(143, 4), new(218, 36), new(207, 125), new(143, 172), new(79, 125), new(68, 36)];
        using Pen glow = new(Cyan, 4);
        g.DrawPolygon(glow, shield);

        using GraphicsPath dog = new();
        dog.AddPolygon([
            new PointF(103, 47), new PointF(116, 18), new PointF(132, 54),
            new PointF(154, 54), new PointF(171, 18), new PointF(184, 47),
            new PointF(178, 103), new PointF(160, 128), new PointF(143, 139),
            new PointF(126, 128), new PointF(108, 103)
        ]);
        using SolidBrush face = new(Color.FromArgb(215, 226, 235));
        using Pen outline = new(Color.FromArgb(32, 45, 60), 3);
        g.FillPath(face, dog);
        g.DrawPath(outline, dog);
        using SolidBrush dark = new(Color.FromArgb(28, 40, 54));
        g.FillPolygon(dark, [new PointF(108, 50), new PointF(139, 78), new PointF(126, 112), new PointF(108, 98)]);
        g.FillPolygon(dark, [new PointF(178, 50), new PointF(147, 78), new PointF(160, 112), new PointF(178, 98)]);
        using SolidBrush eyes = new(Orange);
        g.FillEllipse(eyes, 121, 76, 8, 6);
        g.FillEllipse(eyes, 157, 76, 8, 6);
        g.FillPolygon(Brushes.Black, [new PointF(136, 111), new PointF(150, 111), new PointF(143, 122)]);

        using Font title = new("Segoe UI", 18, FontStyle.Bold);
        using Font sub = new("Segoe UI", 10, FontStyle.Bold);
        DrawCentered(g, "FF GUARDIAN", title, Brushes.White, 143, 143);
        DrawCentered(g, "BY EL.CO", sub, Brushes.DeepSkyBlue, 143, 170);
    }

    private static void DrawCentered(Graphics g, string text, Font font, Brush brush, float centerX, float y)
    {
        SizeF size = g.MeasureString(text, font);
        g.DrawString(text, font, brush, centerX - size.Width / 2, y);
    }

    private void AddNav(Control parent, string text, Func<Task> action)
    {
        Button button = CyberButton(text, 246, 46, Surface2);
        button.Margin = new Padding(0, 3, 0, 3);
        button.TextAlign = ContentAlignment.MiddleLeft;
        button.Padding = new Padding(14, 0, 0, 0);
        button.Click += async (_, _) =>
        {
            _navButtons.ForEach(x => x.BackColor = Surface2);
            button.BackColor = Blue;
            await SafeAsync(action);
        };
        _navButtons.Add(button);
        parent.Controls.Add(button);
    }

    private Panel CreatePage(string title, string subtitle)
    {
        _pageHost.Controls.Clear();
        Panel page = new() { Dock = DockStyle.Fill, BackColor = Bg };
        Panel pageHeader = new() { Dock = DockStyle.Top, Height = 78, BackColor = Color.FromArgb(7, 20, 34), Padding = new Padding(20, 10, 10, 6) };
        pageHeader.Controls.Add(new Label { Text = subtitle, Dock = DockStyle.Bottom, Height = 24, ForeColor = Color.Silver, Font = new Font("Segoe UI", 9) });
        pageHeader.Controls.Add(new Label { Text = title, Dock = DockStyle.Top, Height = 38, Font = new Font("Segoe UI", 20, FontStyle.Bold), ForeColor = Color.White });
        Panel body = new() { Dock = DockStyle.Fill, BackColor = Bg, Padding = new Padding(16) };
        page.Controls.Add(body);
        page.Controls.Add(pageHeader);
        _pageHost.Controls.Add(page);
        return body;
    }

    private async Task ShowDashboardAsync()
    {
        Panel body = CreatePage("Dashboard di Protezione", "Panoramica completa dello stato di sicurezza del sistema");
        _status.Text = "Lettura dello stato Microsoft Defender...";
        SecurityState state = await _defender.GetStateAsync();

        TableLayoutPanel root = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, AutoScroll = true };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 288));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 105));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        TableLayoutPanel top = new() { Dock = DockStyle.Fill, ColumnCount = 3 };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 39));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27));
        top.Controls.Add(ScoreCard(state), 0, 0);
        top.Controls.Add(QuickCard(), 1, 0);
        top.Controls.Add(StateCard(state), 2, 0);

        TableLayoutPanel lower = new() { Dock = DockStyle.Fill, ColumnCount = 3 };
        lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36));
        lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 37));
        lower.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27));
        lower.Controls.Add(ActivityCard(state), 0, 0);
        lower.Controls.Add(AdviceCard(state), 1, 0);
        lower.Controls.Add(InfoCard(state), 2, 0);

        root.Controls.Add(top, 0, 0);
        root.Controls.Add(ProtectionCards(state), 0, 1);
        root.Controls.Add(lower, 0, 2);
        body.Controls.Add(root);
        _status.Text = $"Sistema aggiornato alle {DateTime.Now:HH:mm:ss} — Definizioni {state.SignatureVersion}";
    }

    private Control ScoreCard(SecurityState state)
    {
        Panel panel = Card("PUNTEGGIO DI PROTEZIONE");
        panel.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle ring = new(36, 54, 188, 188);
            using Pen basePen = new(Color.FromArgb(34, 68, 92), 17);
            using Pen scorePen = new(state.Score >= 85 ? Green : state.Score >= 65 ? Orange : Red, 17) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            e.Graphics.DrawArc(basePen, ring, 135, 270);
            e.Graphics.DrawArc(scorePen, ring, 135, 270 * state.Score / 100f);
            using Font big = new("Segoe UI", 42, FontStyle.Bold);
            SizeF number = e.Graphics.MeasureString(state.Score.ToString(), big);
            e.Graphics.DrawString(state.Score.ToString(), big, Brushes.White, 130 - number.Width / 2, 106);
            using Font small = new("Segoe UI", 12, FontStyle.Bold);
            e.Graphics.DrawString("/100", small, Brushes.Silver, 111, 166);
            string label = state.Score >= 85 ? "PROTEZIONE ELEVATA" : state.Score >= 65 ? "DA MIGLIORARE" : "INTERVENTO NECESSARIO";
            using SolidBrush brush = new(state.Score >= 85 ? Green : state.Score >= 65 ? Orange : Red);
            e.Graphics.DrawString(label, small, brush, 34, 248);
        };
        return panel;
    }

    private Control QuickCard()
    {
        Panel panel = Card("AZIONI RAPIDE");
        TableLayoutPanel grid = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(8) };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        grid.Controls.Add(ActionButton("⚡  Scansione rapida", () => RunAsync(_defender.QuickScanAsync, "Scansione rapida avviata.")), 0, 0);
        grid.Controls.Add(ActionButton("◉  Scansione completa", () => RunAsync(_defender.FullScanAsync, "Scansione completa avviata.")), 1, 0);
        grid.Controls.Add(ActionButton("▣  Scansiona cartella", FolderScanAsync), 0, 1);
        grid.Controls.Add(ActionButton("⟳  Aggiorna definizioni", () => RunAsync(_defender.UpdateAsync, "Definizioni aggiornate.")), 1, 1);
        panel.Controls.Add(grid);
        return panel;
    }

    private static Control StateCard(SecurityState state)
    {
        Panel panel = Card("STATO GENERALE");
        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(16),
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = state.Score >= 85 ? Green : Orange,
            Text = state.Score >= 85 ? "SISTEMA PROTETTO\n\nTutte le difese principali risultano operative." : "CONTROLLO RICHIESTO\n\nSono presenti impostazioni da verificare."
        });
        return panel;
    }

    private static Control ProtectionCards(SecurityState state)
    {
        TableLayoutPanel table = new() { Dock = DockStyle.Fill, ColumnCount = 7, RowCount = 1, Padding = new Padding(0, 4, 0, 4) };
        for (int i = 0; i < 7; i++) table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 7f));
        (string Name, bool Active)[] data =
        [
            ("Defender", state.Antivirus), ("Tempo reale", state.Realtime), ("Firewall", state.Firewall),
            ("Definizioni", state.Signatures), ("PUA", state.Pua), ("Protezione rete", state.Network), ("Ransomware", state.Ransomware)
        ];
        for (int i = 0; i < data.Length; i++)
        {
            Panel card = new() { Dock = DockStyle.Fill, Margin = new Padding(4), BackColor = Surface, Padding = new Padding(5) };
            card.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = $"{data[i].Name}\n{(data[i].Active ? "✓  ATTIVO" : "!  VERIFICARE")}",
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = data[i].Active ? Green : Orange
            });
            table.Controls.Add(card, i, 0);
        }
        return table;
    }

    private static Control ActivityCard(SecurityState state)
    {
        Panel panel = Card("ATTIVITÀ RECENTI");
        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            ForeColor = Color.Gainsboro,
            Font = new Font("Segoe UI", 10),
            Text = $"✓ Stato Defender verificato\n\n✓ Definizioni: {state.SignatureVersion}\n\n✓ Ultima rapida: {state.LastQuickScan}\n\n✓ Ultima completa: {state.LastFullScan}"
        });
        return panel;
    }

    private static Control AdviceCard(SecurityState state)
    {
        Panel panel = Card("AZIONI CONSIGLIATE");
        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            ForeColor = state.Issues.Count == 0 ? Green : Orange,
            Font = new Font("Segoe UI", 10),
            AutoEllipsis = true,
            Text = state.Issues.Count == 0 ? "✓ Nessun intervento urgente.\n\nIl sistema risulta protetto." : string.Join("\n\n", state.Issues.Select(x => "• " + x))
        });
        return panel;
    }

    private static Control InfoCard(SecurityState state)
    {
        Panel panel = Card("INFORMAZIONI SISTEMA");
        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(13),
            ForeColor = Color.Gainsboro,
            Font = new Font("Segoe UI", 9),
            AutoEllipsis = true,
            Text = $"Computer: {Environment.MachineName}\nUtente: {Environment.UserName}\nWindows: {Environment.OSVersion.Version}\n.NET: {Environment.Version}\n\nMotore Defender: {state.EngineVersion}\nDefinizioni: {state.SignatureVersion}"
        });
        return panel;
    }

    private void ShowScans()
    {
        Panel body = CreatePage("Centro Scansioni", "Analisi Microsoft Defender e controlli personalizzati");
        FlowLayoutPanel flow = ToolFlow();
        flow.Controls.Add(ToolCard("⚡", "Scansione rapida", "Controlla le aree più esposte del sistema.", "AVVIA SCANSIONE", () => RunAsync(_defender.QuickScanAsync, "Scansione rapida avviata.")));
        flow.Controls.Add(ToolCard("◉", "Scansione completa", "Analizza tutti i file e le unità disponibili.", "AVVIA SCANSIONE", () => RunAsync(_defender.FullScanAsync, "Scansione completa avviata.")));
        flow.Controls.Add(ToolCard("▣", "Cartella personalizzata", "Seleziona una cartella specifica da controllare.", "SELEZIONA CARTELLA", FolderScanAsync));
        flow.Controls.Add(ToolCard("⟳", "Aggiorna definizioni", "Scarica le firme più recenti di Microsoft Defender.", "AGGIORNA ORA", () => RunAsync(_defender.UpdateAsync, "Definizioni aggiornate.")));
        flow.Controls.Add(ToolCard("◈", "Sicurezza Windows", "Apri il centro sicurezza integrato di Windows.", "APRI SICUREZZA", () => { _defender.OpenWindowsSecurity(); return Task.CompletedTask; }));
        body.Controls.Add(flow);
    }

    private async Task ShowThreatsAsync()
    {
        Panel body = CreatePage("Minacce e Quarantena", "Cronologia delle rilevazioni Microsoft Defender");
        List<ThreatRow> data = await _defender.GetThreatsAsync();
        DataGridView grid = CreateGrid();
        grid.DataSource = data;
        body.Controls.Add(grid);
        _status.Text = $"{data.Count} rilevazioni caricate.";
    }

    private async Task ShowProtectionAsync()
    {
        Panel body = CreatePage("Centro Protezione", "Stato completo delle difese Windows");
        SecurityState state = await _defender.GetStateAsync();
        DataGridView grid = CreateGrid();
        grid.DataSource = new[]
        {
            new { Componente = "Microsoft Defender", Stato = state.Antivirus ? "Attivo" : "Disattivato" },
            new { Componente = "Protezione tempo reale", Stato = state.Realtime ? "Attiva" : "Disattivata" },
            new { Componente = "Definizioni", Stato = state.Signatures ? "Aggiornate" : "Da aggiornare" },
            new { Componente = "Firewall", Stato = state.Firewall ? "Attivo" : "Da verificare" },
            new { Componente = "Protezione PUA", Stato = state.Pua ? "Blocco" : "Non in blocco" },
            new { Componente = "Protezione rete", Stato = state.Network ? "Blocco" : "Non in blocco" },
            new { Componente = "Ransomware Guard", Stato = state.Ransomware ? "Attivo" : "Disattivato" }
        };
        body.Controls.Add(grid);
    }

    private void ShowNetwork()
    {
        Panel body = CreatePage("Rete e Firewall", "Controlli di rete, firewall e diagnostica connessioni");
        FlowLayoutPanel flow = ToolFlow();
        flow.Controls.Add(ToolCard("◎", "Stato Firewall", "Apri le impostazioni avanzate di Windows Firewall.", "APRI FIREWALL", () => OpenUriAsync("windowsdefender://network")));
        flow.Controls.Add(ToolCard("⌁", "Configurazione rete", "Visualizza indirizzi, gateway, DNS e schede attive.", "APRI DETTAGLI", () => CommandWindowAsync("ipconfig /all")));
        flow.Controls.Add(ToolCard("↔", "Test connettività", "Esegue ping e verifica la raggiungibilità Internet.", "AVVIA TEST", () => CommandWindowAsync("ping 1.1.1.1")));
        flow.Controls.Add(ToolCard("DNS", "Svuota cache DNS", "Rimuove la cache locale del resolver DNS.", "ESEGUI FLUSH", () => CommandWindowAsync("ipconfig /flushdns")));
        flow.Controls.Add(ToolCard("⚙", "Impostazioni proxy", "Apri la configurazione proxy di Windows.", "APRI PROXY", () => OpenUriAsync("ms-settings:network-proxy")));
        body.Controls.Add(flow);
    }

    private void ShowTools()
    {
        Panel body = CreatePage("Strumenti di Sistema", "Diagnostica, manutenzione e ripristino Windows");
        FlowLayoutPanel flow = ToolFlow();
        flow.Controls.Add(ToolCard(">_", "SFC /SCANNOW", "Verifica e ripara i file di sistema protetti.", "ESEGUI SFC", () => ToolAsync("sfc.exe", "/scannow")));
        flow.Controls.Add(ToolCard("⬡", "DISM RestoreHealth", "Ripristina l'immagine di Windows e i componenti danneggiati.", "ESEGUI DISM", () => ToolAsync("dism.exe", "/Online /Cleanup-Image /RestoreHealth")));
        flow.Controls.Add(ToolCard("⟳", "Windows Update", "Controlla e installa gli aggiornamenti disponibili.", "VERIFICA AGGIORNAMENTI", () => OpenUriAsync("ms-settings:windowsupdate")));
        flow.Controls.Add(ToolCard("▤", "Pulizia file temporanei", "Apre le opzioni di archiviazione e pulizia sicura.", "APRI PULIZIA", () => OpenUriAsync("ms-settings:storagesense")));
        flow.Controls.Add(ToolCard("⌁", "Diagnostica rete", "Mostra configurazione IP e verifica la connettività.", "AVVIA DIAGNOSTICA", () => CommandWindowAsync("ipconfig /all & ping 1.1.1.1")));
        flow.Controls.Add(ToolCard("▣", "Verifica disco", "Esegue CHKDSK in modalità scansione sull'unità C:.", "VERIFICA DISCO", () => CommandWindowAsync("chkdsk C: /scan")));
        flow.Controls.Add(ToolCard("◉", "Avvio automatico", "Gestisci le applicazioni che partono con Windows.", "APRI GESTIONE AVVIO", () => OpenUriAsync("ms-settings:startupapps")));
        flow.Controls.Add(ToolCard("◈", "Sicurezza Windows", "Apri tutte le impostazioni di sicurezza Microsoft.", "APRI SICUREZZA", () => { _defender.OpenWindowsSecurity(); return Task.CompletedTask; }));
        flow.Controls.Add(ToolCard("↶", "Ripristino configurazione", "Avvia il ripristino di sistema a un punto precedente.", "APRI RIPRISTINO", () => LaunchAsync("rstrui.exe")));
        body.Controls.Add(flow);
    }

    private void ShowQuarantine()
    {
        Panel body = CreatePage("Quarantena", "Gestione sicura degli elementi isolati");
        Panel card = Card("QUARANTENA MICROSOFT DEFENDER");
        card.Dock = DockStyle.Fill;
        card.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 14),
            ForeColor = Color.Silver,
            Text = "FF GUARDIAN utilizza la quarantena protetta di Microsoft Defender.\n\nApri la Cronologia protezione per visualizzare, ripristinare o eliminare gli elementi isolati."
        });
        Button open = CyberButton("APRI CRONOLOGIA PROTEZIONE", 360, 54, Blue);
        open.Dock = DockStyle.Bottom;
        open.Click += (_, _) => _defender.OpenWindowsSecurity();
        card.Controls.Add(open);
        body.Controls.Add(card);
    }

    private async Task ShowLogsAsync()
    {
        Panel body = CreatePage("Report e Registro", "Eventi operativi recenti di Microsoft Defender");
        List<EventRow> data = await _defender.GetOperationalEventsAsync();
        DataGridView grid = CreateGrid();
        grid.DataSource = data;
        body.Controls.Add(grid);
    }

    private void ShowInfo()
    {
        Panel body = CreatePage("Informazioni", "FF GUARDIAN Professional Security Suite");
        body.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Silver,
            Font = new Font("Segoe UI", 14),
            Text = "FF GUARDIAN 5.0.2\nNavigation & Tools Fix\n\nConsole avanzata per Microsoft Defender\nBy EL.CO di Francesco Fazzina"
        });
    }

    private static FlowLayoutPanel ToolFlow() => new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = true,
        WrapContents = true,
        Padding = new Padding(14),
        BackColor = Bg
    };

    private static Control ToolCard(string icon, string title, string description, string buttonText, Func<Task> action)
    {
        Panel card = new() { Width = 350, Height = 190, Margin = new Padding(9), BackColor = Surface, Padding = new Padding(16) };
        Label iconLabel = new() { Text = icon, Width = 62, Height = 62, Location = new Point(16, 18), ForeColor = Cyan, Font = new Font("Segoe UI Symbol", 24, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
        Label titleLabel = new() { Text = title, Location = new Point(88, 18), Size = new Size(240, 30), ForeColor = Color.White, Font = new Font("Segoe UI", 12, FontStyle.Bold) };
        Label descriptionLabel = new() { Text = description, Location = new Point(88, 52), Size = new Size(240, 58), ForeColor = Color.Silver, Font = new Font("Segoe UI", 9) };
        Button run = CyberButton(buttonText, 316, 42, Surface2);
        run.Location = new Point(17, 128);
        run.Click += async (_, _) => await action();
        card.Controls.Add(iconLabel);
        card.Controls.Add(titleLabel);
        card.Controls.Add(descriptionLabel);
        card.Controls.Add(run);
        return card;
    }

    private async Task FolderScanAsync()
    {
        using FolderBrowserDialog dialog = new() { Description = "Seleziona la cartella da analizzare" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            await RunAsync(() => _defender.CustomScanAsync(dialog.SelectedPath), "Scansione cartella avviata.");
    }

    private async Task EmergencyAsync()
    {
        DialogResult result = MessageBox.Show("Avviare aggiornamento firme e scansione rapida di emergenza?", "FF GUARDIAN", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (result != DialogResult.Yes) return;
        await RunAsync(async () => { await _defender.UpdateAsync(); await _defender.QuickScanAsync(); }, "Procedura di emergenza avviata.");
    }

    private static Task OpenUriAsync(string uri)
    {
        Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        return Task.CompletedTask;
    }

    private static Task LaunchAsync(string file)
    {
        Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
        return Task.CompletedTask;
    }

    private static Task CommandWindowAsync(string command)
    {
        Process.Start(new ProcessStartInfo("cmd.exe", $"/k {command}") { UseShellExecute = true, Verb = "runas" });
        return Task.CompletedTask;
    }

    private static async Task ToolAsync(string file, string args)
    {
        await Task.Run(() => Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = true, Verb = "runas" }));
    }

    private async Task RunAsync(Func<Task> action, string ok)
    {
        _status.Text = "Operazione in corso...";
        await action();
        _status.Text = ok;
        MessageBox.Show(ok, "FF GUARDIAN", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task SafeAsync(Func<Task> action)
    {
        try { await action(); }
        catch (Exception ex)
        {
            _status.Text = "Errore controllato";
            MessageBox.Show(ex.Message, "FF GUARDIAN", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static Panel Card(string title)
    {
        Panel panel = new() { Dock = DockStyle.Fill, Margin = new Padding(7), BackColor = Surface, Padding = new Padding(12) };
        panel.Controls.Add(new Label { Text = title, Dock = DockStyle.Top, Height = 30, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.White });
        return panel;
    }

    private static Button CyberButton(string text, int width, int height, Color color)
    {
        Button button = new()
        {
            Text = text,
            Width = width,
            Height = height,
            BackColor = color,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand,
            UseCompatibleTextRendering = true
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(0, 175, 255);
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(Math.Min(color.R + 20, 255), Math.Min(color.G + 20, 255), Math.Min(color.B + 20, 255));
        return button;
    }

    private static Button ActionButton(string text, Func<Task> action)
    {
        Button button = CyberButton(text, 190, 82, Surface2);
        button.Dock = DockStyle.Fill;
        button.Margin = new Padding(7);
        button.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        button.Click += async (_, _) => await action();
        return button;
    }

    private static DataGridView CreateGrid()
    {
        DataGridView grid = new()
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Bg,
            ForeColor = Color.White,
            RowHeadersVisible = false,
            BorderStyle = BorderStyle.None,
            AllowUserToAddRows = false
        };
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(0, 96, 180);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        grid.DefaultCellStyle.BackColor = Surface;
        grid.DefaultCellStyle.ForeColor = Color.White;
        grid.DefaultCellStyle.SelectionBackColor = Blue;
        grid.RowTemplate.Height = 36;
        return grid;
    }
}
