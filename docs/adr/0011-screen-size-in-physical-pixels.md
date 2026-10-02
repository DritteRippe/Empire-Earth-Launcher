# 0011 Screen size in physical pixels

Status: **Accepted** (2026-10-02), amended 2026-10-02 (plan review; implementation in L-WP5), see the Amendment
sections

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

## Amendment 2026-10-02 (implementation, L-WP5)

`Platform.WindowsSystemInfo` measures as decided and is created once by `Program`; the log names the result at the
start (`Windows NT 10.0.19045, primary screen 1920x1080 physical, 1536x864 for DPI-unaware programs (125 %)`).
Refinements made while implementing, keeping the decision:

- The Windows version comes from `RtlGetVersion`, which does not depend on the manifest; Wine is detected by the
  export `wine_get_version` of `ntdll.dll`. A Windows function that fails is logged once and gives an unknown
  size (`ScreenSize.Empty`), never an exception.
- The scaling is the physical width divided by the width a DPI-unaware program sees, rounded to whole percent; it is
  100 % when one of them is unknown.
- `ComputedValues.GameWindow` uses the physical size, else the DPI-unaware size, else the minimum 1024x768; the
  warning below 768 pixels needs the physical size (no warning when it is unknown).
- The window check (`ConsistencyChecker`) compares with the physical size if `HIGHDPIAWARE` is an entry of the
  program's `Layers` value in HKCU, HKLM 64-bit or HKLM 32-bit view, else with the DPI-unaware size; the finding
  "fits only with HIGHDPIAWARE" advises the option or 100 %. Switching `HIGHDPIAWARE` off above 100 % asks in place on
  the Settings page.
- `ISystemInfo` also answers whether a path fits the ANSI code page of Windows (ADR 0015, implementation L-WP5).

Evidence: `Core/Platform/SystemInfoTests`, `Core/GameSettings/ComputedValuesTests` (clamp table of contract 3.3),
`ConsistencyChecksTests` (100 % and 150 %, layer in HKCU, HKLM 64 and HKLM 32, none), `CompatibilityOptionsTests`
(the hint); test plan WP5-07 and WP5-13 on real Windows.
