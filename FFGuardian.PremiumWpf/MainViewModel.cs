using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace FFGuardian.PremiumWpf;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly SecurityStatusService _statusService;
    private CancellationTokenSource? _refreshCts;
    private int _securityScore;
    private string _protectionText = "Verifica in corso";
    private string _protectionDetail = "Controllo dei componenti reali…";
    private string _lastScan = "--";
    private string _lastUpdate = "--";
    private string _engineVersion = "--";
    private string _databaseVersion = "--";
    private string _selectedPage = "Dashboard";

    public MainViewModel(SecurityStatusService statusService)
    {
        _statusService = statusService;
        RefreshCommand = new AsyncCommand(RefreshAsync);
        NavigateCommand = new RelayCommand(value => SelectedPage = value?.ToString() ?? "Dashboard");
        ActionCommand = new RelayCommand(value => LastAction = $"Azione richiesta: {value}. Collegamento al motore legacy da completare.");
        _ = RefreshAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<ComponentStatus> Components { get; } = [];
    public ObservableCollection<string> Activities { get; } = ["Interfaccia premium avviata", "Verifica componenti richiesta"];
    public ICommand RefreshCommand { get; }
    public ICommand NavigateCommand { get; }
    public ICommand ActionCommand { get; }

    public int SecurityScore { get => _securityScore; private set => Set(ref _securityScore, value); }
    public string ProtectionText { get => _protectionText; private set => Set(ref _protectionText, value); }
    public string ProtectionDetail { get => _protectionDetail; private set => Set(ref _protectionDetail, value); }
    public string LastScan { get => _lastScan; private set => Set(ref _lastScan, value); }
    public string LastUpdate { get => _lastUpdate; private set => Set(ref _lastUpdate, value); }
    public string EngineVersion { get => _engineVersion; private set => Set(ref _engineVersion, value); }
    public string DatabaseVersion { get => _databaseVersion; private set => Set(ref _databaseVersion, value); }
    public string SelectedPage { get => _selectedPage; set => Set(ref _selectedPage, value); }
    public string LastAction { get; private set; } = "Pronto";

    private async Task RefreshAsync()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
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

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public void Dispose()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = null;
    }
}

public sealed class RelayCommand(Action<object?> execute) : ICommand
{
    public event EventHandler? CanExecuteChanged { add { } remove { } }
    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => execute(parameter);
}

public sealed class AsyncCommand(Func<Task> execute) : ICommand
{
    private bool _running;
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => !_running;
    public async void Execute(object? parameter)
    {
        if (_running) return;
        _running = true; CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        try { await execute(); }
        finally { _running = false; CanExecuteChanged?.Invoke(this, EventArgs.Empty); }
    }
}
