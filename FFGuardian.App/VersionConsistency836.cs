using System.Text.RegularExpressions;

namespace FFGuardian;

internal static class VersionConsistency836
{
    public static void Apply(object? sender, EventArgs e)
    {
        foreach (Form form in Application.OpenForms.Cast<Form>().Where(f => !f.IsDisposed))
        {
            if (!form.Text.Contains("FF GUARDIAN", StringComparison.OrdinalIgnoreCase))
                continue;

            bool isMain = Descendants(form).OfType<Button>()
                .Any(button => button.Text.Contains("Dashboard", StringComparison.OrdinalIgnoreCase));
            if (isMain)
                form.Text = "FF GUARDIAN 8.4 — No-Flicker Stability Rebuild by EL.CO";

            foreach (Control control in Descendants(form))
            {
                if (control is not Label and not Button)
                    continue;

                string text = control.Text;
                text = Regex.Replace(text, @"FF GUARDIAN (?:5|6|8)(?:\.\d+){0,3}", "FF GUARDIAN 8.4", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, @"Versione\s+(?:5|6|8)(?:\.\d+){0,3}", "Versione 8.4", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, @"Cloud Ready (?:5|6|8)(?:\.\d+){0,3}", "Cloud Ready 8.4", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, @"Impostazioni (?:5|6|8)(?:\.\d+){0,3}", "Impostazioni 8.4", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, @"Stato sistema (?:5|6|8)(?:\.\d+){0,3}", "Stato sistema 8.4", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, @"CENTRO RAPPORTI DEFINITIVO (?:5|6|8)(?:\.\d+){0,3}", "CENTRO RAPPORTI DEFINITIVO 8.4", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, @"Diagnostica avanzata (?:5|6|8)(?:\.\d+){0,3}", "Diagnostica avanzata 8.4", RegexOptions.IgnoreCase);
                control.Text = text;
            }
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