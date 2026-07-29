namespace FFGuardian;

internal static class ProfessionalCore61Ui
{
    private static readonly Color Background = Color.FromArgb(5, 8, 9);
    private static readonly Color Surface = Color.FromArgb(10, 14, 15);
    private static readonly Color SurfaceHover = Color.FromArgb(24, 45, 12);
    private static readonly Color Border = Color.FromArgb(55, 82, 66);
    private static readonly Color Neon = Color.FromArgb(145, 255, 0);
    private static readonly Color NeonBright = Color.FromArgb(195, 255, 55);
    private static readonly Color TextPrimary = Color.FromArgb(245, 248, 249);
    private static readonly Color TextSecondary = Color.FromArgb(205, 212, 214);

    public static void Apply(object? sender, EventArgs e)
    {
        foreach (Form form in Application.OpenForms)
        {
            form.Text = "FF GUARDIAN 6.1 — Professional Core by EL.CO";
            form.BackColor = Background;
            form.MinimumSize = new Size(1180, 720);
            StyleChildren(form);
        }
    }

    private static void StyleChildren(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (control is Button button)
                StyleButton(button);
            else if (control is Label label)
                StyleLabel(label);
            else if (control is DataGridView grid)
                StyleGrid(grid);
            else if (control is FlowLayoutPanel flow)
                StyleFlow(flow);
            else if (control is Panel panel)
                StylePanel(panel);

            if (control.HasChildren)
                StyleChildren(control);
        }
    }

    private static void StyleFlow(FlowLayoutPanel flow)
    {
        flow.AutoScroll = true;
        flow.BackColor = Color.Transparent;
        flow.Padding = new Padding(Math.Max(flow.Padding.Left, 8), Math.Max(flow.Padding.Top, 6), Math.Max(flow.Padding.Right, 8), Math.Max(flow.Padding.Bottom, 6));
        StylePanel(flow);
    }

    private static void StyleButton(Button button)
    {
        if (button.Tag is string tag && tag == "FFG61_STYLED")
            return;

        button.Tag = "FFG61_STYLED";
        bool navigation = button.Parent is FlowLayoutPanel navigationFlow &&
            navigationFlow.Controls.OfType<Button>().Any(candidate => candidate.Text.Contains("Dashboard", StringComparison.OrdinalIgnoreCase));

        Color normalBack = navigation ? Color.FromArgb(8, 13, 14) : Color.FromArgb(13, 20, 18);
        Color normalBorder = navigation ? Color.FromArgb(42, 75, 55) : Color.FromArgb(78, 120, 38);

        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = normalBorder;
        button.BackColor = normalBack;
        button.ForeColor = TextPrimary;
        button.Cursor = Cursors.Hand;
        button.UseCompatibleTextRendering = true;
        button.Font = new Font("Segoe UI", navigation ? 10.0f : 10.3f, FontStyle.Bold);
        button.Height = Math.Max(button.Height, navigation ? 45 : 48);
        button.Padding = navigation ? new Padding(14, 0, 10, 0) : new Padding(10, 0, 10, 0);

        button.MouseEnter += (_, _) =>
        {
            button.BackColor = SurfaceHover;
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = NeonBright;
            button.FlatAppearance.BorderSize = 2;
        };

        button.MouseLeave += (_, _) =>
        {
            button.BackColor = normalBack;
            button.ForeColor = TextPrimary;
            button.FlatAppearance.BorderColor = normalBorder;
            button.FlatAppearance.BorderSize = 1;
        };
    }

    private static void StyleLabel(Label label)
    {
        label.UseCompatibleTextRendering = true;
        label.AutoEllipsis = false;

        if (label.Font.Size >= 18f)
        {
            label.Font = new Font("Segoe UI", Math.Max(20f, label.Font.Size), FontStyle.Bold);
            label.ForeColor = TextPrimary;
            label.Height = Math.Max(label.Height, 48);
        }
        else if (label.Font.Bold || label.Dock == DockStyle.Top)
        {
            label.Font = new Font("Segoe UI", Math.Max(10.5f, label.Font.Size), FontStyle.Bold);
            label.ForeColor = TextPrimary;
        }
        else
        {
            label.Font = new Font("Segoe UI", Math.Max(10.2f, label.Font.Size), FontStyle.Regular);
            if (label.ForeColor.GetBrightness() < 0.45f)
                label.ForeColor = TextSecondary;
        }
    }

    private static void StylePanel(Panel panel)
    {
        if (panel.Dock == DockStyle.Top && panel.Height >= 60 && panel.Height <= 130)
        {
            panel.Height = Math.Max(panel.Height, 92);
            panel.BackColor = Color.FromArgb(6, 10, 11);
            panel.Padding = new Padding(Math.Max(panel.Padding.Left, 18), 10, Math.Max(panel.Padding.Right, 18), 10);
            return;
        }

        bool card = panel.Controls.OfType<Label>().Any() || panel.Controls.OfType<Button>().Any();
        if (!card)
            return;

        panel.BackColor = Surface;
        panel.Padding = new Padding(Math.Max(panel.Padding.Left, 14), Math.Max(panel.Padding.Top, 8), Math.Max(panel.Padding.Right, 14), Math.Max(panel.Padding.Bottom, 8));

        Label? title = panel.Controls.OfType<Label>().FirstOrDefault(label => label.Dock == DockStyle.Top);
        Label? body = panel.Controls.OfType<Label>().FirstOrDefault(label => label.Dock == DockStyle.Fill);
        Button? action = panel.Controls.OfType<Button>().FirstOrDefault();

        if (title is not null)
        {
            title.Height = Math.Max(title.Height, 40);
            title.Padding = new Padding(8, 8, 8, 4);
            title.ForeColor = TextPrimary;
            title.Font = new Font("Segoe UI", 10.8f, FontStyle.Bold);
            title.BringToFront();
        }

        if (body is not null)
        {
            body.Padding = new Padding(10, 14, 10, action is null ? 12 : 64);
            body.Font = new Font("Segoe UI", 10.5f, FontStyle.Regular);
            if (body.ForeColor.GetBrightness() < 0.45f)
                body.ForeColor = TextSecondary;
            body.SendToBack();
        }

        if (action is not null)
        {
            action.Dock = DockStyle.Bottom;
            action.Height = Math.Max(action.Height, 48);
            action.BringToFront();
        }
    }

    private static void StyleGrid(DataGridView grid)
    {
        grid.BackgroundColor = Background;
        grid.BorderStyle = BorderStyle.None;
        grid.RowHeadersVisible = false;
        grid.RowTemplate.Height = 38;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersHeight = 42;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(15, 28, 22);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = TextPrimary;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
        grid.DefaultCellStyle.BackColor = Surface;
        grid.DefaultCellStyle.ForeColor = TextSecondary;
        grid.DefaultCellStyle.SelectionBackColor = SurfaceHover;
        grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.DefaultCellStyle.Font = new Font("Segoe UI", 10f, FontStyle.Regular);
        grid.GridColor = Border;
    }
}
