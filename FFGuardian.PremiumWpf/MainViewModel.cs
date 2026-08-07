using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FFGuardian.Security.Core;

namespace FFGuardian.PremiumWpf;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly SecurityStatusService _statusService;
    private readonly NetworkStatusService _networkStatusService;
    private readonly INavigationService _navigation;
    private readonly IScanService _scanService;
    private readonly IScanTargetSelector _scanTargetSelector;
    private CancellationTokenSource? _refreshCts;
    private CancellationTokenSource? _scanCts;
    private int _securityScore;
    private string _protectionText = "Verifica in corso";
    private string _protectionDetail = "Controllo dei componenti reali…";
    private string _lastScan = "--";
    private string _lastUpdate = "--";
    private string _engineVersion = "--";
    private string _databaseVersion = "--";
    private string _lastAction = "Pronto";
    private string _networkAvailability = "Non verificata";
    private string _firewallProfiles = "Non verificati";
    private string _firewallPing = "Non verificato";
    private NavigationPageViewModel _currentPage;
    private NavigationResult? _lastNavigationResult;
    private ScanState _scanState = ScanState.Ready;
    private string _scanStatusMessage = "Pronto";
    private string _scanCurrentFile = string.Empty;
    private string _scanCurrentEngine = string.Empty;
    private int _scanProgressPercent;
    private int _scanFilesScanned;
    private int _scanFilesSkipped;
    private int _scanFilesFailed;
    private int _scanThreatsFound;
    private int _scanTotalFiles;
    private TimeSpan _scanElapsed;
    private TimeSpan? _scanEstimatedRemaining;
    private ScanResult? _lastScanResult;

    public MainViewModel(SecurityStatusService statusService, INavigationService navigation, IScanService scanService, IScanTargetSelector scanTargetSelector, NetworkStatusService? networkStatusService = null)
    {
        _statusService = statusService;
        _networkStatusService = networkStatusService ?? new NetworkStatusService();
        _navigation = navigation;
        _scanService = scanService;
        _scanTargetSelector = scanTargetSelector;
        _currentPage = navigation.CurrentPage;
        _navigation.CurrentPageChanged += OnCurrentPageChanged;
        RefreshCommand = new AsyncCommand(RefreshAsync, HandleCommandError);
        NavigateCommand = new RelayCommand(Navigate, CanNavigate);
        BackCommand = new RelayCommand(_ => GoBack());
        ActionCommand = new AsyncParameterCommand(ExecutePageActionAsync, HandleCommandError);
        QuickScanCommand = new AsyncCommand(() => RunScanAsync(ScanMode.Quick, null), HandleCommandError);
        FullScanCommand = new AsyncCommand(() => RunScanAsync(ScanMode.Full, null), HandleCommandError);
        CustomScanCommand = new AsyncCommand(RunCustomScanAsync, HandleCommandError);
        CancelScanCommand = new AsyncCommand(CancelScanAsync, HandleCommandError);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ObservableCollection<ComponentStatus> Components { get; } = [];
    public ObservableCollection<string> Activities { get; } = ["Interfaccia premium avviata", "Verifica componenti richiesta"];
    public ObservableCollection<ScanDetection> ScanDetections { get; } = [];
    public ObservableCollection<string> ScanErrors { get; } = [];
    public ICommand RefreshCommand { get; }
    public ICommand NavigateCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand ActionCommand { get; }
    public ICommand QuickScanCommand { get; }
    public ICommand FullScanCommand { get; }
    public ICommand CustomScanCommand { get; }
    public ICommand CancelScanCommand { get; }

    public int SecurityScore { get => _securityScore; private set => Set(ref _securityScore, value); }
    public string ProtectionText { get => _protectionText; private set => Set(ref _protectionText, value); }
    public string ProtectionDetail { get => _protectionDetail; private set => Set(ref _protectionDetail, value); }
    public string LastScan { get => _lastScan; private set => Set(ref _lastScan, value); }
    public string LastUpdate { get => _lastUpdate; private set => Set(ref _lastUpdate, value); }
    public string EngineVersion { get => _engineVersion; private set => Set(ref _engineVersion, value); }
    public string DatabaseVersion { get => _databaseVersion; private set => Set(ref _databaseVersion, value); }
    public string NetworkAvailability { get => _networkAvailability; private set => Set(ref _networkAvailability, value); }
    public string FirewallProfiles { get => _firewallProfiles; private set => Set(ref _firewallProfiles, value); }
    public string FirewallPing { get => _firewallPing; private set => Set(ref _firewallPing, value); }
    public string SelectedPage => CurrentPage.Title;
    public string SelectedRoute => CurrentPage.Route;
    public bool IsDashboard => CurrentPage.IsDashboard;
    public bool IsScanPage => string.Equals(SelectedRoute, "scan", StringComparison.OrdinalIgnoreCase);
    public bool IsFirewallPage => string.Equals(SelectedRoute, "firewall", StringComparison.OrdinalIgnoreCase);
    public NavigationPageViewModel CurrentPage { get => _currentPage; private set => Set(ref _currentPage, value); }
    public NavigationResult? LastNavigationResult { get => _lastNavigationResult; private set => Set(ref _lastNavigationResult, value); }
    public string LastAction { get => _lastAction; private set => Set(ref _lastAction, value); }
    public ScanState ScanState { get => _scanState; private set => Set(ref _scanState, value); }
    public string ScanStatusMessage { get => _scanStatusMessage; private set => Set(ref _scanStatusMessage, value); }
    public string ScanCurrentFile { get => _scanCurrentFile; private set => Set(ref _scanCurrentFile, value); }
    public string ScanCurrentEngine { get => _scanCurrentEngine; private set => Set(ref _scanCurrentEngine, value); }
    public int ScanProgressPercent { get => _scanProgressPercent; private set => Set(ref _scanProgressPercent, value); }
    public int ScanFilesScanned { get => _scanFilesScanned; private set => Set(ref _scanFilesScanned, value); }
    public int ScanFilesSkipped { get => _scanFilesSkipped; private set => Set(ref _scanFilesSkipped, value); }
    public int ScanFilesFailed { get => _scanFilesFailed; private set => Set(ref _scanFilesFailed, value); }
    public int ScanThreatsFound { get => _scanThreatsFound; private set => Set(ref _scanThreatsFound, value); }
    public int ScanTotalFiles { get => _scanTotalFiles; private set => Set(ref _scanTotalFiles, value); }
    public TimeSpan ScanElapsed { get => _scanElapsed; private set => Set(ref _scanElapsed, value); }
    public TimeSpan? ScanEstimatedRemaining { get => _scanEstimatedRemaining; private set => Set(ref _scanEstimatedRemaining, value); }
    public bool IsScanning => ScanState is ScanState.Enumerating or ScanState.Scanning or ScanState.Cancelling;
    public ScanResult? LastScanResult { get => _lastScanResult; private set => Set(ref _lastScanResult, value); }

    public async Task RefreshAsync()
    {
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _refreshCts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        ProtectionText = "Verifica in corso";
        ProtectionDetail = "Controllo dei componenti reali, incluso il database ClamAV…";
        try
        {
            Task<DashboardStatus> statusTask = _statusService.ReadAsync(_refreshCts.Token);
            Task<NetworkStatus> networkTask = _networkStatusService.CheckAsync(_refreshCts.Token);
            await Task.WhenAll(statusTask, networkTask);

            DashboardStatus status = await statusTask;
            NetworkStatus network = await networkTask;
            SecurityScore = status.Score;
            ProtectionText = status.ProtectionText;
            ProtectionDetail = status.ProtectionDetail;
            LastScan = status.LastScan;
            LastUpdate = status.LastUpdate;
            EngineVersion = status.EngineVersion;
            DatabaseVersion = status.DatabaseVersion;
            NetworkAvailability = network.NetworkAvailable ? "Rete disponibile" : "Rete non disponibile";
            FirewallProfiles = $"Dominio: {(network.DomainFirewallEnabled ? "ON" : "OFF")}  Privato: {(network.PrivateFirewallEnabled ? "ON" : "OFF")}  Pubblico: {(network.PublicFirewallEnabled ? "ON" : "OFF")}";
            FirewallPing = network.PingSucceeded && network.PingMilliseconds.HasValue
                ? $"{network.PingTarget} — {network.PingMilliseconds.Value} ms"
                : $"{network.PingTarget} — nessuna risposta";
            Components.Clear();
            foreach (ComponentStatus component in status.Components) Components.Add(component);
            Activities.Insert(0, $"Stato aggiornato — {DateTime.Now:HH:mm:ss}");
        }
        catch (OperationCanceledException)
        {
            ProtectionText = "Verifica non completata";
            ProtectionDetail = "Il controllo ha superato 90 secondi o è stato annullato. Controlla lo stato dei singoli motori.";
            Activities.Insert(0, "Controllo stato non completato");
        }
        catch (Exception exception)
        {
            HandleCommandError(exception);
        }
    }

    private async Task RunCustomScanAsync()
    {
        using CancellationTokenSource selectionCancellation = new();
        string? path = await _scanTargetSelector.SelectAsync(selectionCancellation.Token);
        if (string.IsNullOrWhiteSpace(path))
        {
            LastAction = "Scansione personalizzata annullata: nessun percorso selezionato.";
            return;
        }
        await RunScanAsync(ScanMode.Custom, path);
    }

    private async Task RunScanAsync(ScanMode mode, string? customPath)
    {
        if (IsScanning) return;
        _scanCts?.Dispose();
        _scanCts = new CancellationTokenSource();
        ResetScanState(mode);
        Progress<ScanProgress> progress = new(UpdateScanProgress);
        Activities.Insert(0, $"Scansione {mode} avviata");
        try
        {
            ScanResult result = mode switch
            {
                ScanMode.Quick => await _scanService.ScanQuickAsync(progress, _scanCts.Token),
                ScanMode.Full => await _scanService.ScanFullAsync(progress, _scanCts.Token),
                ScanMode.Custom when !string.IsNullOrWhiteSpace(customPath) => await _scanService.ScanCustomAsync(customPath, progress, _scanCts.Token),
                _ => throw new InvalidOperationException("Modalità di scansione non valida.")
            };
            ApplyScanResult(result);
        }
        catch (OperationCanceledException)
        {
            ScanState = ScanState.Cancelled;
            ScanStatusMessage = "Scansione annullata";
            Activities.Insert(0, "Scansione annullata");
        }
        finally
        {
            OnPropertyChanged(nameof(IsScanning));
        }
    }

    private async Task CancelScanAsync()
    {
        if (!IsScanning) return;
        ScanState = ScanState.Cancelling;
        ScanStatusMessage = "Annullamento in corso";
        _scanCts?.Cancel();
        await _scanService.CancelAsync();
        OnPropertyChanged(nameof(IsScanning));
    }

    private void ResetScanState(ScanMode mode)
    {
        ScanDetections.Clear();
        ScanErrors.Clear();
        LastScanResult = null;
        ScanState = ScanState.Enumerating;
        ScanStatusMessage = $"Preparazione scansione {mode}";
        ScanCurrentFile = string.Empty;
        ScanCurrentEngine = string.Empty;
        ScanProgressPercent = 0;
        ScanFilesScanned = 0;
        ScanFilesSkipped = 0;
        ScanFilesFailed = 0;
        ScanThreatsFound = 0;
        ScanTotalFiles = 0;
        ScanElapsed = TimeSpan.Zero;
        ScanEstimatedRemaining = null;
        OnPropertyChanged(nameof(IsScanning));
    }

    private void UpdateScanProgress(ScanProgress progress)
    {
        ScanState = ScanState.Scanning;
        ScanStatusMessage = progress.TotalFiles > 0 ? "Scansione in corso" : "Enumerazione dei file";
        ScanCurrentFile = progress.CurrentPath;
        ScanCurrentEngine = progress.Engine;
        ScanProgressPercent = progress.Percentage;
        ScanFilesScanned = progress.FilesScanned;
        ScanFilesSkipped = progress.FilesSkipped;
        ScanTotalFiles = progress.TotalFiles;
        ScanElapsed = progress.Elapsed;
        ScanEstimatedRemaining = progress.EstimatedRemaining;
        OnPropertyChanged(nameof(IsScanning));
    }

    private void ApplyScanResult(ScanResult result)
    {
        LastScanResult = result;
        ScanState = result.WasCancelled ? ScanState.Cancelled : ScanState.Completed;
        ScanStatusMessage = result.WasCancelled ? "Scansione annullata" : "Scansione completata";
        ScanFilesScanned = result.FilesScanned;
        ScanFilesSkipped = result.FilesSkipped;
        ScanFilesFailed = result.FilesFailed;
        ScanThreatsFound = result.Detections.Count;
        ScanElapsed = result.EndTime - result.StartTime;
        ScanEstimatedRemaining = TimeSpan.Zero;
        ScanProgressPercent = result.WasCancelled ? ScanProgressPercent : 100;
        ScanDetections.Clear();
        foreach (ScanDetection detection in result.Detections) ScanDetections.Add(detection);
        ScanErrors.Clear();
        foreach (string error in result.Errors) ScanErrors.Add(error);
        LastScan = result.EndTime.LocalDateTime.ToString("g", CultureInfo.CurrentCulture);
        LastAction = $"Scansione: {result.FilesScanned} analizzati, {result.FilesSkipped} esclusi, {result.FilesFailed} errori, {result.Detections.Count} minacce.";
        Activities.Insert(0, LastAction);
        OnPropertyChanged(nameof(IsScanning));
    }

    private void Navigate(object? parameter)
    {
        string route = parameter as string ?? string.Empty;
        LastNavigationResult = _navigation.Navigate(route);
        LastAction = LastNavigationResult.Message;
    }

    private bool CanNavigate(object? parameter) => parameter is string route && _navigation.CanNavigate(route);

    private async Task ExecutePageActionAsync(object? parameter)
    {
        string action = parameter as string ?? CurrentPage.ActionId;
        LastAction = $"Esecuzione: {CurrentPage.ActionLabel}";
        if (string.Equals(action, "quick-scan", StringComparison.OrdinalIgnoreCase))
        {
            _navigation.Navigate("scan");
            await RunScanAsync(ScanMode.Quick, null);
            return;
        }
        if (string.Equals(action, "full-scan", StringComparison.OrdinalIgnoreCase))
        {
            _navigation.Navigate("scan");
            await RunScanAsync(ScanMode.Full, null);
            return;
        }
        if (string.Equals(action, "custom-scan", StringComparison.OrdinalIgnoreCase))
        {
            _navigation.Navigate("scan");
            await RunCustomScanAsync();
            return;
        }
        if (string.Equals(action, "refresh", StringComparison.OrdinalIgnoreCase) || string.Equals(action, "verify", StringComparison.OrdinalIgnoreCase))
        {
            await RefreshAsync();
            LastAction = "Stato dei componenti aggiornato.";
            return;
        }
        LastAction = $"{CurrentPage.ActionLabel}: nessuna operazione runtime implementata.";
    }

    private void GoBack()
    {
        LastNavigationResult = _navigation.GoBack();
        LastAction = LastNavigationResult.Message;
    }

    private void OnCurrentPageChanged(object? sender, NavigationPageViewModel page)
    {
        CurrentPage = page;
        OnPropertyChanged(nameof(SelectedPage));
        OnPropertyChanged(nameof(SelectedRoute));
        OnPropertyChanged(nameof(IsDashboard));
        OnPropertyChanged(nameof(IsScanPage));
        OnPropertyChanged(nameof(IsFirewallPage));
        LastAction = $"Pagina aperta: {page.Title}";
    }

    private void HandleCommandError(Exception exception)
    {
        LastAction = $"Errore: {exception.Message}";
        Activities.Insert(0, LastAction);
    }

    private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new(propertyName));

    public void Dispose()
    {
        _navigation.CurrentPageChanged -= OnCurrentPageChanged;
        _refreshCts?.Cancel();
        _refreshCts?.Dispose();
        _scanCts?.Cancel();
        _scanCts?.Dispose();
    }
}