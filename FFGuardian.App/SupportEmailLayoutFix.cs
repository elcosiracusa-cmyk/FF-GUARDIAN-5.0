using System.Reflection;

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
                    label.Text.Equals("DATI ASSISTENZA", StringComparison.OrdinalIgnoreCase)));

            if (details is null)
                continue;

            Label? title = details.Controls.OfType<Label>().FirstOrDefault(label =>
                label.Text.Equals("DATI ASSISTENZA", StringComparison.OrdinalIgnoreCase));
            Label? body = details.Controls.OfType<Label>().FirstOrDefault(label => label != title);

            if (title is not null)
            {
                title.Dock = DockStyle.None;
                title.Location = new Point(18, 14);
                title.Size = new Size(Math.Max(250, details.ClientSize.Width - 36), 38);
                title.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                title.Padding = Padding.Empty;
                title.AutoEllipsis = false;
                title.BringToFront();
            }

            if (body is not null)
            {
                body.Dock = DockStyle.None;
                body.Location = new Point(18, 62);
                body.Size = new Size(
                    Math.Max(280, details.ClientSize.Width - 36),
                    Math.Max(190, details.ClientSize.Height - 132));
                body.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                body.Padding = Padding.Empty;
                body.Text = BuildSupportText();
                body.AutoEllipsis = false;
                body.UseCompatibleTextRendering = true;
                body.TextAlign = ContentAlignment.TopLeft;
                body.ForeColor = Color.Gainsboro;
                body.Font = new Font("Segoe UI", 11, FontStyle.Regular);
                body.BringToFront();
            }

            Button? copyButton = details.Controls.OfType<Button>().FirstOrDefault(button =>
                button.Text.Contains("COPIA EMAIL", StringComparison.OrdinalIgnoreCase));

            if (copyButton is null)
            {
                copyButton = new Button
                {
                    Name = "SupportEmailCopyButton",
                    Text = "COPIA EMAIL ASSISTENZA",
                    Height = 46,
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

            copyButton.Dock = DockStyle.None;
            copyButton.Location = new Point(18, Math.Max(120, details.ClientSize.Height - 58));
            copyButton.Size = new Size(Math.Max(280, details.ClientSize.Width - 36), 42);
            copyButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            copyButton.Visible = true;
            copyButton.Enabled = true;
            copyButton.BringToFront();
        }
    }

    private static string BuildSupportText()
    {
        Version? version = Assembly.GetExecutingAssembly().GetName().Version;
        string displayVersion = version is null
            ? "5.3.0"
            : $"{version.Major}.{version.Minor}.{version.Build}";

        return
            $"EMAIL ASSISTENZA\r\n{SupportEmail}\r\n\r\n" +
            $"Versione: FF GUARDIAN {displayVersion}\r\n" +
            $"Computer: {Environment.MachineName}\r\n" +
            $"Utente: {Environment.UserName}\r\n" +
            $"Windows: {Environment.OSVersion.Version}\r\n" +
            $"Data: {DateTime.Now:dd/MM/yyyy HH:mm}\r\n\r\n" +
            "Premi ASSISTENZA per aprire il programma di posta predefinito.";
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
