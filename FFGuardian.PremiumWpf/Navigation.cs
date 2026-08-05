using System.Collections.ObjectModel;
using System.Diagnostics;

namespace FFGuardian.PremiumWpf;

public interface INavigationService
{
    IReadOnlyCollection<NavigationPageViewModel> Pages { get; }
    NavigationPageViewModel CurrentPage { get; }
    string CurrentRoute { get; }
    bool CanNavigate(string route);
    NavigationResult NavigateTo(string route);
    NavigationResult GoBack();
    event EventHandler<NavigationPageViewModel>? CurrentPageChanged;
}

public sealed record NavigationResult(bool Success, string Route, string? ErrorCode, string Message, Exception? Exception, TimeSpan Duration);

public sealed class NavigationPageViewModel(string route, string title, string description, string primaryAction, bool dashboard = false)
{
    public string Route { get; } = route;
    public string Title { get; } = title;
    public string Description { get; } = description;
    public string PrimaryAction { get; } = primaryAction;
    public bool IsDashboard { get; } = dashboard;
}

public sealed class NavigationService : INavigationService
{
    private readonly Dictionary<string, NavigationPageViewModel> _pages;
    private readonly Stack<string> _history = new();

    public NavigationService()
    {
        NavigationPageViewModel[] pages =
        [
            new("dashboard", "Dashboard", "Riepilogo generale della protezione e dei componenti verificati.", "Aggiorna stato", true),
            new("scan", "Scansione", "Scansione rapida, completa e personalizzata tramite i servizi condivisi.", "Avvia scansione"),
            new("realtime", "Protezione in tempo reale", "Monitoraggio dei file e degli eventi di sicurezza. Lo stato dipende dai controlli runtime.", "Verifica protezione"),
            new("ransom-shield", "Ransom Shield", "Stato reale della protezione comportamentale contro modifiche massive dei file.", "Aggiorna stato"),
            new("firewall", "Firewall", "Stato reale del firewall di Windows rilevato dal Security Center.", "Aggiorna stato"),
            new("usb-shield", "USB Shield", "Stato dei dispositivi rimovibili e delle scansioni USB disponibili.", "Aggiorna stato"),
            new("quarantine", "Quarantena", "Stato del servizio di quarantena e degli elementi isolati.", "Aggiorna stato"),
            new("ai-analysis", "Analisi AI", "Stato del modello locale verificato. Nessuna analisi viene simulata.", "Aggiorna stato"),
            new("security-center", "Security Center", "Controlli reali di salute, integrità e disponibilità dei motori.", "Esegui controllo completo"),
            new("updates", "Aggiornamenti", "Versione dei motori, database firme e stato degli aggiornamenti.", "Aggiorna stato"),
            new("audit", "Report e Audit", "Cronologia operazioni, report diagnostici e risultati dei controlli.", "Aggiorna stato"),
            new("pc-health", "Salute PC", "Informazioni sul sistema e controlli disponibili senza valori simulati.", "Aggiorna stato"),
            new("tools", "Strumenti", "Strumenti di sicurezza disponibili e relativo stato operativo.", "Aggiorna stato"),
            new("recovery", "Recupero file", "Stato dei servizi di recupero e ripristino controllato.", "Aggiorna stato"),
            new("activity", "Attività", "Eventi recenti, controlli e operazioni registrate.", "Aggiorna stato"),
            new("settings", "Impostazioni", "Configurazione dell'applicazione e dei percorsi scrivibili.", "Aggiorna stato"),
            new("support", "Assistenza", "Informazioni diagnostiche e canali di supporto.", "Aggiorna stato")
        ];
        _pages = pages.ToDictionary(page => page.Route, StringComparer.OrdinalIgnoreCase);
        CurrentPage = _pages["dashboard"];
    }

    public IReadOnlyCollection<NavigationPageViewModel> Pages => new ReadOnlyCollection<NavigationPageViewModel>(_pages.Values.ToList());
    public NavigationPageViewModel CurrentPage { get; private set; }
    public string CurrentRoute => CurrentPage.Route;
    public event EventHandler<NavigationPageViewModel>? CurrentPageChanged;
    public bool CanNavigate(string route) => !string.IsNullOrWhiteSpace(route) && _pages.ContainsKey(route);

    public NavigationResult NavigateTo(string route)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            if (!CanNavigate(route)) return new(false, route, "ROUTE_NOT_FOUND", $"Route non registrata: {route}", null, stopwatch.Elapsed);
            NavigationPageViewModel next = _pages[route];
            if (!string.Equals(CurrentPage.Route, next.Route, StringComparison.OrdinalIgnoreCase))
            {
                _history.Push(CurrentPage.Route);
                CurrentPage = next;
                CurrentPageChanged?.Invoke(this, next);
            }
            return new(true, next.Route, null, $"Pagina aperta: {next.Title}", null, stopwatch.Elapsed);
        }
        catch (Exception exception)
        {
            StartupDiagnostics.Write("Navigation.Failed", exception, route);
            return new(false, route, "NAVIGATION_EXCEPTION", exception.Message, exception, stopwatch.Elapsed);
        }
    }

    public NavigationResult GoBack()
    {
        if (_history.Count == 0) return new(false, CurrentRoute, "NO_HISTORY", "Nessuna pagina precedente.", null, TimeSpan.Zero);
        string route = _history.Pop();
        NavigationPageViewModel page = _pages[route];
        CurrentPage = page;
        CurrentPageChanged?.Invoke(this, page);
        return new(true, route, null, $"Ritorno a {page.Title}", null, TimeSpan.Zero);
    }
}
