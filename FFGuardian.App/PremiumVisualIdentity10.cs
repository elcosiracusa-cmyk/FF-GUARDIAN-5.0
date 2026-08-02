using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;

namespace FFGuardian;

internal static class PremiumVisualIdentity10
{
    private static readonly Color Background = Color.FromArgb(3, 8, 12);
    private static readonly Color Surface = Color.FromArgb(10, 20, 26);
    private static readonly Color Raised = Color.FromArgb(17, 31, 39);
    private static readonly Color Neon = Color.FromArgb(108, 255, 36);
    private static readonly Color Muted = Color.FromArgb(174, 190, 200);
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
        StabilityCoordinator82.WriteInformationLog("Identità grafica premium responsive applicata.");
    }

    private static void Apply(IndependentMainForm100 form)
    {
        form.SuspendLayout();
        try
        {
            form.Text = "FFGUARDIAN ULTIMATE PROTECTION — EL.CO";
            form.BackColor = Background;
            form.MinimumSize = new Size(1080, 690);
            form.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            form.StartPosition = FormStartPosition.CenterScreen;

            RestyleTree(form);

            Panel header = new()
            {
                Dock = DockStyle.Top,
                Height = 92,
                BackColor = Surface,
                Padding = new Padding(14, 7, 14, 7),
                Name = "PremiumHeader10"
            };

            TripleDobermannEmblem10 emblem = new()
            {
                Dock = DockStyle.Left,
                Width = 102,
                Margin = new Padding(0),
                BackColor = Surface
            };

            Panel status = new()
            {
                Dock = DockStyle.Right,
                Width = 245,
                BackColor = Raised,
                Padding = new Padding(12, 8, 12, 8),
                Name = "PremiumStatus10"
            };
            status.Controls.Add(new Label
            {
                Dock = DockStyle.Bottom,
                Height = 24,
                Text = "FFGUARDIAN 10.0.1 RC1  •  EL.CO",
                ForeColor = Color.White,
                BackColor = Raised,
                Font = new Font("Segoe UI", 8F),
                TextAlign = ContentAlignment.MiddleRight
            });
            status.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "SISTEMA PROTETTO",
                ForeColor = Neon,
                BackColor = Raised,
                Font = new Font("Segoe UI", 15F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight
            });
            status.Controls.Add(new Label
            {
                Dock = DockStyle.Top,
                Height = 21,
                Text = "STATO PROTEZIONE",
                ForeColor = Muted,
                BackColor = Raised,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight
            });

            Panel titlePanel = new()
            {
                Dock = DockStyle.Fill,
                BackColor = Surface,
                Padding = new Padding(12, 5, 8, 2)
            };
            titlePanel.Controls.Add(new Label
            {
                Dock = DockStyle.Bottom,
                Height = 21,
                Text = "Protezione autonoma  •  Ransom Shield  •  Firewall  •  USB Shield  •  Engine10",
                ForeColor = Muted,
                BackColor = Surface,
                Font = new Font("Segoe UI", 8.5F),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            });
            titlePanel.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "FFGUARDIAN",
                ForeColor = Color.White,
                BackColor = Surface,
                Font = new Font("Segoe UI", 22F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            });
            titlePanel.Controls.Add(new Label
            {
                Dock = DockStyle.Top,
                Height = 21,
                Text = "ULTIMATE PROTECTION  •  THREE DOBERMANN DEFENSE",
                ForeColor = Neon,
                BackColor = Surface,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true
            });

            header.Controls.Add(titlePanel);
            header.Controls.Add(status);
            header.Controls.Add(emblem);
            form.Controls.Add(header);
            header.BringToFront();
        }
        finally
        {
            form.ResumeLayout(performLayout: true);
        }
    }

    private static void RestyleTree(Control root)
    {
        foreach (Control control in root.Controls)
        {
            switch (control)
            {
                case TabControl tabs:
                    ConfigureTabs(tabs);
                    break;
                case TabPage page:
                    page.BackColor = Background;
                    page.ForeColor = Color.White;
                    page.Padding = new Padding(10);
                    break;
                case Button button:
                    button.BackColor = Raised;
                    button.ForeColor = Color.White;
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderColor = Neon;
                    button.FlatAppearance.BorderSize = 1;
                    button.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
                    button.Cursor = Cursors.Hand;
                    button.MinimumSize = new Size(0, 36);
                    button.Padding = new Padding(8, 0, 8, 0);
                    break;
                case GroupBox group:
                    group.BackColor = Surface;
                    group.ForeColor = Neon;
                    group.Padding = new Padding(10);
                    break;
                case Panel panel when panel.Name is not "PremiumHeader10" and not "PremiumStatus10":
                    if (panel.BackColor == SystemColors.Control || panel.BackColor == Color.Transparent)
                        panel.BackColor = Surface;
                    break;
                case FlowLayoutPanel flow:
                    flow.BackColor = Surface;
                    flow.Padding = new Padding(Math.Max(flow.Padding.Left, 8), Math.Max(flow.Padding.Top, 7),
                        Math.Max(flow.Padding.Right, 8), Math.Max(flow.Padding.Bottom, 7));
                    break;
                case TableLayoutPanel table:
                    if (table.BackColor == SystemColors.Control || table.BackColor == Color.Transparent)
                        table.BackColor = Background;
                    break;
                case DataGridView grid:
                    ConfigureGrid(grid);
                    break;
                case TextBox textBox:
                    textBox.BackColor = Raised;
                    textBox.ForeColor = Color.White;
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case RichTextBox richText:
                    richText.BackColor = Background;
                    richText.ForeColor = Color.FromArgb(220, 232, 238);
                    richText.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case ListBox listBox:
                    listBox.BackColor = Surface;
                    listBox.ForeColor = Color.White;
                    break;
                case Label label:
                    if (label.BackColor == SystemColors.Control)
                        label.BackColor = Surface;
                    break;
            }

            RestyleTree(control);
        }
    }

    private static void ConfigureTabs(TabControl tabs)
    {
        tabs.Appearance = TabAppearance.Normal;
        tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
        tabs.SizeMode = TabSizeMode.Fixed;
        tabs.Multiline = true;
        tabs.ItemSize = new Size(125, 32);
        tabs.Padding = new Point(12, 4);
        tabs.BackColor = Background;
        tabs.ForeColor = Color.White;

        tabs.DrawItem += (_, e) =>
        {
            bool selected = e.Index == tabs.SelectedIndex;
            Rectangle bounds = e.Bounds;
            bounds.Inflate(-1, -1);
            Color fill = selected ? Color.FromArgb(28, 68, 34) : Surface;
            Color border = selected ? Neon : Color.FromArgb(45, 65, 74);
            Color textColor = selected ? Neon : Color.FromArgb(210, 220, 226);

            using SolidBrush background = new(fill);
            using Pen outline = new(border, selected ? 2F : 1F);
            using SolidBrush text = new(textColor);
            using StringFormat format = new()
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            };

            e.Graphics.FillRectangle(background, bounds);
            e.Graphics.DrawRectangle(outline, bounds);
            string caption = tabs.TabPages[e.Index].Text.ToUpperInvariant();
            e.Graphics.DrawString(caption, new Font("Segoe UI", 8F, FontStyle.Bold), text, bounds, format);
        };
    }

    private static void ConfigureGrid(DataGridView grid)
    {
        grid.BackgroundColor = Background;
        grid.GridColor = Color.FromArgb(45, 65, 74);
        grid.DefaultCellStyle.BackColor = Surface;
        grid.DefaultCellStyle.ForeColor = Color.White;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(35, 88, 43);
        grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.DefaultCellStyle.Padding = new Padding(4);
        grid.ColumnHeadersDefaultCellStyle.BackColor = Raised;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Neon;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        grid.ColumnHeadersHeight = 34;
        grid.RowTemplate.Height = 30;
        grid.EnableHeadersVisualStyles = false;
        grid.BorderStyle = BorderStyle.None;
    }
}

internal sealed class TripleDobermannEmblem10 : Control
{
    private static readonly Color Neon = Color.FromArgb(108, 255, 36);
    private static readonly Color Dark = Color.FromArgb(5, 11, 15);
    private static readonly Color Surface = Color.FromArgb(10, 20, 26);
    private static readonly Color Metal = Color.FromArgb(92, 108, 116);
    private static readonly Color Tan = Color.FromArgb(178, 96, 42);

    public TripleDobermannEmblem10()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.ResizeRedraw,
            true);
        DoubleBuffered = true;
        BackColor = Surface;
        TabStop = false;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using SolidBrush brush = new(BackColor);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        RectangleF shield = new(7, 3, Math.Max(62, Width - 14), Math.Max(70, Height - 6));

        using GraphicsPath path = new();
        path.AddPolygon([
            new PointF(shield.Left + shield.Width * .12F, shield.Top + shield.Height * .10F),
            new PointF(shield.Right - shield.Width * .12F, shield.Top + shield.Height * .10F),
            new PointF(shield.Right - shield.Width * .06F, shield.Top + shield.Height * .58F),
            new PointF(shield.Left + shield.Width * .50F, shield.Bottom - 2),
            new PointF(shield.Left + shield.Width * .06F, shield.Top + shield.Height * .58F)
        ]);

        using LinearGradientBrush shieldBrush = new(shield, Color.FromArgb(23, 39, 47), Dark, 90F);
        using Pen glow = new(Neon, 2.2F);
        using Pen metal = new(Metal, 5F);
        e.Graphics.FillPath(shieldBrush, path);
        e.Graphics.DrawPath(metal, path);
        e.Graphics.DrawPath(glow, path);

        float scale = Math.Max(.72F, Math.Min(1F, shield.Width / 112F));
        DrawHead(e.Graphics, new RectangleF(shield.Left + 5, shield.Top + 22, 32 * scale, 46 * scale), -8F);
        DrawHead(e.Graphics, new RectangleF(shield.Left + shield.Width / 2F - 19 * scale, shield.Top + 13,
            38 * scale, 54 * scale), 0F);
        DrawHead(e.Graphics, new RectangleF(shield.Right - 37 * scale, shield.Top + 22,
            32 * scale, 46 * scale), 8F);

        using Font font = new("Segoe UI", 7F, FontStyle.Bold);
        using SolidBrush text = new(Neon);
        using StringFormat format = new() { Alignment = StringAlignment.Center };
        e.Graphics.DrawString("FFG", font, text,
            new RectangleF(shield.Left, shield.Bottom - 18, shield.Width, 14), format);
    }

    private static void DrawHead(Graphics graphics, RectangleF box, float rotation)
    {
        GraphicsState state = graphics.Save();
        graphics.TranslateTransform(box.Left + box.Width / 2F, box.Top + box.Height / 2F);
        graphics.RotateTransform(rotation);
        graphics.TranslateTransform(-(box.Left + box.Width / 2F), -(box.Top + box.Height / 2F));

        PointF[] ears =
        [
            new(box.Left + box.Width * .17F, box.Top + box.Height * .34F),
            new(box.Left + box.Width * .22F, box.Top),
            new(box.Left + box.Width * .43F, box.Top + box.Height * .31F),
            new(box.Left + box.Width * .57F, box.Top + box.Height * .31F),
            new(box.Left + box.Width * .78F, box.Top),
            new(box.Left + box.Width * .83F, box.Top + box.Height * .34F)
        ];
        using SolidBrush black = new(Color.FromArgb(8, 12, 15));
        using Pen outline = new(Neon, 1.2F);
        graphics.FillPolygon(black, ears);
        graphics.DrawPolygon(outline, ears);

        RectangleF face = new(box.Left + box.Width * .16F, box.Top + box.Height * .24F,
            box.Width * .68F, box.Height * .66F);
        graphics.FillEllipse(black, face);
        graphics.DrawEllipse(outline, face);

        using SolidBrush tan = new(Tan);
        float eyeSize = Math.Max(2.5F, box.Width * .09F);
        graphics.FillEllipse(tan, box.Left + box.Width * .25F, box.Top + box.Height * .48F, eyeSize * 2F, eyeSize * 1.4F);
        graphics.FillEllipse(tan, box.Right - box.Width * .25F - eyeSize * 2F, box.Top + box.Height * .48F,
            eyeSize * 2F, eyeSize * 1.4F);
        graphics.FillEllipse(tan, box.Left + box.Width * .34F, box.Top + box.Height * .69F,
            box.Width * .32F, Math.Max(6F, box.Height * .14F));

        using SolidBrush eye = new(Neon);
        graphics.FillEllipse(eye, box.Left + box.Width * .31F, box.Top + box.Height * .53F, eyeSize, eyeSize);
        graphics.FillEllipse(eye, box.Right - box.Width * .31F - eyeSize, box.Top + box.Height * .53F, eyeSize, eyeSize);
        graphics.Restore(state);
    }
}
