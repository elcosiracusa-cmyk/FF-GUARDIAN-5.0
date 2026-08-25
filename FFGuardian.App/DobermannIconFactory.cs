using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace FFGuardian;

internal static partial class DobermannIconFactory
{
    public static Bitmap CreateBitmap(int size)
    {
        Bitmap bitmap = new(size, size);
        using Graphics g = Graphics.FromImage(bitmap);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);

        float s = size / 256f;
        PointF P(float x, float y) => new(x * s, y * s);

        using GraphicsPath shield = new();
        shield.AddPolygon([
            P(128, 8), P(226, 48), P(211, 172), P(128, 244), P(45, 172), P(30, 48)
        ]);
        using SolidBrush shieldFill = new(Color.FromArgb(245, 8, 16, 12));
        using Pen shieldPen = new(Color.FromArgb(160, 255, 0), Math.Max(3, 8 * s));
        g.FillPath(shieldFill, shield);
        g.DrawPath(shieldPen, shield);

        using GraphicsPath head = new();
        head.AddPolygon([
            P(75, 79), P(88, 24), P(113, 72), P(128, 63), P(143, 72), P(168, 24),
            P(181, 79), P(173, 154), P(151, 193), P(128, 211), P(105, 193), P(83, 154)
        ]);
        using SolidBrush face = new(Color.FromArgb(232, 35, 37, 41));
        using Pen facePen = new(Color.FromArgb(205, 220, 220, 225), Math.Max(1, 3 * s));
        g.FillPath(face, head);
        g.DrawPath(facePen, head);

        using SolidBrush mask = new(Color.FromArgb(235, 5, 6, 8));
        g.FillPolygon(mask, [P(83, 83), P(118, 102), P(106, 164), P(82, 139)]);
        g.FillPolygon(mask, [P(173, 83), P(138, 102), P(150, 164), P(174, 139)]);

        using SolidBrush tan = new(Color.FromArgb(238, 180, 112, 37));
        g.FillPolygon(tan, [P(92, 133), P(111, 150), P(111, 181), P(96, 166)]);
        g.FillPolygon(tan, [P(164, 133), P(145, 150), P(145, 181), P(160, 166)]);

        using SolidBrush eye = new(Color.FromArgb(145, 255, 0));
        g.FillEllipse(eye, 95 * s, 103 * s, 20 * s, 10 * s);
        g.FillEllipse(eye, 141 * s, 103 * s, 20 * s, 10 * s);

        using SolidBrush nose = new(Color.FromArgb(245, 3, 3, 4));
        g.FillEllipse(nose, 107 * s, 157 * s, 42 * s, 29 * s);
        using Pen muzzle = new(Color.FromArgb(170, 210, 210, 215), Math.Max(1, 2 * s));
        g.DrawArc(muzzle, 103 * s, 166 * s, 50 * s, 30 * s, 10, 160);
        return bitmap;
    }

    public static Icon CreateIcon(int size = 64)
    {
        using Bitmap bitmap = CreateBitmap(size);
        IntPtr hIcon = bitmap.GetHicon();
        try
        {
            using Icon temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            _ = DestroyIcon(hIcon);
        }
    }

    [LibraryImport("user32.dll", EntryPoint = "DestroyIcon", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(IntPtr handle);
}
