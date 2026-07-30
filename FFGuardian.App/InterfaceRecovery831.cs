namespace FFGuardian;

internal static class InterfaceRecovery831
{
    private static readonly HashSet<Form> HookedForms = new();

    public static void Apply(object? sender, EventArgs e)
    {
        foreach (Form form in Application.OpenForms.Cast<Form>().Where(f => !f.IsDisposed))
        {
            if (!form.Text.Contains("FF GUARDIAN", StringComparison.OrdinalIgnoreCase))
                continue;

            NormalizeLabels(form);
            RecoverVisibleTiles(form);

            if (HookedForms.Add(form))
            {
                form.ResizeEnd += (_, _) => RecoverVisibleTiles(form);
                form.FormClosed += (_, _) => HookedForms.Remove(form);
            }
        }
    }

    private static void NormalizeLabels(Control root)
    {
        foreach (Control control in Descendants(root))
        {
            if (control is not Button && control is not Label)
                continue;

            if (control.Text.Contains("Cloud Ready 8.0", StringComparison.OrdinalIgnoreCase))
                control.Text = control.Text.Replace("Cloud Ready 8.0", "Cloud Ready 8.3.1", StringComparison.OrdinalIgnoreCase);

            if (control.Text.Contains("Impostazioni 8.2.1", StringComparison.OrdinalIgnoreCase) ||
                control.Text.Contains("Impostazioni 8.1", StringComparison.OrdinalIgnoreCase))
                control.Text = control.Text.Replace("Impostazioni 8.2.1", "Impostazioni 8.3.1", StringComparison.OrdinalIgnoreCase)
                                           .Replace("Impostazioni 8.1", "Impostazioni 8.3.1", StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void RecoverVisibleTiles(Form form)
    {
        foreach (FlowLayoutPanel flow in Descendants(form).OfType<FlowLayoutPanel>())
        {
            bool isNavigation = flow.Controls.OfType<Button>()
                .Any(button => button.Text.Contains("Dashboard", StringComparison.OrdinalIgnoreCase));
            if (isNavigation)
                continue;

            Panel[] tiles = flow.Controls.OfType<Panel>().ToArray();
            if (tiles.Length == 0)
                continue;

            int availableWidth = Math.Max(360, flow.ClientSize.Width - 40);
            int columns = availableWidth >= 1150 ? 3 : availableWidth >= 760 ? 2 : 1;
            int tileWidth = Math.Max(320, (availableWidth / columns) - 22);

            flow.SuspendLayout();
            flow.WrapContents = true;
            flow.FlowDirection = FlowDirection.LeftToRight;
            flow.AutoScroll = true;

            foreach (Panel tile in tiles)
            {
                tile.Dock = DockStyle.None;
                tile.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                tile.Width = tileWidth;
                tile.Height = Math.Max(tile.Height, 150);
                tile.Visible = true;
                tile.Margin = new Padding(8);
                tile.BringToFront();
            }

            flow.ResumeLayout(true);
            flow.PerformLayout();
        }
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