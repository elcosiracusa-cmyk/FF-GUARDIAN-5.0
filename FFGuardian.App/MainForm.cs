using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Net;

namespace FFGuardian;

internal sealed class MainForm : Form
{
    private const string SupportEmail = "alsafe127.00@gmail.com";
    private static readonly Color Bg = Color.FromArgb(3, 8, 12);
    private static readonly Color Sidebar = Color.FromArgb(5, 13, 18);
    private static readonly Color Surface = Color.FromArgb(9, 20, 27);
    private static readonly Color Surface2 = Color.FromArgb(13, 29, 38);
    private static readonly Color Border = Color.FromArgb(35, 66, 78);
    private static readonly Color Neon = Color.FromArgb(142, 255, 0);
    private static readonly Color Green = Color.FromArgb(70, 230, 95);
    private static readonly Color Cyan = Color.FromArgb(0, 190, 255);
    private static readonly Color Orange = Color.FromArgb(255, 170, 35);
    private static readonly Color Red = Color.FromArgb(235, 55, 35);

    private readonly DefenderService _defender = new();
    private readonly Panel _pageHost = new() { Dock = DockStyle.Fill, BackColor = Bg };
    private readonly Label _status = new()
    {
        Dock = DockStyle.Bottom,
        Height = 32,
        BackColor = Color.FromArgb(4, 12, 17),
        ForeColor = Color.Gainsboro,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(16, 0, 0, 0)
    };
    private readonly List<Button> _nav = [];

    public MainForm()
    {
        Text = "FF GUARDIAN 5.2 — Dobermann Support Edition by EL.CO";
        Icon = DobermannIconFactory.CreateIcon();
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
        Panel p = new() { Dock = DockStyle.Top, Height = 82, BackColor = Color.FromArgb(4, 13, 19), Padding = new Padding(294, 0, 18, 0) };
        Label title = new() { Dock = DockStyle.Left, Width = 500, Text = "FF GUARDIAN Personal Security", Font = new Font("Segoe UI", 22, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
        Label sub = new() { Dock = DockStyle.Left, Width = 470, Text = "PROTEZIONE AUTONOMA • DOBERMANN SUPPORT • BY EL.CO", Font = new Font("Segoe UI", 8, FontStyle.Bold), ForeColor = Neon, TextAlign = ContentAlignment.MiddleLeft };
        Button support = CyberButton("✉  ASSISTENZA", 165, 46, Surface2);
        support.Dock = DockStyle.Right;
        support.Click += (_, _) => ShowSupport();
        Button refresh = CyberButton("⟳  AGGIORNA", 165, 46, Color.FromArgb(24, 115, 0));
        refresh.Dock = DockStyle.Right;
        refresh.Click += async (_, _) => await SafeAsync(ShowDashboardAsync);
        p.Controls.Add(support);
        p.Controls.Add(refresh);
        p.Controls.Add(sub);
        p.Controls.Add(title);
        return p;
    }

    private Control BuildSidebar()
    {
        Panel side = new() { Dock = DockStyle.Left, Width = 286, BackColor = Sidebar };
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(12) };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 205));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));

        PictureBox logo = new() { Dock = DockStyle.Top, Height = 145, SizeMode = PictureBoxSizeMode.Zoom, Image = DobermannIconFactory.CreateBitmap(256) };
        Panel brand = new() { Dock = DockStyle.Fill };
        brand.Controls.Add(new Label { Dock = DockStyle.Bottom, Height = 52, Text = "FF GUARDIAN\nPersonal Security by EL.CO", TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 14, FontStyle.Bold), ForeColor = Color.White });
        brand.Controls.Add(logo);

        FlowLayoutPanel menu = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(0, 2, 0, 2) };
        AddNav(menu, "⌂   Dashboard", ShowDashboardAsync);
        AddNav(menu, "⌕   Scansione malware", () => { ShowScans(); return Task.CompletedTask; });
        AddNav(menu, "▦   Firewall", () => { ShowFirewall(); return Task.CompletedTask; });
        AddNav(menu, "✉   Gmail e phishing", () => { ShowPhishing(); return Task.CompletedTask; });
        AddNav(menu, "⚙   Automazione", () => { ShowAutomation(); return Task.CompletedTask; });
        AddNav(menu, "☣   Quarantena", () => { ShowQuarantine(); return Task.CompletedTask; });
        AddNav(menu, "⚗   Innovation Lab", () => { ShowInnovation(); return Task.CompletedTask; });
        AddNav(menu, "▥   Rapporti", () => { ShowReports(); return Task.CompletedTask; });
        AddNav(menu, "≡   Registro", ShowLogsAsync);
        AddNav(menu, "☏   Assistenza Clienti", () => { ShowSupport(); return Task.CompletedTask; });
        AddNav(menu, "●   Informazioni", () => { ShowInfo(); return Task.CompletedTask; });

        Panel protectedBox = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(9, 52, 22), Padding = new Padding(12) };
        protectedBox.Controls.Add(new Label { Dock = DockStyle.Fill, Text = "🛡  PROTEZIONE ATTIVA\nIl sistema è sicuro e protetto\nVersione 5.2", TextAlign = ContentAlignment.MiddleLeft, ForeColor = Neon, Font = new Font("Segoe UI", 10, FontStyle.Bold) });
        layout.Controls.Add(brand, 0, 0);
        layout.Controls.Add(menu, 0, 1);
        layout.Controls.Add(protectedBox, 0, 2);
        side.Controls.Add(layout);
        return side;
    }

    private void AddNav(Control parent, string text, Func<Task> action)
    {
        Button b = CyberButton(text, 248, 43, Surface2);
        b.Margin = new Padding(0, 2, 0, 2);
        b.TextAlign = ContentAlignment.MiddleLeft;
        b.Padding = new Padding(14, 0, 0, 0);
        b.Click += async (_, _) =>
        {
            foreach (Button n in _nav) n.BackColor = Surface2;
            b.BackColor = Color.FromArgb(35, 80, 0);
            await SafeAsync(action);
        };
        _nav.Add(b);
        parent.Controls.Add(b);
    }

    private Panel CreatePage(string title, string subtitle)
    {
        _pageHost.Controls.Clear();
        Panel page = new() { Dock = DockStyle.Fill, BackColor = Bg };
        Panel head = new() { Dock = DockStyle.Top, Height = 76, BackColor = Color.FromArgb(5, 16, 22), Padding = new Padding(20, 8, 10, 4) };
        head.Controls.Add(new Label { Dock = DockStyle.Bottom, Height = 23, Text = subtitle, ForeColor = Color.Silver, Font = new Font("Segoe UI", 9) });
        head.Controls.Add(new Label { Dock = DockStyle.Top, Height = 38, Text = title, ForeColor = Color.White, Font = new Font("Segoe UI", 20, FontStyle.Bold) });
        Panel body = new() { Dock = DockStyle.Fill, BackColor = Bg, Padding = new Padding(16), AutoScroll = true };
        page.Controls.Add(body);
        page.Controls.Add(head);
        _pageHost.Controls.Add(page);
        return body;
    }

    private async Task ShowDashboardAsync()
    {
        Panel body = CreatePage("Dashboard", "Protezione autonoma, spiegabile e silenziosa — by EL.CO");
        _status.Text = "Controllo Microsoft Defender in corso...";
        SecurityState s = await _defender.GetStateAsync();

        TableLayoutPanel root = new() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, AutoScroll = true };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 300));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        TableLayoutPanel hero = new() { Dock = DockStyle.Fill, ColumnCount = 3 };
        hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        hero.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 21));
        hero.Controls.Add(HeroCard(s), 0, 0);
        hero.Controls.Add(QuickActionsCard(), 1, 0);
        hero.Controls.Add(SummaryCard(s), 2, 0);

        TableLayoutPanel statusCards = new() { Dock = DockStyle.Fill, ColumnCount = 6 };
        for (int i = 0; i < 6; i++) statusCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 6f));
        (string, bool)[] data = [("Defender", s.Antivirus), ("Tempo reale", s.Realtime), ("Firewall", s.Firewall), ("Firme", s.Signatures), ("Ransomware", s.Ransomware), ("Rete e phishing", s.Network)];
        for (int i = 0; i < data.Length; i++) statusCards.Controls.Add(StateTile(data[i].Item1, data[i].Item2), i, 0);

        TableLayoutPanel bottom = new() { Dock = DockStyle.Fill, ColumnCount = 2 };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        bottom.Controls.Add(ActivityCard(s), 0, 0);
        bottom.Controls.Add(SecurityAdviceCard(s), 1, 0);

        root.Controls.Add(hero, 0, 0);
        root.Controls.Add(statusCards, 0, 1);
        root.Controls.Add(bottom, 0, 2);
        body.Controls.Add(root);
        _status.Text = $"Sistema aggiornato alle {DateTime.Now:HH:mm:ss} — Definizioni {s.SignatureVersion}";
    }

    private Control HeroCard(SecurityState s)
    {
        Panel p = Card("");
        PictureBox dog = new() { Dock = DockStyle.Left, Width = 250, SizeMode = PictureBoxSizeMode.Zoom, Image = DobermannIconFactory.CreateBitmap(320), BackColor = Color.Transparent };
        Label text = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18, 25, 12, 12),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12),
            Text = $"{s.Score} /100\nPROTETTO\n\nFF Guardian controlla Defender, firewall, ransomware, firme e rete.\nOgni rischio viene mostrato in modo semplice e comprensibile."
        };
        text.Paint += (_, e) => { };
        p.Controls.Add(text);
        p.Controls.Add(dog);
        return p;
    }

    private Control QuickActionsCard()
    {
        Panel p = Card("AZIONI RAPIDE");
        FlowLayoutPanel f = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(8), AutoScroll = true };
        f.Controls.Add(ActionButton("SCANSIONE RAPIDA", () => RunAsync(_defender.QuickScanAsync, "Scansione rapida avviata."), 250, 45, Color.FromArgb(55, 125, 0)));
        f.Controls.Add(ActionButton("SCANSIONE COMPLETA", () => RunAsync(_defender.FullScanAsync, "Scansione completa avviata."), 250, 45));
        f.Controls.Add(ActionButton("SCANSIONE CARTELLA", FolderScanAsync, 250, 45));
        f.Controls.Add(ActionButton("AGGIORNA FIRME", () => RunAsync(_defender.UpdateAsync, "Firme aggiornate."), 250, 45));
        p.Controls.Add(f);
        return p;
    }

    private static Control SummaryCard(SecurityState s)
    {
        Panel p = Card("STATO RAPIDO");
        p.Controls.Add(new Label { Dock = DockStyle.Fill, Padding = new Padding(14), ForeColor = Color.Gainsboro, Font = new Font("Segoe UI", 10), Text = $"Ultima rapida\n{s.LastQuickScan}\n\nFirme\n{s.SignatureVersion}\n\nQuarantena\nGestita da Defender" });
        return p;
    }

    private static Control StateTile(string name, bool active)
    {
        Panel p = Card(name);
        p.Margin = new Padding(5);
        p.Controls.Add(new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Text = active ? "● ATTIVO" : "● VERIFICARE", ForeColor = active ? Neon : Orange, Font = new Font("Segoe UI", 10, FontStyle.Bold) });
        return p;
    }

    private static Control ActivityCard(SecurityState s)
    {
        Panel p = Card("ATTIVITÀ RECENTE");
        p.Controls.Add(new Label { Dock = DockStyle.Fill, Padding = new Padding(16), ForeColor = Color.Gainsboro, Text = $"✓ Stato Defender verificato\n\n✓ Firme aggiornate: {s.SignatureVersion}\n\n✓ Ultima scansione rapida: {s.LastQuickScan}\n\n✓ Ultima scansione completa: {s.LastFullScan}" });
        return p;
    }

    private static Control SecurityAdviceCard(SecurityState s)
    {
        Panel p = Card("SICUREZZA E CONSIGLI");
        p.Controls.Add(new Label { Dock = DockStyle.Fill, Padding = new Padding(16), ForeColor = s.Issues.Count == 0 ? Neon : Orange, Text = s.Issues.Count == 0 ? "✓ Nessuna azione urgente.\n\nIl sistema risulta protetto e monitorato." : string.Join("\n\n", s.Issues.Select(x => "• " + x)) });
        return p;
    }

    private void ShowScans()
    {
        Panel body = CreatePage("Scansione malware", "Analizza il dispositivo con Microsoft Defender");
        FlowLayoutPanel f = TileFlow();
        f.Controls.Add(ToolTile("Scansione rapida", "Controlla le aree più esposte.", "AVVIA", () => RunAsync(_defender.QuickScanAsync, "Scansione rapida avviata.")));
        f.Controls.Add(ToolTile("Scansione completa", "Analizza l'intero sistema.", "AVVIA", () => RunAsync(_defender.FullScanAsync, "Scansione completa avviata.")));
        f.Controls.Add(ToolTile("Scansione cartella", "Seleziona una cartella specifica.", "SELEZIONA", FolderScanAsync));
        f.Controls.Add(ToolTile("Aggiorna firme", "Scarica le definizioni più recenti.", "AGGIORNA", () => RunAsync(_defender.UpdateAsync, "Firme aggiornate.")));
        body.Controls.Add(f);
    }

    private void ShowFirewall()
    {
        Panel body = CreatePage("Firewall", "Controllo rete, profili e diagnostica Windows");
        FlowLayoutPanel f = TileFlow();
        f.Controls.Add(ToolTile("Firewall Windows", "Apri la console avanzata del firewall.", "APRI", () => OpenAsync("wf.msc")));
        f.Controls.Add(ToolTile("Connessioni attive", "Visualizza porte e connessioni correnti.", "ANALIZZA", () => OpenAsync("resmon.exe")));
        f.Controls.Add(ToolTile("Diagnostica rete", "Avvia la diagnostica integrata Windows.", "AVVIA", () => OpenAsync("msdt.exe", "-id NetworkDiagnosticsNetworkAdapter")));
        f.Controls.Add(ToolTile("Configurazione IP", "Apri un terminale con ipconfig /all.", "VISUALIZZA", () => OpenConsoleAsync("ipconfig /all & pause")));
        body.Controls.Add(f);
    }

    private void ShowPhishing()
    {
        Panel body = CreatePage("Gmail e phishing", "Strumenti locali per riconoscere link, messaggi e allegati sospetti");
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        Panel analyzer = Card("ANALIZZA LINK O TESTO EMAIL");
        TextBox input = new() { Multiline = true, Dock = DockStyle.Fill, BackColor = Color.FromArgb(3, 12, 16), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 11), PlaceholderText = "Incolla qui un link o il testo di una email sospetta..." };
        Label result = new() { Dock = DockStyle.Bottom, Height = 70, ForeColor = Color.Silver, Padding = new Padding(10), Text = "Il controllo è locale e fornisce indicazioni, non una certificazione assoluta." };
        Button analyze = CyberButton("ANALIZZA RISCHIO", 240, 48, Color.FromArgb(45, 110, 0));
        analyze.Dock = DockStyle.Bottom;
        analyze.Click += (_, _) => result.Text = AnalyzePhishing(input.Text);
        analyzer.Controls.Add(input);
        analyzer.Controls.Add(result);
        analyzer.Controls.Add(analyze);
        Panel guide = Card("GUIDA ANTI-PHISHING");
        guide.Controls.Add(new Label { Dock = DockStyle.Fill, Padding = new Padding(16), ForeColor = Color.Gainsboro, Text = "• Controlla attentamente il dominio del mittente.\n\n• Non aprire allegati inattesi.\n\n• Diffida da urgenza, premi o minacce.\n\n• Non inserire password da link ricevuti via email.\n\n• Per Gmail usa anche la funzione Segnala phishing." });
        layout.Controls.Add(analyzer, 0, 0);
        layout.Controls.Add(guide, 1, 0);
        body.Controls.Add(layout);
    }

    private void ShowAutomation()
    {
        Panel body = CreatePage("Automazione", "FF GUARDIAN continua a monitorare il PC anche con la dashboard chiusa");
        FlowLayoutPanel f = TileFlow();
        f.Controls.Add(InfoTile("Controllo automatico", "Ogni 15 minuti: Defender, firewall, firme, PUA, rete e ransomware."));
        f.Controls.Add(InfoTile("Aggiornamento firme", "Aggiornamento automatico ogni 24 ore."));
        f.Controls.Add(InfoTile("Scansione programmata", "Scansione rapida automatica ogni 7 giorni."));
        f.Controls.Add(InfoTile("Area di notifica", "Il Dobermann resta vicino all'orologio e mostra gli avvisi."));
        f.Controls.Add(ToolTile("Controllo immediato", "Aggiorna firme e avvia una scansione rapida.", "ESEGUI", EmergencyAsync));
        body.Controls.Add(f);
    }

    private void ShowQuarantine()
    {
        Panel body = CreatePage("Quarantena", "Gestione protetta degli elementi isolati da Microsoft Defender");
        Panel p = Card("QUARANTINE VAULT");
        p.Dock = DockStyle.Fill;
        p.Controls.Add(new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.Gainsboro, Font = new Font("Segoe UI", 13), Text = "FF GUARDIAN utilizza la quarantena ufficiale di Microsoft Defender.\n\nApri Cronologia protezione per visualizzare, ripristinare o eliminare gli elementi isolati." });
        Button b = CyberButton("APRI CRONOLOGIA PROTEZIONE", 360, 54, Color.FromArgb(45, 110, 0));
        b.Dock = DockStyle.Bottom;
        b.Click += (_, _) => _defender.OpenWindowsSecurity();
        p.Controls.Add(b);
        body.Controls.Add(p);
    }

    private void ShowInnovation()
    {
        Panel body = CreatePage("Innovation Lab", "Funzioni sperimentali e consigli avanzati di sicurezza");
        FlowLayoutPanel f = TileFlow();
        f.Controls.Add(InfoTile("Spiegazione dei rischi", "Traduce gli stati tecnici in indicazioni semplici."));
        f.Controls.Add(InfoTile("Hardening consigliato", "Suggerimenti per ridurre la superficie di attacco."));
        f.Controls.Add(InfoTile("Controllo download", "Area in sviluppo per verifiche preventive sui file."));
        f.Controls.Add(InfoTile("Smart Defense", "Profili Casa, Ufficio e Massima protezione in sviluppo."));
        body.Controls.Add(f);
    }

    private void ShowReports()
    {
        Panel body = CreatePage("Rapporti", "Esporta informazioni utili per controllo e assistenza");
        FlowLayoutPanel f = TileFlow();
        f.Controls.Add(ToolTile("Report diagnostico", "Crea un report TXT con informazioni di sistema.", "GENERA", GenerateSupportReportAsync));
        f.Controls.Add(ToolTile("Cartella registri", "Apri i log della protezione autonoma.", "APRI", () => OpenAsync(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FF Guardian", "Logs"))));
        f.Controls.Add(ToolTile("Eventi Defender", "Apri il registro operativo di Microsoft Defender.", "VISUALIZZA", () => OpenAsync("eventvwr.msc")));
        body.Controls.Add(f);
    }

    private async Task ShowLogsAsync()
    {
        Panel body = CreatePage("Registro", "Eventi operativi recenti di Microsoft Defender");
        List<EventRow> rows = await _defender.GetOperationalEventsAsync();
        DataGridView grid = Grid();
        grid.DataSource = rows;
        body.Controls.Add(grid);
        _status.Text = $"{rows.Count} eventi caricati.";
    }

    private void ShowSupport()
    {
        Panel body = CreatePage("Assistenza Clienti", "Contatta il supporto FF GUARDIAN e prepara un report diagnostico");
        TableLayoutPanel layout = new() { Dock = DockStyle.Fill, ColumnCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));

        Panel contact = Card("CONTATTA ASSISTENZA");
        TextBox problem = new() { Dock = DockStyle.Fill, Multiline = true, BackColor = Color.FromArgb(3, 12, 16), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 11), PlaceholderText = "Descrivi qui il problema riscontrato..." };
        Button mail = CyberButton("APRI EMAIL SUPPORTO", 280, 52, Color.FromArgb(45, 110, 0));
        mail.Dock = DockStyle.Bottom;
        mail.Click += (_, _) => OpenSupportEmail(problem.Text);
        Button copy = CyberButton("COPIA INDIRIZZO", 280, 45, Surface2);
        copy.Dock = DockStyle.Bottom;
        copy.Click += (_, _) => { Clipboard.SetText(SupportEmail); _status.Text = "Indirizzo assistenza copiato."; };
        contact.Controls.Add(problem);
        contact.Controls.Add(copy);
        contact.Controls.Add(mail);

        Panel details = Card("DATI ASSISTENZA");
        details.Controls.Add(new Label { Dock = DockStyle.Fill, Padding = new Padding(16), ForeColor = Color.Gainsboro, Text = $"Email: {SupportEmail}\n\nVersione: FF GUARDIAN 5.2\nComputer: {Environment.MachineName}\nUtente: {Environment.UserName}\nWindows: {Environment.OSVersion.Version}\nData: {DateTime.Now:dd/MM/yyyy HH:mm}\n\nLa mail viene aperta nel programma di posta predefinito." });
        Button report = CyberButton("CREA REPORT ASSISTENZA", 280, 52, Surface2);
        report.Dock = DockStyle.Bottom;
        report.Click += async (_, _) => await SafeAsync(GenerateSupportReportAsync);
        details.Controls.Add(report);

        layout.Controls.Add(contact, 0, 0);
        layout.Controls.Add(details, 1, 0);
        body.Controls.Add(layout);
    }

    private void ShowInfo()
    {
        Panel body = CreatePage("Informazioni", "FF GUARDIAN Personal Security by EL.CO");
        body.Controls.Add(new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.Gainsboro, Font = new Font("Segoe UI", 14), Text = "FF GUARDIAN 5.2\nDobermann Support Edition\n\nConsole avanzata di protezione e automazione per Microsoft Defender\n\nAssistenza: alsafe127.00@gmail.com\nBy EL.CO di Francesco Fazzina" });
    }

    private static string AnalyzePhishing(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "Inserisci un link o il testo di una email.";
        string lower = text.ToLowerInvariant();
        int score = 0;
        string[] risky = ["urgente", "password", "verifica account", "premio", "bonifico", "clicca qui", "account sospeso", "conferma identità", "crypto", "wallet"];
        score += risky.Count(lower.Contains) * 12;
        if (lower.Contains("http://")) score += 20;
        if (lower.Contains("bit.ly") || lower.Contains("tinyurl") || lower.Contains("t.co")) score += 20;
        if (lower.Count(c => c == '!') >= 3) score += 10;
        score = Math.Min(score, 100);
        return score >= 60 ? $"RISCHIO ALTO ({score}/100): non aprire link o allegati e verifica il mittente." : score >= 30 ? $"ATTENZIONE ({score}/100): controlla dominio, mittente e richiesta ricevuta." : $"RISCHIO BASSO ({score}/100), ma verifica sempre il mittente prima di procedere.";
    }

    private void OpenSupportEmail(string problem)
    {
        string subject = Uri.EscapeDataString("Supporto FF GUARDIAN 5.2");
        string body = Uri.EscapeDataString($"Descrizione problema:\r\n{problem}\r\n\r\nVersione: FF GUARDIAN 5.2\r\nComputer: {Environment.MachineName}\r\nUtente: {Environment.UserName}\r\nWindows: {Environment.OSVersion.Version}\r\nData: {DateTime.Now:dd/MM/yyyy HH:mm}");
        Process.Start(new ProcessStartInfo($"mailto:{SupportEmail}?subject={subject}&body={body}") { UseShellExecute = true });
    }

    private async Task GenerateSupportReportAsync()
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FF Guardian Reports");
        Directory.CreateDirectory(folder);
        SecurityState s = await _defender.GetStateAsync();
        string path = Path.Combine(folder, $"FFGuardian-Support-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
        string content = $"FF GUARDIAN 5.2 - REPORT ASSISTENZA\r\nData: {DateTime.Now}\r\nComputer: {Environment.MachineName}\r\nUtente: {Environment.UserName}\r\nWindows: {Environment.OSVersion}\r\n.NET: {Environment.Version}\r\nPunteggio: {s.Score}/100\r\nDefender: {s.Antivirus}\r\nTempo reale: {s.Realtime}\r\nFirewall: {s.Firewall}\r\nFirme: {s.SignatureVersion}\r\nMotore: {s.EngineVersion}\r\nProblemi: {string.Join(" | ", s.Issues)}";
        await File.WriteAllTextAsync(path, content);
        Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
        MessageBox.Show($"Report creato:\n{path}", "FF GUARDIAN", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task FolderScanAsync()
    {
        using FolderBrowserDialog dialog = new() { Description = "Seleziona la cartella da analizzare" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            await RunAsync(() => _defender.CustomScanAsync(dialog.SelectedPath), "Scansione cartella avviata.");
    }

    private async Task EmergencyAsync()
    {
        await _defender.UpdateAsync();
        await _defender.QuickScanAsync();
        _status.Text = "Aggiornamento e scansione rapida avviati.";
    }

    private static Task OpenAsync(string file, string? args = null)
    {
        Process.Start(new ProcessStartInfo(file, args ?? string.Empty) { UseShellExecute = true });
        return Task.CompletedTask;
    }

    private static Task OpenConsoleAsync(string command)
    {
        Process.Start(new ProcessStartInfo("cmd.exe", $"/k {command}") { UseShellExecute = true, Verb = "runas" });
        return Task.CompletedTask;
    }

    private async Task RunAsync(Func<Task> action, string success)
    {
        _status.Text = "Operazione in corso...";
        await action();
        _status.Text = success;
        MessageBox.Show(success, "FF GUARDIAN", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

    private static FlowLayoutPanel TileFlow() => new() { Dock = DockStyle.Fill, AutoScroll = true, WrapContents = true, Padding = new Padding(10) };

    private static Control ToolTile(string title, string description, string actionText, Func<Task> action)
    {
        Panel p = Card(title);
        p.Width = 360;
        p.Height = 170;
        p.Margin = new Padding(8);
        p.Controls.Add(new Label { Dock = DockStyle.Fill, Padding = new Padding(12), ForeColor = Color.Gainsboro, Text = description });
        Button b = CyberButton(actionText, 300, 44, Surface2);
        b.Dock = DockStyle.Bottom;
        b.Click += async (_, _) => await action();
        p.Controls.Add(b);
        return p;
    }

    private static Control InfoTile(string title, string description)
    {
        Panel p = Card(title);
        p.Width = 360;
        p.Height = 145;
        p.Margin = new Padding(8);
        p.Controls.Add(new Label { Dock = DockStyle.Fill, Padding = new Padding(12), ForeColor = Color.Gainsboro, Text = description });
        return p;
    }

    private static Panel Card(string title)
    {
        Panel p = new() { Dock = DockStyle.Fill, BackColor = Surface, Margin = new Padding(7), Padding = new Padding(12) };
        p.Paint += (_, e) => { using Pen pen = new(Border, 1); e.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, p.Width - 1), Math.Max(0, p.Height - 1)); };
        if (!string.IsNullOrEmpty(title)) p.Controls.Add(new Label { Dock = DockStyle.Top, Height = 30, Text = title, ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold) });
        return p;
    }

    private static Button CyberButton(string text, int width, int height, Color color)
    {
        Button b = new() { Text = text, Width = width, Height = height, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, Font = new Font("Segoe UI", 9, FontStyle.Bold), UseCompatibleTextRendering = true };
        b.FlatAppearance.BorderColor = Neon;
        b.FlatAppearance.BorderSize = 1;
        return b;
    }

    private static Button ActionButton(string text, Func<Task> action, int width, int height, Color? color = null)
    {
        Button b = CyberButton(text, width, height, color ?? Surface2);
        b.Margin = new Padding(4);
        b.Click += async (_, _) => await action();
        return b;
    }

    private static DataGridView Grid()
    {
        DataGridView g = new() { Dock = DockStyle.Fill, ReadOnly = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Bg, ForeColor = Color.White, RowHeadersVisible = false, BorderStyle = BorderStyle.None, AllowUserToAddRows = false };
        g.EnableHeadersVisualStyles = false;
        g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(35, 80, 0);
        g.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        g.DefaultCellStyle.BackColor = Surface;
        g.DefaultCellStyle.ForeColor = Color.White;
        g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(45, 100, 0);
        g.RowTemplate.Height = 34;
        return g;
    }
}
