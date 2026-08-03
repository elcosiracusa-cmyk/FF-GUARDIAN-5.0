using System.Runtime.CompilerServices;

namespace FFGuardian;

/// <summary>
/// Applica il layout commerciale mantenendo intatti controlli, eventi e comandi originali.
/// Nessun comando viene simulato: i pulsanti visibili inoltrano il click al controllo reale.
/// </summary>
internal static class CommercialPages18
{
    private static readonly Color Background = Color.FromArgb(5, 9, 12);
    private static readonly Color Surface = Color.FromArgb(12, 18, 23);
    private static readonly Color Raised = Color.FromArgb(20, 29, 35);
    private static readonly Color Neon = Color.FromArgb(111, 255, 28);
    private static readonly Color Text = Color.FromArgb(242, 247, 249);
    private static readonly Color Muted = Color.FromArgb(166, 181, 189);
    private static readonly Color Border = Color.FromArgb(50, 75, 83);

    private static bool _applied;

    [ModuleInitializer]
    internal static void Initialize() => Application.Idle += ApplyWhenReady;

    private static void ApplyWhenReady(object? sender, EventArgs e)
    {
        if (_applied)
            return;

        IndependentMainForm100? form = Application.OpenForms
            .OfType<IndependentMainForm100>()
            .FirstOrDefault();
        if (form is null || form.IsDisposed || !form.IsHandleCreated)
            return;

        TabControl? tabs = FindControls<TabControl>(form)
            .OrderByDescending(item => item.TabCount)
            .FirstOrDefault(item => item.TabCount > 0);
        if (tabs is null)
            return;

        try
        {
            foreach (TabPage page in tabs.TabPages)
                BuildPage(page);

            tabs.SelectedIndexChanged += (_, _) =>
            {
                if (tabs.SelectedTab is TabPage selected)
                    FitPage(selected);
            };
            form.Resize += (_, _) =>
            {
                foreach (TabPage page in tabs.TabPages)
                    FitPage(page);
            };

            _applied = true;
            Application.Idle -= ApplyWhenReady;
            StabilityCoordinator82.WriteInformationLog(
                "Pagine commerciali v18: comandi originali preservati.");
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            Application.Idle -= ApplyWhenReady;
        }
    }

    private static void BuildPage(TabPage page)
    {
        string title = page.Text.ToUpperInvariant();
        if (title.Contains("DASH") || title.Contains("HOME"))
            return;
        if (page.Controls.Cast<Control>().Any(control => control.Name == "CommercialPageRoot18"))
            return;

        List<Control> flattened = page.Controls.Cast<Control>()
            .SelectMany(Flatten)
            .Distinct()
            .ToList();

        // I pulsanti vengono catturati dalla pagina specifica prima di svuotarla.
        // In questo modo nessun comando reale può sparire o essere collegato alla pagina errata.
        List<Button> pageButtons = flattened
            .OfType<Button>()
            .Where(button => !string.IsNullOrWhiteSpace(button.Text))
            .OrderBy(button => AbsoluteTop(button))
            .ThenBy(button => AbsoluteLeft(button))
            .ToList();

        List<Control> contentControls = flattened
            .Where(IsUsefulContent)
            .Where(control => control is not Button)
            .Where(control => !HasUsefulAncestor(control, flattened))
            .ToList();

        foreach (Control control in pageButtons.Cast<Control>().Concat(contentControls).Distinct())
            control.Parent?.Controls.Remove(control);

        page.SuspendLayout();
        try
        {
            page.Controls.Clear();
            page.BackColor = Background;
            page.ForeColor = Text;
            page.Padding = new Padding(12);
            page.AutoScroll = false;

            int commandRows = Math.Max(1, (int)Math.Ceiling(pageButtons.Count / 4D));
            int commandHeight = Math.Clamp(commandRows * 58 + 12, 76, 190);

            TableLayoutPanel root = new()
            {
                Name = "CommercialPageRoot18",
                Dock = DockStyle.Fill,
                BackColor = Background,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, commandHeight));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            root.Controls.Add(BuildHeading(CleanTitle(page.Text), SubtitleFor(title)), 0, 0);
            root.Controls.Add(BuildCommands(pageButtons), 0, 1);
            root.Controls.Add(BuildContent(title, contentControls), 0, 2);
            page.Controls.Add(root);
        }
        finally
        {
            page.ResumeLayout(true);
        }
    }

    private static Control BuildHeading(string title, string subtitle)
    {
        Panel panel = Card();
        panel.Margin = new Padding(0, 0, 0, 8);
        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 9F),
            Text = subtitle,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(10, 0, 18, 0),
            AutoEllipsis = true
        });
        panel.Controls.Add(new Label
        {
            Dock = DockStyle.Left,
            Width = 420,
            BackColor = Surface,
            ForeColor = Text,
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            Text = title,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(18, 0, 0, 0),
            AutoEllipsis = true
        });
        return panel;
    }

    private static Control BuildCommands(IReadOnlyList<Button> originals)
    {
        FlowLayoutPanel flow = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Background,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = false,
            Padding = new Padding(0, 6, 0, 6),
            Margin = Padding.Empty
        };

        if (originals.Count == 0)
        {
            flow.Controls.Add(new Label
            {
                AutoSize = false,
                Width = 420,
                Height = 46,
                BackColor = Surface,
                ForeColor = Muted,
                Text = "Nessun comando operativo disponibile in questa sezione.",
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 0, 0, 0)
            });
            return flow;
        }

        HashSet<string> used = new(StringComparer.OrdinalIgnoreCase);
        foreach (Button target in originals)
        {
            string label = NormalizeCommandLabel(target.Text);
            string key = label + "|" + target.Name;
            if (!used.Add(key))
                continue;
            flow.Controls.Add(CreateCommandButton(label, target));
        }
        return flow;
    }

    private static Control BuildContent(string title, IReadOnlyList<Control> useful)
    {
        TableLayoutPanel content = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Background,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68F));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32F));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        Panel primary = Card();
        primary.Margin = new Padding(0, 0, 6, 0);
        Panel secondary = Card();
        secondary.Margin = new Padding(6, 0, 0, 0);

        if (useful.Count == 0)
        {
            primary.Controls.Add(BuildEmptyState(title));
        }
        else if (useful.Count == 1)
        {
            Control main = useful[0];
            PrepareContentControl(main);
            primary.Controls.Add(main);
        }
        else
        {
            TableLayoutPanel stack = new()
            {
                Dock = DockStyle.Fill,
                BackColor = Surface,
                ColumnCount = 1,
                RowCount = useful.Count,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int index = 0; index < useful.Count; index++)
            {
                stack.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / useful.Count));
                Control control = useful[index];
                PrepareContentControl(control);
                control.Margin = new Padding(0, 0, 0, index == useful.Count - 1 ? 0 : 6);
                stack.Controls.Add(control, 0, index);
            }
            primary.Controls.Add(stack);
        }

        secondary.Controls.Add(BuildSideInformation(title));
        content.Controls.Add(primary, 0, 0);
        content.Controls.Add(secondary, 1, 0);
        return content;
    }

    private static bool IsUsefulContent(Control control)
    {
        return control is DataGridView
            or ListView
            or TreeView
            or RichTextBox
            or CheckedListBox
            or PropertyGrid
            || control is TextBox textBox && textBox.Multiline
            || control is FlowLayoutPanel flow && flow.Controls.Cast<Control>().Any(IsInputControl)
            || control is TableLayoutPanel table && table.Controls.Cast<Control>().Any(IsInputControl)
            || control is Panel panel && panel.Controls.Cast<Control>().Any(IsInputControl)
            || control is GroupBox;
    }

    private static bool IsInputControl(Control control) =>
        control is CheckBox or RadioButton or ComboBox or NumericUpDown or TrackBar or TextBox;

    private static bool HasUsefulAncestor(Control control, IReadOnlyCollection<Control> candidates)
    {
        Control? parent = control.Parent;
        while (parent is not null)
        {
            if (candidates.Contains(parent) && IsUsefulContent(parent))
                return true;
            parent = parent.Parent;
        }
        return false;
    }

    private static void PrepareContentControl(Control control)
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
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            grid.DefaultCellStyle.BackColor = Surface;
            grid.DefaultCellStyle.ForeColor = Text;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(40, 75, 45);
            grid.DefaultCellStyle.SelectionForeColor = Text;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.ScrollBars = ScrollBars.Vertical;
        }
        else if (control is TextBox textBox)
        {
            textBox.BorderStyle = BorderStyle.None;
            textBox.ScrollBars = ScrollBars.Vertical;
        }

        PolishTree(control);
    }

    private static void PolishTree(Control root)
    {
        foreach (Control child in root.Controls)
        {
            child.ForeColor = child is Label label && label.ForeColor == Color.Empty ? Text : child.ForeColor;
            if (child is CheckBox or RadioButton or Label or GroupBox)
                child.BackColor = Surface;
            if (child is Button button)
            {
                button.UseVisualStyleBackColor = false;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = Neon;
                button.BackColor = Raised;
                button.ForeColor = Text;
            }
            PolishTree(child);
        }
    }

    private static Button CreateCommandButton(string label, Button target)
    {
        Button button = new()
        {
            Width = 210,
            Height = 48,
            Margin = new Padding(0, 0, 10, 10),
            Text = label,
            Enabled = target.Enabled,
            UseVisualStyleBackColor = false,
            FlatStyle = FlatStyle.Flat,
            BackColor = Raised,
            ForeColor = Text,
            Font = new Font("Segoe UI", 9.2F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true,
            Tag = target
        };
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Neon;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(38, 58, 44);
        button.Click += (_, _) =>
        {
            if (!target.IsDisposed)
                target.PerformClick();
        };
        target.EnabledChanged += (_, _) =>
        {
            if (!button.IsDisposed)
                button.Enabled = target.Enabled;
        };
        return button;
    }

    private static string NormalizeCommandLabel(string value)
    {
        string label = value.Replace("&", string.Empty, StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
        while (label.Contains("  ", StringComparison.Ordinal))
            label = label.Replace("  ", " ", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(label) ? "ESEGUI" : label.ToUpperInvariant();
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

    private static Control BuildEmptyState(string title)
    {
        string message = title.Contains("RECUP")
            ? "Nessun elemento in quarantena.\r\nGli archivi protetti e i punti di rollback appariranno qui."
            : title.Contains("RANSOM")
                ? "Ransom Shield pronto.\r\nGli eventi comportamentali appariranno qui."
                : title.Contains("AGGIORN")
                    ? "Database firme pronto.\r\nUsa i comandi originali disponibili nella barra superiore."
                    : title.Contains("IMPOST")
                        ? "Configurazione protetta.\r\nI controlli disponibili vengono mantenuti senza modificare le funzioni."
                        : "Nessun evento da mostrare.\r\nLe attività compariranno automaticamente.";

        return new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 12F),
            Text = message,
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(30)
        };
    }

    private static Control BuildSideInformation(string title)
    {
        string text = title.Contains("SCANS")
            ? "STATO SCANSIONE\r\n\r\n• Engine10 pronto\r\n• Firme locali attive\r\n• Auto-esclusione FFGuardian attiva\r\n• Quarantena cifrata pronta"
            : title.Contains("AUDIT")
                ? "AUDIT SISTEMA\r\n\r\nControlla persistenza, servizi, attività pianificate, firme digitali e anomalie di avvio."
                : title.Contains("RECUP")
                    ? "RECUPERO SICURO\r\n\r\nAccesso agli archivi di quarantena e rollback tramite i comandi originali."
                    : title.Contains("AGGIORN")
                        ? "AGGIORNAMENTI\r\n\r\nRicarica del database firme con verifica del motore."
                        : title.Contains("RANSOM")
                            ? "PROTEZIONE COMPORTAMENTALE\r\n\r\nLe funzioni disponibili dipendono dai controlli realmente installati."
                            : title.Contains("IMPOST")
                                ? "IMPOSTAZIONI\r\n\r\nI controlli originali vengono conservati e mostrati nel pannello principale."
                                : "MONITORAGGIO\r\n\r\nRapporti e registro attività restano collegati alle funzioni originali.";

        return new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Text,
            Font = new Font("Segoe UI", 10.5F),
            Text = text,
            TextAlign = ContentAlignment.TopLeft,
            Padding = new Padding(22)
        };
    }

    private static Panel Card() => new()
    {
        Dock = DockStyle.Fill,
        BackColor = Surface,
        Padding = new Padding(10)
    };

    private static void FitPage(TabPage page)
    {
        page.AutoScroll = false;
        foreach (Control control in page.Controls)
        {
            if (control.Name == "CommercialPageRoot18")
                control.Bounds = page.ClientRectangle;
        }
    }

    private static string SubtitleFor(string title)
    {
        if (title.Contains("SCANS")) return "Scansione rapida, file, cartelle, quarantena e annullamento";
        if (title.Contains("AUDIT")) return "Audit completo, rapporto e annullamento";
        if (title.Contains("RECUP")) return "Archivi quarantena e rollback";
        if (title.Contains("AGGIORN")) return "Ricarica sicura del database firme";
        if (title.Contains("ATTIV")) return "Rapporti e registro operativo";
        if (title.Contains("IMPOST")) return "Configurazione disponibile nel motore installato";
        if (title.Contains("RANSOM")) return "Protezione comportamentale disponibile nel motore installato";
        return "FFGuardian Ultimate Protection";
    }

    private static string CleanTitle(string value)
    {
        string cleaned = value.Replace("&", string.Empty, StringComparison.Ordinal).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "SICUREZZA" : cleaned.ToUpperInvariant();
    }

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
}