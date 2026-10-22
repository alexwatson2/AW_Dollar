using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AW_Dollar.Native;

/// <summary>
/// Low-level helpers for finding the real Windows taskbar window and its notification
/// area, so we can dock a small window directly onto it (like NetSpeedMonitor-style
/// taskbar add-ons do) instead of floating a separate window somewhere on the desktop.
///
/// This relies on the classic, undocumented-but-stable-since-Windows-95 "Shell_TrayWnd"
/// window hierarchy that explorer.exe still creates on Windows 10 and 11 for
/// compatibility. It is not an official API, so everything here is written to fail
/// gracefully (return null/false) if the taskbar ever can't be found.
/// </summary>
internal static class TaskbarInterop
{
    private const string TaskbarClass = "Shell_TrayWnd";
    private const string TrayNotifyClass = "TrayNotifyWnd";

    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const int WS_CHILD = 0x40000000;
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll")]
    private static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    /// <summary>Finds the primary taskbar window (explorer.exe's "Shell_TrayWnd").</summary>
    public static IntPtr FindTaskbar() => FindWindow(TaskbarClass, null);

    /// <summary>Finds the notification area (tray icons + clock cluster) inside the taskbar.</summary>
    public static IntPtr FindTrayNotifyArea(IntPtr taskbar) =>
        taskbar == IntPtr.Zero ? IntPtr.Zero : FindWindowEx(taskbar, IntPtr.Zero, TrayNotifyClass, null);

    public static bool IsValid(IntPtr hWnd) => hWnd != IntPtr.Zero && IsWindow(hWnd);

    public static Rectangle? GetScreenRect(IntPtr hWnd)
    {
        if (!IsValid(hWnd)) return null;
        if (!GetWindowRect(hWnd, out var r)) return null;
        return Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
    }

    /// <summary>
    /// Re-parents our window onto the taskbar and flips its style from a normal
    /// top-level window to a real child window, so Windows treats it exactly like
    /// another piece of the taskbar (it moves, hides and z-orders together with it).
    /// </summary>
    public static void DockAsChild(IntPtr child, IntPtr newParent)
    {
        IntPtr style = GetWindowLongPtr(child, GWL_STYLE);
        style = new IntPtr((style.ToInt64() & ~(long)WS_POPUP) | WS_CHILD);
        SetWindowLongPtr(child, GWL_STYLE, style);
        SetParent(child, newParent);
    }

    /// <summary>Positions a (now-docked) child window using screen coordinates.</summary>
    public static void MoveTo(IntPtr child, IntPtr parent, Rectangle screenBounds)
    {
        var topLeft = new POINT { X = screenBounds.Left, Y = screenBounds.Top };
        ScreenToClient(parent, ref topLeft);
        SetWindowPos(child, IntPtr.Zero, topLeft.X, topLeft.Y, screenBounds.Width, screenBounds.Height, SWP_NOZORDER | SWP_NOACTIVATE);
    }

    private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));

    private static void SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr value)
    {
        if (IntPtr.Size == 8) SetWindowLongPtr64(hWnd, nIndex, value);
        else SetWindowLong32(hWnd, nIndex, value.ToInt32());
    }

    /// <summary>
    /// True when Windows is using its light taskbar/start-menu color (so we should draw
    /// dark text); false (the common case) when the taskbar is dark and we need light text.
    /// </summary>
    public static bool IsSystemLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", writable: false);
            return key?.GetValue("SystemUsesLightTheme") is int v && v != 0;
        }
        catch
        {
            return false;
        }
    }
}
