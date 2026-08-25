internal static class RansomShieldMaximumSmokeBootstrap10
{
    // Esecuzione intenzionalmente esplicita: i test di sicurezza non devono
    // partire da un ModuleInitializer prima dell'ingresso in Program.Main.
    // Questo evita deadlock pre-Main e rende timeout/heartbeat diagnostici.
    internal static void Run() => RansomShieldMaximumSmokeTests10.Run();
}
