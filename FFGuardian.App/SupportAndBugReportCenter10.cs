using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace FFGuardian;

internal static class SupportIdentity10
{
    internal const string Creator = "EL.CO — by FFsoftware";
    internal const string SupportEmail = "alsafe127.00@gmail.com";
}

internal static class SupportAndBugReportCenter10
{
    private static bool _attached;

    [ModuleInitializer]
    internal static void Initialize() => Application.Idle += AttachWhenReady;

    private static void AttachWhenReady(object? sender, EventArgs e)
    {
        if (_attached)
            return;

        IndependentMainForm100? form = Application.OpenForms
            .OfType<IndependentMainForm100>()
            .FirstOrDefault();
        if (form is null || form.IsDisposed || !form.IsHandleCreated)
            return;

        TabControl? tabs = FindControl<TabControl>(form);
        if (tabs is null)
            return;

        if (!tabs.TabPages.Cast<TabPage>().Any(page =>
                string.Equals(page.Text, "ASSISTENZA", StringComparison.OrdinalIgnoreCase)))
        {
            tabs.TabPages.Add(BuildSupportPage(form));
        }

        _attached = true;
        Application.Idle -= AttachWhenReady;
    }

    private static TabPage BuildSupportPage(IWin32Window owner)
    {
        TabPage page = new("ASSISTENZA")
        {
            BackColor = Color.FromArgb(3, 8, 12),
            ForeColor = Color.White,
            Padding = new Padding(22)
        };

        FlowLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = page.BackColor,
            Padding = new Padding(12)
        };

        layout.Controls.Add(new Label
        {
            AutoSize = false,
            Width = 900,
            Height = 54,
            Text = "FFGUARDIAN — INFORMAZIONI SOFTWARE",
            ForeColor = Color.FromArgb(108, 255, 36),
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        });

        layout.Controls.Add(new Label
        {
            AutoSize = false,
            Width = 900,
            Height = 118,
            Text = $"Creato da {SupportIdentity10.Creator}\r\n" +
                   $"Assistenza e segnalazione bug: {SupportIdentity10.SupportEmail}\r\n" +
                   "I rapporti vengono creati localmente e mostrati all’utente prima dell’invio. " +
                   "Non vengono allegati documenti personali, password o contenuti dei file analizzati.",
            ForeColor = Color.Gainsboro,
            Font = new Font("Segoe UI", 11F),
            TextAlign = ContentAlignment.MiddleLeft
        });

        Button reportButton = CreateButton("CREA E INVIA RAPPORTO BUG");
        reportButton.Click += (_, _) => CreateAndPrepareReport(owner);
        layout.Controls.Add(reportButton);

        Button mailButton = CreateButton("CONTATTA ASSISTENZA");
        mailButton.Click += (_, _) => OpenMailClient(
            "Richiesta assistenza FFGuardian",
            "Descrivi qui il problema riscontrato.");
        layout.Controls.Add(mailButton);

        page.Controls.Add(layout);
        return page;
    }

    private static void CreateAndPrepareReport(IWin32Window owner)
    {
        try
        {
            string reportsDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FF Guardian", "Reports");
            Directory.CreateDirectory(reportsDirectory);

            string reportPath = Path.Combine(
                reportsDirectory,
                $"FFGuardian-BugReport-{DateTime.Now:yyyyMMdd-HHmmss}.txt");

            StringBuilder report = new();
            report.AppendLine("FFGUARDIAN — RAPPORTO BUG E MALFUNZIONAMENTO");
            report.AppendLine($"Creatore: {SupportIdentity10.Creator}");
            report.AppendLine($"Destinatario assistenza: {SupportIdentity10.SupportEmail}");
            report.AppendLine($"Data locale: {DateTime.Now:O}");
            report.AppendLine($"Versione: {Assembly.GetExecutingAssembly().GetName().Version}");
            report.AppendLine($"Sistema operativo: {Environment.OSVersion}");
            report.AppendLine($"Runtime: {Environment.Version}");
            report.AppendLine($"Architettura processo: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
            report.AppendLine($"Nome computer: {Environment.MachineName}");
            report.AppendLine();
            report.AppendLine("DESCRIZIONE DEL PROBLEMA:");
            report.AppendLine("[Scrivere qui cosa è successo e quali operazioni erano in corso]");

            File.WriteAllText(reportPath, report.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            DialogResult confirmation = MessageBox.Show(
                owner,
                $"Rapporto creato in:\n{reportPath}\n\nVerrà aperta un’e-mail indirizzata a {SupportIdentity10.SupportEmail}. " +
                "Allega il rapporto prima di inviare. Continuare?",
                "FFGuardian — Rapporto bug",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (confirmation != DialogResult.Yes)
                return;

            OpenMailClient(
                $"FFGuardian bug report — {Environment.MachineName}",
                $"È stato creato un rapporto diagnostico locale.\r\nPercorso: {reportPath}\r\n\r\nAllegare il file prima dell’invio.");

            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{reportPath}\"")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            MessageBox.Show(owner, ex.Message, "FFGuardian — Rapporto non creato",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void OpenMailClient(string subject, string body)
    {
        string uri = $"mailto:{SupportIdentity10.SupportEmail}" +
                     $"?subject={Uri.EscapeDataString(subject)}" +
                     $"&body={Uri.EscapeDataString(body)}";
        Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
    }

    private static Button CreateButton(string text)
    {
        Button button = new()
        {
            Width = 330,
            Height = 46,
            Margin = new Padding(0, 8, 0, 8),
            Text = text,
            BackColor = Color.FromArgb(17, 31, 39),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(108, 255, 36);
        return button;
    }

    private static T? FindControl<T>(Control root) where T : Control
    {
        if (root is T match)
            return match;
        foreach (Control child in root.Controls)
        {
            T? found = FindControl<T>(child);
            if (found is not null)
                return found;
        }
        return null;
    }
}
