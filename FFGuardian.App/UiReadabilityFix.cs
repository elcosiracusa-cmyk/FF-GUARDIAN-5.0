namespace FFGuardian;

internal static class UiReadabilityFix
{
    private const string TitleTag = "FFG611_CARD_TITLE";
    private const string BodyTag = "FFG611_CARD_BODY";
    private static readonly HashSet<Form> ConfiguredForms = new();
    private static readonly HashSet<Control> StyledControls = new();
    private static readonly Color TextPrimary = Color.FromArgb(245, 248, 250);
    private static readonly Color TextSecondary = Color.FromArgb(210, 220, 225);
    private static readonly Color Neon = Color.FromArgb(142, 255, 0);
    private static readonly Color PanelBack = Color.FromArgb(11, 22, 27);
    private static readonly Color ButtonBack = Color.FromArgb(24, 48, 55);

    public static void Apply(object? sender, EventArgs e)
    {
        foreach (Form form in Application.OpenForms)
        {
            if (!form.Text.Contains("FF GUARDIAN", StringComparison.OrdinalIgnoreCase))
                continue;

            if (ConfiguredForms.Add(form))
            {
                form.AutoScaleMode = AutoScaleMode.Dpi;
                form.MinimumSize = new Size(1180, 720);
                form.BackColor = Color.FromArgb(5, 12, 16);
                form.ResizeEnd += (_, _) => ReflowAllCards(form, form.ClientSize.Width);
                form.FormClosed += (_, _) =>
                {
                    ConfiguredForms.Remove(form);
                    StyledControls.RemoveWhere(control => control.IsDisposed);
                };
            }

            ImproveNewControls(form, form.ClientSize.Width);
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
            label.Height = Math.Max(label.Height, 48);
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
        if (button.BackColor.GetBrightness() < 0.12F)
            button.BackColor = ButtonBack;
    }

    private static void ImprovePanel(Panel panel, int windowWidth)
    {
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

        const int left = 16;
        const int right = 16;
        const int top = 14;
        const int titleHeight = 38;
        const int gap = 8;
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
        int reservedBottom = action is null ? 18 : buttonHeight + buttonBottom + 14;
        int bodyHeight = Math.Max(46, panel.ClientSize.Height - bodyTop - reservedBottom);

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
            action.Bounds = new Rectangle(
                left,
                panel.ClientSize.Height - buttonHeight - buttonBottom,
                contentWidth,
                buttonHeight);
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
        int columns = available >= 1250 ? 4 : available >= 850 ? 3 : 2;
        int cardWidth = Math.Clamp((available - (columns * 20)) / columns, 330, 440);
        int height = action is null ? 200 : 235;

        panel.Size = new Size(cardWidth, height);
        panel.MinimumSize = new Size(330, height);
        panel.MaximumSize = new Size(470, height + 30);
        panel.Margin = new Padding(10);
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
        grid.RowTemplate.Height = 38;
        grid.ColumnHeadersHeight = 42;
        grid.DefaultCellStyle.ForeColor = TextPrimary;
        grid.DefaultCellStyle.BackColor = PanelBack;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(35, 80, 0);
    }
}
