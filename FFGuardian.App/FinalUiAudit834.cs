using System.Text.RegularExpressions;

namespace FFGuardian;

internal static class FinalUiAudit834
{
    private static readonly HashSet<Form> HookedForms = new();
    private static readonly HashSet<Button> HookedButtons = new();
    private static readonly Color Bg = Color.FromArgb(3, 8, 12);
    private static readonly Color Surface = Color.FromArgb(9, 20, 27);
    private static readonly Color Neon = Color.FromArgb(142, 255, 0);

    public static void Apply(object? sender, EventArgs e)
    {
        foreach (Form form in Application.OpenForms.Cast<Form>().Where(f => !f.IsDisposed))
        {
            if (!form.Text.Contains("FF GUARDIAN", StringComparison.OrdinalIgnoreCase))
                continue;

            NormalizeAllText(form);
            RepairLayout(form);
            HookNavigation(form);

            if (HookedForms.Add(form))
            {
                form.Resize += (_, _) => RepairLayout(form);
                form.Shown += (_, _) => RepairLayout(form);
                form.FormClosed += (_, _) =>
                {
                    HookedForms.Remove(form);
                    HookedButtons.RemoveWhere(button => button.IsDisposed || button.FindForm() == form);
                };
            }
        }
    }

    private static void HookNavigation(Form form)
    {
        foreach (Button button in Descendants(form).OfType<Button>())
        {
            if (!IsNavigationButton(button) || !HookedButtons.Add(button))
                continue;

            button.Click += (_, _) =>
            {
                if (form.IsDisposed || !form.IsHandleCreated)
                    return;

                form.BeginInvoke((MethodInvoker)(() =>
                {
                    NormalizeAllText(form);
                    RepairLayout(form);
                }));
            };
        }
    }

    private static bool IsNavigationButton(Button button)
    {
        string text = button.Text;
        return text.Contains("Dashboard", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Scansione", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Firewall", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Gmail", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Automazione", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Quarantena", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Innovation", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Rapporti", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Registro", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Assistenza", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Informazioni", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Stato sistema", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Impostazioni", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Cloud Ready", StringComparison.OrdinalIgnoreCase);
    }

    private static void NormalizeAllText(Control root)
    {
        foreach (Control control in DescendantsAndSelf(root))
        {
            if (control is not Label and not Button)
                continue;

            string text = control.Text;
            text = Regex.Replace(
                text,
                @"FF GUARDIAN (?:5\.2|6\.0|6\.2|6\.3|8\.0|8\.1|8\.2(?:\.\d+)?|8\.3(?:\.[0-4])?)(?!\.\d)",
                "FF GUARDIAN 8.3.5",
                RegexOptions.IgnoreCase);
            text = Regex.Replace(
                text,
                @"Versione\s+(?:5\.2|6\.0|6\.2|6\.3|8\.0|8\.1|8\.2(?:\.\d+)?|8\.3(?:\.[0-4])?)(?!\.\d)",
                "Versione 8.3.5",
                RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"Cloud Ready (?:8\.0|8\.3(?:\.[0-4])?)", "Cloud Ready 8.3.5", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"Impostazioni (?:8\.1|8\.2\.1|8\.3(?:\.[0-4])?)", "Impostazioni 8.3.5", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"Stato sistema 8\.3(?:\.[0-4])?", "Stato sistema 8.3.5", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"CENTRO RAPPORTI DEFINITIVO 8\.3(?:\.[0-4])?", "CENTRO RAPPORTI DEFINITIVO 8.3.5", RegexOptions.IgnoreCase);
            control.Text = text;
        }
    }

    private static void RepairLayout(Form form)
    {
        foreach (FlowLayoutPanel flow in Descendants(form).OfType<FlowLayoutPanel>())
        {
            bool navigation = flow.Controls.OfType<Button>().Any(b => b.Text.Contains("Dashboard", StringComparison.OrdinalIgnoreCase));
            if (navigation)
            {
                RepairNavigation(flow);
                continue;
            }

            RepairTileFlow(flow);
        }

        RepairReportsCard(form);
        EnsureVisiblePage(form);
    }

    private static void RepairNavigation(FlowLayoutPanel menu)
    {
        menu.SuspendLayout();
        menu.FlowDirection = FlowDirection.TopDown;
        menu.WrapContents = false;
        menu.AutoScroll = true;
        menu.HorizontalScroll.Enabled = false;
        menu.HorizontalScroll.Visible = false;

        int width = Math.Max(220, menu.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 6);
        foreach (Button button in menu.Controls.OfType<Button>())
        {
            button.Dock = DockStyle.None;
            button.Width = width;
            button.Height = Math.Clamp(button.Height, 36, 42);
            button.Margin = new Padding(0, 1, 0, 1);
            button.TextAlign = ContentAlignment.MiddleLeft;
        }
        menu.ResumeLayout(true);
    }

    private static void RepairTileFlow(FlowLayoutPanel flow)
    {
        Panel[] tiles = flow.Controls.OfType<Panel>()
            .Where(panel => panel.Name != "FFG832_DEFINITIVE_REPORTS")
            .ToArray();
        if (tiles.Length == 0)
            return;

        int available = Math.Max(340, flow.ClientSize.Width - 36);
        int columns = available >= 1120 ? 3 : available >= 720 ? 2 : 1;
        int width = Math.Max(300, available / columns - 20);

        flow.SuspendLayout();
        flow.Dock = DockStyle.Fill;
        flow.FlowDirection = FlowDirection.LeftToRight;
        flow.WrapContents = true;
        flow.AutoScroll = true;
        flow.BackColor = Bg;

        foreach (Panel tile in tiles)
        {
            tile.Dock = DockStyle.None;
            tile.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            tile.Width = width;
            tile.Height = Math.Max(tile.Height, 155);
            tile.MinimumSize = new Size(300, 140);
            tile.Margin = new Padding(8);
            tile.Visible = true;
        }
        flow.ResumeLayout(true);
        flow.PerformLayout();
    }

    private static void RepairReportsCard(Form form)
    {
        Control? card = Descendants(form).FirstOrDefault(c => c.Name == "FFG832_DEFINITIVE_REPORTS");
        if (card is null || card.Parent is null)
            return;

        card.Dock = DockStyle.Bottom;
        card.Height = Math.Clamp(card.Height, 180, 210);
        card.MinimumSize = new Size(0, 180);
        card.BackColor = Surface;
        card.Visible = true;
        card.BringToFront();

        foreach (Button button in Descendants(card).OfType<Button>())
        {
            button.Height = 44;
            button.Width = Math.Min(260, Math.Max(200, (card.ClientSize.Width - 60) / 2));
            button.FlatAppearance.BorderColor = Neon;
        }
    }

    private static void EnsureVisiblePage(Form form)
    {
        Panel? pageHost = form.Controls.OfType<Panel>()
            .FirstOrDefault(panel => panel.Dock == DockStyle.Fill);
        if (pageHost is null)
            return;

        foreach (Control page in pageHost.Controls)
        {
            page.Dock = DockStyle.Fill;
            page.Visible = true;
        }
        pageHost.Visible = true;
    }

    private static IEnumerable<Control> DescendantsAndSelf(Control root)
    {
        yield return root;
        foreach (Control child in Descendants(root))
            yield return child;
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