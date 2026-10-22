using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using AW_Dollar.Models;
using AW_Dollar.Native;
using AW_Dollar.Rendering;
using AW_Dollar.Services;

namespace AW_Dollar;

/// <summary>
/// A small strip docked directly onto the real Windows taskbar, immediately to the left
/// of the notification area (tray icons + clock) - the same trick classic "network speed
/// in the taskbar" tools use. It is re-parented onto explorer.exe's own "Shell_TrayWnd"
/// window and converted to a real child window, so it moves, hides and z-orders together
/// with the taskbar automatically. Its background is fully transparent (a
/// <see cref="Form.TransparencyKey"/>), so only the flag + price + trend text is drawn -
/// the real taskbar pixels show through everywhere else, exactly like a native item.
/// </summary>
public sealed class TaskbarPriceStrip : Form
{
    private static readonly Color KeyColor = Color.FromArgb(1, 2, 3); // never used elsewhere, safe transparency key
    private static readonly Color UpColor = Color.FromArgb(74, 222, 128);
    private static readonly Color DownColor = Color.FromArgb(248, 113, 113);
    private static readonly Color FlatColor = Color.FromArgb(156, 163, 175);

    private readonly Bitmap _usdFlag = EmbeddedImageLoader.Load("usd_flag.png");
    private readonly Bitmap _eurFlag = EmbeddedImageLoader.Load("eur_flag.png");
    private readonly System.Windows.Forms.Timer _dockTimer;

    private IntPtr _taskbar;
    private IntPtr _trayNotify;
    private bool _docked;
    private int _failedAttempts;

    private PriceSnapshot? _usd;
    private PriceSnapshot? _eur;
    private DisplaySize _displaySize = DisplaySize.Medium;

    /// <summary>Raised once if the taskbar can't be found/docked after several tries.</summary>
    public event EventHandler? DockingFailed;

    /// <summary>
    /// Raised when the user resizes the strip themselves (mouse wheel over it), so the
    /// host can persist the new choice and keep its menu checkmarks in sync.
    /// </summary>
    public event EventHandler<DisplaySize>? DisplaySizeChangedByUser;

    public TaskbarPriceStrip(DisplaySize initialSize = DisplaySize.Medium)
    {
        _displaySize = initialSize;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = KeyColor;
        TransparencyKey = KeyColor;
        DoubleBuffered = true;
        Size = new Size(StripWidthFor(_displaySize), 48);
        Location = new Point(-Width - 10, -100); // off-screen until docked

        _dockTimer = new System.Windows.Forms.Timer { Interval = 1200 };
        _dockTimer.Tick += (_, _) => SyncWithTaskbar();
    }

    /// <summary>
    /// How big the text/flags should be, chosen by the user from the tray menu. Changing
    /// this immediately resizes and redraws the strip (the row height is still ultimately
    /// capped by how tall the real Windows taskbar is, so very large settings fill as much
    /// of that space as physically fits without being cut off).
    /// </summary>
    public DisplaySize DisplaySize
    {
        get => _displaySize;
        set
        {
            if (_displaySize == value) return;
            _displaySize = value;
            SyncWithTaskbar();
            Invalidate();
        }
    }

    private static int StripWidthFor(DisplaySize size) => size switch
    {
        DisplaySize.Small => 150,
        DisplaySize.Medium => 182,
        DisplaySize.Large => 208,
        DisplaySize.ExtraLarge => 236,
        _ => 182,
    };

    private static float SizeScaleFor(DisplaySize size) => size switch
    {
        DisplaySize.Small => 0.82f,
        DisplaySize.Medium => 1.0f,
        DisplaySize.Large => 1.25f,
        DisplaySize.ExtraLarge => 1.5f,
        _ => 1.0f,
    };

    /// <summary>Scrolling the mouse wheel directly over the strip grows/shrinks it on the
    /// spot - a quick, hands-on way to resize it without opening the right-click menu.</summary>
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);

        DisplaySize next = e.Delta switch
        {
            > 0 => _displaySize switch
            {
                DisplaySize.Small => DisplaySize.Medium,
                DisplaySize.Medium => DisplaySize.Large,
                DisplaySize.Large => DisplaySize.ExtraLarge,
                _ => DisplaySize.ExtraLarge,
            },
            < 0 => _displaySize switch
            {
                DisplaySize.ExtraLarge => DisplaySize.Large,
                DisplaySize.Large => DisplaySize.Medium,
                DisplaySize.Medium => DisplaySize.Small,
                _ => DisplaySize.Small,
            },
            _ => _displaySize,
        };

        if (next == _displaySize) return;

        DisplaySize = next;
        DisplaySizeChangedByUser?.Invoke(this, next);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TOOLWINDOW = 0x80;
            const int WS_EX_NOACTIVATE = 0x08000000;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        SyncWithTaskbar();
        _dockTimer.Start();
    }

    private void SyncWithTaskbar()
    {
        if (!TaskbarInterop.IsValid(_taskbar))
        {
            _taskbar = TaskbarInterop.FindTaskbar();
            _docked = false;
        }

        if (_taskbar == IntPtr.Zero)
        {
            _failedAttempts++;
            if (_failedAttempts == 5) DockingFailed?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (!TaskbarInterop.IsValid(_trayNotify))
        {
            _trayNotify = TaskbarInterop.FindTrayNotifyArea(_taskbar);
        }

        if (_trayNotify == IntPtr.Zero)
        {
            _failedAttempts++;
            if (_failedAttempts == 5) DockingFailed?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (!_docked)
        {
            TaskbarInterop.DockAsChild(Handle, _taskbar);
            _docked = true;
            if (!Visible) Show();
        }

        var trayRect = TaskbarInterop.GetScreenRect(_trayNotify);
        if (trayRect is null) return;

        int width = StripWidthFor(_displaySize);
        var target = new Rectangle(
            trayRect.Value.Left - width,
            trayRect.Value.Top,
            width,
            trayRect.Value.Height);

        if (target.Size != Size) Size = target.Size;
        TaskbarInterop.MoveTo(Handle, _taskbar, target);
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
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.Clear(KeyColor);

        Color textColor = TaskbarInterop.IsSystemLightTheme() ? Color.Black : Color.White;

        int rowHeight = Height / 2;
        DrawRow(g, 0, rowHeight, _usdFlag, _usd, textColor);
        DrawRow(g, rowHeight, Height - rowHeight, _eurFlag, _eur, textColor);
    }

    private void DrawRow(Graphics g, int top, int rowHeight, Bitmap flag, PriceSnapshot? snapshot, Color textColor)
    {
        float scale = SizeScaleFor(_displaySize);

        // Use as much of the available row height as possible for the flag (it can never
        // exceed the row itself, since the row height is capped by the real Windows
        // taskbar's own thickness and we must not spill outside it).
        int flagSize = (int)Math.Clamp((rowHeight - 4) * scale, 10, Math.Max(10, rowHeight - 2));
        var flagRect = new Rectangle(6, top + (rowHeight - flagSize) / 2, flagSize, flagSize);
        using (var clip = new GraphicsPath())
        {
            clip.AddEllipse(flagRect);
            var old = g.Clip;
            g.SetClip(clip, CombineMode.Intersect);
            g.DrawImage(flag, flagRect);
            g.Clip = old;
        }

        string text = snapshot is null || snapshot.PriceToman <= 0
            ? "..."
            : snapshot.PriceToman.ToString("N0", CultureInfo.InvariantCulture);

        int trendWidth = 16;
        var textRect = new Rectangle(flagRect.Right + 6, top, Width - flagRect.Right - 6 - trendWidth, rowHeight);

        // Pick the largest bold font (up to the user's chosen size) that still fits inside
        // the row without being clipped, instead of a fixed guess - this fills the taskbar
        // row as much as possible at every Windows DPI/taskbar-height combination.
        float targetPx = Math.Clamp((rowHeight - 4) * scale, 9, Math.Max(9, rowHeight - 2));
        using var font = FitFont(g, text, textRect.Width, textRect.Height, targetPx);
        TextRenderer.DrawText(
            g,
            text,
            font,
            textRect,
            textColor,
            TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);

        if (snapshot is not null && snapshot.Trend != PriceTrend.Unknown)
        {
            int arrowSize = (int)Math.Clamp(10 * scale, 8, Math.Max(8, rowHeight - 4));
            DrawTrendArrow(g, snapshot.Trend, new Rectangle(
                Width - trendWidth + (trendWidth - arrowSize) / 2,
                top + (rowHeight - arrowSize) / 2,
                arrowSize,
                arrowSize));
        }
    }

    /// <summary>
    /// Finds the largest bold "Segoe UI" pixel size, starting at <paramref name="startPx"/>
    /// and shrinking, whose rendered size still fits within the given box. Guarantees the
    /// price text is never cut off, however big the user's chosen display size is.
    /// </summary>
    private static Font FitFont(Graphics g, string text, int maxWidth, int maxHeight, float startPx)
    {
        float px = Math.Max(startPx, 8f);
        while (px > 8f)
        {
            var font = new Font("Segoe UI", px, FontStyle.Bold, GraphicsUnit.Pixel);
            var measured = TextRenderer.MeasureText(g, text, font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            if (measured.Width <= maxWidth && measured.Height <= maxHeight)
            {
                return font;
            }

            font.Dispose();
            px -= 1f;
        }

        return new Font("Segoe UI", 8f, FontStyle.Bold, GraphicsUnit.Pixel);
    }

    private static void DrawTrendArrow(Graphics g, PriceTrend trend, Rectangle bounds)
    {
        Color color = trend switch
        {
            PriceTrend.Up => UpColor,
            PriceTrend.Down => DownColor,
            _ => FlatColor,
        };

        float cx = bounds.Left + bounds.Width / 2f;
        float cy = bounds.Top + bounds.Height / 2f;
        using var brush = new SolidBrush(color);

        if (trend == PriceTrend.Flat)
        {
            g.FillRectangle(brush, bounds.Left, cy - 1.5f, bounds.Width, 3f);
            return;
        }

        PointF[] triangle = trend == PriceTrend.Up
            ? new[] { new PointF(cx, bounds.Top), new PointF(bounds.Left, bounds.Bottom), new PointF(bounds.Right, bounds.Bottom) }
            : new[] { new PointF(cx, bounds.Bottom), new PointF(bounds.Left, bounds.Top), new PointF(bounds.Right, bounds.Top) };

        g.FillPolygon(brush, triangle);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // This window belongs to the app's own lifetime, not the user's; only our own
        // CloseForReal() (called on app exit) should ever really close it.
        if (!_allowClose)
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
        _dockTimer.Stop();
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dockTimer.Dispose();
            _usdFlag.Dispose();
            _eurFlag.Dispose();
        }
        base.Dispose(disposing);
    }
}
