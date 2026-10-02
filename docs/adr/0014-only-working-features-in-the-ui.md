# 0014 Only working features in the UI

Status: **Accepted** (2026-10-02), amended 2026-10-02 (design review, see the Amendment section)

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

## Amendment 2026-10-02 (design review)

The first list was incomplete. Removed (field, designer code, handlers, resources) are exactly these
controls of `v2` at `cab4d44`; an architecture test fails if one of the names appears in a `*.Designer.cs`
again. Working replacements get new names in their work packages.

- `LauncherSettingsUserControl`: `diagnosticDataKryptonCheckBox` ("Allow us to collect diagnostic data ...":
  contradicts "no telemetry", ARCHITECTURE 10), `associateModFilesKryptonCheckBox`, `gameStartKryptonLabel`,
  `gameStartKryptonComboBox`, `gameCloseKryptonLabel`, `gameCloseKryptonComboBox`;
- `SettingsUserControl`: `magicButtonsKryptonGroupBox` with `repairCdKeysKryptonButton`,
  `resetGameKryptonButton`, `clearRegistryKryptonButton`; `compatibilityKryptonGroupBox` and
  `windowsCompatibilityKryptonGroupBox` with `compatibilityModeKryptonCheckBox`,
  `compatibilityModeKryptonComboBox`, `clearHeapAllocationKryptonCheckBox`,
  `bitDepthMitigationKryptonCheckBox`, `gameWorkingKryptonButton`, `autoDetectCompatibilityKryptonButton`;
  `directXKryptonGroupBox` with `directXKryptonLabel`, `directXKryptonComboBox`,
  `directXWrapperKryptonCheckBox`, `dgVoodooSettingsKryptonButton`, `resolutionKryptonLabel`,
  `resolutionKryptonComboBox`, `monitorKryptonLabel`, `monitorKryptonComboBox`, `gameFontKryptonLabel`,
  `gameFontKryptonTextBox`, `browseGameFontKryptonButton`; `advancedSettingsKryptonGroupBox` with
  `dreXmodKryptonCheckBox`, `dreXmodVersionKryptonComboBox`, `neoEEKryptonCheckBox`,
  `discordPresenceKryptonCheckBox`, `hdTexturesKryptonCheckBox`, `skipIntroKryptonCheckBox`
  (the working compatibility warning panel `compatibilityWarning*` stays);
- `GeneralUserControl`: `languageKryptonGroupBox` with `languageKryptonLabel`, `languageKryptonComboBox`,
  `fallbackLanguageKryptonLabel`, `fallbackLanguageKryptonComboBox`, `gameTextKryptonCheckBox`,
  `voicesKryptonCheckBox`, `lobbyKryptonCheckBox`; `onlineRankingKryptonGroupBox` with
  `rankingUserKryptonLabel`, `rankingPointsKryptonLabel`, `rankingRankKryptonLabel`; `modsInUseKryptonLabel`;
- `MainForm`: `modsKryptonCheckButton`.

Order: the removal is the **first** commit of the localization work package, before texts move to
`ApplyTexts()` and get translated, so that no dead text is translated.

Not in v2, with reasons (README "planned"):

- **DirectX wrapper switch** (forum report table 8 row 4): switching means adding or removing DLLs in the game
  folders; the launcher must not change game files (contract 2.5), the setup does it (custom installation).
  The diagnostics report shows whether a wrapper is installed.
- **Resolution choice with 4:3 hint** (row 6): the game has its own resolution option; v2 offers "apply the
  recommended display settings" (contract 3.6) and the warning below 768 pixels. A chooser is a later feature.
- **GPU driver version in the log** (row 4): needs WMI or HKLM class-key reading for little support value; the
  diagnostics report names the primary display adapter (`EnumDisplayDevices`).
