using System.Net.NetworkInformation;
using System.ServiceProcess;

namespace FFGuardian;

internal enum CoreProtectionState
{
    Protected,
    Partial,
    ActionRequired
}

internal sealed record CoreHealthSnapshot(
    CoreProtectionState State,
    bool DefenderServiceRunning,
    bool FirewallServiceRunning,
    bool NetworkAvailable,
    long FreeSystemDriveBytes,
    DateTime CheckedAt,
    string Summary);

internal static class CoreHealth83
{
    private const string ButtonName = "FFG83_CORE_HEALTH";
    private static readonly HashSet<Form> ConfiguredForms = new();
    private static readonly SemaphoreSlim ScanGate = new(1, 1);
    private static readonly Color Bg = Color.FromArgb(5, 10, 13);
    private static readonly Color Surface = Color.FromArgb(11, 20, 24);
    private static readonly Color Neon = Color.FromArgb(142, 255, 0);
    private static CoreHealthSnapshot? _lastSnapshot;

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
            Text = "◉   Stato sistema 8.3",
            Width = Math.Max(235, menu.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 8),
            Height = 39,
            Margin = new Padding(0, 1, 0, 1),
            Padding = new Padding(12, 0, 0, 0),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Surface,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.4F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        button.FlatAppearance.BorderColor = Neon;
        button.Click += async (_, _) => await ShowHealthAsync(owner);
        menu.Controls.Add(button);
    }

    private static async Task ShowHealthAsync(Form owner)
    {
        using Form dialog = new()
        {
            Text = "FF GUARDIAN 8.3 — Core Health",
            Icon = owner.Icon,
            StartPosition = FormStartPosition.CenterParent,
            Size = new Size(760, 580),
            MinimumSize = new Size(700, 520),
            BackColor = Bg,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F)
        };

        Label status = new()
        {
            Dock = DockStyle.Top,
            Height = 92,
            Text = "Controllo dello stato di protezione in corso…",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 17F, FontStyle.Bold),
            ForeColor = Color.White
        };
        TextBox details = new()
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Surface,
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Consolas", 10.5F)
        };
        Button refresh = new()
        {
            Dock = DockStyle.Bottom,
            Height = 52,
            Text = "RIPETI CONTROLLO",
            BackColor = Surface,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        };
        refresh.FlatAppearance.BorderColor = Neon;

        dialog.Controls.Add(details);
        dialog.Controls.Add(refresh);
        dialog.Controls.Add(status);

        async Task RefreshAsync()
        {
            refresh.Enabled = false;
            status.Text = "Controllo dello stato di protezione in corso…";
            CoreHealthSnapshot snapshot = await CheckAsync(TimeSpan.FromSeconds(8));
            _lastSnapshot = snapshot;
            status.Text = snapshot.State switch
            {
                CoreProtectionState.Protected => "PROTETTO",
                CoreProtectionState.Partial => "PROTEZIONE PARZIALE",
                _ => "INTERVENTO RICHIESTO"
            };
            status.ForeColor = snapshot.State switch
            {
                CoreProtectionState.Protected => Neon,
                CoreProtectionState.Partial => Color.Gold,
                _ => Color.OrangeRed
            };
            details.Text = $"Stato: {status.Text}\r\n" +
                           $"Controllato: {snapshot.CheckedAt:dd/MM/yyyy HH:mm:ss}\r\n\r\n" +
                           $"Microsoft Defender Service: {(snapshot.DefenderServiceRunning ? "ATTIVO" : "NON ATTIVO")}\r\n" +
                           $"Windows Firewall Service: {(snapshot.FirewallServiceRunning ? "ATTIVO" : "NON ATTIVO")}\r\n" +
                           $"Rete disponibile: {(snapshot.NetworkAvailable ? "SÌ" : "NO")}\r\n" +
                           $"Spazio libero unità di sistema: {FormatBytes(snapshot.FreeSystemDriveBytes)}\r\n\r\n" +
                           snapshot.Summary;
            refresh.Enabled = true;
        }

        refresh.Click += async (_, _) => await RefreshAsync();
        dialog.Shown += async (_, _) => await RefreshAsync();
        dialog.ShowDialog(owner);
    }

    public static async Task<CoreHealthSnapshot> CheckAsync(TimeSpan timeout)
    {
        if (!await ScanGate.WaitAsync(0))
            return _lastSnapshot ?? EmptySnapshot("Controllo già in corso.");

        try
        {
            using CancellationTokenSource cts = new(timeout);
            Task<CoreHealthSnapshot> task = Task.Run(BuildSnapshot, cts.Token);
            try
            {
                return await task.WaitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                return EmptySnapshot("Controllo interrotto per timeout. Riprova.");
            }
            catch (Exception ex)
            {
                StabilityCoordinator82.WriteStabilityLog(ex);
                return EmptySnapshot("Controllo non completato. Consulta il registro stabilità.");
            }
        }
        finally
        {
            ScanGate.Release();
        }
    }

    private static CoreHealthSnapshot BuildSnapshot()
    {
        bool defender = IsServiceRunning("WinDefend");
        bool firewall = IsServiceRunning("mpssvc");
        bool network = NetworkInterface.GetIsNetworkAvailable();
        long freeBytes = GetSystemDriveFreeBytes();

        CoreProtectionState state = !defender || !firewall
            ? CoreProtectionState.ActionRequired
            : (!network || freeBytes < 2L * 1024 * 1024 * 1024)
                ? CoreProtectionState.Partial
                : CoreProtectionState.Protected;

        string summary = state switch
        {
            CoreProtectionState.Protected => "I servizi essenziali risultano attivi e il sistema dispone delle risorse minime consigliate.",
            CoreProtectionState.Partial => "La protezione principale è attiva, ma rete o spazio libero richiedono attenzione.",
            _ => "Uno o più servizi essenziali di sicurezza non risultano attivi. Apri Sicurezza di Windows e verifica Defender e Firewall."
        };

        return new CoreHealthSnapshot(state, defender, firewall, network, freeBytes, DateTime.Now, summary);
    }

    private static bool IsServiceRunning(string serviceName)
    {
        try
        {
            using ServiceController service = new(serviceName);
            return service.Status == ServiceControllerStatus.Running;
        }
        catch
        {
            return false;
        }
    }

    private static long GetSystemDriveFreeBytes()
    {
        try
        {
            string root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
            return new DriveInfo(root).AvailableFreeSpace;
        }
        catch
        {
            return 0;
        }
    }

    private static CoreHealthSnapshot EmptySnapshot(string summary) =>
        new(CoreProtectionState.Partial, false, false, NetworkInterface.GetIsNetworkAvailable(), 0, DateTime.Now, summary);

    private static string FormatBytes(long bytes)
    {
        if (bytes <= 0) return "Non disponibile";
        double gb = bytes / 1024d / 1024d / 1024d;
        return $"{gb:0.0} GB";
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
