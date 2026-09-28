using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace TopMostFriend;

public sealed class WindowInfo
{
    public IntPtr Handle { get; }
    public uint ProcessId { get; }
    public uint ThreadId { get; }
    public string ProcessName { get; }

    public string Title => Win32.GetWindowTextString(Handle);

    public bool IsValid
    {
        get
        {
            if (Handle == IntPtr.Zero || !Win32.IsWindow(Handle))
                return false;

            uint threadId = Win32.GetWindowThreadProcessId(Handle, out uint processId);
            return processId == ProcessId && threadId == ThreadId;
        }
    }

    public bool IsTopMost
    {
        get
        {
            if (!IsValid)
                return false;

            long style = Win32.GetWindowLongPtr(Handle, Win32.GWL_EXSTYLE).ToInt64();
            return (style & Win32.WS_EX_TOPMOST) != 0;
        }
    }

    public bool IsOwnWindow => ProcessId == unchecked((uint)Environment.ProcessId);

    public WindowInfo(IntPtr handle)
    {
        Handle = handle;
        ThreadId = Win32.GetWindowThreadProcessId(handle, out uint processId);
        ProcessId = processId;
        ProcessName = GetProcessName(processId);
    }

    public WindowInfo(IntPtr handle, uint processId, uint threadId)
    {
        Handle = handle;
        ProcessId = processId;
        ThreadId = threadId;
        ProcessName = GetProcessName(processId);
    }

    private static string GetProcessName(uint processId)
    {
        if (processId == 0)
            return string.Empty;

        try
        {
            using Process process = Process.GetProcessById((int)processId);
            return process.ProcessName + ".exe";
        }
        catch (Exception ex) when (
            ex is ArgumentException ||
            ex is InvalidOperationException ||
            ex is System.ComponentModel.Win32Exception)
        {
            return string.Empty;
        }
    }

    public bool TrySetTopMost(bool value)
    {
        if (!IsValid)
            return false;

        bool success = Win32.SetWindowPos(
            Handle,
            value ? Win32.HWND_TOPMOST : Win32.HWND_NOTOPMOST,
            0,
            0,
            0,
            0,
            SetWindowPosFlags.SWP_NOMOVE |
            SetWindowPosFlags.SWP_NOSIZE |
            SetWindowPosFlags.SWP_NOACTIVATE);

        return success && IsValid && IsTopMost == value;
    }

    public bool TryToggleTopMost(bool activateWhenPinned, out bool originalState, out bool newState)
    {
        originalState = IsTopMost;
        newState = originalState;

        if (!IsValid)
            return false;

        bool target = !originalState;
        if (!TrySetTopMost(target))
            return false;

        newState = target;
        if (target && activateWhenPinned)
            TryActivate();

        return true;
    }

    public bool TryActivate()
    {
        if (!IsValid)
            return false;

        if (Win32.IsIconic(Handle))
            Win32.ShowWindow(Handle, Win32.SW_RESTORE);

        return Win32.SetForegroundWindow(Handle);
    }

    public Icon? TryGetIconClone(uint timeoutMilliseconds = 80)
    {
        if (!IsValid)
            return null;

        try
        {
            IntPtr handle = Win32.TryGetWindowIconHandle(Handle, timeoutMilliseconds);
            if (handle == IntPtr.Zero)
                return null;

            using Icon borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        catch (Exception ex) when (ex is ArgumentException || ex is ExternalException)
        {
            return null;
        }
    }

    public static WindowInfo? GetForegroundWindow()
    {
        IntPtr handle = Win32.GetForegroundWindow();
        if (handle == IntPtr.Zero || !Win32.IsWindow(handle))
            return null;

        WindowInfo window = new(handle);
        return window.ProcessId == 0 ? null : window;
    }

    public static IReadOnlyList<WindowInfo> GetAllWindows(bool includeHidden = false, bool includeCloaked = false)
    {
        List<WindowInfo> windows = new();

        Win32.EnumWindows((hWnd, _) =>
        {
            try
            {
                if (!includeHidden && !Win32.IsWindowVisible(hWnd))
                    return true;

                if (!includeCloaked && Win32.IsWindowCloaked(hWnd))
                    return true;

                WindowInfo window = new(hWnd);
                if (window.ProcessId != 0 && window.ThreadId != 0 && window.IsValid)
                    windows.Add(window);
            }
            catch (Exception ex)
            {
                AppLog.Write("Ignoring a window that disappeared during enumeration.", ex);
            }

            return true;
        }, IntPtr.Zero);

        return windows;
    }
}