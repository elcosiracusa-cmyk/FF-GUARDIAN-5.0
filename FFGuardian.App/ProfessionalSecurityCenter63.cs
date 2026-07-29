using System.Diagnostics;

namespace FFGuardian;

internal static class ProfessionalSecurityCenter63
{
    private const string ButtonName = "FFG63_SECURITY_CENTER";
    private static readonly HashSet<Form> ConfiguredForms = new();
    private static readonly Color Bg = Color.FromArgb(5, 10, 13);
    private static readonly Color Surface = Color.FromArgb(11, 20, 24);
    private static readonly Color Surface2 = Color.FromArgb(20, 38, 43);
    private static readonly Color Neon = Color.FromArgb(142, 255, 0);
    private static readonly Color TextSecondary = Color.FromArgb(205, 215, 220);

    public static void Apply(object? sender, EventArgs e)
    {
        foreach (Form form in Application.OpenForms)
        {
            if (!form.Text.Contains("FF GUARDIAN", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!ConfiguredForms.Add(form))
                continue;

            AddSecurityCenterButton(form);
            form.FormClosed += (_, _) => ConfiguredForms.Remove(form);
        }
    }

    private static void AddSecurityCenterButton(Form owner)
    {
        FlowLayoutPanel? menu = FindControls<FlowLayoutPanel>(owner)
            .FirstOrDefault(flow => flow.Controls.OfType<Button>()
                .Any(button => button.Text.Contains("Dashboard", StringComparison.OrdinalIgnoreCase)));

        if (menu is null || menu.Controls.Find(ButtonName, false).Length > 0)
            return;

        Button button = new()
        {
            Name = ButtonName,
            Text = "◈   Centro sicurezza",
            Width = Math.Max(248, menu.ClientSize.Width - 12),
            Height = 47,
            Margin = new Padding(0, 3, 0, 3),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(14, 0, 0, 0),
            BackColor = Surface2,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = Neon;
        button.FlatAppearance.BorderSize = 1;
        button.MouseEnter += (_, _) => button.BackColor = Color.FromArgb(35, 68, 25);
        button.MouseLeave += (_, _) => button.BackColor = Surface2;
        button.Click += (_, _) => ShowCenter(owner);

        int insertIndex = Math.Max(0, menu.Controls.Count - 3);
        menu.Controls.Add(button);
        menu.Controls.SetChildIndex(button, insertIndex);
    }

    private static void ShowCenter(Form owner)
    {
        using Form center = new()
        {
            Text = "FF GUARDIAN 6.3 — Professional Security Center",
            Icon = owner.Icon,
            StartPosition = FormStartPosition.CenterParent,
            MinimumSize = new Size(1040, 700),
            Size = new Size(1180, 780),
            BackColor = Bg,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F)
        };

        TabControl tabs = new()
        {
            Dock = DockStyle.Fill,
            Appearance = TabAppearance.FlatButtons,
            ItemSize = new Size(180, 42),
            SizeMode = TabSizeMode.Fixed,
            Padding = new Point(16, 8)
        };

        tabs.TabPages.Add(BuildOverviewPage());
        tabs.TabPages.Add(BuildNotificationsPage());
        tabs.TabPages.Add(BuildSettingsPage());
        tabs.TabPages.Add(BuildDiagnosticsPage());
        center.Controls.Add(tabs);
        center.ShowDialog(owner);
    }

    private static TabPage BuildOverviewPage()
    {
        TabPage page = NewPage("Panoramica");
        AutonomousSnapshot snapshot = AutonomousSecurityEngine.GetSnapshot();

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(18)
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

        layout.Controls.Add(InfoCard("PUNTEGGIO SICUREZZA", $"{snapshot.Score}/100\n{snapshot.Status}\n\nProfilo: {ProfileName(snapshot.Profile)}", snapshot.Score >= 90 ? Neon : Color.Orange), 0, 0);
        layout.Controls.Add(InfoCard("ULTIMO CONTROLLO", FormatDate(snapshot.LastProtectionCheck) + $"\n\nFile Download controllati: {snapshot.DownloadFilesChecked}"), 1, 0);
        layout.Controls.Add(InfoCard("SCANSIONI", $"Rapida: {FormatDate(snapshot.LastQuickScan)}\n\nCompleta: {FormatDate(snapshot.LastFullScan)}"), 0, 1);
        layout.Controls.Add(InfoCard("STATO MOTORE", string.IsNullOrWhiteSpace(snapshot.LastError) ? "Motore autonomo operativo.\nNessun errore recente." : $"Ultimo errore:\n{snapshot.LastError}", string.IsNullOrWhiteSpace(snapshot.LastError) ? Neon : Color.Orange), 1, 1);

        page.Controls.Add(layout);
        return page;
    }

    private static TabPage BuildNotificationsPage()
    {
        TabPage page = NewPage("Notifiche");
        string logPath = GetLogPath();

        TextBox eventsBox = new()
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Surface,
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 10F),
            Text = ReadRecentLog(logPath)
        };

        Button refresh = ActionButton("AGGIORNA EVENTI");
        refresh.Dock = DockStyle.Bottom;
        refresh.Click += (_, _) => eventsBox.Text = ReadRecentLog(logPath);

        Button open = ActionButton("APRI CARTELLA LOG");
        open.Dock = DockStyle.Bottom;
        open.Click += (_, _) => OpenFolder(Path.GetDirectoryName(logPath)!);

        page.Controls.Add(eventsBox);
        page.Controls.Add(open);
        page.Controls.Add(refresh);
        return page;
    }

    private static TabPage BuildSettingsPage()
    {
        TabPage page = NewPage("Impostazioni");
        AutonomousSnapshot snapshot = AutonomousSecurityEngine.GetSnapshot();

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Top,
            Height = 320,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(24)
        };

        Label title = new()
        {
            Dock = DockStyle.Fill,
            Text = "PROFILO DI PROTEZIONE",
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft
        };

        ComboBox profile = new()
        {
            Dock = DockStyle.Top,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Height = 42,
            Font = new Font("Segoe UI", 11F),
            BackColor = Surface,
            ForeColor = Color.White
        };
        profile.Items.AddRange(["Casa", "Ufficio", "Massima protezione"]);
        profile.SelectedIndex = snapshot.Profile switch
        {
            ProtectionProfile.Ufficio => 1,
            ProtectionProfile.MassimaProtezione => 2,
            _ => 0
        };

        Label explanation = new()
        {
            Dock = DockStyle.Fill,
            ForeColor = TextSecondary,
            Text = "Casa: scansione rapida ogni 7 giorni.\nUfficio: ogni 5 giorni.\nMassima protezione: ogni 3 giorni.\n\nL'aggiornamento firme resta giornaliero e la scansione completa mensile.",
            Font = new Font("Segoe UI", 11F)
        };

        Label saved = new()
        {
            Dock = DockStyle.Fill,
            ForeColor = Neon,
            TextAlign = ContentAlignment.MiddleLeft
        };

        Button apply = ActionButton("APPLICA PROFILO");
        apply.Click += (_, _) =>
        {
            ProtectionProfile selected = profile.SelectedIndex switch
            {
                1 => ProtectionProfile.Ufficio,
                2 => ProtectionProfile.MassimaProtezione,
                _ => ProtectionProfile.Casa
            };
            AutonomousSecurityEngine.SetProfile(selected);
            saved.Text = $"Profilo applicato: {ProfileName(selected)}";
        };

        layout.Controls.Add(title);
        layout.Controls.Add(profile);
        layout.Controls.Add(explanation);
        layout.Controls.Add(apply);
        layout.Controls.Add(saved);
        page.Controls.Add(layout);
        return page;
    }

    private static TabPage BuildDiagnosticsPage()
    {
        TabPage page = NewPage("Diagnostica");
        AutonomousSnapshot snapshot = AutonomousSecurityEngine.GetSnapshot();
        string logPath = GetLogPath();

        Label details = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            BackColor = Surface,
            ForeColor = TextSecondary,
            Font = new Font("Segoe UI", 11F),
            Text = $"Versione FF GUARDIAN: 6.3\nWindows: {Environment.OSVersion}\nComputer: {Environment.MachineName}\nUtente: {Environment.UserName}\n\nMotore autonomo: ATTIVO\nProfilo: {ProfileName(snapshot.Profile)}\nUltimo controllo: {FormatDate(snapshot.LastProtectionCheck)}\nUltimo aggiornamento firme: {FormatDate(snapshot.LastSignatureUpdate)}\nUltimo errore: {snapshot.LastError ?? "Nessuno"}\n\nLog: {logPath}"
        };

        Button openLogs = ActionButton("APRI CARTELLA DIAGNOSTICA");
        openLogs.Dock = DockStyle.Bottom;
        openLogs.Click += (_, _) => OpenFolder(Path.GetDirectoryName(logPath)!);

        page.Controls.Add(details);
        page.Controls.Add(openLogs);
        return page;
    }

    private static TabPage NewPage(string title) => new(title)
    {
        BackColor = Bg,
        ForeColor = Color.White,
        Padding = new Padding(10)
    };

    private static Panel InfoCard(string title, string text, Color? accent = null)
    {
        Panel card = new()
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(10),
            Padding = new Padding(20),
            BackColor = Surface
        };
        Label heading = new()
        {
            Dock = DockStyle.Top,
            Height = 44,
            Text = title,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold)
        };
        Label body = new()
        {
            Dock = DockStyle.Fill,
            Text = text,
            ForeColor = accent ?? TextSecondary,
            Font = new Font("Segoe UI", 12F),
            TextAlign = ContentAlignment.MiddleLeft
        };
        card.Controls.Add(body);
        card.Controls.Add(heading);
        return card;
    }

    private static Button ActionButton(string text)
    {
        Button button = new()
        {
            Text = text,
            Height = 50,
            BackColor = Surface2,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = Neon;
        button.FlatAppearance.BorderSize = 1;
        return button;
    }

    private static string GetLogPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "FF Guardian", "Logs", "autonomous-engine-v6.log");

    private static string ReadRecentLog(string path)
    {
        try
        {
            if (!File.Exists(path)) return "Nessun evento disponibile.";
            string[] lines = File.ReadAllLines(path);
            return string.Join(Environment.NewLine, lines.TakeLast(120).Reverse());
        }
        catch (Exception ex)
        {
            return "Impossibile leggere gli eventi: " + ex.Message;
        }
    }

    private static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
    }

    private static string FormatDate(DateTime? value) => value?.ToString("dd/MM/yyyy HH:mm") ?? "Non disponibile";

    private static string ProfileName(ProtectionProfile profile) => profile switch
    {
        ProtectionProfile.Ufficio => "Ufficio",
        ProtectionProfile.MassimaProtezione => "Massima protezione",
        _ => "Casa"
    };

    private static IEnumerable<T> FindControls<T>(Control parent) where T : Control
    {
        foreach (Control child in parent.Controls)
        {
            if (child is T match) yield return match;
            foreach (T nested in FindControls<T>(child)) yield return nested;
        }
    }
}
