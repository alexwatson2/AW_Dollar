using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using AW_Dollar.Models;
using AW_Dollar.Native;
using AW_Dollar.Rendering;

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
    public const int StripWidth = 170;

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

    /// <summary>Raised once if the taskbar can't be found/docked after several tries.</summary>
    public event EventHandler? DockingFailed;

    public TaskbarPriceStrip()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = KeyColor;
        TransparencyKey = KeyColor;
        DoubleBuffered = true;
        Size = new Size(StripWidth, 48);
        Location = new Point(-StripWidth - 10, -100); // off-screen until docked

        _dockTimer = new System.Windows.Forms.Timer { Interval = 1200 };
        _dockTimer.Tick += (_, _) => SyncWithTaskbar();
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

        var target = new Rectangle(
            trayRect.Value.Left - StripWidth,
            trayRect.Value.Top,
            StripWidth,
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
        int flagSize = Math.Min(rowHeight - 6, 20);
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

        var textRect = new Rectangle(flagRect.Right + 6, top, Width - flagRect.Right - 6 - 16, rowHeight);
        using (var font = new Font("Segoe UI", Math.Max(9, rowHeight - 14), FontStyle.Bold, GraphicsUnit.Pixel))
        using (var brush = new SolidBrush(textColor))
        using (var format = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center })
        {
            g.DrawString(text, font, brush, textRect, format);
        }

        if (snapshot is not null && snapshot.Trend != PriceTrend.Unknown)
        {
            DrawTrendArrow(g, snapshot.Trend, new Rectangle(Width - 16, top + (rowHeight - 12) / 2, 12, 12));
        }
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
