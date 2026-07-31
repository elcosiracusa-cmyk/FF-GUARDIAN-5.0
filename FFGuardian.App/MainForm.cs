using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Net;
using Microsoft.Extensions.DependencyInjection;

namespace FFGuardian;

internal sealed class MainForm : Form
{
    private const string SupportEmail = "alsafe127.00@gmail.com";
    private static readonly Color Bg = Color.FromArgb(3, 8, 12);
    private static readonly Color Sidebar = Color.FromArgb(5, 13, 18);
    private static readonly Color Surface = Color.FromArgb(9, 20, 27);
    private static readonly Color Surface2 = Color.FromArgb(13, 29, 38);
    private static readonly Color Border = Color.FromArgb(35, 66, 78);
    private static readonly Color Neon = Color.FromArgb(142, 255, 0);
    private static readonly Color Green = Color.FromArgb(70, 230, 95);
    private static readonly Color Cyan = Color.FromArgb(0, 190, 255);
    private static readonly Color Orange = Color.FromArgb(255, 170, 35);
    private static readonly Color Red = Color.FromArgb(235, 55, 35);

    private readonly IDefenderService _defender;
    private readonly Panel _pageHost = new() { Dock = DockStyle.Fill, BackColor = Bg };
    private readonly Label _status = new()
    {
        Dock = DockStyle.Bottom,
        Height = 32,
        BackColor = Color.FromArgb(4, 12, 17),
        ForeColor = Color.Gainsboro,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(16, 0, 0, 0)
    };
    private readonly List<Button> _nav = new();

    // Riceviamo il provider per risolvere servizi tramite DI
    public MainForm(IServiceProvider provider)
    {
        _defender = provider.GetRequiredService<IDefenderService>();

        Text = "FF GUARDIAN 5.2 — Dobermann Support Edition by EL.CO";
        Icon = DobermannIconFactory.CreateIcon();
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1280, 800);
        BackColor = Bg;
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);
        DoubleBuffered = true;
        Controls.Add(_pageHost);
        Controls.Add(BuildSidebar());
        Controls.Add(BuildHeader());
        Controls.Add(_status);
        Shown += async (_, _) => await SafeAsync(ShowDashboardAsync);
    }

    // ... il resto del file rimane invariato, troncato qui per brevità ...
}
