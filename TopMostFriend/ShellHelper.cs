using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace TopMostFriend;

public static class ShellHelper
{
    public static string ExecutablePath =>
        Environment.ProcessPath
        ?? Process.GetCurrentProcess().MainModule?.FileName
        ?? throw new InvalidOperationException("Unable to determine executable path.");

    public static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception || ex is InvalidOperationException)
        {
            AppLog.Write($"Unable to open URL: {url}", ex);
        }
    }

    public static bool StartCurrentProcess(bool elevated = false)
    {
        try
        {
            ProcessStartInfo info = new()
            {
                FileName = ExecutablePath,
                WorkingDirectory = AppContext.BaseDirectory,
                UseShellExecute = true,
            };

            if (elevated)
                info.Verb = "runas";

            Process.Start(info);
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception || ex is InvalidOperationException)
        {
            AppLog.Write("Unable to restart TopMostFriend.", ex);
            return false;
        }
    }

    public static string QuoteArgument(string argument)
    {
        if (string.IsNullOrEmpty(argument))
            return "\"\"";

        if (argument.IndexOfAny(new[] { ' ', '\t', '"' }) < 0)
            return argument;

        // Windows CommandLineToArgvW-compatible quoting.
        System.Text.StringBuilder result = new();
        result.Append('"');
        int slashes = 0;

        foreach (char c in argument)
        {
            if (c == '\\')
            {
                slashes++;
                continue;
            }

            if (c == '"')
            {
                result.Append('\\', slashes * 2 + 1);
                result.Append('"');
                slashes = 0;
                continue;
            }

            result.Append('\\', slashes);
            slashes = 0;
            result.Append(c);
        }

        result.Append('\\', slashes * 2);
        result.Append('"');
        return result.ToString();
    }
}
