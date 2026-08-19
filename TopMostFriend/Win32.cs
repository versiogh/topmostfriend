using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace TopMostFriend;

[Flags]
public enum Win32ModKeys : uint
{
    [Description("Alt")]
    MOD_ALT = 0x0001,
    [Description("Ctrl")]
    MOD_CONTROL = 0x0002,
    [Description("Shift")]
    MOD_SHIFT = 0x0004,
    [Description("Windows")]
    MOD_WIN = 0x0008,
    MOD_NOREPEAT = 0x4000,
}

[Flags]
public enum SetWindowPosFlags : uint
{
    SWP_NOSIZE = 0x0001,
    SWP_NOMOVE = 0x0002,
    SWP_NOACTIVATE = 0x0010,
    SWP_SHOWWINDOW = 0x0040,
}

[Flags]
public enum SendMessageTimeoutFlags : uint
{
    SMTO_NORMAL = 0x0000,
    SMTO_BLOCK = 0x0001,
    SMTO_ABORTIFHUNG = 0x0002,
    SMTO_NOTIMEOUTIFNOTHUNG = 0x0008,
    SMTO_ERRORONEXIT = 0x0020,
}

public static class Win32
{
    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new(-2);

    public const int GWL_EXSTYLE = -20;
    public const int GCL_HICON = -14;
    public const int GCL_HICONSM = -34;
    public const long WS_EX_TOPMOST = 0x00000008L;

    public const int WM_GETICON = 0x007F;
    public const int WM_HOTKEY = 0x0312;
    public const int ICON_SMALL = 0;
    public const int ICON_BIG = 1;
    public const int ICON_SMALL2 = 2;
    public const int SW_RESTORE = 9;
    public const int DWMWA_CLOAKED = 14;
    public const int SM_CXSMICON = 49;
    public const int SM_CYSMICON = 50;
    public const int DEFAULT_DPI = 96;

    private const string TaskbarClassName = "Shell_TrayWnd";

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowTextW(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowTextLengthW(IntPtr hWnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetClassLongW", SetLastError = true)]
    private static extern uint GetClassLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW", SetLastError = true)]
    private static extern IntPtr GetClassLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        SetWindowPosFlags flags);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeoutW(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        SendMessageTimeoutFlags flags,
        uint timeout,
        out IntPtr result);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindowW(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();

    [DllImport("user32.dll")]
    private static extern int GetSystemMetricsForDpi(int nIndex, uint dpi);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern ushort GlobalAddAtomW(string lpString);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern ushort GlobalDeleteAtom(ushort atom);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr hWnd,
        int dwAttribute,
        out int pvAttribute,
        int cbAttribute);

    public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));

    public static IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex) =>
        IntPtr.Size == 8 ? GetClassLongPtr64(hWnd, nIndex) : new IntPtr(unchecked((int)GetClassLong32(hWnd, nIndex)));

    /// <summary>
    /// Returns the DPI the notification area is rendered at. GetSystemMetrics reports 96 dpi
    /// values inside a per-monitor aware process, so the taskbar has to be queried directly to
    /// know which icon size the shell expects.
    /// </summary>
    public static int GetNotificationAreaDpi()
    {
        try
        {
            IntPtr taskbar = FindWindowW(TaskbarClassName, null);
            if (taskbar != IntPtr.Zero)
            {
                uint windowDpi = GetDpiForWindow(taskbar);
                if (IsPlausibleDpi(windowDpi))
                    return (int)windowDpi;
            }

            uint systemDpi = GetDpiForSystem();
            if (IsPlausibleDpi(systemDpi))
                return (int)systemDpi;
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException || ex is DllNotFoundException)
        {
            AppLog.Write("Per-monitor DPI APIs are unavailable; falling back to 96 dpi.", ex);
        }

        return DEFAULT_DPI;
    }

    /// <summary>
    /// Returns the small-icon size Windows expects at the given DPI.
    /// </summary>
    public static Size GetSmallIconSizeForDpi(int dpi)
    {
        try
        {
            int width = GetSystemMetricsForDpi(SM_CXSMICON, (uint)dpi);
            int height = GetSystemMetricsForDpi(SM_CYSMICON, (uint)dpi);
            if (width > 0 && height > 0)
                return new Size(width, height);
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException || ex is DllNotFoundException)
        {
            AppLog.Write("GetSystemMetricsForDpi is unavailable; using the process default icon size.", ex);
        }

        Size fallback = SystemInformation.SmallIconSize;
        return fallback.Width > 0 && fallback.Height > 0 ? fallback : new Size(16, 16);
    }

    private static bool IsPlausibleDpi(uint dpi) => dpi >= 48 && dpi <= 960;

    public static string GetWindowTextString(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero || !IsWindow(hWnd))
            return string.Empty;

        try
        {
            int length = GetWindowTextLengthW(hWnd);
            if (length <= 0)
                return string.Empty;

            // Keep a hard upper bound in case a broken window reports nonsense.
            length = Math.Min(length, 32767);
            StringBuilder buffer = new(length + 1);
            int copied = GetWindowTextW(hWnd, buffer, buffer.Capacity);
            return copied > 0 ? buffer.ToString(0, copied) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public static bool IsWindowCloaked(IntPtr hWnd)
    {
        try
        {
            return DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    public static bool TrySendMessageTimeout(
        IntPtr hWnd,
        int message,
        int wParam,
        int lParam,
        uint timeoutMilliseconds,
        out IntPtr result)
    {
        result = IntPtr.Zero;
        if (hWnd == IntPtr.Zero || !IsWindow(hWnd))
            return false;

        IntPtr success = SendMessageTimeoutW(
            hWnd,
            unchecked((uint)message),
            new IntPtr(wParam),
            new IntPtr(lParam),
            SendMessageTimeoutFlags.SMTO_ABORTIFHUNG |
            SendMessageTimeoutFlags.SMTO_BLOCK |
            SendMessageTimeoutFlags.SMTO_ERRORONEXIT,
            timeoutMilliseconds,
            out result);

        return success != IntPtr.Zero;
    }

    public static IntPtr TryGetWindowIconHandle(IntPtr hWnd, uint timeoutMilliseconds = 80)
    {
        int[] sizes = { ICON_SMALL2, ICON_SMALL, ICON_BIG };

        foreach (int size in sizes)
        {
            if (TrySendMessageTimeout(hWnd, WM_GETICON, size, 0, timeoutMilliseconds, out IntPtr icon) && icon != IntPtr.Zero)
                return icon;
        }

        IntPtr classIcon = GetClassLongPtr(hWnd, GCL_HICONSM);
        if (classIcon == IntPtr.Zero)
            classIcon = GetClassLongPtr(hWnd, GCL_HICON);

        return classIcon;
    }
}
