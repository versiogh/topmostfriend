using Microsoft.Win32;
using System;
using System.IO;
using System.Security;

namespace TopMostFriend;

public static class StartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TopMostFriend";

    public static bool IsEnabled
    {
        get
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException)
            {
                AppLog.Write("Unable to read Windows startup setting.", ex);
                return false;
            }
        }
    }

    public static bool SetEnabled(bool enabled)
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true)
                ?? throw new InvalidOperationException("Unable to open the Windows Run registry key.");

            if (enabled)
                key.SetValue(ValueName, $"\"{ShellHelper.ExecutablePath}\"", RegistryValueKind.String);
            else
                key.DeleteValue(ValueName, false);

            return true;
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is SecurityException ||
            ex is InvalidOperationException)
        {
            AppLog.Write("Unable to change Windows startup setting.", ex);
            return false;
        }
    }
}
