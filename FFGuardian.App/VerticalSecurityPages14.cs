using System.Runtime.CompilerServices;

namespace FFGuardian;

/// <summary>
/// Organizza in modo verticale e leggibile le pagine Recupero, Aggiornamenti
/// e Ransom Shield, conservando i controlli originali e i relativi eventi.
/// </summary>
internal static class VerticalSecurityPages14
{
    private static readonly Color Background = Color.FromArgb(5, 9, 13);
    private static readonly Color Surface = Color.FromArgb(14, 22, 28);
    private static readonly Color Raised = Color.FromArgb(24, 35, 43);
    private static readonly Color Neon = Color.FromArgb(108, 255, 36);
    private static readonly Color Text = Color.FromArgb(244, 248, 250);
    private static readonly Color Muted = Color.FromArgb(180, 195, 204);
    private static readonly Color Border = Color.FromArgb(61, 83, 93);

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
            foreach (TabPage page in tabs.TabPages)
            {
                if (IsTargetPage(page.Text))
                    ConvertToVerticalPage(page);
            }

            tabs.SelectedIndexChanged += (_, _) =>
            {
                if (tabs.SelectedTab is TabPage selected && IsTargetPage(selected.Text))
                    RefreshVerticalPage(selected);
            };

            form.Resize += (_, _) =>
            {
                foreach (TabPage page in tabs.TabPages)
                {
                    if (IsTargetPage(page.Text))
                        RefreshVerticalPage(page);
                }
            };

            _applied = true;
            Application.Idle -= ApplyWhenReady;
            StabilityCoordinator82.WriteInformationLog(
                "Pagine Recupero, Aggiornamenti e Ransom Shield organizzate verticalmente.");
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            Application.Idle -= ApplyWhenReady;
        }
    }

    private static bool IsTargetPage(string title)
    {
        return title.Contains("RECUP", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("RIPRIST", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("AGGIORN", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("UPDATE", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("RANSOM", StringComparison.OrdinalIgnoreCase);
    }

    private static void ConvertToVerticalPage(TabPage page)
    {
        if (page.Controls.OfType<Panel>().Any(panel => panel.Name == "VerticalSecurityHost14"))
            return;

        page.SuspendLayout();
        try
        {
            List<Control> originalControls = page.Controls.Cast<Control>()
                .OrderBy(control => control.Top)
                .ThenBy(control => control.Left)
                .ToList();

            page.Controls.Clear();
            page.BackColor = Background;
            page.ForeColor = Text;
            page.Padding = new Padding(12);
            page.AutoScroll = false;

            Panel host = new()
            {
                Name = "VerticalSecurityHost14",
                Dock = DockStyle.Fill,
                BackColor = Background,
                Padding = new Padding(8),
                AutoScroll = true
            };

            FlowLayoutPanel column = new()
            {
                Name = "VerticalSecurityColumn14",
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = Background,
                Padding = new Padding(0),
                Margin = Padding.Empty,
                Location = Point.Empty,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            Label heading = new()
            {
                Name = "VerticalSecurityHeading14",
                Height = 58,
                Margin = new Padding(0, 0, 0, 10),
                BackColor = Surface,
                ForeColor = Text,
                Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                Text = CleanHeading(page.Text),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(18, 0, 12, 0),
                AutoEllipsis = true
            };
            column.Controls.Add(heading);

            foreach (Control control in originalControls)
            {
                PrepareControl(control);
                column.Controls.Add(control);
            }

            if (originalControls.Count == 0)
            {
                column.Controls.Add(CreateInformationCard(
                    "SEZIONE PRONTA",
                    "Questa funzione non contiene ancora controlli visibili. Il motore resta operativo e la pagina è pronta per i componenti futuri."));
            }

            host.Controls.Add(column);
            page.Controls.Add(host);
            ResizeColumn(host, column);
            host.Resize += (_, _) => ResizeColumn(host, column);
        }
        finally
        {
            page.ResumeLayout(true);
        }
    }

    private static void PrepareControl(Control control)
    {
        control.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        control.Dock = DockStyle.None;
        control.Margin = new Padding(0, 0, 0, 10);

        switch (control)
        {
            case DataGridView grid:
                ConfigureGrid(grid);
                grid.Height = Math.Max(280, grid.Height);
                break;

            case ListView listView:
                listView.BackColor = Surface;
                listView.ForeColor = Text;
                listView.BorderStyle = BorderStyle.FixedSingle;
                listView.Height = Math.Max(260, listView.Height);
                break;

            case TextBox textBox when textBox.Multiline:
                textBox.BackColor = Surface;
                textBox.ForeColor = Text;
                textBox.BorderStyle = BorderStyle.FixedSingle;
                textBox.Height = Math.Max(150, textBox.Height);
                break;

            case RichTextBox richTextBox:
                richTextBox.BackColor = Surface;
                richTextBox.ForeColor = Text;
                richTextBox.BorderStyle = BorderStyle.FixedSingle;
                richTextBox.Height = Math.Max(180, richTextBox.Height);
                break;

            case GroupBox group:
                group.BackColor = Surface;
                group.ForeColor = Text;
                group.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
                group.Padding = new Padding(14);
                group.Height = Math.Max(120, group.Height);
                PolishTree(group);
                break;

            case Panel panel:
                if (panel.BackColor == Color.Transparent || panel.BackColor == Background)
                    panel.BackColor = Surface;
                panel.Padding = new Padding(
                    Math.Max(12, panel.Padding.Left),
                    Math.Max(10, panel.Padding.Top),
                    Math.Max(12, panel.Padding.Right),
                    Math.Max(10, panel.Padding.Bottom));
                panel.Height = Math.Max(84, panel.Height);
                PolishTree(panel);
                break;

            case TableLayoutPanel table:
                table.BackColor = Surface;
                table.Padding = new Padding(10);
                table.Height = Math.Max(120, table.Height);
                PolishTree(table);
                break;

            case FlowLayoutPanel flow:
                flow.BackColor = Surface;
                flow.Padding = new Padding(10);
                flow.WrapContents = true;
                flow.AutoScroll = true;
                flow.Height = Math.Max(90, flow.Height);
                PolishTree(flow);
                break;

            case Button button:
                PolishButton(button);
                button.Height = Math.Max(44, button.Height);
                break;

            case Label label:
                label.ForeColor = label.ForeColor == Neon ? Neon : Text;
                label.BackColor = Surface;
                label.AutoEllipsis = true;
                label.Padding = new Padding(12, 6, 12, 6);
                label.Height = Math.Max(44, label.Height);
                break;

            default:
                control.BackColor = control.BackColor == Color.Transparent ? Surface : control.BackColor;
                control.ForeColor = Text;
                control.Height = Math.Max(44, control.Height);
                PolishTree(control);
                break;
        }
    }

    private static void PolishTree(Control root)
    {
        foreach (Control child in root.Controls)
        {
            switch (child)
            {
                case Button button:
                    PolishButton(button);
                    break;
                case Label label:
                    label.ForeColor = label.ForeColor == Neon ? Neon : Text;
                    label.AutoEllipsis = true;
                    break;
                case CheckBox checkBox:
                    checkBox.ForeColor = Text;
                    checkBox.BackColor = root.BackColor;
                    checkBox.AutoSize = true;
                    break;
                case RadioButton radioButton:
                    radioButton.ForeColor = Text;
                    radioButton.BackColor = root.BackColor;
                    radioButton.AutoSize = true;
                    break;
                case ComboBox comboBox:
                    comboBox.Font = new Font("Segoe UI", 10F);
                    break;
                case DataGridView grid:
                    ConfigureGrid(grid);
                    break;
            }

            PolishTree(child);
        }
    }

    private static void PolishButton(Button button)
    {
        button.UseVisualStyleBackColor = false;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Neon;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(42, 62, 50);
        button.BackColor = Raised;
        button.ForeColor = Text;
        button.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.AutoEllipsis = true;
        button.MinimumSize = new Size(150, 44);
    }

    private static void ConfigureGrid(DataGridView grid)
    {
        grid.BackgroundColor = Background;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.GridColor = Border;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Raised;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Neon;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        grid.ColumnHeadersHeight = 38;
        grid.DefaultCellStyle.BackColor = Surface;
        grid.DefaultCellStyle.ForeColor = Text;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(50, 80, 55);
        grid.DefaultCellStyle.SelectionForeColor = Text;
        grid.RowTemplate.Height = 30;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.AllowUserToResizeColumns = true;
        grid.AllowUserToOrderColumns = true;
    }

    private static Panel CreateInformationCard(string title, string detail)
    {
        Panel card = new()
        {
            Height = 130,
            BackColor = Surface,
            Padding = new Padding(18),
            Margin = new Padding(0, 0, 0, 10)
        };
        card.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 10F),
            Text = detail,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        });
        card.Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            Height = 34,
            BackColor = Surface,
            ForeColor = Neon,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            Text = title,
            TextAlign = ContentAlignment.MiddleLeft
        });
        return card;
    }

    private static void RefreshVerticalPage(TabPage page)
    {
        Panel? host = page.Controls.OfType<Panel>()
            .FirstOrDefault(panel => panel.Name == "VerticalSecurityHost14");
        FlowLayoutPanel? column = host?.Controls.OfType<FlowLayoutPanel>()
            .FirstOrDefault(flow => flow.Name == "VerticalSecurityColumn14");
        if (host is not null && column is not null)
            ResizeColumn(host, column);
    }

    private static void ResizeColumn(Panel host, FlowLayoutPanel column)
    {
        int available = Math.Max(620, host.ClientSize.Width - host.Padding.Horizontal -
            (host.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0));
        column.Width = available;

        foreach (Control child in column.Controls)
            child.Width = Math.Max(580, available - child.Margin.Horizontal);
    }

    private static string CleanHeading(string text)
    {
        string value = text.Replace("&", string.Empty, StringComparison.Ordinal).Trim();
        return string.IsNullOrWhiteSpace(value) ? "SICUREZZA" : value.ToUpperInvariant();
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
