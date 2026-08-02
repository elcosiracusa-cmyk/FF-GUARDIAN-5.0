using System.Reflection;
using System.Runtime.CompilerServices;
using FFGuardian.Engine10;

namespace FFGuardian;

internal static class AdvancedProcessBootstrap10
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
        if (form is null)
            return;

        FFGuardianEngine10? engine = form.GetType()
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
            .Where(field => typeof(FFGuardianEngine10).IsAssignableFrom(field.FieldType))
            .Select(field => field.GetValue(form) as FFGuardianEngine10)
            .FirstOrDefault(value => value is not null);

        if (engine is null)
            return;

        AdvancedProcessCenter10.Attach(form, engine);
        _attached = true;
        Application.Idle -= AttachWhenReady;
    }
}
