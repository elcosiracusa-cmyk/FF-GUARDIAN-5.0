using System.Runtime.CompilerServices;

namespace FFGuardian;

internal static class FirewallCenterBootstrap10
{
    private static bool _attached;

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

        FirewallCenter10.Attach(form);
        _attached = true;
        Application.Idle -= AttachWhenReady;
        StabilityCoordinator82.WriteInformationLog("Firewall Center collegato all'interfaccia principale.");
    }
}
