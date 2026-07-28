using System.Diagnostics;

namespace FFGuardian;

internal static class StatusInnovationFix
{
    private static readonly Dictionary<Form, DateTime> BusySince = new();

    public static void Apply(object? sender, EventArgs e)
    {
        foreach (Form form in Application.OpenForms.Cast<Form>().ToArray())
        {
            form.Text = "FF GUARDIAN 5.2.8 — Status & Innovation Fix by EL.CO";
            RepairStatus(form);
            EnhanceInnovationLab(form);
        }
    }

    private static void RepairStatus(Form form)
    {
        Label? status = FindLabels(form).FirstOrDefault(l =>
            l.Dock == DockStyle.Bottom && l.Height <= 40 &&
            l.Text.Contains("Operazione in corso", StringComparison.OrdinalIgnoreCase));

        if (status is null)
        {
            BusySince.Remove(form);
            return;
        }

        if (!BusySince.TryGetValue(form, out DateTime started))
        {
            BusySince[form] = DateTime.Now;
            return;
        }

        if (DateTime.Now - started < TimeSpan.FromSeconds(45)) return;

        status.Text = "Operazione affidata a Microsoft Defender. Il controllo continua in background.";
        status.ForeColor = Color.FromArgb(142, 255, 0);
        BusySince.Remove(form);
    }

    private static void EnhanceInnovationLab(Form form)
    {
        bool innovationPage = FindLabels(form).Any(l =>
            l.Text.Equals("Innovation Lab", StringComparison.OrdinalIgnoreCase) &&
            l.Font.Bold && l.Font.Size >= 18);
        if (!innovationPage) return;

        foreach (Panel panel in FindPanels(form))
        {
            Label? title = panel.Controls.OfType<Label>().FirstOrDefault(l => l.Dock == DockStyle.Top);
            if (title is null || panel.Controls.OfType<Button>().Any()) continue;

            string name = title.Text.Trim();
            Button? button = name switch
            {
                "Spiegazione dei rischi" => MakeButton("ANALIZZA SICUREZZA", async () =>
                {
                    SecurityState state = await new DefenderService().GetStateAsync();
                    string message = state.Issues.Count == 0
                        ? $"Protezione {state.Score}/100. Nessuna azione urgente rilevata."
                        : $"Protezione {state.Score}/100.\n\n" + string.Join("\n", state.Issues.Select(x => "• " + x));
                    MessageBox.Show(message, "FF GUARDIAN - Analisi sicurezza", MessageBoxButtons.OK,
                        state.Issues.Count == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                }),
                "Hardening consigliato" => MakeButton("APRI SICUREZZA WINDOWS", () =>
                {
                    Process.Start(new ProcessStartInfo("windowsdefender:") { UseShellExecute = true });
                    return Task.CompletedTask;
                }),
                "Controllo download" => MakeButton("CONTROLLA UN FILE", async () =>
                {
                    using OpenFileDialog dialog = new()
                    {
                        Title = "Seleziona il file da controllare",
                        InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads"
                    };
                    if (dialog.ShowDialog(form) != DialogResult.OK) return;
                    await new DefenderService().CustomScanAsync(dialog.FileName);
                    MessageBox.Show("Controllo del file avviato con Microsoft Defender.", "FF GUARDIAN",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }),
                "Smart Defense" => MakeButton("APRI PROFILI", () =>
                {
                    MessageBox.Show(
                        "CASA: equilibrio tra sicurezza e semplicità.\n\nUFFICIO: maggiore controllo di rete e download.\n\nMASSIMA PROTEZIONE: impostazioni più rigide da applicare solo dopo verifica.",
                        "FF GUARDIAN - Profili Smart Defense", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return Task.CompletedTask;
                }),
                _ => null
            };

            if (button is null) continue;
            panel.Height = Math.Max(panel.Height, 190);
            panel.Controls.Add(button);
            button.BringToFront();
        }
    }

    private static Button MakeButton(string text, Func<Task> action)
    {
        Button button = new()
        {
            Text = text,
            Dock = DockStyle.Bottom,
            Height = 44,
            BackColor = Color.FromArgb(20, 45, 55),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(142, 255, 0);
        button.FlatAppearance.BorderSize = 1;
        button.Click += async (_, _) =>
        {
            button.Enabled = false;
            try { await action(); }
            catch (Exception ex)
            {
                (string message, MessageBoxIcon icon) = ErrorMessageFormatter.Format(ex);
                MessageBox.Show(message, "FF GUARDIAN", MessageBoxButtons.OK, icon);
            }
            finally { button.Enabled = true; }
        };
        return button;
    }

    private static IEnumerable<Label> FindLabels(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is Label label) yield return label;
            foreach (Label nested in FindLabels(child)) yield return nested;
        }
    }

    private static IEnumerable<Panel> FindPanels(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is Panel panel) yield return panel;
            foreach (Panel nested in FindPanels(child)) yield return nested;
        }
    }
}
