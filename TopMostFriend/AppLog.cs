using System;
using System.IO;
using System.Text;

namespace TopMostFriend;

public static class AppLog
{
    private const long MaxLogBytes = 1024 * 1024;

    private static readonly object Sync = new();

    public static string LogDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TopMostFriend");

    public static string LogPath => Path.Combine(LogDirectory, "topmostfriend.log");

    public static void Write(string message, Exception? exception = null)
    {
        try
        {
            lock (Sync)
            {
                Directory.CreateDirectory(LogDirectory);
                RotateIfOversized();
                StringBuilder text = new();
                text.Append('[').Append(DateTimeOffset.Now.ToString("O")).Append("] ").AppendLine(message);
                if (exception != null)
                    text.AppendLine(exception.ToString());
                File.AppendAllText(LogPath, text.ToString(), Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never be able to crash the tray utility.
        }
    }

    /// <summary>
    /// Keeps a single previous generation of the log. A background icon read that fails on every
    /// menu refresh would otherwise grow the file without any bound.
    /// </summary>
    private static void RotateIfOversized()
    {
        FileInfo current = new(LogPath);
        if (!current.Exists || current.Length < MaxLogBytes)
            return;

        string previous = LogPath + ".1";
        File.Delete(previous);
        File.Move(LogPath, previous);
    }
}
