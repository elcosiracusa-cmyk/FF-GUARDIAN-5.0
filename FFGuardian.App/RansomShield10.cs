using System.Collections.Concurrent;
using System.Text.Json;

namespace FFGuardian;

internal sealed class RansomShieldSettings10
{
    public bool Enabled { get; set; } = true;
    public bool ProtectPersonalFolders { get; set; } = true;
    public bool ShowAlerts { get; set; } = true;
    public int ChangeThreshold { get; set; } = 35;
    public int WindowSeconds { get; set; } = 15;
    public List<string> CustomFolders { get; set; } = [];

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FF Guardian", "Engine10", "ransom-shield.json");

    public static RansomShieldSettings10 Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new RansomShieldSettings10();
            RansomShieldSettings10? value = JsonSerializer.Deserialize<RansomShieldSettings10>(File.ReadAllText(SettingsPath));
            if (value is null) return new RansomShieldSettings10();
            value.ChangeThreshold = Math.Clamp(value.ChangeThreshold, 10, 500);
            value.WindowSeconds = Math.Clamp(value.WindowSeconds, 5, 120);
            value.CustomFolders ??= [];
            return value;
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            return new RansomShieldSettings10();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this,
            new JsonSerializerOptions { WriteIndented = true }));
    }

    public IEnumerable<string> GetProtectedFolders()
    {
        List<string> folders = [];
        if (ProtectPersonalFolders)
        {
            folders.Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            folders.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            folders.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
            folders.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
        }
        folders.AddRange(CustomFolders);
        return folders.Where(Directory.Exists).Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}

internal sealed record RansomShieldAlert10(DateTime CreatedUtc, string Folder, int Changes,
    int Renames, int Deletes, string Status);

internal sealed class RansomShieldMonitor10 : IDisposable
{
    private readonly RansomShieldSettings10 _settings;
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly ConcurrentQueue<(DateTime Time, string Kind, string Path)> _events = new();
    private readonly System.Threading.Timer _timer;
    private bool _started;
    private bool _alertOpen;

    public event EventHandler<RansomShieldAlert10>? Alert;
    public int ProtectedFolderCount => _watchers.Count;
    public bool IsRunning => _started;

    public RansomShieldMonitor10(RansomShieldSettings10 settings)
    {
        _settings = settings;
        _timer = new System.Threading.Timer(Evaluate, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start()
    {
        Stop();
        if (!_settings.Enabled) return;

        foreach (string folder in _settings.GetProtectedFolders())
        {
            try
            {
                FileSystemWatcher watcher = new(folder)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                watcher.Changed += (_, e) => Record("Changed", e.FullPath);
                watcher.Created += (_, e) => Record("Created", e.FullPath);
                watcher.Deleted += (_, e) => Record("Deleted", e.FullPath);
                watcher.Renamed += (_, e) => Record("Renamed", e.FullPath);
                watcher.Error += (_, e) => StabilityCoordinator82.WriteStabilityLog(
                    e.GetException() ?? new IOException("Errore monitor Ransom Shield."));
                _watchers.Add(watcher);
            }
            catch (Exception ex)
            {
                StabilityCoordinator82.WriteStabilityLog(ex);
            }
        }

        _started = _watchers.Count > 0;
        _timer.Change(1000, 1000);
    }

    public void Restart() => Start();

    private void Record(string kind, string path)
    {
        if (Directory.Exists(path)) return;
        _events.Enqueue((DateTime.UtcNow, kind, path));
    }

    private void Evaluate(object? state)
    {
        DateTime cutoff = DateTime.UtcNow.AddSeconds(-_settings.WindowSeconds);
        while (_events.TryPeek(out var old) && old.Time < cutoff)
            _events.TryDequeue(out _);

        var snapshot = _events.ToArray().Where(e => e.Time >= cutoff).ToArray();
        if (snapshot.Length < _settings.ChangeThreshold)
        {
            _alertOpen = false;
            return;
        }
        if (_alertOpen) return;
        _alertOpen = true;

        int renames = snapshot.Count(e => e.Kind == "Renamed");
        int deletes = snapshot.Count(e => e.Kind == "Deleted");
        string folder = snapshot.GroupBy(e => Path.GetDirectoryName(e.Path) ?? string.Empty,
                StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count()).FirstOrDefault()?.Key ?? "Cartella protetta";
        string status = renames + deletes >= Math.Max(5, _settings.ChangeThreshold / 3)
            ? "Comportamento compatibile con modifica o cifratura massiva"
            : "Attività file anomala rilevata";

        RansomShieldAlert10 alert = new(DateTime.UtcNow, folder, snapshot.Length, renames, deletes, status);
        WriteEvent(alert);
        Alert?.Invoke(this, alert);
    }

    private static void WriteEvent(RansomShieldAlert10 alert)
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FF Guardian", "Engine10", "RansomShield");
        Directory.CreateDirectory(folder);
        string line = JsonSerializer.Serialize(alert) + Environment.NewLine;
        File.AppendAllText(Path.Combine(folder, "events.jsonl"), line);
        StabilityCoordinator82.WriteInformationLog($"RANSOM SHIELD: {alert.Status} — {alert.Changes} eventi — {alert.Folder}");
    }

    public void Stop()
    {
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        foreach (FileSystemWatcher watcher in _watchers) watcher.Dispose();
        _watchers.Clear();
        while (_events.TryDequeue(out _)) { }
        _started = false;
        _alertOpen = false;
    }

    public void Dispose()
    {
        Stop();
        _timer.Dispose();
    }
}

internal static class RansomShieldCenter10
{
    private static readonly Color Background = Color.FromArgb(3, 8, 12);
    private static readonly Color Surface = Color.FromArgb(17, 31, 39);
    private static readonly Color Neon = Color.FromArgb(160, 255, 0);

    public static void Attach(IndependentMainForm100 form, RansomShieldSettings10 settings, Action changed)
    {
        TabControl? tabs = FindControl<TabControl>(form);
        if (tabs is null || tabs.TabPages.Cast<TabPage>().Any(p => p.Text == "RANSOM SHIELD")) return;

        TabPage page = new("RANSOM SHIELD") { BackColor = Background, ForeColor = Color.White, Padding = new Padding(22) };
        FlowLayoutPanel panel = new() { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            WrapContents = false, AutoScroll = true, BackColor = Background, Padding = new Padding(10) };

        panel.Controls.Add(new Label { Width = 850, Height = 58, Text = "RANSOM SHIELD — PROTEZIONE COMPORTAMENTALE",
            Font = new Font("Segoe UI", 18F, FontStyle.Bold), ForeColor = Color.White });
        panel.Controls.Add(new Label { Width = 850, Height = 70,
            Text = "Sorveglia le cartelle personali e segnala modifiche, rinomine o eliminazioni massive. Questa versione avvisa e registra l’evento; non termina automaticamente processi.",
            ForeColor = Color.Gainsboro });

        CheckBox enabled = Toggle("Attiva Ransom Shield", settings.Enabled);
        CheckBox personal = Toggle("Proteggi Desktop, Documenti, Immagini e Video", settings.ProtectPersonalFolders);
        CheckBox alerts = Toggle("Mostra avvisi immediati", settings.ShowAlerts);
        panel.Controls.Add(enabled); panel.Controls.Add(personal); panel.Controls.Add(alerts);

        NumericUpDown threshold = Number(settings.ChangeThreshold, 10, 500);
        NumericUpDown seconds = Number(settings.WindowSeconds, 5, 120);
        panel.Controls.Add(Row("Soglia modifiche prima dell’avviso", threshold));
        panel.Controls.Add(Row("Finestra di rilevamento (secondi)", seconds));

        Button addFolder = Button("AGGIUNGI CARTELLA PROTETTA");
        Button openLog = Button("APRI REGISTRO EVENTI");
        Button save = Button("SALVA E RIAVVIA PROTEZIONE");
        addFolder.Click += (_, _) =>
        {
            using FolderBrowserDialog dialog = new() { Description = "Seleziona una cartella da proteggere" };
            if (dialog.ShowDialog(form) == DialogResult.OK &&
                !settings.CustomFolders.Contains(dialog.SelectedPath, StringComparer.OrdinalIgnoreCase))
                settings.CustomFolders.Add(dialog.SelectedPath);
        };
        openLog.Click += (_, _) =>
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FF Guardian", "Engine10", "RansomShield");
            Directory.CreateDirectory(folder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
        };
        save.Click += (_, _) =>
        {
            settings.Enabled = enabled.Checked;
            settings.ProtectPersonalFolders = personal.Checked;
            settings.ShowAlerts = alerts.Checked;
            settings.ChangeThreshold = decimal.ToInt32(threshold.Value);
            settings.WindowSeconds = decimal.ToInt32(seconds.Value);
            settings.Save();
            changed();
            MessageBox.Show(form, "Ransom Shield aggiornato.", "FF GUARDIAN 10", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        panel.Controls.Add(addFolder); panel.Controls.Add(openLog); panel.Controls.Add(save);
        page.Controls.Add(panel); tabs.TabPages.Add(page);
    }

    private static CheckBox Toggle(string text, bool value) => new() { Width = 820, Height = 52, Text = text,
        Checked = value, BackColor = Surface, ForeColor = Color.White, Padding = new Padding(14, 0, 0, 0),
        FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
    private static NumericUpDown Number(int value, int min, int max) => new() { Minimum = min, Maximum = max,
        Value = Math.Clamp(value, min, max), Width = 110, BackColor = Background, ForeColor = Color.White };
    private static FlowLayoutPanel Row(string text, Control control)
    {
        FlowLayoutPanel row = new() { Width = 820, Height = 56, BackColor = Surface, Padding = new Padding(14, 10, 14, 10) };
        row.Controls.Add(new Label { Width = 560, Height = 30, Text = text, ForeColor = Color.White });
        row.Controls.Add(control); return row;
    }
    private static Button Button(string text)
    {
        Button button = new() { Width = 300, Height = 42, Margin = new Padding(0, 8, 0, 0), Text = text,
            BackColor = Surface, ForeColor = Color.White, FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
        button.FlatAppearance.BorderColor = Neon; return button;
    }
    private static T? FindControl<T>(Control root) where T : Control
    {
        if (root is T match) return match;
        foreach (Control child in root.Controls) { T? result = FindControl<T>(child); if (result is not null) return result; }
        return null;
    }
}
