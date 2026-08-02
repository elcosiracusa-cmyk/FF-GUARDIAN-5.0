using System.Runtime.CompilerServices;

namespace FFGuardian;

internal static class ResponsiveLayoutRefinement10
{
    private static readonly Color Background = Color.FromArgb(3, 8, 12);
    private static readonly Color Surface = Color.FromArgb(10, 20, 26);
    private static readonly Color Raised = Color.FromArgb(17, 31, 39);
    private static readonly Color Neon = Color.FromArgb(108, 255, 36);
    private static bool _applied;

    [ModuleInitializer]
    internal static void Initialize()
    {
        Application.Idle += ApplyWhenReady;
    }

    private static void ApplyWhenReady(object? sender, EventArgs e)
    {
        if (_applied)
            return;

        IndependentMainForm100? form = Application.OpenForms
            .OfType<IndependentMainForm100>()
            .FirstOrDefault();
        if (form is null || form.IsDisposed || !form.IsHandleCreated)
            return;

        Apply(form);
        _applied = true;
        Application.Idle -= ApplyWhenReady;
        StabilityCoordinator82.WriteInformationLog("Layout responsive e leggibile applicato.");
    }

    private static void Apply(IndependentMainForm100 form)
    {
        form.SuspendLayout();
        try
        {
            form.MinimumSize = new Size(1180, 760);
            if (Screen.FromControl(form).WorkingArea.Width >= 1450)
                form.Size = new Size(1420, 880);
            else
                form.WindowState = FormWindowState.Maximized;

            foreach (TabControl tabs in FindControls<TabControl>(form))
                ConfigureTabs(tabs);

            foreach (FlowLayoutPanel flow in FindControls<FlowLayoutPanel>(form))
                ConfigureCommandBar(flow);

            foreach (Button button in FindControls<Button>(form))
                ConfigureButton(button);

            foreach (DataGridView grid in FindControls<DataGridView>(form))
                ConfigureGrid(grid);

            foreach (TabPage page in FindControls<TabPage>(form))
            {
                page.Padding = new Padding(18, 16, 18, 16);
                page.BackColor = Background;
            }

            foreach (GroupBox group in FindControls<GroupBox>(form))
            {
                group.Padding = new Padding(14, 20, 14, 14);
                group.Margin = new Padding(8);
            }
        }
        finally
        {
            form.ResumeLayout(true);
        }
    }

    private static void ConfigureTabs(TabControl tabs)
    {
        tabs.Multiline = false;
        tabs.SizeMode = TabSizeMode.Normal;
        tabs.ItemSize = new Size(132, 38);
        tabs.Padding = new Point(16, 6);
        tabs.Margin = new Padding(0, 10, 0, 10);
    }

    private static void ConfigureCommandBar(FlowLayoutPanel flow)
    {
        bool containsButtons = flow.Controls.OfType<Button>().Any();
        if (!containsButtons)
            return;

        flow.AutoScroll = true;
        flow.WrapContents = false;
        flow.FlowDirection = FlowDirection.LeftToRight;
        flow.Padding = new Padding(14, 12, 14, 12);
        flow.Margin = new Padding(0, 10, 0, 14);
        flow.BackColor = Surface;
        flow.MinimumSize = new Size(0, 68);
        flow.AutoSize = false;
        if (flow.Height < 68)
            flow.Height = 68;
    }

    private static void ConfigureButton(Button button)
    {
        button.AutoSize = false;
        button.Height = Math.Max(button.Height, 44);
        button.Width = Math.Clamp(button.Width, 170, 235);
        button.Margin = new Padding(7, 6, 7, 6);
        button.Padding = new Padding(12, 0, 12, 0);
        button.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.UseCompatibleTextRendering = true;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Neon;
        button.FlatAppearance.BorderSize = 1;
        button.BackColor = Raised;
        button.ForeColor = Color.White;
    }

    private static void ConfigureGrid(DataGridView grid)
    {
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
        grid.RowTemplate.Height = 36;
        grid.ColumnHeadersHeight = 42;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.DefaultCellStyle.Padding = new Padding(8, 4, 8, 4);
        grid.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        grid.BackgroundColor = Background;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.GridColor = Color.FromArgb(47, 69, 80);
        grid.ScrollBars = ScrollBars.Both;

        if (grid.Columns.Contains("Path"))
            grid.Columns["Path"].FillWeight = 170;
        if (grid.Columns.Contains("SHA256"))
            grid.Columns["SHA256"].FillWeight = 135;
        if (grid.Columns.Contains("Evidence"))
            grid.Columns["Evidence"].FillWeight = 160;
    }

    private static IEnumerable<T> FindControls<T>(Control root) where T : Control
    {
        if (root is T match)
            yield return match;

        foreach (Control child in root.Controls)
        {
            foreach (T found in FindControls<T>(child))
                yield return found;
        }
    }
}
