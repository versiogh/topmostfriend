using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Threading.Tasks;

namespace TopMostFriend;

public static class UAC
{
    public const int ErrorCancelled = 1223;
    public const int ErrorTimeout = 1460;

    private static bool? _isElevated;

    public static bool IsElevated
    {
        get
        {
            if (_isElevated.HasValue)
                return _isElevated.Value;

            using WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new(identity);
            _isElevated = principal.IsInRole(WindowsBuiltInRole.Administrator);
            return _isElevated.Value;
        }
    }

    public static int SetWindowTopMostElevated(WindowInfo window, bool targetState, bool activate) =>
        RunElevatedTask(BuildSetTopMostArguments(window, targetState, activate));

    /// <summary>
    /// Runs the elevated helper without blocking the calling thread. RunElevatedTask waits for the
    /// helper to exit, so calling it directly from the UI thread freezes the tray icon, the menu
    /// and every dialog for the duration of the wait.
    /// </summary>
    public static Task<int> SetWindowTopMostElevatedAsync(WindowInfo window, bool targetState, bool activate)
    {
        string[] args = BuildSetTopMostArguments(window, targetState, activate);
        return Task.Run(() => RunElevatedTask(args));
    }

    private static string[] BuildSetTopMostArguments(WindowInfo window, bool targetState, bool activate) =>
        new[]
        {
            $"--set-topmost={(targetState ? 1 : 0)}",
            $"--hwnd={window.Handle.ToInt64()}",
            $"--pid={window.ProcessId}",
            $"--tid={window.ThreadId}",
            $"--activate={(activate ? 1 : 0)}",
        };

    public static int RunElevatedTask(params string[] args)
    {
        if (args == null || args.Length == 0)
            throw new ArgumentException("No arguments provided.", nameof(args));

        try
        {
            ProcessStartInfo info = new()
            {
                UseShellExecute = true,
                FileName = ShellHelper.ExecutablePath,
                WorkingDirectory = AppContext.BaseDirectory,
                Arguments = string.Join(" ", args.Select(ShellHelper.QuoteArgument)),
                Verb = "runas",
            };

            using Process? process = Process.Start(info);
            if (process == null)
                return -1;

            // The helper only performs a SetWindowPos operation, but a self-contained
            // single-file build has to extract its bundle on the first elevated run and
            // may also be scanned by security software, so the budget is generous.
            // The caller still verifies the effective window state afterwards.
            if (!process.WaitForExit(60_000))
            {
                AppLog.Write("Elevated helper timed out.");
                return ErrorTimeout;
            }

            return process.ExitCode;
        }
        catch (Win32Exception ex)
        {
            AppLog.Write("Elevated helper could not be started.", ex);
            return ex.NativeErrorCode;
        }
        catch (Exception ex)
        {
            AppLog.Write("Elevated helper failed.", ex);
            return -1;
        }
    }

    public static bool TryRestartElevated()
    {
        if (IsElevated)
            return true;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                UseShellExecute = true,
                FileName = ShellHelper.ExecutablePath,
                WorkingDirectory = AppContext.BaseDirectory,
                Verb = "runas",
            });
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception || ex is InvalidOperationException)
        {
            AppLog.Write("Elevated restart was cancelled or failed.", ex);
            return false;
        }
    }
}
