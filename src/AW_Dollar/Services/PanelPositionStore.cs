using System.Drawing;
using Microsoft.Win32;

namespace AW_Dollar.Services;

/// <summary>
/// Remembers where the user dragged the floating price card to, so it reappears in the
/// same spot next time the app starts. Stored per-user in the registry (no admin rights).
/// </summary>
public static class PanelPositionStore
{
    private const string KeyPath = @"Software\AW_Dollar";
    private const string XValue = "PanelX";
    private const string YValue = "PanelY";

    public static Point? Load()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: false);
        if (key is null) return null;

        if (key.GetValue(XValue) is int x && key.GetValue(YValue) is int y)
        {
            return new Point(x, y);
        }

        return null;
    }

    public static void Save(Point location)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
        key.SetValue(XValue, location.X, RegistryValueKind.DWord);
        key.SetValue(YValue, location.Y, RegistryValueKind.DWord);
    }
}
