using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace FFGuardian.PremiumWpf;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly SecurityStatusService _statusService;
    private readonly INavigationService _navigation;
    private CancellationTokenSource? _refreshCts;
    private int _securityScore;
    private string _protectionText = "Verifica in corso";
    private string _protectionDetail = "Controllo dei componenti reali…";
    private string _lastScan = "--";
    private string _lastUpdate = "--";
    private string _engineVersion = "--";
    private string _databaseVersion = "--";
    private string _lastAction = "Pronto";
    private NavigationPageViewModel _currentPage;
    private NavigationResult? _lastNavigationResult;

    public MainViewModel(SecurityStatusService statusService, INavigationService navigation)
    {
        _statusService = statusService;
        _navigation = navigation;
        _currentPage = navigation.CurrentPage;
        _navigation.CurrentPageChanged += OnCurrentPageChanged;
        RefreshCommand = new AsyncCommand(RefreshAsync, HandleCommandError);
        NavigateCommand = new RelayCommand(Navigate, CanNavigate);
        BackCommand = new RelayCommand(_ => GoBack());
        ActionCommand = new RelayCommand(ExecutePageAction);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<ComponentStatus> Components { get; } = [];
    public ObservableCollection<string> Activities { get; } = ["Interfaccia premium avviata", "Verifica componenti richiesta"];
    public ICommand RefreshCommand { get; }
    public ICommand NavigateCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand ActionCommand { get; }

    public int SecurityScore { get => _securityScore; private set => Set(ref _securityScore, value); }
    public string ProtectionText { get => _protectionText; private set => Set(ref _protectionText, value); }
    public string ProtectionDetail { get => _protectionDetail; private set => Set(ref _protectionDetail, value); }
    public string LastScan { get => _lastScan; private set => Set(ref _lastScan, value); }
    public string LastUpdate { get => _lastUpdate; private set => Set(ref _lastUpdate, value); }
    public string EngineVersion { get => _engineVersion; private set => Set(ref _engineVersion, value); }
    public string DatabaseVersion { get => _databaseVersion; private set => Set(ref _databaseVersion, value); }
    public string SelectedPage => CurrentPage.Title;
    public string SelectedRoute => CurrentPage.Route;
    public bool IsDashboard => CurrentPage.IsDashboard;
    public NavigationPageViewModel CurrentPage { get => _currentPage; private set => Set(ref _currentPage, value); }
    public NavigationResult? LastNavigationResult { get => _lastNavigationResult; private set => Set(ref _lastNavigationResult, value); }
    public string LastAction { get => _lastAction; private set => Set(ref _lastAction, value); }

    public async Task RefreshAsync()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        try
        {
            DashboardStatus status = await _statusService.ReadAsync(_refreshCts.Token);
            SecurityScore = status.Score;
            ProtectionText = status.ProtectionText;
            ProtectionDetail = status.ProtectionDetail;
            LastScan = status.LastScan;
            LastUpdate = status.LastUpdate;
            EngineVersion = status.EngineVersion;
            DatabaseVersion = status.DatabaseVersion;
            Components.Clear();
            foreach (ComponentStatus component in status.Components) Components.Add(component);
            Activities.Insert(0, $"Stato aggiornato — {DateTime.Now:HH:mm:ss}");
        }
        catch (OperationCanceledException)
        {
            ProtectionText = "Verifica non completata";
            ProtectionDetail = "Il controllo è stato annullato o ha superato il tempo massimo.";
            Activities.Insert(0, "Controllo stato non completato");
        }
        catch (Exception exception)
        {
            HandleCommandError(exception);
        }
    }

    private bool CanNavigate(object? parameter) => parameter is string route && _navigation.CanNavigate(route);

    private void Navigate(object? parameter)
    {
        string route = parameter?.ToString() ?? string.Empty;
        NavigationResult result = _navigation.NavigateTo(route);
        LastNavigationResult = result;
        LastAction = result.Message;
        StartupDiagnostics.Write(result.Success ? "Navigation.Success" : "Navigation.Rejected", result.Exception,
            $"Route={result.Route}; DurationMs={result.Duration.TotalMilliseconds:F1}; Message={result.Message}");
        if (!result.Success) Activities.Insert(0, $"Navigazione non riuscita: {route}");
    }

    private void GoBack()
    {
        NavigationResult result = _navigation.GoBack();
        LastNavigationResult = result;
        LastAction = result.Message;
    }

    private void OnCurrentPageChanged(object? sender, NavigationPageViewModel page)
    {
        CurrentPage = page;
        OnPropertyChanged(nameof(SelectedPage));
        OnPropertyChanged(nameof(SelectedRoute));
        OnPropertyChanged(nameof(IsDashboard));
        Activities.Insert(0, $"Pagina aperta: {page.Title}");
    }

    private void ExecutePageAction(object? parameter)
    {
        string action = parameter?.ToString() ?? CurrentPage.PrimaryAction;
        LastAction = $"Azione richiesta: {action}";
        Activities.Insert(0, LastAction);
        if (string.Equals(action, "Aggiorna stato", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(action, "Verifica protezione", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(action, "Esegui controllo completo", StringComparison.OrdinalIgnoreCase))
        {
            RefreshCommand.Execute(null);
        }
    }

    private void HandleCommandError(Exception exception)
    {
        ProtectionText = "Attenzione richiesta";
        ProtectionDetail = "Errore durante la verifica dei componenti. Consulta il log di avvio.";
        LastAction = exception.Message;
        Activities.Insert(0, "Errore durante il controllo dei componenti");
        StartupDiagnostics.Write("ViewModel", exception);
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(name);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void Dispose()
    {
        _navigation.CurrentPageChanged -= OnCurrentPageChanged;
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = null;
    }
}

public sealed class RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => execute(parameter);
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class AsyncCommand(Func<Task> execute, Action<Exception>? onError = null) : ICommand
{
    private bool _running;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !_running;

    public async void Execute(object? parameter)
    {
        if (_running) return;
        _running = true;
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            await execute();
        }
        catch (Exception exception)
        {
            onError?.Invoke(exception);
        }
        finally
        {
            _running = false;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
