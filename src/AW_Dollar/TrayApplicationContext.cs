using System.Collections.Generic;
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
/// Owns the lifetime of the whole app. The real, always-visible UI is
/// <see cref="TaskbarPriceStrip"/>, a small strip docked directly onto the Windows
/// taskbar right next to the notification area (clock/Wi-Fi/volume). A single plain
/// <see cref="NotifyIcon"/> provides the right-click menu, and an optional bigger
/// floating card (<see cref="PricePanelForm"/>) can be toggled for a larger, exact view
/// (and is shown automatically as a fallback if docking onto the taskbar ever fails).
/// Everything refreshes every 30 minutes.
/// </summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(30);

    private readonly PriceApiClient _api = new();
    private readonly System.Windows.Forms.Timer _timer;
    private readonly NotifyIcon _trayIcon;
    private readonly TaskbarPriceStrip _strip;
    private readonly PricePanelForm _panel;
    private ToolStripMenuItem _toggleCardItem = null!;
    private readonly Dictionary<DisplaySize, ToolStripMenuItem> _sizeItems = new();

    private PriceSnapshot? _lastUsd;
    private PriceSnapshot? _lastEur;
    private bool _disposed;

    public TrayApplicationContext()
    {
        _strip = new TaskbarPriceStrip(DisplaySizeStore.Load());
        _strip.DockingFailed += OnDockingFailed;
        _strip.DisplaySizeChangedByUser += (_, size) => SetDisplaySize(size);

        _panel = new PricePanelForm();

        _trayIcon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "AW Dollar - در حال دریافت قیمت...",
            Visible = true,
        };

        var menu = BuildMenu();
        _trayIcon.ContextMenuStrip = menu;
        _strip.ContextMenuStrip = menu;
        _panel.ContextMenuStrip = menu;

        _trayIcon.DoubleClick += (_, _) => TogglePanel();

        _strip.Show();

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

    private void OnDockingFailed(object? sender, EventArgs e)
    {
        // Couldn't attach to the real taskbar (unsupported Windows build, locked-down
        // Explorer, etc.) - fall back to the floating card so the prices are still visible.
        if (!_panel.Visible)
        {
            TogglePanel();
        }

        _trayIcon.ShowBalloonTip(
            5000,
            "AW Dollar",
            "اتصال به نوار وظیفه ممکن نشد؛ به‌جای آن کارت شناور قیمت نمایش داده شد.",
            ToolTipIcon.Warning);
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        _toggleCardItem = new ToolStripMenuItem("نمایش کارت بزرگ قیمت");
        _toggleCardItem.Click += (_, _) => TogglePanel();
        menu.Items.Add(_toggleCardItem);

        menu.Items.Add(BuildSizeMenu());
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

    private ToolStripMenuItem BuildSizeMenu()
    {
        var sizeMenu = new ToolStripMenuItem("اندازه متن و آیکون روی Taskbar");

        AddSizeOption(sizeMenu, DisplaySize.Small, "کوچک");
        AddSizeOption(sizeMenu, DisplaySize.Medium, "متوسط (پیش‌فرض)");
        AddSizeOption(sizeMenu, DisplaySize.Large, "بزرگ");
        AddSizeOption(sizeMenu, DisplaySize.ExtraLarge, "خیلی بزرگ");

        _sizeItems[_strip.DisplaySize].Checked = true;
        return sizeMenu;
    }

    private void AddSizeOption(ToolStripMenuItem parent, DisplaySize size, string label)
    {
        var item = new ToolStripMenuItem(label) { CheckOnClick = false };
        item.Click += (_, _) => SetDisplaySize(size);
        parent.DropDownItems.Add(item);
        _sizeItems[size] = item;
    }

    private void SetDisplaySize(DisplaySize size)
    {
        _strip.DisplaySize = size;
        DisplaySizeStore.Save(size);

        foreach (var pair in _sizeItems)
        {
            pair.Value.Checked = pair.Key == size;
        }
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
        }
    }

    private void ApplySnapshot(bool isUsd, PriceSnapshot snapshot)
    {
        if (isUsd)
        {
            _lastUsd = snapshot;
            _strip.UpdateUsd(snapshot);
            _panel.UpdateUsd(snapshot);
        }
        else
        {
            _lastEur = snapshot;
            _strip.UpdateEur(snapshot);
            _panel.UpdateEur(snapshot);
        }

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
        _strip.CloseForReal();
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
            _strip.Dispose();
            _panel.Dispose();
            _api.Dispose();
        }

        base.Dispose(disposing);
    }
}
