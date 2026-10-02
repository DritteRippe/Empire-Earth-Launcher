# 0014 Only working features in the UI

Status: **Accepted** (2026-10-02)

## Context

The designer files contain many controls without function: Play had no click handler, the settings page
shows "Repair CD-Keys", "Reset the Game", "Clear Registry", compatibility checkboxes, resolution, DirectX
wrapper, dreXmod, Discord presence, HD textures, skip intro, game font; the Play page shows language
checkboxes and ranking labels; the launcher page a file association checkbox; the navigation a Mods page.
The forum study lists them as dummies (forum report section 8, "Nicht verdrahtet"). A player cannot tell a
dummy from a broken feature.

## Decision

- v2 shows **only controls that work**. Controls of features that are not implemented are removed from the
  designer (or hidden and disabled where the layout depends on them) and stay listed as planned features in
  the README.
- The functions v2 implements replace their dummies: Play, reset, cleanup, compatibility options,
  repair advice (instead of "Repair CD-Keys": the setup repairs CD keys, ADR 0007/contract 4).
- No "Repair CD-Keys" button that calls anything but the repair advice (D6: never call `authtools.dll`).

## Evidence

- `SettingsUserControl.Designer.cs`, `GeneralUserControl.Designer.cs`, `LauncherSettingsUserControl.Designer.cs`
  on `v2` (texts listed above); `MainForm.cs`: "A button without a page (Mods, not implemented yet) hides all
  pages".
- Forum report section 8, "Vorab zum Launcher".

## Consequences

- The UI looks smaller than the old mock-up; every visible control is covered by the test plan.
- Re-adding a feature means adding its control with its function in the same work package.

## Alternatives considered

- **Keep dummies disabled with "coming soon"**: still noise for players and testers. Rejected.
