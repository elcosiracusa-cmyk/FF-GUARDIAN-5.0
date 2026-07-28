using System.Diagnostics;

namespace FFGuardian;

internal static class SupportButtonInjector
{
    private const string SupportEmail = "alsafe127.00@gmail.com";

    public static void ApplyToOpenForms(object? sender, EventArgs e)
    {
        foreach (Form form in Application.OpenForms)
        {
            if (form.Controls.ContainsKey("FixedSupportButton"))
                continue;

            Button button = new()
            {
                Name = "FixedSupportButton",
                Text = "ASSISTENZA CLIENTI",
                Width = 210,
                Height = 46,
                BackColor = Color.FromArgb(78, 145, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(Math.Max(10, form.ClientSize.Width - 232), 18)
            };

            button.FlatAppearance.BorderColor = Color.FromArgb(142, 255, 0);
            button.FlatAppearance.BorderSize = 2;
            button.Click += (_, _) => OpenSupportEmail();
            form.Controls.Add(button);
            button.BringToFront();

            form.Resize += (_, _) =>
            {
                button.Location = new Point(Math.Max(10, form.ClientSize.Width - 232), 18);
                button.BringToFront();
            };
        }
    }

    private static void OpenSupportEmail()
    {
        string subject = Uri.EscapeDataString("Supporto FF GUARDIAN 5.2.3");
        string body = Uri.EscapeDataString($"Descrizione problema:\r\n\r\nVersione: FF GUARDIAN 5.2.3\r\nComputer: {Environment.MachineName}\r\nUtente: {Environment.UserName}\r\nData: {DateTime.Now:dd/MM/yyyy HH:mm}");
        Process.Start(new ProcessStartInfo($"mailto:{SupportEmail}?subject={subject}&body={body}") { UseShellExecute = true });
    }
}
