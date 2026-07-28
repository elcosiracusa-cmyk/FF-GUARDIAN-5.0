namespace FFGuardian;

internal static class VersionBranding
{
    public static void ApplyToOpenForms(object? sender, EventArgs e)
    {
        foreach (Form form in Application.OpenForms)
        {
            form.Text = "FF GUARDIAN 5.2.4 — Commands & Layout Fix by EL.CO";
            RepairTree(form);
        }
    }

    private static void RepairTree(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (!string.IsNullOrWhiteSpace(control.Text))
            {
                control.Text = control.Text
                    .Replace("FF GUARDIAN 5.1", "FF GUARDIAN 5.2.4")
                    .Replace("FF GUARDIAN 5.2.3", "FF GUARDIAN 5.2.4")
                    .Replace("FF GUARDIAN 5.2.2", "FF GUARDIAN 5.2.4")
                    .Replace("FF GUARDIAN 5.2.1", "FF GUARDIAN 5.2.4")
                    .Replace("FF GUARDIAN 5.2", "FF GUARDIAN 5.2.4")
                    .Replace("AUTONOMOUS PROTECTION", "DOBERMANN PERSONAL SECURITY")
                    .Replace("Autonomous Protection", "Dobermann Personal Security");
            }

            if (control is FlowLayoutPanel flow)
            {
                flow.Visible = true;
                flow.BringToFront();
                foreach (Control child in flow.Controls)
                {
                    child.Dock = DockStyle.None;
                    child.Visible = true;
                    if (child.Width < 300) child.Width = 360;
                    if (child.Height < 120) child.Height = 170;
                }
                flow.PerformLayout();
            }

            if (control.HasChildren) RepairTree(control);
        }
    }
}
