namespace FFGuardian;

internal static class LayoutRepair
{
    public static void ApplyToOpenForms(object? sender, EventArgs e)
    {
        foreach (Form form in Application.OpenForms)
        {
            form.Text = "FF GUARDIAN 5.2.5 — Dashboard & Commands Fix by EL.CO";
            RepairTree(form);
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
        flow.WrapContents = false;
        flow.AutoScroll = true;

        Button[] buttons = flow.Controls.OfType<Button>().ToArray();
        Panel[] panels = flow.Controls.OfType<Panel>().ToArray();

        bool navigation = buttons.Any(b => b.Text.Contains("Dashboard", StringComparison.OrdinalIgnoreCase));
        bool quickActions = buttons.Any(b => b.Text.Contains("SCANSIONE RAPIDA", StringComparison.OrdinalIgnoreCase));

        if (navigation)
        {
            flow.FlowDirection = FlowDirection.TopDown;
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
                if (panel.Width < 300 || panel.Width > 500) panel.Width = 360;
                if (panel.Height < 120 || panel.Height > 240) panel.Height = 170;
                panel.MinimumSize = new Size(320, 140);
                panel.MaximumSize = new Size(420, 220);
                panel.Margin = new Padding(8);
            }
        }
    }

    private static void RemoveDuplicateSupportButtons(Form form)
    {
        List<Button> supportButtons = FindButtons(form)
            .Where(b => b.Text.Contains("ASSISTENZA", StringComparison.OrdinalIgnoreCase))
            .OrderBy(b => b.Top)
            .ThenBy(b => b.Left)
            .ToList();

        // Mantiene il pulsante originale dell'intestazione e rimuove solo eventuali copie sovrapposte.
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
}
