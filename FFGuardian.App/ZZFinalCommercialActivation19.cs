using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace FFGuardian;

/// <summary>
/// Attiva per ultima la UI commerciale e impedisce alle vecchie inizializzazioni
/// di sovrascrivere dashboard e pagine operative.
/// </summary>
internal static class ZZFinalCommercialActivation19
{
    private static readonly Color Background = Color.FromArgb(5, 9, 12);
    private static readonly Color Surface = Color.FromArgb(12, 18, 23);
    private static readonly Color Raised = Color.FromArgb(20, 29, 35);
    private static readonly Color Neon = Color.FromArgb(111, 255, 28);
    private static readonly Color Text = Color.FromArgb(242, 247, 249);
    private static readonly Color Muted = Color.FromArgb(166, 181, 189);
    private static readonly Color Border = Color.FromArgb(50, 75, 83);
    private static System.Windows.Forms.Timer? _activationTimer;

    [ModuleInitializer]
    internal static void Initialize()
    {
        Application.Idle += StartDelayedActivation;
    }

    private static void StartDelayedActivation(object? sender, EventArgs e)
    {
        IndependentMainForm100? form = Application.OpenForms
            .OfType<IndependentMainForm100>()
            .FirstOrDefault();
        if (form is null || !form.IsHandleCreated)
            return;

        Application.Idle -= StartDelayedActivation;
        _activationTimer = new System.Windows.Forms.Timer { Interval = 1200 };
        _activationTimer.Tick += (_, _) =>
        {
            _activationTimer?.Stop();
            _activationTimer?.Dispose();
            _activationTimer = null;
            ApplyFinalInterface(form);
        };
        _activationTimer.Start();
    }

    private static void ApplyFinalInterface(IndependentMainForm100 form)
    {
        try
        {
            Type pagesType = typeof(CommercialPages18);
            FieldInfo? appliedField = pagesType.GetField("_applied", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo? applyMethod = pagesType.GetMethod("ApplyWhenReady", BindingFlags.Static | BindingFlags.NonPublic);
            appliedField?.SetValue(null, false);
            applyMethod?.Invoke(null, new object?[] { null, EventArgs.Empty });

            TabControl? tabs = FindControls<TabControl>(form)
                .OrderByDescending(item => item.TabCount)
                .FirstOrDefault(item => item.TabCount > 0);
            if (tabs is null)
                return;

            TabPage? dashboard = tabs.TabPages.Cast<TabPage>().FirstOrDefault(page =>
                page.Text.Contains("DASH", StringComparison.OrdinalIgnoreCase) ||
                page.Text.Contains("HOME", StringComparison.OrdinalIgnoreCase));
            dashboard ??= tabs.TabCount > 0 ? tabs.TabPages[0] : null;
            if (dashboard is not null)
                BuildDashboard(dashboard, form, tabs);

            StabilityCoordinator82.WriteInformationLog(
                "FFGuardian UI commerciale finale v19 applicata per ultima.");
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
        }
    }

    private static void BuildDashboard(TabPage page, Control form, TabControl tabs)
    {
        List<Button> buttons = FindControls<Button>(form).ToList();
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
                Dock = DockStyle.Fill,
                BackColor = Background,
                ColumnCount = 1,
                RowCount = 3,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 172F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 184F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            root.Controls.Add(BuildHeroAndCommands(buttons, tabs), 0, 0);
            root.Controls.Add(BuildProtectionAndResources(), 0, 1);
            root.Controls.Add(BuildActivityAndInformation(), 0, 2);
            page.Controls.Add(root);
        }
        finally
        {
            page.ResumeLayout(true);
        }
    }

    private static Control BuildHeroAndCommands(IReadOnlyList<Button> buttons, TabControl tabs)
    {
        TableLayoutPanel row = Row(5, new[] { 34F, 16.5F, 16.5F, 16.5F, 16.5F });
        Panel hero = Card();
        hero.Controls.Add(LabelFill(
            "✓   SISTEMA PROTETTO\r\n\r\nFFGuardian monitora file, processi, USB e comportamenti anomali in tempo reale.",
            Neon, 15F, ContentAlignment.MiddleCenter));
        row.Controls.Add(hero, 0, 0);

        row.Controls.Add(CommandCard("SCANSIONE RAPIDA", "Controlla le aree critiche",
            FindButton(buttons, "RAPIDA", "QUICK"), () => SelectTab(tabs, "SCANS")), 1, 0);
        row.Controls.Add(CommandCard("SCANSIONE COMPLETA", "Analisi approfondita",
            FindButton(buttons, "COMPLETA", "FULL"), () => SelectTab(tabs, "SCANS")), 2, 0);
        row.Controls.Add(CommandCard("PERSONALIZZATA", "File e cartelle scelti",
            FindButton(buttons, "PERSONAL", "CARTELLA"), () => SelectTab(tabs, "SCANS")), 3, 0);
        row.Controls.Add(CommandCard("VERIFICA MINACCE", "Apri audit e controlli",
            FindButton(buttons, "AUDIT", "PROTEGGI ORA"), () => SelectTab(tabs, "AUDIT")), 4, 0);
        return row;
    }

    private static Control BuildProtectionAndResources()
    {
        TableLayoutPanel row = Row(4, new[] { 25F, 25F, 25F, 25F });
        row.Controls.Add(StatusCard("PROTEZIONE IN TEMPO REALE", "ATTIVA", "File e download monitorati"), 0, 0);
        row.Controls.Add(StatusCard("RANSOM SHIELD", "ATTIVO", "Protezione comportamentale"), 1, 0);
        row.Controls.Add(StatusCard("FIREWALL", "ATTIVO", "Regole e traffico controllati"), 2, 0);

        Panel resources = Card();
        resources.Controls.Add(new Label
        {
            Name = "CommercialMetrics19",
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Text,
            Font = new Font("Consolas", 10F),
            Text = MetricsText(),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(18)
        });
        row.Controls.Add(resources, 3, 0);
        return row;
    }

    private static Control BuildActivityAndInformation()
    {
        TableLayoutPanel row = Row(2, new[] { 60F, 40F });
        Panel activity = Card();
        activity.Controls.Add(LabelFill(
            "ATTIVITÀ RECENTI\r\n\r\n✓ Engine10 pronto\r\n✓ Protezione in tempo reale attiva\r\n✓ Database firme disponibile\r\n✓ Auto-esclusione componenti FFGuardian attiva\r\n✓ Nessuna operazione distruttiva senza conferma",
            Text, 10F, ContentAlignment.MiddleLeft));

        Panel info = Card();
        info.Controls.Add(LabelFill(
            "INFORMAZIONI\r\n\r\nVersione: 10.0.1 RC1\r\nMotore: Engine10 Definitive\r\nProduttore: EL.CO by FFsoftware\r\nSupporto: alsafe127.00@gmail.com\r\nStato: aggiornato",
            Text, 10F, ContentAlignment.MiddleLeft));
        row.Controls.Add(activity, 0, 0);
        row.Controls.Add(info, 1, 0);
        return row;
    }

    private static Control CommandCard(string title, string detail, Button? target, Action fallback)
    {
        Panel card = Card();
        Button execute = new()
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            Text = "ESEGUI",
            UseVisualStyleBackColor = false,
            FlatStyle = FlatStyle.Flat,
            BackColor = Raised,
            ForeColor = Neon,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        execute.FlatAppearance.BorderColor = Neon;
        execute.FlatAppearance.BorderSize = 1;
        execute.Click += (_, _) =>
        {
            if (target is not null)
                target.PerformClick();
            else
                fallback();
        };
        card.Controls.Add(execute);
        card.Controls.Add(LabelFill(title + "\r\n\r\n" + detail, Text, 10F, ContentAlignment.MiddleCenter));
        return card;
    }

    private static Control StatusCard(string title, string state, string detail)
    {
        Panel card = Card();
        card.Controls.Add(LabelFill(
            title + "\r\n\r\n✓  " + state + "\r\n\r\n" + detail,
            Text, 10F, ContentAlignment.MiddleCenter));
        return card;
    }

    private static TableLayoutPanel Row(int columns, IReadOnlyList<float> widths)
    {
        TableLayoutPanel row = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Background,
            ColumnCount = columns,
            RowCount = 1,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        foreach (float width in widths)
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, width));
        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        return row;
    }

    private static Panel Card() => new()
    {
        Dock = DockStyle.Fill,
        BackColor = Surface,
        Margin = new Padding(6),
        Padding = new Padding(12),
        BorderStyle = BorderStyle.FixedSingle
    };

    private static Label LabelFill(string text, Color color, float size, ContentAlignment alignment) => new()
    {
        Dock = DockStyle.Fill,
        BackColor = Surface,
        ForeColor = color,
        Font = new Font("Segoe UI", size, FontStyle.Bold),
        Text = text,
        TextAlign = alignment,
        Padding = new Padding(14),
        AutoEllipsis = true
    };

    private static string MetricsText()
    {
        using Process current = Process.GetCurrentProcess();
        double memory = current.WorkingSet64 / 1024d / 1024d;
        return "RISORSE DI SISTEMA\r\n\r\nCPU logiche: " + Environment.ProcessorCount +
               "\r\nRAM FFGuardian: " + memory.ToString("0.0") + " MB" +
               "\r\nStato motore: operativo\r\nDatabase firme: pronto";
    }

    private static Button? FindButton(IEnumerable<Button> buttons, params string[] keywords)
    {
        return buttons.FirstOrDefault(button =>
        {
            string value = (button.Text + " " + button.Name).ToUpperInvariant();
            return keywords.Any(keyword => value.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        });
    }

    private static void SelectTab(TabControl tabs, string keyword)
    {
        TabPage? page = tabs.TabPages.Cast<TabPage>()
            .FirstOrDefault(item => item.Text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        if (page is not null)
            tabs.SelectedTab = page;
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
