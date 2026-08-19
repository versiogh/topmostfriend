# Technical audit of TopMostFriend 1.6.0 and the hardening changes

This document covers three rounds: the migration from 1.6.0 to the 2.0.0 edition, the review that
produced 2.1.0, and the notification-area review that produced 2.2.0. Later rounds appear further
down.

## Scope

The tray icon logic, window enumeration, Win32 P/Invoke, hot keys, UAC, registry persistence,
state restoration on exit, startup with Windows, localisation, resource lifetime and x86/x64
compatibility were all reviewed.

This edition targets Windows 10 and Windows 11 and moves the project from .NET Framework 4.0 to
.NET 8 WinForms.

## Findings in the original code

| Severity | Finding | Effect | Correction applied |
|---|---|---|---|
| Critical | Synchronous `SendMessage(WM_GETICON)` during `RefreshWindowList()` | An unresponsive third-party window can freeze the UI thread and leave the tray menu black or hung | `SendMessageTimeout` with a non-zero timeout; icons loaded off the UI thread |
| High | `Process` objects compared to identify processes | `IsOwnWindow` and the per-process separators can misbehave, and there are races if the process disappears | Comparison by numeric PID/TID and revalidation of the HWND |
| High | HWND reused without an identity check | Restoring or elevating could act on a different window if Windows recycles the handle | HWND plus PID plus TID stored and checked |
| High | The hot key did not record the original state for `RevertOnExit` | Windows toggled by hot key were not restored | Single toggle path shared by the menu and the hot key |
| High | Hot-key registration was not transactional | If the new hot key fails, the previous one is lost and an invalid configuration can persist | The previous hot key is restored and the value is only persisted after success |
| Medium | `WM_HOTKEY` was matched through packed `lParam` | More fragile than using the registered identifier | `wParam`/atom is used, which is the documented identifier |
| Medium | No `MOD_NOREPEAT` | Holding the combination down can toggle repeatedly | `MOD_NOREPEAT` added |
| High | Mutex created as acquired and then acquired again | Recursive ownership count and races during elevated restarts | Mutex not acquired on construction; explicit and idempotent acquire and release |
| High | `Shutdown()` depended indirectly on the double acquisition | Risk of an incorrect release during restart or elevation | `Shutdown()` is idempotent and the mutex has explicit ownership state |
| High | Restart and elevation arguments built without robust quoting | Paths and arguments containing spaces can break | `CommandLineToArgvW`-compatible quoting and an explicit helper |
| High | The elevated helper toggled rather than set a state | If the state changes during the UAC prompt, it can end up inverted | The helper receives the target state and verifies PID/TID |
| Medium | Unbounded wait for the elevated helper | A faulty helper could block the UI indefinitely | Maximum wait for the helper |
| Medium | `RegistryKey` not closed on reads and writes | Progressive registry handle leak | `using`/`Dispose` on every operation |
| Medium | An empty blacklist array deleted the value | The default blacklist reappeared on the next start | An empty `REG_MULTI_SZ` is persisted, and the 1.x format is still read |
| Medium | Incomplete settings reset | Some options survived "Reset All" | The whole settings subkey is deleted |
| Medium | The hot-key editor reset did not clear all state | A modifier could rebuild an old key | Local state and CTRL/ALT/SHIFT/WIN are cleared together |
| Medium | `ActionTimeout` used a raw thread and touched the UI from the background | Races during shutdown and unsafe access to UI components | `System.Windows.Forms.Timer` for UI work, tasks only for external work |
| Medium | The dynamic menu did not release images explicitly | Possible GDI and memory growth after many menu openings | Dynamic images are disposed on every refresh |
| Medium | Win32 signatures using `int` where a pointer or LPARAM is required | x64 risk and incorrect conversions | Pointer-sized signatures (`IntPtr`, `uint`) and x86/x64 wrappers |
| Medium | `SWP_SHOWWINDOW` when changing the top-most flag | Could make a window visible as a side effect | `SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE`; activation is separate and only when appropriate |
| Medium | Startup with Windows depended on the `IWshRuntimeLibrary` COM component | An old dependency, harder to build and distribute | Per-user entry under `HKCU\...\Run` |
| Medium | First Run used `Thread.Sleep`, manual threads and `Invoke` during its animation | Temporary freezes and races when changing pages or closing | Simple, synchronous First Run without a multithreaded animation |
| Low | A credit button was created but never added to `Controls` | Invisible link | About window simplified |
| Low | Version displayed via `Substring(length - 2)` | Fragile if the version format changes | `Application.ProductVersion` used directly |
| Medium | The custom language file was resolved relative to the working directory | May not be found when started from elsewhere | Resolved against `AppContext.BaseDirectory` |
| Medium | Malformed translation formats could raise `FormatException` | A custom locale could crash the UI | Fallback and logging for invalid formats |
| Medium | .NET Framework 4.0 base and legacy DPI handling | Poor fit for Windows 10/11 and modern scaling | `net8.0-windows`, modern WinForms and PerMonitorV2 |

## Risks that cannot be eliminated

TopMostFriend acts on windows belonging to other processes. Windows can refuse some operations
because of integrity levels, UAC, protected windows or the target program's own policies. The goal
of this edition is that those cases fail in a controlled way without hanging the application.

A source ZIP cannot amount to a compatibility certification. Final validation must include a real
build and testing on physical machines or virtual machines running Windows 10 and Windows 11,
especially with elevated applications, UWP/WinUI applications, multiple monitors and a
deliberately hung application.

## Second round: findings against the 2.0.0 edition

Independent review of the 2.0.0 package, cross-checked against the original repository and against
a real build of the project.

| Severity | Finding | Effect | Correction applied |
|---|---|---|---|
| High | The 2.0.0 package included neither `TopMostFriend.ico` nor an `<ApplicationIcon>` declaration | Generic system icon in the tray and on the executable | Icon recovered from the original project, declared in the `.csproj` and embedded as a resource (`AppIcon.cs`) |
| High | `ApplySettings()` aborted if hot-key registration failed | An already-taken combination prevented every other option from being saved, and the OK button did not close the dialog | The remaining options are always persisted; the return value only controls closing |
| High | `AutoScaleMode.Dpi` without `AutoScaleDimensions` | Scale factor of 1: controls at 96 dpi with a scaled font, clipping text at 125 % and above | `AutoScaleDimensions` set to 96 dpi in all four windows |
| Medium | 10 second wait for the elevated helper and a decision based on the exit code | False "protected window" warning on the first elevated run of a self-contained executable | 60 second budget and verification of the effective window state |
| Medium | `Dispose()` on `ToolStripItem` while the menu can still be open | Risk of an exception during painting in the Refresh flow | Only the image is released; the items are left to the garbage collector |
| Low | The previous `CancellationTokenSource` was disposed while its task was still running | Exception recorded in the log and interrupted icon loading | Each task receives its own source and disposes it on completion |
| Low | `BlacklistEditorEditing` used `{0}`, which is reserved for the application name | The editor displayed the application name instead of the title being edited | Index corrected to `{1}` in `en-GB` and `nl-NL` |
| Low | Eight strings hard-coded in English inside a localised system | Mixed-language interface for users of other languages | Strings moved into the language files |
| Low | `app.manifest` version out of step with the `.csproj` | Inconsistent metadata | Versions aligned |

## Third round: findings against the 2.1.0 edition

Review driven by a reported symptom: the icon and the menu next to the clock did not render
correctly. Verified against a real build (`dotnet build` and single-file `dotnet publish` for
`win-x64` and `win-x86`) and against the artwork shipped in the package.

| Severity | Finding | Effect | Correction applied |
|---|---|---|---|
| High | `TopMostFriend.ico` carries a single 32x32 image at 8 bits per pixel with a 1-bit mask, byte-identical to the original project's file | Windows stretches that one image down to tray size, so the icon beside the clock is jagged, flat and has hard edges | Icon rebuilt from the same artwork with ten entries from 16 to 256 pixels, all 32-bit with an alpha channel |
| High | The tray icon size came from `SystemInformation.SmallIconSize` | That API reports 96 dpi metrics in a per-monitor aware process, so a 16x16 icon was supplied at every scaling factor and the shell stretched it back up | `Win32.GetNotificationAreaDpi()` reads the DPI of `Shell_TrayWnd`; `Win32.GetSmallIconSizeForDpi()` uses `GetSystemMetricsForDpi` |
| Medium | The tray icon was created once during startup and never rebuilt | Changing the scaling factor or moving the taskbar to another monitor leaves an icon of the wrong size | Rebuilt on `DisplaySettingsChanged` and on the relevant `UserPreferenceChanged` categories; handlers detached in `Shutdown()` |
| High | `RunElevatedTask()` waits for the helper to exit and was called from the menu click handler on the UI thread | The whole application, including the open tray menu, froze for up to 60 seconds and could not repaint. The 2.1.0 change from 10 to 60 seconds made this worse rather than better | `UAC.SetWindowTopMostElevatedAsync()` moves the wait to a background thread; the menu is closed before the dialog and the UAC prompt |
| Medium | The window list was rebuilt from `ContextMenuStrip.Opening` | The drop-down is positioned and sized before `Opening` runs, so it is laid out for its previous contents and can be drawn clipped | The list is built from `NotifyIcon.MouseDown`, before the shell shows the menu; `Opening` remains as a fallback |
| Medium | The menu was shown without bringing a window to the foreground | Notification-area menus can fail to repaint and refuse to dismiss without a foreground owner | The hidden message window is activated in the `MouseDown` handler |
| Medium | The drop-down height was unbounded | With many open windows the menu exceeds the display and is drawn across the taskbar | Height capped to the working area of the screen under the pointer |
| Low | The hot-key preview icon was assigned at whatever size the target window published | A 32x32 or larger icon squeezed into a 16x16 tray slot | `AppIcon.Rescale()` redraws it at the notification-area size |
| Low | `topmostfriend.log` had no size bound | A failure that repeats on every menu refresh grows the file indefinitely | Rolls over to `topmostfriend.log.1` at 1 MB |
| Low | The first table of this document claimed that dynamic menu items were disposed on every refresh | Contradicts the 2.1.0 correction, which deliberately stopped disposing them | Row corrected to refer to images only |
| Low | `README.md`, `CHANGELOG.md` and `AUDIT.md` were written in Spanish while the code, logs and language files were in English | Inconsistent project language | Documentation and build script output rewritten in English |

### Checks that found no problem

- Window titles are read with `GetWindowText`, which does not send `WM_GETTEXT` across processes,
  so opening the menu cannot block on a hung application.
- `Process.Start` with the `runas` verb does not return until the user answers the UAC dialog, so
  consent time does not consume the helper's wait budget.
- The embedded resource names match the prefix `Locale` expects, and the rebuilt icon is confirmed
  to carry ten size entries inside the compiled assembly.
- The hot-key identifier arrives in `WM_HOTKEY` through `wParam`, which is the documented
  parameter for the registered identifier.
- Every localisation key used from code exists in `en-GB.xml`, and both language files hold the
  same key set.

### Known issues left in place deliberately

- Hot keys are registered with an identifier returned by `GlobalAddAtom`, which falls in the
  `0xC000`-`0xFFFF` range that the Win32 documentation reserves for shared DLLs rather than
  applications. Hot-key identifiers are scoped to the owning window, so collisions across
  processes are not possible and the original behaviour is preserved.
- The Refresh, Settings, About and Quit menu entries have no images. The original used bitmaps
  from `Properties.Resources` that are not part of this package.
- The menu background image feature of 1.x is not implemented. `ListBackgroundPath` and
  `ListBackgroundLayout` remain as named constants with no effect.
- The 16, 20 and 24 pixel icon entries are resampled from a 32x32 source, which is all the
  original artwork provides. Redrawing the artwork is the only way to improve them further.
