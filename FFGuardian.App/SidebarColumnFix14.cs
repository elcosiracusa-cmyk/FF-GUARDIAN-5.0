using System.Runtime.CompilerServices;

namespace FFGuardian;

/// <summary>
/// Ricostruisce il menu laterale con una colonna reale e stabile.
/// Evita il FlowLayoutPanel che su alcuni DPI può mostrare solo la barra di scorrimento.
/// </summary>
internal static class SidebarColumnFix14
{
    private static readonly Color Background = Color.FromArgb(5, 9, 13);
    private static readonly Color Surface = Color.FromArgb(14, 22, 28);
    private static readonly Color Raised = Color.FromArgb(25, 37, 45);
    private static readonly Color Neon = Color.FromArgb(108, 255, 36);
    private static readonly Color Text = Color.FromArgb(244, 248, 250);
    private static readonly Color Border = Color.FromArgb(66, 91, 102);
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

        Panel? shell = FindControls<Panel>(form)
            .FirstOrDefault(control => control.Name == "FinalUnifiedShell12");
        TabControl? tabs = FindControls<TabControl>(form)
            .OrderByDescending(control => control.TabCount)
            .FirstOrDefault(control => control.TabCount > 0);
        TableLayoutPanel? body = shell is null
            ? null
            : FindControls<TableLayoutPanel>(shell)
                .FirstOrDefault(control => control.ColumnCount == 2 && control.RowCount == 1);

        if (shell is null || tabs is null || body is null)
            return;

        try
        {
            RebuildSidebar(form, body, tabs);
            _applied = true;
            Application.Idle -= ApplyWhenReady;
            StabilityCoordinator82.WriteInformationLog("Menu laterale verticale 14 applicato.");
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            Application.Idle -= ApplyWhenReady;
        }
    }

    private static void RebuildSidebar(Form form, TableLayoutPanel body, TabControl tabs)
    {
        body.SuspendLayout();
        try
        {
            Control? previous = body.GetControlFromPosition(0, 0);
            if (previous is not null)
            {
                body.Controls.Remove(previous);
                previous.Dispose();
            }

            body.ColumnStyles[0].SizeType = SizeType.Absolute;
            body.ColumnStyles[0].Width = form.ClientSize.Width < 1180 ? 190F : 230F;

            Panel sidebar = new()
            {
                Name = "StableSidebar14",
                Dock = DockStyle.Fill,
                BackColor = Surface,
                Padding = new Padding(10, 12, 10, 12),
                Margin = Padding.Empty,
                AutoScroll = true
            };

            TableLayoutPanel column = new()
            {
                Name = "StableSidebarColumn14",
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Surface,
                ColumnCount = 1,
                RowCount = tabs.TabCount + 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            column.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

            for (int index = 0; index < tabs.TabCount; index++)
            {
                int targetIndex = index;
                column.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));

                Button button = new()
                {
                    Name = $"StableSidebarButton14_{index}",
                    Tag = index,
                    Dock = DockStyle.Fill,
                    Height = 44,
                    Margin = new Padding(0, 0, 0, 6),
                    Padding = new Padding(14, 0, 8, 0),
                    Text = CleanTitle(tabs.TabPages[index].Text),
                    TextAlign = ContentAlignment.MiddleLeft,
                    Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                    FlatStyle = FlatStyle.Flat,
                    UseVisualStyleBackColor = false,
                    BackColor = Raised,
                    ForeColor = Text,
                    Cursor = Cursors.Hand,
                    AutoEllipsis = true,
                    TabStop = true,
                    Visible = true
                };
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.BorderColor = Border;
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(42, 62, 50);
                button.FlatAppearance.MouseDownBackColor = Color.FromArgb(55, 82, 52);
                button.Click += (_, _) => tabs.SelectedIndex = targetIndex;
                column.Controls.Add(button, 0, index);
            }

            column.RowStyles.Add(new RowStyle(SizeType.Absolute, 1F));
            sidebar.Controls.Add(column);
            body.Controls.Add(sidebar, 0, 0);
            sidebar.BringToFront();

            void RefreshSelection()
            {
                foreach (Button button in column.Controls.OfType<Button>())
                {
                    bool selected = button.Tag is int index && index == tabs.SelectedIndex;
                    button.BackColor = selected ? Neon : Raised;
                    button.ForeColor = selected ? Background : Text;
                    button.FlatAppearance.BorderColor = selected ? Neon : Border;
                }
            }

            tabs.SelectedIndexChanged += (_, _) => RefreshSelection();
            form.Resize += (_, _) =>
            {
                body.ColumnStyles[0].Width = form.ClientSize.Width < 1180 ? 190F : 230F;
                sidebar.PerformLayout();
                column.PerformLayout();
            };

            RefreshSelection();
            sidebar.PerformLayout();
            column.PerformLayout();
        }
        finally
        {
            body.ResumeLayout(true);
        }
    }

    private static string CleanTitle(string text)
    {
        string cleaned = text.Replace("&", string.Empty, StringComparison.Ordinal).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "FUNZIONE" : cleaned.ToUpperInvariant();
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
