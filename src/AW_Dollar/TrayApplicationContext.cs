using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using AW_Dollar.Models;
using AW_Dollar.Services;

namespace AW_Dollar;

/// <summary>
/// Owns the lifetime of the whole app: one small NotifyIcon in the taskbar notification
/// area (used only as a quick menu - show/hide, refresh, exit) plus the real UI, the
/// borderless <see cref="PricePanelForm"/> card that shows the live USD/EUR rows.
/// Both are refreshed together on a shared 30-minute timer.
/// </summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(30);

    private readonly PriceApiClient _api = new();
    private readonly System.Windows.Forms.Timer _timer;
    private readonly NotifyIcon _trayIcon;
    private readonly PricePanelForm _panel;
    private ToolStripMenuItem _toggleItem = null!;
    private ToolStripMenuItem _usdStatusItem = null!;
    private ToolStripMenuItem _eurStatusItem = null!;

    private PriceSnapshot? _lastUsd;
    private PriceSnapshot? _lastEur;
    private bool _disposed;

    public TrayApplicationContext()
    {
        _panel = new PricePanelForm();

        _trayIcon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "AW Dollar - در حال دریافت قیمت...",
            Visible = true,
        };

        _trayIcon.ContextMenuStrip = BuildMenu();
        _trayIcon.DoubleClick += (_, _) => TogglePanel();

        _panel.ContextMenuStrip = _trayIcon.ContextMenuStrip;
        _panel.Show();

        _timer = new System.Windows.Forms.Timer { Interval = (int)RefreshInterval.TotalMilliseconds };
        _timer.Tick += async (_, _) => await RefreshAllAsync();
        _timer.Start();

        _ = RefreshAllAsync();
    }

    private static Icon LoadAppIcon()
    {
        try
        {
            string? path = Application.ExecutablePath;
            var icon = Icon.ExtractAssociatedIcon(path);
            if (icon is not null) return icon;
        }
        catch
        {
            // fall through to the default icon below
        }

        return SystemIcons.Application;
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        _usdStatusItem = new ToolStripMenuItem("دلار: در حال دریافت...") { Enabled = false };
        _eurStatusItem = new ToolStripMenuItem("یورو: در حال دریافت...") { Enabled = false };
        menu.Items.Add(_usdStatusItem);
        menu.Items.Add(_eurStatusItem);
        menu.Items.Add(new ToolStripSeparator());

        _toggleItem = new ToolStripMenuItem("مخفی کردن کارت قیمت");
        _toggleItem.Click += (_, _) => TogglePanel();
        menu.Items.Add(_toggleItem);

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
            _toggleItem.Text = "نمایش کارت قیمت";
        }
        else
        {
            _panel.Show();
            _toggleItem.Text = "مخفی کردن کارت قیمت";
        }
    }

    private async Task RefreshAllAsync(bool manual = false)
    {
        await Task.WhenAll(
            RefreshOneAsync("usd", "دلار", manual, isUsd: true),
            RefreshOneAsync("euro", "یورو", manual, isUsd: false));
    }

    private async Task RefreshOneAsync(string apiSymbol, string displayNameFa, bool manual, bool isUsd)
    {
        var previous = isUsd ? _lastUsd : _lastEur;

        try
        {
            var fresh = await _api.GetLatestAsync(apiSymbol, displayNameFa, CancellationToken.None);

            if (previous is { PriceToman: > 0 })
            {
                fresh.Trend = fresh.PriceToman > previous.PriceToman ? PriceTrend.Up
                    : fresh.PriceToman < previous.PriceToman ? PriceTrend.Down
                    : PriceTrend.Flat;
            }

            bool firstSuccess = previous is null;
            ApplySnapshot(isUsd, fresh);

            if (manual || firstSuccess)
            {
                _trayIcon.ShowBalloonTip(3000, displayNameFa, $"{FormatToman(fresh.PriceToman)} تومان", ToolTipIcon.Info);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AW_Dollar] refresh failed for {apiSymbol}: {ex}");

            if (previous is not null)
            {
                var staleCopy = new PriceSnapshot
                {
                    Symbol = previous.Symbol,
                    DisplayNameFa = previous.DisplayNameFa,
                    PriceToman = previous.PriceToman,
                    HighToman = previous.HighToman,
                    LowToman = previous.LowToman,
                    UpdatedAtUtc = previous.UpdatedAtUtc,
                    Trend = previous.Trend,
                    IsStale = true,
                };
                ApplySnapshot(isUsd, staleCopy);
            }
            else
            {
                var statusItem = isUsd ? _usdStatusItem : _eurStatusItem;
                statusItem.Text = $"{displayNameFa}: خطا در دریافت قیمت";
            }
        }
    }

    private void ApplySnapshot(bool isUsd, PriceSnapshot snapshot)
    {
        if (isUsd)
        {
            _lastUsd = snapshot;
            _panel.UpdateUsd(snapshot);
        }
        else
        {
            _lastEur = snapshot;
            _panel.UpdateEur(snapshot);
        }

        string staleSuffix = snapshot.IsStale ? " (قیمت قبلی)" : string.Empty;
        var statusItem = isUsd ? _usdStatusItem : _eurStatusItem;
        statusItem.Text = $"{snapshot.DisplayNameFa}: {FormatToman(snapshot.PriceToman)} تومان{staleSuffix}";

        UpdateTrayTooltip();
    }

    private void UpdateTrayTooltip()
    {
        string usdPart = _lastUsd is null ? "دلار: ..." : $"دلار: {FormatToman(_lastUsd.PriceToman)} تومان";
        string eurPart = _lastEur is null ? "یورو: ..." : $"یورو: {FormatToman(_lastEur.PriceToman)} تومان";
        _trayIcon.Text = Truncate($"{usdPart}\n{eurPart}");
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
        _trayIcon.Visible = false;
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
            _trayIcon.Dispose();
            _panel.Dispose();
            _api.Dispose();
        }

        base.Dispose(disposing);
    }
}
