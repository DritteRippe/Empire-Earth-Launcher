# 0011 Screen size in physical pixels

Status: **Accepted** (2026-10-02), amended 2026-10-02 (plan review, see the Amendment section)

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

## Amendment 2026-10-02 (plan review)

Contract O4: the game sees physical pixels only with `HIGHDPIAWARE`; without it, it is DPI-virtualized on a scaled
screen and sees logical pixels. The recommended window size (contract 3.3) stays in physical pixels, identical to
the setup; only the consistency check and the hints take the game's view into account:

- `ISystemInfo` also returns the primary screen as a DPI-unaware program sees it (`GetSystemMetrics`
  `SM_CXSCREEN`/`SM_CYSCREEN` in the launcher, which is DPI-unaware like the game) and the scaling derived from
  both sizes.
- **"Window larger than the screen"** compares with the physical size if `HIGHDPIAWARE` is effective for that
  program (an entry of the `Layers` value in HKCU or HKLM), else with the size a DPI-unaware program sees. If the
  window fits physically but not without the layer, the finding is its own code: the advice is to switch on the
  compatibility option (Windows 8 and later) or to set the scaling to 100 %, not a reset, which would write the
  same values again.
- Switching `HIGHDPIAWARE` off on a screen scaled above 100 % shows that hint before the change.
- Tests with `FakeSystemInfo` at 100 % and 150 %, with and without the layer in HKCU and in HKLM. The test plan's
  O4 case also runs with the layer switched off.
