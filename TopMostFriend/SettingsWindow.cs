using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TopMostFriend.Languages;

namespace TopMostFriend;

public sealed class SettingsWindow : Form
{
    public static SettingsWindow? Instance { get; private set; }

    private int _keyCode;
    private bool _updatingHotKey;
    private readonly TextBox _keyText;
    private readonly CheckBox _ctrl;
    private readonly CheckBox _alt;
    private readonly CheckBox _shift;
    private readonly CheckBox _win;

    private readonly CheckBox _alwaysAdmin;
    private readonly CheckBox _toggleNotification;
    private readonly CheckBox _shiftClickBlacklist;
    private readonly CheckBox _showHotkeyIcon;
    private readonly CheckBox _playHotkeySound;
    private readonly CheckBox _showWindowList;
    private readonly CheckBox _showMenuLeftClick;
    private readonly CheckBox _showMenuRightClick;
    private readonly CheckBox _alwaysRetryAsAdmin;
    private readonly CheckBox _revertOnExit;
    private readonly CheckBox _showEmptyTitles;
    private readonly ComboBox _language;

    public static void Display()
    {
        if (Instance != null && !Instance.IsDisposed)
        {
            if (Instance.WindowState == FormWindowState.Minimized)
                Instance.WindowState = FormWindowState.Normal;
            Instance.Show();
            Instance.BringToFront();
            Instance.Activate();
            return;
        }

        Instance = new SettingsWindow();
        Instance.Show();
    }

    private SettingsWindow()
    {
        Text = Locale.String("SettingsTitle");
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        ClientSize = new Size(560, 568);
        MaximizeBox = false;
        MinimizeBox = false;
        TopMost = true;
        Icon = AppIcon.Window;

        _keyCode = Settings.Get(Program.FOREGROUND_HOTKEY_SETTING, 0);

        GroupBox hotKeyGroup = new()
        {
            Text = Locale.String("SettingsHotKeysTitle"),
            Location = new Point(10, 10),
            Size = new Size(540, 100),
        };
        hotKeyGroup.Controls.Add(new Label
        {
            Text = Locale.String("SettingsHotKeysToggle"),
            Location = new Point(12, 22),
            AutoSize = true,
        });

        _keyText = new TextBox
        {
            Location = new Point(12, 53),
            Width = 120,
            ReadOnly = true,
            TabStop = true,
        };
        _keyText.KeyDown += KeyText_KeyDown;

        _ctrl = CreateModifier("CTRL", 140, hotKeyGroup);
        _win = CreateModifier("WIN", 198, hotKeyGroup);
        _alt = CreateModifier("ALT", 256, hotKeyGroup);
        _shift = CreateModifier("SHIFT", 314, hotKeyGroup, 68);

        Button resetHotKey = new()
        {
            Text = Locale.String("SettingsHotKeysReset"),
            Location = new Point(458, 51),
            Size = new Size(70, 27),
        };
        resetHotKey.Click += (_, _) => ResetHotKey();
        hotKeyGroup.Controls.AddRange(new Control[] { _keyText, resetHotKey });
        LoadHotKeyControls();

        GroupBox options = new()
        {
            Text = Locale.String("SettingsOptionsTitle"),
            Location = new Point(10, 118),
            Size = new Size(540, 268),
        };

        _toggleNotification = AddOption(options, Locale.String("SettingsOptionsToggleNotify"), 22, Settings.Get(Program.TOGGLE_BALLOON_SETTING, Program.ToggleBalloonDefault));
        _showHotkeyIcon = AddOption(options, Locale.String("SettingsOptionsToggleNotifyIcon"), 46, Settings.Get(Program.SHOW_HOTKEY_ICON, true));
        _playHotkeySound = AddOption(options, Locale.String("SettingsOptionsHotkeySound"), 70, Settings.Get(Program.HOTKEY_SOUND, false));
        _alwaysRetryAsAdmin = AddOption(options, Locale.String("SettingsOptionsElevatedRetry"), 94, Settings.Get(Program.ALWAYS_RETRY_ELEVATED, false));
        _shiftClickBlacklist = AddOption(options, Locale.String("SettingsOptionsShiftBlacklist"), 118, Settings.Get(Program.SHIFT_CLICK_BLACKLIST, true));
        _revertOnExit = AddOption(options, Locale.String("SettingsOptionsRevertOnExit"), 142, Settings.Get(Program.REVERT_ON_EXIT, false));
        _showWindowList = AddOption(options, Locale.String("SettingsOptionsShowTrayList"), 166, Settings.Get(Program.SHOW_WINDOW_LIST, true));
        _alwaysAdmin = AddOption(options, Locale.String("SettingsOptionsAlwaysAdmin"), 190, Settings.Get(Program.ALWAYS_ADMIN_SETTING, false));
        _showEmptyTitles = AddOption(options, Locale.String("SettingsOptionsShowEmptyTitles"), 214, Settings.Get(Program.SHOW_EMPTY_WINDOW_SETTING, false));

        Label showMenuLabel = new()
        {
            Text = Locale.String("SettingsOptionsShowMenu"),
            Location = new Point(12, 244),
            AutoSize = true,
        };

        Size showMenuLabelSize = TextRenderer.MeasureText(
            showMenuLabel.Text,
            showMenuLabel.Font);

        int showMenuLeftClickX = showMenuLabel.Left + showMenuLabelSize.Width + 8;

        _showMenuLeftClick = new CheckBox
        {
            Text = Locale.String("SettingsOptionsShowMenuLeftClick"),
            Location = new Point(showMenuLeftClickX, 242),
            AutoSize = true,
            Checked = Settings.Get(Program.SHOW_MENU_LEFT_CLICK, true),
        };

        _showMenuRightClick = new CheckBox
        {
            Text = Locale.String("SettingsOptionsShowMenuRightClick"),
            Location = new Point(_showMenuLeftClick.Right + 12, 242),
            AutoSize = true,
            Checked = Settings.Get(Program.SHOW_MENU_RIGHT_CLICK, true),
        };

        options.Controls.AddRange(new Control[]
        {
            showMenuLabel,
            _showMenuLeftClick,
            _showMenuRightClick
        });

        GroupBox languageGroup = new()
        {
            Text = Locale.String("SettingsLanguageTitle"),
            Location = new Point(10, 394),
            Size = new Size(540, 62),
        };
        _language = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(12, 25),
            Width = 516,
        };
        _language.Items.AddRange(Locale.GetAvailableLanguages());
        string currentLanguage = Locale.GetCurrentLanguage().Id;
        _language.SelectedItem = _language.Items.Cast<LanguageInfo>().FirstOrDefault(x => string.Equals(x.Id, currentLanguage, StringComparison.OrdinalIgnoreCase));
        languageGroup.Controls.Add(_language);

        GroupBox other = new()
        {
            Text = Locale.String("SettingsOtherTitle"),
            Location = new Point(10, 464),
            Size = new Size(540, 58),
        };

        Button blacklist = new()
        {
            Text = Locale.String("SettingsOtherBlacklistButton"),
            Location = new Point(10, 21),
            AutoSize = true,
        };
        blacklist.Click += (_, _) =>
        {
            (string[] Titles, string[] Processes)? result = BlacklistWindow.Display(
                Locale.String("SettingsOtherBlacklistWindowTitle"),
                Program.GetBlacklistedTitles(),
                Program.GetBlacklistedProcesses());

            if (result != null)
            {
                Program.ApplyBlacklistedTitles(result.Value.Titles);
                Program.SaveBlacklistedTitles();

                Program.ApplyBlacklistedProcesses(result.Value.Processes);
                Program.SaveBlacklistedProcesses();
            }
        };

        Button startup = new()
        {
            Text = Locale.String("SettingsOtherStartupButton"),
            Location = new Point(180, 21),
            AutoSize = true,
        };
        startup.Click += (_, _) => ConfigureStartup();

        Button resetAll = new()
        {
            Text = Locale.String("SettingsOtherResetButton"),
            Location = new Point(350, 21),
            AutoSize = true,
        };
        resetAll.Click += (_, _) => ResetAllSettings();
        other.Controls.AddRange(new Control[] { blacklist, startup, resetAll });

        Button ok = new()
        {
            Text = Locale.String("SettingsOk"),
            Location = new Point(310, 532),
            Size = new Size(75, 27),
        };
        ok.Click += (_, _) => { if (ApplySettings()) Close(); };

        Button cancel = new()
        {
            Text = Locale.String("SettingsCancel"),
            Location = new Point(391, 532),
            Size = new Size(75, 27),
        };
        cancel.Click += (_, _) => Close();

        Button apply = new()
        {
            Text = Locale.String("SettingsApply"),
            Location = new Point(472, 532),
            Size = new Size(75, 27),
        };
        apply.Click += (_, _) => ApplySettings();

        Controls.AddRange(new Control[] { hotKeyGroup, options, languageGroup, other, ok, cancel, apply });
    }

    private CheckBox CreateModifier(string text, int x, Control parent, int width = 54)
    {
        CheckBox checkBox = new()
        {
            Text = text,
            Location = new Point(x, 51),
            Size = new Size(width, 27),
            Appearance = Appearance.Button,
            TextAlign = ContentAlignment.MiddleCenter,
        };
        checkBox.CheckedChanged += (_, _) => RebuildHotKeyCodeFromControls();
        parent.Controls.Add(checkBox);
        return checkBox;
    }

    private static CheckBox AddOption(Control parent, string text, int y, bool value)
    {
        CheckBox option = new()
        {
            Text = text,
            Location = new Point(12, y),
            AutoSize = true,
            Checked = value,
        };
        parent.Controls.Add(option);
        return option;
    }

    private void LoadHotKeyControls()
    {
        _updatingHotKey = true;
        try
        {
            int low = _keyCode & 0xFFFF;
            _ctrl.Checked = (low & (int)Win32ModKeys.MOD_CONTROL) != 0;
            _alt.Checked = (low & (int)Win32ModKeys.MOD_ALT) != 0;
            _shift.Checked = (low & (int)Win32ModKeys.MOD_SHIFT) != 0;
            _win.Checked = (low & (int)Win32ModKeys.MOD_WIN) != 0;
            _keyText.Text = ((Keys)((_keyCode >> 16) & 0xFFFF)).ToString();
        }
        finally
        {
            _updatingHotKey = false;
        }
    }

    private void ResetHotKey()
    {
        _updatingHotKey = true;
        try
        {
            _keyCode = 0;
            _ctrl.Checked = false;
            _alt.Checked = false;
            _shift.Checked = false;
            _win.Checked = false;
            _keyText.Text = Keys.None.ToString();
        }
        finally
        {
            _updatingHotKey = false;
        }
    }

    private void RebuildHotKeyCodeFromControls()
    {
        if (_updatingHotKey)
            return;

        int high = _keyCode & unchecked((int)0xFFFF0000);
        int low = 0;
        if (_ctrl.Checked) low |= (int)Win32ModKeys.MOD_CONTROL;
        if (_alt.Checked) low |= (int)Win32ModKeys.MOD_ALT;
        if (_shift.Checked) low |= (int)Win32ModKeys.MOD_SHIFT;
        if (_win.Checked) low |= (int)Win32ModKeys.MOD_WIN;
        _keyCode = high | low;
    }

    private void KeyText_KeyDown(object? sender, KeyEventArgs e)
    {
        e.Handled = true;
        e.SuppressKeyPress = true;

        if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
            return;

        _updatingHotKey = true;
        try
        {
            if (e.Control) _ctrl.Checked = true;
            if (e.Alt) _alt.Checked = true;
            if (e.Shift) _shift.Checked = true;
            int low = 0;
            if (_ctrl.Checked) low |= (int)Win32ModKeys.MOD_CONTROL;
            if (_alt.Checked) low |= (int)Win32ModKeys.MOD_ALT;
            if (_shift.Checked) low |= (int)Win32ModKeys.MOD_SHIFT;
            if (_win.Checked) low |= (int)Win32ModKeys.MOD_WIN;
            _keyCode = ((int)e.KeyCode << 16) | low;
            _keyText.Text = e.KeyCode.ToString();
        }
        finally
        {
            _updatingHotKey = false;
        }
    }

    private bool ApplySettings()
    {
        if (!_showMenuLeftClick.Checked && !_showMenuRightClick.Checked)
        {
            MessageBox.Show(
                Locale.String("SettingsOptionsShowMenuError"),
                Program.TITLE,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        // Every other option is still persisted when the hot key is rejected by Windows,
        // otherwise a single conflicting shortcut would block the whole dialog.
        bool hotKeyApplied = Program.SetForegroundHotKey(_keyCode, persist: true, showError: true);

        Settings.Set(Program.ALWAYS_ADMIN_SETTING, _alwaysAdmin.Checked);
        Settings.Set(Program.TOGGLE_BALLOON_SETTING, _toggleNotification.Checked);
        Settings.Set(Program.SHIFT_CLICK_BLACKLIST, _shiftClickBlacklist.Checked);
        Settings.Set(Program.SHOW_HOTKEY_ICON, _showHotkeyIcon.Checked);
        Settings.Set(Program.HOTKEY_SOUND, _playHotkeySound.Checked);
        Settings.Set(Program.SHOW_WINDOW_LIST, _showWindowList.Checked);
        Settings.Set(Program.SHOW_MENU_LEFT_CLICK, _showMenuLeftClick.Checked);
        Settings.Set(Program.SHOW_MENU_RIGHT_CLICK, _showMenuRightClick.Checked);

        Program.ApplyTrayMenuMouseSettings();

        Settings.Set(Program.ALWAYS_RETRY_ELEVATED, _alwaysRetryAsAdmin.Checked);
        Settings.Set(Program.REVERT_ON_EXIT, _revertOnExit.Checked);
        Settings.Set(Program.SHOW_EMPTY_WINDOW_SETTING, _showEmptyTitles.Checked);

        if (_language.SelectedItem is LanguageInfo selected)
        {
            string oldId = Locale.GetCurrentLanguage().Id;
            if (!string.Equals(selected.Id, oldId, StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(selected.Id, Locale.GetPreferredLanguage(), StringComparison.OrdinalIgnoreCase))
                    Settings.Remove(Program.LANGUAGE);
                else
                    Settings.Set(Program.LANGUAGE, selected.Id);

                Locale.SetLanguage(selected);
                MessageBox.Show(Locale.String("SettingsLanguageChangedInfo"), Program.TITLE, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        return hotKeyApplied;
    }

    private void ConfigureStartup()
    {
        DialogResult answer = MessageBox.Show(
            Locale.String("SettingsOtherStartupConfirm"),
            Program.TITLE,
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question);

        if (answer == DialogResult.Cancel)
            return;

        if (!StartupManager.SetEnabled(answer == DialogResult.Yes))
            MessageBox.Show(Locale.String("SettingsOtherStartupError"), Program.TITLE, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void ResetAllSettings()
    {
        DialogResult answer = MessageBox.Show(
            Locale.String("SettingsOtherResetConfirm"),
            Program.TITLE,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (answer != DialogResult.Yes)
            return;

        Settings.ResetAll();
        StartupManager.SetEnabled(false);
        Program.RequestRestart();
        Close();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        Instance = null;
    }
}