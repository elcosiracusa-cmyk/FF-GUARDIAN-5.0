namespace FFGuardian;

internal static class SupportEmailLayoutFix
{
    private const string SupportEmail = "alsafe127.00@gmail.com";

    public static void Apply(object? sender, EventArgs e)
    {
        foreach (Form form in Application.OpenForms)
        {
            Label? pageTitle = FindLabels(form).FirstOrDefault(label =>
                label.Text.Equals("Assistenza Clienti", StringComparison.OrdinalIgnoreCase));

            if (pageTitle is null)
                continue;

            Panel? details = FindPanels(form).FirstOrDefault(panel =>
                panel.Controls.OfType<Label>().Any(label =>
                    label.Dock == DockStyle.Top &&
                    label.Text.Equals("DATI ASSISTENZA", StringComparison.OrdinalIgnoreCase)));

            if (details is null)
                continue;

            Label? title = details.Controls.OfType<Label>().FirstOrDefault(label => label.Dock == DockStyle.Top);
            Label? body = details.Controls.OfType<Label>().FirstOrDefault(label => label.Dock == DockStyle.Fill);

            if (title is not null)
            {
                title.Height = 38;
                title.Padding = new Padding(4, 2, 4, 2);
                title.BringToFront();
            }

            if (body is not null)
            {
                body.Padding = new Padding(18, 18, 18, 72);
                body.Text =
                    $"Email assistenza:\r\n{SupportEmail}\r\n\r\n" +
                    $"Versione: FF GUARDIAN 5.2.9\r\n" +
                    $"Computer: {Environment.MachineName}\r\n" +
                    $"Utente: {Environment.UserName}\r\n" +
                    $"Windows: {Environment.OSVersion.Version}\r\n" +
                    $"Data: {DateTime.Now:dd/MM/yyyy HH:mm}\r\n\r\n" +
                    "La mail viene aperta nel programma di posta predefinito.";
                body.AutoEllipsis = false;
                body.UseCompatibleTextRendering = true;
                body.ForeColor = Color.Gainsboro;
                body.Font = new Font("Segoe UI", 11, FontStyle.Regular);
            }

            Button? copyButton = details.Controls.OfType<Button>().FirstOrDefault(button =>
                button.Text.Contains("COPIA", StringComparison.OrdinalIgnoreCase));

            if (copyButton is null)
            {
                copyButton = new Button
                {
                    Text = "COPIA EMAIL ASSISTENZA",
                    Dock = DockStyle.Bottom,
                    Height = 48,
                    BackColor = Color.FromArgb(35, 70, 15),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat,
                    Font = new Font("Segoe UI", 9, FontStyle.Bold),
                    Cursor = Cursors.Hand
                };
                copyButton.FlatAppearance.BorderColor = Color.FromArgb(142, 255, 0);
                copyButton.FlatAppearance.BorderSize = 1;
                copyButton.Click += (_, _) =>
                {
                    Clipboard.SetText(SupportEmail);
                    MessageBox.Show(
                        $"Indirizzo copiato:\n{SupportEmail}",
                        "FF GUARDIAN - Assistenza",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                };
                details.Controls.Add(copyButton);
            }

            copyButton.BringToFront();
        }
    }

    private static IEnumerable<Label> FindLabels(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is Label label)
                yield return label;

            foreach (Label nested in FindLabels(child))
                yield return nested;
        }
    }

    private static IEnumerable<Panel> FindPanels(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is Panel panel)
                yield return panel;

            foreach (Panel nested in FindPanels(child))
                yield return nested;
        }
    }
}
