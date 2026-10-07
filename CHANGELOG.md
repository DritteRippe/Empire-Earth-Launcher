# Changelog

All notable changes to Empire Earth Launcher, its libraries and the mod creator are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html). All assemblies carry the version of
`SharedAssemblyInfo.cs`: 1.0.0 since the optional additions for the suite installer, `0.1.0-alpha` before. The fixes of
the code review that preceded v2 (branch `refactor/quality-fixes`) are described in the git history.

## [Unreleased]

### Changed

- `docs/CONTRACT.md`, revision 6 (identical to the copy in the setup repository; contract version still 1, compatible, no
  MUST or MUST NOT relaxed): one revision with the launcher part (the player's explicit choice of the game window size is
  the consent of 3.2, within the limits of 3.3, after the guard and the backup of 3.6; the Graphics page below) and the
  suite part of suite 1.1.0 (1.7 points 2, 3 and 5: the product setups run with `/VERYSILENT`, the suite reads their log
  lines for its progress display and waits with a process handle), which asks nothing of the launcher. Contract 7
  "Additions of revision 6" lists the launcher items; ARCHITECTURE section 15 ticks them and `ContractChecklistTests`
  checks that table like those of revisions 4 and 5.
- The launcher window can be resized freely; its smallest size is the size it opens with, and the content grows with the
  window (report 4c: a maximized window showed the same content in the upper left corner). The navigation buttons sit in a
  panel at the left edge and the pages fill the rest (`MainForm`). The *Game settings* and *Tools* pages stack their
  controls for the width of the window (`ScrollPageLayout`): texts, lists and boxes take the whole width, a wrapping label is as
  high as its text needs at that width, buttons keep the width their text needs and wrap into a second line where they do not
  fit, and the page keeps its scroll position. A check box or label whose one-line text is wider than the window (a long
  translation in a large font) makes the page scroll sideways instead of cutting the text off. The block of the compatibility
  warning (book picture, text, button) has the width of the page and the height of its parts, so the picture stays inside it
  below the heading. The installation line ("NeoEE in C:\...", any path length) of both pages and the line "no hints" wrap
  (they were labels of one line) ([ADR 0017](docs/adr/0017-resizable-layout.md); test plan WP1-07, WP5-17).
- The *Play* and *Launcher* pages grow with the window and stack by text height as well (ADR 0017, amendment). *Play*: the
  online players and the Play button keep their width at the right edge and the height of the page (the player list takes the
  room between the heading and the profile line, the state line above it takes the height of its text); the group of the game
  choice, whose texts (program versions, result of the version check, integrity state, state line) were labels of a fixed
  height that cut a long German translation off, is as wide as the game column and as high as its texts need, a text that is
  empty takes no room, and the info bar sits below the group; the column scrolls where the window is too small. *Launcher*: the
  group of the settings takes the width of the window, the text box of the game folder and the list of the installations grow
  with it, the list takes the height the window leaves, the labels of the rows share one column, the line with the origin of
  the game folder and the note about the new language wrap (they were labels of one line), and the page scrolls where the
  window is too small (test plan WP1-07, WP3-06, WP5-17, WP6-14).

- The update question and the repair advice open the download page of the product (contract 4.3, revision 6):
  `https://empireearth.eu/download/ee/` for EE, `https://empireearth.eu/download/neo/` for NeoEE, `https://empireearth.eu/download/`
  for a foreign installation and an unknown product (`Repair.SetupDownloadPage`), instead of the address the update API
  names. That address pointed to `cdn.empireearth.eu`, which no longer resolves, and the URL check let it pass because it
  is a subdomain of `empireearth.eu`. The window shows the address at once, without a request and without a wait; the
  browser gets the setup file from the site. The update API is asked only for the version check and, as the reference of the
  network check, with `&type=game` (the latest game version) instead of the query without `&type=`
  ([ADR 0008](docs/adr/0008-https-policy-and-update-api.md), amendment of 2026-10-07; test plan WP7-12, WP14-01 to WP14-03).
- A second start of the launcher without an argument brings the window of the running launcher to the front and ends without
  a message (it said that the launcher is running). `InstanceMessage` carries `show` for it; `--product=EE|NeoEE` as before
  also selects the product ([ADR 0010](docs/adr/0010-game-start-and-mutex-probing.md), amendment of 2026-10-07; test plan
  WP13-06).
- `settings.json` keeps the folder you chose per product (`ProductFolders`) and the product chosen last (`LastProduct`), both
  optional members, so `SchemaVersion` stays 1. `GameDirectory` repeats the folder of the last product, so that launcher 1.0.0
  reads the same installation, and a `GameDirectory` that is not the folder of the last product is read as the choice of an
  older launcher (ADR 0005 amendment, rule 3; [ADR 0005](docs/adr/0005-own-settings-file-instead-of-user-config.md), amendment of
  2026-10-07; test plan WP13-02, WP13-03).
- `docs/CONTRACT.md`, revision 6, extended (still unreleased, contract version still 1): the one shortcut of suite 1.1.0
  that starts the launcher without an argument (1.7), the choice per product in the default selection (1.4), the
  download pages of the products (4.3, instead of the answer of the update API) and the items of "Additions of revision 6"
  in 7; ARCHITECTURE section 15 ticks them.

### Fixed

- Game start: after the start the launcher hands the foreground to the window of the game (report 1: the mouse was dead
  until the window was minimized and restored, probably because the game was never activated while the launcher stayed in
  front). `GameStarter` allows the foreground (`AllowSetForegroundWindow`) right before the shell start; `GameWindowActivator`
  then looks every 100 ms (60 s at most) for the first visible window of the game process, calls `SetForegroundWindow` on it
  while the launcher or the game owns the foreground, looks again after 2 s and hands it over again at most three times.
  If another program is in front the launcher changes nothing; it never minimizes, hides or closes a window and never ends
  a process; the start stays a shell start (ADR 0010 amendment of 2026-10-06, `IWindowSystem` / `WindowsWindowSystem`,
  `ForegroundRulesTests`, test plan WP6-18). One log line says after how many milliseconds the window was brought to the
  foreground. Whether this is the cause is decided by the test on real Windows (WP6-18).
- Game start, the hand-over of the foreground after the review of run 5d (A1; [ADR 0010](docs/adr/0010-game-start-and-mutex-probing.md),
  amendment of 2026-10-07): the window is the main window, not the splash `Loading Game Window` (a tool window that
  `FindVisibleTopLevelWindow` skips, `WindowRules`); a game that is in front already gets no `SetForegroundWindow` (`... already in
  the foreground, nothing to do.`); a missing foreground window (Windows has none for a moment while a window is created or
  the display mode switches) is looked at again three times and counts as nobody's, no longer as "the player switched"; and
  for 60 s after the hand-over the launcher only watches, every 250 ms, and logs each change of the foreground window and of
  the rectangle and styles of the main window with the time since the start (`Watch t+10.0 s: ...`, `WindowState`). That is a
  measurement of when a wrapper such as dgVoodoo switches the display mode; A1 does not fix the dgVoodoo case, and the
  `WM_ACTIVATE` of A1b is not part of 1.1.0 (test plan WP6-18).
- "Auto-detect" on the *Launcher* page keeps the product the player is looking at also when the launcher was started with
  `--product=` (an old shortcut, the hand-over of a second launcher): the product is saved as the product chosen last, so no
  page jumps to the product chosen before (`InstallationService.UseAutomaticDetectionAsync`; test plan WP10-01).
- `settings.json` after a downgrade and back: launcher 1.0.0 empties `GameDirectory` with its "Auto-detect" and does not know
  `ProductFolders`; an empty `GameDirectory` next to a folder of the product chosen last is read as that automatic detection
  and the folder is dropped in memory (`ProductChoices.DropFolderClearedByOlderLauncher`, ADR 0005 amendment; test plan
  WP13-07).
- "Open dreXmod.config" on a Windows with no program for `.config` ended with an error message (error 1155); the launcher now
  asks Windows for the "Open with" dialog (`openas`). `IProcessStarter.OpenFile` is an allow-list now (`.config`, `.conf`,
  `.txt`, `.ini`, `.log`) instead of a list of what Windows runs (test plan WP12-04).
- The size chosen in the list of the *Graphics* page is kept only for the installation it was chosen for; every click on the
  Play list can switch the product, and the other installation starts from its own sizes (`GraphicsView.KeepChoice`).
- Game settings page: the header, the description and the "NeoEE in ..." line lay on top of each other, and the book picture
  of the compatibility warning lay over the buttons "Apply recommended display" and "Reset game settings". The page is
  created hidden and filled before the window is shown, and its layout skipped the header, the installation line and
  the two buttons because `Control.Visible` reads false then (`SettingsUserControl.IsShown`); they kept the places of the
  designer.

### Added

- README: the section "FAQ and known issues" (K1) says what is known, what is only probable and what has not been verified
  about three reports of the laptop test. The mouse that is dead in the menu until the window is minimized and restored: the
  probable cause (the game acquires its DirectInput devices with the foreground cooperative level) and what the hand-over of
  the foreground does and does not do. The game that minimizes itself when another window takes the foreground: it is
  behavior of the game program (`Empire Earth.exe` minimizes its window when its application is deactivated), which the
  launcher cannot and may not change (no game program is modified, the NeoEE programs stay untouched); what avoids the
  triggers (tray programs, **Do not disturb** / *Nicht stören* of Windows 11, no second launcher start with `--product` while
  a game runs) and that the aim is a reliable restore, not a game that never minimizes. A hand edit of `dgVoodoo.conf` that
  does nothing (the VirtualStore copy of the file). The 2 GB limit of the 32-bit game programs, which is address space, not
  RAM, with the PowerShell command that measures the peak of a big game; no launcher or program change comes with it. What
  to send with a report (diagnostics report, the log lines of the hand-over, what was seen). The comparison table and the
  feature list name the hand-over and the resizable window. Test plan WP6-19 (the game minimizes itself, with and without
  Do not disturb) and WP6-20 (optional: the memory peak of a big game); the cases of this version's other changes are WP1-07,
  WP3-06, WP5-17 and WP6-14 (layout), WP6-18 (foreground), WP8-19 (VirtualStore copy), WP11-01 to WP11-08 (Graphics) and
  WP12-01 to WP12-09 (Mods).
- Mods page (M2; [ADR 0014](docs/adr/0014-only-working-features-in-the-ui.md) amendment of 2026-10-07): the planned
  "dreXmod support" and "browsable mods list" in a form that is true today, read only. For an installation whose setup run
  installed dreXmod 3 (the component `additional\drexmod\v3`; without component information the folder `Data\dxm\mods`
  decides) a sixth navigation button leads to a page that lists, for each game, the presets in `Data\dxm\mods` (the folder
  name, which is what `dreXmod.config` names, with the name, `Last Edit` and `Created by` of the `CREDITS` file and the size;
  `CreditsParser` is tolerant of free text, missing lines and dates that are none) and marks the active mod and the active
  lobby theme that the `<Mod>` and `<LobbyTheme>` blocks of `dreXmod.config` name (`DreXmodConfigReader` reads the file as
  text where the game reads it, a VirtualStore copy first: a comment before the first element, tabs, CRLF, element names
  inside comments and the variant without telemetry are no problem). The folder `template` (the skeleton for authors) is
  hidden unless the check box asks for it. "Open mods folder" and "Open dreXmod.config" open the folder in the Explorer and
  the file in its program through the shell (`IProcessStarter.OpenFile`, which opens only `.config`, `.conf`, `.txt`, `.ini`
  and `.log` files and refuses anything else, and asks Windows for the "Open with" dialog when no program is registered for
  `.config`, as on a stock Windows); the page says how to switch a preset by hand, that a setup run resets the choice to the shipped default
  but keeps presets the player made, and that whether a mod has an effect in multiplayer or ranked games has not been
  verified. dreXmod 2 has no mod system and no page. There is no version and no description in the files of a preset, so the
  page shows none. **The launcher writes nothing here, installs no mod and changes neither `dreXmod.config` nor
  `dgVoodoo.conf` in 1.1.0** (switching the preset comes in 1.2 with an allow-list; `ModsPageRulesTests` keep the sources
  free of anything that writes). `ModsModel` reads after every search and when the page is shown and never while a setup
  runs, `ModsView` holds what the page decides, `NavigationStack` closes the gap of the navigation bar when the button is
  hidden (the Play page is shown if the page was open). Texts in English, German and French. Test plan WP12-01 to WP12-09.
- Graphics page (E1; contract 3.2 revision 6, [ADR 0014](docs/adr/0014-only-working-features-in-the-ui.md) amendment of
  2026-10-07): the player chooses the size of the game window from a list of the usual 4:3, 5:4, 16:10 and 16:9 sizes that
  fit the primary screen, between 1024x768 and 1920x1080 (the limit of contract 3.3; offering more was not decided for
  1.1.0), and "Use this size" writes `Game Window Width` and `Game Window Height` of every game of the installation and
  nothing else (`ResolutionOptions`, `GameDefaultsService.SetGameWindow`): the mutation guard first (no setup, no game), a
  `.reg` backup of the game settings of every game, a refusal for an installation of a newer contract and for a size
  outside the limits; the marker is not touched. The page says that a repair or an update with the setup writes the
  recommended size again and that the game's own resolution option writes the same values, and it notes a scaled screen
  (the game then sees a smaller screen; the *Settings* page offers HIGHDPIAWARE). Below it the page shows, read only, the
  DirectX wrapper the setup installed (`WrapperInfo`: native, DirectX 7, DirectX 9, dgVoodoo with DirectX version and API
  level, from `install.ini`, the uninstall key or, without component information, the wrapper files) and, for a wrapper that
  can be dgVoodoo, `OutputAPI` and the keys of `dgVoodoo.conf` that decide the screen mode (`DgVoodooConfReader`, read where
  the game reads the file, a VirtualStore copy first; tolerant of CRLF, comments, tabs and missing keys), and the steps to
  change the wrapper in the setup, in the words of the suite and the wizard in each language. **The launcher changes no
  file and does not change `dgVoodoo.conf` or `dreXmod.config` in 1.1.0** (editing comes in 1.2 with an allow-list).
  `GraphicsModel` reads after every search and when the page is shown and never while a setup runs, `GraphicsView` holds
  what the page decides (tested on Mono, where the combo box cannot be created), `GraphicsPageWorld` drives the page for the
  geometry tests and the page pictures. Texts in English, German and French; a fifth navigation button. Test plan WP11-01
  to WP11-08.
- VirtualStore check: a copy of `dgVoodoo.conf` in `%LOCALAPPDATA%\VirtualStore\<game folder>` that differs from the file
  in the game folder is reported (A5). The game reads the copy, so a hand edit of the real file has no effect. The *Tools*
  page shows a hint with the copy, the button "Open VirtualStore folder" opens its folder, `log.txt` and the diagnostics
  report name it (`VirtualStoreFinding.DiffersFromOriginal`, `VirtualStoreReport.ShadowingWrapperConfigs`). Only that
  file is compared (by length, then by content); an identical copy gives no hint. The launcher offers only to open the
  folder and never deletes the copy (contract 2.5). Test plan WP8-19.
- Geometry tests of the four pages (`PageLayoutTests`, `LayoutChecker`, ADR 0012 amendment of 2026-10-06): no overlap,
  nothing outside its page, no cut-off text and content that grows with the page, at four window sizes, in English, German
  and French, with the system font and one 50 % larger; the *Game settings* page is driven through its real model in
  every state (`SettingsPageWorld`). The list of rules the page broke under Mono (`KnownDefects`) is gone with the layout
  work of 1.1.0 (ADR 0012 amendment of 2026-10-06 and ADR 0017); `ScrollPageLayoutTests` and `MainWindowLayoutTests` test the
  layout class and the docking of the main window.
- CI: the step "Render page pictures" saves PNG files of the pages (`PageScreenshotTests`) and uploads them as the artifact
  `page-pictures`.
- Geometry tests of the *Play* and *Launcher* pages in states (`PlayPageWorld`, `LauncherPageWorld`: long texts of the language,
  info bar, state line of the player list, long folder and hints), the rules for radio buttons and the named excuses of
  `LayoutChecker.NarrowByDesign`, and the first layout of a page created at its size (`LauncherPages.Resize`); the cases
  need the Krypton combo box and lists and run on Windows only (ADR 0012 amendment of 2026-10-06). `ScrollPageLayoutTests`
  covers the additions to the layout class.
- One list of the four games on the *Play* page: "Empire Earth", "Empire Earth - The Art of Conquest", "Neo Empire Earth" and
  "Neo Empire Earth - The Art of Conquest" (English, German and French), then the *Play* button. A game that is not installed
  is greyed out, with one hint (the four games stay in the same place); the choice is remembered (`LastProduct`, `LastGame`)
  and selects the installation of that product for every page, so the NeoEE player list shows only for NeoEE
  (`PlayEntry`, `PlayModel.Entries`, `GeneralUserControl`; [ADR 0014](docs/adr/0014-only-working-features-in-the-ui.md),
  amendment of 2026-10-07; test plan WP13-01 to WP13-08). The radio buttons replace the former choice of the game with its
  separate Art of Conquest option.
- One chosen folder per product (`ProductChoices`, `UserChoice`, `DiscoveryResult.ForProduct`, `InstallationDiscovery.DiscoverChoices`):
  choosing a folder or an installation on the *Launcher* page for EE leaves the choice for NeoEE as it was, and the other
  way round; *Auto-detect* clears the choice of the selected product only.
- `--product=EE|NeoEE` preselects the game of that product in the list for the session (the folder chosen for that product
  wins, nothing is saved); the hand-over to a running launcher is unchanged. The shortcuts of suite 1.0.0 keep working, suite
  1.1.0 creates one icon "Empire Earth Community" without an argument.
- Geometry and picture tests of the *Play* page with the four entries (`PlayPageWorld` states `AllEntries` and
  `EntriesWithHint`, `PlayPageEntriesTests`); they run on Windows only. `NetworkDestinationTests` allow the update API and the
  three download pages as URL literals and nothing else.

### Removed

- The request for a download URL and its check: `SetupDownloadLocator`, `SetupDownloadLocation`, `FallbackReason` and
  `UpdateUrlPolicy` (the port of the setup's `IsAllowedUpdateUrl`) with their tests; `UpdateApi` keeps the query of contract 4.5
  and the failure reasons of the version check (`UpdateApiFailure`).
- `DiscoveryResult.ForSessionProduct`, `PlayModel.SelectGame` and `CanChooseArtOfConquest`, and three texts of the repair
  window (`FailureUrlRejected`, `RepairLocating`, `RepairFallbackFormat`), in all three languages.

## [1.0.0] - 2026-10-05

Launcher v2 was built on branch `v2` in the work packages L-WP1 to L-WP9 ([docs/ARCHITECTURE.md](docs/ARCHITECTURE.md),
section 15); all of them are done, newest first below. Version 1.0.0 adds the optional additions of contract revision 4
for the suite installer "Empire Earth Community" of the setup repository (setup ADR 0013), which installs the launcher
and the games in one run; the launcher works as before without it. The test on real Windows follows
([docs/TEST-PLAN.de.md](docs/TEST-PLAN.de.md), L-WP10 for the suite additions).

### Added

- Launcher 1.0.0, the optional additions of contract revision 4 (`docs/CONTRACT.md` 0, 1.4, 1.6, 4.2, 4.4; checklist at
  the end of ARCHITECTURE section 15, test plan WP10-01 to WP10-07):
  - `Main(string[] args)` with `--product=EE` and `--product=NeoEE` (the argument of the suite's game shortcuts,
    `LauncherArguments`): the first installation of that product is selected for this session only, in the order of the
    sources and with the user's choice first if it is of that product (`DiscoveryResult.ForSessionProduct`,
    `InstallationService.SelectProductForSession`). Nothing is saved, the choice of an installation in the list ends
    it, an invalid value or an unknown argument is ignored and logged (the value must be exactly `EE` or `NeoEE`), and
    so is a product without an installation.
  - Second launcher: a launcher that finds the single-instance mutex taken and was started with `--product` hands the
    product to the running one and ends without a message (`InstanceForwarder`; `WM_COPYDATA` to the message-only window
    `EmpireEarthCommunityLauncher.<Windows session id>`, `WindowsInstanceChannel`, `InstanceMessageWindow`; up to 50
    attempts 200 ms apart while there is no window, because it appears a moment after the mutex; a timeout is not
    retried and not reported, the message is queued). The running launcher
    (`InstanceReceiver`, `LauncherInstanceTarget`) brings its window to the front (`AllowSetForegroundWindow` of the
    second process, `SetForegroundWindow`, restore if minimized) and selects the product unless a game start, an
    operation or an open dialog holds it (then the product is applied when it is idle again; the window accepts
    `WM_COPYDATA` from a non-elevated launcher). A message that is not exactly `product=EE` or `product=NeoEE` changes nothing; without `--product`, or if the
    hand-over does not work (for example an elevated running launcher, UIPI), the usual "already running" message
    follows.
  - The suite mutex `EmpireEarthCommunity_Suite` is a setup mutex like `EE_Setup` and `NeoEE_Setup` (contract 4.2):
    `SetupKind` (NeoEE, EE, Suite) replaces the product in `RunningGameDetector`, `MutationGuard`, `SetupWatcher`,
    `GameStarter`, `InstallationService`, `IntegrityModel`, the models and the texts. The suite holds it for its whole
    run, so the launcher does not search, start or change anything in the gap between the two product setups where
    neither product mutex exists, and the watcher raises one "setup finished" for the whole run.
  - `SuiteRecordReader` / `SuiteRecord` (contract 1.6): the suite record `HKLM64\Software\Empire Earth
    Community\Suite`, read-only and tolerant (a missing key is no record and is not logged, a value of another type or an
    invalid path is left out), no discovery source. `SuiteRepairLocator` gives the folder `SourceDir` to the repair advice
    when the record lists the product of the installation, the folder exists and the installation is not foreign.
  - Repair advice with the suite (contract 4.4): the first step is "Run "Empire Earth Community Setup" again from the
    folder you unpacked it to (...)" (`RepairStep.RunSuiteSetupAgain`) instead of the download of the product setup; the
    window has a button "Open setup folder" that opens the folder in the Explorer and never starts a program from it
    (contract 4.1), and the official download page stays below as the second option ("If that folder is gone, ..."). The
    steps for folder, mode and the NeoEE CD-key task stay; all new texts exist in English, German and French
    (`RepairStepRunSuiteFormat`, `RepairDownloadPageSuiteLabel`, `RepairOpenSuiteFolderButton`,
    `RepairSuiteFolderNotOpenedFormat`, `OnlinePlayersNeoOnly`; `SetupRunningFormat` and `BlockedBySetupFormat` also name
    "Empire Earth Community").
  - The online player list is polled only while the selected installation is NeoEE (`PlayerListPolling`): with EE, a retail
    or GOG installation or none the launcher sends no request to the status server ("Online Players (NeoEE only)"), and a
    new selection starts or ends the polling.
  - Tests without GUI and network on the existing fakes: `LauncherArgumentsTests`, `InstanceForwardingTests` (round trip in
    one process through a fake channel), `LauncherInstanceTargetTests`, `DiscoveryResultSessionProductTests`,
    `SuiteRecordReaderTests`, `SuiteRepairTests`, `SetupKindTests`, `PlayerListPollingTests`, and the suite cases in
    `SetupWatcherTests`, `MutationGuardTests`, `GameStarterTests`, `RunningGameDetectorTests`, `InstallationServiceTests`.

- Contract revision 5 (`docs/CONTRACT.md` 0, 1.3, 1.4, 1.6; checklist at the end of ARCHITECTURE section 15, test plan
  WP10-10): `UninstallKeyScanner` skips the suite's own uninstall key, recognised by `Empire Earth Community: Suite` or,
  for a suite built before revision 5, by the suite root `InstallPath` of the suite record (with an AppId the record
  does not embed). `InstallationDiscovery` reads the suite record for it; the record is still no source.

- Real-machine checks for the CI end-to-end test of the setup repository (`Empire-Earth-Launcher.RealMachineTests`,
  ADR 0012 amendment of 2026-10-03): a second NUnitLite program that runs the launcher core against a real installation
  on a GitHub-hosted Windows runner after each step of a scenario: discovery, quick and full integrity check, status of
  the defaults and consistency findings, on request the defaults of the launcher start, a second start and the reset, and
  a final comparison of the CD keys, records, Inno uninstall keys, compatibility layers, HKLM game settings and the files
  of the installation with their state before. A strict JSON expectation file per step says what to check;
  `pick-targets` names one code, data and mutable file of the EE folder for the damage tests. The fixtures (category
  `RealMachine`) are explicit and need `EE_LAUNCHER_REAL_MACHINE_TESTS=1` on Windows on a GitHub-hosted runner; elsewhere
  only the 93 self-tests run, on the in-memory fakes of the unit tests. The core log stays in the work folder and no output
  holds a hash. `RealMachineTestRulesTests` checks that the program keeps the rules of `TestIsolationTests` except the
  one registry adapter, and that nothing references it, so the laptop package's `Tests\` folder never contains it.
  The setup workflow runs a pinned commit of the program that must be on the launcher branch it names (README, Tests,
  "Real machine").
- Network check (R7, `Empire-Earth-Launcher-Core/Diagnostics/NetworkDiagnostics`, `DiagnosticsModel`), only when the
  player clicks "Check network" on the *Tools* page or the link below an unavailable player list: the network adapters
  (type, driver name, state, IPv4 with prefix and gateway, IPv6 only as none, link-local only or available; VPN, Hamachi
  and other virtual adapters marked, forum 4.10), the name lookup of the NeoEE status server and of the `Server` of each
  `NeoEE.cfg` (5 seconds each), the update API with the query of contract 4.3 (only the AppId is sent; not asked without
  an installation that has one) and the NeoEE status server with the request of the player list, all at the same time.
  Hints for a computer without an IPv4 gateway (offline, or IPv6 only), virtual or VPN adapters, several adapters with a
  gateway, and from `upnp_info.txt` a CGNAT (100.64.0.0/10) or missing external IPv4 address (DS-Lite when a global IPv6
  address exists) or a private one (a second router). No "what is my IP" service, no connection to the ports 10002 and
  10003 of NeoEE; `NetworkDestinationTests` keeps the launcher to its three destinations.
- Outage hint (forum report section 8 row 9, not droppable, `OutageHint`): when the status host resolves and the update
  API answers but the status server does not, the verdict is "Probably a server outage, not your computer"; every other
  combination of name lookup, update API and status server has its own verdict (no connection, a firewall, a name that
  does not resolve, not determinable without the update API, status server not configured), tested in all 18
  combinations. Below a player list that says "not available" the *Play* page shows "Why? Check the network", which opens
  the *Tools* page at "Network" and starts the check.
- `NeoEE.cfg` and `WONLobby.cfg` of both game folders, read only (the VirtualStore copy first, at most 64 KiB):
  `Active`, `Server`, `DefaultPort`, `MemberPorts`, `PortCheck`, `TryUPnP`, `CDKeyCheck`, `EEFileTransferPort` and
  `LobbyPort`, invalid values named; the port forwarding table for hosting (default 33334 and 33336 TCP+UDP, 33335 TCP,
  forum t=11057 p=48100) with the IPv4 address of this computer when exactly one real adapter has a gateway; hints when
  RIP hosting is off and, for NeoEE, when `CDKeyCheck` is not `true` (t=10950; the launcher never offers to change it).
- `upnp_info.txt` with a tolerant parser (`UpnpInfoParser`): labelled lines of the external and the local address and up
  to ten port lines are recognized, anything else is "unknown format"; the external address is kept only as its class.
  This part could have been dropped (ARCHITECTURE 15); it is implemented. Its real format is still to be confirmed with a
  file from a real computer (test case WP9-10).
- Diagnostics report (ARCHITECTURE 4.6, `DiagnosticsReport`, `ReportAnonymizer`): "Copy report" and "Save report..." on
  the *Tools* page give one English text with the launcher and Windows version, the screen and its display adapter, the
  installations with their folders, kinds, versions and AppIds, the program versions, whether a DirectX wrapper is
  installed per game (contract 3.3, with its source), the integrity state and its findings, the game defaults and the
  consistency findings, the VirtualStore, the name check, whether each `CDKeys` key exists, the cleanup counts and the
  latest network check. Privacy rules of ADR 0013 (plan review): paths with `%USERPROFILE%`, `%LOCALAPPDATA%`, `<user>`
  (also `D:\Users\<name>` and VirtualStore paths), `<computer>`, `<server>` and `<domain>`; public, CGNAT and IPv6
  addresses only as their class; no MAC address, adapter GUID, user-chosen adapter name or DNS suffix; player and profile
  names only as "EE lobby profile 1: characters outside printable ASCII"; never a CD key. The report is saved as UTF-8
  into the file the player chooses (never into an installation), never sent, and never logged.
- Platform: `INetworkInfo` with `WindowsNetworkInfo` (adapters, name lookups with a 5-second limit) and `FakeNetworkInfo`;
  `ISystemInfo.PrimaryDisplayAdapter` (the device string of `EnumDisplayDevices`); `ComputedValues.DirectXWrapper`, the
  wrapper rule of contract 3.3 on its own.
- 76 new texts in English, German and French for the network check, the report and the link of the *Play* page (now
  369); the report and the log stay English.
- Tests: the readers, the address classes, the outage verdict, the hints, the port table and the privacy of the log lines
  with fakes; the report as a golden file (`Core/Diagnostics/Golden/DiagnosticsReport.txt`, CRLF) and a negative test
  with a computer full of IP, IPv6 and MAC addresses, adapter GUIDs and names, computer, domain, user and player names
  and a filled `Software\Sierra\CDKeys` (`NOT-A-KEY-0000`); `DiagnosticsModel` (only on request, one check at a time,
  saving refused inside an installation); `NetworkDestinationTests`; `ContractChecklistTests` (the ticked contract
  checklist of ARCHITECTURE 15 names only existing test classes); `TestPlanTests` in their final form.
- Test plan: cases WP9-01 to WP9-16 (adapters and VPN with the collection task where the game stores its adapter
  choice, cable and WLAN, offline, the outage with a wrong status port, a wrong host and a firewall rule, no AppId,
  `NeoEE.cfg`, `WONLobby.cfg` and the port table while hosting, RIP hosting and `CDKeyCheck` off, the collection task
  for `upnp_info.txt`, reading the copied report for personal data, saving it, the privacy of the log, the three
  languages, the laptop package, the log) and W7-06; R7 and forum test cases 11 and 12 are assigned, nothing in the plan
  is "offen" any more, and the table "Vertrag 7" assigns the five launcher items of the contract checklist to cases.
- Nothing of L-WP9 was dropped. Dropped in v2 as ARCHITECTURE 15 allows, all in L-WP8: the zip export and the zip import
  of saved games. Kept as planned and not in v2 (README): the DirectX wrapper switch, a resolution chooser, the GPU
  driver version in the log, a port check from outside, ending a hanging process, the mods page and the other old
  placeholders.

- Registry cleanup (R5, [ADR 0007](docs/adr/0007-registry-write-scope-and-reg-backups.md);
  `Empire-Earth-Launcher-Core/Maintenance`: `CleanupCandidates`, `CleanupAdvice`, `RegistryCleanup`): cleanup of HKCU
  entries; HKLM entries are only shown, with advice. The list is a code table of 17 explicit keys without wildcards,
  equal to the table of ARCHITECTURE 4.6 (a test compares them), each with evidence from the forum or the setup
  (`t=`, `p=` or `setup:`): the four game settings keys of contract 3.1 and the registry VirtualStore copies of the SSSI
  and Mad Doc keys, which the launcher may delete; the SSSI and Mad Doc keys of HKLM (32 and 64 bit), shown with the
  advice to export them and delete them with the Registry Editor as administrator; and `Software\Sierra` in HKLM, HKCU
  and the VirtualStore, shown as "do not delete: it contains the NeoEE CD keys" with only whether `CDKeys` exists. A key
  is offered only when no installation of its product is found and the folder of its own "Installed From" values is
  missing on a present, fixed, local drive; otherwise the page says why it is kept. Deleting asks first, then the
  mutation guard, checks every selected key again, writes one `.reg` file of all of them into
  `Backups\<time>_registry-cleanup\` and only then deletes them; if the backup fails nothing is deleted, and whether
  the CD keys exist is logged before and after. Without a key to delete, the page says "nothing to clean up", shows the
  read-only list and no enabled delete button. Keys without evidence (other subkeys of `Software\Sierra`, Stainless
  Steel Studios, InstallShield) are not on the list; test case WP8-02 collects samples.
- WON login reset (R6, `WonLoginReset`, forum p=83519): `_wonkver.pub` and `_wonlogin.ks` of the Empire Earth and The
  Art of Conquest folders and of their VirtualStore copies are moved into `Backups\<time>_won-login-reset\` (`EE\`,
  `AoC\`, `EE-VirtualStore\`, `AoC-VirtualStore\`, with `moved-files.txt`); files of the setup's list are never moved,
  and nothing is moved when that list exists but cannot be read. A file Windows does not let the launcher remove is
  named ("access denied"). The page says that the backup folder now contains login data.
- File backups (`Backup/FileBackup`): files are copied into the backup folder and read back, the list of their original
  places is written, and only then are the originals removed (or, for an import, left in place); a failed copy removes
  nothing.
- VirtualStore check (R8, `VirtualStoreScanner`): for game folders below `Program Files`, `Program Files (x86)`,
  `ProgramData` or the Windows folder the page lists the copies in `%LOCALAPPDATA%\VirtualStore`; copies of files of the
  setup's list and of program files are serious (the game uses them instead of the installation), the others
  (lobby profiles, logs, saved games) information. The registry VirtualStore is shown through the cleanup list.
- Saved games and scenarios (R10, `SavedGames`, forum t=9004 p=44629): "Export..." copies every `.ees`
  (`Data\Saved Games`) and `.scn` (`Data\Scenarios`) file of both games, from the game folder and its VirtualStore copy,
  into a new folder `Empire Earth saves <time>` of the folder the player chooses (not a game folder); on a name conflict
  the VirtualStore copy, which the game uses, wins and the other is named. "Import into Empire Earth..." and "Import
  into The Art of Conquest..." check every chosen file (only `.ees`/`.scn`, a plain name of at most 200 characters with
  characters of the ANSI code page, at most 64 MiB, not a file of the setup's list, no two of the same name), ask
  before a file is replaced, copy the old one into `Backups\<time>_import-saved-games\` and write where the game reads:
  into the VirtualStore folder when Windows denies the game folder. Names outside plain ASCII get the note that every
  multiplayer player needs exactly this name.
- Name check (forum report test case 17, `NameChecks`): lobby profile names and the player folders `Users\<Name>` of
  both games (also in the VirtualStore) with characters outside printable ASCII are listed with the forum's reason
  (t=3563 p=23879, t=2126 p=14281); the page also says that the host needs the ports 33334 to 33336. The names are never
  logged.
- "Backups" on the *Tools* page with "Open backup folder" (Explorer, through the shell; the folder is created when it
  is missing) and the note that the folder contains login data.
- Every writing tool asks the mutation guard ([ADR 0016](docs/adr/0016-mutation-guard-and-effective-game-paths.md)):
  while a setup runs its buttons are disabled, while a game runs the action says "Not possible while ... is running";
  tests for both blocked cases of the cleanup, the WON reset and the import.
- Platform: the drive kind of a path (`IFileSystem.GetDriveKind`: fixed, removable, network, ...), opening a folder in
  the Explorer (`IProcessStarter.OpenFolder`), the protection check of the write policy on its own
  (`RegistryWritePolicy.ProtectionOf`).
- Tests with real files in a temporary folder (`TestSupport/MappedFileSystem`) for the file backup, the WON reset and
  the export and import of a saved game with an umlaut in its name; the UI mapping of the cleanup (`CleanupView`),
  `MaintenanceModel` and the texts with fakes.
- 80 new texts in English, German and French for the maintenance tools (now 293).
- Test plan: cases WP8-01 to WP8-16 (the CD keys unchanged after a cleanup, real HKCU and HKLM leftovers of CD and GOG
  installations, a stale key with backup and restore, keys that are kept, HKLM advice, the WON reset also with access
  denied, the VirtualStore with a standard user, export and import with umlaut names and a standard user, the name
  check, blocked while a setup or a game runs, the backup folder, the three languages, the log); R5, R6, R8, R10 and
  forum test case 17 are no longer open.
- Dropped as ARCHITECTURE 15 allows: the zip export and the zip import of saved games. The folder export is how the
  forum shares saved games, and a zip reader would be new attack surface for files from other players; the core no
  longer may reference `System.IO.Compression`.

- Integrity check (R2, contract 2, `Empire-Earth-Launcher-Core/Integrity`, `IntegrityModel`): the launcher reads the
  manifest `_setupdata_<Product>\files.sha256` and `install.ini` that the community setup writes since v2. The reader
  accepts what the contract says readers must (BOM, LF, CRLF, uppercase hex digits, the binary marker ` *`, empty
  lines) and refuses the whole manifest for one invalid line or an unsafe path (absolute, drive, `:`, `\`, `..`), so it
  never opens a file outside the install root. Files are classed by the table of contract 2.4 (`code`: the
  `CodeFileExtensions` of the setup; `mutable`: `cfg ini conf config log`; `data`: the rest). After every search of
  the installations, so at start and after a setup has ended, the quick check runs in the background: every listed
  file must exist and the `code` files are hashed, each file opened once, no `data` file hashed. The states of
  contract 2.5: OK, Modified (a changed data file, only information), Incomplete, Damaged (the worst finding wins;
  files of `[MissingAfterInstall]` count as missing), Unknown (setup up to 1.7.2, a newer contract, no or an invalid
  manifest, unreadable files, or the uninstall key of the same root without `Empire Earth Community: ContractVersion`:
  an older setup ran afterwards), "not checked" for foreign installations, and "unreliable" when EE and NeoEE share
  the folder (O11). Changed NeoEE program files are worded neutrally, "changed since the installation" (O2). The check
  only reads, logs every finding once with path, class, expected and actual hash, and never blocks Play.
- A running setup wins over the check (contract 4.2, [ADR 0016](docs/adr/0016-mutation-guard-and-effective-game-paths.md)
  plan review): no check starts while `EE_Setup` or `NeoEE_Setup` exists, a check stops (no findings, file closed)
  when one appears, and it runs again when the setup has ended; files are opened with
  `FileShare.ReadWrite | FileShare.Delete`, so a setup can delete and rename a file the launcher is reading.
- *Tools* page (new, between *Settings* and *Launcher*): the integrity state of the selected installation with its
  explanation and every missing or changed file, "Check all files" (the full check of contract 2.5, also the game data,
  with progress and "Cancel check"), "Repair advice" and "Check for updates". The page is laid out from its texts and
  scrolls. The *Play* page shows the state below the file versions, with "Details" (the *Tools* page) or, when the
  state offers the repair, "Repair...": a legacy installation only gets its badge, never the advice, a foreign one
  nothing (contract 2.5).
- Repair hand-off through the update API (R9, contract 4.3, [ADR 0008](docs/adr/0008-https-policy-and-update-api.md);
  `SetupDownloadLocator`, `UpdateUrlPolicy`): the repair advice asks
  `https://api.empireearth.eu/setup/?product=<AppId>` (the AppId as read, the query of the setup) when it opens and
  shows the answer only if the port of the setup's `IsAllowedUpdateUrl` allows it (`https`, no user information, port,
  backslash, space, control or non-ASCII character; `empireearth.eu`, `neoee.net` and subdomains, or
  `github.com/EE-modders/...` without `..` or `%`). Without an AppId, with another status than 200 (also a redirect),
  after a timeout, a TLS error or a network error, or with a refused answer it uses
  `https://empireearth.eu/download`; the window says so below the address and the log names the reason. For damaged or
  incomplete installations the advice names the files (at most ten) and puts the antivirus exception first.
- Version check on request (contract 4.5, `UpdateChecker`): "Check version" on the *Play* page asks
  `&type=game&version=<GameVersion>`, "Check for updates" on the *Tools* page also `&type=setup&version=<SetupVersion>`,
  for every installation with an AppId, also those of setups up to 1.7.2 (AppId and versions of the uninstall key). As
  in the setup, the answer `false` means outdated; then the latest version is asked and shown only if it has at most 32
  characters of `0-9 . - _ space A-Z a-z`, else `?`, and the hand-off of the repair advice opens. Unlike the setup, a
  missing answer is "could not be asked", never "up to date". The game version check is not droppable (ADR 0008 plan
  review); the setup version check, which could have been dropped, is implemented as well.
- HTTPS client (`HttpsClient`, [ADR 0008](docs/adr/0008-https-policy-and-update-api.md)): one `HttpClient` for the
  launcher's lifetime with the certificate check of Windows, no redirects, no cookies, 10 seconds and answers of at
  most 4 KiB; errors are results (timeout, TLS with the inner exception type, network). `Program` sets
  `ServicePointManager.SecurityProtocol` once: exactly TLS 1.2 on Windows 7, the system default elsewhere.
- `docs/contract-samples/` (ADR 0012 plan review): synthetic byte samples of `install.ini` of the three install modes
  (ASCII, CRLF; the user one with `[MissingAfterInstall]`), `files.sha256` (ASCII, LF, sorted ignoring case) and the
  admin install record as a `.reg` file (UTF-16 LE with BOM, CRLF); `.gitattributes` keeps every byte (`-text`). The
  launcher's readers are tested against them, the record against the `RegFileWriter` export of the discovery seed. The
  setup repository is to take the folder over (ARCHITECTURE 14).
- 59 new texts in English, German and French for the integrity check, the *Tools* page, the version check and the
  download of the update API (now 213).
- Test plan: cases WP7-01 to WP7-15 (the quick check at start, a renamed program and data file with the repair window,
  the full check with cancel, a setup started during the full check, the full check after playing for O6, the NeoEE
  updater for O2, legacy and foreign installations, an uninstall key without the contract version, the version check
  on both pages, the download of the update API with and without network, two products in one folder, the three
  languages, the log); the mapping of section 7 has no "offen (L-WP7)" any more.
- Tests: the manifest reader, the file classes against the table of contract 2.4 (read from `docs/CONTRACT.md`), the
  state table of 2.5 with every rule, the counted cost of both checks, a setup mutex that appears while a file is
  hashed, a check that never writes (write-forbidding fakes); the URL policy with the 13 cases of the setup's
  `TestIsAllowedUpdateUrl` under the same names plus ports, capitals, spaces, non-ASCII and escapes; the locator with
  every fallback and the query of the setup; the version check; the HTTPS client's handler and limits (no request);
  the share mode of `LocalFileSystem.OpenRead`; the readers against the contract samples; `IntegrityModel` and
  `UpdateModel`; the texts. Architecture tests: no certificate override in any source
  (`NoCertificateOverrideTests`, the list of ADR 0008), no `Tls13`, `Tls11`, `Tls`, `Ssl3` and no `SecurityProtocol`
  assignment outside `Program` (`TlsSettingTests`), and every 64-hex token of the fixtures and samples is the SHA-256 of
  `sample-<n>` (`FixtureProvenanceTests`, no hash of real game data).

- Play (R3, [ADR 0010](docs/adr/0010-game-start-and-mutex-probing.md), contract 3.6, 3.7, 4.2;
  `Empire-Earth-Launcher-Core/Play/GameStarter.cs`): the *Play* page starts Empire Earth or The Art of Conquest of the
  selected installation (The Art of Conquest only if the installation has an AoC folder; the choice is saved as
  `LastGame` in `settings.json`, an optional member of schema 1). Fixed order: a running setup (`EE_Setup`,
  `NeoEE_Setup`) refuses the start; the same game running refuses it, with the hint how to end a hanging
  `Empire Earth.exe` in the Task Manager when a process of that name exists (forum t=2815, t=5859; the launcher never
  ends a process); the other game running asks "start anyway?"; a missing program refuses it with the repair advice;
  then the "Installed From" values of the game are synchronized from its real folder and the defaults of a first run
  applied (a display question goes to the info bar and never blocks the start); then the program starts through the
  Windows shell (`UseShellExecute = true`) in its real game folder, without arguments and without asking for elevation
  itself, so that every compatibility layer applies, also "Run as administrator" (no error 740). The start is logged
  with installation, game, program and process id (`pid unknown` when the shell gives no process). Start errors are
  messages, not exceptions: a cancelled elevation prompt (1223), denied access (5, 1260), a file an antivirus blocks
  (225, 226, with the repair advice), any other error with its number. When the other game runs and the player starts
  anyway, the game settings are not touched (the mutation guard, ADR 0016) and the game starts.
- File versions of `Empire Earth.exe` and `EE-AOC.exe` on the *Play* page, as Explorer shows them (forum report
  section 8 row 1), through `IFileVersionReader`.
- Repair advice (R9, contract 4.4; `Empire-Earth-Launcher-Core/Repair/RepairAdvice.cs`, `RepairAdviceDialog`): when a
  program is missing, a window explains it and lists the steps: an antivirus exception for the install folder first,
  close the game and run the current community setup, keep the folder and the install mode ("Install for all users",
  "Install for me only" or the portable setup), keep the NeoEE task "Register NeoEE CDKeys"; for a foreign
  installation that the community setup installs its own copy instead of repairing it. "Open download page" opens
  `https://empireearth.eu/download` in the default browser through the shell, not elevated; the update API comes with
  L-WP7. The launcher never downloads or starts the setup.
- A running setup is watched while the launcher runs (contract 4.2, `SetupWatcher`): every two seconds the setup
  mutexes are probed; while one exists, Play, the answers of the info bar and every change on the *Settings* page are
  disabled and both pages say "The … setup is running …"; the installations are not searched (no `install.ini` is read)
  until it has ended, and then searched again. The events "setup started" and "setup finished" are the hooks for the
  integrity check of L-WP7.
- One launcher per Windows session: the launcher holds the mutex `EmpireEarthCommunityLauncher` (reserved for a future
  `AppMutex` of the setup, contract O10); a second start shows "Empire Earth Launcher is already running …" and ends.
- 29 new texts in English, German and French for Play, the repair advice, a running setup and a second launcher (now
  154).
- Platform adapters in the core: `IProcessStarter`/`ShellProcessStarter` (shell execute, only absolute `https` URLs),
  `IFileVersionReader`/`WindowsFileVersionReader`, `IProcessList`/`WindowsProcessList` (only counts processes),
  `IMutexOwner`/`WindowsMutexOwner` (the single-instance mutex; if it cannot be created, the launcher starts anyway).
- Test plan: cases WP6-01 to WP6-15 (both games, file versions, the last game, AoC without a previous Empire Earth
  start, a running setup before and after the launcher start, a running and a hanging game, "Run as administrator"
  with the UAC prompt, a missing program with the repair window and the download page, a damaged NeoEE installation,
  the player list without network, a second launcher, the three languages, the log); the mapping of section 7 has no
  "offen (L-WP6)" any more.
- Tests: the start order with fakes and once with the real game settings, every refusal and start error, the setup
  watcher with a fake clock and manual ticks, the deferred search during a setup, the player list poller (no request
  before Start, one log line per outage and one when the list is back, exceptions of a request, cancel, dispose, a
  request still running at dispose, the context of the events), the repair advice per kind, product and mode, the
  single instance with `FakeMutexProbe`, the Play page model, the start information of the shell starter (also under
  Mono), the file version of a real file. Two architecture tests (category `SourceTree`): `TestIsolationTests` (the
  test program creates no `WindowsRegistry`, uses no `Microsoft.Win32.Registry`, no HTTP client, socket or DNS lookup
  and no file of the launcher's real folder, so it can run on the laptop) and `ProcessRulesTests` (only the shell
  starter calls `Process.Start`, never without the shell or with "runas", nothing ends a process).
- Game settings for the Windows account that runs the launcher (contract 3, R1, R3, R4;
  `Empire-Earth-Launcher-Core/GameSettings`): the table of contract 3.2 with its classes S ("Installed From"), D
  (display) and P (player defaults), the computed values of 3.3 and the defaults marker of 3.5
  (`HKCU\Software\Empire Earth Community\GameDefaults\<Product>`, values `EE` and `AoC`). After every discovery, and
  only for an installation that is the only one using its game settings key (ADR 0015) and while no setup and no game
  runs: missing "Installed From" values are created when both are missing (never changed at the start); without a
  marker the first run creates the missing P and D values and the GPU preference (Windows 10 and later with the task
  `compatibility_windows`, 3.4), asks once whether existing display values that differ should be replaced, and writes
  the marker with either answer; a lower marker creates only the values added since. A marker of a newer contract
  changes nothing. Every value written or deleted is logged with old and new value. `SynchronizeInstalledFrom` keeps
  class S in step with the game that is started (for the Play button of L-WP6).
- *Settings* page with the game settings of the selected installation (no placeholders, ADR 0014): the defaults state
  of each game, the display question, "Apply recommended display", "Reset game settings" with an inline confirmation,
  the hints of the consistency checks with a "Play page" checkbox each, the compatibility options and the result of
  the last action. The *Play* page shows the display question or the first visible hint in an info bar ("Details"
  opens the *Settings* page, "Hide" hides that hint).
- `.reg` backups ([ADR 0007](docs/adr/0007-registry-write-scope-and-reg-backups.md)) in
  `%LOCALAPPDATA%\Empire Earth Launcher\Backups\<yyyy-MM-dd_HHmmss>_<action>\`, one file per game
  (`<time>_<Product>_<EE|AoC>.reg`): the game settings key with its subkeys, a delete line for every value the action
  creates, and the GPU preference and the marker. The files are UTF-16 LE with BOM and CRLF like those of `regedit`
  (`.gitattributes` treats `.reg` as binary); a double-click restores exactly the previous state. A backup that cannot
  be written and read back completely means that nothing is changed. The display settings, the answer "replace" to the
  display question, the reset and the removal of `~ RUNASADMIN` write one; switching a compatibility option does not
  (switching back is its undo).
- Reset of the game settings of an installation: S, D, P and the GPU preference written again (a value of another
  type deleted first, like the setup's `deletevalue`), the markers last; values outside the table (player names, CD
  keys, anything else) stay as they are. Refused for an installation of a newer contract.
- Consistency checks at every start, shown with an offer and never fixed by themselves: `Game Bit Depth` other than
  `Texture Bit Depth`, 16 bit on Windows 8 and later, a `Rasterizer Name` against the wrapper rule of 3.3, a game
  window larger than the screen as the game sees it (physical pixels with an effective `HIGHDPIAWARE` in HKCU or HKLM,
  else the size a DPI-unaware program sees; [ADR 0011](docs/adr/0011-screen-size-in-physical-pixels.md)), a window
  that fits only with `HIGHDPIAWARE`, a screen lower than 768 pixels, a game folder without a drive letter and a game
  folder with characters outside the ANSI code page of Windows, which the game may not open (ADR 0015, forum test
  case 20). A hint hidden from the *Play* page is stored per finding, game settings key and values (or folder) as
  `HiddenHints` in `settings.json` (an optional member of schema 1) and comes back when they change.
- Compatibility options (contract 3.7): from Windows 8 on and outside Wine the entries `DWM8And16BitMitigation`,
  `HIGHDPIAWARE`, `HeapClearAllocation` and `WIN7RTM` as switches for the programs of the installation, written into
  HKCU only with every other entry of the value kept; the HKLM value is shown read-only, no second Windows version
  mode is offered, and switching `HIGHDPIAWARE` off at a scaling above 100 % asks first. On every Windows an HKCU value
  that is exactly `~ RUNASADMIN` can be removed (with a backup). On Windows 7 and under Wine there are no switches;
  old values of earlier setups are shown read-only with the advice to run the current setup, except for an
  installation with the setup's opt-in task `compatibility_legacy`, whose values are its own (contract 3.7,
  revision 2).
- `ISystemInfo` with its Windows implementation `WindowsSystemInfo` in the core (created by `Program`): the Windows
  version from `RtlGetVersion`, Wine, the primary screen in physical pixels (`EnumDisplaySettings`) and as a
  DPI-unaware program sees it (`GetSystemMetrics`), and whether a path fits the ANSI code page; the log names the
  version and the screen at the start
  (`Windows NT 10.0.19045, primary screen 1920x1080 physical, 1536x864 for DPI-unaware programs (125 %)`).
- 51 new texts in English, German and French for the game settings (now 125).
- Test plan: cases WP5-01 to WP5-20 (first run for a second account, the display question, ambiguous installations,
  the reset with its `.reg` files and their import, 100 % and 150 % scaling with and without `HIGHDPIAWARE`, the
  compatibility options on Windows 10/11 and Windows 7, the GPU preference, a running setup or game, hidden hints, the
  three languages), and section 7, which maps the requirements R1 to R10 and R17 and the forum test cases 1 to 22 to
  cases, later work packages, the setup or "dropped".
- Tests: the value table against the table of contract 3.2 and the old values and rows against 3.7 (both read
  `docs/CONTRACT.md`, category `SourceTree`); the computed values with the real folder (`C:\Games\EE`,
  `D:\Empire Earth`, a network path, Turkish culture); the marker, the first run, the display question and class S at
  the start (a second account, ambiguous installations, one value missing, a running setup); the reset and the
  restore of its backup by import; golden files of every value type for the `.reg` writer; the consistency checks
  with 100 % and 150 % and `HIGHDPIAWARE` in HKCU and HKLM; the compatibility options and their write policy; every
  writing action blocked by a running setup and by a running game; the test plan rules (`TestPlanTests`).
- Discovery of every Empire Earth installation (contract 1.4, `Empire-Earth-Launcher-Core/Installations`), in place of
  the old game folder detection. Five sources: the folder chosen on the *Launcher* page, the install records of setups
  since v2 (HKCU, HKLM 64-bit, HKLM 32-bit view; NeoEE before EE), the uninstall keys `{<GUID>}_is1` of community
  setups (only the two exact publishers of the contract, `Inno Setup: App Path` before `InstallLocation`), the
  "Installed From" values (key before hive: `Software\Neo\Empire Earth` in HKCU, HKLM32, HKLM64, then
  `Software\SSSI\Empire Earth`; the old detection let HKCU win over the key, so an old retail entry beat a NeoEE
  installation, forum report section 8 test case 8) and the launcher folder or its parent. Entries of the same
  install folder are one installation (the most specific source wins); `install.ini` (BOM, LF/CRLF, unknown sections
  and keys, names ignoring case) decides between community setups since v2, setups up to 1.7.2 and other
  installations (NeoEE if `neoee.dll` is there). Each installation carries its real Empire Earth and Art of Conquest
  folder ([ADR 0015](docs/adr/0015-game-settings-target-folders-and-write-timing.md)): `C:\Games\EE` stays
  `C:\Games\EE`, `D:\Empire Earth` has the root `D:\`. A missing `Empire Earth.exe` (or `EE-AOC.exe` with the
  component `gameaoc`) means damaged, not "not found"; a chosen folder that does not exist stays chosen. Two products
  in one folder use the `install.ini` written last (O11); a `ContractVersion` above the launcher's is flagged
  (contract 5). A source that cannot be read drops only that candidate, with one log line; the discovery only reads
  and runs in the background.
- *Launcher* page: a list of the installations found (product, install folder, Empire Earth folder, type, state, with
  tooltips); picking one makes it the choice. Below the list a hint when several installations share one set of game
  settings (ADR 0015), when EE and NeoEE share a folder, when a setup is newer than the launcher, and the antivirus
  advice for a damaged installation. "..." now also accepts the install folder or the Art of Conquest folder without a
  question. 21 new texts in English, German and French.
- Effective game paths ([ADR 0016](docs/adr/0016-mutation-guard-and-effective-game-paths.md)): in a game folder below
  `Program Files`, `Program Files (x86)`, `ProgramData` or the Windows folder the launcher reads the copy of a file in
  `%LOCALAPPDATA%\VirtualStore` first, as the game does; the lobby profiles and friends use it.
- Test plan: cases WP4-01 to WP4-18 (installations of setup 1.7.2 and v2 for all users and for one user, a second
  Windows account, retail/GOG or a copy with another folder name, key before hive, damaged installations, the choice of
  root, game folder or AoC folder, VirtualStore, the page in three languages, the log).
- Tests: a table test with a case for every rule of contract 1.4 and every row of 1.5 (it reads the rules from
  `docs/CONTRACT.md`), the readers of every source, 32-bit Windows without duplicates, no write (fakes that fail on a
  write), a discovery with a blocking registry that returns at once, the effective paths. The cases of the old
  `GameDirectoryLocatorTests` live on under their names in `GameDirectoryLocatorPortTests`; no test is skipped under
  Mono any more.
- German user interface: `Properties/Resources.de.resx` translates all 50 texts of the launcher
  ([ADR 0009](docs/adr/0009-localization-with-resx-en-de-fr.md)); the build puts it next to the program as
  `de\Empire Earth Launcher.resources.dll`, like the French `fr\`. The launcher uses it when the Windows display
  language is German (or the language setting chooses it). The German texts address the player formally ("Sie"),
  as the setup does.
- Language setting on the *Launcher* page: Windows language (default), English, Deutsch or Français. It is saved
  as `UiCulture` in `settings.json` (an optional member of schema 1) and applied when the launcher starts, before
  the first window; a hint says so after a change. Only the texts change, number and date formats stay those of
  Windows. An unknown value in the file is logged and means the Windows language. The log names the language
  in use (`UI language: de (launcher setting)`).
- `docs/TRANSLATING.md`: the languages of the launcher, where the texts are, the rules for placeholders, line
  breaks and lengths, how to add a text or a language, how to test a translation, and the review state (German:
  proof-reading in the laptop test; French: review open for all texts but the two navigation texts of the
  original authors).
- Test plan: cases WP3-01 to WP3-17 (removed placeholders, every page in German, English and French, proof-reading
  the German texts, the language setting, the satellite folders, scaling with German texts).
- `ResourceParityTests`: English, German and French have the same string keys (comments and entries with a
  `type` or `mimetype` are not compared), no empty text and the same `{n}` placeholders; images and file
  references exist only in the neutral `Resources.resx`; `Resources.Designer.cs` has a property for every text;
  the launcher project embeds every translation; and the built `de\` and `fr\` satellite assemblies hold exactly
  the texts of their resx files.
- The UI-free core library `Empire-Earth-Launcher-Core` (`Empire_Earth_Launcher_Core.dll`, next to the launcher;
  [ADR 0003](docs/adr/0003-ui-free-core-library.md)). It references only the BCL and the WON library, which an
  architecture test checks, and holds:
  - the fixed names of the shared contract (products, games, keys, files, mutexes, contract version), tested
    against `docs/CONTRACT.md`;
  - platform abstractions with fakes for the tests ([ADR 0006](docs/adr/0006-platform-abstractions-and-windows-path-logic.md)):
    registry access with an explicit HKLM view, a file system, a clock, a mutex probe, and `WinPath`, the Windows
    path rules as string logic (also the check of manifest paths, so that the manifest can never point outside the
    install root);
  - the registry write policy ([ADR 0007](docs/adr/0007-registry-write-scope-and-reg-backups.md)): every key is put
    into a canonical form first (case, `/`, `WOW6432Node`, registry VirtualStore), then `Software\Sierra\CDKeys`,
    the install records and the uninstall keys are refused with their subtrees and ancestors in every hive and
    view, then only listed HKCU keys are allowed; a table test covers every alias and operation;
  - the mutation guard ([ADR 0016](docs/adr/0016-mutation-guard-and-effective-game-paths.md)): no change while a
    setup or a game runs (setup and game mutexes);
  - `settings.json` (see Changed).
- `UiOperation` in the launcher: the one place for asynchronous work of event handlers
  ([ADR 0004](docs/adr/0004-async-await-threading-model.md)); unobserved exceptions of background tasks are logged.
- Test plan: cases WP2-01 to WP2-11 (`settings.json`, the core library in the package, the log).
- The launcher has its own application manifest (`app.manifest`). As before, it runs without elevation
  (`asInvoker`, which also keeps UAC virtualization off) and is not DPI-aware; new is the list of supported
  Windows versions (7 to 11), so Windows 8.1 and later report their real version to the launcher instead of
  Windows 8 ([ADR 0011](docs/adr/0011-screen-size-in-physical-pixels.md)).
- Architecture tests (`Empire-Earth-Launcher.Tests/Architecture/ProjectConventionsTests.cs`) check the settings
  every project shares: .NET Framework 4.8, C# 8.0, deterministic builds, AnyCPU without `Prefer32Bit` for the
  executables, the `lib` folder of each NuGet package, `packages.config`, the 4.8 runtime in `App.config`, no
  elevation in an application manifest, the launcher manifest, the target framework of the built assemblies
  and the reference assemblies of the CI build.
- `docs/TEST-PLAN.de.md`: the German test plan for the manual test on a real Windows computer (prerequisites,
  getting and checking the test package, building on Windows, where the launcher keeps its files, the cases of
  each work package; optional Windows 7 SP1 cases).
- This changelog.

### Changed

- `docs/CONTRACT.md`, revision 4 (identical to the copy in the setup repository; contract version still 1, draft:
  optional additions only, 4.1 and 4.3 unchanged) for the suite installer "Empire Earth Community" of the setup
  repository (its ADR 0013): the suite's names and mutexes and the launcher mutex `EmpireEarthCommunityLauncher` as the
  suite's `AppMutex` (0), the argument `--product=EE|NeoEE` that selects for one session (1.4), the optional suite record
  (1.6), how the suite runs the product setups and its shortcuts (1.7, O10 answered: the launcher lives outside the
  product roots), `EmpireEarthCommunity_Suite` as a setup mutex (4.2), the repair advice with the package folder (4.4).
  The launcher implements these optional additions in 1.0.0 (see "Added" of 1.0.0 above; contract 7 "Additions of revision 4").
  `DiscoveryContractTests` now counts the rows of the table of 1.5 up to the next heading, because 1.6 and 1.7 have
  tables of their own.
- `docs/CONTRACT.md`, revision 3 (identical to the copy in the setup repository; contract version still 1, draft: only
  compatible clarifications by its section 5). It now says what launcher v2 already does, so the code does not change
  (one comment of `InstalledFromReader` names the contract): "Installed From" key before hive, the real EE and AoC
  folders of foreign installations (the AoC folder from the same hive and view or from the user choice, which may be an
  AoC folder), a registry record without `install.ini` also means `community` (1.4); Modified gets no message and no
  repair offer, the state may be shown, as the line "Files: OK, game data changed" of the *Play* page does (2.5); at the
  launcher start class S is only created when both values are missing, and like the first run only for an unambiguous
  installation; class S before every game start while no other game runs; the display question until the player
  answers (3.2, 3.5, 3.6); a request without an answer of HTTP 200 is no statement about the version (4.5). For the
  setup, O11 also names the `<AppId>` folder of setups up to 1.7.2 as a trigger of its shared-folder question.
  ARCHITECTURE 14 lists these points as done and keeps the ASCII-only upper-casing of 3.3 and the shared byte samples
  open; ADR 0015 records it in an amendment.
- The *Tools* page ends with "Network" and "Diagnostics report"; the *Play* page has the link "Why? Check the
  network" in the group of the online players, visible only while the list is "not available". `Program` composes
  `WindowsNetworkInfo`, the status server, `NetworkDiagnostics` and `DiagnosticsModel` with the user, computer and
  domain names of Windows for the privacy rules (kept only in memory).
- `ComputedValues.Rasterizer` takes the wrapper rule from `ComputedValues.DirectXWrapper`; the recommendation is
  unchanged.
- `TestIsolationTests` leaves out the rule samples of `NetworkDestinationTests`, as it leaves out its own.
- ARCHITECTURE.md describes v2 as built (status, module map, 4.6, logging, files, localization, security, testing, open
  points) and ticks the launcher items of the contract checklist; ADR 0006, 0008, 0012, 0013 and 0014 record the
  implementation of L-WP9 in amendments; `docs/TRANSLATING.md` lists 369 texts and the new screens to check; the
  README describes the finished v2 with its privacy rules.

- The launcher's write policy (`LauncherWritePolicy`) allows deleting exactly the eight HKCU keys of the cleanup list
  as trees (before: no tree deletion at all); the protected keys are still refused first, also through every alias.
- The cleanup table of ARCHITECTURE 4.6 is now the code table with ids: its HKLM rows name `Mad Doc Software\EE-AOC`
  instead of the vendor root `Mad Doc Software` (p=4756: only the keys of EE and AoC when other Mad Doc games are
  installed), and the registry VirtualStore copies are offered under the same two conditions as the HKCU keys.
- The *Tools* page continues below "Updates" with the maintenance tools; `Program` composes them (`RegistryCleanup`,
  `WonLoginReset`, `VirtualStoreScanner`, `SavedGames`, `NameChecks` and `MaintenanceModel`, which scans after every
  search and every action).
- README: the integrity paragraph named the manifest `_setupdata_<Product>\files.sha256` with a control character in
  place of `\f`.
- ADR 0007, 0012, 0013, 0014 and 0016 record the implementation of L-WP8 in amendments; `docs/TRANSLATING.md` lists
  293 texts and the new screens to check.

- The repair advice window asks the update API for the download when it opens; "Open download page" waits for the
  answer (at most 10 seconds) and a closed window cancels the request. The download page opens through `UpdateModel`
  (`PlayModel` no longer does).
- `LocalFileSystem.OpenRead` shares the file for reading, writing and deleting (`FileShare.ReadWrite |
  FileShare.Delete`, before without `Delete`), so that a setup can replace a file the integrity check is reading.
- The navigation has a fourth button, *Tools*, between *Settings* and *Launcher*; the *Launcher* button moved down by
  one place. On the *Play* page the game group grew by 20 pixels for the integrity state and the version check, and the
  info bar below it is 20 pixels lower.
- `Program` composes the integrity check (`IntegrityChecker` on the real file system, registry and mutexes, and
  `IntegrityModel`) and the update API (one `HttpsClient`, disposed at the end, `SetupDownloadLocator`,
  `UpdateChecker`, `UpdateModel`); closing the main window cancels a running check.
- ADR 0004, 0008, 0012, 0014 and 0016 record the implementation of L-WP7 in amendments; `docs/TRANSLATING.md` lists
  213 texts and the new screens to check.

- The online player list is polled by `PlayerListPoller` in the core (`async` loop with `Task.Delay`,
  [ADR 0004](docs/adr/0004-async-await-threading-model.md)) instead of a `BackgroundWorker` of the *Play* page. It keeps
  the fixes of the code review: no request before the page has loaded, a failed or throwing request is shown as
  "unavailable" and the polling goes on, an outage is logged once and its end once (same log texts), no result after
  the page is gone, and a failure of the loop itself is logged and shown as "see the log". Its event "available /
  unavailable" is the hook for the outage hint of L-WP9.
- `Program` composes Play: the setup watcher (ticked by the main window every half second), the shell starter, the
  process list, the file version reader and `PlayModel`; `InstallationService` waits for a running setup. The single
  instance is checked before any window or service exists.
- ADR 0004, 0005, 0010, 0012 and 0016 record the implementation of L-WP6 in amendments; `docs/TRANSLATING.md` lists
  154 texts and the new screens to check.

- The registry write policy is narrower ([ADR 0007](docs/adr/0007-registry-write-scope-and-reg-backups.md) plan
  review): every allowed key lists its value names (the values of contract 3.2 per game settings key, `EE` and `AoC`
  for the marker); in `UserGpuPreferences` and `AppCompatFlags\Layers` only the full path of a game program is a
  valid value name; and a compatibility value may only change by the entries the launcher may switch on this
  Windows (no `RUNASADMIN` or Windows version mode added, no other entry removed; the policy reads the current value
  for that). The launcher's list moved from `RegistryWritePolicy.Default` to `LauncherWritePolicy` in the game
  settings, which without Windows 8 or under Wine allows no switch at all.
- `Program` composes the game settings: system information (logged), the write policy for this Windows, the mutation
  guard with the real mutexes, the backup folder and `GameSettingsModel`, which the *Settings* and *Play* pages
  share. The *Settings* page no longer shows only the compatibility warning: it shows the game settings, and the
  warning stands in place of the compatibility options until the player confirms it.
- `.gitignore` no longer hides the folders `Backup` of the core library and of the tests (its pattern `Backup*/` is
  meant for Visual Studio's conversion backups).
- Every test that reads the source tree is in the NUnit category `SourceTree` (before only those of L-WP5), so the
  laptop package can run the others with `--where "cat != SourceTree"`; `RepositoryRoot` fails a test method that
  reads the source tree without the category ([ADR 0012](docs/adr/0012-test-strategy.md)).
- ADR 0007, 0011, 0012, 0015 and 0016 record the implementation details of the game settings in amendments
  ("implementation, L-WP5"); `docs/TRANSLATING.md` lists the 125 texts and how to check the *Settings* page.
- `docs/CONTRACT.md`, revision 2 (identical to the copy in the setup repository; contract version still 1, draft):
  tables of the window size limits (3.3) and of the GPU preference values (3.4); the setup's opt-in task
  `compatibility_legacy` with the flags on Windows 7, without a Windows version layer, and the marker `(opt-in)` in
  the table of 3.7 (the launcher MAY offer these flags on Windows 7 now, and must not call such a value a leftover
  of an earlier setup when `Tasks` contains `compatibility_legacy`); while `EE_Setup` or `NeoEE_Setup` exists the
  launcher must neither read `install.ini` and `files.sha256` nor run an integrity check, and it opens these files
  with at least `FileShare.Read | FileShare.Delete` (4.2, as planned in ADR 0016); a manifest the setup could not
  replace means Unknown, through the missing `Empire Earth Community: ContractVersion` (1.3, 2.1, 2.5). The launcher
  code does not change with this revision; its packages implement the new rules.
- The launcher wraps the Windows registry in the write policy of ADR 0007 (`PolicyCheckedRegistry`); the discovery only
  reads. It starts once the main window is shown, through `UiOperation`; until it has finished the pages show
  "Searching for Empire Earth installations...".
- The lobby profiles come from the Empire Earth folder of the selected installation, through the file system
  abstraction of the core.
- The rows of the *Launcher* page moved up to make room for the list of installations; the theme list has the width
  of the other lists.
- Every text of the launcher's windows comes from `Properties/Resources.resx` and is set in one `ApplyTexts()`
  method per window or page ([ADR 0009](docs/adr/0009-localization-with-resx-en-de-fr.md)); about 20 texts that
  existed only in the designer (navigation, page headings, labels, buttons, the compatibility warning, the
  buttons of the launcher's message dialog) are now resources with a translator comment. The French navigation
  texts moved from `MainForm.fr.resx` (removed) into `Resources.fr.resx`, and French translations of the new
  texts were added. The launcher declares English as its neutral language. `Texts` turns results (player state,
  lobby profiles and friends, origin of the game folder) into texts. An architecture test (`ApplyTextsTests`)
  checks that every designer text with a letter is set again in `ApplyTexts()`, that hand-written code assigns
  no literal text and that no window has a resx of its own per language. Some English texts were adjusted:
  "Game" instead of "Game Settings" above the game choice (the group holds only the choice now), "Profile:"
  instead of "User:", "Player" for the player column, "Empire Earth folder:" without the space before the
  colon and "Custom file..." for the first item of the theme list; the message dialog shows its title also in
  the taskbar instead of "LauncherDialog".
- The logger, the launcher's file locations and the lobby profile reader moved unchanged into the core library
  (namespaces `Empire_Earth_Launcher.Core.Logging`, `.Settings`, `.Lobby`); their tests moved with them.
- The launcher keeps its user settings (game folder, theme, custom theme file) in
  `%LOCALAPPDATA%\Empire Earth Launcher\settings.json` instead of .NET's `user.config`
  ([ADR 0005](docs/adr/0005-own-settings-file-instead-of-user-config.md)): they survive moving or updating the
  launcher; the file is written through `settings.json.tmp`; a damaged file is renamed to
  `settings.json.damaged` and the defaults are used; a file that cannot be read or that a newer launcher wrote
  is never overwritten; members a newer launcher added are kept. Settings of earlier test builds are not taken
  over. The server settings stay in `Empire Earth Launcher.exe.config`.
- All projects (launcher, WON library, mod library, mod creator, tests) target the **.NET Framework 4.8**
  instead of 4.0 ([ADR 0001](docs/adr/0001-target-dotnet-framework-4-8.md)). The launcher and the mod creator
  need the .NET Framework 4.8 at run time; where it is missing, Windows offers to install it. `async`/`await`,
  `Task.Run` and `HttpClient` may now be used.
- Krypton.Toolkit 5.550 is referenced from its .NET Framework 4.8 build (same version and assembly identity),
  NUnit and NUnitLite 3.14 from their .NET Framework 4.5 builds.
- The executables set `Prefer32Bit` to false explicitly (they stay AnyCPU and run as 64-bit processes on 64-bit
  Windows, as before); the mod library now builds deterministically like the other projects.
- CI builds against `Microsoft.NETFramework.ReferenceAssemblies.net48` 1.0.3 instead of the net40 package.
- README: the feature list says what works, what comes with v2 and what is planned but not in v2 (the removed
  placeholders), and that the launcher has no telemetry; localization, settings and tests describe the three
  languages.
- README: requirements per Windows version, Windows build commands for 4.8, the limits of the Mono build; the
  rule "no `async`/`await`" of the 4.0 build is gone.
- Docs after the review panel: ADR 0004, 0007 and 0016 record the review fixes in amendments; the ADR index counts the
  amendments of 0004, 0007, 0009, 0014 and 0016 correctly; ARCHITECTURE describes the cleanup rules (a folder that is
  surely missing, no registry links, no names a `.reg` file cannot hold), proposes the reading of contract 2.5 for
  Modified (the *Tools* page and the report are the diagnostics, the *Play* page shows the state as one informative line),
  names the maintenance tools in the contract checklist of 4.2, and marks R7 as partly done (the comparison with the
  adapter the game uses waits for test case WP9-02); `docs/TRANSLATING.md` lists 371 texts.

### Removed

- The `BackgroundWorker` loop of the *Play* page (replaced by `PlayerListPoller`). The mod creator keeps its own
  build worker.
- The Play button and game choice without function of L-WP3 (ADR 0014): both work now.

- `GameDirectoryLocator` and `GameDirectoryService` of the launcher (replaced by the discovery of the core and
  `InstallationService`) and their tests, which ran on Windows only (the cases are ported).
- The placeholder controls that had no function ([ADR 0014](docs/adr/0014-only-working-features-in-the-ui.md),
  exact list in its amendment): on the *Launcher* page the checkbox "Allow us to collect diagnostic data" (the
  launcher sends no telemetry), the `.eem` file association and the "When starting/closing the game" choices; on
  the *Settings* page the "Magic Button" group (Repair CD-Keys, Reset the Game, Clear Registry) and the
  Compatibility, Windows, DirectX and Advanced Settings groups (compatibility mode, heap, 8/16 bit, DirectX
  wrapper, dgVoodoo, resolution, monitor, game font, dreXmod, NeoEE, Discord presence, HD textures, skip intro);
  on the *Play* page the game language and online ranking groups and "No mods in use"; the *Mods* navigation
  button. The working compatibility warning of the *Settings* page stays. Removed are the fields, the designer
  code and the resources; none of them had a handler. An architecture test (`PlaceholderControlsTests`) keeps
  the names out of the launcher's designer files, code and resources. Features that v2 implements come back as
  working controls with new names in their work packages (reset, compatibility options, repair advice, ...).
- `UserSettingsRecovery` (recovery from a damaged `user.config`): the launcher has no user-scoped .NET settings
  left; the behaviour lives on in `SettingsStore` with its tests.
- Support for Windows XP, Vista, Windows 8.0 and Windows 10 versions 1507 and 1511, which cannot run the
  .NET Framework 4.8 (Windows 8.0 users can update to 8.1 for free). Windows 7 SP1 stays supported but has not
  been tested yet.

### Fixed

- The suite's uninstall key (`Publisher` `Empire Earth Community`, the publisher of EE) was listed as a damaged
  `community-legacy` EE installation of the suite folder. This made the EE game settings ambiguous, so the defaults were
  not written at the start, and gave the update check the suite's AppId (laptop test TP-93).
- Wrapping texts no longer turn into a red X after a setting of Windows changed (bug report of 2026-10-03: the four
  status lines of the *Play* page and the error dialog "Ungültiger Parameter" while Empire Earth ran in full screen). On
  every setting change (`WM_SETTINGCHANGE`, which Windows also sends when a full-screen game changes the display mode)
  Krypton's palettes dispose the fonts they handed out and create new ones with the same values. `KryptonWrapLabel` kept
  the disposed font, because `Control.Font` ignores a font that is equal in value; its paint threw `ArgumentException` in
  `Graphics.DrawString`, and WinForms drew a red X instead of the label until the launcher was restarted. Every wrapping
  text (the *Play*, *Game settings*, *Tools* and *Launcher* pages and the repair advice) is now a `LauncherWrapLabel`,
  which draws with a copy of the palette font that it owns, so that neither the paint nor the page layouts (which
  measured the texts with the label's font) can meet a disposed font. Should drawing fail anyway, the label draws its
  text with the default font of WinForms and logs the first failure (`The text … could not be drawn with its font …`)
  instead of showing a red X. The labels now use the label style the designer gives them (`NormalControl`;
  `KryptonWrapLabel` ignored it and used `NormalPanel`, which looks the same with the launcher's colors). Tests: the new
  category `WinForms` (controls painted into a bitmap, never shown; under Mono with `xvfb-run`): `LauncherWrapLabelTests`,
  `WrapLabelPaintTests` (every page; under Mono only the *Game settings* page, the others need Windows) and
  `WrapLabelRulesTests`; test plan WP6-16 and WP6-17.
- The list "Theme" of the *Launcher* page no longer shows nothing while no theme file is applied, which is the normal
  case because no theme files are shipped yet (bug report of 2026-10-03: an empty "Design" list next to the log line
  `The default theme "Light" is not installed, the built-in colors are used.`). Its first item is now "Built-in colors"
  (en/de/fr, `ThemeBuiltIn`), selected while no theme file is applied; "Custom file..." moved to the second place. Chosen
  while a theme file is applied, the built-in colors are saved as `"ThemeName": "<built-in>"` and used from the next
  start on (`ThemeBuiltInNextStart` says so; the colors of the designer cannot be restored in open windows); the start
  then applies no theme, not even the default theme, unless a custom theme file was chosen later. A theme file that is
  applied but not in the list shows as "Custom file..." instead of nothing. Tests: `ThemeChoiceTests`,
  `SettingsStoreTests`.
- The heading of the player group of the *Play* page no longer ends in a cut-off text (bug report of 2026-10-03:
  "Spieler online (nicht verfügbar)" followed by "Kei"). Why the lobby profiles are missing ("Kein Lobby-Profil
  gefunden", a running setup, no installation, unreadable profiles) and the number of friends of the profile were the
  description of the group's heading, which Krypton draws in the same line to the right of the heading, and the group is
  only 210 pixels wide. They are now a line of their own above "Profile:" (`lobbyStatusLauncherWrapLabel`), as high as the
  text (at most about four lines), and the player list above it gets shorter by as much. Tests: `LobbyStatusTests`
  (the layout under Mono; the page itself on Windows), `LobbyStatusSourceTests`; test plan WP3-02.
- Buttons and check boxes that the page disabled while their action ran stay disabled afterwards (build/UI review):
  `UiOperation.Run` takes the page's state logic as `restore` and applies it after enabling the trigger again, so the
  delete button of the registry cleanup is no longer enabled next to "Nothing to clean up" after the last key was
  deleted, "Check all files" stays disabled when a setup started meanwhile, and the compatibility options and the
  buttons of the *Game settings* page follow their state. `UiOperationRestoreTests` requires the restore for every
  trigger whose `Enabled` the page sets.
- The network check no longer reads the adapter list of Windows and the configuration files of the game folders on the
  UI thread before its first request (build/UI review, ADR 0004): `NetworkDiagnostics.RunAsync` runs completely on the
  thread pool, so slow VPN or virtual adapters and a game folder on a network drive do not freeze the window; the
  *Tools* page cancels a running check when it is closed.
- The maintenance tools no longer read `files.sha256` while a setup runs (coverage review, contract 4.2): the scans
  wait for the search that follows the setup, `ManifestFiles.Read` does not open the manifest while a setup mutex
  exists (the mutation guard is asked; the WON login reset and the import then change nothing), and the import checks
  again after its file dialog whether a setup started meanwhile.
- A scan that fails after a successful action of the *Tools* page no longer hides the result of the action
  (build/UI review): the page shows that the keys were deleted, the WON login was reset or the files were imported,
  and the failed scan is logged.
- A quick click on "Play" while the defaults of the launcher start are still being applied no longer runs the first
  run twice (build/UI review): the writing methods of `GameDefaultsService` run one at a time, so the start waits for
  the defaults and finds their marker (one backup, one "first run" line in the log).
- The hint below "Saved games and scenarios" names the same port table as the network check (coverage review): by
  default 33334 and 33336 TCP and UDP, 33335 TCP only (before: "33334 to 33336, TCP and UDP"), in en/de/fr, README,
  ARCHITECTURE and the test plan.

### Security

- The import of saved games and scenarios imports nothing when the manifest of the setup exists but cannot be read or
  is invalid (security review): before, every file counted as "not a file of the installation", so a file the setup
  installed (for example a scenario in `Data\Scenarios`) could be replaced. Each file is refused with the new reason
  `ImportCheckManifestUnusable` (en/de/fr), as the WON login reset already did.
- `.reg` backups can no longer be used to smuggle lines into the file (security review, D6): a value or key name with a
  line break ended its line, so a key to back up could carry a line such as `[-HKEY_LOCAL_MACHINE\SOFTWARE\Sierra\CDKeys]`
  that a restore by double-click would have run. `RegFileWriter` refuses names with control characters and key names with
  `]`; `RegistryExport` reports such a tree as `InvalidName`, so the backup fails and the cleanup or reset changes nothing.
- The registry cleanup no longer offers a key as stale because Windows hides its folder (security review): `Directory.Exists`
  is also false when access is denied, so a folder now counts as missing only if its parent can be listed without it (or
  the parent is missing in the same sense). Otherwise the key is kept as "whether the folder exists cannot be told"
  (`CleanupKeepFolderUnknownFormat`, en/de/fr).
- The registry cleanup and the reset of the game settings never follow a symbolic registry link (security review, D6):
  `RegistryKey` follows links, so a link below a key to delete (for example to `Software\Sierra\CDKeys`) would have
  been exported into the backup and deleted with the tree. `IRegistry.IsLink` opens each key with
  `REG_OPTION_OPEN_LINK`; `RegistryExport` refuses a tree with a link, so the backup fails and nothing changes, and the
  cleanup reads the tree once more right before each deletion. Test plan: WP8-17 (link, optional) and WP8-18 (a folder
  the launcher may not look at).
