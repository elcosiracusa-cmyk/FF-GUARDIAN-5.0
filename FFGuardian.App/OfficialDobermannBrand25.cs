using System.Runtime.CompilerServices;

namespace FFGuardian;

/// <summary>
/// Applica l'identità visiva ufficiale FFGuardian senza ricostruire le pagine
/// e senza modificare gli eventi dei comandi di sicurezza.
/// </summary>
internal static class OfficialDobermannBrand25
{
    private static System.Windows.Forms.Timer? _startupTimer;
    private static Image? _dashboardImage;
    private static Icon? _applicationIcon;
    private static bool _started;

    [ModuleInitializer]
    internal static void Initialize() => Application.Idle += StartWhenReady;

    private static void StartWhenReady(object? sender, EventArgs e)
    {
        if (_started)
            return;

        IndependentMainForm100? form = Application.OpenForms
            .OfType<IndependentMainForm100>()
            .FirstOrDefault();
        if (form is null || form.IsDisposed || !form.IsHandleCreated)
            return;

        _started = true;
        Application.Idle -= StartWhenReady;

        form.AutoScaleMode = AutoScaleMode.Dpi;
        form.MinimumSize = new Size(1180, 720);
        ApplyWindowIcon(form);

        _startupTimer = new System.Windows.Forms.Timer { Interval = 3400 };
        _startupTimer.Tick += (_, _) =>
        {
            _startupTimer?.Stop();
            _startupTimer?.Dispose();
            _startupTimer = null;
            ApplyDashboardDobermann(form);
        };
        _startupTimer.Start();

        form.FormClosed += (_, _) => DisposeAssets();
    }

    private static void ApplyWindowIcon(Form form)
    {
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "dobermann.ico");
        if (!File.Exists(iconPath))
            return;

        try
        {
            using FileStream source = new(iconPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            _applicationIcon = new Icon(source);
            form.Icon = _applicationIcon;
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
        }
    }

    private static void ApplyDashboardDobermann(Control form)
    {
        TabControl? tabs = FindControls<TabControl>(form)
            .OrderByDescending(candidate => candidate.TabCount)
            .FirstOrDefault(candidate => candidate.TabCount > 0);
        if (tabs is null)
            return;

        TabPage? dashboard = tabs.TabPages.Cast<TabPage>()
            .FirstOrDefault(page =>
                page.Text.Contains("DASH", StringComparison.OrdinalIgnoreCase) ||
                FindControls<Control>(page).Any(control => control.Name == "CommercialDashboard18"));
        if (dashboard is null)
            return;

        Control? oldMark = FindControls<DobermannShieldControl23>(dashboard)
            .Cast<Control>()
            .FirstOrDefault();
        oldMark ??= FindControls<Label>(dashboard)
            .FirstOrDefault(label => label.Text.Trim() == "✓" && label.Font.Size >= 36F);
        if (oldMark?.Parent is not TableLayoutPanel parent)
            return;

        string imagePath = Path.Combine(AppContext.BaseDirectory, "Assets", "dobermann-dashboard.jpg");
        if (!File.Exists(imagePath))
            return;

        try
        {
            byte[] bytes = File.ReadAllBytes(imagePath);
            using MemoryStream stream = new(bytes, writable: false);
            using Image decoded = Image.FromStream(stream);
            _dashboardImage = new Bitmap(decoded);

            TableLayoutPanelCellPosition position = parent.GetPositionFromControl(oldMark);
            parent.Controls.Remove(oldMark);
            oldMark.Dispose();

            PictureBox picture = new()
            {
                Name = "OfficialDobermannDashboard25",
                Dock = DockStyle.Fill,
                Margin = Padding.Empty,
                Padding = new Padding(6),
                BackColor = Color.FromArgb(4, 8, 11),
                Image = _dashboardImage,
                SizeMode = PictureBoxSizeMode.Zoom,
                TabStop = false,
                AccessibleName = "Dobermann FFGuardian"
            };
            parent.Controls.Add(picture, position.Column, position.Row);
            picture.BringToFront();
            parent.PerformLayout();

            StabilityCoordinator82.WriteInformationLog(
                "Identità Dobermann ufficiale caricata nella Dashboard e nella finestra principale.");
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
        }
    }

    private static IEnumerable<T> FindControls<T>(Control root) where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T match)
                yield return match;
            foreach (T nested in FindControls<T>(child))
                yield return nested;
        }
    }

    private static void DisposeAssets()
    {
        _startupTimer?.Stop();
        _startupTimer?.Dispose();
        _startupTimer = null;

        _dashboardImage?.Dispose();
        _dashboardImage = null;
        _applicationIcon?.Dispose();
        _applicationIcon = null;
    }
}
