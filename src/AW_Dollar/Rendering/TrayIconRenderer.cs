using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using AW_Dollar.Models;

namespace AW_Dollar.Rendering;

/// <summary>
/// Draws the small, dynamic bitmap that becomes the real Windows taskbar notification-area
/// icon (the little area next to the clock/Wi-Fi/volume icons - not a separate floating
/// window). Each currency gets its own icon: the flag badge plus the price, abbreviated to
/// "thousands of Toman" (e.g. "272" for ~272,000 Toman) so it stays legible at the ~16-24px
/// size Windows actually renders notification icons at, plus a tiny up/down trend triangle.
/// The exact full price lives in the icon's tooltip (hover) text.
/// </summary>
public static class TrayIconRenderer
{
    private const int CanvasSize = 64;
    private static readonly Color BadgeColor = Color.FromArgb(17, 24, 39);
    private static readonly Color UpColor = Color.FromArgb(74, 222, 128);
    private static readonly Color DownColor = Color.FromArgb(248, 113, 113);
    private static readonly Color FlatColor = Color.FromArgb(156, 163, 175);

    public static Icon BuildPriceIcon(PriceSnapshot snapshot, Bitmap flag)
    {
        using var bmp = new Bitmap(CanvasSize, CanvasSize);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Color.Transparent);

            // The flag circle fills almost the whole icon and is allowed to tuck in behind
            // the price badge at the bottom, the same layering network-speed tray icons use.
            var circleRect = new Rectangle(2, 0, CanvasSize - 4, CanvasSize - 4);
            using (var clip = new GraphicsPath())
            {
                clip.AddEllipse(circleRect);
                g.SetClip(clip, CombineMode.Replace);
                g.DrawImage(flag, circleRect);
                g.ResetClip();
            }

            var badgeRect = new Rectangle(0, CanvasSize - 26, CanvasSize, 26);
            using (var badgeBrush = new SolidBrush(BadgeColor))
            using (var path = RoundedRect(badgeRect, 8))
            {
                g.FillPath(badgeBrush, path);
            }

            DrawFittedText(g, AbbreviatePrice(snapshot.PriceToman), badgeRect);
            DrawTrendBadge(g, snapshot.Trend, new Rectangle(CanvasSize - 22, 0, 22, 22));

            if (snapshot.IsStale)
            {
                using var dot = new SolidBrush(Color.FromArgb(234, 179, 8));
                g.FillEllipse(dot, 0, 0, 14, 14);
            }
        }

        return ToIcon(bmp);
    }

    public static Icon BuildLoadingIcon(Bitmap flag)
    {
        using var bmp = new Bitmap(CanvasSize, CanvasSize);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var clip = new GraphicsPath();
            clip.AddEllipse(2, 2, CanvasSize - 4, CanvasSize - 4);
            g.SetClip(clip, CombineMode.Replace);
            g.DrawImage(flag, new Rectangle(2, 2, CanvasSize - 4, CanvasSize - 4));
        }

        return ToIcon(bmp);
    }

    public static Icon BuildErrorIcon(Bitmap flag)
    {
        using var bmp = new Bitmap(CanvasSize, CanvasSize);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var circleRect = new Rectangle(2, 0, CanvasSize - 4, CanvasSize - 4);
            var grayMatrix = new ColorMatrix(new float[][]
            {
                new float[] { 0.4f, 0.4f, 0.4f, 0, 0 },
                new float[] { 0.4f, 0.4f, 0.4f, 0, 0 },
                new float[] { 0.4f, 0.4f, 0.4f, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 },
                new float[] { 0, 0, 0, 0, 1 },
            });
            using var attrs = new ImageAttributes();
            attrs.SetColorMatrix(grayMatrix);
            using (var clip = new GraphicsPath())
            {
                clip.AddEllipse(circleRect);
                g.SetClip(clip, CombineMode.Replace);
                g.DrawImage(flag, circleRect, 0, 0, flag.Width, flag.Height, GraphicsUnit.Pixel, attrs);
                g.ResetClip();
            }

            var badgeRect = new Rectangle(0, CanvasSize - 26, CanvasSize, 26);
            using (var badgeBrush = new SolidBrush(Color.FromArgb(185, 28, 28)))
            using (var path = RoundedRect(badgeRect, 8))
            {
                g.FillPath(badgeBrush, path);
            }

            DrawFittedText(g, "!", badgeRect);
        }

        return ToIcon(bmp);
    }

    /// <summary>"272" means ~272,000 Toman - the way these prices are usually quoted colloquially.</summary>
    private static string AbbreviatePrice(decimal priceToman)
    {
        if (priceToman <= 0) return "--";
        long thousands = (long)Math.Round(priceToman / 1000m, MidpointRounding.AwayFromZero);
        return thousands.ToString(CultureInfo.InvariantCulture);
    }

    private static void DrawFittedText(Graphics g, string text, Rectangle bounds)
    {
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using var brush = new SolidBrush(Color.White);

        float fontSize = 20f;
        while (fontSize > 9f)
        {
            using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            var size = g.MeasureString(text, font);
            if (size.Width <= bounds.Width - 2 && size.Height <= bounds.Height)
            {
                g.DrawString(text, font, brush, bounds, format);
                return;
            }
            fontSize -= 1.5f;
        }

        using var smallest = new Font("Segoe UI", 9f, FontStyle.Bold, GraphicsUnit.Pixel);
        g.DrawString(text, smallest, brush, bounds, format);
    }

    private static void DrawTrendBadge(Graphics g, PriceTrend trend, Rectangle bounds)
    {
        if (trend == PriceTrend.Unknown) return;

        Color color = trend switch
        {
            PriceTrend.Up => UpColor,
            PriceTrend.Down => DownColor,
            _ => FlatColor,
        };

        using (var circleBrush = new SolidBrush(Color.White))
        {
            g.FillEllipse(circleBrush, bounds);
        }
        using (var border = new Pen(color, 1.5f))
        {
            g.DrawEllipse(border, bounds);
        }

        float cx = bounds.Left + bounds.Width / 2f;
        float cy = bounds.Top + bounds.Height / 2f;
        using var brush = new SolidBrush(color);

        if (trend == PriceTrend.Flat)
        {
            g.FillRectangle(brush, cx - 4f, cy - 1.5f, 8f, 3f);
            return;
        }

        PointF[] triangle = trend == PriceTrend.Up
            ? new[] { new PointF(cx, cy - 4.5f), new PointF(cx - 4f, cy + 3.5f), new PointF(cx + 4f, cy + 3.5f) }
            : new[] { new PointF(cx, cy + 4.5f), new PointF(cx - 4f, cy - 3.5f), new PointF(cx + 4f, cy - 3.5f) };

        g.FillPolygon(brush, triangle);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Icon ToIcon(Bitmap bmp)
    {
        IntPtr hIcon = bmp.GetHicon();
        try
        {
            // Icon.FromHandle wraps the handle; cloning it into a self-contained Icon lets us
            // safely destroy the original native handle right away instead of leaking it.
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(hIcon);
        }
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool DestroyIcon(IntPtr hIcon);
    }
}
