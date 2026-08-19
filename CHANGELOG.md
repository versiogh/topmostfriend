# Changelog

## 2.2.0

Review focused on how the application appears and behaves in the notification area. Every item
below was verified by building the project with the .NET 8 SDK (`dotnet build` and single-file
`dotnet publish` for `win-x64` and `win-x86`, with no errors and no warnings).

### Fixed

- **Notification-area icon quality.** `TopMostFriend.ico` held a single 32x32 image at 8 bits per
  pixel with a 1-bit mask, so Windows had to stretch that image down to tray size. The icon is
  rebuilt from the same artwork with 16, 20, 24, 32, 40, 48, 64, 96, 128 and 256 pixel entries,
  all 32-bit with an alpha channel.
- **Icon size above 100 % scaling.** `SystemInformation.SmallIconSize` returns 96 dpi metrics in a
  per-monitor aware process, so the tray icon was always requested at 16x16 and then stretched
  back up by the shell. `Win32.GetNotificationAreaDpi()` now reads the DPI of `Shell_TrayWnd` and
  `Win32.GetSmallIconSizeForDpi()` derives the size from `GetSystemMetricsForDpi`.
- **Icon not rebuilt on DPI change.** The icon was created once during startup. `Program` now
  subscribes to `SystemEvents.DisplaySettingsChanged` and `SystemEvents.UserPreferenceChanged`
  and regenerates it whenever the taskbar DPI changes. Both handlers are detached in `Shutdown()`.
- **Menu populated while it was being displayed.** `RefreshWindowList()` ran from the
  `ContextMenuStrip.Opening` event, which fires after the drop-down has been positioned and sized
  for its previous contents. The list is now built from `NotifyIcon.MouseDown`, before the shell
  shows the menu; `Opening` only rebuilds it when the menu was invoked without a right-click.
- **Menu shown without a foreground owner.** The hidden message window is brought to the
  foreground before the drop-down appears, which notification-area menus require in order to
  repaint reliably and to dismiss when the user clicks elsewhere.
- **Unbounded menu height.** The drop-down is capped to the working area of the screen under the
  pointer, so a long window list scrolls instead of being drawn across the taskbar.
- **User interface frozen during elevation.** `UAC.RunElevatedTask()` blocks until the helper
  exits, and it was invoked directly from the menu click handler. Raising the wait from 10 to 60
  seconds in 2.1.0 therefore turned a ten-second freeze into a sixty-second one, with the tray
  menu still on screen and unable to repaint. `UAC.SetWindowTopMostElevatedAsync()` moves the wait
  off the UI thread, and the menu is closed before the confirmation dialog and the UAC prompt.
- **Hot-key preview icon size.** The icon read from the target window is published at whatever
  size that window chose. `AppIcon.Rescale()` redraws it at the notification-area size before it
  is assigned to the tray icon.
- **Unbounded log file.** `topmostfriend.log` now rolls over to `topmostfriend.log.1` at 1 MB.

### Changed

- `WindowMenuItem_Click` and `ToggleForegroundWindow` are asynchronous. The path that does not
  need elevation still completes synchronously, so the check mark is updated on the same message.
- `TryToggleWindow` and `TryElevatedToggle` are replaced by `ToggleWindowAsync` and
  `TryElevatedToggleAsync`, which return a `ToggleOutcome` instead of using an `out` parameter.
- `AUDIT.md` no longer claims that dynamic menu items are disposed on every refresh. That was
  true of 2.0.0 and was deliberately reverted in 2.1.0; only their images are released.
- `README.md`, `CHANGELOG.md` and `AUDIT.md` are written in English, matching the code comments,
  the log messages and the language files. The console output of `build-win64.bat` and
  `build-all.ps1` is in English as well.
- The stale `git tag v2.0.0` example in the release instructions is corrected.
- Known limitations are documented explicitly: resampled small icon sizes, missing menu item
  images, the unimplemented menu background feature, and hot-key identifiers taken from the
  `GlobalAddAtom` range.
- Versions in `TopMostFriend.csproj` and `app.manifest` are raised to 2.2.0.

## 2.1.0

Review following an independent audit of the 2.0.0 edition. Every item below was verified by
building the project with the .NET 8 SDK.

### Fixed

- **Application icon.** The 2.0.0 edition did not include `TopMostFriend.ico`, so the tray icon
  and the executable icon were the generic system ones. The original project's icon is restored,
  declared through `<ApplicationIcon>` and embedded as a resource so it also resolves inside a
  single-file executable (`AppIcon.cs`).
- **Settings blocked by the hot key.** `SettingsWindow.ApplySettings()` aborted on its first line
  when Windows rejected the shortcut, so an already-taken combination prevented every other
  option from being saved and the OK button did not close the window. The remaining options are
  now always persisted, and only the hot key keeps the dialog open.
- **DPI scaling.** All four windows declared `AutoScaleMode.Dpi` without assigning
  `AutoScaleDimensions`, which leaves the scale factor at 1: controls kept their 96 dpi
  coordinates while the font grew in points, clipping text at 125 % and above.
  `AutoScaleDimensions` is set to 96 dpi in all four windows.
- **False negative from the elevated helper.** The 10 second wait could expire on the first
  elevated run, when a self-contained executable extracts its bundle and antivirus software scans
  it. The result was judged from the exit code, so the "protected window" warning appeared even
  when the change had been applied. The budget rises to 60 seconds and the result is decided from
  the effective window state.
- **Menu items disposed while in use.** `DisposeDynamicMenuItems()` called `Dispose()` on the
  `ToolStripItem` instances, including in the Refresh flow where the menu is deliberately kept
  open. Only the associated image is released now; the items are left to the garbage collector.
- **Menu `CancellationTokenSource` race.** The previous source was disposed while the icon task
  could still be using it. Each task now receives its own source and disposes it on completion.
- **Localisation argument index.** `Locale.String()` inserts the application name as `{0}`, so
  arguments start at `{1}`. `BlacklistEditorEditing` used `{0}` and displayed the application name
  instead of the title being edited, in both `en-GB` and `nl-NL`.

### Changed

- Strings that were hard-coded in English inside the code moved to the language files: the
  no-title window label, the show-untitled-windows option, the start-with-Windows error, the
  hot-key-unavailable warning, the About window description, the First Run continue button and
  the two unhandled-error messages. Eight new entries were added to `en-GB.xml` and `nl-NL.xml`.
- The About window description is no longer an auto-sized `Label` with hard-coded line breaks.
- The version declared in `app.manifest` is aligned with the one in the `.csproj`.
