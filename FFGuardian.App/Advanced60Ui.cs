namespace FFGuardian;

internal static class Advanced60Ui
{
    private const string ProtectButtonName = "FFG60_PROTECT_NOW";
    private const string ProfileButtonName = "FFG60_PROFILE";
    private const string StatusLabelName = "FFG60_ENGINE_STATUS";

    public static void Apply(object? sender, EventArgs e)
    {
        foreach (Form form in Application.OpenForms)
        {
            form.Text = "FF GUARDIAN 6.0 Advanced — Autonomous Security Engine by EL.CO";
            Panel? header = FindPanels(form)
                .FirstOrDefault(panel => panel.Dock == DockStyle.Top && panel.Height is >= 70 and <= 100);
            if (header is null) continue;

            EnsureAdvancedControls(header);
            RefreshStatus(header, AutonomousSecurityEngine.GetSnapshot());
        }
    }

    private static void EnsureAdvancedControls(Panel header)
    {
        if (header.Controls.Find(ProtectButtonName, false).FirstOrDefault() is null)
        {
            Button protect = CreateButton("🛡  PROTEGGI ADESSO", 190, Color.FromArgb(45, 120, 0));
            protect.Name = ProtectButtonName;
            protect.Dock = DockStyle.Right;
            protect.Click += async (_, _) =>
            {
                protect.Enabled = false;
                string oldText = protect.Text;
                protect.Text = "CONTROLLO IN CORSO...";
                try
                {
                    AutonomousSnapshot result = await AutonomousSecurityEngine.ProtectNowAsync();
                    MessageBox.Show(
                        $"Controllo completato.\n\nPunteggio: {result.Score}/100\nStato: {result.Status}\nProfilo: {ProfileName(result.Profile)}",
                        "FF GUARDIAN 6.0 — Proteggi adesso",
                        MessageBoxButtons.OK,
                        result.Score >= 90 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
                }
                finally
                {
                    protect.Text = oldText;
                    protect.Enabled = true;
                }
            };
            header.Controls.Add(protect);
            protect.BringToFront();
        }

        if (header.Controls.Find(ProfileButtonName, false).FirstOrDefault() is null)
        {
            Button profile = CreateButton("PROFILO: CASA", 150, Color.FromArgb(18, 45, 55));
            profile.Name = ProfileButtonName;
            profile.Dock = DockStyle.Right;
            ContextMenuStrip menu = new();
            AddProfileItem(menu, profile, "Casa", ProtectionProfile.Casa);
            AddProfileItem(menu, profile, "Ufficio", ProtectionProfile.Ufficio);
            AddProfileItem(menu, profile, "Massima protezione", ProtectionProfile.MassimaProtezione);
            profile.Click += (_, _) => menu.Show(profile, new Point(0, profile.Height));
            header.Controls.Add(profile);
            profile.BringToFront();
        }

        if (header.Controls.Find(StatusLabelName, false).FirstOrDefault() is null)
        {
            Label status = new()
            {
                Name = StatusLabelName,
                Dock = DockStyle.Right,
                Width = 175,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(142, 255, 0),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = Color.Transparent
            };
            header.Controls.Add(status);
            status.BringToFront();
        }
    }

    private static void AddProfileItem(ContextMenuStrip menu, Button profileButton, string text, ProtectionProfile profile)
    {
        ToolStripMenuItem item = new(text);
        item.Click += (_, _) =>
        {
            AutonomousSecurityEngine.SetProfile(profile);
            profileButton.Text = $"PROFILO: {text.ToUpperInvariant()}";
        };
        menu.Items.Add(item);
    }

    private static void RefreshStatus(Panel header, AutonomousSnapshot snapshot)
    {
        if (header.Controls.Find(StatusLabelName, false).FirstOrDefault() is Label status)
        {
            status.Text = $"ENGINE AUTONOMO\n{snapshot.Score}/100 • {snapshot.Status}";
            status.ForeColor = snapshot.Score >= 90
                ? Color.FromArgb(142, 255, 0)
                : snapshot.Score >= 70 ? Color.Orange : Color.OrangeRed;
        }

        if (header.Controls.Find(ProfileButtonName, false).FirstOrDefault() is Button profile)
            profile.Text = $"PROFILO: {ProfileName(snapshot.Profile).ToUpperInvariant()}";
    }

    private static Button CreateButton(string text, int width, Color backColor)
    {
        Button button = new()
        {
            Text = text,
            Width = width,
            Height = 46,
            Margin = new Padding(5, 18, 5, 18),
            BackColor = backColor,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            UseCompatibleTextRendering = true
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(142, 255, 0);
        button.FlatAppearance.BorderSize = 1;
        return button;
    }

    private static string ProfileName(ProtectionProfile profile) => profile switch
    {
        ProtectionProfile.Casa => "Casa",
        ProtectionProfile.Ufficio => "Ufficio",
        _ => "Massima protezione"
    };

    private static IEnumerable<Panel> FindPanels(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is Panel panel) yield return panel;
            foreach (Panel nested in FindPanels(child)) yield return nested;
        }
    }
}