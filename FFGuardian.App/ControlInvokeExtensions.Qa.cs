namespace FFGuardian;

/// <summary>
/// Overload tipizzato per l'invocazione asincrona sulla UI WinForms.
/// Evita conversioni implicite non valide da lambda a System.Delegate.
/// </summary>
internal static class ControlInvokeExtensionsQa
{
    public static IAsyncResult BeginInvoke(this Control control, Action action)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(action);
        return control.BeginInvoke((Delegate)action);
    }
}
