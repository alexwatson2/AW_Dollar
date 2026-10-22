using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using AW_Dollar.Models;
using AW_Dollar.Rendering;
using AW_Dollar.Services;

namespace AW_Dollar;

/// <summary>
/// The small, always-on-top, borderless "card" that floats on the desktop (by default
/// docked near the bottom-right corner, just above the taskbar) showing the USD and EUR
/// rows exactly like the reference mock-up: a flag badge, the full price and an
/// up/down trend triangle per row. It can be dragged anywhere and remembers where you
/// left it. The system tray icon is just a small remote control for this panel
/// (show/hide, refresh, exit) - all the actual numbers live here.
/// </summary>
public sealed class PricePanelForm : Form
{
    private const int CornerRadius = 22;
    private const int PanelWidth = 440;
    private const int RowHeight = 76;
    private const int Padding = 14;
    private const int FlagSize = 56;

    private static readonly Color BackgroundColor = Color.FromArgb(34, 34, 34);
    private static readonly Color BorderColor = Color.FromArgb(70, 70, 70);
    private static readonly Color UpColor = Color.FromArgb(74, 222, 128);
    private static readonly Color DownColor = Color.FromArgb(248, 113, 113);
    private static readonly Color FlatColor = Color.FromArgb(156, 163, 175);

    private readonly Bitmap _usdFlag = EmbeddedImageLoader.Load("usd_flag.png");
    private readonly Bitmap _eurFlag = EmbeddedImageLoader.Load("eur_flag.png");
    private readonly Font _priceFont = new("Segoe UI", 24f, FontStyle.Bold, GraphicsUnit.Pixel);
    private readonly Font _statusFont = new("Segoe UI", 15f, FontStyle.Regular, GraphicsUnit.Pixel);

    private PriceSnapshot? _usd;
    private PriceSnapshot? _eur;
    private Point _dragStart;
    private bool _dragging;

    public PricePanelForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        // The window Region (set below, after Load/Resize) clips the form to a rounded
        // rectangle, so whatever is outside that shape is never drawn - no need for a
        // transparency key. BackColor only shows through briefly during a resize/first paint.
        BackColor = BackgroundColor;
        DoubleBuffered = true;
        ClientSize = new Size(PanelWidth, RowHeight * 2 + Padding * 3);

        Location = PanelPositionStore.Load() ?? DefaultPosition();

        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
    }

    /// <summary>Default spot: bottom-right corner of the working area, just above the taskbar.</summary>
    private Point DefaultPosition()
    {
        var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1040);
        int x = area.Right - ClientSize.Width - 16;
        int y = area.Bottom - ClientSize.Height - 16;
        return new Point(Math.Max(area.Left, x), Math.Max(area.Top, y));
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TOOLWINDOW = 0x80;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW; // keep it out of Alt+Tab
            return cp;
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        ApplyRoundedRegion();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        ApplyRoundedRegion();
    }

    private void ApplyRoundedRegion()
    {
        using var path = RoundedRectPath(new Rectangle(0, 0, Width, Height), CornerRadius);
        Region = new Region(path);
    }

    public void UpdateUsd(PriceSnapshot snapshot)
    {
        _usd = snapshot;
        Invalidate();
    }

    public void UpdateEur(PriceSnapshot snapshot)
    {
        _eur = snapshot;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using (var path = RoundedRectPath(bounds, CornerRadius))
        using (var backBrush = new SolidBrush(BackgroundColor))
        using (var borderPen = new Pen(BorderColor, 1.4f))
        {
            g.FillPath(backBrush, path);
            g.DrawPath(borderPen, path);
        }

        DrawRow(g, Padding, _usdFlag, _usd);
        DrawRow(g, Padding * 2 + RowHeight, _eurFlag, _eur);
    }

    private void DrawRow(Graphics g, int top, Bitmap flag, PriceSnapshot? snapshot)
    {
        var flagRect = new Rectangle(Padding, top + (RowHeight - FlagSize) / 2, FlagSize, FlagSize);
        using (var clip = new GraphicsPath())
        {
            clip.AddEllipse(flagRect);
            var oldClip = g.Clip;
            g.SetClip(clip, CombineMode.Intersect);
            g.DrawImage(flag, flagRect);
            g.Clip = oldClip;
        }

        string text = snapshot is null ? "..." : FormatPrice(snapshot.PriceToman);
        var textRect = new Rectangle(flagRect.Right + 20, top, Width - flagRect.Right - 20 - 70, RowHeight);
        using (var textBrush = new SolidBrush(Color.White))
        using (var format = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
        {
            Font font = snapshot is null ? _statusFont : _priceFont;
            g.DrawString(text, font, textBrush, textRect, format);
        }

        if (snapshot is not null && snapshot.Trend != PriceTrend.Unknown)
        {
            DrawTrendTriangle(g, snapshot.Trend, new Rectangle(Width - Padding - 34, top + (RowHeight - 30) / 2, 34, 30));
        }

        if (snapshot is { IsStale: true })
        {
            using var dot = new SolidBrush(Color.FromArgb(234, 179, 8));
            g.FillEllipse(dot, Width - Padding - 44, top + 8, 8, 8);
        }
    }

    private static void DrawTrendTriangle(Graphics g, PriceTrend trend, Rectangle bounds)
    {
        Color color = trend switch
        {
            PriceTrend.Up => UpColor,
            PriceTrend.Down => DownColor,
            _ => FlatColor,
        };

        float cx = bounds.Left + bounds.Width / 2f;
        float cy = bounds.Top + bounds.Height / 2f;
        float top = bounds.Top;
        float bottom = bounds.Bottom;

        using var brush = new SolidBrush(color);

        if (trend == PriceTrend.Flat)
        {
            // A short, slightly rounded horizontal dash for "unchanged".
            var dash = new RectangleF(bounds.Left, cy - 2.5f, bounds.Width, 5f);
            g.FillRectangle(brush, dash);
            return;
        }

        PointF[] triangle = trend == PriceTrend.Up
            ? new[]
            {
                new PointF(cx, top),
                new PointF(bounds.Left, bottom),
                new PointF(bounds.Right, bottom),
            }
            : new[]
            {
                new PointF(cx, bottom),
                new PointF(bounds.Left, top),
                new PointF(bounds.Right, top),
            };

        g.FillPolygon(brush, triangle);
    }

    private static string FormatPrice(decimal value) =>
        value <= 0 ? "..." : value.ToString("N0", CultureInfo.InvariantCulture);

    private static GraphicsPath RoundedRectPath(Rectangle bounds, int radius)
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

    #region Dragging (click + hold anywhere on the card to move it)

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left) return;
        _dragging = true;
        _dragStart = e.Location;
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        Location = new Point(Location.X + e.X - _dragStart.X, Location.Y + e.Y - _dragStart.Y);
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        PanelPositionStore.Save(Location);
    }

    #endregion

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Alt+F4 (or any non-application-initiated close) just hides the card; the tray
        // icon's "Exit" menu item is the only thing allowed to really close the app.
        if (e.CloseReason != CloseReason.ApplicationExitCall && !_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnFormClosing(e);
    }

    private bool _allowClose;

    public void CloseForReal()
    {
        _allowClose = true;
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _usdFlag.Dispose();
            _eurFlag.Dispose();
            _priceFont.Dispose();
            _statusFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
