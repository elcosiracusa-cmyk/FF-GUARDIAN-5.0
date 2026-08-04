using System.Windows;

namespace FFGuardian.PremiumWpf;

public partial class App : Application
{
    private MainViewModel? _viewModel;

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
        _viewModel?.Dispose();
        _viewModel = null;
        base.OnExit(e);
    }
}
