using System.Text.RegularExpressions;

namespace FFGuardian;

internal static class Version60Fix
{
    public static void Apply(object? sender, EventArgs e)
    {
        foreach (Form form in Application.OpenForms)
        {
            form.Text = "FF GUARDIAN 6.3.1 — Sidebar & Firewall Text Fix by EL.CO";
            Normalize(form);
        }
    }

    private static void Normalize(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (control is Label or Button)
            {
                control.Text = Regex.Replace(
                    control.Text,
                    @"FF GUARDIAN (?:5|6)(?:\.\d+){0,2}",
                    "FF GUARDIAN 6.3.1",
                    RegexOptions.IgnoreCase);
                control.Text = Regex.Replace(
                    control.Text,
                    @"Versione\s+(?:5|6)(?:\.\d+){0,2}",
                    "Versione 6.3.1",
                    RegexOptions.IgnoreCase);
            }

            if (control.HasChildren) Normalize(control);
        }
    }
}
