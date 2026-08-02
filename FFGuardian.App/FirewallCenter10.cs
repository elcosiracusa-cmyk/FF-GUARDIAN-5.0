using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;

namespace FFGuardian;

internal static class FirewallCenter10
{
    private static readonly Color Background = Color.FromArgb(3, 8, 12);
    private static readonly Color Surface = Color.FromArgb(17, 31, 39);
    private static readonly Color Neon = Color.FromArgb(160, 255, 0);
    private const string RulePrefix = "FFGuardian Block ";

    public static void Attach(IndependentMainForm100 form)
    {
        ArgumentNullException.ThrowIfNull(form);
        TabControl? tabs = FindControl<TabControl>(form);
        if (tabs is null || tabs.TabPages.Cast<TabPage>().Any(page => page.Text == "FIREWALL"))
            return;

        TabPage page = new("FIREWALL")
        {
            BackColor = Background,
            ForeColor = Color.White,
            Padding = new Padding(16)
        };

        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Background
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));

        root.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "FF GUARDIAN FIREWALL CENTER",
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);

        FlowLayoutPanel profiles = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(10),
            WrapContents = false
        };
        Label domain = StatusLabel("DOMINIO: verifica…");
        Label privateProfile = StatusLabel("PRIVATO: verifica…");
        Label publicProfile = StatusLabel("PUBBLICO: verifica…");
        profiles.Controls.Add(domain);
        profiles.Controls.Add(privateProfile);
        profiles.Controls.Add(publicProfile);
        root.Controls.Add(profiles, 0, 1);

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
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false
        };
        grid.Columns.Add("Protocol", "PROTOCOLLO");
        grid.Columns.Add("Local", "INDIRIZZO LOCALE");
        grid.Columns.Add("Remote", "INDIRIZZO REMOTO");
        grid.Columns.Add("State", "STATO");
        grid.Columns.Add("Pid", "PID");
        grid.Columns.Add("Process", "PROCESSO");
        root.Controls.Add(grid, 0, 2);

        FlowLayoutPanel commands = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = Surface,
            Padding = new Padding(7)
        };
        Button refresh = Button("AGGIORNA");
        Button enable = Button("ATTIVA FIREWALL");
        Button block = Button("BLOCCA PROGRAMMA");
        Button unblock = Button("SBLOCCA PROGRAMMA");
        Button backup = Button("BACKUP REGOLE");
        commands.Controls.Add(refresh);
        commands.Controls.Add(enable);
        commands.Controls.Add(block);
        commands.Controls.Add(unblock);
        commands.Controls.Add(backup);
        root.Controls.Add(commands, 0, 3);

        async Task RefreshAsync()
        {
            refresh.Enabled = false;
            try
            {
                Dictionary<string, bool> state = await GetProfileStateAsync();
                SetStatus(domain, "DOMINIO", state.GetValueOrDefault("Domain"));
                SetStatus(privateProfile, "PRIVATO", state.GetValueOrDefault("Private"));
                SetStatus(publicProfile, "PUBBLICO", state.GetValueOrDefault("Public"));
                grid.Rows.Clear();
                foreach (ConnectionRow10 connection in GetConnections())
                    grid.Rows.Add(connection.Protocol, connection.Local, connection.Remote,
                        connection.State, connection.ProcessId, connection.ProcessName);
            }
            catch (Exception ex)
            {
                StabilityCoordinator82.WriteStabilityLog(ex);
                MessageBox.Show(form, ex.Message, "FF GUARDIAN — Firewall Center",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                refresh.Enabled = true;
            }
        }

        refresh.Click += async (_, _) => await RefreshAsync();
        enable.Click += async (_, _) =>
        {
            if (!Confirm(form, "Attivare Windows Firewall per tutti i profili?")) return;
            await RunNetshAsync("advfirewall set allprofiles state on");
            await RefreshAsync();
        };
        backup.Click += async (_, _) =>
        {
            string path = await BackupRulesAsync();
            MessageBox.Show(form, $"Backup creato in:\n{path}", "FF GUARDIAN",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        block.Click += async (_, _) =>
        {
            using OpenFileDialog dialog = new()
            {
                Filter = "Programmi Windows (*.exe)|*.exe",
                Title = "Seleziona il programma da bloccare"
            };
            if (dialog.ShowDialog(form) != DialogResult.OK) return;
            string path = Path.GetFullPath(dialog.FileName);
            if (!Confirm(form, $"Bloccare tutte le connessioni in entrata e uscita per:\n{path}?")) return;
            await BackupRulesAsync();
            string ruleName = RulePrefix + SafeRuleName(path);
            await RunNetshAsync($"advfirewall firewall add rule name=\"{ruleName} OUT\" dir=out action=block program=\"{path}\" enable=yes");
            await RunNetshAsync($"advfirewall firewall add rule name=\"{ruleName} IN\" dir=in action=block program=\"{path}\" enable=yes");
            StabilityCoordinator82.WriteInformationLog($"Firewall: programma bloccato con conferma — {path}");
            MessageBox.Show(form, "Programma bloccato.", "FF GUARDIAN",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        };
        unblock.Click += async (_, _) =>
        {
            using OpenFileDialog dialog = new()
            {
                Filter = "Programmi Windows (*.exe)|*.exe",
                Title = "Seleziona il programma da sbloccare"
            };
            if (dialog.ShowDialog(form) != DialogResult.OK) return;
            string path = Path.GetFullPath(dialog.FileName);
            if (!Confirm(form, $"Rimuovere le regole FFGuardian per:\n{path}?")) return;
            string ruleName = RulePrefix + SafeRuleName(path);
            await RunNetshAsync($"advfirewall firewall delete rule name=\"{ruleName} OUT\"");
            await RunNetshAsync($"advfirewall firewall delete rule name=\"{ruleName} IN\"");
            StabilityCoordinator82.WriteInformationLog($"Firewall: programma sbloccato con conferma — {path}");
            MessageBox.Show(form, "Regole rimosse.", "FF GUARDIAN",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        page.Controls.Add(root);
        tabs.TabPages.Add(page);
        page.Enter += async (_, _) => await RefreshAsync();
    }

    private static IEnumerable<ConnectionRow10> GetConnections()
    {
        IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();
        foreach (TcpConnectionInformation connection in properties.GetActiveTcpConnections())
        {
            yield return new ConnectionRow10("TCP", connection.LocalEndPoint.ToString(),
                connection.RemoteEndPoint.ToString(), connection.State.ToString(), 0, "N/D");
        }
        foreach (IPEndPoint listener in properties.GetActiveTcpListeners())
            yield return new ConnectionRow10("TCP LISTEN", listener.ToString(), "-", "Listen", 0, "N/D");
        foreach (IPEndPoint listener in properties.GetActiveUdpListeners())
            yield return new ConnectionRow10("UDP", listener.ToString(), "-", "Listen", 0, "N/D");
    }

    private static async Task<Dictionary<string, bool>> GetProfileStateAsync()
    {
        string output = await RunPowerShellAsync(
            "Get-NetFirewallProfile | ForEach-Object { \"$($_.Name)|$($_.Enabled)\" }");
        Dictionary<string, bool> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (string line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = line.Split('|', 2);
            if (parts.Length == 2 && bool.TryParse(parts[1], out bool enabled))
                result[parts[0].Trim()] = enabled;
        }
        return result;
    }

    private static async Task<string> BackupRulesAsync()
    {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "FF Guardian Reports", "FirewallBackups");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, $"firewall-{DateTime.Now:yyyyMMdd-HHmmss}.wfw");
        await RunNetshAsync($"advfirewall export \"{path}\"");
        return path;
    }

    private static async Task<string> RunNetshAsync(string arguments) =>
        await RunProcessAsync("netsh.exe", arguments);

    private static async Task<string> RunPowerShellAsync(string command) =>
        await RunProcessAsync("powershell.exe", $"-NoProfile -NonInteractive -Command \"{command.Replace("\"", "\\\"")}\"");

    private static async Task<string> RunProcessAsync(string fileName, string arguments)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo(fileName, arguments)
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

    private static string SafeRuleName(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        string hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(path)))[..12];
        return $"{name} {hash}";
    }

    private static bool Confirm(IWin32Window owner, string text) =>
        MessageBox.Show(owner, text, "Conferma Firewall Center",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;

    private static Label StatusLabel(string text) => new()
    {
        Width = 240,
        Height = 48,
        Text = text,
        BackColor = Background,
        ForeColor = Color.White,
        TextAlign = ContentAlignment.MiddleCenter,
        Margin = new Padding(6)
    };

    private static void SetStatus(Label label, string profile, bool enabled)
    {
        label.Text = $"{profile}: {(enabled ? "ATTIVO" : "DISATTIVATO")}";
        label.ForeColor = enabled ? Neon : Color.OrangeRed;
    }

    private static Button Button(string text)
    {
        Button button = new()
        {
            Width = 205,
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

    private sealed record ConnectionRow10(string Protocol, string Local, string Remote,
        string State, int ProcessId, string ProcessName);
}
