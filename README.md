# TopMostFriend, hardened edition for Windows 10/11

Version 2.2.0

This folder is derived from the open-source project `flashwave/topmostfriend` (0BSD licence). It keeps the original idea and features, but moves the code base to **.NET 8 + WinForms** and fixes a number of fragile points in the original implementation.

## Main changes

- The user interface no longer blocks while reading icons from other applications' windows: icons are fetched on a background thread and `WM_GETICON` uses `SendMessageTimeout`.
- `Process.GetProcessById()` is no longer used to compare windows. Numeric PID/TID values are used instead, and the `HWND` is revalidated as belonging to the same window before anything acts on it.
- `IsOwnWindow` and the per-process separator logic are corrected; the original compared distinct `Process` objects.
- An old `HWND` is never reused without a PID/TID check when restoring state or invoking the elevated helper.
- Windows toggled through the hot key are also recorded for `RevertOnExit`.
- Hot-key registration is transactional: if Windows rejects the new shortcut, the previous one is restored.
- `MOD_NOREPEAT` is used so holding the combination down does not toggle repeatedly.
- Win32 declarations with signatures that are wrong on x64 (`LPARAM`, pointers, styles) have been removed.
- The old doubly-acquired mutex pattern is replaced by an explicit, idempotent single-instance mutex.
- `Shutdown()` is idempotent and does not depend on releasing the mutex twice.
- Registry keys are closed properly (`RegistryKey.Dispose`) so handles are not leaked.
- Registry lists are written as `REG_MULTI_SZ` while still reading the binary format used by TopMostFriend 1.x.
- An empty blacklist can now be persisted without the default entries reappearing on the next start.
- Startup with Windows uses `HKCU\...\Run` instead of the `IWshRuntimeLibrary` COM dependency.
- The manual First Run animation thread and its `Thread.Sleep`/`Invoke` calls are gone, removing a class of UI race.
- Language loading tolerates incomplete XML, and custom languages are resolved next to the executable rather than in the current working directory.
- Web links use `UseShellExecute=true`, which modern .NET requires.
- Dynamic menu images are disposed to limit GDI and memory growth.
- A log file is written to `%LOCALAPPDATA%\TopMostFriend\topmostfriend.log`.
- A Per-Monitor V2 DPI manifest and a modern Windows 10/11 compatibility target are included.
- GitHub Actions workflows build `win-x64` and `win-x86` and publish releases from tags.

### Fixes in review 2.1.0

- The application icon, missing in 2.0.0, is restored. It is declared through `<ApplicationIcon>` and embedded as a resource so it also resolves in the single-file executable.
- A hot key rejected by Windows no longer prevents the rest of the settings dialog from being saved.
- All four windows set `AutoScaleDimensions`. Without it, `AutoScaleMode.Dpi` leaves the scale factor at 1 and text is clipped above 100 %.
- The elevated helper is given 60 seconds and its result is judged from the effective window state rather than from the exit code.
- Dynamic menu items are no longer disposed while the menu may still be open. Only their images are released; the items themselves are left to the garbage collector.
- Each icon-loading task owns its own `CancellationTokenSource`.
- The blacklist editor's format argument index is corrected; it previously displayed the application name instead of the title being edited.
- Eight strings that were hard-coded in English were moved into the language files.

### Fixes in review 2.2.0

This round targets how the application presents itself in the notification area, which was the remaining visible problem.

- **Notification-area icon.** `TopMostFriend.ico` contained a single 32x32 image at 8 bits per pixel with a 1-bit mask. Windows therefore had to stretch that one image down to 16x16 for the tray, producing a jagged, flat-looking icon next to the clock. The icon is rebuilt from the same artwork with 16, 20, 24, 32, 40, 48, 64, 96, 128 and 256 pixel entries, all 32-bit with a real alpha channel.
- **Icon size at scaling factors above 100 %.** `SystemInformation.SmallIconSize` reports 96 dpi metrics inside a per-monitor aware process, so the tray icon was always requested at 16x16 and the shell stretched it back up. The taskbar DPI is now queried directly (`GetDpiForWindow` on `Shell_TrayWnd`) and the icon size comes from `GetSystemMetricsForDpi`.
- **DPI changes at runtime.** The icon was created once at startup and never rebuilt. It is now regenerated on `DisplaySettingsChanged` and on the relevant `UserPreferenceChanged` categories, so moving the taskbar to a monitor with different scaling no longer leaves a stretched icon behind.
- **Menu built while it was being shown.** The window list was rebuilt from the `Opening` event, after the drop-down had already been positioned and sized for its previous contents. It is now built from the tray icon's `MouseDown` handler, before the shell displays the menu, with `Opening` kept only as a fallback for keyboard invocation.
- **Menu without a foreground owner.** The hidden message window is brought to the foreground before the menu is shown, which is the documented requirement for notification-area menus that would otherwise fail to repaint or refuse to dismiss.
- **Menu taller than the screen.** The drop-down height is capped to the working area of the screen the pointer is on, so a machine with many open windows gets a scrollable menu instead of one drawn clipped across the taskbar.
- **User interface frozen during elevation.** `RunElevatedTask` waits for the helper process to exit. It was called directly from the click handler, so raising the timeout from 10 to 60 seconds in 2.1.0 turned a ten-second freeze into a sixty-second one, with the tray menu still on screen and unable to repaint. The wait now runs off the UI thread and the menu is closed before the elevation prompt appears.
- **Hot-key preview icon.** The icon collected from the target window is published at whatever size that window chose, usually 32x32 or larger. It is now redrawn at the notification-area size before being assigned.
- **Log growth.** `topmostfriend.log` had no bound. It now rolls over to `topmostfriend.log.1` at 1 MB.

Full details are in `CHANGELOG.md` and in the tables in `AUDIT.md`.

## Known limitations

- The small icon sizes are resampled from a 32x32 source, because that is all the original artwork provides. They are clearly better than a runtime stretch, but 16x16 remains the weakest size. Redrawing the artwork is the only way to improve it further.
- The menu entries for Refresh, Settings, About and Quit have no images. The original used bitmaps from `Properties.Resources` that are not part of this package.
- The menu background image feature of TopMostFriend 1.x is not implemented. `ListBackgroundPath` and `ListBackgroundLayout` are still read from the registry by name only and have no effect.
- Hot keys are registered with an identifier obtained from `GlobalAddAtom`, which falls in the `0xC000`-`0xFFFF` range that the Win32 documentation reserves for shared DLLs rather than applications. This is inherited from the original project and works in practice, since hot-key identifiers are scoped to the owning window.

## Build requirements

Recommended route:

1. Install **Visual Studio 2022**.
2. In Visual Studio Installer, select the **.NET desktop development** workload.
3. Make sure the **.NET 8 SDK** is present.
4. Open `TopMostFriend.sln`.
5. Select `Release`.
6. Use **Build > Build Solution**.

A plain `dotnet build` places its output under:

`TopMostFriend\bin\Release\net8.0-windows\`

### Building a self-contained 64-bit executable

Open PowerShell in the root folder and run:

```powershell
dotnet publish .\TopMostFriend\TopMostFriend.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -p:DebugType=None `
  -p:DebugSymbols=false `
  -o .\publish\win-x64
```

The result is:

`publish\win-x64\TopMostFriend.exe`

The target machine does not need .NET installed, because the publish is self-contained.

If you build from Linux or macOS for verification purposes, add `-p:EnableWindowsTargeting=true`. The resulting binary still only runs on Windows.

## Building automatically on GitHub

The package already contains `.github/workflows/build.yml`.

1. Create your fork or your own repository.
2. Upload the contents of this folder, keeping `.github/workflows/` intact.
3. Open the **Actions** tab on GitHub.
4. Select **Build Windows executables**.
5. Press **Run workflow**.
6. When it finishes, **Artifacts** will contain:
   - `TopMostFriend-win-x64`
   - `TopMostFriend-win-x86`

### Creating a Release automatically

`.github/workflows/release.yml` is included as well.

From your machine:

```powershell
git tag v2.2.0
git push origin v2.2.0
```

GitHub Actions builds both architectures and creates a Release with two ZIP files.

## Command-line arguments

- `--reset-admin`: removes the "Always run as Administrator" option before starting. Useful if that option was enabled and the machine can no longer complete the UAC dialog.
- `--set-topmost=0|1 --hwnd=<handle> --pid=<pid> --tid=<tid> --activate=0|1`: internal helper mode. The application invokes this itself when retrying with elevated permissions; it is not intended for manual use.
- `--toggle=<handle>` and `--background=<handle>`: legacy helper mode from TopMostFriend 1.x, kept for compatibility.

## Recommended testing on Windows 10/11

Before replacing your current version, test:

1. Opening and closing the tray menu repeatedly with many applications running.
2. Deliberately hanging an application and confirming the menu still opens.
3. Pinning and unpinning normal windows, elevated windows and windows from different processes.
4. Holding the hot key down: it must toggle once per press.
5. Enabling `Revert status ... on exit`, toggling from both the menu and the hot key, quitting TopMostFriend and verifying restoration.
6. `Start with Windows`.
7. Display scaling at 100 %, 125 %, 150 % and 200 %, and two monitors with different scaling factors if you have them. Move the taskbar between monitors and confirm the tray icon stays sharp.
8. Toggling a window that requires elevation and confirming the menu closes, the UAC prompt appears and the application stays responsive throughout.
9. Reviewing `%LOCALAPPDATA%\TopMostFriend\topmostfriend.log` if anything fails.

## Compatibility note

This edition targets **Windows 10 and Windows 11**. Unlike the historical project, it no longer attempts to remain compatible with Windows XP/7 or .NET Framework 4.0.

There is no responsible way to promise that a utility that manipulates other applications' windows will work with every application. Windows imposes limits through integrity levels and UAC, and some protected applications will reject the change. The code is designed to fail in a controlled way, not to freeze the process, and to verify the identity of a window before modifying it.

## Helper scripts in this package

- `build-win64.bat`: double-click to produce the self-contained x64 executable directly.
- `build-all.ps1`: publishes x64 and x86 in a single run.
- `AUDIT.md`: a detailed list of the bugs and risks found in the original code and in the previous editions of this fork, with the correction applied to each.

## Validation status of this package

The following was run against this revision:

1. `dotnet build -c Release` with the .NET 8 SDK

Completes with 0 errors and 0 warnings.

2. `dotnet publish`, single-file and self-contained, for `win-x64` and `win-x86`

Both produce their `TopMostFriend.exe`.

3. Verification of the embedded resources in the assembly

The resulting names are `TopMostFriend.Languages.en-GB.xml`, `TopMostFriend.Languages.nl-NL.xml` and `TopMostFriend.TopMostFriend.ico`, which are the names the code looks for. The embedded icon is confirmed to carry ten size entries.

4. Localisation key cross-check

Every key used from code exists in `en-GB.xml`, and `en-GB.xml` and `nl-NL.xml` hold the same key set.

5. Review against the original code in `flashwave/topmostfriend`

Each finding listed in `AUDIT.md` was verified against the corresponding file and line of the 1.6.0 project.

What has not been done here is running the binary: compilation took place with the .NET SDK on a non-Windows environment, so runtime behaviour must be validated on a real machine or virtual machine using the test list above. The included workflow also builds on a Windows runner, which is a useful second check before distributing a Release.

## Credits and licence

Original project by flashwave (`https://github.com/flashwave/topmostfriend`), published under the 0BSD licence. The `TopMostFriend.ico` artwork comes from that project; the file in this package is a multi-size rebuild of it and is kept under the same licence. This edition remains 0BSD, included in `LICENSE`.
