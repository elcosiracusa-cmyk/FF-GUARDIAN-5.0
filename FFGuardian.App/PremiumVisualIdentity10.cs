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
        StabilityCoordinator82.WriteInformationLog("Identità grafica premium con stemma dei tre Dobermann applicata.");
    }

    private static void Apply(IndependentMainForm100 form)
    {
        form.Text = "FFGUARDIAN ULTIMATE PROTECTION — EL.CO";
        form.BackColor = Background;
        form.MinimumSize = new Size(1220, 760);
        form.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);

        RestyleTree(form);

        Panel header = new()
        {
            Dock = DockStyle.Top,
            Height = 118,
            BackColor = Surface,
            Padding = new Padding(18, 10, 18, 10),
            Name = "PremiumHeader10"
        };

        TripleDobermannEmblem10 emblem = new()
        {
            Dock = DockStyle.Left,
            Width = 128,
            Margin = new Padding(0),
            BackColor = Surface
        };

        Panel titlePanel = new()
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(14, 12, 0, 0)
        };
        titlePanel.Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            Height = 48,
            Text = "FFGUARDIAN",
            ForeColor = Color.White,
            BackColor = Surface,
            Font = new Font("Segoe UI", 27F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        });
        titlePanel.Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = "ULTIMATE PROTECTION  •  THREE DOBERMANN DEFENSE",
            ForeColor = Neon,
            BackColor = Surface,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        });
        titlePanel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "Protezione autonoma • Ransom Shield • Firewall • USB Shield • Engine10",
            ForeColor = Muted,
            BackColor = Surface,
            Font = new Font("Segoe UI", 9F),
            TextAlign = ContentAlignment.MiddleLeft
        });

        Panel status = new()
        {
            Dock = DockStyle.Right,
            Width = 285,
            BackColor = Raised,
            Padding = new Padding(16, 14, 16, 12)
        };
        status.Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            Height = 24,
            Text = "STATO PROTEZIONE",
            ForeColor = Muted,
            BackColor = Raised,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        });
        status.Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            Height = 42,
            Text = "SISTEMA PROTETTO",
            ForeColor = Neon,
            BackColor = Raised,
            Font = new Font("Segoe UI", 17F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleRight
        });
        status.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Text = "FFGUARDIAN 10.0.1 RC1\nCreato da Francesco Fazzina by EL.CO",
            ForeColor = Color.White,
            BackColor = Raised,
            Font = new Font("Segoe UI", 8.5F),
            TextAlign = ContentAlignment.MiddleRight
        });

        header.Controls.Add(titlePanel);
        header.Controls.Add(status);
        header.Controls.Add(emblem);
        form.Controls.Add(header);
        header.BringToFront();
    }

    private static void RestyleTree(Control root)
    {
        foreach (Control control in root.Controls)
        {
            switch (control)
            {
                case TabControl tabs:
                    tabs.Appearance = TabAppearance.FlatButtons;
                    tabs.ItemSize = new Size(168, 38);
                    tabs.SizeMode = TabSizeMode.Fixed;
                    tabs.Padding = new Point(16, 6);
                    tabs.BackColor = Background;
                    tabs.ForeColor = Color.White;
                    break;
                case TabPage page:
                    page.BackColor = Background;
                    page.ForeColor = Color.White;
                    break;
                case Button button:
                    button.BackColor = Raised;
                    button.ForeColor = Color.White;
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderColor = Neon;
                    button.FlatAppearance.BorderSize = 1;
                    button.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                    button.Cursor = Cursors.Hand;
                    break;
                case GroupBox group:
                    group.BackColor = Surface;
                    group.ForeColor = Neon;
                    break;
                case Panel panel when panel.Name != "PremiumHeader10":
                    if (panel.BackColor == SystemColors.Control)
                        panel.BackColor = Surface;
                    break;
                case DataGridView grid:
                    grid.BackgroundColor = Background;
                    grid.GridColor = Color.FromArgb(45, 65, 74);
                    grid.DefaultCellStyle.BackColor = Surface;
                    grid.DefaultCellStyle.ForeColor = Color.White;
                    grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(35, 88, 43);
                    grid.DefaultCellStyle.SelectionForeColor = Color.White;
                    grid.ColumnHeadersDefaultCellStyle.BackColor = Raised;
                    grid.ColumnHeadersDefaultCellStyle.ForeColor = Neon;
                    grid.EnableHeadersVisualStyles = false;
                    grid.BorderStyle = BorderStyle.None;
                    break;
            }

            RestyleTree(control);
        }
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
        RectangleF shield = new(8, 5, Math.Max(70, Width - 16), Math.Max(80, Height - 10));

        using GraphicsPath path = new();
        path.AddPolygon([
            new PointF(shield.Left + shield.Width * .12F, shield.Top + shield.Height * .10F),
            new PointF(shield.Right - shield.Width * .12F, shield.Top + shield.Height * .10F),
            new PointF(shield.Right - shield.Width * .06F, shield.Top + shield.Height * .58F),
            new PointF(shield.Left + shield.Width * .50F, shield.Bottom - 2),
            new PointF(shield.Left + shield.Width * .06F, shield.Top + shield.Height * .58F)
        ]);

        using LinearGradientBrush shieldBrush = new(shield, Color.FromArgb(23, 39, 47), Dark, 90F);
        using Pen glow = new(Neon, 3F);
        using Pen metal = new(Metal, 7F);
        e.Graphics.FillPath(shieldBrush, path);
        e.Graphics.DrawPath(metal, path);
        e.Graphics.DrawPath(glow, path);

        DrawHead(e.Graphics, new RectangleF(shield.Left + 7, shield.Top + 30, 39, 55), -8F);
        DrawHead(e.Graphics, new RectangleF(shield.Left + shield.Width / 2F - 24, shield.Top + 17, 48, 67), 0F);
        DrawHead(e.Graphics, new RectangleF(shield.Right - 46, shield.Top + 30, 39, 55), 8F);

        using Font font = new("Segoe UI", 8F, FontStyle.Bold);
        using SolidBrush text = new(Neon);
        using StringFormat format = new() { Alignment = StringAlignment.Center };
        e.Graphics.DrawString("FFG", font, text,
            new RectangleF(shield.Left, shield.Bottom - 24, shield.Width, 18), format);
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
        using Pen outline = new(Neon, 1.4F);
        graphics.FillPolygon(black, ears);
        graphics.DrawPolygon(outline, ears);

        RectangleF face = new(box.Left + box.Width * .16F, box.Top + box.Height * .24F,
            box.Width * .68F, box.Height * .66F);
        graphics.FillEllipse(black, face);
        graphics.DrawEllipse(outline, face);

        using SolidBrush tan = new(Tan);
        graphics.FillEllipse(tan, box.Left + box.Width * .25F, box.Top + box.Height * .48F, 7F, 5F);
        graphics.FillEllipse(tan, box.Right - box.Width * .25F - 7F, box.Top + box.Height * .48F, 7F, 5F);
        graphics.FillEllipse(tan, box.Left + box.Width * .34F, box.Top + box.Height * .69F, box.Width * .32F, 9F);

        using SolidBrush eye = new(Neon);
        graphics.FillEllipse(eye, box.Left + box.Width * .31F, box.Top + box.Height * .53F, 3.5F, 3.5F);
        graphics.FillEllipse(eye, box.Right - box.Width * .31F - 3.5F, box.Top + box.Height * .53F, 3.5F, 3.5F);
        graphics.Restore(state);
    }
}
