using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;

namespace FFGuardian;

internal static class ProfessionalCore61Ui
{
    private static readonly ConditionalWeakTable<Control, object> Styled = new();
    private static readonly object Marker = new();
    private static readonly Color Background = Color.FromArgb(5, 8, 9);
    private static readonly Color Surface = Color.FromArgb(10, 14, 15);
    private static readonly Color SurfaceHover = Color.FromArgb(20, 38, 12);
    private static readonly Color Border = Color.FromArgb(48, 66, 70);
    private static readonly Color Neon = Color.FromArgb(145, 255, 0);
    private static readonly Color NeonBright = Color.FromArgb(190, 255, 45);
    private static readonly Color TextPrimary = Color.FromArgb(244, 247, 248);
    private static readonly Color TextSecondary = Color.FromArgb(190, 199, 202);

    public static void Apply(object? sender, EventArgs e)
    {
        foreach (Form form in Application.OpenForms)
        {
            form.Text = "FF GUARDIAN 6.1 — Professional Core by EL.CO";
            form.BackColor = Background;
            form.MinimumSize = new Size(1180, 720);
            StyleTree(form);
        }
    }

    private static void StyleTree(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (!Styled.TryGetValue(control, out _))
            {
                switch (control)
                {
                    case Button button:
                        StyleButton(button);
                        break;
                    case Label label:
                        StyleLabel(label);
                        break;
                    case FlowLayoutPanel flow:
                        StyleFlow(flow);
                        break;
                    case Panel panel:
                        StylePanel(panel);
                        break;
                    case DataGridView grid:
                        StyleGrid(grid);
                        break;
                }

                Styled.Add(control, Marker);
            }

            if (control.HasChildren)
                StyleTree(control);
        }
    }

    private static void StyleFlow(FlowLayoutPanel flow)
    {
        flow.BackColor = Color.Transparent;
        flow.Padding = new Padding(Math.Max(flow.Padding.Left, 8), Math.Max(flow.Padding.Top, 8), Math.Max(flow.Padding.Right, 8), Math.Max(flow.Padding.Bottom, 8));
    }

    private static void StyleButton(Button button)
    {
        bool navigation = button.Parent is FlowLayoutPanel flow &&
            flow.Controls.OfType<Button>().Any(b => b.Text.Contains("Dashboard", StringComparison.OrdinalIgnoreCase));

        Color normalBack = navigation ? Color.FromArgb(8, 13, 14) : Color.FromArgb(13, 20, 18);
        Color normalBorder = navigation ? Color.FromArgb(35, 65, 48) : Color.FromArgb(75, 115, 35);

        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = normalBorder;
        button.BackColor = normalBack;
        button.ForeColor = TextPrimary;
        button.Cursor = Cursors.Hand;
        button.UseCompatibleTextRendering = true;
        button.Font = new Font("Segoe UI Semibold", navigation ? 10.2f : 10.5f, FontStyle.Bold);
        button.Height = Math.Max(button.Height, navigation ? 45 : 48);
        button.Padding = navigation ? new Padding(14, 0, 10, 0) : new Padding(10, 0, 10, 0);

        button.MouseEnter += (_, _) =>
        {
            button.BackColor = SurfaceHover;
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = NeonBright;
            button.FlatAppearance.BorderSize = 2;
            button.Invalidate();
        };

        button.MouseLeave += (_, _) =>
        {
            button.BackColor = normalBack;
            button.ForeColor = TextPrimary;
            button.FlatAppearance.BorderColor = normalBorder;
            button.FlatAppearance.BorderSize = 1;
            button.Invalidate();
        };

        button.Paint += (_, e) =>
        {
            if (!button.ClientRectangle.Contains(button.PointToClient(Cursor.Position))) return;
            Rectangle rectangle = button.ClientRectangle;
            rectangle.Inflate(-2, -2);
            using Pen glow = new(Neon, 2);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawRectangle(glow, rectangle);
        };
    }

    private static void StyleLabel(Label label)
    {
        if (label.Font.Size >= 18)
        {
            label.Font = new Font("Segoe UI Semibold", Math.Max(20f, label.Font.Size), FontStyle.Bold);
            label.ForeColor = TextPrimary;
            label.Height = Math.Max(label.Height, 48);
            return;
        }

        if (label.Font.Bold || label.Dock == DockStyle.Top)
        {
            label.Font = new Font("Segoe UI Semibold", Math.Max(10.5f, label.Font.Size), FontStyle.Bold);
            label.ForeColor = TextPrimary;
        }
        else
        {
            label.Font = new Font("Segoe UI", Math.Max(10.2f, label.Font.Size), FontStyle.Regular);
            if (label.ForeColor.GetBrightness() < 0.45f)
                label.ForeColor = TextSecondary;
        }

        label.UseCompatibleTextRendering = true;
        label.AutoEllipsis = false;
    }

    private static void StylePanel(Panel panel)
    {
        if (panel.Dock == DockStyle.Top && panel.Height is >= 60 and <= 130)
        {
            panel.Height = Math.Max(panel.Height, 92);
            panel.BackColor = Color.FromArgb(6, 10, 11);
            panel.Padding = new Padding(Math.Max(panel.Padding.Left, 18), 10, Math.Max(panel.Padding.Right, 18), 10);
            return;
        }

        bool card = panel.Controls.OfType<Label>().Any() || panel.Controls.OfType<Button>().Any();
        if (!card) return;

        panel.BackColor = Surface;
        panel.Padding = new Padding(Math.Max(panel.Padding.Left, 14));
        panel.Paint += (_, e) =>
        {
            Rectangle rectangle = panel.ClientRectangle;
            rectangle.Inflate(-1, -1);
            using Pen pen = new(Border, 1);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.DrawRectangle(pen, rectangle);
        };

        Label? title = panel.Controls.OfType<Label>().FirstOrDefault(l => l.Dock == DockStyle.Top);
        Label? body = panel.Controls.OfType<Label>().FirstOrDefault(l => l.Dock == DockStyle.Fill);
        Button? action = panel.Controls.OfType<Button>().FirstOrDefault();

        if (title is not null)
        {
            title.Height = Math.Max(title.Height, 40);
            title.Padding = new Padding(8, 8, 8, 4);
            title.ForeColor = TextPrimary;
            title.Font = new Font("Segoe UI Semibold", 10.8f, FontStyle.Bold);
            title.BringToFront();
        }

        if (body is not null)
        {
            body.Padding = new Padding(10, 14, 10, action is null ? 12 : 64);
            body.ForeColor = body.ForeColor == Neon ? NeonBright : TextSecondary;
            body.Font = new Font("Segoe UI", 10.5f, FontStyle.Regular);
            body.SendToBack();
        }

        if (action is not null)
        {
            action.Dock = DockStyle.Bottom;
            action.Height = Math.Max(action.Height, 48);
            action.BringToFront();
        }
    }

    private static void StyleGrid(DataGridView grid)
    {
        grid.BackgroundColor = Background;
        grid.BorderStyle = BorderStyle.None;
        grid.RowHeadersVisible = false;
        grid.RowTemplate.Height = 38;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersHeight = 42;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(15, 28, 22);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = TextPrimary;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold);
        grid.DefaultCellStyle.BackColor = Surface;
        grid.DefaultCellStyle.ForeColor = TextSecondary;
        grid.DefaultCellStyle.SelectionBackColor = SurfaceHover;
        grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.DefaultCellStyle.Font = new Font("Segoe UI", 10f);
        grid.GridColor = Border;
    }
}
