namespace FFGuardian;

internal static class LayoutRepair
{
    public static void ApplyToOpenForms(object? sender, EventArgs e)
    {
        foreach (Form form in Application.OpenForms)
        {
            form.Text = "FF GUARDIAN 5.2.9 — Support Email Layout Fix by EL.CO";
            RepairTree(form);
            PolishCurrentPage(form);
            RemoveDuplicateSupportButtons(form);
        }
    }

    private static void RepairTree(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (control is FlowLayoutPanel flow)
                RepairFlow(flow);

            if (control.HasChildren)
                RepairTree(control);
        }
    }

    private static void RepairFlow(FlowLayoutPanel flow)
    {
        flow.AutoScroll = true;
        Button[] buttons = flow.Controls.OfType<Button>().ToArray();
        Panel[] panels = flow.Controls.OfType<Panel>().ToArray();

        bool navigation = buttons.Any(b => b.Text.Contains("Dashboard", StringComparison.OrdinalIgnoreCase));
        bool quickActions = buttons.Any(b => b.Text.Contains("SCANSIONE RAPIDA", StringComparison.OrdinalIgnoreCase));

        if (navigation)
        {
            flow.FlowDirection = FlowDirection.TopDown;
            flow.WrapContents = false;
            foreach (Button button in buttons)
            {
                button.Dock = DockStyle.None;
                button.AutoSize = false;
                button.Width = 248;
                button.Height = 43;
                button.Margin = new Padding(0, 2, 0, 2);
                button.MinimumSize = new Size(248, 43);
                button.MaximumSize = new Size(248, 43);
            }
            return;
        }

        if (quickActions)
        {
            flow.FlowDirection = FlowDirection.TopDown;
            flow.WrapContents = false;
            int width = Math.Max(190, Math.Min(260, flow.ClientSize.Width - 28));
            foreach (Button button in buttons)
            {
                button.Dock = DockStyle.None;
                button.AutoSize = false;
                button.Width = width;
                button.Height = 45;
                button.Margin = new Padding(4);
                button.MinimumSize = new Size(180, 45);
                button.MaximumSize = new Size(280, 45);
            }
            return;
        }

        if (panels.Length > 0)
        {
            flow.FlowDirection = FlowDirection.LeftToRight;
            flow.WrapContents = true;
            foreach (Panel panel in panels)
            {
                panel.Dock = DockStyle.None;
                panel.AutoSize = false;
                panel.Width = 360;
                panel.Height = panel.Controls.OfType<Button>().Any() ? 190 : 165;
                panel.MinimumSize = new Size(320, 150);
                panel.MaximumSize = new Size(420, 210);
                panel.Margin = new Padding(8);
                RepairCard(panel);
            }
        }
    }

    private static void RepairCard(Panel panel)
    {
        Label? title = panel.Controls.OfType<Label>().FirstOrDefault(l => l.Dock == DockStyle.Top);
        Label? body = panel.Controls.OfType<Label>().FirstOrDefault(l => l.Dock == DockStyle.Fill);
        Button? action = panel.Controls.OfType<Button>().FirstOrDefault();

        if (title is not null)
        {
            title.Height = 34;
            title.AutoEllipsis = false;
        }

        if (body is not null)
        {
            body.Padding = new Padding(12, 8, 12, action is null ? 12 : 58);
            body.AutoEllipsis = false;
            body.UseCompatibleTextRendering = true;
        }

        if (action is not null)
        {
            action.Visible = true;
            action.Enabled = true;
            action.Dock = DockStyle.Bottom;
            action.Height = 46;
            action.BringToFront();
        }
    }

    private static void PolishCurrentPage(Form form)
    {
        Label? pageTitle = FindLabels(form).FirstOrDefault(l =>
            l.Font.Bold && l.Font.Size >= 18 &&
            (l.Text.Equals("Automazione", StringComparison.OrdinalIgnoreCase) ||
             l.Text.Equals("Innovation Lab", StringComparison.OrdinalIgnoreCase)));

        if (pageTitle is null) return;

        if (pageTitle.Text.Equals("Automazione", StringComparison.OrdinalIgnoreCase))
            PolishAutomation(form);
        else
            PolishInnovation(form);
    }

    private static void PolishAutomation(Form form)
    {
        Dictionary<string, string> descriptions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Controllo automatico"] = "ATTIVO\nVerifica Defender, firewall, firme, PUA, rete e ransomware ogni 15 minuti.",
            ["Aggiornamento firme"] = "ATTIVO\nAggiornamento automatico delle definizioni ogni 24 ore.",
            ["Scansione programmata"] = "ATTIVA\nScansione rapida automatica ogni 7 giorni.",
            ["Area di notifica"] = "ATTIVA\nIl Dobermann resta vicino all'orologio e mostra gli avvisi di sicurezza.",
            ["Controllo immediato"] = "Aggiorna le firme, verifica le protezioni e avvia la scansione solo se Defender non ne sta già eseguendo una."
        };

        foreach (Panel panel in FindPanels(form))
        {
            Label? title = panel.Controls.OfType<Label>().FirstOrDefault(l => l.Dock == DockStyle.Top);
            Label? body = panel.Controls.OfType<Label>().FirstOrDefault(l => l.Dock == DockStyle.Fill);
            if (title is null || body is null || !descriptions.TryGetValue(title.Text.Trim(), out string? text)) continue;

            body.Text = text;
            body.ForeColor = title.Text.Equals("Controllo immediato", StringComparison.OrdinalIgnoreCase)
                ? Color.Gainsboro
                : Color.FromArgb(142, 255, 0);
            RepairCard(panel);
        }
    }

    private static void PolishInnovation(Form form)
    {
        Dictionary<string, string> descriptions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Spiegazione dei rischi"] = "Traduce gli stati tecnici di Windows in indicazioni chiare e comprensibili.",
            ["Hardening consigliato"] = "Suggerisce impostazioni sicure per ridurre la superficie di attacco del PC.",
            ["Controllo download"] = "Analizza preventivamente i file scaricati usando i controlli disponibili in Windows.",
            ["Smart Defense"] = "Profili Casa, Ufficio e Massima protezione in fase di sviluppo controllato."
        };

        foreach (Panel panel in FindPanels(form))
        {
            Label? title = panel.Controls.OfType<Label>().FirstOrDefault(l => l.Dock == DockStyle.Top);
            Label? body = panel.Controls.OfType<Label>().FirstOrDefault(l => l.Dock == DockStyle.Fill);
            if (title is null || body is null || !descriptions.TryGetValue(title.Text.Trim(), out string? text)) continue;
            body.Text = text;
            body.ForeColor = Color.Gainsboro;
            RepairCard(panel);
        }
    }

    private static void RemoveDuplicateSupportButtons(Form form)
    {
        List<Button> supportButtons = FindButtons(form)
            .Where(b => b.Text.Contains("ASSISTENZA", StringComparison.OrdinalIgnoreCase))
            .OrderBy(b => b.Top)
            .ThenBy(b => b.Left)
            .ToList();

        foreach (Button duplicate in supportButtons.Skip(1).Where(b => b.Parent == form))
        {
            duplicate.Visible = false;
            duplicate.Enabled = false;
        }
    }

    private static IEnumerable<Button> FindButtons(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is Button button) yield return button;
            foreach (Button nested in FindButtons(child)) yield return nested;
        }
    }

    private static IEnumerable<Label> FindLabels(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is Label label) yield return label;
            foreach (Label nested in FindLabels(child)) yield return nested;
        }
    }

    private static IEnumerable<Panel> FindPanels(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is Panel panel) yield return panel;
            foreach (Panel nested in FindPanels(child)) yield return nested;
        }
    }
}
