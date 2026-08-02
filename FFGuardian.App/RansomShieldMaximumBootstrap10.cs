using System.Reflection;
using System.Runtime.CompilerServices;

namespace FFGuardian;

internal static class RansomShieldMaximumBootstrap10
{
    private static RansomShieldMaximum10? _maximum;

    [ModuleInitializer]
    internal static void Initialize()
    {
        try
        {
            string entryName = Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty;
            if (!entryName.Equals("FFGuardian", StringComparison.OrdinalIgnoreCase))
                return;

            RansomShieldSettings10 settings = RansomShieldSettings10.Load();
            if (!settings.Enabled)
                return;

            _maximum = new RansomShieldMaximum10(settings);
            _maximum.Alert += OnAlert;
            _maximum.Start();

            AppDomain.CurrentDomain.ProcessExit += (_, _) => DisposeMaximum();
            Application.ApplicationExit += (_, _) => DisposeMaximum();
            StabilityCoordinator82.WriteInformationLog(
                $"Ransom Shield Maximum avviato su {_maximum.ProtectedFolderCount} cartelle.");
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
        }
    }

    private static void OnAlert(object? sender, RansomMaximumAlert10 alert)
    {
        try
        {
            StabilityCoordinator82.WriteInformationLog(
                $"Ransom Shield Maximum: {alert.Severity} {alert.Score}/100 — " +
                $"{alert.ChangedFiles} modifiche, {alert.RenamedFiles} rinomine, " +
                $"{alert.DeletedFiles} eliminazioni, {alert.HighEntropyFiles} file ad alta entropia — " +
                alert.SuspectedProcess);

            if (!RansomShieldSettings10.Load().ShowAlerts)
                return;

            using NotifyIcon notification = new()
            {
                Icon = DobermannIconFactory.CreateIcon(),
                Visible = true,
                BalloonTipTitle = $"FF GUARDIAN — Ransom Shield {alert.Severity}",
                BalloonTipText =
                    $"Attività compatibile con ransomware ({alert.Score}/100). " +
                    $"Cartella: {Path.GetFileName(alert.Folder)}. {alert.SuspectedProcess}",
                BalloonTipIcon = ToolTipIcon.Error
            };
            notification.ShowBalloonTip(8000);
            Thread.Sleep(500);
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
        }
    }

    private static void DisposeMaximum()
    {
        RansomShieldMaximum10? maximum = Interlocked.Exchange(ref _maximum, null);
        if (maximum is null)
            return;
        try
        {
            maximum.Alert -= OnAlert;
            maximum.Dispose();
        }
        catch
        {
        }
    }
}
