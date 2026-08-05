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
            new("ransom-shield", "Ransom Shield", "Controllo della protezione comportamentale contro modifiche massive dei file.", "Verifica Ransom Shield"),
            new("firewall", "Firewall", "Stato e gestione del firewall di Windows.", "Verifica firewall"),
            new("usb-shield", "USB Shield", "Controllo dei dispositivi rimovibili e delle scansioni USB.", "Verifica USB Shield"),
            new("quarantine", "Quarantena", "Elementi isolati, verifica hash, ripristino ed eliminazione sicura.", "Aggiorna quarantena"),
            new("ai-analysis", "Analisi AI", "Analisi locale spiegabile basata su evidenze. Il modello ONNX non viene usato finché hash e versione non sono verificati.", "Seleziona file da analizzare"),
            new("security-center", "Security Center", "Controlli reali di salute, integrità e disponibilità dei motori.", "Esegui controllo completo"),
            new("updates", "Aggiornamenti", "Versione dei motori, database firme e stato degli aggiornamenti.", "Controlla aggiornamenti"),
            new("audit", "Report e Audit", "Cronologia operazioni, report diagnostici e risultati dei controlli.", "Aggiorna report"),
            new("pc-health", "Salute PC", "Informazioni sul sistema e controlli disponibili senza valori simulati.", "Aggiorna diagnostica"),
            new("tools", "Strumenti", "Strumenti di sicurezza disponibili e relativo stato operativo.", "Verifica strumenti"),
            new("recovery", "Recupero file", "Ripristino controllato dei file e verifica della destinazione.", "Apri recupero"),
            new("activity", "Attività", "Eventi recenti, controlli e operazioni registrate.", "Aggiorna attività"),
            new("settings", "Impostazioni", "Configurazione dell'applicazione e dei percorsi scrivibili.", "Verifica configurazione"),
            new("support", "Assistenza", "Informazioni diagnostiche e canali di supporto.", "Apri diagnostica")
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
            if (!string.Equals(CurrentPage.Route, next.Route, StringComparison.OrdinalIgnoreCase)) { _history.Push(CurrentPage.Route); CurrentPage = next; CurrentPageChanged?.Invoke(this, next); }
            return new(true, next.Route, null, $"Pagina aperta: {next.Title}", null, stopwatch.Elapsed);
        }
        catch (Exception exception) { StartupDiagnostics.Write("Navigation.Failed", exception, route); return new(false, route, "NAVIGATION_EXCEPTION", exception.Message, exception, stopwatch.Elapsed); }
    }
    public NavigationResult GoBack()
    {
        if (_history.Count == 0) return new(false, CurrentRoute, "NO_HISTORY", "Nessuna pagina precedente.", null, TimeSpan.Zero);
        string route = _history.Pop(); NavigationPageViewModel page = _pages[route]; CurrentPage = page; CurrentPageChanged?.Invoke(this, page); return new(true, route, null, $"Ritorno a {page.Title}", null, TimeSpan.Zero);
    }
}
