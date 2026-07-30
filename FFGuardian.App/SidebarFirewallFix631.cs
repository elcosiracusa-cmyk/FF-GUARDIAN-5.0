namespace FFGuardian;

internal static class SidebarFirewallFix631
{
    private static readonly HashSet<Control> FixedControls = new();

    public static void Apply(object? sender, EventArgs e)
    {
        foreach (Form form in Application.OpenForms)
        {
            if (!form.Text.Contains("FF GUARDIAN", StringComparison.OrdinalIgnoreCase))
                continue;

            FixSidebar(form);
            FixCompactStatusCards(form);
            FixedControls.RemoveWhere(control => control.IsDisposed);
        }
    }

    private static void FixSidebar(Control root)
    {
        FlowLayoutPanel? menu = FindControls<FlowLayoutPanel>(root)
            .FirstOrDefault(flow => flow.Controls.OfType<Button>()
                .Any(button => button.Text.Contains("Dashboard", StringComparison.OrdinalIgnoreCase)));

        if (menu is null)
            return;

        Panel? sidebar = FindParentPanel(menu);
        if (sidebar is not null)
        {
            sidebar.Width = 305;
            sidebar.MinimumSize = new Size(305, 0);
        }

        if (menu.Parent is TableLayoutPanel layout)
        {
            layout.Padding = new Padding(10, 8, 10, 8);
            if (layout.RowStyles.Count >= 3)
            {
                layout.RowStyles[0].SizeType = SizeType.Absolute;
                layout.RowStyles[0].Height = 178;
                layout.RowStyles[1].SizeType = SizeType.Percent;
                layout.RowStyles[1].Height = 100;
                layout.RowStyles[2].SizeType = SizeType.Absolute;
                layout.RowStyles[2].Height = 88;
            }

            if (layout.GetControlFromPosition(0, 0) is Panel brandPanel)
            {
                PictureBox? logo = brandPanel.Controls.OfType<PictureBox>().FirstOrDefault();
                if (logo is not null) logo.Height = 118;
            }

            if (layout.GetControlFromPosition(0, 2) is Panel protectedBox)
                protectedBox.Padding = new Padding(10, 6, 10, 6);
        }

        menu.Padding = new Padding(0, 0, 18, 0);
        menu.WrapContents = false;
        menu.FlowDirection = FlowDirection.TopDown;
        menu.AutoScroll = true;
        menu.HorizontalScroll.Enabled = false;
        menu.HorizontalScroll.Visible = false;

        int usableWidth = Math.Max(230,
            menu.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 10);

        foreach (Button button in menu.Controls.OfType<Button>())
        {
            button.Width = usableWidth;
            button.Height = 39;
            button.MinimumSize = new Size(120, 39);
            button.MaximumSize = new Size(usableWidth, 39);
            button.Margin = new Padding(0, 1, 0, 1);
            button.Padding = new Padding(12, 0, 6, 0);
            button.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            button.TextAlign = ContentAlignment.MiddleLeft;
        }

        Label? brand = FindControls<Label>(sidebar ?? root)
            .FirstOrDefault(label => label.Text.Contains("Personal Security", StringComparison.OrdinalIgnoreCase));

        if (brand is not null)
        {
            brand.Text = "FF GUARDIAN\nPersonal Security by EL.CO";
            brand.Font = new Font("Segoe UI", 12.5F, FontStyle.Bold);
            brand.Height = 48;
            brand.AutoEllipsis = false;
            brand.TextAlign = ContentAlignment.MiddleCenter;
            brand.Padding = new Padding(4, 0, 4, 0);
        }
    }

    private static void FixCompactStatusCards(Control root)
    {
        foreach (TableLayoutPanel table in FindControls<TableLayoutPanel>(root))
        {
            if (table.ColumnCount < 6)
                continue;

            foreach (Panel panel in table.Controls.OfType<Panel>())
            {
                Label[] labels = panel.Controls.OfType<Label>().ToArray();
                if (labels.Length < 2)
                    continue;

                Label title = labels.FirstOrDefault(label =>
                    label.Text is "Defender" or "Tempo reale" or "Firewall" or "Firme" or "Ransomware" or "Rete e phishing")
                    ?? labels[0];
                Label body = labels.First(label => !ReferenceEquals(label, title));

                title.Font = new Font("Segoe UI", 9.2F, FontStyle.Bold);
                title.AutoSize = false;
                title.Dock = DockStyle.None;
                title.Bounds = new Rectangle(10, 7, Math.Max(70, panel.ClientSize.Width - 20), 27);
                title.TextAlign = ContentAlignment.MiddleLeft;
                title.Padding = Padding.Empty;
                title.AutoEllipsis = false;

                body.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
                body.AutoSize = false;
                body.Dock = DockStyle.None;
                body.Bounds = new Rectangle(10, 35, Math.Max(70, panel.ClientSize.Width - 20), Math.Max(34, panel.ClientSize.Height - 42));
                body.TextAlign = ContentAlignment.MiddleCenter;
                body.Padding = Padding.Empty;

                title.BringToFront();
                body.BringToFront();
                FixedControls.Add(panel);
            }
        }
    }

    private static Panel? FindParentPanel(Control control)
    {
        Control? current = control.Parent;
        while (current is not null)
        {
            if (current is Panel panel && panel.Dock == DockStyle.Left)
                return panel;
            current = current.Parent;
        }
        return null;
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
