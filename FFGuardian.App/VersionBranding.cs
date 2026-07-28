namespace FFGuardian;

internal static class VersionBranding
{
    private static bool _applied;

    public static void ApplyToOpenForms(object? sender, EventArgs e)
    {
        if (_applied || Application.OpenForms.Count == 0) return;

        foreach (Form form in Application.OpenForms)
        {
            form.Text = "FF GUARDIAN 5.1 — Autonomous Protection by EL.CO";
            ReplaceText(form);
        }

        _applied = true;
        Application.Idle -= ApplyToOpenForms;
    }

    private static void ReplaceText(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (!string.IsNullOrWhiteSpace(control.Text))
            {
                control.Text = control.Text
                    .Replace("FF GUARDIAN 5.0.2", "FF GUARDIAN 5.1")
                    .Replace("NAVIGATION & TOOLS FIX", "AUTONOMOUS PROTECTION")
                    .Replace("Navigation & Tools Fix", "Autonomous Protection");
            }

            if (control.HasChildren) ReplaceText(control);
        }
    }
}
