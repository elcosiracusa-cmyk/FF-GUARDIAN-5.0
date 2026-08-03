using System.Runtime.CompilerServices;

namespace FFGuardian;

internal static class UltimateDashboardExperience10
{
    private static readonly Color Background = Color.FromArgb(3, 8, 12);
    private static readonly Color Surface = Color.FromArgb(10, 20, 26);
    private static readonly Color Raised = Color.FromArgb(17, 31, 39);
    private static readonly Color Neon = Color.FromArgb(108, 255, 36);
    private static readonly Color Muted = Color.FromArgb(174, 190, 200);
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
        TabPage? dashboard = tabs?.TabPages.Cast<TabPage>()
            .FirstOrDefault(page => page.Text.Equals("DASHBOARD", StringComparison.OrdinalIgnoreCase));
        if (dashboard is null || tabs is null)
            return;

        try
        {
            Apply(form, dashboard, tabs);
            _applied = true;
            Application.Idle -= ApplyWhenReady;
            StabilityCoordinator82.WriteInformationLog("Ultimate Dashboard Experience 10 Safe applicata.");
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            Application.Idle -= ApplyWhenReady;
        }
    }

    private static void Apply(IndependentMainForm100 form, TabPage dashboard, TabControl tabs)
    {
        dashboard.SuspendLayout();
        try
        {
            dashboard.Padding = new Padding(12);
            dashboard.AutoScroll = true;

            Panel host = BuildCommandDeck(form, tabs);
            dashboard.Controls.Add(host);
            host.BringToFront();

            form.KeyPreview = true;
            form.KeyDown += (_, key) =>
            {
                if (key.Control && key.KeyCode == Keys.Space)
                {
                    FindButton(form, "PROTEGGI ORA", "UltimateCommandDeck10")?.PerformClick();
                    key.Handled = true;
                }
                else if (key.Control && key.KeyCode == Keys.F)
                {
                    SelectTab(tabs, "SCANSIONE");
                    key.Handled = true;
                }
            };
        }
        finally
        {
            dashboard.ResumeLayout(true);
        }
    }

    private static Panel BuildCommandDeck(IndependentMainForm100 form, TabControl tabs)
    {
        Panel host = new()
        {
            Name = "UltimateCommandDeck10",
            Dock = DockStyle.Top,
            Height = 300,
            BackColor = Background,
            Padding = new Padding(10)
        };

        TableLayoutPanel card = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(20),
            ColumnCount = 2,
            RowCount = 3,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 67));
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        card.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));

        Label brand = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Neon,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Text = "FFGUARDIAN ULTIMATE  •  THREE DOBERMANN DEFENSE",
            TextAlign = ContentAlignment.MiddleLeft
        };
        card.SetColumnSpan(brand, 2);
        card.Controls.Add(brand, 0, 0);

        Panel message = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(4)
        };
        message.Controls.Add(new Label
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            BackColor = Surface,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 9.5F),
            Text = "Motore autonomo, Ransom Shield e controllo integrità.\nLe azioni sensibili richiedono conferma e rollback."
        });
        message.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 19F, FontStyle.Bold),
            Text = "IL TUO SISTEMA È SOTTO PROTEZIONE",
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        });
        card.Controls.Add(message, 0, 1);

        Panel state = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Raised,
            Padding = new Padding(16)
        };
        state.Controls.Add(new Label
        {
            Dock = DockStyle.Bottom,
            Height = 42,
            BackColor = Raised,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 8.5F),
            Text = "Monitoraggio in tempo reale\nEngine10 Definitive",
            TextAlign = ContentAlignment.BottomRight
        });
        state.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Raised,
            ForeColor = Neon,
            Font = new Font("Segoe UI", 22F, FontStyle.Bold),
            Text = "PROTETTO",
            TextAlign = ContentAlignment.MiddleRight
        });
        card.Controls.Add(state, 1, 1);

        FlowLayoutPanel commands = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(4, 10, 4, 4)
        };
        commands.Controls.Add(CreateButton("PROTEGGI ORA", true, () => InvokeExistingOrTab(form, tabs, "PROTEGGI ORA", "SCANSIONE")));
        commands.Controls.Add(CreateButton("SCANSIONE COMPLETA", false, () => InvokeExistingOrTab(form, tabs, "SCANSIONE COMPLETA", "SCANSIONE")));
        commands.Controls.Add(CreateButton("PROCESSI ATTIVI", false, () => InvokeExistingOrTab(form, tabs, "PROCESSI ATTIVI", "PROCESSI")));
        commands.Controls.Add(CreateButton("CONTROLLO AVVIO", false, () => InvokeExistingOrTab(form, tabs, "CONTROLLO AVVIO", "AUDIT")));
        commands.Controls.Add(CreateButton("QUARANTENA", false, () => InvokeExistingOrTab(form, tabs, "QUARANTENA", "RECUPERO")));
        commands.Controls.Add(CreateButton("AGGIORNA FIRME", false, () => InvokeExistingOrTab(form, tabs, "AGGIORNA FIRME", "AGGIORNAMENTI")));
        card.SetColumnSpan(commands, 2);
        card.Controls.Add(commands, 0, 2);

        host.Controls.Add(card);
        return host;
    }

    private static Button CreateButton(string text, bool primary, Action action)
    {
        Button button = new()
        {
            Width = primary ? 180 : 160,
            Height = 44,
            Margin = new Padding(5),
            Text = text,
            BackColor = primary ? Neon : Raised,
            ForeColor = primary ? Background : Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", primary ? 10F : 8.5F, FontStyle.Bold),
            AccessibleName = text,
            TabStop = true
        };
        button.FlatAppearance.BorderColor = primary ? Neon : Color.FromArgb(74, 106, 116);
        button.FlatAppearance.BorderSize = 1;
        button.Click += (_, _) => action();
        return button;
    }

    private static void InvokeExistingOrTab(Control root, TabControl tabs, string command, string tab)
    {
        Button? original = FindButton(root, command, "UltimateCommandDeck10");
        if (original is not null)
            original.PerformClick();
        else
            SelectTab(tabs, tab);
    }

    private static void SelectTab(TabControl tabs, string text)
    {
        TabPage? page = tabs.TabPages.Cast<TabPage>()
            .FirstOrDefault(candidate => candidate.Text.Contains(text, StringComparison.OrdinalIgnoreCase));
        if (page is not null)
            tabs.SelectedTab = page;
    }

    private static Button? FindButton(Control root, string text, string excludedParent)
    {
        foreach (Control control in root.Controls)
        {
            if (control is Button button &&
                button.Text.Contains(text, StringComparison.OrdinalIgnoreCase) &&
                !IsInsideNamedParent(button, excludedParent))
                return button;

            Button? nested = FindButton(control, text, excludedParent);
            if (nested is not null)
                return nested;
        }
        return null;
    }

    private static bool IsInsideNamedParent(Control control, string name)
    {
        for (Control? parent = control.Parent; parent is not null; parent = parent.Parent)
            if (parent.Name.Equals(name, StringComparison.Ordinal))
                return true;
        return false;
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
