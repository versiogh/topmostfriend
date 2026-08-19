using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace TopMostFriend;

/// <summary>
/// Provides the application icon at the size Windows expects for a given surface.
/// The icon is embedded in the assembly so it also resolves inside a single-file build.
/// </summary>
public static class AppIcon
{
    private const string ResourceName = "TopMostFriend.TopMostFriend.ico";

    private static Icon? _window;

    /// <summary>
    /// Shared icon for dialog windows. Forms do not dispose the icon assigned to them,
    /// so a single instance can safely be reused by every window.
    /// </summary>
    public static Icon Window => _window ??= Load(SystemInformation.IconSize);

    /// <summary>
    /// Returns a new icon sized for the notification area at the DPI the taskbar is using.
    /// </summary>
    public static Icon CreateTrayIcon() => CreateTrayIcon(Win32.GetNotificationAreaDpi());

    /// <summary>
    /// Returns a new icon sized for the notification area at the given DPI. The shell scales
    /// whatever handle it receives, so supplying the wrong size is what makes the icon next to
    /// the clock look blurred at scaling factors above 100 %.
    /// </summary>
    public static Icon CreateTrayIcon(int dpi) => Load(Win32.GetSmallIconSizeForDpi(dpi));

    public static Icon Load(Size size)
    {
        try
        {
            using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
            if (stream != null)
                return new Icon(stream, size);
        }
        catch (Exception ex)
        {
            AppLog.Write("Embedded application icon could not be loaded.", ex);
        }

        try
        {
            Icon? associated = Icon.ExtractAssociatedIcon(ShellHelper.ExecutablePath);
            if (associated != null)
                return associated;
        }
        catch (Exception ex)
        {
            AppLog.Write("Application icon could not be extracted from the executable.", ex);
        }

        return (Icon)SystemIcons.Application.Clone();
    }

    /// <summary>
    /// Redraws an icon at the requested size and disposes the source. Icons collected from other
    /// windows come in whatever size that window published, which is rarely the size the
    /// notification area needs.
    /// </summary>
    public static Icon Rescale(Icon icon, Size size)
    {
        ArgumentNullException.ThrowIfNull(icon);

        if (size.Width <= 0 || size.Height <= 0 || icon.Size == size)
            return icon;

        try
        {
            using Bitmap bitmap = new(size.Width, size.Height, PixelFormat.Format32bppArgb);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.DrawIcon(icon, new Rectangle(Point.Empty, size));
            }

            IntPtr handle = bitmap.GetHicon();
            try
            {
                using Icon borrowed = Icon.FromHandle(handle);
                Icon result = (Icon)borrowed.Clone();
                icon.Dispose();
                return result;
            }
            finally
            {
                Win32.DestroyIcon(handle);
            }
        }
        catch (Exception ex)
        {
            AppLog.Write("Window icon could not be resized for the notification area.", ex);
            return icon;
        }
    }
}
