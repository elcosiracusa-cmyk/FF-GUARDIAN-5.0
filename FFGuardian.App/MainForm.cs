using System.Drawing.Drawing2D;

namespace FFGuardian;

internal sealed class MainForm : Form
{
    private static readonly Color Bg = Color.FromArgb(5, 11, 20);
    private static readonly Color Surface = Color.FromArgb(12, 27, 44);
    private static readonly Color Surface2 = Color.FromArgb(17, 39, 62);
    private static readonly Color Blue = Color.FromArgb(0, 122, 255);
    private static readonly Color Cyan = Color.FromArgb(0, 215, 255);
    private static readonly Color Green = Color.FromArgb(70, 226, 118);
    private static readonly Color Orange = Color.FromArgb(255, 174, 48);
    private static readonly Color Red = Color.FromArgb(244, 72, 82);

    private readonly DefenderService _defender = new();
    private readonly Panel _content = new() { Dock = DockStyle.Fill, BackColor = Bg };
    private readonly Label _status = new()
    {
        Dock = DockStyle.Bottom,
        Height = 30,
        ForeColor = Color.Silver,
        BackColor = Color.FromArgb(5, 15, 26),
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(15, 0, 0, 0)
    };
    private readonly List<Button> _nav = [];

    public MainForm()
    {
        Text = "FF GUARDIAN 5.0.1 — Professional UI Fix by EL.CO";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1280, 800);
        BackColor = Bg;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);
        DoubleBuffered = true;
        Controls.Add(_content);
        Controls.Add(BuildSidebar());
        Controls.Add(BuildHeader());
        Controls.Add(_status);
        Shown += async (_, _) => await SafeAsync(ShowDashboardAsync);
    }

    private Control BuildHeader()
    {
        Panel p = new() { Dock = DockStyle.Top, Height = 82, BackColor = Color.FromArgb(6, 17, 30), Padding = new Padding(304, 0, 18, 0) };
        Label title = new()
        {
            Text = "FF GUARDIAN 5.0.1",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            Dock = DockStyle.Left,
            Width = 410,
            TextAlign = ContentAlignment.MiddleLeft
        };
        Label subtitle = new()
        {
            Text = "PROFESSIONAL SECURITY SUITE  •  BY EL.CO",
            ForeColor = Cyan,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Dock = DockStyle.Left,
            Width = 390,
            TextAlign = ContentAlignment.MiddleLeft
        };
        Button emergency = Button("⚠  EMERGENZA", 178, 46, Red);
        emergency.Dock = DockStyle.Right;
        emergency.Click += async (_, _) => await EmergencyAsync();
        Button refresh = Button("⟳  AGGIORNA", 166, 46, Blue);
        refresh.Dock = DockStyle.Right;
        refresh.Click += async (_, _) => await SafeAsync(ShowDashboardAsync);
        p.Controls.Add(emergency);
        p.Controls.Add(refresh);
        p.Controls.Add(subtitle);
        p.Controls.Add(title);
        return p;
    }

    private Control BuildSidebar()
    {
        Panel p = new() { Dock = DockStyle.Left, Width = 286, BackColor = Color.FromArgb(5, 17, 29), Padding = new Padding(14) };
        Panel brand = new() { Dock = DockStyle.Top, Height = 230 };
        brand.Paint += (_, e) => PaintBrand(e.Graphics, brand.ClientRectangle);
        p.Controls.Add(brand);

        FlowLayoutPanel menu = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(0, 8, 0, 0)
        };
        AddNav(menu, "⌂   Dashboard", ShowDashboardAsync);
        AddNav(menu, "⌕   Scansioni", () => { ShowScans(); return Task.CompletedTask; });
        AddNav(menu, "⚠   Minacce", ShowThreatsAsync);
        AddNav(menu, "◈   Centro Protezione", ShowProtectionAsync);
        AddNav(menu, "▣   Quarantena", () => { ShowQuarantine(); return Task.CompletedTask; });
        AddNav(menu, "⚙   Strumenti Sistema", () => { ShowTools(); return Task.CompletedTask; });
        AddNav(menu, "≡   Report e Registro", ShowLogsAsync);
        AddNav(menu, "●   Informazioni", () => { ShowInfo(); return Task.CompletedTask; });
        p.Controls.Add(menu);
        return p;
    }

    private static void PaintBrand(Graphics g, Rectangle bounds)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
        PointF[] shield =
        [
            new(143, 8), new(225, 42), new(212, 146),
            new(143, 196), new(74, 146), new(61, 42)
        ];
        using Pen glow = new(Cyan, 4);
        g.DrawPolygon(glow, shield);

        using GraphicsPath head = new();
        head.AddPolygon([
            new PointF(102, 56), new PointF(116, 34), new PointF(130, 62),
            new PointF(155, 58), new PointF(171, 32), new PointF(184, 61),
            new PointF(178, 116), new PointF(160, 139), new PointF(143, 150),
            new PointF(125, 139), new PointF(107, 116)
        ]);
        using SolidBrush face = new(Color.FromArgb(225, 238, 246));
        g.FillPath(face, head);
        using Pen dark = new(Color.FromArgb(35, 49, 64), 3);
        g.DrawPath(dark, head);
        using SolidBrush mask = new(Color.FromArgb(42, 55, 70));
        g.FillPolygon(mask, [new PointF(112, 61), new PointF(139, 85), new PointF(126, 116), new PointF(108, 104)]);
        g.FillPolygon(mask, [new PointF(174, 61), new PointF(147, 85), new PointF(160, 116), new PointF(178, 104)]);
        using SolidBrush eye = new(Orange);
        g.FillEllipse(eye, 121, 83, 8, 6);
        g.FillEllipse(eye, 157, 83, 8, 6);
        g.FillPolygon(Brushes.Black, [new PointF(136, 119), new PointF(150, 119), new PointF(143, 129)]);

        using Font title = new("Segoe UI", 18, FontStyle.Bold);
        using Font sub = new("Segoe UI", 10, FontStyle.Bold);
        string name = "FF GUARDIAN";
        SizeF size = g.MeasureString(name, title);
        g.DrawString(name, title, Brushes.White, 143 - size.Width / 2, 177);
        string by = "BY EL.CO";
        SizeF bySize = g.MeasureString(by, sub);
        g.DrawString(by, sub, Brushes.DeepSkyBlue, 143 - bySize.Width / 2, 206);
    }

    private void AddNav(Control parent, string text, Func<Task> action)
    {
        Button b = Button(text, 246, 50, Surface2);
        b.Margin = new Padding(0, 3, 0, 3);
        b.TextAlign = ContentAlignment.MiddleLeft;
        b.Padding = new Padding(15, 0, 0, 0);
        b.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        b.Click += async (_, _) =>
        {
            _nav.ForEach(x => x.BackColor = Surface2);
            b.BackColor = Blue;
            await SafeAsync(action);
        };
        _nav.Add(b);
        parent.Controls.Add(b);
    }

    private async Task ShowDashboardAsync()
    {
        Clear("Dashboard di Protezione", "Stato completo e aggiornato della sicurezza del sistema");
        _status.Text = "Lettura dello stato Microsoft Defender...";
        SecurityState s = await _defender.GetStateAsync();

        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(18),
            AutoScroll = true
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 300));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        TableLayoutPanel top = new() { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(0) };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27));
        top.Controls.Add(ScoreCard(s), 0, 0);
        top.Controls.Add(QuickCard(), 1, 0);
        top.Controls.Add(BrandCard(s), 2, 0);

        root.Controls.Add(top, 0, 0);
        root.Controls.Add(ProtectionCards(s), 0, 1);

        TableLayoutPanel bottom = new() { Dock = DockStyle.Fill, ColumnCount = 3, Padding = new Padding(0) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27));
        bottom.Controls.Add(ActivityCard(s), 0, 0);
        bottom.Controls.Add(AdviceCard(s), 1, 0);
        bottom.Controls.Add(InfoCard(s), 2, 0);
        root.Controls.Add(bottom, 0, 2);

        _content.Controls.Add(root);
        root.BringToFront();
        _status.Text = $"Sistema aggiornato alle {DateTime.Now:HH:mm:ss} — Definizioni {s.SignatureVersion}";
    }

    private Control ScoreCard(SecurityState s)
    {
        Panel p = Card("PUNTEGGIO DI PROTEZIONE");
        p.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new(38, 58, 190, 190);
            using Pen basePen = new(Color.FromArgb(35, 70, 95), 17);
            using Pen scorePen = new(s.Score >= 85 ? Green : s.Score >= 65 ? Orange : Red, 17)
            { StartCap = LineCap.Round, EndCap = LineCap.Round };
            e.Graphics.DrawArc(basePen, r, 135, 270);
            e.Graphics.DrawArc(scorePen, r, 135, 270 * s.Score / 100f);
            using Font f = new("Segoe UI", 42, FontStyle.Bold);
            string text = s.Score.ToString();
            SizeF z = e.Graphics.MeasureString(text, f);
            e.Graphics.DrawString(text, f, Brushes.White, 133 - z.Width / 2, 112);
            using Font small = new("Segoe UI", 12, FontStyle.Bold);
            e.Graphics.DrawString("/100", small, Brushes.Silver, 113, 171);
            string status = s.Score >= 85 ? "PROTEZIONE ELEVATA" : s.Score >= 65 ? "DA MIGLIORARE" : "INTERVENTO NECESSARIO";
            using SolidBrush statusBrush = new(s.Score >= 85 ? Green : s.Score >= 65 ? Orange : Red);
            e.Graphics.DrawString(status, small, statusBrush, 35, 253);
        };
        return p;
    }

    private Control QuickCard()
    {
        Panel p = Card("AZIONI RAPIDE");
        TableLayoutPanel g = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(10) };
        g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        g.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        g.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        g.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        g.Controls.Add(Action("⚡  Scansione rapida", () => RunAsync(_defender.QuickScanAsync, "Scansione rapida avviata.")), 0, 0);
        g.Controls.Add(Action("◉  Scansione completa", () => RunAsync(_defender.FullScanAsync, "Scansione completa avviata.")), 1, 0);
        g.Controls.Add(Action("▣  Scansiona cartella", FolderScanAsync), 0, 1);
        g.Controls.Add(Action("⟳  Aggiorna definizioni", () => RunAsync(_defender.UpdateAsync, "Definizioni aggiornate.")), 1, 1);
        p.Controls.Add(g);
        return p;
    }

    private static Control BrandCard(SecurityState s)
    {
        Panel p = Card("STATO GENERALE");
        Label l = new()
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 14, FontStyle.Bold),
            ForeColor = s.Score >= 85 ? Green : Orange,
            Padding = new Padding(12),
            Text = s.Score >= 85
                ? "SISTEMA PROTETTO\n\nTutte le difese principali risultano operative."
                : "CONTROLLO RICHIESTO\n\nSono presenti impostazioni da verificare."
        };
        p.Controls.Add(l);
        return p;
    }

    private static Control ProtectionCards(SecurityState s)
    {
        TableLayoutPanel table = new() { Dock = DockStyle.Fill, ColumnCount = 7, RowCount = 1, Padding = new Padding(0, 5, 0, 5) };
        for (int i = 0; i < 7; i++) table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 7f));
        (string Name, bool Active)[] data =
        [
            ("Defender", s.Antivirus), ("Tempo reale", s.Realtime), ("Firewall", s.Firewall),
            ("Definizioni", s.Signatures), ("PUA", s.Pua), ("Protezione rete", s.Network), ("Ransomware", s.Ransomware)
        ];
        for (int i = 0; i < data.Length; i++)
        {
            Panel card = new() { Dock = DockStyle.Fill, Margin = new Padding(5), BackColor = Surface, Padding = new Padding(6) };
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

    private static Control ActivityCard(SecurityState s)
    {
        Panel p = Card("ATTIVITÀ RECENTI");
        p.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ForeColor = Color.Gainsboro,
            Font = new Font("Segoe UI", 10),
            Text = $"✓ Stato Defender verificato\n\n✓ Definizioni: {s.SignatureVersion}\n\n✓ Ultima rapida: {s.LastQuickScan}\n\n✓ Ultima completa: {s.LastFullScan}"
        });
        return p;
    }

    private static Control AdviceCard(SecurityState s)
    {
        Panel p = Card("AZIONI CONSIGLIATE");
        p.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ForeColor = s.Issues.Count == 0 ? Green : Orange,
            Font = new Font("Segoe UI", 10),
            AutoEllipsis = true,
            Text = s.Issues.Count == 0 ? "✓ Nessun intervento urgente.\n\nIl sistema risulta protetto." : string.Join("\n\n", s.Issues.Select(x => "• " + x))
        });
        return p;
    }

    private static Control InfoCard(SecurityState s)
    {
        Panel p = Card("INFORMAZIONI SISTEMA");
        p.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            ForeColor = Color.Gainsboro,
            Font = new Font("Segoe UI", 9),
            AutoEllipsis = true,
            Text = $"Computer: {Environment.MachineName}\nUtente: {Environment.UserName}\nWindows: {Environment.OSVersion.Version}\n.NET: {Environment.Version}\n\nMotore Defender: {s.EngineVersion}\nDefinizioni: {s.SignatureVersion}"
        });
        return p;
    }

    private void ShowScans()
    {
        Clear("Centro Scansioni", "Analisi Microsoft Defender e controlli personalizzati");
        FlowLayoutPanel f = new() { Dock = DockStyle.Fill, Padding = new Padding(35), AutoScroll = true };
        f.Controls.Add(Action("⚡  SCANSIONE RAPIDA", () => RunAsync(_defender.QuickScanAsync, "Scansione rapida avviata."), 320, 115));
        f.Controls.Add(Action("◉  SCANSIONE COMPLETA", () => RunAsync(_defender.FullScanAsync, "Scansione completa avviata."), 320, 115));
        f.Controls.Add(Action("▣  CARTELLA PERSONALIZZATA", FolderScanAsync, 320, 115));
        f.Controls.Add(Action("⟳  AGGIORNA DEFINIZIONI", () => RunAsync(_defender.UpdateAsync, "Definizioni aggiornate."), 320, 115));
        f.Controls.Add(Action("◈  SICUREZZA WINDOWS", () => { _defender.OpenWindowsSecurity(); return Task.CompletedTask; }, 320, 115));
        _content.Controls.Add(f);
        f.BringToFront();
    }

    private async Task ShowThreatsAsync()
    {
        Clear("Minacce rilevate", "Cronologia delle rilevazioni Microsoft Defender");
        List<ThreatRow> d = await _defender.GetThreatsAsync();
        DataGridView g = Grid();
        g.DataSource = d;
        _content.Controls.Add(g);
        g.BringToFront();
        _status.Text = $"{d.Count} rilevazioni caricate.";
    }

    private async Task ShowProtectionAsync()
    {
        Clear("Centro Protezione", "Stato completo delle difese Windows");
        SecurityState s = await _defender.GetStateAsync();
        DataGridView g = Grid();
        g.DataSource = new[]
        {
            new { Componente = "Microsoft Defender", Stato = s.Antivirus ? "Attivo" : "Disattivato" },
            new { Componente = "Protezione tempo reale", Stato = s.Realtime ? "Attiva" : "Disattivata" },
            new { Componente = "Definizioni", Stato = s.Signatures ? "Aggiornate" : "Da aggiornare" },
            new { Componente = "Firewall", Stato = s.Firewall ? "Attivo" : "Da verificare" },
            new { Componente = "Protezione PUA", Stato = s.Pua ? "Blocco" : "Non in blocco" },
            new { Componente = "Protezione rete", Stato = s.Network ? "Blocco" : "Non in blocco" },
            new { Componente = "Ransomware Guard", Stato = s.Ransomware ? "Attivo" : "Disattivato" }
        };
        _content.Controls.Add(g);
        g.BringToFront();
    }

    private void ShowQuarantine()
    {
        Clear("Quarantena", "Gestione sicura degli elementi isolati");
        Panel p = Card("QUARANTENA MICROSOFT DEFENDER");
        p.Dock = DockStyle.Fill;
        p.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 14),
            ForeColor = Color.Silver,
            Text = "FF GUARDIAN utilizza la quarantena protetta di Microsoft Defender.\n\nApri la Cronologia protezione per visualizzare, ripristinare o eliminare gli elementi isolati."
        });
        Button b = Button("APRI CRONOLOGIA PROTEZIONE", 340, 54, Blue);
        b.Dock = DockStyle.Bottom;
        b.Click += (_, _) => _defender.OpenWindowsSecurity();
        p.Controls.Add(b);
        _content.Controls.Add(p);
        p.BringToFront();
    }

    private void ShowTools()
    {
        Clear("Strumenti di Sistema", "Diagnostica, riparazione e manutenzione Windows");
        FlowLayoutPanel f = new() { Dock = DockStyle.Fill, Padding = new Padding(35), AutoScroll = true };
        f.Controls.Add(Action("SFC /SCANNOW", () => ToolAsync("sfc.exe", "/scannow"), 300, 110));
        f.Controls.Add(Action("DISM RESTOREHEALTH", () => ToolAsync("dism.exe", "/Online /Cleanup-Image /RestoreHealth"), 300, 110));
        f.Controls.Add(Action("WINDOWS UPDATE", () => { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("ms-settings:windowsupdate") { UseShellExecute = true }); return Task.CompletedTask; }, 300, 110));
        f.Controls.Add(Action("SICUREZZA WINDOWS", () => { _defender.OpenWindowsSecurity(); return Task.CompletedTask; }, 300, 110));
        f.Controls.Add(Action("GESTIONE ATTIVITÀ", () => { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("taskmgr.exe") { UseShellExecute = true }); return Task.CompletedTask; }, 300, 110));
        _content.Controls.Add(f);
        f.BringToFront();
    }

    private async Task ShowLogsAsync()
    {
        Clear("Report e Registro", "Eventi operativi recenti di Microsoft Defender");
        List<EventRow> d = await _defender.GetOperationalEventsAsync();
        DataGridView g = Grid();
        g.DataSource = d;
        _content.Controls.Add(g);
        g.BringToFront();
    }

    private void ShowInfo()
    {
        Clear("Informazioni", "FF GUARDIAN Professional Security Suite");
        Label l = new()
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Silver,
            Font = new Font("Segoe UI", 14),
            Text = "FF GUARDIAN 5.0.1\nProfessional UI Fix\n\nConsole avanzata per Microsoft Defender\nBy EL.CO di Francesco Fazzina"
        };
        _content.Controls.Add(l);
        l.BringToFront();
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

    private async Task ToolAsync(string file, string args)
    {
        await Task.Run(() => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(file, args) { UseShellExecute = true, Verb = "runas" }));
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

    private void Clear(string title, string subtitle)
    {
        _content.Controls.Clear();
        Panel h = new() { Dock = DockStyle.Top, Height = 76, BackColor = Color.FromArgb(7, 20, 34), Padding = new Padding(20, 10, 10, 6) };
        h.Controls.Add(new Label { Text = subtitle, Dock = DockStyle.Bottom, Height = 24, ForeColor = Color.Silver, Font = new Font("Segoe UI", 9) });
        h.Controls.Add(new Label { Text = title, Dock = DockStyle.Top, Height = 36, Font = new Font("Segoe UI", 19, FontStyle.Bold), ForeColor = Color.White });
        _content.Controls.Add(h);
        h.BringToFront();
    }

    private static Panel Card(string title)
    {
        Panel p = new() { Dock = DockStyle.Fill, Margin = new Padding(7), BackColor = Surface, Padding = new Padding(12) };
        p.Controls.Add(new Label { Text = title, Dock = DockStyle.Top, Height = 30, Font = new Font("Segoe UI", 10, FontStyle.Bold), ForeColor = Color.White });
        return p;
    }

    private static Button Button(string text, int width, int height, Color color)
    {
        Button b = new()
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
        b.FlatAppearance.BorderColor = Color.FromArgb(0, 175, 255);
        b.FlatAppearance.BorderSize = 1;
        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(Math.Min(color.R + 20, 255), Math.Min(color.G + 20, 255), Math.Min(color.B + 20, 255));
        return b;
    }

    private static Button Action(string text, Func<Task> action, int width = 190, int height = 82)
    {
        Button b = Button(text, width, height, Surface2);
        b.Dock = DockStyle.Fill;
        b.Margin = new Padding(8);
        b.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        b.Click += async (_, _) => await action();
        return b;
    }

    private static DataGridView Grid()
    {
        DataGridView g = new()
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
        g.EnableHeadersVisualStyles = false;
        g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(0, 96, 180);
        g.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        g.DefaultCellStyle.BackColor = Surface;
        g.DefaultCellStyle.ForeColor = Color.White;
        g.DefaultCellStyle.SelectionBackColor = Blue;
        g.RowTemplate.Height = 36;
        return g;
    }
}
