using System.Text.Json;
using FFGuardian.Engine10;

namespace FFGuardian;

internal enum UsbShieldMode10
{
    Ask,
    AutomaticScan,
    Ignore
}

internal sealed class UsbShieldSettings10
{
    public bool Enabled { get; set; } = true;
    public UsbShieldMode10 Mode { get; set; } = UsbShieldMode10.Ask;
    public bool CheckAutorun { get; set; } = true;
    public bool ShowNotifications { get; set; } = true;

    private static string PathName => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FF Guardian", "Engine10", "usb-shield.json");

    public static UsbShieldSettings10 Load()
    {
        try
        {
            return File.Exists(PathName)
                ? JsonSerializer.Deserialize<UsbShieldSettings10>(File.ReadAllText(PathName)) ?? new()
                : new();
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            return new();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PathName)!);
        File.WriteAllText(PathName, JsonSerializer.Serialize(this,
            new JsonSerializerOptions { WriteIndented = true }));
    }
}

internal sealed record UsbDeviceEvent10(
    DateTime CreatedUtc,
    string Drive,
    string Label,
    string FileSystem,
    long TotalBytes,
    string Action,
    int FilesScanned,
    int Suspicious,
    int Malicious,
    string Status);

internal sealed class UsbShieldMonitor10 : IDisposable
{
    private static readonly string[] RiskExtensions =
    [
        ".exe", ".dll", ".scr", ".com", ".bat", ".cmd", ".ps1", ".vbs", ".vbe",
        ".js", ".jse", ".wsf", ".wsh", ".hta", ".lnk", ".msi", ".reg"
    ];

    private readonly IndependentMainForm100 _form;
    private readonly FFGuardianEngine10 _engine;
    private readonly UsbShieldSettings10 _settings;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly HashSet<string> _known = new(StringComparer.OrdinalIgnoreCase);
    private bool _checking;

    public event EventHandler<UsbDeviceEvent10>? Activity;

    public UsbShieldMonitor10(IndependentMainForm100 form, FFGuardianEngine10 engine, UsbShieldSettings10 settings)
    {
        _form = form;
        _engine = engine;
        _settings = settings;
        _timer = new System.Windows.Forms.Timer { Interval = 3000 };
        _timer.Tick += async (_, _) => await CheckAsync();
    }

    public void Start()
    {
        _known.Clear();
        foreach (DriveInfo drive in GetRemovableDrives())
            _known.Add(drive.Name);
        _timer.Enabled = _settings.Enabled;
    }

    public void Restart() => Start();

    private async Task CheckAsync()
    {
        if (_checking || !_settings.Enabled)
            return;

        _checking = true;
        try
        {
            DriveInfo[] current = GetRemovableDrives();
            HashSet<string> names = current.Select(drive => drive.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            _known.RemoveWhere(name => !names.Contains(name));

            foreach (DriveInfo drive in current)
            {
                if (!_known.Add(drive.Name))
                    continue;
                await HandleInsertedAsync(drive);
            }
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
        }
        finally
        {
            _checking = false;
        }
    }

    private async Task HandleInsertedAsync(DriveInfo drive)
    {
        string label = Safe(() => drive.VolumeLabel, "Senza etichetta");
        string fileSystem = Safe(() => drive.DriveFormat, "N/D");
        long size = Safe(() => drive.TotalSize, 0L);

        if (_settings.Mode == UsbShieldMode10.Ignore)
        {
            Raise(new(DateTime.UtcNow, drive.Name, label, fileSystem, size,
                "IGNORATO", 0, 0, 0, "Dispositivo rilevato e ignorato secondo le impostazioni."));
            return;
        }

        if (_settings.Mode == UsbShieldMode10.Ask)
        {
            DialogResult answer = MessageBox.Show(_form,
                $"È stato collegato un dispositivo USB:\n\n{drive.Name} — {label}\n\nAvviare ora la scansione?",
                "FF GUARDIAN — USB Shield", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes)
            {
                Raise(new(DateTime.UtcNow, drive.Name, label, fileSystem, size,
                    "RIFIUTATO", 0, 0, 0, "Scansione non autorizzata dall’utente."));
                return;
            }
        }

        await ScanAsync(drive, label, fileSystem, size);
    }

    private async Task ScanAsync(DriveInfo drive, string label, string fileSystem, long size)
    {
        int quickWarnings = 0;
        List<string> indicators = [];

        if (_settings.CheckAutorun)
        {
            string autorun = Path.Combine(drive.RootDirectory.FullName, "autorun.inf");
            if (File.Exists(autorun))
            {
                quickWarnings++;
                indicators.Add("autorun.inf presente");
            }
        }

        try
        {
            foreach (string file in Directory.EnumerateFiles(drive.RootDirectory.FullName, "*",
                         SearchOption.AllDirectories).Take(20000))
            {
                string extension = Path.GetExtension(file);
                if (RiskExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    quickWarnings++;
                    if (indicators.Count < 8)
                        indicators.Add(Path.GetFileName(file));
                }
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
        }

        FolderScanSummary10 summary = await _engine.ScanFolderAsync(
            drive.RootDirectory.FullName,
            new Progress<string>(_ => { }),
            CancellationToken.None);

        int suspicious = summary.SuspiciousFiles + quickWarnings;
        string status = summary.MaliciousFiles > 0
            ? "Minacce rilevate"
            : suspicious > 0
                ? $"Elementi da verificare: {string.Join(", ", indicators.Take(4))}"
                : "Nessuna minaccia rilevata";

        Raise(new(DateTime.UtcNow, drive.Name, label, fileSystem, size,
            "SCANSIONE", summary.FilesScanned, suspicious, summary.MaliciousFiles, status));

        MessageBox.Show(_form,
            $"Scansione USB completata.\n\nUnità: {drive.Name} — {label}\nFile analizzati: {summary.FilesScanned:N0}\nElementi sospetti: {suspicious}\nMinacce: {summary.MaliciousFiles}\n\n{status}",
            "FF GUARDIAN — USB Shield",
            MessageBoxButtons.OK,
            summary.MaliciousFiles > 0 ? MessageBoxIcon.Error :
                suspicious > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
    }

    private void Raise(UsbDeviceEvent10 activity)
    {
        WriteHistory(activity);
        Activity?.Invoke(this, activity);
    }

    private static DriveInfo[] GetRemovableDrives() => DriveInfo.GetDrives()
        .Where(drive => drive.DriveType == DriveType.Removable && drive.IsReady)
        .ToArray();

    private static T Safe<T>(Func<T> action, T fallback)
    {
        try { return action(); }
        catch { return fallback; }
    }

    private static void WriteHistory(UsbDeviceEvent10 activity)
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FF Guardian", "Engine10", "UsbShield");
        Directory.CreateDirectory(folder);
        File.AppendAllText(Path.Combine(folder, "history.jsonl"),
            JsonSerializer.Serialize(activity) + Environment.NewLine);
        StabilityCoordinator82.WriteInformationLog(
            $"USB SHIELD: {activity.Drive} — {activity.Action} — {activity.Status}");
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}

internal static class UsbShieldCenter10
{
    private static readonly Color Background = Color.FromArgb(3, 8, 12);
    private static readonly Color Surface = Color.FromArgb(17, 31, 39);
    private static readonly Color Neon = Color.FromArgb(160, 255, 0);

    public static UsbShieldMonitor10 Attach(
        IndependentMainForm100 form,
        FFGuardianEngine10 engine,
        UsbShieldSettings10 settings)
    {
        TabControl? tabs = FindControl<TabControl>(form);
        if (tabs is null)
            throw new InvalidOperationException("Schede principali non trovate.");

        TabPage page = new("USB SHIELD")
        {
            BackColor = Background,
            ForeColor = Color.White,
            Padding = new Padding(18)
        };

        FlowLayoutPanel panel = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Background,
            Padding = new Padding(10)
        };
        panel.Controls.Add(new Label
        {
            Width = 850,
            Height = 58,
            Text = "USB SHIELD — PROTEZIONE DISPOSITIVI RIMOVIBILI",
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = Color.White
        });

        CheckBox enabled = Toggle("Attiva rilevamento dispositivi USB", settings.Enabled);
        CheckBox autorun = Toggle("Controlla autorun.inf, script, collegamenti ed eseguibili", settings.CheckAutorun);
        ComboBox mode = new()
        {
            Width = 280,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Surface,
            ForeColor = Color.White
        };
        mode.Items.AddRange(["CHIEDI PRIMA", "SCANSIONE AUTOMATICA", "IGNORA"]);
        mode.SelectedIndex = settings.Mode switch
        {
            UsbShieldMode10.AutomaticScan => 1,
            UsbShieldMode10.Ignore => 2,
            _ => 0
        };

        panel.Controls.Add(enabled);
        panel.Controls.Add(autorun);
        panel.Controls.Add(Row("Comportamento all’inserimento", mode));

        Label status = new()
        {
            Width = 850,
            Height = 70,
            BackColor = Surface,
            ForeColor = Color.White,
            Padding = new Padding(14),
            Text = "USB Shield pronto. In attesa di un dispositivo rimovibile."
        };
        panel.Controls.Add(status);

        Button save = Button("SALVA E RIAVVIA USB SHIELD");
        Button history = Button("APRI CRONOLOGIA USB");
        panel.Controls.Add(save);
        panel.Controls.Add(history);

        page.Controls.Add(panel);
        tabs.TabPages.Add(page);

        UsbShieldMonitor10 monitor = new(form, engine, settings);
        monitor.Activity += (_, activity) =>
        {
            status.Text = $"{activity.CreatedUtc.ToLocalTime():dd/MM/yyyy HH:mm} — {activity.Drive} {activity.Label}\n" +
                $"Azione: {activity.Action} | File: {activity.FilesScanned:N0} | Sospetti: {activity.Suspicious} | Minacce: {activity.Malicious}\n{activity.Status}";
            status.ForeColor = activity.Malicious > 0 ? Color.OrangeRed :
                activity.Suspicious > 0 ? Color.Gold : Neon;
        };

        save.Click += (_, _) =>
        {
            settings.Enabled = enabled.Checked;
            settings.CheckAutorun = autorun.Checked;
            settings.Mode = mode.SelectedIndex switch
            {
                1 => UsbShieldMode10.AutomaticScan,
                2 => UsbShieldMode10.Ignore,
                _ => UsbShieldMode10.Ask
            };
            settings.Save();
            monitor.Restart();
            MessageBox.Show(form, "USB Shield aggiornato.", "FF GUARDIAN",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        history.Click += (_, _) =>
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FF Guardian", "Engine10", "UsbShield");
            Directory.CreateDirectory(folder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", folder)
            {
                UseShellExecute = true
            });
        };

        monitor.Start();
        return monitor;
    }

    private static CheckBox Toggle(string text, bool value) => new()
    {
        Width = 850,
        Height = 52,
        Text = text,
        Checked = value,
        BackColor = Surface,
        ForeColor = Color.White,
        Padding = new Padding(14, 0, 0, 0),
        FlatStyle = FlatStyle.Flat,
        Font = new Font("Segoe UI", 10F, FontStyle.Bold)
    };

    private static FlowLayoutPanel Row(string text, Control control)
    {
        FlowLayoutPanel row = new()
        {
            Width = 850,
            Height = 60,
            BackColor = Surface,
            Padding = new Padding(14, 12, 14, 8)
        };
        row.Controls.Add(new Label { Width = 470, Height = 32, Text = text, ForeColor = Color.White });
        row.Controls.Add(control);
        return row;
    }

    private static Button Button(string text)
    {
        Button button = new()
        {
            Width = 310,
            Height = 44,
            Margin = new Padding(0, 8, 0, 0),
            Text = text,
            BackColor = Surface,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        button.FlatAppearance.BorderColor = Neon;
        return button;
    }

    private static T? FindControl<T>(Control root) where T : Control
    {
        if (root is T match) return match;
        foreach (Control child in root.Controls)
        {
            T? found = FindControl<T>(child);
            if (found is not null) return found;
        }
        return null;
    }
}
