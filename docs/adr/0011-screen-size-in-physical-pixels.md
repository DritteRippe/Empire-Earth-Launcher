# 0011 Screen size in physical pixels

Status: **Accepted** (2026-10-02)

## Context

The default game window size is the primary screen size in physical pixels, limited to 1024-1920 by
768-1080 (contract 3.3); screens lower than 768 pixels get a warning (R13, t=3863). A DPI-unaware process
gets scaled ("logical") sizes from `GetSystemMetrics` and `Screen.PrimaryScreen` on scaled displays, e.g.
1536x864 instead of 1920x1080 at 125 %. The launcher UI is designed with fixed bitmaps and runs DPI-unaware
today (no `dpiAware` in its manifest).

## Decision

- The launcher measures the primary screen with `EnumDisplayDevices` (the device with
  `DISPLAY_DEVICE_PRIMARY_DEVICE`) and `EnumDisplaySettings(device, ENUM_CURRENT_SETTINGS)`
  (`dmPelsWidth`, `dmPelsHeight`): the current display mode, which is in physical pixels whatever the DPI
  awareness of the process. Implemented in `Platform.WindowsSystemInfo`, faked in tests.
- The launcher's own UI stays **DPI-unaware** in v2 (no change of layout risk); its new `app.manifest`
  declares `asInvoker` and the supported Windows versions (Windows 7 to 11), not `dpiAware`.
- The clamp and the warning are pure core logic (`GameSettings.ComputedValues`), tested with the fake.

## Evidence

- CONTRACT.md 3.3: "The launcher MUST measure physical pixels (DPI-aware process or `EnumDisplaySettings`)",
  O4.
- Win32 documentation of `EnumDisplaySettings`: `ENUM_CURRENT_SETTINGS` returns the current mode of the
  display device; `EnumDisplayDevices` flags the primary device.
- `Empire Earth Launcher.csproj` on `v2` has no `ApplicationManifest`; the mod creator's `app.manifest`
  shows the supported-OS list that Windows needs to report its real version (`WindowsVersion.cs` remarks).

## Consequences

- Setup and launcher compute the same size if the setup also gets physical pixels (O4, test plan: compare
  the values written by the setup and by the launcher's reset on a 125 % screen).
- A later DPI-aware launcher UI is a separate decision.

## Alternatives considered

- **Make the launcher per-monitor DPI-aware** and use `GetSystemMetrics`: correct numbers but a layout
  rework of every form with fixed bitmaps. Rejected for v2.
- **`Screen.PrimaryScreen.Bounds`**: logical pixels in a DPI-unaware process. Rejected.
