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
/// Owns the lifetime of the whole app. There is no main window: two NotifyIcon
/// instances (one per currency) live in the Windows taskbar notification area,
/// each refreshed on a shared timer.
/// </summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(30);

    private readonly PriceApiClient _api = new();
    private readonly System.Windows.Forms.Timer _timer;
    private readonly CurrencyTray _usd;
    private readonly CurrencyTray _eur;
    private bool _disposed;

    public TrayApplicationContext()
    {
        _usd = new CurrencyTray(
            apiSymbol: "usd",
            displayNameFa: "دلار",
            resourceName: "dollar_base.png",
            accentColor: Color.FromArgb(34, 197, 94),
            badgeColor: Color.FromArgb(21, 128, 61));

        _eur = new CurrencyTray(
            apiSymbol: "euro",
            displayNameFa: "یورو",
            resourceName: "euro_base.png",
            accentColor: Color.FromArgb(37, 99, 235),
            badgeColor: Color.FromArgb(30, 64, 175));

        BuildMenu(_usd);
        BuildMenu(_eur);

        _usd.NotifyIcon.Icon = TrayIconFactory.BuildLoadingIcon(_usd.ResourceName);
        _eur.NotifyIcon.Icon = TrayIconFactory.BuildLoadingIcon(_eur.ResourceName);
        _usd.NotifyIcon.Text = "دلار - در حال دریافت قیمت...";
        _eur.NotifyIcon.Text = "یورو - در حال دریافت قیمت...";
        _usd.NotifyIcon.Visible = true;
        _eur.NotifyIcon.Visible = true;

        _timer = new System.Windows.Forms.Timer { Interval = (int)RefreshInterval.TotalMilliseconds };
        _timer.Tick += async (_, _) => await RefreshAllAsync();
        _timer.Start();

        _ = RefreshAllAsync();
    }

    private void BuildMenu(CurrencyTray tray)
    {
        var menu = new ContextMenuStrip();

        var statusItem = new ToolStripMenuItem("در حال دریافت قیمت...") { Enabled = false };
        menu.Items.Add(statusItem);
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

        tray.StatusMenuItem = statusItem;
        tray.NotifyIcon.ContextMenuStrip = menu;
        tray.NotifyIcon.DoubleClick += (_, _) => OpenUrl("https://priceto.day/");
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

            if (tray.Last is { } previous && previous.PriceToman > 0)
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
                tray.NotifyIcon.ShowBalloonTip(
                    3000,
                    $"{fresh.DisplayNameFa}",
                    $"{FormatToman(fresh.PriceToman)} تومان",
                    ToolTipIcon.Info);
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
                tray.NotifyIcon.Icon = TrayIconFactory.BuildErrorIcon(tray.ResourceName);
                tray.NotifyIcon.Text = Truncate($"{tray.DisplayNameFa} - خطا در دریافت قیمت. برای تلاش مجدد کلیک کنید.");
                tray.StatusMenuItem.Text = "خطا در دریافت قیمت";
            }
        }
    }

    private static void ApplyToIcon(CurrencyTray tray, PriceSnapshot snapshot)
    {
        var oldIcon = tray.NotifyIcon.Icon;
        tray.NotifyIcon.Icon = TrayIconFactory.BuildPriceIcon(snapshot, tray.AccentColor, tray.BadgeColor, tray.ResourceName);
        oldIcon?.Dispose();

        string localTime = snapshot.UpdatedAtUtc == DateTime.MinValue
            ? "نامشخص"
            : snapshot.UpdatedAtUtc.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

        string staleSuffix = snapshot.IsStale ? " (قیمت قبلی - بروزرسانی ناموفق)" : string.Empty;

        tray.NotifyIcon.Text = Truncate(
            $"{snapshot.DisplayNameFa} (بازار آزاد): {FormatToman(snapshot.PriceToman)} تومان{staleSuffix}\n" +
            $"کمترین/بیشترین امروز: {FormatToman(snapshot.LowToman)} / {FormatToman(snapshot.HighToman)}\n" +
            $"بروزرسانی: {localTime} | هر ۳۰ دقیقه");

        tray.StatusMenuItem.Text = $"{snapshot.DisplayNameFa}: {FormatToman(snapshot.PriceToman)} تومان{staleSuffix}";
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
            _api.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>Everything one currency's tray presence needs: its icon, menu and last known price.</summary>
    private sealed class CurrencyTray : IDisposable
    {
        public CurrencyTray(string apiSymbol, string displayNameFa, string resourceName, Color accentColor, Color badgeColor)
        {
            ApiSymbol = apiSymbol;
            DisplayNameFa = displayNameFa;
            ResourceName = resourceName;
            AccentColor = accentColor;
            BadgeColor = badgeColor;
            NotifyIcon = new NotifyIcon();
        }

        public string ApiSymbol { get; }
        public string DisplayNameFa { get; }
        public string ResourceName { get; }
        public Color AccentColor { get; }
        public Color BadgeColor { get; }
        public NotifyIcon NotifyIcon { get; }
        public ToolStripMenuItem StatusMenuItem { get; set; } = null!;
        public PriceSnapshot? Last { get; set; }

        public void Dispose()
        {
            NotifyIcon.Visible = false;
            NotifyIcon.Dispose();
        }
    }
}
