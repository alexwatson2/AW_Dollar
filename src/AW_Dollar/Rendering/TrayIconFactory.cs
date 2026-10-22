using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;
using System.Runtime.InteropServices;
using AW_Dollar.Models;

namespace AW_Dollar.Rendering;

/// <summary>
/// Draws the small, dynamic bitmap that becomes the taskbar notification-area icon:
/// the currency glyph (from the embedded dollar/euro artwork) on top, with the latest
/// price baked in as text on a solid badge at the bottom, plus a tiny up/down/flat
/// trend arrow. Windows notification icons are tiny (16-24px once drawn), so the price
/// is abbreviated to "thousands of Toman" (e.g. 269 ≈ 269,000 Toman) to stay legible.
/// </summary>
public static class TrayIconFactory
{
    private const int CanvasSize = 64;

    /// <summary>Builds the live icon showing the current price for a currency.</summary>
    public static Icon BuildPriceIcon(PriceSnapshot snapshot, Color accentColor, Color badgeColor, string resourceName)
    {
        using var glyph = LoadEmbeddedGlyph(resourceName);
        using var bmp = new Bitmap(CanvasSize, CanvasSize);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            // Glyph takes the top ~60% of the canvas.
            const int glyphSize = 46;
            var glyphRect = new Rectangle((CanvasSize - glyphSize) / 2, 0, glyphSize, glyphSize);

            using (var ring = new Pen(accentColor, 2f))
            {
                var ringRect = Rectangle.Inflate(glyphRect, 3, 3);
                g.DrawEllipse(ring, ringRect);
            }

            g.DrawImage(glyph, glyphRect);

            // Solid badge band across the bottom, overlapping the glyph a little.
            var badgeRect = new Rectangle(0, 30, CanvasSize, CanvasSize - 30);
            using (var badgeBrush = new SolidBrush(badgeColor))
            using (var path = RoundedRect(badgeRect, 10))
            {
                g.FillPath(badgeBrush, path);
            }

            string text = AbbreviatePrice(snapshot.PriceToman);
            DrawFittedText(g, text, badgeRect);

            DrawTrendArrow(g, snapshot.Trend, new Rectangle(CanvasSize - 20, 0, 20, 20));

            if (snapshot.IsStale)
            {
                DrawStaleMarker(g, new Rectangle(0, 0, 18, 18));
            }
        }

        return ToIcon(bmp);
    }

    /// <summary>Neutral "loading" icon shown before the first successful refresh.</summary>
    public static Icon BuildLoadingIcon(string resourceName)
    {
        using var glyph = LoadEmbeddedGlyph(resourceName);
        using var bmp = new Bitmap(CanvasSize, CanvasSize);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            g.DrawImage(glyph, new Rectangle(4, 4, CanvasSize - 8, CanvasSize - 8));
        }

        return ToIcon(bmp);
    }

    /// <summary>Icon shown when a refresh fails and there is no cached price yet.</summary>
    public static Icon BuildErrorIcon(string resourceName)
    {
        using var glyph = LoadEmbeddedGlyph(resourceName);
        using var bmp = new Bitmap(CanvasSize, CanvasSize);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            var glyphRect = new Rectangle((CanvasSize - 46) / 2, 0, 46, 46);

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
            g.DrawImage(glyph, glyphRect, 0, 0, glyph.Width, glyph.Height, GraphicsUnit.Pixel, attrs);

            var badgeRect = new Rectangle(0, 30, CanvasSize, CanvasSize - 30);
            using (var badgeBrush = new SolidBrush(Color.FromArgb(185, 28, 28)))
            using (var path = RoundedRect(badgeRect, 10))
            {
                g.FillPath(badgeBrush, path);
            }

            DrawFittedText(g, "!", badgeRect);
        }

        return ToIcon(bmp);
    }

    /// <summary>"269" means ~269,000 Toman - the way these prices are usually quoted colloquially.</summary>
    private static string AbbreviatePrice(decimal priceToman)
    {
        if (priceToman <= 0) return "--";
        long thousands = (long)Math.Round(priceToman / 1000m, MidpointRounding.AwayFromZero);
        return thousands.ToString();
    }

    private static void DrawFittedText(Graphics g, string text, Rectangle bounds)
    {
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
        };

        float fontSize = 26f;
        using var brush = new SolidBrush(Color.White);
        while (fontSize > 10f)
        {
            using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
            var size = g.MeasureString(text, font);
            if (size.Width <= bounds.Width - 4 && size.Height <= bounds.Height)
            {
                g.DrawString(text, font, brush, bounds, format);
                return;
            }
            fontSize -= 2f;
        }

        using var smallest = new Font("Segoe UI", 10f, FontStyle.Bold, GraphicsUnit.Pixel);
        g.DrawString(text, smallest, brush, bounds, format);
    }

    private static void DrawTrendArrow(Graphics g, PriceTrend trend, Rectangle bounds)
    {
        if (trend == PriceTrend.Unknown) return;

        Color color = trend switch
        {
            PriceTrend.Up => Color.FromArgb(34, 197, 94),
            PriceTrend.Down => Color.FromArgb(239, 68, 68),
            _ => Color.FromArgb(156, 163, 175),
        };

        using var circleBrush = new SolidBrush(Color.White);
        g.FillEllipse(circleBrush, bounds);
        using var border = new Pen(color, 1.5f);
        g.DrawEllipse(border, bounds);

        var cx = bounds.Left + bounds.Width / 2f;
        var cy = bounds.Top + bounds.Height / 2f;
        using var arrowBrush = new SolidBrush(color);

        PointF[] triangle = trend switch
        {
            PriceTrend.Up => new[]
            {
                new PointF(cx, cy - 4.5f),
                new PointF(cx - 4f, cy + 3.5f),
                new PointF(cx + 4f, cy + 3.5f),
            },
            PriceTrend.Down => new[]
            {
                new PointF(cx, cy + 4.5f),
                new PointF(cx - 4f, cy - 3.5f),
                new PointF(cx + 4f, cy - 3.5f),
            },
            _ => new[]
            {
                new PointF(cx - 4f, cy),
                new PointF(cx + 4f, cy - 0.1f),
                new PointF(cx + 4f, cy + 0.1f),
            },
        };
        g.FillPolygon(arrowBrush, triangle);
    }

    private static void DrawStaleMarker(Graphics g, Rectangle bounds)
    {
        using var brush = new SolidBrush(Color.FromArgb(234, 179, 8));
        g.FillEllipse(brush, bounds);
        using var font = new Font("Segoe UI", 12f, FontStyle.Bold, GraphicsUnit.Pixel);
        using var textBrush = new SolidBrush(Color.Black);
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("i", font, textBrush, bounds, format);
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

    private static Bitmap LoadEmbeddedGlyph(string resourceName)
    {
        var asm = Assembly.GetExecutingAssembly();
        var fullName = $"AW_Dollar.Resources.{resourceName}";
        using var resourceStream = asm.GetManifestResourceStream(fullName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {fullName}");

        // Copy fully into memory and clone the decoded bitmap so it does not keep a lazy
        // reference back into the (about to be disposed) resource stream.
        using var memory = new MemoryStream();
        resourceStream.CopyTo(memory);
        memory.Position = 0;
        using var decoded = new Bitmap(memory);
        return new Bitmap(decoded);
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
