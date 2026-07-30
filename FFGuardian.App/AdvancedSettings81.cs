using System.Diagnostics;
using System.Text.Json;
using Microsoft.Win32;

namespace FFGuardian;

internal sealed class AdvancedSettings81State
{
    public bool StartWithWindows { get; set; }
    public bool SilentMode { get; set; }
    public bool NotificationsEnabled { get; set; } = true;
    public bool DownloadMonitoring { get; set; } = true;
    public bool AutomaticSignatureUpdates { get; set; } = true;
    public bool AutomaticUpdateChecks { get; set; } = true;
    public bool BatteryFriendlyScans { get; set; }
    public int LogRetentionDays { get; set; } = 30;
}

internal static class AdvancedSettings81
{
    private const string ButtonName = "FFG81_ADVANCED_SETTINGS";
    private static readonly HashSet<Form> ConfiguredForms = new();
    private static readonly Color Bg = Color.FromArgb(5, 10, 13);
    private static readonly Color Surface = Color.FromArgb(11, 20, 24);
    private static readonly Color Surface2 = Color.FromArgb(20, 38, 43);
    private static readonly Color Neon = Color.FromArgb(142, 255, 0);
    private static readonly Color TextSecondary = Color.FromArgb(205, 215, 220);
    private static readonly string DataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "FF Guardian");
    private static readonly string SettingsPath = Path.Combine(DataFolder, "advanced-settings-v81.json");
    private static readonly string BackupPath = SettingsPath + ".bak";
    private static readonly string TempPath = SettingsPath + ".tmp";

    public static void Apply(object? sender, EventArgs e)
    {
        foreach (Form form in Application.OpenForms)
        {
            if (!form.Text.Contains("FF GUARDIAN", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!ConfiguredForms.Add(form))
                continue;

            AddButton(form);
            form.FormClosed += (_, _) => ConfiguredForms.Remove(form);
        }
    }

    private static void AddButton(Form owner)
    {
        FlowLayoutPanel? menu = FindControls<FlowLayoutPanel>(owner)
            .FirstOrDefault(flow => flow.Controls.OfType<Button>()
                .Any(button => button.Text.Contains("Dashboard", StringComparison.OrdinalIgnoreCase)));
        if (menu is null || menu.Controls.Find(ButtonName, false).Length > 0)
            return;

        Button button = new()
        {
            Name = ButtonName,
            Text = "⚙   Impostazioni 8.2.1",
            Width = Math.Max(235, menu.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 8),
            Height = 39,
            Margin = new Padding(0, 1, 0, 1),
            Padding = new Padding(12, 0, 0, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Surface2,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.4F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = Neon;
        button.FlatAppearance.BorderSize = 1;
        button.Click += (_, _) => ShowSettings(owner);

        menu.Controls.Add(button);
        int cloudIndex = menu.Controls.OfType<Button>().ToList()
            .FindIndex(b => b.Text.Contains("Cloud Ready", StringComparison.OrdinalIgnoreCase));
        if (cloudIndex >= 0)
            menu.Controls.SetChildIndex(button, Math.Min(cloudIndex + 1, menu.Controls.Count - 1));
    }

    private static void ShowSettings(Form owner)
    {
        AdvancedSettings81State state = Load();
        using Form dialog = new()
        {
            Text = "FF GUARDIAN 8.2.1 — Hardening & Reliability",
            Icon = owner.Icon,
            StartPosition = FormStartPosition.CenterParent,
            MinimumSize = new Size(880, 680),
            Size = new Size(980, 760),
            BackColor = Bg,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F)
        };

        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(18)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));

        FlowLayoutPanel left = SettingsColumn();
        FlowLayoutPanel right = SettingsColumn();

        CheckBox startup = SettingCheck("Avvia FF GUARDIAN con Windows", state.StartWithWindows);
        CheckBox silent = SettingCheck("Modalità silenziosa", state.SilentMode);
        CheckBox notifications = SettingCheck("Notifiche attive", state.NotificationsEnabled);
        CheckBox downloads = SettingCheck("Controllo automatico Download", state.DownloadMonitoring);
        CheckBox signatures = SettingCheck("Aggiornamento firme automatico", state.AutomaticSignatureUpdates);
        CheckBox updateChecks = SettingCheck("Controllo aggiornamenti FF GUARDIAN", state.AutomaticUpdateChecks);
        CheckBox battery = SettingCheck("Scansioni pesanti solo con alimentatore", state.BatteryFriendlyScans);

        left.Controls.Add(SectionTitle("AVVIO E COMPORTAMENTO"));
        left.Controls.Add(startup);
        left.Controls.Add(silent);
        left.Controls.Add(notifications);
        left.Controls.Add(SectionTitle("PROTEZIONE AUTOMATICA"));
        left.Controls.Add(downloads);
        left.Controls.Add(signatures);
        left.Controls.Add(updateChecks);
        left.Controls.Add(battery);

        right.Controls.Add(SectionTitle("PROFILO DI PROTEZIONE"));
        ComboBox profile = new()
        {
            Width = 390,
            Height = 42,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Surface,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10.5F)
        };
        profile.Items.AddRange(["Casa", "Ufficio", "Massima protezione"]);
        ProtectionProfile current = AutonomousSecurityEngine.GetSnapshot().Profile;
        profile.SelectedIndex = current == ProtectionProfile.Ufficio ? 1 : current == ProtectionProfile.MassimaProtezione ? 2 : 0;
        right.Controls.Add(profile);

        right.Controls.Add(SectionTitle("GESTIONE REGISTRI"));
        ComboBox retention = new()
        {
            Width = 390,
            Height = 42,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Surface,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10.5F)
        };
        retention.Items.AddRange(["7 giorni", "30 giorni", "90 giorni"]);
        retention.SelectedIndex = state.LogRetentionDays == 7 ? 0 : state.LogRetentionDays == 90 ? 2 : 1;
        right.Controls.Add(retention);

        Button openLogs = ActionButton("APRI CARTELLA LOG");
        openLogs.Click += (_, _) => OpenLogs();
        Button clearLogs = ActionButton("CANCELLA LOG LOCALI");
        clearLogs.Click += (_, _) => ClearLogs(dialog);
        Button reset = ActionButton("RIPRISTINA PREDEFINITE");
        reset.Click += (_, _) =>
        {
            state = new AdvancedSettings81State();
            Save(state);
            MessageBox.Show("Impostazioni predefinite ripristinate. Riapri questa finestra per visualizzarle.", "FF GUARDIAN", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        right.Controls.Add(openLogs);
        right.Controls.Add(clearLogs);
        right.Controls.Add(reset);

        Label status = new()
        {
            Dock = DockStyle.Fill,
            ForeColor = Neon,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0)
        };
        Button save = ActionButton("SALVA IMPOSTAZIONI");
        save.Dock = DockStyle.Right;
        save.Width = 260;
        save.Click += (_, _) =>
        {
            try
            {
                state.StartWithWindows = startup.Checked;
                state.SilentMode = silent.Checked;
                state.NotificationsEnabled = notifications.Checked;
                state.DownloadMonitoring = downloads.Checked;
                state.AutomaticSignatureUpdates = signatures.Checked;
                state.AutomaticUpdateChecks = updateChecks.Checked;
                state.BatteryFriendlyScans = battery.Checked;
                state.LogRetentionDays = retention.SelectedIndex == 0 ? 7 : retention.SelectedIndex == 2 ? 90 : 30;
                ApplyStartup(state.StartWithWindows);
                ProtectionProfile selected = profile.SelectedIndex == 1 ? ProtectionProfile.Ufficio : profile.SelectedIndex == 2 ? ProtectionProfile.MassimaProtezione : ProtectionProfile.Casa;
                AutonomousSecurityEngine.SetProfile(selected);
                Save(state);
                status.Text = "✓ Impostazioni salvate e protette da backup";
            }
            catch (Exception ex)
            {
                StabilityCoordinator82.WriteStabilityLog(ex);
                status.Text = "Salvataggio non completato. Le impostazioni precedenti sono state mantenute.";
            }
        };

        Panel footer = new() { Dock = DockStyle.Fill, BackColor = Surface };
        footer.Controls.Add(status);
        footer.Controls.Add(save);
        root.Controls.Add(left, 0, 0);
        root.Controls.Add(right, 1, 0);
        root.SetColumnSpan(footer, 2);
        root.Controls.Add(footer, 0, 1);
        dialog.Controls.Add(root);
        dialog.ShowDialog(owner);
    }

    private static FlowLayoutPanel SettingsColumn() => new()
    {
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoScroll = true,
        Padding = new Padding(14),
        BackColor = Surface,
        Margin = new Padding(8)
    };

    private static Label SectionTitle(string text) => new()
    {
        Text = text,
        Width = 400,
        Height = 48,
        ForeColor = Color.White,
        Font = new Font("Segoe UI", 12F, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleLeft,
        Margin = new Padding(4, 12, 4, 4)
    };

    private static CheckBox SettingCheck(string text, bool value) => new()
    {
        Text = text,
        Checked = value,
        Width = 400,
        Height = 42,
        ForeColor = TextSecondary,
        Font = new Font("Segoe UI", 10F),
        FlatStyle = FlatStyle.Flat,
        Margin = new Padding(4)
    };

    private static Button ActionButton(string text)
    {
        Button button = new()
        {
            Text = text,
            Width = 390,
            Height = 46,
            BackColor = Surface2,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(4, 8, 4, 4)
        };
        button.FlatAppearance.BorderColor = Neon;
        return button;
    }

    private static AdvancedSettings81State Load()
    {
        AdvancedSettings81State? state = TryLoad(SettingsPath);
        if (state is not null)
            return state;

        state = TryLoad(BackupPath);
        if (state is not null)
        {
            try { Save(state); } catch { }
            return state;
        }

        return new AdvancedSettings81State { StartWithWindows = IsStartupEnabled() };
    }

    private static AdvancedSettings81State? TryLoad(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<AdvancedSettings81State>(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            return null;
        }
    }

    private static void Save(AdvancedSettings81State state)
    {
        Directory.CreateDirectory(DataFolder);
        string json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(TempPath, json);

        if (File.Exists(SettingsPath))
            File.Replace(TempPath, SettingsPath, BackupPath, true);
        else
        {
            File.Move(TempPath, SettingsPath, true);
            File.Copy(SettingsPath, BackupPath, true);
        }
    }

    private static void ApplyStartup(bool enabled)
    {
        using RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled)
            key.SetValue("FFGuardian", $"\"{Environment.ProcessPath}\"");
        else
            key.DeleteValue("FFGuardian", false);
    }

    private static bool IsStartupEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        return key?.GetValue("FFGuardian") is not null;
    }

    private static void OpenLogs()
    {
        string folder = Path.Combine(DataFolder, "Logs");
        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
    }

    private static void ClearLogs(IWin32Window owner)
    {
        if (MessageBox.Show(owner, "Cancellare tutti i registri diagnostici locali?", "FF GUARDIAN", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;
        string folder = Path.Combine(DataFolder, "Logs");
        int deleted = 0;
        int skipped = 0;
        if (Directory.Exists(folder))
        {
            foreach (string file in Directory.GetFiles(folder))
            {
                try { File.Delete(file); deleted++; }
                catch { skipped++; }
            }
        }
        MessageBox.Show(owner, $"Registri eliminati: {deleted}. File in uso mantenuti: {skipped}.", "FF GUARDIAN", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static IEnumerable<T> FindControls<T>(Control root) where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T match) yield return match;
            foreach (T nested in FindControls<T>(child)) yield return nested;
        }
    }
}
