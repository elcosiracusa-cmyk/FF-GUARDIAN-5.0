using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace FFGuardian;

internal static class SystemHealthCenter10
{
    private static readonly Color Background = Color.FromArgb(3, 8, 12);
    private static readonly Color Surface = Color.FromArgb(17, 31, 39);
    private static readonly Color Neon = Color.FromArgb(160, 255, 0);

    public static void Attach(IndependentMainForm100 form)
    {
        ArgumentNullException.ThrowIfNull(form);
        TabControl? tabs = FindControl<TabControl>(form);
        if (tabs is null || tabs.TabPages.Cast<TabPage>().Any(page => page.Text == "SALUTE PC"))
            return;

        TabPage page = new("SALUTE PC") { BackColor = Background, ForeColor = Color.White, Padding = new Padding(16) };
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Background
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "FF GUARDIAN — SALUTE E MANUTENZIONE PC",
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        FlowLayoutPanel cards = new() { Dock = DockStyle.Fill, BackColor = Surface, Padding = new Padding(8), WrapContents = false };
        Label cpu = Card("CPU", "verifica…");
        Label ram = Card("RAM", "verifica…");
        Label disk = Card("DISCO", "verifica…");
        Label restart = Card("RIAVVIO", "verifica…");
        cards.Controls.Add(cpu); cards.Controls.Add(ram); cards.Controls.Add(disk); cards.Controls.Add(restart);
        root.Controls.Add(cards, 0, 1);

        DataGridView grid = new()
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Background,
            ForeColor = Color.White,
            GridColor = Color.FromArgb(58, 76, 84),
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };
        grid.Columns.Add("Area", "AREA");
        grid.Columns.Add("Status", "STATO");
        grid.Columns.Add("Details", "DETTAGLI");
        root.Controls.Add(grid, 0, 2);

        FlowLayoutPanel commands = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Surface,
            Padding = new Padding(7)
        };
        Button refresh = Button("AGGIORNA STATO");
        Button cleanup = Button("PULIZIA TEMPORANEI");
        Button report = Button("ESPORTA REPORT");
        commands.Controls.Add(refresh); commands.Controls.Add(cleanup); commands.Controls.Add(report);
        root.Controls.Add(commands, 0, 3);

        HealthSnapshot10? latest = null;

        async Task RefreshAsync()
        {
            refresh.Enabled = false;
            try
            {
                latest = await CollectAsync();
                cpu.Text = $"CPU\n{latest.CpuPercent:0}%";
                ram.Text = $"RAM\n{latest.MemoryUsedPercent:0}%";
                disk.Text = $"DISCO C:\n{latest.SystemDriveFreeGb:0.0} GB liberi";
                restart.Text = $"RIAVVIO\n{(latest.RestartRequired ? "RICHIESTO" : "NON RICHIESTO")}";
                restart.ForeColor = latest.RestartRequired ? Color.OrangeRed : Neon;

                grid.Rows.Clear();
                AddRow(grid, "Processore", latest.CpuPercent < 90 ? "OK" : "CARICO ELEVATO", $"Utilizzo stimato {latest.CpuPercent:0}%");
                AddRow(grid, "Memoria", latest.MemoryUsedPercent < 90 ? "OK" : "QUASI ESAURITA", $"{latest.MemoryUsedGb:0.0} / {latest.MemoryTotalGb:0.0} GB utilizzati");
                AddRow(grid, "Disco di sistema", latest.SystemDriveFreePercent >= 10 ? "OK" : "SPAZIO RIDOTTO", $"{latest.SystemDriveFreeGb:0.0} GB liberi ({latest.SystemDriveFreePercent:0}%)");
                AddRow(grid, "Unità fisiche", latest.StorageStatus, latest.StorageDetails);
                AddRow(grid, "Windows Update", latest.UpdateStatus, latest.UpdateDetails);
                AddRow(grid, "Riavvio", latest.RestartRequired ? "RICHIESTO" : "OK", latest.RestartRequired ? "Windows segnala operazioni in attesa di riavvio." : "Nessun riavvio pendente rilevato.");
                AddRow(grid, "Avvio automatico", latest.StartupCount > 25 ? "NUMEROSO" : "OK", $"{latest.StartupCount} elementi rilevati");
                AddRow(grid, "File temporanei", "STIMA", $"Circa {latest.TempBytes / 1024d / 1024d:0.0} MB accessibili");
            }
            catch (Exception ex)
            {
                StabilityCoordinator82.WriteStabilityLog(ex);
                MessageBox.Show(form, ex.Message, "FF GUARDIAN — Salute PC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { refresh.Enabled = true; }
        }

        refresh.Click += async (_, _) => await RefreshAsync();
        cleanup.Click += async (_, _) =>
        {
            if (MessageBox.Show(form,
                "Eliminare soltanto i file temporanei accessibili del profilo utente?\n\nI file in uso verranno ignorati.",
                "Conferma pulizia sicura", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            cleanup.Enabled = false;
            try
            {
                CleanupResult10 result = await Task.Run(CleanupTempFiles);
                StabilityCoordinator82.WriteInformationLog($"Pulizia temporanei: {result.FilesDeleted} file, {result.BytesFreed} byte liberati, {result.Errors} errori ignorati.");
                MessageBox.Show(form,
                    $"Pulizia completata.\n\nFile eliminati: {result.FilesDeleted:N0}\nSpazio recuperato: {result.BytesFreed / 1024d / 1024d:0.0} MB\nFile saltati: {result.Errors:N0}",
                    "FF GUARDIAN — Manutenzione", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await RefreshAsync();
            }
            finally { cleanup.Enabled = true; }
        };
        report.Click += async (_, _) =>
        {
            latest ??= await CollectAsync();
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FF Guardian Reports", "SystemHealth");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, $"system-health-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(latest, new JsonSerializerOptions { WriteIndented = true }));
            MessageBox.Show(form, $"Report salvato in:\n{path}", "FF GUARDIAN", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        page.Controls.Add(root);
        tabs.TabPages.Add(page);
        page.Enter += async (_, _) => await RefreshAsync();
    }

    private static async Task<HealthSnapshot10> CollectAsync()
    {
        string ps = """
$os=Get-CimInstance Win32_OperatingSystem
$cpu=(Get-CimInstance Win32_Processor | Measure-Object LoadPercentage -Average).Average
$memTotal=[double]$os.TotalVisibleMemorySize*1KB
$memFree=[double]$os.FreePhysicalMemory*1KB
$drive=Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='C:'"
$disks=Get-PhysicalDisk -ErrorAction SilentlyContinue | ForEach-Object { "$($_.FriendlyName)|$($_.HealthStatus)|$($_.OperationalStatus)" }
$restart=(Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired') -or (Test-Path 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\PendingFileRenameOperations')
$startup=(Get-CimInstance Win32_StartupCommand -ErrorAction SilentlyContinue | Measure-Object).Count
[pscustomobject]@{Cpu=$cpu;MemTotal=$memTotal;MemFree=$memFree;DriveSize=[double]$drive.Size;DriveFree=[double]$drive.FreeSpace;Restart=$restart;Startup=$startup;Disks=($disks -join '; ')} | ConvertTo-Json -Compress
""";
        string json = await RunPowerShellAsync(ps);
        using JsonDocument doc = JsonDocument.Parse(json.Trim());
        JsonElement root = doc.RootElement;
        double total = GetDouble(root, "MemTotal");
        double free = GetDouble(root, "MemFree");
        double size = GetDouble(root, "DriveSize");
        double driveFree = GetDouble(root, "DriveFree");
        string disks = root.TryGetProperty("Disks", out JsonElement d) ? d.GetString() ?? "Informazioni non disponibili" : "Informazioni non disponibili";
        long tempBytes = EstimateTempBytes();

        return new HealthSnapshot10(
            DateTime.UtcNow,
            GetDouble(root, "Cpu"),
            (total - free) / 1024d / 1024d / 1024d,
            total / 1024d / 1024d / 1024d,
            total <= 0 ? 0 : (total - free) * 100d / total,
            driveFree / 1024d / 1024d / 1024d,
            size <= 0 ? 0 : driveFree * 100d / size,
            root.TryGetProperty("Restart", out JsonElement r) && r.GetBoolean(),
            root.TryGetProperty("Startup", out JsonElement s) ? s.GetInt32() : 0,
            disks.Contains("Unhealthy", StringComparison.OrdinalIgnoreCase) ? "ATTENZIONE" : "OK",
            disks,
            "VERIFICA MANUALE",
            "Apri Windows Update per controllare gli aggiornamenti disponibili.",
            tempBytes);
    }

    private static CleanupResult10 CleanupTempFiles()
    {
        long bytes = 0;
        int deleted = 0;
        int errors = 0;
        string temp = Path.GetTempPath();
        foreach (string file in Directory.EnumerateFiles(temp, "*", SearchOption.AllDirectories).Take(100000))
        {
            try
            {
                FileInfo info = new(file);
                if (info.LastWriteTimeUtc > DateTime.UtcNow.AddHours(-24))
                    continue;
                long length = info.Length;
                info.IsReadOnly = false;
                info.Delete();
                bytes += length;
                deleted++;
            }
            catch { errors++; }
        }
        return new CleanupResult10(deleted, bytes, errors);
    }

    private static long EstimateTempBytes()
    {
        long total = 0;
        try
        {
            foreach (string file in Directory.EnumerateFiles(Path.GetTempPath(), "*", SearchOption.AllDirectories).Take(100000))
                try { total += new FileInfo(file).Length; } catch { }
        }
        catch { }
        return total;
    }

    private static async Task<string> RunPowerShellAsync(string command)
    {
        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(command));
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -EncodedCommand {encoded}")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };
        process.Start();
        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error) ? output : error);
        return output;
    }

    private static double GetDouble(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.TryGetDouble(out double result) ? result : 0;

    private static void AddRow(DataGridView grid, string area, string status, string details) => grid.Rows.Add(area, status, details);

    private static Label Card(string title, string value) => new()
    {
        Width = 245,
        Height = 92,
        Margin = new Padding(6),
        Text = $"{title}\n{value}",
        BackColor = Background,
        ForeColor = Neon,
        Font = new Font("Segoe UI", 11F, FontStyle.Bold),
        TextAlign = ContentAlignment.MiddleCenter
    };

    private static Button Button(string text)
    {
        Button button = new()
        {
            Width = 215,
            Height = 42,
            Margin = new Padding(5),
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
        if (root is T match) return match;
        foreach (Control child in root.Controls)
        {
            T? found = FindControl<T>(child);
            if (found is not null) return found;
        }
        return null;
    }

    private sealed record CleanupResult10(int FilesDeleted, long BytesFreed, int Errors);
    private sealed record HealthSnapshot10(
        DateTime CreatedUtc,
        double CpuPercent,
        double MemoryUsedGb,
        double MemoryTotalGb,
        double MemoryUsedPercent,
        double SystemDriveFreeGb,
        double SystemDriveFreePercent,
        bool RestartRequired,
        int StartupCount,
        string StorageStatus,
        string StorageDetails,
        string UpdateStatus,
        string UpdateDetails,
        long TempBytes);
}
