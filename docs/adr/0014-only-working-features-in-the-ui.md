# 0014 Only working features in the UI

Status: **Accepted** (2026-10-02), amended 2026-10-02 (design review and implementation in L-WP3; implementation in
L-WP6, L-WP7, L-WP8 and L-WP9), see the Amendment sections

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

## Amendment 2026-10-02 (implementation, L-WP3)

The 51 controls of the list above (6 on the Launcher page, 31 on the Settings page, 13 on the Play page, the Mods
button) were removed in the first commit of L-WP3 with their fields, designer statements, `Controls.Add` calls and
resx entries; none of them had an event handler. A check of the designer diff (script in the scratch folder of
the v2 work) confirmed that the removal and the following localization commits change nothing in the designer
files but the listed controls and text assignments, so no remaining control moved. `PlaceholderControlsTests`
reads the list above from this file and checks that no name of it appears in a designer file, the code or the
resx files of the launcher, or as a field of the assembly, and that the compatibility warning is still there.

Details:

- **Navigation**: the Launcher button moved up to the position of the removed Mods button (its location is in
  `MainForm.resx`), so the navigation has no gap. This is the only layout change.
- **New controls get new names**: the language setting of ADR 0009 uses `uiLanguageKryptonLabel`,
  `uiLanguageKryptonComboBox` and `uiLanguageHintKryptonLabel`, placed where the removed "When starting the game"
  row was.
- **Still without function until their work packages**, because they are not placeholders of a feature outside
  v2: the Play button and the game choice of the Play page (L-WP6) and the compatibility warning of the Settings
  page, whose options come with L-WP5 (after confirming it the page is empty). The test plan names these gaps.
- The README lists the removed features as planned ("not in v2") with the reasons above.

## Amendment 2026-10-02 (implementation, L-WP6)

The game choice and the Play button, which L-WP3 kept without function as the only exception of this record, work:
Play starts the chosen game (ADR 0010), The Art of Conquest can only be chosen when the installation has it, and the
group shows the file versions and a state line. No control without function is left on the *Play* page.

## Amendment 2026-10-02 (implementation, L-WP7)

- The *Tools* page of the target UI (ARCHITECTURE 2) exists with the functions of L-WP7 only: the integrity state and
  its files, "Check all files" with progress and "Cancel check", "Repair advice" and "Check for updates". The tools of
  L-WP8 and L-WP9 get their controls with their functions; the page is laid out from its texts and scrolls, so they can
  be added below. New controls have new names (`toolsKryptonCheckButton`, `toolsUserControl`, `fullCheckKryptonButton`,
  `integrityKryptonWrapLabel`, `versionCheckKryptonButton`, ...); none is a name of the list above.
- **Navigation**: *Tools* sits between *Settings* and *Launcher* (the order of the page table of ARCHITECTURE 2); the
  *Launcher* button moved down by one place (its location in `MainForm.resx`).
- **Play page**: the game group grew by 20 pixels for the integrity state, its "Details"/"Repair..." button, the result
  line and "Check version"; the info bar below it is 20 pixels lower (90 instead of 110 pixels of text). Test plan
  WP7-14 checks with screenshots that its texts stay readable.

## Amendment 2026-10-02 (implementation, L-WP8)

- The *Tools* page gets the maintenance tools below "Updates", each section with its explanation, state and result
  line: "Old registry entries", "WON login", "VirtualStore", "Saved games and scenarios", "Player names" and
  "Backups" with "Open backup folder". New controls have new names (`cleanupKryptonCheckedListBox`,
  `cleanupDeleteKryptonButton`, `wonResetKryptonButton`, `virtualStoreFilesKryptonTextBox`, `exportSavesKryptonButton`,
  `importEeSavesKryptonButton`, `importAocSavesKryptonButton`, `openBackupFolderKryptonButton`, ...); none is a name of
  the list above, and every text is set in `ApplyTexts()`.
- **No button without an action**: "Delete selected..." works only when the launcher offers a key, one is selected and
  no setup runs; without an offered key the section says "nothing to clean up", shows the read-only list with its
  advice and no enabled delete button (`CleanupView`, tested). The WON reset and the imports need an installation and
  no running setup, the AoC import an installation with AoC; the export needs an installation. HKLM keys have no check
  box at all. The zip export and import were dropped, so there are no buttons for them; the network diagnostics and
  the report come with their functions in L-WP9.
- **Confirmation before every change**: the cleanup asks with the keys and the backup folder, an import that would
  replace a file asks with the files; the default button of both questions is "No".

## Amendment 2026-10-02 (implementation, L-WP9)

- The *Tools* page ends with "Network" and "Diagnostics report", the last two functions of the page table of
  ARCHITECTURE 2. New controls have new names (`networkCheckKryptonButton`, `networkVerdictKryptonWrapLabel`,
  `networkHintsKryptonWrapLabel`, `networkDetailsKryptonTextBox`, `copyReportKryptonButton`, `saveReportKryptonButton`,
  `reportKryptonTextBox`, ...); none is a name of the list above (`PlaceholderControlsTests`), and every text is set in
  `ApplyTexts()` (`ApplyTextsTests`).
- **Every control works**: "Check network" runs the check (disabled while it runs, "Checking the network ..."); the
  verdict, the hints and the details appear only after a check; "Copy report" puts the report on the clipboard and shows
  it in the text box, "Save report..." asks for a file and saves it there, never into an installation. Both always work,
  also before a network check ("Network: not checked") and without an installation. There is no "send" button: the
  launcher sends no report (no telemetry).
- **The link of the Play page** ("Why? Check the network") is visible only while the player list says "not available"
  (`OutageHint.LinksToNetworkCheck`: the server did not answer; not for a list that arrived or a polling that ended by
  an error of the launcher); it opens the *Tools* page at "Network" and starts the check that the click asked for. It
  lies at the top of the group of the online players, over the empty list.
- Not added, with the reason in ARCHITECTURE 16 and the README: the GPU driver version, a resolution chooser and a
  port check from outside. As the design review promised in their place, the report names the display adapter of the
  primary screen (`EnumDisplayDevices`) and says per game whether a DirectX wrapper is installed (the wrapper rule of
  contract 3.3, with its source); the review of L-WP9 against this record found both missing in the first version of
  the report, and they were added (`DiagnosticsReportTests`, `ComputedValuesTests.DirectXWrapper_3_3_TheWrapperRuleWithoutWine`).

Review against this ADR (L-WP9): the designer files contain no name of the removed placeholder list, no new control is
hidden without a function, and the "collect diagnostic data" checkbox stays removed; the report is the opposite of
telemetry (made and passed on only by the player). Test plan WP9-05, WP9-11, WP9-12 and WP9-14 check the controls on
real Windows.
