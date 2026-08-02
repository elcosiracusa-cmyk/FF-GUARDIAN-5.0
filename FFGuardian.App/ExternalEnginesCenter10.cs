using System.Runtime.CompilerServices;
using FFGuardian.Engine10;

namespace FFGuardian;

internal static class ExternalEnginesCenter10
{
    private static readonly Color Background = Color.FromArgb(3, 8, 12);
    private static readonly Color Surface = Color.FromArgb(17, 31, 39);
    private static readonly Color Neon = Color.FromArgb(160, 255, 0);
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
        TabPage? page = tabs?.TabPages.Cast<TabPage>()
            .FirstOrDefault(candidate => string.Equals(candidate.Text, "AGGIORNAMENTI", StringComparison.OrdinalIgnoreCase));
        FlowLayoutPanel? panel = page is null ? null : FindControl<FlowLayoutPanel>(page);
        if (panel is null)
            return;

        Label status = new()
        {
            Width = 820,
            Height = 92,
            BackColor = Surface,
            ForeColor = Color.White,
            Padding = new Padding(14),
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Text = BuildStatus()
        };

        Button refresh = CreateButton("AGGIORNA DATABASE CLAMAV", emphasized: true);
        refresh.Click += async (_, _) =>
        {
            refresh.Enabled = false;
            try
            {
                IReadOnlyList<string> messages = await ExternalThreatEngines10.UpdateDatabasesAsync();
                status.Text = BuildStatus() + Environment.NewLine + string.Join(" ", messages);
                MessageBox.Show(form, string.Join(Environment.NewLine, messages),
                    "FF GUARDIAN — ClamAV", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                StabilityCoordinator82.WriteStabilityLog(ex);
                MessageBox.Show(form, ex.Message, "FF GUARDIAN — Aggiornamento ClamAV",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                refresh.Enabled = true;
            }
        };

        Button openRules = CreateButton("APRI REGOLE YARA");
        openRules.Click += (_, _) =>
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FF Guardian", "Engine10", "YaraRules");
            Directory.CreateDirectory(folder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", folder)
            {
                UseShellExecute = true
            });
        };

        Button recheck = CreateButton("RILEVA MOTORI");
        recheck.Click += (_, _) => status.Text = BuildStatus();

        panel.Controls.Add(new Label
        {
            Width = 820,
            Height = 36,
            Text = "MOTORI PROFESSIONALI ESTERNI",
            ForeColor = Neon,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold)
        });
        panel.Controls.Add(status);
        panel.Controls.Add(refresh);
        panel.Controls.Add(openRules);
        panel.Controls.Add(recheck);

        _attached = true;
        Application.Idle -= AttachWhenReady;
    }

    private static string BuildStatus()
    {
        ExternalEngineStatus10 status = ExternalThreatEngines10.GetStatus();
        return
            $"CLAMAV: {(status.ClamAvAvailable ? "ATTIVO" : "NON INSTALLATO")}  ·  " +
            $"FRESHCLAM: {(status.FreshClamAvailable ? "DISPONIBILE" : "NON DISPONIBILE")}\n" +
            $"YARA REALE: {(status.YaraAvailable ? "ATTIVO" : "NON INSTALLATO")}  ·  " +
            $"FILE REGOLE: {status.YaraRuleFiles}\n" +
            "Priorità: EICAR → firme EL.CO → ClamAV/YARA → analisi euristica.";
    }

    private static Button CreateButton(string text, bool emphasized = false)
    {
        Button button = new()
        {
            Width = emphasized ? 310 : 250,
            Height = 44,
            Margin = new Padding(6),
            Text = text,
            BackColor = emphasized ? Neon : Surface,
            ForeColor = emphasized ? Color.Black : Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = Neon;
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
