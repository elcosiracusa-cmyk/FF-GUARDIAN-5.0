using System.Runtime.CompilerServices;

namespace FFGuardian;

/// <summary>
/// Rende la navigazione principale leggibile anche su schermi 1366x768 e con ridimensionamento DPI.
/// Non usa trasparenze né coordinate assolute.
/// </summary>
internal static class NavigationTabsReadability10
{
    private static readonly Color Background = Color.FromArgb(3, 8, 12);
    private static readonly Color Inactive = Color.FromArgb(17, 31, 39);
    private static readonly Color Active = Color.FromArgb(108, 255, 36);
    private static readonly Color Border = Color.FromArgb(75, 105, 115);
    private static bool _applied;

    [ModuleInitializer]
    internal static void Initialize() => Application.Idle += ApplyWhenReady;

    private static void ApplyWhenReady(object? sender, EventArgs e)
    {
        if (_applied)
            return;

        IndependentMainForm100? form = Application.OpenForms
            .OfType<IndependentMainForm100>()
            .FirstOrDefault();
        if (form is null || form.IsDisposed || !form.IsHandleCreated)
            return;

        TabControl? tabs = FindControl<TabControl>(form);
        if (tabs is null)
            return;

        try
        {
            ConfigureTabs(tabs);
            _applied = true;
            Application.Idle -= ApplyWhenReady;
            StabilityCoordinator82.WriteInformationLog("Barra schede responsive e leggibile applicata.");
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            Application.Idle -= ApplyWhenReady;
        }
    }

    private static void ConfigureTabs(TabControl tabs)
    {
        tabs.SuspendLayout();
        try
        {
            tabs.Appearance = TabAppearance.Normal;
            tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabs.SizeMode = TabSizeMode.Fixed;
            tabs.Multiline = true;
            tabs.ItemSize = new Size(CalculateTabWidth(tabs), 42);
            tabs.Padding = new Point(18, 8);
            tabs.HotTrack = true;

            foreach (TabPage page in tabs.TabPages)
            {
                page.BackColor = Background;
                page.Padding = new Padding(10);
                page.UseVisualStyleBackColor = false;
            }

            tabs.DrawItem -= DrawTab;
            tabs.DrawItem += DrawTab;
            tabs.Resize -= TabsOnResize;
            tabs.Resize += TabsOnResize;
            tabs.SelectedIndexChanged -= TabsOnSelectedIndexChanged;
            tabs.SelectedIndexChanged += TabsOnSelectedIndexChanged;
            tabs.Invalidate();
        }
        finally
        {
            tabs.ResumeLayout(true);
        }
    }

    private static void TabsOnResize(object? sender, EventArgs e)
    {
        if (sender is not TabControl tabs || tabs.IsDisposed)
            return;

        Size target = new(CalculateTabWidth(tabs), 42);
        if (tabs.ItemSize != target)
            tabs.ItemSize = target;
        tabs.Invalidate();
    }

    private static void TabsOnSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (sender is TabControl tabs)
            tabs.Invalidate();
    }

    private static int CalculateTabWidth(TabControl tabs)
    {
        int count = Math.Max(1, tabs.TabCount);
        int available = Math.Max(600, tabs.ClientSize.Width - 24);

        // Mantiene etichette leggibili; se non entrano, WinForms crea automaticamente più righe.
        int ideal = available / Math.Min(count, 7);
        return Math.Clamp(ideal, 145, 205);
    }

    private static void DrawTab(object? sender, DrawItemEventArgs e)
    {
        if (sender is not TabControl tabs || e.Index < 0 || e.Index >= tabs.TabCount)
            return;

        Rectangle bounds = e.Bounds;
        bool selected = e.Index == tabs.SelectedIndex;
        Color fill = selected ? Active : Inactive;
        Color text = selected ? Background : Color.White;

        using SolidBrush backgroundBrush = new(fill);
        using Pen borderPen = new(selected ? Active : Border, selected ? 2F : 1F);
        e.Graphics.FillRectangle(backgroundBrush, bounds);
        e.Graphics.DrawRectangle(borderPen, bounds.X, bounds.Y, Math.Max(0, bounds.Width - 1), Math.Max(0, bounds.Height - 1));

        Rectangle textBounds = Rectangle.Inflate(bounds, -10, -4);
        TextRenderer.DrawText(
            e.Graphics,
            tabs.TabPages[e.Index].Text.ToUpperInvariant(),
            new Font("Segoe UI", 9F, FontStyle.Bold),
            textBounds,
            text,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix);

        if (selected)
        {
            using Pen underline = new(Color.White, 2F);
            e.Graphics.DrawLine(underline, bounds.Left + 8, bounds.Bottom - 3, bounds.Right - 8, bounds.Bottom - 3);
        }
    }

    private static T? FindControl<T>(Control root) where T : Control
    {
        if (root is T match)
            return match;

        foreach (Control child in root.Controls)
        {
            T? found = FindControl<T>(child);
            if (found is not null)
                return found;
        }

        return null;
    }
}
