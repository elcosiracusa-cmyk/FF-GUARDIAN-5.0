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

            form.Text = form == Application.OpenForms.Cast<Form>().FirstOrDefault(f =>
                    Descendants(f).OfType<Button>().Any(b => b.Text.Contains("Dashboard", StringComparison.OrdinalIgnoreCase)))
                ? "FF GUARDIAN 8.3.6 — Diagnostic Reliability Fix by EL.CO"
                : form.Text;

            foreach (Control control in Descendants(form))
            {
                if (control is not Label and not Button)
                    continue;

                string text = control.Text;
                text = Regex.Replace(text, @"FF GUARDIAN (?:5|6|8)(?:\.\d+){0,2}", "FF GUARDIAN 8.3.6", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, @"Versione\s+(?:5|6|8)(?:\.\d+){0,2}", "Versione 8.3.6", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, @"Cloud Ready (?:5|6|8)(?:\.\d+){0,2}", "Cloud Ready 8.3.6", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, @"Impostazioni (?:5|6|8)(?:\.\d+){0,2}", "Impostazioni 8.3.6", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, @"Stato sistema (?:5|6|8)(?:\.\d+){0,2}", "Stato sistema 8.3.6", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, @"CENTRO RAPPORTI DEFINITIVO (?:5|6|8)(?:\.\d+){0,2}", "CENTRO RAPPORTI DEFINITIVO 8.3.6", RegexOptions.IgnoreCase);
                text = Regex.Replace(text, @"Diagnostica avanzata (?:5|6|8)(?:\.\d+){0,2}", "Diagnostica avanzata 8.3.6", RegexOptions.IgnoreCase);
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
