using System.Runtime.CompilerServices;

namespace FFGuardian;

/// <summary>
/// Rifinitura finale della shell commerciale. Elimina l'overflow orizzontale
/// che genera le barre bianche native di WinForms e mantiene ogni pagina
/// entro la larghezza utile della finestra.
/// </summary>
internal static class CommercialLayoutSanitizer18
{
    private static readonly Color Background = Color.FromArgb(5, 9, 13);
    private static readonly Color Surface = Color.FromArgb(14, 22, 28);
    private static readonly Color Raised = Color.FromArgb(24, 35, 43);
    private static readonly Color Neon = Color.FromArgb(108, 255, 36);
    private static readonly Color Text = Color.FromArgb(244, 248, 250);
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

        TabControl? tabs = FindControls<TabControl>(form)
            .OrderByDescending(control => control.TabCount)
            .FirstOrDefault(control => control.TabCount > 0);
        if (tabs is null)
            return;

        try
        {
            SanitizeAllPages(tabs);
            tabs.SelectedIndexChanged += (_, _) =>
            {
                if (tabs.SelectedTab is not null)
                    SanitizePage(tabs.SelectedTab);
            };
            form.Resize += (_, _) => SanitizeAllPages(tabs);

            _applied = true;
            Application.Idle -= ApplyWhenReady;
            StabilityCoordinator82.WriteInformationLog(
                "Overflow orizzontale e barre bianche eliminati dalla UI commerciale.");
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            Application.Idle -= ApplyWhenReady;
        }
    }

    private static void SanitizeAllPages(TabControl tabs)
    {
        foreach (TabPage page in tabs.TabPages)
            SanitizePage(page);
    }

    private static void SanitizePage(TabPage page)
    {
        page.SuspendLayout();
        try
        {
            page.BackColor = Background;
            page.ForeColor = Text;

            // Le barre bianche mostrate nelle schermate sono scrollbar native
            // generate da controlli più larghi dell'area disponibile.
            page.AutoScroll = false;
            page.AutoScrollMinSize = Size.Empty;

            int availableWidth = Math.Max(320, page.ClientSize.Width - page.Padding.Horizontal - 4);
            FitChildren(page, availableWidth);
        }
        finally
        {
            page.ResumeLayout(true);
        }
    }

    private static void FitChildren(Control parent, int availableWidth)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is HScrollBar horizontal)
            {
                horizontal.Visible = false;
                horizontal.Enabled = false;
                continue;
            }

            if (child is VScrollBar)
                continue;

            int width = Math.Max(120, availableWidth - child.Margin.Horizontal);

            if (child.Dock == DockStyle.None || child.Dock == DockStyle.Top || child.Dock == DockStyle.Bottom)
            {
                child.Left = Math.Max(parent.Padding.Left, child.Margin.Left);
                child.Width = width;
                child.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            }

            switch (child)
            {
                case DataGridView grid:
                    ConfigureGrid(grid);
                    grid.Dock = DockStyle.Fill;
                    break;

                case TableLayoutPanel table:
                    table.BackColor = table.BackColor == Color.Transparent ? Background : table.BackColor;
                    table.AutoScroll = false;
                    table.AutoScrollMinSize = Size.Empty;
                    table.Width = width;
                    break;

                case FlowLayoutPanel flow:
                    flow.BackColor = flow.BackColor == Color.Transparent ? Background : flow.BackColor;
                    flow.AutoScroll = false;
                    flow.WrapContents = true;
                    flow.Width = width;
                    break;

                case Panel panel:
                    panel.BackColor = panel.BackColor == Color.Transparent ? Surface : panel.BackColor;
                    panel.AutoScroll = false;
                    panel.AutoScrollMinSize = Size.Empty;
                    panel.Width = width;
                    break;

                case GroupBox group:
                    group.BackColor = Surface;
                    group.ForeColor = Text;
                    group.Width = width;
                    break;

                case Button button:
                    button.UseVisualStyleBackColor = false;
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderColor = Neon;
                    button.BackColor = Raised;
                    button.ForeColor = Text;
                    button.AutoEllipsis = true;
                    break;

                case Label label:
                    if (label.BackColor == Color.Transparent || IsAlmostWhite(label.BackColor))
                        label.BackColor = label.Parent?.BackColor ?? Background;
                    label.ForeColor = Text;
                    label.AutoEllipsis = true;
                    break;
            }

            int nestedWidth = Math.Max(120, child.ClientSize.Width - child.Padding.Horizontal - 2);
            FitChildren(child, nestedWidth);
        }
    }

    private static void ConfigureGrid(DataGridView grid)
    {
        grid.BackgroundColor = Background;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Raised;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Neon;
        grid.DefaultCellStyle.BackColor = Surface;
        grid.DefaultCellStyle.ForeColor = Text;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.ScrollBars = ScrollBars.Vertical;
    }

    private static bool IsAlmostWhite(Color color)
    {
        return color.A > 0 && color.R > 220 && color.G > 220 && color.B > 220;
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
}
