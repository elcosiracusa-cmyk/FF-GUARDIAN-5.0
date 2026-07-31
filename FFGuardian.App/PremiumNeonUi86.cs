namespace FFGuardian;

internal static class PremiumNeonUi86
{
    private static readonly HashSet<Form> HookedForms = new();
    private static readonly HashSet<Button> HookedNavigation = new();
    private static readonly Color Background = Color.FromArgb(2, 7, 10);
    private static readonly Color Sidebar = Color.FromArgb(4, 11, 15);
    private static readonly Color Header = Color.FromArgb(3, 10, 14);
    private static readonly Color Card = Color.FromArgb(10, 19, 24);
    private static readonly Color CardAlt = Color.FromArgb(13, 24, 30);
    private static readonly Color Border = Color.FromArgb(62, 78, 84);
    private static readonly Color Neon = Color.FromArgb(173, 255, 0);
    private static readonly Color PrimaryText = Color.FromArgb(248, 250, 252);
    private static readonly Color SecondaryText = Color.FromArgb(211, 218, 222);

    public static void Apply(object? sender, EventArgs e)
    {
        foreach (Form form in Application.OpenForms.Cast<Form>().Where(f => !f.IsDisposed))
        {
            if (!form.Text.Contains("FF GUARDIAN", StringComparison.OrdinalIgnoreCase))
                continue;

            ApplyTheme(form);
            HookNavigation(form);

            if (HookedForms.Add(form))
            {
                form.ResizeEnd += (_, _) => ApplyTheme(form);
                form.FormClosed += (_, _) =>
                {
                    HookedForms.Remove(form);
                    HookedNavigation.RemoveWhere(button => button.IsDisposed || button.FindForm() == form);
                };
            }
        }
    }

    private static void HookNavigation(Form form)
    {
        foreach (Button button in Descendants(form).OfType<Button>().Where(IsNavigationButton))
        {
            if (!HookedNavigation.Add(button))
                continue;

            button.Click += (_, _) =>
            {
                if (form.IsDisposed || !form.IsHandleCreated)
                    return;

                form.BeginInvoke((MethodInvoker)(() => ApplyTheme(form)));
            };
        }
    }

    private static bool IsNavigationButton(Button button)
    {
        Control? parent = button.Parent;
        if (parent is not FlowLayoutPanel flow)
            return false;

        return flow.Controls.OfType<Button>().Any(b => b.Text.Contains("Dashboard", StringComparison.OrdinalIgnoreCase));
    }

    private static void ApplyTheme(Form form)
    {
        form.SuspendLayout();
        form.BackColor = Background;
        form.ForeColor = PrimaryText;
        form.Font = new Font("Segoe UI", 10F);
        form.Text = "FF GUARDIAN 8.6 — Premium Neon UI by EL.CO";

        Panel? sidebar = form.Controls.OfType<Panel>().FirstOrDefault(panel => panel.Dock == DockStyle.Left);
        if (sidebar is not null)
        {
            sidebar.Width = 282;
            sidebar.BackColor = Sidebar;
            StyleSidebar(sidebar);
        }

        Panel? header = form.Controls.OfType<Panel>().FirstOrDefault(panel => panel.Dock == DockStyle.Top);
        if (header is not null)
        {
            header.Height = 82;
            header.BackColor = Header;
            StyleHeader(header);
        }

        foreach (Control control in Descendants(form))
        {
            switch (control)
            {
                case FlowLayoutPanel flow:
                    StyleFlow(flow);
                    break;
                case TableLayoutPanel table:
                    table.BackColor = Background;
                    break;
                case Panel panel:
                    StylePanel(panel, sidebar, header);
                    break;
                case Button button:
                    StyleButton(button);
                    break;
                case Label label:
                    StyleLabel(label);
                    break;
                case DataGridView grid:
                    StyleGrid(grid);
                    break;
                case TextBox textBox:
                    textBox.BackColor = Color.FromArgb(5, 13, 17);
                    textBox.ForeColor = PrimaryText;
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    break;
            }
        }

        StyleScanTiles(form);
        form.ResumeLayout(true);
        form.PerformLayout();
        form.Invalidate(true);
    }

    private static void StyleSidebar(Panel sidebar)
    {
        foreach (FlowLayoutPanel menu in Descendants(sidebar).OfType<FlowLayoutPanel>())
        {
            menu.BackColor = Sidebar;
            menu.FlowDirection = FlowDirection.TopDown;
            menu.WrapContents = false;
            menu.AutoScroll = true;
            menu.HorizontalScroll.Enabled = false;
            menu.HorizontalScroll.Visible = false;

            int width = Math.Max(220, menu.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4);
            foreach (Button button in menu.Controls.OfType<Button>())
            {
                button.Width = width;
                button.Height = 40;
                button.Margin = new Padding(0, 1, 0, 1);
                button.Padding = new Padding(14, 0, 4, 0);
                button.TextAlign = ContentAlignment.MiddleLeft;
                button.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
                button.ForeColor = PrimaryText;
                button.BackColor = Sidebar;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.BorderColor = Color.FromArgb(18, 32, 38);
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(25, 48, 18);
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(34, 68, 12);
            }
        }
    }

    private static void StyleHeader(Panel header)
    {
        foreach (Label label in header.Controls.OfType<Label>())
        {
            label.BackColor = Header;
            label.ForeColor = label.Font.Size >= 16F ? PrimaryText : SecondaryText;
        }

        foreach (Button button in header.Controls.OfType<Button>())
        {
            button.Height = 48;
            button.ForeColor = PrimaryText;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = button.Text.Contains("AGGIORNA", StringComparison.OrdinalIgnoreCase) ? Neon : Border;
        }
    }

    private static void StylePanel(Panel panel, Panel? sidebar, Panel? header)
    {
        if (ReferenceEquals(panel, sidebar) || ReferenceEquals(panel, header))
            return;

        bool pageHeader = panel.Dock == DockStyle.Top && panel.Height is >= 60 and <= 100;
        bool cardLike = panel.Controls.Count > 0 && (panel.Margin.All > 0 || panel.Padding.All > 0);

        if (pageHeader)
            panel.BackColor = Header;
        else if (cardLike)
            panel.BackColor = Card;
        else
            panel.BackColor = Background;
    }

    private static void StyleFlow(FlowLayoutPanel flow)
    {
        bool navigation = flow.Controls.OfType<Button>().Any(b => b.Text.Contains("Dashboard", StringComparison.OrdinalIgnoreCase));
        if (navigation)
            return;

        flow.BackColor = Background;
        flow.FlowDirection = FlowDirection.LeftToRight;
        flow.WrapContents = true;
        flow.AutoScroll = true;
        flow.Padding = new Padding(8);
    }

    private static void StyleButton(Button button)
    {
        if (IsNavigationButton(button))
            return;

        button.UseVisualStyleBackColor = false;
        button.BackColor = Color.FromArgb(5, 12, 15);
        button.ForeColor = PrimaryText;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Neon;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(28, 49, 11);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(42, 72, 12);
        button.Font = new Font("Segoe UI", Math.Max(9F, button.Font.Size), FontStyle.Bold);
        button.Cursor = Cursors.Hand;
    }

    private static void StyleLabel(Label label)
    {
        if (label.BackColor != Color.Transparent)
            label.BackColor = label.Parent?.BackColor ?? Background;

        if (label.Font.Size >= 16F || label.Font.Bold)
            label.ForeColor = PrimaryText;
        else if (label.ForeColor != Neon && label.ForeColor != Color.OrangeRed && label.ForeColor != Color.Gold)
            label.ForeColor = SecondaryText;

        label.UseMnemonic = false;
    }

    private static void StyleScanTiles(Form form)
    {
        bool scanPageVisible = Descendants(form).OfType<Label>()
            .Any(label => label.Visible && label.Text.Trim().Equals("Scansione malware", StringComparison.OrdinalIgnoreCase));
        if (!scanPageVisible)
            return;

        foreach (FlowLayoutPanel flow in Descendants(form).OfType<FlowLayoutPanel>())
        {
            if (flow.Controls.OfType<Button>().Any(b => b.Text.Contains("Dashboard", StringComparison.OrdinalIgnoreCase)))
                continue;

            Panel[] tiles = flow.Controls.OfType<Panel>().Where(panel => panel.Visible).ToArray();
            if (tiles.Length == 0)
                continue;

            int available = Math.Max(780, flow.ClientSize.Width - 38);
            int columns = available >= 1050 ? 4 : available >= 760 ? 2 : 1;
            int width = Math.Max(300, available / columns - 18);

            foreach (Panel tile in tiles)
            {
                tile.Dock = DockStyle.None;
                tile.Width = width;
                tile.Height = 270;
                tile.MinimumSize = new Size(290, 250);
                tile.Margin = new Padding(8);
                tile.Padding = new Padding(22, 18, 22, 18);
                tile.BackColor = CardAlt;
                tile.Visible = true;

                Label[] labels = Descendants(tile).OfType<Label>().ToArray();
                foreach (Label label in labels)
                {
                    label.AutoEllipsis = false;
                    label.ForeColor = SecondaryText;
                    label.BackColor = CardAlt;
                }

                Label? title = labels
                    .Where(label => !string.IsNullOrWhiteSpace(label.Text))
                    .OrderByDescending(label => label.Font.Size)
                    .FirstOrDefault();
                if (title is not null)
                {
                    title.ForeColor = PrimaryText;
                    title.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
                    title.Height = Math.Max(title.Height, 38);
                }

                foreach (Label description in labels.Where(label => !ReferenceEquals(label, title)))
                {
                    description.ForeColor = SecondaryText;
                    description.Font = new Font("Segoe UI", 10.2F, FontStyle.Regular);
                    description.AutoSize = false;
                    description.Height = Math.Max(description.Height, 64);
                }

                foreach (Button action in Descendants(tile).OfType<Button>())
                {
                    action.Height = 50;
                    action.MinimumSize = new Size(190, 46);
                    action.BackColor = Color.FromArgb(4, 11, 14);
                    action.ForeColor = PrimaryText;
                    action.FlatStyle = FlatStyle.Flat;
                    action.FlatAppearance.BorderColor = Neon;
                    action.FlatAppearance.BorderSize = 1;
                    action.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
                }
            }

            flow.PerformLayout();
        }
    }

    private static void StyleGrid(DataGridView grid)
    {
        grid.BackgroundColor = Background;
        grid.GridColor = Border;
        grid.BorderStyle = BorderStyle.None;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(27, 50, 14);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = PrimaryText;
        grid.DefaultCellStyle.BackColor = Card;
        grid.DefaultCellStyle.ForeColor = PrimaryText;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(38, 75, 12);
        grid.DefaultCellStyle.SelectionForeColor = PrimaryText;
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;
            foreach (Control nested in Descendants(child))
                yield return nested;
        }
    }
}