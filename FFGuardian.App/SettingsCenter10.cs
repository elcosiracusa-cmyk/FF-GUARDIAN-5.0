using System.Text.Json;
using Microsoft.Win32;

namespace FFGuardian;

internal sealed class AppSettings10
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "FFGuardian";

    public bool StartWithWindows { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public bool UpdateSignaturesAtStartup { get; set; } = true;
    public bool EnableAuditReminders { get; set; } = true;
    public int AuditReminderHours { get; set; } = 6;
    public bool ShowSecurityNotifications { get; set; } = true;

    private static string SettingsFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FF Guardian", "Engine10");

    private static string SettingsPath => Path.Combine(SettingsFolder, "settings.json");

    public static AppSettings10 Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new AppSettings10();

            string json = File.ReadAllText(SettingsPath);
            AppSettings10? settings = JsonSerializer.Deserialize<AppSettings10>(json);
            if (settings is null)
                return new AppSettings10();

            settings.AuditReminderHours = Math.Clamp(settings.AuditReminderHours, 1, 24);
            return settings;
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            return new AppSettings10();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsFolder);
        string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
        ApplyStartup(StartWithWindows);
    }

    public static bool IsStartupEnabled()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(RunValueName) is string;
    }

    public static void ApplyStartup(bool enabled)
    {
        using RegistryKey? key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (key is null)
            throw new InvalidOperationException("Impossibile configurare l’avvio automatico.");

        if (enabled)
            key.SetValue(RunValueName, $"\"{Environment.ProcessPath}\"");
        else
            key.DeleteValue(RunValueName, false);
    }
}

internal static class SettingsCenter10
{
    private static readonly Color Background = Color.FromArgb(3, 8, 12);
    private static readonly Color Surface = Color.FromArgb(17, 31, 39);
    private static readonly Color Neon = Color.FromArgb(160, 255, 0);
    private static readonly Color Muted = Color.FromArgb(188, 200, 207);

    public static void Attach(IndependentMainForm100 form, AppSettings10 settings, Action settingsChanged)
    {
        ArgumentNullException.ThrowIfNull(form);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(settingsChanged);

        TabControl? tabs = FindControl<TabControl>(form);
        if (tabs is null || tabs.TabPages.Cast<TabPage>().Any(page => page.Text == "IMPOSTAZIONI"))
            return;

        TabPage page = new("IMPOSTAZIONI")
        {
            BackColor = Background,
            ForeColor = Color.White,
            Padding = new Padding(22)
        };

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Background
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));

        layout.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "IMPOSTAZIONI FF GUARDIAN",
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        FlowLayoutPanel options = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Background,
            Padding = new Padding(8)
        };

        CheckBox startup = CreateToggle("Avvia FFGuardian con Windows", settings.StartWithWindows);
        CheckBox tray = CreateToggle("Riduci nell’area di notifica quando chiudo la finestra", settings.MinimizeToTray);
        CheckBox signatures = CreateToggle("Aggiorna le firme automaticamente all’avvio", settings.UpdateSignaturesAtStartup);
        CheckBox reminders = CreateToggle("Mostra promemoria periodici di sicurezza", settings.EnableAuditReminders);
        CheckBox notifications = CreateToggle("Mostra notifiche per file sospetti e minacce", settings.ShowSecurityNotifications);

        FlowLayoutPanel intervalRow = new()
        {
            Width = 760,
            Height = 56,
            BackColor = Surface,
            Padding = new Padding(14, 10, 14, 10),
            Margin = new Padding(0, 8, 0, 8)
        };
        intervalRow.Controls.Add(new Label
        {
            Width = 430,
            Height = 32,
            Text = "Intervallo promemoria audit (ore)",
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft
        });
        NumericUpDown interval = new()
        {
            Minimum = 1,
            Maximum = 24,
            Value = settings.AuditReminderHours,
            Width = 90,
            BackColor = Background,
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        intervalRow.Controls.Add(interval);

        options.Controls.Add(startup);
        options.Controls.Add(tray);
        options.Controls.Add(signatures);
        options.Controls.Add(reminders);
        options.Controls.Add(intervalRow);
        options.Controls.Add(notifications);
        options.Controls.Add(new Label
        {
            Width = 760,
            Height = 72,
            Margin = new Padding(0, 12, 0, 0),
            Text = "Le impostazioni vengono salvate nel profilo locale dell’utente. Le modifiche all’avvio con Windows vengono applicate immediatamente.",
            ForeColor = Muted,
            TextAlign = ContentAlignment.MiddleLeft
        });

        FlowLayoutPanel commands = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Background,
            Padding = new Padding(8)
        };
        Button save = CreateButton("SALVA IMPOSTAZIONI");
        Button reset = CreateButton("RIPRISTINA PREDEFINITE");
        commands.Controls.Add(save);
        commands.Controls.Add(reset);

        void LoadIntoControls(AppSettings10 value)
        {
            startup.Checked = value.StartWithWindows;
            tray.Checked = value.MinimizeToTray;
            signatures.Checked = value.UpdateSignaturesAtStartup;
            reminders.Checked = value.EnableAuditReminders;
            interval.Value = Math.Clamp(value.AuditReminderHours, 1, 24);
            notifications.Checked = value.ShowSecurityNotifications;
        }

        save.Click += (_, _) =>
        {
            settings.StartWithWindows = startup.Checked;
            settings.MinimizeToTray = tray.Checked;
            settings.UpdateSignaturesAtStartup = signatures.Checked;
            settings.EnableAuditReminders = reminders.Checked;
            settings.AuditReminderHours = decimal.ToInt32(interval.Value);
            settings.ShowSecurityNotifications = notifications.Checked;
            settings.Save();
            settingsChanged();
            MessageBox.Show(form, "Impostazioni salvate e applicate.", "FF GUARDIAN 10",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        reset.Click += (_, _) =>
        {
            AppSettings10 defaults = new();
            settings.StartWithWindows = defaults.StartWithWindows;
            settings.MinimizeToTray = defaults.MinimizeToTray;
            settings.UpdateSignaturesAtStartup = defaults.UpdateSignaturesAtStartup;
            settings.EnableAuditReminders = defaults.EnableAuditReminders;
            settings.AuditReminderHours = defaults.AuditReminderHours;
            settings.ShowSecurityNotifications = defaults.ShowSecurityNotifications;
            LoadIntoControls(settings);
        };

        layout.Controls.Add(options, 0, 1);
        layout.Controls.Add(commands, 0, 2);
        page.Controls.Add(layout);
        tabs.TabPages.Add(page);
    }

    private static CheckBox CreateToggle(string text, bool value) => new()
    {
        Width = 760,
        Height = 56,
        Margin = new Padding(0, 8, 0, 8),
        Padding = new Padding(14, 0, 14, 0),
        Text = text,
        Checked = value,
        BackColor = Surface,
        ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat,
        Font = new Font("Segoe UI", 10F, FontStyle.Bold),
        Cursor = Cursors.Hand
    };

    private static Button CreateButton(string text)
    {
        Button button = new()
        {
            Width = 230,
            Height = 42,
            Margin = new Padding(6),
            Text = text,
            BackColor = Surface,
            ForeColor = Color.White,
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
