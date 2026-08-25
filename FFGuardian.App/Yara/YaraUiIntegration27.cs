using System.Runtime.CompilerServices;

namespace FFGuardian;

internal static class YaraUiIntegration27
{
    private static readonly Color Background = Color.FromArgb(4, 8, 11);
    private static readonly Color Surface = Color.FromArgb(10, 16, 20);
    private static readonly Color Neon = Color.FromArgb(112, 255, 24);
    private static readonly Color Text = Color.FromArgb(242, 247, 249);
    private static readonly Color Muted = Color.FromArgb(158, 174, 181);
    private static YaraRuntime? _runtime;
    private static CancellationTokenSource? _operation;
    private static Label? _state;
    private static Label? _version;
    private static Label? _rules;
    private static Label? _checked;
    private static Label? _updated;
    private static Label? _detail;
    private static ProgressBar? _progress;
    private static bool _started;

    [ModuleInitializer]
    internal static void Initialize() => Application.Idle += StartWhenReady;

    private static void StartWhenReady(object? sender, EventArgs e)
    {
        if (_started) return;
        IndependentMainForm100? form = Application.OpenForms.OfType<IndependentMainForm100>().FirstOrDefault();
        if (form is null || form.IsDisposed || !form.IsHandleCreated) return;
        TabControl? tabs = FindControls<TabControl>(form).OrderByDescending(x => x.TabCount).FirstOrDefault();
        TabPage? page = tabs?.TabPages.Cast<TabPage>().FirstOrDefault(x =>
            x.Text.Contains("AGGIORN", StringComparison.OrdinalIgnoreCase));
        if (page is null) return;
        _started = true; Application.Idle -= StartWhenReady;
        _runtime = new YaraRuntime();
        InstallUi(page, form);
        _ = RefreshAsync();
        form.FormClosed += (_, _) => Dispose();
    }

    private static void InstallUi(TabPage page, Form owner)
    {
        Control? existing = page.Controls.Cast<Control>().FirstOrDefault();
        page.Controls.Clear(); page.BackColor = Background;
        TableLayoutPanel root = new()
        {
            Name = "YaraRealUpdates27", Dock = DockStyle.Fill, BackColor = Background,
            ColumnCount = 1, RowCount = 3, Padding = new Padding(14)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        FlowLayoutPanel commands = new()
        {
            Dock = DockStyle.Fill, BackColor = Background, AutoScroll = true,
            WrapContents = true, FlowDirection = FlowDirection.LeftToRight
        };
        AddButton(commands, "INSTALLA YARA", async () => await InstallAsync(owner));
        AddButton(commands, "VERIFICA MOTORE", RefreshAsync);
        AddButton(commands, "AGGIORNA MOTORE YARA", async () => await UpdateAsync(owner));
        AddButton(commands, "AGGIORNA REGOLE", async () => await CompileRulesAsync(owner));
        AddButton(commands, "IMPORTA REGOLE", async () => await ImportRulesAsync(owner));
        AddButton(commands, "TESTA YARA", async () => await TestAsync(owner));
        AddButton(commands, "APRI LOG", () => { OpenLogs(); return Task.CompletedTask; });
        root.Controls.Add(commands, 0, 0);

        TableLayoutPanel body = new()
        {
            Dock = DockStyle.Fill, BackColor = Surface, ColumnCount = 2, RowCount = 7,
            Padding = new Padding(22), Margin = new Padding(0, 8, 0, 8)
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 245));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 6; i++) body.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _state = AddRow(body, 0, "YARA REALE:");
        _version = AddRow(body, 1, "VERSIONE MOTORE:");
        _rules = AddRow(body, 2, "REGOLE VALIDE:");
        _checked = AddRow(body, 3, "ULTIMO CONTROLLO:");
        _updated = AddRow(body, 4, "ULTIMO AGGIORNAMENTO:");
        _detail = AddRow(body, 5, "DETTAGLIO:");
        if (existing is not null)
        {
            Panel host = new() { Dock = DockStyle.Fill, BackColor = Surface, AutoScroll = true };
            existing.Dock = DockStyle.Top; existing.Height = 220; host.Controls.Add(existing);
            body.Controls.Add(host, 0, 6); body.SetColumnSpan(host, 2);
        }
        root.Controls.Add(body, 0, 1);
        _progress = new ProgressBar { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100, Style = ProgressBarStyle.Continuous };
        root.Controls.Add(_progress, 0, 2);
        page.Controls.Add(root);
    }

    private static Label AddRow(TableLayoutPanel table, int row, string title)
    {
        Label heading = new() { Dock = DockStyle.Fill, Text = title, ForeColor = Text,
            BackColor = Surface, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft };
        Label value = new() { Dock = DockStyle.Fill, Text = "VERIFICA IN CORSO", ForeColor = Muted,
            BackColor = Surface, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
        table.Controls.Add(heading, 0, row); table.Controls.Add(value, 1, row); return value;
    }

    private static void AddButton(FlowLayoutPanel panel, string text, Func<Task> action)
    {
        Button button = new() { Text = text, Width = 205, Height = 48, Margin = new Padding(0, 0, 10, 10),
            BackColor = Color.FromArgb(16, 24, 29), ForeColor = Neon, FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), UseVisualStyleBackColor = false,
            AutoEllipsis = true };
        button.FlatAppearance.BorderColor = Neon;
        button.Click += async (_, _) =>
        {
            button.Enabled = false;
            try { await action(); }
            catch (OperationCanceledException) { SetDetail("Operazione annullata."); }
            catch (Exception ex) { StabilityCoordinator82.WriteStabilityLog(ex); SetDetail(ex.Message); }
            finally { if (!button.IsDisposed) button.Enabled = true; }
        };
        panel.Controls.Add(button);
    }

    private static async Task RefreshAsync()
    {
        YaraPortableProbeResult probe = await YaraPortableManager29.ProbeAsync(CancellationToken.None);
        int validRules = 0;
        if (_runtime is not null)
        {
            try { validRules = _runtime.Rules.GetEnabledRuleFiles().Count; }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        Post(() =>
        {
            if (_state is null || _state.IsDisposed) return;
            _state.Text = probe.Active ? "ATTIVO" : probe.Installed ? "ERRORE MOTORE" : "NON INSTALLATO";
            _state.ForeColor = probe.Active ? Neon : Color.OrangeRed;
            if (_version is not null) _version.Text = probe.Version;
            if (_rules is not null) _rules.Text = validRules.ToString();
            if (_checked is not null) _checked.Text = DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss");
            if (_updated is not null)
            {
                DateTime update = File.Exists(probe.ExecutablePath)
                    ? File.GetLastWriteTime(probe.ExecutablePath) : DateTime.MinValue;
                _updated.Text = update == DateTime.MinValue ? "--" : update.ToString("dd/MM/yyyy HH:mm:ss");
            }
            if (_detail is not null)
            {
                string path = string.IsNullOrWhiteSpace(probe.ExecutablePath) ? string.Empty :
                    $" Percorso: {probe.ExecutablePath}";
                _detail.Text = probe.Detail + path;
            }
        });
    }

    private static async Task InstallAsync(Form owner)
    {
        BeginOperation();
        try
        {
            Progress<int> percent = new(value => Post(() =>
            {
                if (_progress is not null) _progress.Value = Math.Clamp(value, 0, 100);
            }));
            Progress<string> status = new(SetDetail);
            YaraPortableProbeResult probe = await YaraPortableManager29.InstallOfficialWindowsX64Async(
                percent, status, _operation!.Token);
            await RefreshAsync();
            MessageBox.Show(owner,
                $"YARA installato e verificato realmente.\nVersione: {probe.Version}\nPercorso: {probe.ExecutablePath}",
                "FFGuardian", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        finally { EndOperation(); }
    }

    private static async Task UpdateAsync(Form owner)
    {
        await InstallAsync(owner);
    }

    private static async Task CompileRulesAsync(Form owner)
    {
        if (_runtime is null) return;
        int count = await _runtime.Rules.ValidateAndCompileAsync(CancellationToken.None);
        await RefreshAsync();
        MessageBox.Show(owner, $"Regole YARA valide: {count}", "FFGuardian", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static async Task ImportRulesAsync(Form owner)
    {
        if (_runtime is null) return;
        using OpenFileDialog dialog = new() { Title = "Importa regole YARA",
            Filter = "Regole YARA|*.yar;*.yara", Multiselect = true };
        if (dialog.ShowDialog(owner) != DialogResult.OK) return;
        foreach (string file in dialog.FileNames) await _runtime.Rules.ImportAsync(file, CancellationToken.None);
        await RefreshAsync();
    }

    private static async Task TestAsync(Form owner)
    {
        YaraPortableProbeResult probe = await YaraPortableManager29.ProbeAsync(CancellationToken.None);
        await RefreshAsync();
        MessageBox.Show(owner,
            probe.Active
                ? $"Test YARA superato realmente.\nVersione: {probe.Version}\nPercorso: {probe.ExecutablePath}"
                : "Test YARA non superato: " + probe.Detail,
            "FFGuardian — Test YARA", MessageBoxButtons.OK,
            probe.Active ? MessageBoxIcon.Information : MessageBoxIcon.Error);
    }

    private static void OpenLogs()
    {
        string logs = _runtime?.Configuration.LogsDirectory
            ?? Path.Combine(AppContext.BaseDirectory, "Engine", "Yara", "Logs");
        Directory.CreateDirectory(logs);
        Process.Start(new ProcessStartInfo { FileName = logs, UseShellExecute = true });
    }

    private static void BeginOperation()
    {
        _operation?.Cancel(); _operation?.Dispose(); _operation = new CancellationTokenSource();
        if (_progress is not null) _progress.Value = 0;
    }
    private static void EndOperation() { _operation?.Dispose(); _operation = null; }
    private static void SetDetail(string text) => Post(() => { if (_detail is not null) _detail.Text = text; });
    private static void Post(Action action)
    {
        Control? control = _state;
        if (control is null || control.IsDisposed) return;
        if (control.InvokeRequired) control.BeginInvoke(action); else action();
    }
    private static IEnumerable<T> FindControls<T>(Control root) where T : Control
    {
        foreach (Control child in root.Controls)
        { if (child is T match) yield return match; foreach (T nested in FindControls<T>(child)) yield return nested; }
    }
    private static void Dispose()
    {
        _operation?.Cancel(); _operation?.Dispose(); _operation = null;
        _runtime?.Dispose(); _runtime = null;
    }
}
