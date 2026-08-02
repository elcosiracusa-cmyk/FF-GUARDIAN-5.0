using System.Reflection;
using System.Runtime.CompilerServices;
using FFGuardian.Engine10;

namespace FFGuardian;

internal static class UsbShieldBootstrap10
{
    private static bool _attached;
    private static UsbShieldMonitor10? _monitor;

    [ModuleInitializer]
    internal static void Initialize()
    {
        Application.Idle += AttachWhenReady;
    }

    private static void AttachWhenReady(object? sender, EventArgs e)
    {
        if (_attached)
            return;

        IndependentMainForm100? form = Application.OpenForms
            .OfType<IndependentMainForm100>()
            .FirstOrDefault();
        if (form is null || form.IsDisposed || !form.IsHandleCreated)
            return;

        FieldInfo? engineField = typeof(IndependentMainForm100).GetField(
            "_engine", BindingFlags.Instance | BindingFlags.NonPublic);
        if (engineField?.GetValue(form) is not FFGuardianEngine10 engine)
        {
            StabilityCoordinator82.WriteInformationLog("USB Shield: orchestratore condiviso non disponibile.");
            return;
        }

        UsbShieldSettings10 settings = UsbShieldSettings10.Load();
        _monitor = UsbShieldCenter10.Attach(form, engine, settings);
        form.Disposed += (_, _) =>
        {
            _monitor?.Dispose();
            _monitor = null;
        };

        _attached = true;
        Application.Idle -= AttachWhenReady;
        StabilityCoordinator82.WriteInformationLog("USB Shield collegato all'orchestratore condiviso.");
    }
}
