using System.Runtime.CompilerServices;

internal static class RansomShieldMaximumSmokeBootstrap10
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        try
        {
            RansomShieldMaximumSmokeTests10.Run();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Ransom Shield Maximum score tests: FAILED");
            Console.Error.WriteLine(ex.ToString());
            throw;
        }
    }
}
