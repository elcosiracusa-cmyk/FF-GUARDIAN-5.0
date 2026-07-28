using System.Drawing.Drawing2D;

namespace FFGuardian;

internal sealed class MainForm : Form
{
    private readonly DefenderService _defender = new();
    private readonly Panel _content = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(6, 15, 26) };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 28, ForeColor = Color.Silver, BackColor = Color.FromArgb(8, 20, 34), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(12, 0, 0, 0) };
    private readonly List<Button> _nav = [];

    public MainForm()
    {
        Text = "FF GUARDIAN 5.0 Beta — By EL.CO";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1180, 760);
        BackColor = Color.FromArgb(4, 12, 22);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);
        Controls.Add(_content);
        Controls.Add(BuildSidebar());
        Controls.Add(BuildHeader());
        Controls.Add(_status);
        Shown += async (_, _) => await ShowDashboardAsync();
    }

    private Control BuildHeader()
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = 72, BackColor = Color.FromArgb(7, 17, 29), Padding = new Padding(300, 0, 18, 0) };
        panel.Controls.Add(new Label { Text = "FF GUARDIAN 5.0 BETA", ForeColor = Color.White, Font = new Font("Segoe UI", 21, FontStyle.Bold), Dock = DockStyle.Left, Width = 390, TextAlign = ContentAlignment.MiddleLeft });
        var refresh = CyberButton("⟳  AGGIORNA SISTEMA", 190, 42, Color.FromArgb(0, 102, 220));
        refresh.Dock = DockStyle.Right;
        refresh.Click += async (_, _) => await ShowDashboardAsync();
        panel.Controls.Add(refresh);
        return panel;
    }

    private Control BuildSidebar()
    {
        var panel = new Panel { Dock = DockStyle.Left, Width = 280, BackColor = Color.FromArgb(5, 18, 31), Padding = new Padding(14) };
        var brand = new Panel { Dock = DockStyle.Top, Height = 190 };
        brand.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var glow = new Pen(Color.FromArgb(0, 170, 255), 4);
            var points = new[] { new Point(140, 12), new Point(215, 45), new Point(205, 130), new Point(140, 172), new Point(75, 130), new Point(65, 45) };
            e.Graphics.DrawPolygon(glow, points);
            using var f1 = new Font("Segoe UI", 34, FontStyle.Bold);
            e.Graphics.DrawString("D", f1, Brushes.White, 101, 48);
            using var f2 = new Font("Segoe UI", 17, FontStyle.Bold);
            e.Graphics.DrawString("FFGuardian", f2, Brushes.White, 67, 120);
            using var f3 = new Font("Segoe UI", 11, FontStyle.Bold);
            e.Graphics.DrawString("By EL.CO", f3, Brushes.DeepSkyBlue, 105, 151);
        };
        panel.Controls.Add(brand);

        var menu = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(0, 8, 0, 0) };
        AddNav(menu, "⌂  Dashboard", ShowDashboardAsync);
        AddNav(menu, "⌕  Scansioni", () => { ShowScanPage(); return Task.CompletedTask; });
        AddNav(menu, "⚠  Minacce", ShowThreatsAsync);
        AddNav(menu, "▣  Quarantena", () => { ShowQuarantine(); return Task.CompletedTask; });
        AddNav(menu, "⚙  Impostazioni", () => { ShowInfo(); return Task.CompletedTask; });
        panel.Controls.Add(menu);
        return panel;
    }

    private void AddNav(Control parent, string text, Func<Task> action)
    {
        var button = CyberButton(text, 238, 52, Color.FromArgb(10, 31, 50));
        button.Margin = new Padding(0, 4, 0, 4);
        button.TextAlign = ContentAlignment.MiddleLeft;
        button.Padding = new Padding(16, 0, 0, 0);
        button.Click += async (_, _) =>
        {
            _nav.ForEach(x => x.BackColor = Color.FromArgb(10, 31, 50));
            button.BackColor = Color.FromArgb(0, 96, 210);
            await action();
        };
        _nav.Add(button);
        parent.Controls.Add(button);
    }

    private async Task ShowDashboardAsync()
    {
        try
        {
            Clear("Dashboard di Protezione");
            _status.Text = "Lettura dello stato Microsoft Defender...";
            var state = await _defender.GetStateAsync();
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 2, Padding = new Padding(18) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
            grid.Controls.Add(ScoreCard(state), 0, 0);
            grid.Controls.Add(QuickCard(), 1, 0);
            grid.Controls.Add(BrandCard(), 2, 0);
            var statusCard = StatusCard(state);
            grid.Controls.Add(statusCard, 0, 1);
            grid.SetColumnSpan(statusCard, 2);
            grid.Controls.Add(InfoCard(state), 2, 1);
            _content.Controls.Add(grid);
            _status.Text = $"Sistema aggiornato alle {DateTime.Now:HH:mm:ss} — Firme {state.SignatureVersion}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "FF GUARDIAN", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _status.Text = "Impossibile leggere Microsoft Defender.";
        }
    }

    private Control ScoreCard(SecurityState state)
    {
        var panel = Card("PUNTEGGIO DI PROTEZIONE");
        panel.Controls.Add(new Label { Text = state.Score.ToString(), Font = new Font("Segoe UI", 54, FontStyle.Bold), ForeColor = state.Score >= 85 ? Color.LimeGreen : Color.Orange, Dock = DockStyle.Left, Width = 190, TextAlign = ContentAlignment.MiddleCenter });
        panel.Controls.Add(new Label { Text = $"/100\n\n{(state.Score >= 85 ? "ECCELLENTE" : "DA MIGLIORARE")}", Font = new Font("Segoe UI", 15, FontStyle.Bold), ForeColor = Color.White, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft });
        return panel;
    }

    private Control QuickCard()
    {
        var panel = Card("AZIONI RAPIDE");
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(10), WrapContents = true };
        flow.Controls.Add(ActionButton("⚡ Scansione rapida", async () => await RunAsync(_defender.QuickScanAsync, "Scansione rapida completata.")));
        flow.Controls.Add(ActionButton("◉ Scansione completa", async () => await RunAsync(_defender.FullScanAsync, "Scansione completa completata.")));
        flow.Controls.Add(ActionButton("⟳ Aggiorna firme", async () => await RunAsync(_defender.UpdateAsync, "Firme aggiornate.")));
        flow.Controls.Add(ActionButton("▣ Scansiona cartella", ChooseFolderScanAsync));
        panel.Controls.Add(flow);
        return panel;
    }

    private static Control BrandCard()
    {
        var panel = Card("FFGuardian By EL.CO");
        panel.Controls.Add(new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Text = "◢\nFFGuardian\nBy EL.CO\n\nSICUREZZA • CONTROLLO • PROTEZIONE", Font = new Font("Segoe UI", 17, FontStyle.Bold), ForeColor = Color.DeepSkyBlue });
        return panel;
    }

    private static Control StatusCard(SecurityState state)
    {
        var panel = Card("STATO PROTEZIONE");
        var items = new[] { ("Microsoft Defender", state.Antivirus), ("Tempo reale", state.Realtime), ("Firme aggiornate", state.Signatures), ("Firewall", state.Firewall), ("Protezione PUA", state.Pua), ("Protezione rete", state.Network), ("Ransomware Guard", state.Ransomware) };
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12) };
        foreach (var item in items)
        {
            flow.Controls.Add(new Label { Width = 245, Height = 44, Text = $"●  {item.Item1}\n{(item.Item2 ? "Attivo" : "Da configurare")}", ForeColor = item.Item2 ? Color.LimeGreen : Color.Orange, BackColor = Color.FromArgb(9, 28, 44), Padding = new Padding(10, 5, 0, 0) });
        }
        panel.Controls.Add(flow);
        return panel;
    }

    private static Control InfoCard(SecurityState state)
    {
        var panel = Card("INFORMAZIONI SISTEMA");
        panel.Controls.Add(new Label { Dock = DockStyle.Fill, Padding = new Padding(15), ForeColor = Color.Gainsboro, Text = $"Computer: {Environment.MachineName}\nUtente: {Environment.UserName}\nWindows: {Environment.OSVersion}\n.NET: {Environment.Version}\n\nFirme Defender:\n{state.SignatureVersion}" });
        return panel;
    }

    private void ShowScanPage()
    {
        Clear("Centro Scansioni");
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(25) };
        flow.Controls.Add(ActionButton("⚡ SCANSIONE RAPIDA", async () => await RunAsync(_defender.QuickScanAsync, "Scansione rapida completata."), 300, 110));
        flow.Controls.Add(ActionButton("◉ SCANSIONE COMPLETA", async () => await RunAsync(_defender.FullScanAsync, "Scansione completa completata."), 300, 110));
        flow.Controls.Add(ActionButton("▣ CARTELLA PERSONALIZZATA", ChooseFolderScanAsync, 300, 110));
        flow.Controls.Add(ActionButton("⟳ AGGIORNA DEFINIZIONI", async () => await RunAsync(_defender.UpdateAsync, "Firme aggiornate."), 300, 110));
        _content.Controls.Add(flow);
    }

    private async Task ShowThreatsAsync()
    {
        Clear("Minacce rilevate da Microsoft Defender");
        var data = await _defender.GetThreatsAsync();
        var grid = new DataGridView { Dock = DockStyle.Fill, DataSource = data, ReadOnly = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.FromArgb(8, 22, 36), ForeColor = Color.White, RowHeadersVisible = false, BorderStyle = BorderStyle.None };
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(0, 96, 180);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.DefaultCellStyle.BackColor = Color.FromArgb(10, 28, 44);
        grid.DefaultCellStyle.ForeColor = Color.White;
        _content.Controls.Add(grid);
    }

    private void ShowQuarantine()
    {
        Clear("Quarantena");
        _content.Controls.Add(new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 16), ForeColor = Color.Silver, Text = "La Beta gestisce la quarantena tramite la Cronologia protezione di Microsoft Defender.\n\nApri Sicurezza di Windows → Protezione da virus e minacce → Cronologia protezione." });
    }

    private void ShowInfo()
    {
        Clear("Impostazioni");
        _content.Controls.Add(new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.Silver, Text = "FF GUARDIAN 5.0 Beta\nConsole avanzata per Microsoft Defender\nBy EL.CO di Francesco Fazzina\n\nQuesta Beta utilizza Microsoft Defender come motore antimalware reale." });
    }

    private async Task ChooseFolderScanAsync()
    {
        using var dialog = new FolderBrowserDialog { Description = "Seleziona la cartella da analizzare" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            await RunAsync(() => _defender.CustomScanAsync(dialog.SelectedPath), "Scansione cartella completata.");
    }

    private async Task RunAsync(Func<Task> action, string ok)
    {
        try
        {
            _status.Text = "Operazione in corso...";
            await action();
            _status.Text = ok;
            MessageBox.Show(ok, "FF GUARDIAN", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _status.Text = "Errore";
            MessageBox.Show(ex.Message, "FF GUARDIAN", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Clear(string title)
    {
        _content.Controls.Clear();
        var header = new Label { Text = title, Dock = DockStyle.Top, Height = 58, Font = new Font("Segoe UI", 19, FontStyle.Bold), ForeColor = Color.White, Padding = new Padding(20, 12, 0, 0), BackColor = Color.FromArgb(7, 20, 34) };
        _content.Controls.Add(header);
        header.BringToFront();
    }

    private static Panel Card(string title)
    {
        var panel = new Panel { Dock = DockStyle.Fill, Margin = new Padding(8), BackColor = Color.FromArgb(8, 24, 40), Padding = new Padding(12) };
        panel.Controls.Add(new Label { Text = title, Dock = DockStyle.Top, Height = 34, Font = new Font("Segoe UI", 11, FontStyle.Bold), ForeColor = Color.White });
        return panel;
    }

    private static Button CyberButton(string text, int width, int height, Color color) => new() { Text = text, Width = width, Height = height, BackColor = color, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold), Cursor = Cursors.Hand };

    private static Button ActionButton(string text, Func<Task> action, int width = 180, int height = 72)
    {
        var button = CyberButton(text, width, height, Color.FromArgb(9, 54, 88));
        button.Margin = new Padding(8);
        button.Click += async (_, _) => await action();
        return button;
    }
}
