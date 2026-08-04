using System.Windows;

namespace FFGuardian.PremiumWpf;

public partial class App : Application, IDisposable
{
    private MainViewModel? _viewModel;
    private bool _disposed;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        SecurityStatusService statusService = new();
        _viewModel = new MainViewModel(statusService);
        MainWindow window = new(_viewModel);
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Dispose();
        base.OnExit(e);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _viewModel?.Dispose();
        _viewModel = null;
        GC.SuppressFinalize(this);
    }
}
