using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AW_Dollar.Models;
using AW_Dollar.Rendering;
using AW_Dollar.Services;

namespace AW_Dollar;

/// <summary>
/// Owns the lifetime of the whole app. The real, always-visible UI is two small
/// <see cref="NotifyIcon"/>s that live right inside the Windows taskbar's notification
/// area (next to the clock/Wi-Fi/volume icons) - one for USD, one for EUR - each redrawn
/// with the flag, the current price and an up/down trend triangle. A bigger, optional
/// floating card (<see cref="PricePanelForm"/>) with the full numbers can be toggled from
/// the tray menu for anyone who wants a larger view. Everything refreshes every 30 minutes.
/// </summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(30);

    private readonly PriceApiClient _api = new();
    private readonly System.Windows.Forms.Timer _timer;
    private readonly PricePanelForm _panel;
    private readonly CurrencyTray _usd;
    private readonly CurrencyTray _eur;
    private ToolStripMenuItem _toggleCardItem = null!;
    private bool _disposed;

    public TrayApplicationContext()
    {
        _panel = new PricePanelForm();

        _usd = new CurrencyTray("usd", "دلار", EmbeddedImageLoader.Load("usd_flag.png"));
        _eur = new CurrencyTray("euro", "یورو", EmbeddedImageLoader.Load("eur_flag.png"));

        var menu = BuildMenu();
        _usd.NotifyIcon.ContextMenuStrip = menu;
        _eur.NotifyIcon.ContextMenuStrip = menu;
        _panel.ContextMenuStrip = menu;

        _usd.NotifyIcon.DoubleClick += (_, _) => TogglePanel();
        _eur.NotifyIcon.DoubleClick += (_, _) => TogglePanel();

        _usd.NotifyIcon.Icon = TrayIconRenderer.BuildLoadingIcon(_usd.Flag);
        _eur.NotifyIcon.Icon = TrayIconRenderer.BuildLoadingIcon(_eur.Flag);
        _usd.NotifyIcon.Text = "دلار - در حال دریافت قیمت...";
        _eur.NotifyIcon.Text = "یورو - در حال دریافت قیمت...";
        _usd.NotifyIcon.Visible = true;
        _eur.NotifyIcon.Visible = true;

        _timer = new System.Windows.Forms.Timer { Interval = (int)RefreshInterval.TotalMilliseconds };
        _timer.Tick += async (_, _) => await RefreshAllAsync();
        _timer.Start();

        _ = RefreshAllAsync();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        _toggleCardItem = new ToolStripMenuItem("نمایش کارت بزرگ قیمت");
        _toggleCardItem.Click += (_, _) => TogglePanel();
        menu.Items.Add(_toggleCardItem);
        menu.Items.Add(new ToolStripSeparator());

        var refreshItem = new ToolStripMenuItem("به‌روزرسانی الان");
        refreshItem.Click += async (_, _) => await RefreshAllAsync(manual: true);
        menu.Items.Add(refreshItem);

        var websiteItem = new ToolStripMenuItem("مشاهده در مرورگر (priceto.day)");
        websiteItem.Click += (_, _) => OpenUrl("https://priceto.day/");
        menu.Items.Add(websiteItem);

        var startupItem = new ToolStripMenuItem("اجرا هنگام روشن شدن ویندوز") { CheckOnClick = true };
        startupItem.Checked = StartupManager.IsEnabled();
        startupItem.CheckedChanged += (_, _) => StartupManager.SetEnabled(startupItem.Checked);
        menu.Items.Add(startupItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("خروج");
        exitItem.Click += (_, _) => ExitApplication();
        menu.Items.Add(exitItem);

        return menu;
    }

    private void TogglePanel()
    {
        if (_panel.Visible)
        {
            _panel.Hide();
            _toggleCardItem.Text = "نمایش کارت بزرگ قیمت";
        }
        else
        {
            _panel.Show();
            _toggleCardItem.Text = "مخفی کردن کارت بزرگ قیمت";
        }
    }

    private async Task RefreshAllAsync(bool manual = false)
    {
        await Task.WhenAll(
            RefreshOneAsync(_usd, manual),
            RefreshOneAsync(_eur, manual));
    }

    private async Task RefreshOneAsync(CurrencyTray tray, bool manual)
    {
        try
        {
            var fresh = await _api.GetLatestAsync(tray.ApiSymbol, tray.DisplayNameFa, CancellationToken.None);

            if (tray.Last is { PriceToman: > 0 } previous)
            {
                fresh.Trend = fresh.PriceToman > previous.PriceToman ? PriceTrend.Up
                    : fresh.PriceToman < previous.PriceToman ? PriceTrend.Down
                    : PriceTrend.Flat;
            }

            bool firstSuccess = tray.Last is null;
            tray.Last = fresh;
            ApplyToIcon(tray, fresh);

            if (manual || firstSuccess)
            {
                tray.NotifyIcon.ShowBalloonTip(3000, fresh.DisplayNameFa, $"{FormatToman(fresh.PriceToman)} تومان", ToolTipIcon.Info);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AW_Dollar] refresh failed for {tray.ApiSymbol}: {ex}");

            if (tray.Last is { } stale)
            {
                var staleCopy = new PriceSnapshot
                {
                    Symbol = stale.Symbol,
                    DisplayNameFa = stale.DisplayNameFa,
                    PriceToman = stale.PriceToman,
                    HighToman = stale.HighToman,
                    LowToman = stale.LowToman,
                    UpdatedAtUtc = stale.UpdatedAtUtc,
                    Trend = stale.Trend,
                    IsStale = true,
                };
                tray.Last = staleCopy;
                ApplyToIcon(tray, staleCopy);
            }
            else
            {
                var oldIcon = tray.NotifyIcon.Icon;
                tray.NotifyIcon.Icon = TrayIconRenderer.BuildErrorIcon(tray.Flag);
                oldIcon?.Dispose();
                tray.NotifyIcon.Text = Truncate($"{tray.DisplayNameFa} - خطا در دریافت قیمت. برای تلاش مجدد کلیک کنید.");
            }
        }
    }

    private static void ApplyToIcon(CurrencyTray tray, PriceSnapshot snapshot)
    {
        var oldIcon = tray.NotifyIcon.Icon;
        tray.NotifyIcon.Icon = TrayIconRenderer.BuildPriceIcon(snapshot, tray.Flag);
        oldIcon?.Dispose();

        string localTime = snapshot.UpdatedAtUtc == DateTime.MinValue
            ? "نامشخص"
            : snapshot.UpdatedAtUtc.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

        string staleSuffix = snapshot.IsStale ? " (قیمت قبلی - بروزرسانی ناموفق)" : string.Empty;

        tray.NotifyIcon.Text = Truncate(
            $"{snapshot.DisplayNameFa} (بازار آزاد): {FormatToman(snapshot.PriceToman)} تومان{staleSuffix}\n" +
            $"کمترین/بیشترین امروز: {FormatToman(snapshot.LowToman)} / {FormatToman(snapshot.HighToman)}\n" +
            $"بروزرسانی: {localTime} | هر ۳۰ دقیقه");
    }

    private static string FormatToman(decimal value) => value.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>NotifyIcon.Text has a hard 127-character limit.</summary>
    private static string Truncate(string text) => text.Length <= 127 ? text : text[..124] + "...";

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AW_Dollar] failed to open url: {ex}");
        }
    }

    private void ExitApplication()
    {
        _timer.Stop();
        _usd.NotifyIcon.Visible = false;
        _eur.NotifyIcon.Visible = false;
        _panel.CloseForReal();
        Dispose();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        if (disposing)
        {
            _timer.Dispose();
            _usd.Dispose();
            _eur.Dispose();
            _panel.Dispose();
            _api.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>Everything one currency's tray icon needs: its flag, icon and last known price.</summary>
    private sealed class CurrencyTray : IDisposable
    {
        public CurrencyTray(string apiSymbol, string displayNameFa, Bitmap flag)
        {
            ApiSymbol = apiSymbol;
            DisplayNameFa = displayNameFa;
            Flag = flag;
            NotifyIcon = new NotifyIcon();
        }

        public string ApiSymbol { get; }
        public string DisplayNameFa { get; }
        public Bitmap Flag { get; }
        public NotifyIcon NotifyIcon { get; }
        public PriceSnapshot? Last { get; set; }

        public void Dispose()
        {
            NotifyIcon.Visible = false;
            NotifyIcon.Dispose();
            Flag.Dispose();
        }
    }
}
