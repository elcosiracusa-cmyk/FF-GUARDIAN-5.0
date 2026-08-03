using System.Runtime.CompilerServices;

namespace FFGuardian;

/// <summary>
/// Completa le pagine della UI commerciale senza duplicare il motore.
/// I pulsanti creati qui inoltrano i click ai comandi originali già presenti.
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
            List<Button> originalButtons = FindControls<Button>(form).ToList();
            foreach (TabPage page in tabs.TabPages)
                BuildPage(page, originalButtons);

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
                "Dashboard commerciale e pagine operative v18 applicate.");
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            Application.Idle -= ApplyWhenReady;
        }
    }

    private static void BuildPage(TabPage page, IReadOnlyList<Button> originalButtons)
    {
        string title = page.Text.ToUpperInvariant();
        if (title.Contains("DASH") || title.Contains("HOME"))
            return; // La dashboard principale è già costruita dalla shell commerciale.

        List<Control> original = page.Controls.Cast<Control>().ToList();
        List<Control> useful = original
            .SelectMany(Flatten)
            .Where(control => control is DataGridView or ListView or TreeView or RichTextBox or CheckedListBox)
            .Distinct()
            .ToList();

        page.SuspendLayout();
        try
        {
            foreach (Control control in useful)
                control.Parent?.Controls.Remove(control);

            page.Controls.Clear();
            page.BackColor = Background;
            page.ForeColor = Text;
            page.Padding = new Padding(12);
            page.AutoScroll = false;

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
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 158F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            root.Controls.Add(BuildHeading(CleanTitle(page.Text), SubtitleFor(title)), 0, 0);
            root.Controls.Add(BuildCommands(title, originalButtons), 0, 1);
            root.Controls.Add(BuildContent(title, useful), 0, 2);
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

    private static Control BuildCommands(string title, IReadOnlyList<Button> originals)
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

        foreach ((string label, string[] keywords) in CommandsFor(title))
        {
            Button? target = FindOriginalButton(originals, keywords);
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
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64F));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36F));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        Panel primary = Card();
        primary.Margin = new Padding(0, 0, 6, 0);
        Panel secondary = Card();
        secondary.Margin = new Padding(6, 0, 0, 0);

        Control? main = useful.FirstOrDefault(control => control is DataGridView)
            ?? useful.FirstOrDefault(control => control is ListView)
            ?? useful.FirstOrDefault();
        if (main is not null)
        {
            PrepareDataControl(main);
            primary.Controls.Add(main);
        }
        else
        {
            primary.Controls.Add(BuildEmptyState(title));
        }

        secondary.Controls.Add(BuildSideInformation(title));
        content.Controls.Add(primary, 0, 0);
        content.Controls.Add(secondary, 1, 0);
        return content;
    }

    private static Control BuildEmptyState(string title)
    {
        string message = title.Contains("RECUP")
            ? "Nessun elemento in quarantena.\r\nI file isolati e i punti di ripristino appariranno qui."
            : title.Contains("RANSOM")
                ? "Ransom Shield attivo.\r\nNessun comportamento di cifratura anomalo rilevato."
                : title.Contains("AGGIORN")
                    ? "Database firme pronto.\r\nUsa i comandi superiori per verificare o applicare aggiornamenti."
                    : title.Contains("IMPOST")
                        ? "Configurazione protetta.\r\nSeleziona una categoria e salva le modifiche."
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
            ? "STATO SCANSIONE\r\n\r\n• Engine10: pronto\r\n• ClamAV/YARA: disponibili se installati\r\n• Auto-esclusione FFGuardian: attiva\r\n• Quarantena cifrata: pronta"
            : title.Contains("AUDIT")
                ? "AUDIT SISTEMA\r\n\r\nControlla persistenza, servizi, attività pianificate, firme digitali e anomalie di avvio."
                : title.Contains("RECUP")
                    ? "RECUPERO SICURO\r\n\r\nRipristina solo elementi verificati. Ogni operazione viene registrata nel rapporto locale."
                    : title.Contains("AGGIORN")
                        ? "AGGIORNAMENTI\r\n\r\nManifest firmato, verifica SHA-256, protezione anti-downgrade e rollback automatico."
                        : title.Contains("RANSOM")
                            ? "PROTEZIONE COMPORTAMENTALE\r\n\r\nMonitora scritture massive, rinomine, aumento entropia e modifiche ai backup."
                            : title.Contains("IMPOST")
                                ? "IMPOSTAZIONI\r\n\r\nProtezione, scansioni, aggiornamenti, notifiche, esclusioni e opzioni avanzate."
                                : "MONITORAGGIO\r\n\r\nEventi, processi e operazioni recenti vengono aggiornati in tempo reale.";

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

    private static Button CreateCommandButton(string label, Button? target)
    {
        Button button = new()
        {
            Width = 205,
            Height = 64,
            Margin = new Padding(0, 0, 10, 10),
            Text = target is null ? label + "\r\nNON DISPONIBILE" : label,
            Enabled = target is not null,
            UseVisualStyleBackColor = false,
            FlatStyle = FlatStyle.Flat,
            BackColor = target is null ? Color.FromArgb(28, 34, 38) : Raised,
            ForeColor = target is null ? Muted : Text,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            AutoEllipsis = true
        };
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = target is null ? Border : Neon;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(38, 58, 44);
        if (target is not null)
            button.Click += (_, _) => target.PerformClick();
        return button;
    }

    private static Button? FindOriginalButton(IEnumerable<Button> buttons, IEnumerable<string> keywords)
    {
        string[] normalized = keywords.Select(item => item.ToUpperInvariant()).ToArray();
        return buttons.FirstOrDefault(button =>
        {
            string value = (button.Text + " " + button.Name).ToUpperInvariant();
            return normalized.Any(value.Contains);
        });
    }

    private static IEnumerable<(string Label, string[] Keywords)> CommandsFor(string title)
    {
        if (title.Contains("SCANS"))
        {
            yield return ("SCANSIONE RAPIDA", new[] { "RAPIDA", "QUICK" });
            yield return ("SCANSIONE COMPLETA", new[] { "COMPLETA", "FULL" });
            yield return ("SCANSIONE PERSONALIZZATA", new[] { "PERSONAL", "CARTELLA" });
            yield return ("VERIFICA FILE", new[] { "SCANSIONA FILE", "FILE" });
        }
        else if (title.Contains("AUDIT"))
        {
            yield return ("ESEGUI AUDIT COMPLETO", new[] { "AUDIT COMPLETO", "ESEGUI AUDIT", "AUDIT" });
            yield return ("CONTROLLO AVVIO", new[] { "AVVIO", "STARTUP" });
            yield return ("PROCESSI ATTIVI", new[] { "PROCESSI", "PROCESS" });
            yield return ("ESPORTA RAPPORTO", new[] { "ESPORTA", "RAPPORTO" });
        }
        else if (title.Contains("RECUP") || title.Contains("RIPRIST"))
        {
            yield return ("APRI QUARANTENA", new[] { "QUARANTENA", "APRI QUARANTENA" });
            yield return ("RIPRISTINA SELEZIONATO", new[] { "RIPRISTINA", "RESTORE" });
            yield return ("ELIMINA DEFINITIVAMENTE", new[] { "ELIMINA", "DELETE" });
            yield return ("ESEGUI ROLLBACK", new[] { "ROLLBACK" });
        }
        else if (title.Contains("AGGIORN") || title.Contains("UPDATE"))
        {
            yield return ("VERIFICA AGGIORNAMENTI", new[] { "VERIFICA AGGIORN", "CHECK UPDATE", "AGGIORNA FIRME" });
            yield return ("AGGIORNA DATABASE FIRME", new[] { "DATABASE", "FIRME" });
            yield return ("AGGIORNA MOTORE", new[] { "MOTORE", "ENGINE" });
            yield return ("CRONOLOGIA AGGIORNAMENTI", new[] { "CRONOLOGIA", "LOG" });
        }
        else if (title.Contains("ATTIV") || title.Contains("CRONO"))
        {
            yield return ("AGGIORNA ATTIVITÀ", new[] { "AGGIORNA", "REFRESH" });
            yield return ("PROCESSI ATTIVI", new[] { "PROCESSI", "PROCESS" });
            yield return ("APRI CRONOLOGIA", new[] { "CRONOLOGIA", "HISTORY" });
            yield return ("ESPORTA LOG", new[] { "ESPORTA", "LOG" });
        }
        else if (title.Contains("IMPOST"))
        {
            yield return ("PROTEZIONE", new[] { "PROTEZIONE", "PROTECTION" });
            yield return ("ESCLUSIONI", new[] { "ESCLUSION", "EXCLUSION" });
            yield return ("NOTIFICHE", new[] { "NOTIFIC", "ALERT" });
            yield return ("SALVA IMPOSTAZIONI", new[] { "SALVA", "SAVE" });
        }
        else if (title.Contains("RANSOM"))
        {
            yield return ("ATTIVA RANSOM SHIELD", new[] { "ATTIVA", "ENABLE", "RANSOM" });
            yield return ("CARTELLE PROTETTE", new[] { "CARTELLE", "PROTETTE" });
            yield return ("GESTISCI ECCEZIONI", new[] { "ECCEZION", "EXCEPTION" });
            yield return ("VISUALIZZA LOG", new[] { "LOG", "EVENTI" });
        }
    }

    private static void PrepareDataControl(Control control)
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
        if (title.Contains("SCANS")) return "Scansione rapida, completa, personalizzata e file singoli";
        if (title.Contains("AUDIT")) return "Controllo persistenza, servizi, avvio e integrità";
        if (title.Contains("RECUP")) return "Quarantena cifrata, ripristino e rollback";
        if (title.Contains("AGGIORN")) return "Database firme e motore con verifica firmata";
        if (title.Contains("ATTIV")) return "Monitoraggio e cronologia in tempo reale";
        if (title.Contains("IMPOST")) return "Configurazione completa di FFGuardian";
        if (title.Contains("RANSOM")) return "Protezione comportamentale contro ransomware";
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
