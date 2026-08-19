using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;

namespace TopMostFriend;

public static class Settings
{
    private const string RootPath = @"Software\flash.moe\TopMostFriend";

    private static RegistryKey OpenRoot(bool writable = true) =>
        Registry.CurrentUser.CreateSubKey(RootPath, writable)
        ?? throw new InvalidOperationException("Unable to open TopMostFriend settings registry key.");

    public static T Get<T>(string name, T fallback = default!)
    {
        try
        {
            using RegistryKey key = OpenRoot(false);
            object? value = key.GetValue(name, null);
            if (value == null)
                return fallback;

            Type target = typeof(T);
            if (target == typeof(bool))
                return (T)(object)(Convert.ToInt32(value, CultureInfo.InvariantCulture) != 0);

            if (target.IsEnum)
                return (T)Enum.ToObject(target, Convert.ToInt32(value, CultureInfo.InvariantCulture));

            return (T)Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is SecurityException ||
            ex is InvalidCastException ||
            ex is FormatException ||
            ex is OverflowException)
        {
            AppLog.Write($"Failed to read setting '{name}'.", ex);
            return fallback;
        }
    }

    public static string[]? Get(string name, string[]? fallback = null)
    {
        try
        {
            using RegistryKey key = OpenRoot(false);
            object? value = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);

            if (value is string[] multiString)
                return multiString;

            // Backward compatibility with TopMostFriend 1.x, which stored arrays
            // as a NUL-delimited UTF-8 binary value.
            if (value is byte[] buffer)
            {
                List<string> values = new();
                using MemoryStream current = new();

                foreach (byte b in buffer)
                {
                    if (b == 0)
                    {
                        values.Add(Encoding.UTF8.GetString(current.ToArray()));
                        current.SetLength(0);
                    }
                    else
                    {
                        current.WriteByte(b);
                    }
                }

                if (current.Length > 0)
                    values.Add(Encoding.UTF8.GetString(current.ToArray()));

                return values.ToArray();
            }

            return fallback;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException)
        {
            AppLog.Write($"Failed to read array setting '{name}'.", ex);
            return fallback;
        }
    }

    public static bool Has(string name)
    {
        try
        {
            using RegistryKey key = OpenRoot(false);
            return key.GetValue(name, null) != null;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException)
        {
            AppLog.Write($"Failed to check setting '{name}'.", ex);
            return false;
        }
    }

    public static void Set(string name, object? value)
    {
        if (value == null)
        {
            Remove(name);
            return;
        }

        try
        {
            using RegistryKey key = OpenRoot();
            switch (value)
            {
                case bool b:
                    key.SetValue(name, b ? 1 : 0, RegistryValueKind.DWord);
                    break;
                case int i:
                    key.SetValue(name, i, RegistryValueKind.DWord);
                    break;
                case string s:
                    key.SetValue(name, s, RegistryValueKind.String);
                    break;
                default:
                    key.SetValue(name, value);
                    break;
            }
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException)
        {
            AppLog.Write($"Failed to write setting '{name}'.", ex);
        }
    }

    public static void Set(string name, string[]? values)
    {
        try
        {
            using RegistryKey key = OpenRoot();
            key.SetValue(name, values ?? Array.Empty<string>(), RegistryValueKind.MultiString);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException)
        {
            AppLog.Write($"Failed to write array setting '{name}'.", ex);
        }
    }

    public static void SetDefault(string name, object value)
    {
        if (!Has(name))
            Set(name, value);
    }

    public static void SetDefault(string name, string[] value)
    {
        if (!Has(name))
            Set(name, value);
    }

    public static void Remove(string name)
    {
        try
        {
            using RegistryKey key = OpenRoot();
            key.DeleteValue(name, false);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException)
        {
            AppLog.Write($"Failed to remove setting '{name}'.", ex);
        }
    }

    public static void ResetAll()
    {
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(RootPath, false);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException)
        {
            AppLog.Write("Failed to reset settings.", ex);
        }
    }
}
