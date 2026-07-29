namespace FFGuardian;

internal static class UiReadabilityFix
{
    private const string TitleTag = "FFG62_CARD_TITLE";
    private const string BodyTag = "FFG62_CARD_BODY";
    private const string ButtonTag = "FFG62_BUTTON";
    private static readonly HashSet<Form> ConfiguredForms = new();
    private static readonly HashSet<Control> StyledControls = new();
    private static readonly Color Background = Color.FromArgb(5, 10, 13);
    private static readonly Color TextPrimary = Color.FromArgb(248, 250, 251);
    private static readonly Color TextSecondary = Color.FromArgb(205, 215, 220);
    private static readonly Color Neon = Color.FromArgb(142, 255, 0);
    private static readonly Color NeonBright = Color.FromArgb(190, 255, 45);
    private static readonly Color PanelBack = Color.FromArgb(11, 20, 24);
    private static readonly Color ButtonBack = Color.FromArgb(20, 38, 43);
    private static readonly Color ButtonHover = Color.FromArgb(35, 68, 25);

    public static void Apply(object? sender, EventArgs e)
    {
        foreach (Form form in Application.OpenForms)
        {
            if (!form.Text.Contains("FF GUARDIAN", StringComparison.OrdinalIgnoreCase))
                continue;

            if (ConfiguredForms.Add(form))
            {
                ConfigureForm(form);
                form.ResizeEnd += (_, _) => StabilizeLayout(form);
                form.Shown += (_, _) => StabilizeLayout(form);
                form.FormClosed += (_, _) =>
                {
                    ConfiguredForms.Remove(form);
                    StyledControls.RemoveWhere(control => control.IsDisposed);
                };
            }

            ImproveNewControls(form, form.ClientSize.Width);
        }
    }

    private static void ConfigureForm(Form form)
    {
        form.AutoScaleMode = AutoScaleMode.Dpi;
        form.MinimumSize = new Size(1180, 720);
        form.BackColor = Background;
    }

    private static void StabilizeLayout(Form form)
    {
        if (form.IsDisposed) return;
        form.SuspendLayout();
        try
        {
            ReflowAllCards(form, form.ClientSize.Width);
            ResizeNavigation(form);
        }
        finally
        {
            form.ResumeLayout(true);
        }
    }

    private static void ImproveNewControls(Control parent, int windowWidth)
    {
        foreach (Control control in parent.Controls)
        {
            if (StyledControls.Add(control))
            {
                switch (control)
                {
                    case Label label:
                        ImproveLabel(label);
                        break;
                    case Button button:
                        ImproveButton(button);
                        break;
                    case FlowLayoutPanel flow:
                        ImproveFlow(flow);
                        break;
                    case Panel panel:
                        ImprovePanel(panel, windowWidth);
                        break;
                    case DataGridView grid:
                        ImproveGrid(grid);
                        break;
                    case TextBox textBox:
                        textBox.Font = new Font("Segoe UI", 11F, FontStyle.Regular);
                        textBox.ForeColor = TextPrimary;
                        textBox.BackColor = PanelBack;
                        break;
                }
            }

            if (control.HasChildren)
                ImproveNewControls(control, windowWidth);
        }
    }

    private static void ImproveLabel(Label label)
    {
        label.UseCompatibleTextRendering = true;
        label.AutoEllipsis = false;

        if (label.Font.Size >= 18F || IsPageTitle(label.Text))
        {
            label.Font = new Font("Segoe UI", Math.Max(20F, label.Font.Size), FontStyle.Bold);
            label.ForeColor = TextPrimary;
            label.Height = Math.Max(label.Height, 50);
            return;
        }

        if (label.Dock == DockStyle.Top && label.Font.Bold)
        {
            label.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            label.ForeColor = TextPrimary;
            label.Height = Math.Max(label.Height, 42);
            return;
        }

        label.Font = new Font("Segoe UI", Math.Max(10.2F, label.Font.Size), label.Font.Style);
        if (label.ForeColor.GetBrightness() < 0.45F)
            label.ForeColor = TextSecondary;
    }

    private static bool IsPageTitle(string text) => text is
        "Dashboard" or "Scansione malware" or "Firewall" or "Gmail e phishing" or
        "Automazione" or "Quarantena" or "Innovation Lab" or "Rapporti" or
        "Registro" or "Assistenza Clienti" or "Informazioni";

    private static void ImproveButton(Button button)
    {
        if (Equals(button.Tag, ButtonTag)) return;
        button.Tag = ButtonTag;
        button.AutoSize = false;
        button.UseCompatibleTextRendering = true;
        button.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Neon;
        button.FlatAppearance.BorderSize = 1;
        button.MinimumSize = new Size(Math.Max(120, button.MinimumSize.Width), Math.Max(46, button.MinimumSize.Height));
        button.Height = Math.Max(46, button.Height);
        button.Padding = new Padding(8, 2, 8, 2);
        button.Cursor = Cursors.Hand;
        if (button.BackColor.GetBrightness() < 0.12F)
            button.BackColor = ButtonBack;

        Color normalBack = button.BackColor;
        Color normalBorder = button.FlatAppearance.BorderColor;
        button.MouseEnter += (_, _) =>
        {
            button.BackColor = ButtonHover;
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = NeonBright;
        };
        button.MouseLeave += (_, _) =>
        {
            button.BackColor = normalBack;
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = normalBorder;
        };
    }

    private static void ImprovePanel(Panel panel, int windowWidth)
    {
        if (panel.Dock == DockStyle.Top && panel.Height >= 60 && panel.Height <= 150)
        {
            panel.Height = Math.Max(panel.Height, 100);
            panel.BackColor = Color.FromArgb(7, 13, 16);
            panel.Padding = new Padding(Math.Max(panel.Padding.Left, 18), 10, Math.Max(panel.Padding.Right, 18), 10);
            return;
        }

        Label? title = panel.Controls.OfType<Label>()
            .FirstOrDefault(label => label.Dock == DockStyle.Top && label.Font.Bold);
        Label? body = panel.Controls.OfType<Label>()
            .FirstOrDefault(label => label.Dock == DockStyle.Fill);

        if (title is null || body is null)
            return;

        title.Tag = TitleTag;
        body.Tag = BodyTag;
        panel.BackColor = PanelBack;
        panel.Padding = Padding.Empty;
        ResizeCard(panel, windowWidth);
        ReflowCard(panel);
    }

    private static Label? FindCardTitle(Panel panel) => panel.Controls
        .OfType<Label>()
        .FirstOrDefault(label => Equals(label.Tag, TitleTag))
        ?? panel.Controls.OfType<Label>()
            .FirstOrDefault(label => label.Dock == DockStyle.Top && label.Font.Bold);

    private static Label? FindCardBody(Panel panel) => panel.Controls
        .OfType<Label>()
        .FirstOrDefault(label => Equals(label.Tag, BodyTag))
        ?? panel.Controls.OfType<Label>()
            .FirstOrDefault(label => label.Dock == DockStyle.Fill);

    private static void ReflowCard(Panel panel)
    {
        Label? title = FindCardTitle(panel);
        Label? body = FindCardBody(panel);
        Button? action = panel.Controls.OfType<Button>().FirstOrDefault();
        if (title is null || body is null)
            return;

        title.Tag = TitleTag;
        body.Tag = BodyTag;

        const int left = 18;
        const int right = 18;
        const int top = 16;
        const int titleHeight = 40;
        const int gap = 10;
        const int buttonHeight = 48;
        const int buttonBottom = 16;
        int contentWidth = Math.Max(100, panel.ClientSize.Width - left - right);

        title.Dock = DockStyle.None;
        title.AutoSize = false;
        title.Bounds = new Rectangle(left, top, contentWidth, titleHeight);
        title.Padding = new Padding(0, 3, 0, 2);
        title.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        title.ForeColor = TextPrimary;
        title.TextAlign = ContentAlignment.MiddleLeft;
        title.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

        int bodyTop = top + titleHeight + gap;
        int reservedBottom = action is null ? 20 : buttonHeight + buttonBottom + 16;
        int bodyHeight = Math.Max(52, panel.ClientSize.Height - bodyTop - reservedBottom);

        body.Dock = DockStyle.None;
        body.AutoSize = false;
        body.Bounds = new Rectangle(left, bodyTop, contentWidth, bodyHeight);
        body.Font = new Font("Segoe UI", 10.2F, FontStyle.Regular);
        body.Padding = Padding.Empty;
        body.TextAlign = ContentAlignment.TopLeft;
        body.AutoEllipsis = false;
        body.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        if (body.ForeColor.GetBrightness() < 0.45F)
            body.ForeColor = TextSecondary;

        if (action is not null)
        {
            action.Dock = DockStyle.None;
            action.Bounds = new Rectangle(left, panel.ClientSize.Height - buttonHeight - buttonBottom, contentWidth, buttonHeight);
            action.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
        }

        title.BringToFront();
        body.BringToFront();
        action?.BringToFront();
    }

    private static void ResizeCard(Panel panel, int windowWidth)
    {
        Button? action = panel.Controls.OfType<Button>().FirstOrDefault();
        int available = Math.Max(330, windowWidth - 340);
        int columns = available >= 1320 ? 4 : available >= 900 ? 3 : 2;
        int cardWidth = Math.Clamp((available - (columns * 22)) / columns, 340, 455);
        int height = action is null ? 210 : 250;

        panel.Size = new Size(cardWidth, height);
        panel.MinimumSize = new Size(340, height);
        panel.MaximumSize = new Size(490, height + 20);
        panel.Margin = new Padding(11);
    }

    private static void ImproveFlow(FlowLayoutPanel flow)
    {
        flow.AutoScroll = true;
        flow.Padding = new Padding(12);
        bool navigation = flow.Controls.OfType<Button>()
            .Any(button => button.Text.Contains("Dashboard", StringComparison.OrdinalIgnoreCase));

        if (navigation)
        {
            flow.FlowDirection = FlowDirection.TopDown;
            flow.WrapContents = false;
            foreach (Button button in flow.Controls.OfType<Button>())
            {
                button.Width = Math.Max(250, flow.ClientSize.Width - 12);
                button.Height = 47;
                button.Margin = new Padding(0, 3, 0, 3);
            }
        }
        else
        {
            flow.FlowDirection = FlowDirection.LeftToRight;
            flow.WrapContents = true;
        }
    }

    private static void ResizeNavigation(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (control is FlowLayoutPanel flow && flow.Controls.OfType<Button>()
                .Any(button => button.Text.Contains("Dashboard", StringComparison.OrdinalIgnoreCase)))
            {
                foreach (Button button in flow.Controls.OfType<Button>())
                    button.Width = Math.Max(250, flow.ClientSize.Width - 12);
            }

            if (control.HasChildren)
                ResizeNavigation(control);
        }
    }

    private static void ReflowAllCards(Control parent, int windowWidth)
    {
        foreach (Control control in parent.Controls)
        {
            if (control is Panel panel && control is not FlowLayoutPanel &&
                FindCardTitle(panel) is not null && FindCardBody(panel) is not null)
            {
                ResizeCard(panel, windowWidth);
                ReflowCard(panel);
            }

            if (control.HasChildren)
                ReflowAllCards(control, windowWidth);
        }
    }

    private static void ImproveGrid(DataGridView grid)
    {
        grid.Font = new Font("Segoe UI", 10.5F, FontStyle.Regular);
        grid.RowTemplate.Height = 40;
        grid.ColumnHeadersHeight = 44;
        grid.EnableHeadersVisualStyles = false;
        grid.DefaultCellStyle.ForeColor = TextPrimary;
        grid.DefaultCellStyle.BackColor = PanelBack;
        grid.DefaultCellStyle.SelectionBackColor = ButtonHover;
        grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(28, 62, 20);
        grid.GridColor = Color.FromArgb(55, 75, 78);
    }
}