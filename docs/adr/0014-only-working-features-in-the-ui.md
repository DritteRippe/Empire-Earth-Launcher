# 0014 Only working features in the UI

Status: **Accepted** (2026-10-02), amended 2026-10-02 (design review and implementation in L-WP3; implementation in
L-WP6, L-WP7, L-WP8 and L-WP9) and 2026-10-07 (launcher 1.1.0: the Graphics page and the Mods page), see the Amendment sections

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
  `uiLanguageKryptonComboBox` and `uiLanguageHintKryptonLabel` (a wrapping label since the layout work of 1.1.0, ADR 0017:
  `uiLanguageHintKryptonWrapLabel`), placed where the removed "When starting the game" row was.
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

## Amendment 2026-10-07 (launcher 1.1.0, the Graphics page)

Two of the features that "Not in v2" above left out come back in 1.1.0, within the rule of this ADR (a control is shown
only if it works) and without the file rule changing:

- **The resolution choice returns.** The reason given above for leaving it out was that the game has its own option and
  the launcher offers only the recommended display settings. The new *Graphics* page lets the player choose the size of
  the game window from the usual 4:3, 5:4, 16:10 and 16:9 sizes that fit the primary screen (`ResolutionOptions`), never
  above 1920x1080 (the limit of contract 3.3; forum reports of crashes above it; the project owner decided to keep it
  for 1.1.0) and never below 1024x768. "Use this size" writes `Game Window Width` and `Game Window Height` of every
  game of the installation and nothing else (`GameDefaultsService.SetGameWindow`): these are registry values of class D
  that are on the write allow-list by name already (ADR 0007), no game file changes. The click is the explicit choice
  that contract 3.2 (revision 6) counts as the consent to overwrite a display value; the mutation guard refuses it while
  a setup or a game runs (ADR 0016), a `.reg` backup of the game settings of every game is written first and, if it
  fails, nothing changes (ADR 0007), and the marker is not touched. The setup overwrites both values on every run, so a
  repair or an update sets the recommended size again; the page says so. The game's own resolution option writes the
  same values: whichever wrote last counts.
- **The wrapper is shown, never changed.** The page names the DirectX wrapper the setup installed (`WrapperInfo`: from
  the components of `install.ini`, else of the uninstall key, only without component information from the wrapper files,
  the rule of contract 3.3) and, for a wrapper that can be dgVoodoo, shows `OutputAPI` and the keys that decide the
  screen mode of its `dgVoodoo.conf` (`DgVoodooConf.ScreenModeKeys`), read where the game reads the file (the
  VirtualStore copy first, ADR 0016). Switching the wrapper means adding or removing DLLs, which the launcher must not do
  (contract 2.5, 4.1) and cannot (the setup grants no right to create or delete code files); the page says so and tells
  the steps in the suite and the setup wizard in the words of both (suite: "Advanced ...", then "Custom install
  settings", then "DirectX Wrapper", or "Recommended settings" and "Native" for none). **The launcher does not change
  `dgVoodoo.conf` or `dreXmod.config` in 1.1.0**: editing them needs a contract section and an allow-list of files and
  keys and is planned for 1.2, after the tests on real computers show which keys really help.
- **Names.** The controls of the page have new names (`windowSizeKryptonComboBox`, `windowSizeApplyKryptonButton`,
  `wrapperInstalledKryptonWrapLabel`, ...); none is a name of the list in the amendment of 2026-10-02
  (`PlaceholderControlsTests`), and every text is set in `ApplyTexts()` (`ApplyTextsTests`). `GraphicsView` holds what
  the page decides, so that it is tested on Mono, where the Krypton combo box cannot be created; `PageLayoutTests`
  measure the page on Windows.
- **Still not in the UI**: the DirectX wrapper switch (the wizard of the setup does it), an editor of the screen mode, and a
  choice above 1920x1080.

## Amendment 2026-10-07 (launcher 1.1.0, the Mods page)

The *Mods* page that the amendment of 2026-10-02 removed returns in 1.1.0 as a page that does what the launcher can do
truthfully today: it shows. The placeholder promised a mod system; what exists and works is a list of the dreXmod presets
the setup installed and the choice that `dreXmod.config` makes, so that is what the page offers, and nothing the
launcher cannot do (contract 2.5: it does not change, delete, restore or download game files):

- **Only for dreXmod 3.** dreXmod 3 has presets (the folders of `Data\dxm\mods`) and two independent selectors in its
  `dreXmod.config`, `Mod` (sounds and textures of the game) and `LobbyTheme`; dreXmod 2 has no mod system. The page and its
  navigation button exist for an installation whose setup run installed the component `additional\drexmod\v3`
  (`DreXmodInfo`; without component information the folder of the presets decides) and are hidden otherwise, so no control
  ever leads to a page that has nothing to show. The button of the page is called `modPresetsKryptonCheckButton`, not
  `modsKryptonCheckButton`, which stays on the list of removed names (`PlaceholderControlsTests`).
- **What it shows**: for each game the presets with the name of the folder (the value `dreXmod.config` names), the name,
  `Last Edit` and `Created by` of the `CREDITS` file where there is one (a tolerant parser: the file is free text), the size,
  the badges "active mod" and "active lobby theme" (a selector that is on and names the folder, ignoring case), and the
  choice of the config in words, also when it names a folder that does not exist. The skeleton `template` is hidden unless
  asked. There is no version and no description: the files of a preset have none. The two buttons open the folder in the
  Explorer and the config in the program Windows has for it (`IProcessStarter.OpenFolder`, `OpenFile`: through the shell
  without a verb; a program, script, shortcut or registry file is refused); they change nothing.
- **Read only, as the rule of the wrapper page: the launcher does not change `dreXmod.config` or `dgVoodoo.conf` in 1.1.0.**
  Switching the preset means writing two values of a game file; that needs a contract section, an allow-list of files and
  values, a backup and the guard, and the tests on real computers must first show what dreXmod does with a name that is no
  folder. It is planned for 1.2. Installing mods (`.eem`) is not planned for the launcher: the format has no target game,
  no hash that is checked and no uninstall record. `ModsPageRulesTests` keep the sources of the page free of every call
  that writes (`WriteAllBytes`, `DeleteFile`, `CreateDirectory`, `File.*`, the registry, the backup, the guard).
- **Honest texts**: the page says how to switch a preset by hand, that a setup run installs `dreXmod.config` anew (the choice
  returns to the shipped default: mod off, lobby theme `dxm`) but keeps presets the player made (the setup deletes only what it
  installed; setup repository), and that whether a mod has an effect in multiplayer or ranked games, and what dreXmod does with
  a name that is no folder, has not been verified. It does not offer mods as a fix for the mouse or the window problems.
- **Names and tests.** The controls have new names (`modsGame1PresetsKryptonWrapLabel`, `modsGame1OpenFolderKryptonButton`,
  `modsShowTemplateKryptonCheckBox`, ...); every text is set in `ApplyTexts()` or in `ModsUserControl.ShowState` from
  `ModsView` (`ApplyTextsTests`). `ModsView` and `ModsModel` hold what the page decides and are tested on Mono; the page is
  measured by `PageLayoutTests` in four states (`ModsPageWorld`).
- **Still not in the UI**: switching the preset, installing mods, a mod catalogue or downloads.
