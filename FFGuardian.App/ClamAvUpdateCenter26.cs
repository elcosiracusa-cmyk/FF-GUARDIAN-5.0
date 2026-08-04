using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FFGuardian;

internal sealed record ClamAvEngineManifest26(
    string Component,
    string Version,
    string Architecture,
    string DownloadUrl,
    string Sha256);

internal sealed record ClamAvStatus26(
    bool EngineActive,
    string EngineVersion,
    bool DatabaseUpdated,
    DateTime? LastUpdate,
    string Detail,
    ClamAvEngineManifest26? Manifest);

/// <summary>
/// Centro aggiornamenti ClamAV di FFGuardian.
/// Usa esclusivamente freshclam.exe per aggiornare le firme ufficiali.
/// Un pacchetto motore viene soltanto predisposto dopo HTTPS e SHA-256 validi.
/// </summary>
internal static class ClamAvUpdateCenter26
{
    private static readonly Color Background = Color.FromArgb(4, 8, 11);
    private static readonly Color Surface = Color.FromArgb(10, 16, 20);
    private static readonly Color Neon = Color.FromArgb(112, 255, 24);
    private static readonly Color Text = Color.FromArgb(242, 247, 249);
    private static readonly Color Muted = Color.FromArgb(158, 174, 181);
    private static readonly SemaphoreSlim UpdateGate = new(1, 1);
    private static readonly HttpClient Http = CreateHttpClient();

    private static System.Windows.Forms.Timer? _startupTimer;
    private static System.Windows.Forms.Timer? _periodicTimer;
    private static Label? _engineState;
    private static Label? _engineVersion;
    private static Label? _databaseState;
    private static Label? _lastUpdate;
    private static Label? _detail;
    private static TextBox? _manifestView;
    private static Button? _updateButton;
    private static Form? _form;
    private static bool _started;

    private static string DataRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FF Guardian", "ClamAV");

    private static string DatabaseDirectory => Path.Combine(DataRoot, "Database");
    private static string StagingDirectory => Path.Combine(DataRoot, "Staging");

    [ModuleInitializer]
    internal static void Initialize() => Application.Idle += StartWhenReady;

    private static void StartWhenReady(object? sender, EventArgs e)
    {
        if (_started)
            return;

        IndependentMainForm100? form = Application.OpenForms
            .OfType<IndependentMainForm100>()
            .FirstOrDefault();
        if (form is null || form.IsDisposed || !form.IsHandleCreated)
            return;

        _started = true;
        _form = form;
        Application.Idle -= StartWhenReady;

        _startupTimer = new System.Windows.Forms.Timer { Interval = 5200 };
        _startupTimer.Tick += async (_, _) =>
        {
            _startupTimer?.Stop();
            _startupTimer?.Dispose();
            _startupTimer = null;
            InstallUpdatesUi(form);
            await RefreshStatusAsync(CancellationToken.None);

            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(20)).ConfigureAwait(false);
                await UpdateSignaturesAutomaticallyAsync().ConfigureAwait(false);
            });
        };
        _startupTimer.Start();

        _periodicTimer = new System.Windows.Forms.Timer
        {
            Interval = checked((int)TimeSpan.FromHours(6).TotalMilliseconds)
        };
        _periodicTimer.Tick += async (_, _) => await UpdateSignaturesAutomaticallyAsync();
        _periodicTimer.Start();

        form.FormClosed += (_, _) => DisposeResources();
    }

    private static void InstallUpdatesUi(Control form)
    {
        TabControl? tabs = FindControls<TabControl>(form)
            .OrderByDescending(candidate => candidate.TabCount)
            .FirstOrDefault(candidate => candidate.TabCount > 0);
        TabPage? page = tabs?.TabPages.Cast<TabPage>()
            .FirstOrDefault(candidate => candidate.Text.Contains(
                "AGGIORN", StringComparison.OrdinalIgnoreCase));
        if (page is null || FindControls<Control>(page).Any(
                control => control.Name == "ClamAvUpdateCenter26"))
            return;

        Control? root = page.Controls.Cast<Control>().FirstOrDefault(control =>
            control.Name == "CommercialPageRoot18");
        if (root is not TableLayoutPanel table || table.RowCount < 3)
            return;

        FlowLayoutPanel? commands = FindControls<FlowLayoutPanel>(table)
            .FirstOrDefault(flow => FindControls<Button>(flow).Any());
        if (commands is null)
            return;

        _updateButton = CreateButton("AGGIORNA FIRME CLAMAV");
        _updateButton.Name = "ClamAvFreshclamButton26";
        _updateButton.Click += async (_, _) => await RunManualUpdateAsync();
        commands.Controls.Add(_updateButton);

        TableLayoutPanel status = new()
        {
            Name = "ClamAvUpdateCenter26",
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Text,
            ColumnCount = 2,
            RowCount = 6,
            Padding = new Padding(18),
            Margin = new Padding(0, 8, 0, 0)
        };
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230F));
        status.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (int index = 0; index < 5; index++)
            status.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        status.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        _engineState = AddStatusRow(status, 0, "MOTORE CLAMAV:");
        _engineVersion = AddStatusRow(status, 1, "VERSIONE MOTORE:");
        _databaseState = AddStatusRow(status, 2, "DATABASE FIRME:");
        _lastUpdate = AddStatusRow(status, 3, "ULTIMO AGGIORNAMENTO:");
        _detail = AddStatusRow(status, 4, "DETTAGLIO:");

        _manifestView = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Background,
            ForeColor = Muted,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 9.5F),
            Text = "Manifest ClamAV in caricamento…"
        };
        status.Controls.Add(_manifestView, 0, 5);
        status.SetColumnSpan(_manifestView, 2);

        Control? content = table.GetControlFromPosition(0, 2);
        if (content is null)
        {
            table.Controls.Add(status, 0, 2);
        }
        else
        {
            TableLayoutPanel host = new()
            {
                Dock = DockStyle.Fill,
                BackColor = Background,
                ColumnCount = 1,
                RowCount = 2,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            host.RowStyles.Add(new RowStyle(SizeType.Percent, 48F));
            host.RowStyles.Add(new RowStyle(SizeType.Percent, 52F));
            table.Controls.Remove(content);
            host.Controls.Add(content, 0, 0);
            host.Controls.Add(status, 0, 1);
            table.Controls.Add(host, 0, 2);
        }

        table.PerformLayout();
    }

    private static Label AddStatusRow(TableLayoutPanel table, int row, string title)
    {
        Label heading = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Text,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Text = title,
            TextAlign = ContentAlignment.MiddleLeft
        };
        Label value = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Text = "VERIFICA IN CORSO",
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
        table.Controls.Add(heading, 0, row);
        table.Controls.Add(value, 1, row);
        return value;
    }

    private static Button CreateButton(string text)
    {
        Button button = new()
        {
            AutoSize = false,
            Width = 250,
            Height = 48,
            Margin = new Padding(0, 0, 12, 12),
            Text = text,
            BackColor = Color.FromArgb(16, 24, 29),
            ForeColor = Neon,
            FlatStyle = FlatStyle.Flat,
            UseVisualStyleBackColor = false,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            AutoEllipsis = true
        };
        button.FlatAppearance.BorderColor = Neon;
        button.FlatAppearance.BorderSize = 1;
        return button;
    }

    private static async Task RunManualUpdateAsync()
    {
        if (_form is null || _form.IsDisposed)
            return;

        SetButtonBusy(true);
        try
        {
            ClamAvStatus26 status = await UpdateSignaturesAsync(CancellationToken.None);
            ApplyStatus(status);
            string message = status.DatabaseUpdated
                ? "Database firme ClamAV aggiornato correttamente."
                : status.Detail;
            MessageBox.Show(
                _form,
                message,
                "FFGuardian — Aggiornamento ClamAV",
                MessageBoxButtons.OK,
                status.DatabaseUpdated ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            MessageBox.Show(
                _form,
                ex.Message,
                "FFGuardian — Errore aggiornamento ClamAV",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            SetButtonBusy(false);
        }
    }

    private static async Task UpdateSignaturesAutomaticallyAsync()
    {
        try
        {
            ClamAvStatus26 before = await InspectStatusAsync(CancellationToken.None)
                .ConfigureAwait(false);
            if (!before.EngineActive || LocateExecutable("freshclam.exe") is null)
            {
                PostStatus(before);
                return;
            }

            ClamAvStatus26 updated = await UpdateSignaturesAsync(CancellationToken.None)
                .ConfigureAwait(false);
            PostStatus(updated);
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
        }
    }

    private static async Task<ClamAvStatus26> UpdateSignaturesAsync(
        CancellationToken cancellationToken)
    {
        await UpdateGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string? freshclam = LocateExecutable("freshclam.exe");
            if (freshclam is null)
                return await InspectStatusAsync(cancellationToken, "freshclam.exe non trovato.")
                    .ConfigureAwait(false);

            Directory.CreateDirectory(DatabaseDirectory);
            string arguments = $"--datadir=\"{DatabaseDirectory}\" --stdout";
            ProcessResult26 result = await RunProcessAsync(
                freshclam,
                arguments,
                TimeSpan.FromMinutes(8),
                cancellationToken).ConfigureAwait(false);

            bool success = result.ExitCode == 0 ||
                result.Output.Contains("up-to-date", StringComparison.OrdinalIgnoreCase) ||
                result.Output.Contains("is up to date", StringComparison.OrdinalIgnoreCase);
            string detail = success
                ? "freshclam.exe completato: firme verificate."
                : $"freshclam.exe terminato con codice {result.ExitCode}: {TrimDetail(result.Output)}";

            return await InspectStatusAsync(cancellationToken, detail, success)
                .ConfigureAwait(false);
        }
        finally
        {
            UpdateGate.Release();
        }
    }

    private static async Task RefreshStatusAsync(CancellationToken cancellationToken)
    {
        ClamAvStatus26 status = await InspectStatusAsync(cancellationToken).ConfigureAwait(false);
        PostStatus(status);
    }

    private static async Task<ClamAvStatus26> InspectStatusAsync(
        CancellationToken cancellationToken,
        string? detailOverride = null,
        bool? databaseUpdatedOverride = null)
    {
        string? scanner = LocateExecutable("clamscan.exe");
        string? freshclam = LocateExecutable("freshclam.exe");
        bool active = scanner is not null || freshclam is not null;
        string version = active
            ? await ReadEngineVersionAsync(scanner ?? freshclam!, cancellationToken).ConfigureAwait(false)
            : "NON INSTALLATO";

        DateTime? lastUpdate = GetLatestDatabaseWriteTime();
        bool databaseUpdated = databaseUpdatedOverride ?? lastUpdate.HasValue;
        ClamAvEngineManifest26? manifest = ReadManifest();
        string detail = detailOverride ?? (active
            ? freshclam is null
                ? "Motore rilevato, ma freshclam.exe non è disponibile."
                : "Motore e aggiornamento firme disponibili."
            : "ClamAV non rilevato. Installare il pacchetto x64 verificato.");

        return new ClamAvStatus26(
            active,
            version,
            databaseUpdated,
            lastUpdate,
            detail,
            manifest);
    }

    private static string? LocateExecutable(string fileName)
    {
        string? environmentHome = Environment.GetEnvironmentVariable("CLAMAV_HOME");
        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, "ClamAV", fileName),
            Path.Combine(AppContext.BaseDirectory, fileName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "ClamAV", fileName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "ClamAV", fileName),
            string.IsNullOrWhiteSpace(environmentHome)
                ? string.Empty
                : Path.Combine(environmentHome, fileName)
        ];

        return candidates.FirstOrDefault(path =>
            !string.IsNullOrWhiteSpace(path) && File.Exists(path));
    }

    private static async Task<string> ReadEngineVersionAsync(
        string executable,
        CancellationToken cancellationToken)
    {
        try
        {
            ProcessResult26 result = await RunProcessAsync(
                executable,
                "--version",
                TimeSpan.FromSeconds(20),
                cancellationToken).ConfigureAwait(false);
            Match match = Regex.Match(
                result.Output,
                @"ClamAV\s+(?<version>\d+(?:\.\d+){1,3})",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            return match.Success ? match.Groups["version"].Value : "1.x";
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            return "1.x";
        }
    }

    private static DateTime? GetLatestDatabaseWriteTime()
    {
        if (!Directory.Exists(DatabaseDirectory))
            return null;

        string[] extensions = ["*.cvd", "*.cld"];
        DateTime? latest = null;
        foreach (string extension in extensions)
        {
            foreach (string file in Directory.EnumerateFiles(DatabaseDirectory, extension))
            {
                DateTime writeTime = File.GetLastWriteTime(file);
                if (!latest.HasValue || writeTime > latest.Value)
                    latest = writeTime;
            }
        }
        return latest;
    }

    private static ClamAvEngineManifest26? ReadManifest()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Assets", "clamav-engine-manifest.json");
        if (!File.Exists(path))
            return null;

        try
        {
            using FileStream stream = File.OpenRead(path);
            JsonDocument document = JsonDocument.Parse(stream);
            JsonElement root = document.RootElement;
            return new ClamAvEngineManifest26(
                root.GetProperty("component").GetString() ?? string.Empty,
                root.GetProperty("version").GetString() ?? string.Empty,
                root.GetProperty("architecture").GetString() ?? string.Empty,
                root.GetProperty("downloadUrl").GetString() ?? string.Empty,
                root.GetProperty("sha256").GetString() ?? string.Empty);
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            return null;
        }
    }

    internal static async Task<string?> StageVerifiedEnginePackageAsync(
        CancellationToken cancellationToken = default)
    {
        ClamAvEngineManifest26 manifest = ReadManifest()
            ?? throw new InvalidOperationException("Manifest ClamAV non disponibile.");
        ValidateManifest(manifest);

        Directory.CreateDirectory(StagingDirectory);
        string destination = Path.Combine(
            StagingDirectory,
            $"clamav-{manifest.Version}-x64.package");
        string temporary = destination + ".download";

        using HttpResponseMessage response = await Http.GetAsync(
            manifest.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using (Stream input = await response.Content.ReadAsStreamAsync(cancellationToken)
                         .ConfigureAwait(false))
        await using (FileStream output = new(
                         temporary,
                         FileMode.Create,
                         FileAccess.Write,
                         FileShare.None,
                         81920,
                         useAsync: true))
        {
            await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        }

        string actualHash = await ComputeSha256Async(temporary, cancellationToken)
            .ConfigureAwait(false);
        if (!actualHash.Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(temporary);
            throw new CryptographicException("SHA-256 del pacchetto ClamAV non valido.");
        }

        File.Move(temporary, destination, overwrite: true);
        return destination;
    }

    private static void ValidateManifest(ClamAvEngineManifest26 manifest)
    {
        if (!manifest.Component.Equals("clamav-engine", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Componente del manifest non valido.");
        if (!manifest.Architecture.Equals("x64", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Il pacchetto ClamAV deve essere x64.");
        if (!Uri.TryCreate(manifest.DownloadUrl, UriKind.Absolute, out Uri? uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            manifest.DownloadUrl.Contains("URL_DEL_", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("URL HTTPS verificato non configurato.");
        if (!Regex.IsMatch(manifest.Sha256, "^[A-Fa-f0-9]{64}$") ||
            manifest.Sha256.Contains("HASH_", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("SHA-256 del manifest non configurato.");
        if (!Regex.IsMatch(manifest.Version, "^\\d+(?:\\.\\d+){1,3}$"))
            throw new InvalidDataException("Versione ClamAV non valida.");
    }

    private static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using FileStream stream = new(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private static async Task<ProcessResult26> RunProcessAsync(
        string executable,
        string arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(executable) ?? AppContext.BaseDirectory
            },
            EnableRaisingEvents = true
        };

        StringBuilder output = new();
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                output.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                output.AppendLine(e.Data);
        };

        if (!process.Start())
            throw new InvalidOperationException($"Impossibile avviare {Path.GetFileName(executable)}.");
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using CancellationTokenSource timeoutSource = new(timeout);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, timeoutSource.Token);
        try
        {
            await process.WaitForExitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
            throw;
        }

        return new ProcessResult26(process.ExitCode, output.ToString());
    }

    private static HttpClient CreateHttpClient()
    {
        HttpClient client = new()
        {
            Timeout = TimeSpan.FromMinutes(10)
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("FFGuardian/10.0.1");
        return client;
    }

    private static void PostStatus(ClamAvStatus26 status)
    {
        Form? form = _form;
        if (form is null || form.IsDisposed)
            return;
        try
        {
            form.BeginInvoke(() => ApplyStatus(status));
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static void ApplyStatus(ClamAvStatus26 status)
    {
        SetStatus(_engineState, status.EngineActive ? "ATTIVO" : "NON INSTALLATO",
            status.EngineActive ? Neon : Color.OrangeRed);
        SetStatus(_engineVersion, status.EngineVersion, Text);
        SetStatus(_databaseState, status.DatabaseUpdated ? "AGGIORNATO" : "NON AGGIORNATO",
            status.DatabaseUpdated ? Neon : Color.Gold);
        SetStatus(_lastUpdate,
            status.LastUpdate?.ToString("dd/MM/yyyy HH:mm:ss") ?? "MAI",
            status.LastUpdate.HasValue ? Text : Muted);
        SetStatus(_detail, status.Detail, Muted);

        if (_manifestView is not null && !_manifestView.IsDisposed)
        {
            _manifestView.Text = status.Manifest is null
                ? "Manifest ClamAV non disponibile."
                : JsonSerializer.Serialize(status.Manifest, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
        }
    }

    private static void SetStatus(Label? label, string value, Color color)
    {
        if (label is null || label.IsDisposed)
            return;
        label.Text = value;
        label.ForeColor = color;
    }

    private static void SetButtonBusy(bool busy)
    {
        if (_updateButton is null || _updateButton.IsDisposed)
            return;
        _updateButton.Enabled = !busy;
        _updateButton.Text = busy ? "AGGIORNAMENTO CLAMAV…" : "AGGIORNA FIRME CLAMAV";
    }

    private static string TrimDetail(string value)
    {
        string compact = value.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();
        while (compact.Contains("  ", StringComparison.Ordinal))
            compact = compact.Replace("  ", " ", StringComparison.Ordinal);
        return compact.Length <= 240 ? compact : compact[..240] + "…";
    }

    private static IEnumerable<T> FindControls<T>(Control root) where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T match)
                yield return match;
            foreach (T nested in FindControls<T>(child))
                yield return nested;
        }
    }

    private static void DisposeResources()
    {
        _startupTimer?.Stop();
        _startupTimer?.Dispose();
        _startupTimer = null;
        _periodicTimer?.Stop();
        _periodicTimer?.Dispose();
        _periodicTimer = null;
        Http.Dispose();
        UpdateGate.Dispose();
        _form = null;
    }

    private sealed record ProcessResult26(int ExitCode, string Output);
}
