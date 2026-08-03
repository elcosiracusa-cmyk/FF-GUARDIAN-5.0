using System.Runtime.CompilerServices;

namespace FFGuardian;

/// <summary>
/// Finalizzatore della UI commerciale. Viene eseguito dopo le vecchie patch grafiche
/// e garantisce che ogni pagina mantenga la stessa shell della dashboard.
/// Riutilizza i controlli originali, quindi non sostituisce né simula i comandi reali.
/// </summary>
internal static class UnifiedCommercialShell19
{
    private static readonly Color Background = Color.FromArgb(4, 8, 11);
    private static readonly Color Surface = Color.FromArgb(10, 16, 20);
    private static readonly Color Raised = Color.FromArgb(16, 24, 29);
    private static readonly Color Neon = Color.FromArgb(112, 255, 24);
    private static readonly Color Text = Color.FromArgb(242, 247, 249);
    private static readonly Color Muted = Color.FromArgb(158, 174, 181);
    private static readonly Color Border = Color.FromArgb(42, 61, 68);

    private static System.Windows.Forms.Timer? _startupTimer;
    private static System.Windows.Forms.Timer? _navigationTimer;
    private static TabControl? _tabs;

    [ModuleInitializer]
    internal static void Initialize()
    {
        Application.Idle += StartWhenReady;
    }

    private static void StartWhenReady(object? sender, EventArgs e)
    {
        IndependentMainForm100? form = Application.OpenForms
            .OfType<IndependentMainForm100>()
            .FirstOrDefault();
        if (form is null || form.IsDisposed || !form.IsHandleCreated)
            return;

        TabControl? tabs = FindControls<TabControl>(form)
            .OrderByDescending(control => control.TabCount)
            .FirstOrDefault(control => control.TabCount > 0);
        if (tabs is null)
            return;

        Application.Idle -= StartWhenReady;
        _tabs = tabs;

        _startupTimer = new System.Windows.Forms.Timer { Interval = 1800 };
        _startupTimer.Tick += (_, _) =>
        {
            _startupTimer?.Stop();
            _startupTimer?.Dispose();
            _startupTimer = null;
            ApplyAllPages();
        };
        _startupTimer.Start();

        tabs.SelectedIndexChanged += (_, _) => ScheduleSelectedPage();
        form.Resize += (_, _) =>
        {
            if (_tabs?.SelectedTab is TabPage selected)
                Fit(selected);
        };
        form.FormClosed += (_, _) => DisposeTimers();
    }

    private static void ScheduleSelectedPage()
    {
        _navigationTimer?.Stop();
        _navigationTimer?.Dispose();
        _navigationTimer = new System.Windows.Forms.Timer { Interval = 220 };
        _navigationTimer.Tick += (_, _) =>
        {
            _navigationTimer?.Stop();
            _navigationTimer?.Dispose();
            _navigationTimer = null;
            if (_tabs?.SelectedTab is TabPage selected && !IsDashboard(selected.Text))
                ApplyPage(selected, force: !HasUnifiedShell(selected));
        };
        _navigationTimer.Start();
    }

    private static void ApplyAllPages()
    {
        if (_tabs is null || _tabs.IsDisposed)
            return;

        foreach (TabPage page in _tabs.TabPages)
        {
            if (!IsDashboard(page.Text))
                ApplyPage(page, force: !HasUnifiedShell(page));
        }

        if (_tabs.SelectedTab is TabPage selected)
            Fit(selected);

        StabilityCoordinator82.WriteInformationLog(
            "Shell commerciale unificata applicata a tutte le pagine.");
    }

    private static void ApplyPage(TabPage page, bool force)
    {
        if (page.IsDisposed || IsDashboard(page.Text))
            return;
        if (!force && HasUnifiedShell(page))
            return;

        List<Control> flattened = page.Controls.Cast<Control>()
            .SelectMany(Flatten)
            .Distinct()
            .ToList();

        // Non riacquisire i controlli creati da questa stessa shell.
        List<Button> commands = flattened
            .OfType<Button>()
            .Where(button => !IsInsideUnifiedShell(button))
            .Where(button => !string.IsNullOrWhiteSpace(button.Text))
            .OrderBy(AbsoluteTop)
            .ThenBy(AbsoluteLeft)
            .ToList();

        List<Control> dataControls = flattened
            .Where(control => !IsInsideUnifiedShell(control))
            .Where(IsPrimaryContent)
            .Where(control => control is not Button)
            .Where(control => !HasPrimaryAncestor(control, flattened))
            .ToList();

        // Se una vecchia patch ha già creato dei proxy, mantieni soltanto i controlli
        // con un evento reale o i contenitori dati utili.
        commands = commands
            .GroupBy(button => Normalize(button.Text), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        foreach (Control control in commands.Cast<Control>().Concat(dataControls).Distinct())
            control.Parent?.Controls.Remove(control);

        page.SuspendLayout();
        try
        {
            page.Controls.Clear();
            page.BackColor = Background;
            page.ForeColor = Text;
            page.Padding = new Padding(10);
            page.AutoScroll = false;

            TableLayoutPanel root = new()
            {
                Name = "UnifiedCommercialPage19",
                Dock = DockStyle.Fill,
                BackColor = Background,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, CalculateCommandHeight(commands.Count)));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            root.Controls.Add(BuildHeader(page.Text), 0, 0);
            root.Controls.Add(BuildCommandGrid(commands), 0, 1);
            root.Controls.Add(BuildBody(page.Text, dataControls), 0, 2);
            page.Controls.Add(root);
        }
        finally
        {
            page.ResumeLayout(true);
        }
    }

    private static Control BuildHeader(string pageText)
    {
        TableLayoutPanel header = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(16, 0, 16, 0),
            Margin = new Padding(0, 0, 0, 6)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54F));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46F));
        header.Controls.Add(LabelFor(Clean(pageText), Text, 18F, FontStyle.Bold, ContentAlignment.MiddleLeft), 0, 0);
        header.Controls.Add(LabelFor(Subtitle(pageText), Muted, 8.5F, FontStyle.Regular, ContentAlignment.MiddleRight), 1, 0);
        return Framed(header);
    }

    private static Control BuildCommandGrid(IReadOnlyList<Button> commands)
    {
        if (commands.Count == 0)
        {
            Panel empty = PanelCard();
            empty.Controls.Add(LabelFor(
                "Nessun comando operativo disponibile in questa sezione.",
                Muted,
                10F,
                FontStyle.Regular,
                ContentAlignment.MiddleCenter));
            return empty;
        }

        int columns = Math.Min(4, Math.Max(1, commands.Count));
        int rows = (int)Math.Ceiling(commands.Count / (double)columns);
        TableLayoutPanel grid = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Background,
            ColumnCount = columns,
            RowCount = rows,
            Padding = new Padding(0, 4, 0, 4),
            Margin = Padding.Empty
        };
        for (int column = 0; column < columns; column++)
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / columns));
        for (int row = 0; row < rows; row++)
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / rows));

        for (int index = 0; index < commands.Count; index++)
        {
            Button command = commands[index];
            StyleRealCommand(command);
            command.Margin = new Padding(
                index % columns == 0 ? 0 : 5,
                index / columns == 0 ? 0 : 5,
                index % columns == columns - 1 ? 0 : 5,
                index / columns == rows - 1 ? 0 : 5);
            grid.Controls.Add(Framed(command), index % columns, index / columns);
        }
        return grid;
    }

    private static Control BuildBody(string pageText, IReadOnlyList<Control> dataControls)
    {
        TableLayoutPanel body = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Background,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, 5, 0, 0),
            Margin = Padding.Empty
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 72F));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        Panel main = PanelCard();
        main.Margin = new Padding(0, 0, 5, 0);
        Panel side = PanelCard();
        side.Margin = new Padding(5, 0, 0, 0);

        if (dataControls.Count == 0)
        {
            main.Controls.Add(LabelFor(
                EmptyText(pageText), Muted, 11F, FontStyle.Regular,
                ContentAlignment.MiddleCenter));
        }
        else if (dataControls.Count == 1)
        {
            Control control = dataControls[0];
            StyleContent(control);
            main.Controls.Add(control);
        }
        else
        {
            TableLayoutPanel stack = new()
            {
                Dock = DockStyle.Fill,
                BackColor = Surface,
                ColumnCount = 1,
                RowCount = dataControls.Count,
                Padding = Padding.Empty,
                Margin = Padding.Empty
            };
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int index = 0; index < dataControls.Count; index++)
            {
                stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / dataControls.Count));
                Control control = dataControls[index];
                StyleContent(control);
                control.Margin = new Padding(0, 0, 0, index == dataControls.Count - 1 ? 0 : 5);
                stack.Controls.Add(control, 0, index);
            }
            main.Controls.Add(stack);
        }

        side.Controls.Add(LabelFor(
            SideText(pageText), Text, 9.5F, FontStyle.Regular,
            ContentAlignment.TopLeft, new Padding(18)));
        body.Controls.Add(Framed(main), 0, 0);
        body.Controls.Add(Framed(side), 1, 0);
        return body;
    }

    private static void StyleRealCommand(Button button)
    {
        button.Dock = DockStyle.Fill;
        button.Height = 54;
        button.UseVisualStyleBackColor = false;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(31, 51, 38);
        button.BackColor = Raised;
        button.ForeColor = Neon;
        button.Font = new Font("Segoe UI", 9.2F, FontStyle.Bold);
        button.Text = Normalize(button.Text);
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.AutoEllipsis = true;
    }

    private static void StyleContent(Control control)
    {
        control.Dock = DockStyle.Fill;
        control.Margin = Padding.Empty;
        control.BackColor = Surface;
        control.ForeColor = Text;

        if (control is DataGridView grid)
        {
            grid.BackgroundColor = Surface;
            grid.BorderStyle = BorderStyle.None;
            grid.EnableHeadersVisualStyles = false;
            grid.GridColor = Border;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Raised;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Neon;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            grid.DefaultCellStyle.BackColor = Surface;
            grid.DefaultCellStyle.ForeColor = Text;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(34, 67, 39);
            grid.DefaultCellStyle.SelectionForeColor = Text;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.ScrollBars = ScrollBars.Vertical;
        }
        else if (control is TextBox textBox)
        {
            textBox.BorderStyle = BorderStyle.None;
            textBox.ScrollBars = ScrollBars.Vertical;
        }

        StyleChildren(control);
    }

    private static void StyleChildren(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (child is Label or CheckBox or RadioButton or GroupBox)
            {
                child.BackColor = Surface;
                child.ForeColor = Text;
            }
            if (child is Button button)
                StyleRealCommand(button);
            StyleChildren(child);
        }
    }

    private static bool IsPrimaryContent(Control control)
    {
        return control is DataGridView or ListView or TreeView or RichTextBox or CheckedListBox or PropertyGrid
            || control is TextBox textBox && textBox.Multiline
            || control is GroupBox
            || control is FlowLayoutPanel flow && flow.Controls.Cast<Control>().Any(IsInput)
            || control is TableLayoutPanel table && table.Controls.Cast<Control>().Any(IsInput)
            || control is Panel panel && panel.Controls.Cast<Control>().Any(IsInput);
    }

    private static bool IsInput(Control control) =>
        control is CheckBox or RadioButton or ComboBox or NumericUpDown or TrackBar or TextBox;

    private static bool HasPrimaryAncestor(Control control, IReadOnlyCollection<Control> controls)
    {
        for (Control? parent = control.Parent; parent is not null; parent = parent.Parent)
        {
            if (controls.Contains(parent) && IsPrimaryContent(parent))
                return true;
        }
        return false;
    }

    private static bool HasUnifiedShell(TabPage page) =>
        page.Controls.Cast<Control>().Any(control => control.Name == "UnifiedCommercialPage19");

    private static bool IsInsideUnifiedShell(Control control)
    {
        for (Control? current = control; current is not null; current = current.Parent)
        {
            if (current.Name == "UnifiedCommercialPage19")
                return true;
        }
        return false;
    }

    private static int CalculateCommandHeight(int count)
    {
        int rows = Math.Max(1, (int)Math.Ceiling(count / 4D));
        return Math.Clamp(rows * 64 + 8, 72, 200);
    }

    private static string Subtitle(string text)
    {
        string title = text.ToUpperInvariant();
        if (title.Contains("SCANS")) return "Scansione rapida, file, cartelle, quarantena e annullamento";
        if (title.Contains("AUDIT")) return "Controllo persistenza, servizi, avvio e integrità";
        if (title.Contains("RECUP")) return "Quarantena cifrata, archivi protetti e rollback";
        if (title.Contains("AGGIORN")) return "Database firme e aggiornamento sicuro";
        if (title.Contains("ATTIV")) return "Monitoraggio, processi, rapporti e cronologia";
        if (title.Contains("IMPOST")) return "Configurazione completa della protezione";
        if (title.Contains("RANSOM")) return "Protezione comportamentale contro ransomware";
        return "FFGuardian Ultimate Protection";
    }

    private static string EmptyText(string text)
    {
        string title = text.ToUpperInvariant();
        if (title.Contains("RECUP")) return "Nessun elemento in quarantena.\r\nGli archivi protetti e i punti di rollback appariranno qui.";
        if (title.Contains("AGGIORN")) return "Database firme pronto.\r\nUsa i comandi superiori per verificare o applicare aggiornamenti.";
        if (title.Contains("RANSOM")) return "Ransom Shield attivo.\r\nNessun comportamento anomalo rilevato.";
        if (title.Contains("IMPOST")) return "Configurazione protetta.\r\nSeleziona una categoria e salva le modifiche.";
        return "Nessun evento da mostrare.\r\nLe attività compariranno automaticamente.";
    }

    private static string SideText(string text)
    {
        string title = text.ToUpperInvariant();
        if (title.Contains("SCANS")) return "STATO SCANSIONE\r\n\r\n• Engine10 pronto\r\n• Firme locali attive\r\n• Auto-esclusione FFGuardian attiva\r\n• Quarantena cifrata pronta";
        if (title.Contains("AUDIT")) return "AUDIT SISTEMA\r\n\r\nControlla persistenza, servizi, attività pianificate, firme digitali e anomalie di avvio.";
        if (title.Contains("RECUP")) return "RECUPERO SICURO\r\n\r\nAccesso agli archivi di quarantena e rollback tramite i comandi reali.";
        if (title.Contains("AGGIORN")) return "AGGIORNAMENTI\r\n\r\nManifest firmato, verifica SHA-256 e protezione anti-downgrade.";
        if (title.Contains("ATTIV")) return "MONITORAGGIO\r\n\r\nRapporti, processi e attività recenti restano collegati alle funzioni originali.";
        if (title.Contains("IMPOST")) return "IMPOSTAZIONI\r\n\r\nProtezione, scansioni, aggiornamenti, notifiche ed esclusioni.";
        if (title.Contains("RANSOM")) return "PROTEZIONE COMPORTAMENTALE\r\n\r\nMonitoraggio di scritture massive, rinomine ed eventi sospetti.";
        return "FFGUARDIAN\r\n\r\nUltimate Protection · Engine10";
    }

    private static string Clean(string value)
    {
        string cleaned = Normalize(value);
        return string.IsNullOrWhiteSpace(cleaned) ? "SICUREZZA" : cleaned;
    }

    private static string Normalize(string value)
    {
        string text = value.Replace("&", string.Empty, StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
        while (text.Contains("  ", StringComparison.Ordinal))
            text = text.Replace("  ", " ", StringComparison.Ordinal);
        return text.ToUpperInvariant();
    }

    private static Label LabelFor(
        string text,
        Color color,
        float size,
        FontStyle style,
        ContentAlignment alignment,
        Padding? padding = null)
    {
        return new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = color,
            Font = new Font("Segoe UI", size, style),
            Text = text,
            TextAlign = alignment,
            Padding = padding ?? Padding.Empty,
            AutoEllipsis = true
        };
    }

    private static Panel PanelCard() => new()
    {
        Dock = DockStyle.Fill,
        BackColor = Surface,
        Padding = new Padding(10),
        Margin = Padding.Empty
    };

    private static Control Framed(Control control)
    {
        Panel frame = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Border,
            Padding = new Padding(1),
            Margin = control.Margin
        };
        control.Margin = Padding.Empty;
        control.Dock = DockStyle.Fill;
        frame.Controls.Add(control);
        return frame;
    }

    private static void Fit(TabPage page)
    {
        page.AutoScroll = false;
        foreach (Control control in page.Controls)
        {
            if (control.Name == "UnifiedCommercialPage19")
                control.Bounds = page.ClientRectangle;
        }
    }

    private static int AbsoluteTop(Control control)
    {
        int result = control.Top;
        for (Control? parent = control.Parent; parent is not null; parent = parent.Parent)
            result += parent.Top;
        return result;
    }

    private static int AbsoluteLeft(Control control)
    {
        int result = control.Left;
        for (Control? parent = control.Parent; parent is not null; parent = parent.Parent)
            result += parent.Left;
        return result;
    }

    private static bool IsDashboard(string text) =>
        text.Contains("DASH", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("HOME", StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<Control> Flatten(Control root)
    {
        yield return root;
        foreach (Control child in root.Controls)
        {
            foreach (Control nested in Flatten(child))
                yield return nested;
        }
    }

    private static IEnumerable<T> FindControls<T>(Control root) where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T match)
                yield return match;
            foreach (T nested in FindControls<T>(child))
                yield return nested;
        }
    }

    private static void DisposeTimers()
    {
        _startupTimer?.Stop();
        _startupTimer?.Dispose();
        _startupTimer = null;
        _navigationTimer?.Stop();
        _navigationTimer?.Dispose();
        _navigationTimer = null;
    }
}
