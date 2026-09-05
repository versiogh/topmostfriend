using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TopMostFriend;

public static class Program
{
    public const string TITLE = "Top Most Friend";

    private const string MutexName = @"Local\{5BE25191-E1E2-48A7-B038-E986CD989E91}";
    private const string CustomLanguageFile = "TopMostFriendLanguage.xml";
    public const string FOREGROUND_HOTKEY_ATOM = "{86795D64-770D-4BD6-AA26-FA638FBAABCF}";

    public const string FOREGROUND_HOTKEY_SETTING = "ForegroundHotKey";
    public const string PROCESS_SEPARATOR_SETTING = "InsertProcessSeparator";
    public const string LIST_SELF_SETTING = "ListSelf";
    public const string SHOW_EMPTY_WINDOW_SETTING = "ShowEmptyWindowTitles";
    public const string LIST_BACKGROUND_PATH_SETTING = "ListBackgroundPath";
    public const string LIST_BACKGROUND_LAYOUT_SETTING = "ListBackgroundLayout";
    public const string ALWAYS_ADMIN_SETTING = "RunAsAdministrator";
    public const string TOGGLE_BALLOON_SETTING = "ShowNotificationOnHotKey";
    public const string SHIFT_CLICK_BLACKLIST = "ShiftClickToBlacklist";
    public const string TITLE_BLACKLIST = "TitleBlacklist";
    public const string SHOW_HOTKEY_ICON = "ShowHotkeyIcon";
    public const string SHOW_WINDOW_LIST = "ShowWindowList";
    public const string SHOW_MENU_LEFT_CLICK = "ShowMenuLeftClick";
    public const string SHOW_MENU_RIGHT_CLICK = "ShowMenuRightClick";
    public const string LAST_VERSION = "LastVersion";
    public const string ALWAYS_RETRY_ELEVATED = "AlwaysRetryElevated";
    public const string REVERT_ON_EXIT = "RevertOnExit";
    public const string LANGUAGE = "Language";

    // Windows 10/11 notifications are more intrusive than old tray balloons, so default off.
    public static readonly bool ToggleBalloonDefault = false;

    private static Mutex? _singleInstanceMutex;
    private static bool _ownsSingleInstanceMutex;
    private static NotifyIcon? _trayIcon;
    private static ContextMenuStrip? _contextMenu;
    private static HotKeyWindow? _hotKeys;
    private static ToolStripMenuItem? _refreshButton;
    private static ToolStripItem[] _listActionItems = Array.Empty<ToolStripItem>();
    private static ToolStripItem[] _appActionItems = Array.Empty<ToolStripItem>();
    private static readonly List<string> TitleBlacklist = new();
    private static readonly Dictionary<WindowIdentity, bool> OriginalStates = new();

    private static Icon? _originalIcon;
    private static Icon? _temporaryIcon;
    private static System.Windows.Forms.Timer? _iconResetTimer;
    private static CancellationTokenSource? _menuIconCancellation;
    private static int _menuGeneration;
    private static int _registeredHotKeyCode;
    private static int _trayIconDpi = Win32.DEFAULT_DPI;
    private static bool _listBuiltBeforeOpening;
    private static bool _systemEventsAttached;
    private static bool _keepMenuOpenOnce;
    private static bool _restartRequested;
    private static int _shutdownStarted;

    private static readonly MethodInfo? ContextMenuShowInTaskbarMethod =
        typeof(ContextMenuStrip).GetMethod(
            "ShowInTaskbar",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: new[] { typeof(int), typeof(int) },
            modifiers: null);

    private static readonly object DynamicSeparatorTag = new();

    private readonly record struct WindowIdentity(IntPtr Handle, uint ProcessId, uint ThreadId);
    private readonly record struct ToggleOutcome(bool Succeeded, bool NewState)
    {
        public static ToggleOutcome Failed => new(false, false);
    }
    private sealed record WindowMenuTag(WindowInfo Window, string TitleSnapshot);
    private sealed record IconJob(WindowInfo Window, ToolStripMenuItem Item);

    [STAThread]
    public static int Main(string[] args)
    {
        int? helperResult = HandleHelperCommandLine(args);
        if (helperResult.HasValue)
            return helperResult.Value;

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.ThreadException += (_, e) =>
        {
            AppLog.Write("Unhandled WinForms exception.", e.Exception);
            MessageBox.Show(
                Locale.String("ErrUnhandled", AppLog.LogPath),
                TITLE,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                AppLog.Write("Unhandled application exception.", ex);
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLog.Write("Unobserved background task exception.", e.Exception);
            e.SetObserved();
        };

        if (args.Contains("--reset-admin", StringComparer.OrdinalIgnoreCase))
            Settings.Remove(ALWAYS_ADMIN_SETTING);

        bool firstRun = !Settings.Has(FOREGROUND_HOTKEY_SETTING);
        InitialiseDefaults();
        InitialiseLocale();
        InitialiseTitleBlacklist();
        Settings.Set(LAST_VERSION, Application.ProductVersion);

        if (!AcquireSingleInstance())
        {
            MessageBox.Show(Locale.String("AlreadyRunning"), TITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return -1;
        }

        if (Settings.Get(ALWAYS_ADMIN_SETTING, false) && !UAC.IsElevated)
        {
            ReleaseSingleInstance();
            if (UAC.TryRestartElevated())
                return -2;

            // UAC was cancelled. Continue non-elevated rather than leaving the user with no tray app.
            if (!AcquireSingleInstance())
                return -1;
        }

        try
        {
            InitialiseTrayApplication();

            _hotKeys = new HotKeyWindow();
            SetForegroundHotKey(Settings.Get(FOREGROUND_HOTKEY_SETTING, 0), persist: false, showError: false);

            if (firstRun)
                FirstRunWindow.Display();

            Application.Run();

            if (Settings.Get(REVERT_ON_EXIT, false))
                RevertTopMostStatus();
        }
        catch (Exception ex)
        {
            AppLog.Write("Fatal startup/runtime error.", ex);
            MessageBox.Show(
                Locale.String("ErrFatal", AppLog.LogPath, ex.Message),
                TITLE,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return -10;
        }
        finally
        {
            Shutdown();
        }

        if (_restartRequested)
            ShellHelper.StartCurrentProcess();

        return 0;
    }

    private static void InitialiseDefaults()
    {
        Settings.SetDefault(FOREGROUND_HOTKEY_SETTING, 0);
        Settings.SetDefault(ALWAYS_ADMIN_SETTING, false);
        Settings.SetDefault(SHIFT_CLICK_BLACKLIST, true);
        Settings.SetDefault(SHOW_HOTKEY_ICON, true);
        Settings.SetDefault(SHOW_WINDOW_LIST, true);
        Settings.SetDefault(ALWAYS_RETRY_ELEVATED, false);
        Settings.SetDefault(REVERT_ON_EXIT, false);
        Settings.SetDefault(TOGGLE_BALLOON_SETTING, ToggleBalloonDefault);
        Settings.SetDefault(SHOW_EMPTY_WINDOW_SETTING, false);
        Settings.SetDefault(PROCESS_SEPARATOR_SETTING, false);
        Settings.SetDefault(LIST_SELF_SETTING, false);
        Settings.SetDefault(SHOW_MENU_LEFT_CLICK, true);
        Settings.SetDefault(SHOW_MENU_RIGHT_CLICK, true);
    }

    private static void InitialiseLocale()
    {
        string customPath = Path.Combine(AppContext.BaseDirectory, CustomLanguageFile);
        if (File.Exists(customPath))
        {
            try
            {
                using Stream stream = File.OpenRead(customPath);
                string customLanguage = Locale.LoadLanguage(stream, replaceExisting: true);
                Locale.SetLanguage(customLanguage);
                return;
            }
            catch (Exception ex)
            {
                AppLog.Write("Custom language file could not be loaded; falling back to a built-in language.", ex);
            }
        }

        Locale.SetLanguage(Locale.GetPreferredLanguage());
    }

    private static void InitialiseTitleBlacklist()
    {
        if (!Settings.Has(TITLE_BLACKLIST))
        {
            Settings.Set(TITLE_BLACKLIST, new[]
            {
                "Program Manager",
                "Windows Shell Experience Host",
                "Start",
            });
        }

        ApplyBlacklistedTitles(Settings.Get(TITLE_BLACKLIST, Array.Empty<string>()) ?? Array.Empty<string>());
    }

    private static void InitialiseTrayApplication()
    {
        _trayIconDpi = Win32.GetNotificationAreaDpi();
        _originalIcon = AppIcon.CreateTrayIcon(_trayIconDpi);

        _contextMenu = new ContextMenuStrip();
        _contextMenu.Opening += ContextMenu_Opening;
        _contextMenu.ItemClicked += ContextMenu_ItemClicked;
        _contextMenu.Closing += ContextMenu_Closing;

        _refreshButton = new ToolStripMenuItem(Locale.String("TrayRefresh"));
        _refreshButton.Click += (_, _) =>
        {
            if (_hotKeys != null && !_hotKeys.IsDisposed)
                _hotKeys.BeginInvoke(new Action(RefreshWindowList));
        };

        ToolStripMenuItem settings = new(Locale.String("TraySettings"));
        settings.Click += (_, _) => SettingsWindow.Display();

        ToolStripMenuItem about = new(Locale.String("TrayAbout"));
        about.Click += (_, _) => AboutWindow.Display();

        ToolStripMenuItem quit = new(Locale.String("TrayQuit"));
        quit.Click += (_, _) => Application.Exit();

        ToolStripMenuItem actions = new(Locale.String("TrayActions"));
        actions.DropDownItems.Add(_refreshButton);
        actions.DropDownItems.Add(settings);
        actions.DropDownItems.Add(about);
        actions.DropDownItems.Add(quit);

        _listActionItems = new ToolStripItem[]
        {
        new ToolStripSeparator(),
        actions,
        };

        _appActionItems = Array.Empty<ToolStripItem>();

        _contextMenu.Items.AddRange(_appActionItems);

        _trayIcon = new NotifyIcon
        {
            Visible = true,
            Icon = _originalIcon,
            Text = TITLE,
            ContextMenuStrip = null,
        };

        ApplyTrayMenuMouseSettings();

        _trayIcon.MouseDown += TrayIcon_MouseDown;

        _iconResetTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _iconResetTimer.Tick += (_, _) => ResetTrayIcon();

        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
        _systemEventsAttached = true;
    }

    internal static void ApplyTrayMenuMouseSettings()
    {
        NotifyIcon? trayIcon = _trayIcon;
        if (trayIcon == null)
            return;

        trayIcon.ContextMenuStrip =
            Settings.Get(SHOW_MENU_RIGHT_CLICK, true)
                ? _contextMenu
                : null;
    }

    private static void TrayIcon_MouseDown(object? sender, MouseEventArgs e)
    {
        if ((e.Button & MouseButtons.Left) == 0 ||
            !Settings.Get(SHOW_MENU_LEFT_CLICK, true))
            return;

        // Build the window list before showing the menu so that its final size is
        // known before WinForms positions the drop-down.
        try
        {
            RefreshWindowList();
            _listBuiltBeforeOpening = true;
        }
        catch (Exception ex)
        {
            AppLog.Write("Failed to build the tray menu.", ex);
        }

        // This is the same foreground-window handling used by NotifyIcon itself
        // before displaying its native context menu.
        if (_hotKeys != null && !_hotKeys.IsDisposed && _hotKeys.IsHandleCreated)
            Win32.SetForegroundWindow(_hotKeys.Handle);

        if (_contextMenu == null || _contextMenu.IsDisposed)
            return;

        Point cursor = Cursor.Position;

        try
        {
            // NotifyIcon uses ContextMenuStrip.ShowInTaskbar() for its native
            // right-click menu. This internal method deliberately allows the
            // menu to overlap the taskbar and applies the same positioning logic.
            if (ContextMenuShowInTaskbarMethod != null)
            {
                ContextMenuShowInTaskbarMethod.Invoke(
                    _contextMenu,
                    new object[] { cursor.X, cursor.Y });
            }
            else
            {
                // Fallback for a future WinForms version where the internal
                // method might no longer exist.
                _contextMenu.Show(
                    cursor,
                    ToolStripDropDownDirection.AboveLeft);
            }
        }
        catch (Exception ex)
        {
            AppLog.Write("Failed to show the tray menu on left click.", ex);

            if (!_contextMenu.IsDisposed)
            {
                try
                {
                    _contextMenu.Show(
                        cursor,
                        ToolStripDropDownDirection.AboveLeft);
                }
                catch (Exception fallbackEx)
                {
                    AppLog.Write("Fallback tray menu display also failed.", fallbackEx);
                }
            }
        }
    }

    private static void ContextMenu_Opening(object? sender, CancelEventArgs e)
    {
        // Already built by the mouse handler; rebuilding here would resize the drop-down after
        // its position has been calculated.
        if (_listBuiltBeforeOpening)
        {
            _listBuiltBeforeOpening = false;
            return;
        }

        try
        {
            RefreshWindowList();
        }
        catch (Exception ex)
        {
            AppLog.Write("Failed to refresh tray menu.", ex);
        }
    }

    private static void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e) =>
        PostToUi(RefreshTrayIconForCurrentDpi);

    private static void SystemEvents_UserPreferenceChanged(object? sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
    {
        if (e.Category == Microsoft.Win32.UserPreferenceCategory.Desktop ||
            e.Category == Microsoft.Win32.UserPreferenceCategory.General ||
            e.Category == Microsoft.Win32.UserPreferenceCategory.Icon ||
            e.Category == Microsoft.Win32.UserPreferenceCategory.VisualStyle)
        {
            PostToUi(RefreshTrayIconForCurrentDpi);
        }
    }

    /// <summary>
    /// Rebuilds the notification-area icon when the taskbar DPI changes. The icon is created once
    /// at startup, so moving the taskbar to a monitor with a different scaling factor otherwise
    /// leaves the shell stretching an icon of the wrong size.
    /// </summary>
    private static void RefreshTrayIconForCurrentDpi()
    {
        if (_trayIcon == null)
            return;

        int dpi = Win32.GetNotificationAreaDpi();
        if (dpi == _trayIconDpi && _originalIcon != null)
            return;

        Icon updated;
        try
        {
            updated = AppIcon.CreateTrayIcon(dpi);
        }
        catch (Exception ex)
        {
            AppLog.Write("Tray icon could not be rebuilt for the current DPI.", ex);
            return;
        }

        _trayIconDpi = dpi;
        Icon? previous = _originalIcon;
        _originalIcon = updated;

        if (_temporaryIcon == null)
            _trayIcon.Icon = _originalIcon;

        previous?.Dispose();
    }

    private static void ContextMenu_ItemClicked(object? sender, ToolStripItemClickedEventArgs e)
    {
        // ItemClicked is raised before Closing. Using the item's Click event here is too late
        // on some WinForms versions and the Refresh command would close the menu.
        if (ReferenceEquals(e.ClickedItem, _refreshButton))
            _keepMenuOpenOnce = true;
    }

    private static void ContextMenu_Closing(object? sender, ToolStripDropDownClosingEventArgs e)
    {
        if (_keepMenuOpenOnce && e.CloseReason == ToolStripDropDownCloseReason.ItemClicked)
        {
            _keepMenuOpenOnce = false;
            e.Cancel = true;
        }
        else if (e.CloseReason != ToolStripDropDownCloseReason.ItemClicked)
        {
            _keepMenuOpenOnce = false;
        }
    }

    private static void RefreshWindowList()
    {
        if (_contextMenu == null || _contextMenu.IsDisposed)
            return;

        int generation = Interlocked.Increment(ref _menuGeneration);
        _menuIconCancellation?.Cancel();
        CancellationTokenSource cancellation = new();
        _menuIconCancellation = cancellation;
        CancellationToken token = cancellation.Token;

        DisposeDynamicMenuItems();
        List<ToolStripItem> items = new();
        List<IconJob> iconJobs = new();

        if (Settings.Get(SHOW_WINDOW_LIST, true))
        {
            IReadOnlyList<WindowInfo> windows = WindowInfo.GetAllWindows();
            bool separateProcesses = Settings.Get(PROCESS_SEPARATOR_SETTING, false);
            bool showEmptyTitles = Settings.Get(SHOW_EMPTY_WINDOW_SETTING, false);
            bool listSelf = Settings.Get(LIST_SELF_SETTING, false);
            uint? lastProcessId = null;

            foreach (WindowInfo window in windows)
            {
                if (!listSelf && window.IsOwnWindow)
                    continue;

                string title = window.Title;
                if (!showEmptyTitles && string.IsNullOrEmpty(title))
                    continue;
                if (CheckBlacklistedTitles(title))
                    continue;

                if (separateProcesses && lastProcessId.HasValue && lastProcessId.Value != window.ProcessId)
                    items.Add(new ToolStripSeparator { Tag = DynamicSeparatorTag });
                lastProcessId = window.ProcessId;

                string displayTitle = string.IsNullOrEmpty(title) ? Locale.String("TrayWindowNoTitle") : title;
                string menuText = EscapeMenuText(Truncate(displayTitle, 140));

                ToolStripMenuItem item = new(menuText)
                {
                    Checked = window.IsTopMost,
                    CheckOnClick = false,
                    Tag = new WindowMenuTag(window, title),
                    ToolTipText = displayTitle,
                };
                item.Click += WindowMenuItem_Click;
                items.Add(item);
                iconJobs.Add(new IconJob(window, item));
            }

            items.AddRange(_listActionItems);
        }

        items.AddRange(_appActionItems);

        _contextMenu.SuspendLayout();
        try
        {
            _contextMenu.Items.Clear();
            _contextMenu.Items.AddRange(items.ToArray());
            ApplyMenuSizeLimit();
        }
        finally
        {
            _contextMenu.ResumeLayout(true);
        }

        _contextMenu.PerformLayout();
        if (_contextMenu.Visible)
            _contextMenu.Refresh();

        _ = LoadMenuIconsAsync(iconJobs, generation, cancellation);
    }

    /// <summary>
    /// Keeps the drop-down inside the working area of the screen the pointer is on. Without a
    /// bound, a machine with many open windows produces a menu taller than the display, which
    /// the shell then draws clipped over the taskbar.
    /// </summary>
    private static void ApplyMenuSizeLimit()
    {
        if (_contextMenu == null)
            return;

        try
        {
            Rectangle working = Screen.FromPoint(Cursor.Position).WorkingArea;
            int maxHeight = Math.Max(240, working.Height - 24);
            if (_contextMenu.MaximumSize.Height != maxHeight)
                _contextMenu.MaximumSize = new Size(0, maxHeight);
        }
        catch (Exception ex)
        {
            AppLog.Write("Could not constrain the tray menu height.", ex);
        }
    }

    private static async void WindowMenuItem_Click(object? sender, EventArgs e)
    {
        if (sender is not ToolStripMenuItem item || item.Tag is not WindowMenuTag tag)
            return;

        WindowInfo window = tag.Window;
        if (Settings.Get(SHIFT_CLICK_BLACKLIST, true) && Control.ModifierKeys.HasFlag(Keys.Shift))
        {
            AddBlacklistedTitle(tag.TitleSnapshot);
            SaveBlacklistedTitles();
            return;
        }

        try
        {
            ToggleOutcome outcome = await ToggleWindowAsync(window, activateWhenPinned: true);
            if (item.IsDisposed)
                return;

            if (outcome.Succeeded)
                item.Checked = outcome.NewState;
            else if (window.IsValid)
                item.Checked = window.IsTopMost;
        }
        catch (Exception ex)
        {
            AppLog.Write("Failed to toggle a window from the tray menu.", ex);
        }
    }

    private static async Task LoadMenuIconsAsync(IReadOnlyList<IconJob> jobs, int generation, CancellationTokenSource cancellation)
    {
        CancellationToken token = cancellation.Token;

        try
        {
            foreach (IconJob job in jobs)
            {
                token.ThrowIfCancellationRequested();
                Icon? icon = await Task.Run(() => job.Window.TryGetIconClone(80), token).ConfigureAwait(false);
                if (icon == null)
                    continue;

                Bitmap bitmap;
                using (icon)
                    bitmap = icon.ToBitmap();

                if (token.IsCancellationRequested)
                {
                    bitmap.Dispose();
                    break;
                }

                bool posted = PostToUi(() =>
                {
                    if (generation != _menuGeneration ||
                        _contextMenu == null ||
                        _contextMenu.IsDisposed ||
                        job.Item.Owner != _contextMenu)
                    {
                        bitmap.Dispose();
                        return;
                    }

                    Image? old = job.Item.Image;
                    job.Item.Image = bitmap;
                    old?.Dispose();
                });

                if (!posted)
                    bitmap.Dispose();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            AppLog.Write("Background window-icon loading failed.", ex);
        }
        finally
        {
            Interlocked.CompareExchange(ref _menuIconCancellation, null, cancellation);
            cancellation.Dispose();
        }
    }

    private static bool PostToUi(Action action)
    {
        try
        {
            if (_hotKeys == null || _hotKeys.IsDisposed || !_hotKeys.IsHandleCreated)
                return false;
            _hotKeys.BeginInvoke(action);
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void DisposeDynamicMenuItems()
    {
        if (_contextMenu == null)
            return;

        ToolStripItem[] dynamicItems = _contextMenu.Items
            .Cast<ToolStripItem>()
            .Where(item => item.Tag is WindowMenuTag || ReferenceEquals(item.Tag, DynamicSeparatorTag))
            .ToArray();

        foreach (ToolStripItem item in dynamicItems)
        {
            _contextMenu.Items.Remove(item);
            Image? image = item.Image;
            item.Image = null;
            image?.Dispose();
        }
    }

    private static async Task<ToggleOutcome> ToggleWindowAsync(WindowInfo window, bool activateWhenPinned)
    {
        if (!window.IsValid)
            return ToggleOutcome.Failed;

        bool originalState = window.IsTopMost;
        bool targetState = !originalState;

        // The common case does not touch the elevated helper and completes synchronously, so no
        // continuation is scheduled and the menu item is updated on the same message.
        if (window.TrySetTopMost(targetState))
        {
            RecordOriginalState(window, originalState);
            if (targetState && activateWhenPinned)
                window.TryActivate();
            return new ToggleOutcome(true, targetState);
        }

        return await TryElevatedToggleAsync(window, originalState, targetState, activateWhenPinned);
    }

    private static async Task<ToggleOutcome> TryElevatedToggleAsync(
        WindowInfo window,
        bool originalState,
        bool targetState,
        bool activateWhenPinned)
    {
        if (UAC.IsElevated)
        {
            ShowProtectedWindowError();
            return ToggleOutcome.Failed;
        }

        // The drop-down must not stay on screen while a dialog and the UAC prompt are displayed;
        // an open menu that cannot repaint is what leaves a black rectangle next to the clock.
        _contextMenu?.Close(ToolStripDropDownCloseReason.AppFocusChange);

        bool retryElevated = Settings.Get(ALWAYS_RETRY_ELEVATED, false);
        if (!retryElevated)
        {
            string message = Locale.String("ErrUnableAlterStatus1") + Environment.NewLine + Locale.String("ErrUnableAlterStatus2");
            retryElevated = MessageBox.Show(message, TITLE, MessageBoxButtons.YesNo, MessageBoxIcon.Error) == DialogResult.Yes;
        }

        if (!retryElevated)
            return ToggleOutcome.Failed;

        // The helper is waited for off the UI thread. Waiting inline froze the whole application,
        // including the tray menu and the icon, for as long as the helper took to exit.
        int result = await UAC.SetWindowTopMostElevatedAsync(window, targetState, targetState && activateWhenPinned)
            .ConfigureAwait(true);

        if (result == UAC.ErrorCancelled)
            return ToggleOutcome.Failed;

        if (window.IsValid && window.IsTopMost == targetState)
        {
            RecordOriginalState(window, originalState);
            return new ToggleOutcome(true, targetState);
        }

        ShowProtectedWindowError();
        return ToggleOutcome.Failed;
    }

    private static void ShowProtectedWindowError() =>
        MessageBox.Show(Locale.String("ErrUnableAlterStatusProtected"), TITLE, MessageBoxButtons.OK, MessageBoxIcon.Error);

    private static void RecordOriginalState(WindowInfo window, bool state)
    {
        WindowIdentity identity = new(window.Handle, window.ProcessId, window.ThreadId);
        if (!OriginalStates.ContainsKey(identity))
            OriginalStates[identity] = state;
    }

    public static async void ToggleForegroundWindow()
    {
        try
        {
            WindowInfo? window = WindowInfo.GetForegroundWindow();
            if (window == null)
                return;

            ToggleOutcome outcome = await ToggleWindowAsync(window, activateWhenPinned: false);
            if (!outcome.Succeeded)
                return;

            if (Settings.Get(TOGGLE_BALLOON_SETTING, false) && _trayIcon != null)
            {
                string title = window.Title;
                _trayIcon.ShowBalloonTip(
                    2000,
                    Locale.String(outcome.NewState ? "NotifyOnTop" : "NotifyNoLonger"),
                    string.IsNullOrEmpty(title) ? Locale.String("NotifyNoTitle") : title,
                    ToolTipIcon.Info);
            }

            if (Settings.Get(SHOW_HOTKEY_ICON, true))
                _ = ShowTemporaryWindowIconAsync(window);
        }
        catch (Exception ex)
        {
            AppLog.Write("Failed to toggle the foreground window.", ex);
        }
    }

    private static async Task ShowTemporaryWindowIconAsync(WindowInfo window)
    {
        try
        {
            Icon? icon = await Task.Run(() => window.TryGetIconClone(80)).ConfigureAwait(false);
            if (icon == null)
                return;

            if (!PostToUi(() => SetTemporaryTrayIcon(icon)))
                icon.Dispose();
        }
        catch (Exception ex)
        {
            AppLog.Write("Unable to show temporary hot-key icon.", ex);
        }
    }

    private static void SetTemporaryTrayIcon(Icon icon)
    {
        if (_trayIcon == null)
        {
            icon.Dispose();
            return;
        }

        // Other windows publish their icon at whatever size they chose, usually 32x32 or larger.
        icon = AppIcon.Rescale(icon, Win32.GetSmallIconSizeForDpi(_trayIconDpi));

        _iconResetTimer?.Stop();
        _trayIcon.Icon = icon;
        _temporaryIcon?.Dispose();
        _temporaryIcon = icon;
        _iconResetTimer?.Start();
    }

    private static void ResetTrayIcon()
    {
        _iconResetTimer?.Stop();
        if (_trayIcon != null && _originalIcon != null)
            _trayIcon.Icon = _originalIcon;
        _temporaryIcon?.Dispose();
        _temporaryIcon = null;
    }

    public static bool SetForegroundHotKey(int keyCode, bool persist = true, bool showError = false)
    {
        // A main key and at least one modifier are required, matching the original app's behaviour.
        Win32ModKeys modifiers = (Win32ModKeys)(keyCode & 0xFFFF);
        Keys key = (Keys)((keyCode >> 16) & 0xFFFF);
        if (modifiers == 0 || key == Keys.None)
            keyCode = 0;

        if (_hotKeys == null)
        {
            if (persist)
                Settings.Set(FOREGROUND_HOTKEY_SETTING, keyCode);
            _registeredHotKeyCode = keyCode;
            return true;
        }

        int oldCode = _registeredHotKeyCode;
        if (oldCode == keyCode)
        {
            if (persist)
                Settings.Set(FOREGROUND_HOTKEY_SETTING, keyCode);
            return true;
        }

        _hotKeys.Unregister(FOREGROUND_HOTKEY_ATOM);

        try
        {
            RegisterHotKeyCode(keyCode);
            _registeredHotKeyCode = keyCode;
            if (persist)
                Settings.Set(FOREGROUND_HOTKEY_SETTING, keyCode);
            return true;
        }
        catch (Win32Exception ex)
        {
            AppLog.Write("Hot-key registration failed.", ex);

            try
            {
                RegisterHotKeyCode(oldCode);
                _registeredHotKeyCode = oldCode;
            }
            catch (Exception restoreEx)
            {
                _registeredHotKeyCode = 0;
                AppLog.Write("Previous hot key could not be restored.", restoreEx);
            }

            if (showError)
            {
                MessageBox.Show(
                    Locale.String("ErrHotKeyUnavailable"),
                    TITLE,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            return false;
        }
    }

    private static void RegisterHotKeyCode(int keyCode)
    {
        if (_hotKeys == null || keyCode == 0)
            return;

        Win32ModKeys modifiers = (Win32ModKeys)(keyCode & 0xFFFF);
        Keys key = (Keys)((keyCode >> 16) & 0xFFFF);
        _hotKeys.Register(FOREGROUND_HOTKEY_ATOM, modifiers, key, ToggleForegroundWindow);
    }

    public static void RevertTopMostStatus()
    {
        foreach (KeyValuePair<WindowIdentity, bool> pair in OriginalStates.ToArray())
        {
            WindowIdentity identity = pair.Key;
            bool originalState = pair.Value;
            try
            {
                WindowInfo window = new(identity.Handle, identity.ProcessId, identity.ThreadId);
                if (!window.IsValid)
                    continue;
                if (window.IsTopMost != originalState && !window.TrySetTopMost(originalState))
                    AppLog.Write($"Could not restore top-most state for HWND {identity.Handle}.");
            }
            catch (Exception ex)
            {
                AppLog.Write("Error while restoring a window's original top-most state.", ex);
            }
        }
    }

    public static void AddBlacklistedTitle(string title)
    {
        lock (TitleBlacklist)
        {
            if (!TitleBlacklist.Contains(title, StringComparer.Ordinal))
                TitleBlacklist.Add(title);
        }
    }

    public static void RemoveBlacklistedTitle(string title)
    {
        lock (TitleBlacklist)
            TitleBlacklist.RemoveAll(x => string.Equals(x, title, StringComparison.Ordinal));
    }

    public static void ApplyBlacklistedTitles(IEnumerable<string> titles)
    {
        lock (TitleBlacklist)
        {
            TitleBlacklist.Clear();
            TitleBlacklist.AddRange(titles.Distinct(StringComparer.Ordinal));
        }
    }

    public static bool CheckBlacklistedTitles(string title)
    {
        lock (TitleBlacklist)
            return TitleBlacklist.Contains(title, StringComparer.Ordinal);
    }

    public static string[] GetBlacklistedTitles()
    {
        lock (TitleBlacklist)
            return TitleBlacklist.ToArray();
    }

    public static void SaveBlacklistedTitles()
    {
        lock (TitleBlacklist)
            Settings.Set(TITLE_BLACKLIST, TitleBlacklist.ToArray());
    }

    public static void RequestRestart()
    {
        _restartRequested = true;
        Application.Exit();
    }

    public static void Shutdown()
    {
        if (Interlocked.Exchange(ref _shutdownStarted, 1) != 0)
            return;

        try
        {
            if (_systemEventsAttached)
            {
                Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
                Microsoft.Win32.SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
                _systemEventsAttached = false;
            }

            _menuIconCancellation?.Cancel();
            _menuIconCancellation = null;

            _iconResetTimer?.Stop();
            _iconResetTimer?.Dispose();
            _iconResetTimer = null;

            ResetTrayIcon();

            if (_trayIcon != null)
            {
                _trayIcon.Visible = false;
                _trayIcon.Dispose();
                _trayIcon = null;
            }

            if (_contextMenu != null)
            {
                DisposeDynamicMenuItems();
                _contextMenu.Dispose();
                _contextMenu = null;
            }

            _hotKeys?.Dispose();
            _hotKeys = null;

            _temporaryIcon?.Dispose();
            _temporaryIcon = null;
            _originalIcon?.Dispose();
            _originalIcon = null;
        }
        catch (Exception ex)
        {
            AppLog.Write("Error while shutting down.", ex);
        }
        finally
        {
            ReleaseSingleInstance();
            _singleInstanceMutex?.Dispose();
            _singleInstanceMutex = null;
        }
    }

    private static bool AcquireSingleInstance()
    {
        _singleInstanceMutex ??= new Mutex(false, MutexName);
        if (_ownsSingleInstanceMutex)
            return true;

        try
        {
            _ownsSingleInstanceMutex = _singleInstanceMutex.WaitOne(0, false);
        }
        catch (AbandonedMutexException)
        {
            _ownsSingleInstanceMutex = true;
        }

        return _ownsSingleInstanceMutex;
    }

    private static void ReleaseSingleInstance()
    {
        if (!_ownsSingleInstanceMutex || _singleInstanceMutex == null)
            return;

        try
        {
            _singleInstanceMutex.ReleaseMutex();
        }
        catch (ApplicationException ex)
        {
            AppLog.Write("Single-instance mutex was not owned during release.", ex);
        }
        finally
        {
            _ownsSingleInstanceMutex = false;
        }
    }

    private static int? HandleHelperCommandLine(string[] args)
    {
        string? setValue = GetArgument(args, "--set-topmost=");
        if (setValue != null)
        {
            string? hwndValue = GetArgument(args, "--hwnd=");
            if (!int.TryParse(setValue, out int state) || (state != 0 && state != 1) ||
                !TryParseWindowHandle(hwndValue, out IntPtr handle))
                return 1;

            uint.TryParse(GetArgument(args, "--pid="), out uint expectedPid);
            uint.TryParse(GetArgument(args, "--tid="), out uint expectedTid);
            bool activate = GetArgument(args, "--activate=") == "1";

            WindowInfo window = expectedPid != 0 && expectedTid != 0
                ? new WindowInfo(handle, expectedPid, expectedTid)
                : new WindowInfo(handle);

            if (!window.IsValid)
                return 3;
            if (!window.TrySetTopMost(state == 1))
                return 2;
            if (state == 1 && activate)
                window.TryActivate();
            return 0;
        }

        string[] legacyToggles = args
            .Where(a => a.StartsWith("--toggle=", StringComparison.OrdinalIgnoreCase))
            .Select(a => a.Substring("--toggle=".Length))
            .ToArray();

        if (legacyToggles.Length > 0)
        {
            foreach (string value in legacyToggles)
            {
                if (!TryParseWindowHandle(value, out IntPtr handle))
                    return 1;

                WindowInfo window = new(handle);
                bool activate = !args.Contains($"--background={value}", StringComparer.OrdinalIgnoreCase);
                if (!window.TryToggleTopMost(activate, out _, out _))
                    return 2;
            }
            return 0;
        }

        return null;
    }

    private static bool TryParseWindowHandle(string? value, out IntPtr handle)
    {
        handle = IntPtr.Zero;
        if (!long.TryParse(value, out long raw))
            return false;

        try
        {
            if (IntPtr.Size == 4 && (raw < int.MinValue || raw > uint.MaxValue))
                return false;

            handle = IntPtr.Size == 4
                ? new IntPtr(unchecked((int)raw))
                : new IntPtr(raw);
            return handle != IntPtr.Zero;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static string? GetArgument(IEnumerable<string> args, string prefix) =>
        args.FirstOrDefault(a => a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?.Substring(prefix.Length);

    private static string EscapeMenuText(string text) => text.Replace("&", "&&", StringComparison.Ordinal);

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text.Substring(0, maxLength - 1) + "…";
}
