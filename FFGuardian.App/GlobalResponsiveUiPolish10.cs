using System.Runtime.CompilerServices;

namespace FFGuardian;

/// <summary>
/// Rifinitura globale e non invasiva dell'interfaccia WinForms.
/// Riduce l'header, rende leggibili schede e pulsanti e recupera spazio utile
/// senza modificare la logica del motore antivirus.
/// </summary>
internal static class GlobalResponsiveUiPolish10
{
    private static readonly Color Background = Color.FromArgb(3, 8, 12);
    private static readonly Color Surface = Color.FromArgb(15, 23, 29);
    private static readonly Color Raised = Color.FromArgb(27, 38, 45);
    private static readonly Color Neon = Color.FromArgb(108, 255, 36);
    private static readonly Color Text = Color.FromArgb(240, 246, 248);
    private static readonly Color Muted = Color.FromArgb(184, 199, 207);
    private static readonly Color Border = Color.FromArgb(70, 103, 113);
    private static bool _applied;

    [ModuleInitializer]
    internal static void Initialize() => Application.Idle += ApplyWhenReady;

    private static void ApplyWhenReady(object? sender, EventArgs e)
    {
        if (_applied)
            return;

        Form? form = Application.OpenForms.Cast<Form>().FirstOrDefault();
        if (form is null || form.IsDisposed || !form.IsHandleCreated)
            return;

        try
        {
            Apply(form);
            _applied = true;
            Application.Idle -= ApplyWhenReady;
            StabilityCoordinator82.WriteInformationLog("Rifinitura UI globale responsive applicata.");
        }
        catch (Exception ex)
        {
            StabilityCoordinator82.WriteStabilityLog(ex);
            Application.Idle -= ApplyWhenReady;
        }
    }

    private static void Apply(Form form)
    {
        form.MinimumSize = new Size(1100, 700);
        form.BackColor = Background;

        PolishTree(form);
        CompactHeader(form);

        form.Resize += (_, _) =>
        {
            try
            {
                CompactHeader(form);
                PolishTabs(form);
                RepairPageLayouts(form);
            }
            catch (Exception ex)
            {
                StabilityCoordinator82.WriteStabilityLog(ex);
            }
        };

        PolishTabs(form);
        RepairPageLayouts(form);
    }

    private static void PolishTree(Control root)
    {
        foreach (Control control in root.Controls)
        {
            switch (control)
            {
                case Button button:
                    PolishButton(button);
                    break;
                case TabControl tabs:
                    PolishTabControl(tabs);
                    break;
                case GroupBox group:
                    group.ForeColor = Text;
                    group.BackColor = Surface;
                    group.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
                    group.Padding = new Padding(12);
                    break;
                case Label label:
                    PolishLabel(label);
                    break;
                case CheckBox checkBox:
                    checkBox.ForeColor = Text;
                    checkBox.BackColor = Surface;
                    checkBox.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
                    checkBox.AutoSize = true;
                    break;
                case ComboBox comboBox:
                    comboBox.Font = new Font("Segoe UI", 10F, FontStyle.Regular);
                    comboBox.IntegralHeight = false;
                    comboBox.DropDownHeight = 240;
                    break;
                case Panel panel:
                    if (panel.BackColor == Color.Transparent)
                        panel.BackColor = Surface;
                    break;
            }

            PolishTree(control);
        }
    }

    private static void PolishButton(Button button)
    {
        button.UseVisualStyleBackColor = false;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 2;
        button.FlatAppearance.BorderColor = Neon;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(45, 72, 50);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(70, 110, 60);
        button.BackColor = Raised;
        button.ForeColor = Text;
        button.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        button.TextAlign = ContentAlignment.MiddleCenter;
        button.Padding = new Padding(8, 2, 8, 2);
        button.MinimumSize = new Size(138, 44);
        button.AutoEllipsis = true;

        if (button.Text.Contains("PROTEGGI", StringComparison.OrdinalIgnoreCase))
        {
            button.BackColor = Neon;
            button.ForeColor = Background;
        }
    }

    private static void PolishLabel(Label label)
    {
        if (string.IsNullOrWhiteSpace(label.Text))
            return;

        label.UseCompatibleTextRendering = false;
        label.AutoEllipsis = true;

        if (label.Text.Contains("FFGUARDIAN", StringComparison.OrdinalIgnoreCase))
        {
            label.Font = new Font("Segoe UI", 24F, FontStyle.Bold);
            label.ForeColor = Text;
            label.MaximumSize = new Size(620, 56);
        }
        else if (label.Text.Contains("ULTIMATE PROTECTION", StringComparison.OrdinalIgnoreCase) ||
                 label.Text.Contains("THREE DOBERMANN", StringComparison.OrdinalIgnoreCase))
        {
            label.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            label.ForeColor = Neon;
        }
        else if (label.Font.Size > 14F)
        {
            label.Font = new Font("Segoe UI", 14F, label.Font.Style);
            label.ForeColor = Text;
        }
    }

    private static void PolishTabControl(TabControl tabs)
    {
        tabs.Appearance = TabAppearance.Normal;
        tabs.SizeMode = TabSizeMode.Fixed;
        tabs.ItemSize = new Size(Math.Max(118, Math.Min(170, (tabs.ClientSize.Width - 24) / Math.Max(1, Math.Min(tabs.TabCount, 7)))), 42);
        tabs.Multiline = true;
        tabs.Padding = new Point(10, 6);
        tabs.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        tabs.HotTrack = true;

        foreach (TabPage page in tabs.TabPages)
        {
            page.BackColor = Background;
            page.ForeColor = Text;
            page.Padding = new Padding(12);
            page.AutoScroll = true;
        }
    }

    private static void PolishTabs(Control root)
    {
        foreach (TabControl tabs in FindControls<TabControl>(root))
            PolishTabControl(tabs);
    }

    private static void CompactHeader(Control root)
    {
        int maxHeaderHeight = Math.Clamp(root.ClientSize.Height / 6, 92, 128);

        foreach (Control control in root.Controls)
        {
            bool headerCandidate = control.Top <= 20 && control.Height >= 110 &&
                                   control is Panel or UserControl or PictureBox;

            if (headerCandidate)
            {
                control.Height = Math.Min(control.Height, maxHeaderHeight);
                control.Padding = new Padding(10, 6, 10, 6);
            }

            if (control is PictureBox picture && picture.Top < 160)
            {
                picture.SizeMode = PictureBoxSizeMode.Zoom;
                picture.MaximumSize = new Size(92, 92);
                if (picture.Width > 92 || picture.Height > 92)
                    picture.Size = new Size(92, 92);
            }

            CompactHeader(control);
        }
    }

    private static void RepairPageLayouts(Control root)
    {
        foreach (TabPage page in FindControls<TabPage>(root))
        {
            page.AutoScroll = true;
            page.Padding = new Padding(14);

            foreach (Control child in page.Controls)
            {
                if (child.Dock == DockStyle.None && child.Width < page.ClientSize.Width * 0.62)
                {
                    child.Anchor |= AnchorStyles.Left | AnchorStyles.Right;
                    int available = Math.Max(520, page.ClientSize.Width - page.Padding.Horizontal - 20);
                    child.Width = available;
                }

                if (child is Panel or GroupBox or TableLayoutPanel or FlowLayoutPanel)
                {
                    child.Margin = new Padding(8);
                    child.Padding = new Padding(Math.Max(10, child.Padding.Left), Math.Max(8, child.Padding.Top),
                        Math.Max(10, child.Padding.Right), Math.Max(8, child.Padding.Bottom));
                }
            }
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
}
