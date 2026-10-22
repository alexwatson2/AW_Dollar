using Microsoft.Win32;

namespace AW_Dollar.Services;

/// <summary>How big the user wants the taskbar price strip's text and flag icons to be.</summary>
public enum DisplaySize
{
    Small = 0,
    Medium = 1,
    Large = 2,
    ExtraLarge = 3,
}

/// <summary>
/// Remembers the user's preferred text/icon size for <see cref="AW_Dollar.TaskbarPriceStrip"/>
/// across restarts. Stored per-user in the registry (no admin rights), same key as the
/// floating card's remembered position.
/// </summary>
public static class DisplaySizeStore
{
    private const string KeyPath = @"Software\AW_Dollar";
    private const string ValueName = "StripSize";

    public static DisplaySize Load()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: false);
        if (key?.GetValue(ValueName) is int raw && Enum.IsDefined(typeof(DisplaySize), raw))
        {
            return (DisplaySize)raw;
        }

        return DisplaySize.Medium;
    }

    public static void Save(DisplaySize size)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
        key.SetValue(ValueName, (int)size, RegistryValueKind.DWord);
    }
}
