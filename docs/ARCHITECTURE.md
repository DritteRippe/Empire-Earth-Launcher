# Launcher v2 architecture

Target architecture of Empire Earth Launcher v2: what it consists of, how data flows through it, and the
rules every change has to follow. The decisions behind it are recorded as ADRs in [adr/](adr/README.md);
what the launcher may read and change on a computer is specified in [CONTRACT.md](CONTRACT.md), the
contract shared with the Empire Earth Setup.

| | |
|---|---|
| Status | **Built**: describes v2 as built on branch `v2` in the work packages L-WP1 to L-WP9 (section 15, all done; the launcher items of the contract checklist are ticked there); what is still open is in section 14 and in the test plan for real Windows. The README describes the launcher as it is |
| Based on | branch `v2` at `2dc6c43` (refactor/quality-fixes plus the contract), contract version 1 (draft); revised after the design review (ADR amendments of 2026-10-02, ADR 0015, 0016) and after the plan review before L-WP5 (amendments "plan review" of ADR 0007, 0008, 0011, 0012, 0013, 0015, 0016); implementation notes of L-WP2 to L-WP9 in the sections and the ADR amendments |
| Scope | the launcher, its UI-free core library, the WON library, the mod library and mod creator, the tests, the build |

Contents: [1. Goals and constraints](#1-goals-and-constraints) · [2. Module map](#2-module-map) ·
[3. Dependency rules](#3-dependency-rules) · [4. Data flows](#4-data-flows) ·
[5. Threading](#5-threading) · [6. Error handling](#6-error-handling) · [7. Logging](#7-logging) ·
[8. Files and settings](#8-files-and-settings) · [9. Localization](#9-localization) ·
[10. Security and privacy](#10-security-and-privacy) · [11. Testing](#11-testing) ·
[12. Build and CI](#12-build-and-ci) · [13. Requirements map](#13-requirements-map) ·
[14. Open points](#14-open-points) · [15. Work packages](#15-work-packages) ·
[16. Not in v2](#16-not-in-v2)

## 1. Goals and constraints

The launcher is the player's tool for an installed Empire Earth (EE) or NeoEE: start the game, keep its
per-user settings sane, recognize a damaged installation and send the player to the setup for the repair,
and explain the problems the save-ee.com support forum saw again and again (network, antivirus,
VirtualStore, old registry entries, saved games).

Fixed constraints (decided before this document, see the v2 briefing):

- **.NET Framework 4.8**, WinForms with Krypton.Toolkit 5.550, C# `LangVersion` 8.0, no API beyond .NET 4.8
  ([ADR 0001](adr/0001-target-dotnet-framework-4-8.md)). Windows 7 SP1, 8.1, 10 and 11: the setup's range
  except Windows 8.0, which .NET Framework 4.8 does not support; Windows 7 SP1 is supported but untested.
- **UI-free core library** with unit tests, **thin WinForms UI** ([ADR 0003](adr/0003-ui-free-core-library.md)).
- **The contract rules**: the launcher reads the install record, `install.ini`, the manifest and the uninstall
  keys and never writes them; it writes only HKCU; it never asks for elevation; it never repairs, downloads
  or deletes game files; the setup is the only repair tool.
- **Legal and safety**: never call `authtools.dll`, never touch `Software\Sierra\CDKeys` (any hive or view),
  no telemetry, no links to pirated downloads, tests never use the network.
- **Keep** the mod creator, the WON/NeoEE library and every robustness fix of the quality review (damaged
  `user.config` handling, lobby-file parser limits, request deadline, log trimming, no I/O in control
  constructors, ...). Where v2 replaces a component, the fix moves with it and keeps its test.

Quality goals, in this order: **safe** (no data loss, nothing irreversible without a backup, no CD-key
damage), **honest** (no control that does nothing, every finding explained with a next step),
**robust** (a broken registry value, file or server never stops the launcher), **testable** (logic runs
without Windows, registry, network or UI), **localized** (English, German, French).

## 2. Module map

```
Empire-Earth.sln
├─ SharedAssemblyInfo.cs                  one version for all assemblies
├─ Empire-Earth-Launcher-Core/            NEW  Empire_Earth_Launcher_Core.dll - UI-free, no WinForms/Krypton
│  ├─ Contract/        fixed names of the contract: products, keys, folders, publishers, mutexes, file
│  │                   classes, contract version; CompatibilityLayers (entries of 3.7, old values)
│  ├─ Platform/        abstractions + Windows implementations (ADR 0006):
│  │                   IRegistry / WindowsRegistry (hive + view always explicit), IFileSystem /
│  │                   LocalFileSystem (drive kinds since L-WP8), WinPath (Windows path rules as pure string
│  │                   logic), IProcessStarter / ShellProcessStarter (L-WP6; opens a folder in the Explorer
│  │                   since L-WP8), IProcessList, IFileVersionReader, IMutexProbe, IMutexOwner
│  │                   (single instance), ISystemInfo (Windows version, Wine, primary screen in physical and in
│  │                   DPI-unaware pixels, ANSI code page, display adapter since L-WP9),
│  │                   IClock, IHttpsClient (ADR 0008), INetworkInfo / WindowsNetworkInfo (adapters and DNS
│  │                   lookups, L-WP9); RegistryPath (canonical form:
│  │                   WOW6432Node, registry VirtualStore), RegistryWritePolicy and PolicyCheckedRegistry
│  │                   (the IRegistry wrapper every change passes, ADR 0007)
│  ├─ Logging/         ILogger, LogLevel, TraceFileLogger, log trimming (moved from the launcher)
│  ├─ Settings/        LauncherSettings, SettingsStore (settings.json, ADR 0005), LauncherPaths, UiLanguage
│  │                   (the language setting, ADR 0009)
│  ├─ Installations/   install record, install.ini, uninstall keys, "Installed From", launcher folder,
│  │                   user choice -> InstallationDiscovery -> Installation with real EE/AoC folders
│  │                   (contract 1, ADR 0015); EffectivePathResolver (VirtualStore, ADR 0016)
│  ├─ Integrity/       ManifestReader, FileClassifier, IntegrityChecker (quick/full), IntegrityReport
│  │                   (contract 2)
│  ├─ GameSettings/    GameSettingsTable (3.2), ComputedValues and RecommendedValues (3.3), GameDefaultsService
│  │                   (marker, class S at start and before Play, first run, display question, display
│  │                   settings, reset, GPU preference), ConsistencyChecker and HintVisibility (3.6),
│  │                   CompatibilityOptions (3.7), LauncherWritePolicy (the launcher's allow-list, ADR 0007)
│  │                   (contract 3, L-WP5)
│  ├─ Backup/          RegFileWriter (.reg export), RegistryExport, BackupLocations (ADR 0007, L-WP5);
│  │                   FileBackup (move files into a dated backup folder, L-WP8)
│  ├─ Play/            GameStarter, RunningGameDetector, SetupWatcher, ProgramVersions, SingleInstance,
│  │                   MutationGuard (contract 4.2, 3.7, ADR 0010, ADR 0016; L-WP6)
│  ├─ Repair/          UpdateUrlPolicy (port of the setup's IsAllowedUpdateUrl), SetupDownloadLocator,
│  │                   UpdateChecker, RepairAdvice (contract 4; RepairAdvice since L-WP6, the rest L-WP7)
│  ├─ Maintenance/     CleanupCandidates (the list of 4.6), CleanupAdvice, RegistryCleanup, ManifestFiles,
│  │                   WonLoginReset, VirtualStoreScanner, SavedGames (folder export, import), NameChecks
│  │                   (L-WP8)
│  ├─ Diagnostics/     NetworkDiagnostics (adapters, DNS, update API, NeoEE status, hints), OutageHint,
│  │                   AddressClassifier, NeoEeConfigReader, WonLobbyConfigReader (CDKeyCheck, read-only),
│  │                   UpnpInfoParser, ReportAnonymizer (privacy rules), DiagnosticsReport (L-WP9)
│  └─ Lobby/           LobbyProfileRepository (moved), PlayerListPoller (async replacement of the
│                      worker loop of the Play page, L-WP6)
├─ Empire-Earth-WON/                      Empire_Earth_WON.dll - unchanged role: NeoEE status protocol
│                                         (NeoApiClient, DeadlineStream), WON lobby files
├─ Empire-Earth-Mod/
│  ├─ Empire-Earth-Mod-Lib/               Empire_Earth_Mod_Lib.dll - unchanged role: .eem format
│  └─ Empire-Earth-Mod/                   mod creator (WinForms) - retargeted only
├─ Empire Earth Launcher/                 Empire Earth Launcher.exe - thin UI:
│                                         Program (composition root), MainForm, pages, dialogs,
│                                         Texts (core results -> localized strings), UiOperation (async
│                                         event handler helper), InstallationService, GameSettingsModel
│                                         (state of the game settings for two pages), PlayModel (the Play
│                                         page), IntegrityModel (the integrity check of the selected
│                                         installation), UpdateModel (the update API), MaintenanceModel (the
│                                         maintenance tools, L-WP8), CleanupView (what the registry cleanup
│                                         shows), DiagnosticsModel (network check and report, L-WP9),
│                                         ToolsUserControl (the Tools page), RepairAdviceDialog,
│                                         LauncherWrapLabel (wrapping text with a copy of the palette
│                                         font, never a red X), KryptonThemeService, app.manifest
├─ Empire-Earth-Launcher.Tests/           one NUnitLite program: Core/, Launcher/, Won/, Mod/,
│                                         Architecture/ (dependency, project and resource rules),
│                                         Fakes/ (in-memory registry and file system, fake HTTP, network
│                                         information, process, mutex, clock), TestSupport/ (worlds of
│                                         installations, MappedFileSystem for real files in a temporary folder)
└─ Empire-Earth-Launcher.RealMachineTests/ NUnitLite program for the CI end-to-end test of the setup
                                          repository: the core against a real installation on a GitHub-hosted
                                          runner (category RealMachine, explicit and switched off elsewhere),
                                          its self-tests on the fakes of the unit tests (section 11)
```

The core is one assembly with namespaces per area (`Empire_Earth_Launcher.Core.Installations`, ...),
not one assembly per area: the areas share the platform abstractions and the contract names, and one
assembly keeps the classic project files and the CI packaging simple. The WON and mod libraries stay
separate assemblies because the mod creator uses the mod library without the launcher, and the WON
library is the reverse-engineered protocol with its own history.

### UI pages

The navigation keeps the existing look (MainForm, Krypton palette, gold buttons). Pages:

| Page | Content | Requirements |
|---|---|---|
| **Play** | selected installation (product, folder, kind, integrity badge), file versions of `Empire Earth.exe` / `EE-AOC.exe`, choice EE / AoC (AoC only if installed), Play, "setup is running" and "game is running" states (with the hanging-process hint), non-modal warnings that can be hidden per value, lobby profiles and online player list (existing) | R2, R3 |
| **Game settings** (the *Settings* navigation button) | defaults state, consistency warnings, apply recommended display settings, reset (with backup), compatibility options (HKCU only, HKLM read-only; Windows 8 and later only, on Windows 7 only removing `~ RUNASADMIN` and the old values shown, ADR 0007 plan review), screen warning below 768 pixels | R1, R4 |
| **Tools** | integrity details and full check, repair advice, registry cleanup (HKCU keys to select, HKLM keys read-only with advice), WON login reset, VirtualStore check, saved games and scenarios (folder export, import), player names, network diagnostics, "copy diagnostics report", open backup folder | R2, R5 to R10 |
| **Launcher** | installations found and the user's choice, hint when several installations share one game settings key, theme, language (system, English, German, French) | R1, R17 |

The placeholders of the old designer were removed from the UI in L-WP3, as the first step of the localization
work, so that none of them was translated
([ADR 0014](adr/0014-only-working-features-in-the-ui.md), amendment: the exact list of control names, which
an architecture test checks; it includes the "collect diagnostic data" checkbox, which contradicts "no
telemetry"). The README keeps them as planned features. The game choice and the Play button, kept without function
until then, work since L-WP6: below the choice the group shows the file versions of both programs and a state line
(searching, setup running, started); refusals and start errors are message boxes, a missing program opens the repair
advice window. Since L-WP7 the navigation has the *Tools* page between *Settings* and *Launcher* (integrity state with
explanation and files, full check with progress and cancel, repair advice, version check of game and setup; laid out
from its texts and scrolling, so the tools of L-WP8 and L-WP9 can follow), and the Play page shows the integrity state
below the versions ("Details" opens the *Tools* page, "Repair..." the repair advice when the state offers the repair; a
legacy installation only its badge, a foreign one nothing) and "Check version" (ADR 0014 amendment of L-WP7). A Damaged
or Incomplete state is not a window that opens by itself: the check runs in the background after the window is
shown, so its message of contract 2.5 is the state on the Play page with "Repair...", the explanation on the *Tools*
page and the repair window, which names the files and puts the antivirus exception first. Since L-WP5 the *Settings*
page is the
Game settings page (one scrolling panel; the compatibility warning stands in place of the compatibility options until
it is confirmed), and the Play page shows the display question or the first visible hint in an info bar with "Hide"
and "Details". Since L-WP8 the *Tools* page continues below "Updates" with the maintenance tools, one section each:
old registry entries (the offered HKCU keys as check boxes with "Delete selected...", or "nothing to clean up"; below
them the kept and read-only keys with the reason or the advice), WON login, VirtualStore, saved games and scenarios
(export, import into EE or AoC), player names, and backups with "Open backup folder". Their read-only scans run in the
background after every search and every action (`MaintenanceModel`); while a setup runs the writing buttons are
disabled and the sections say why, while a game runs the mutation guard refuses the action with a message (ADR 0014
and 0016 amendments of L-WP8). Since L-WP9 the *Tools* page ends with "Network" ("Check network", the verdict, the
hints and the details in a read-only text box) and "Diagnostics report" ("Copy report", "Save report...", the text
in a read-only text box), and below an unavailable player list the *Play* page shows the link "Why? Check the network",
which opens the *Tools* page at "Network" and starts the check (ADR 0014 amendment of L-WP9).

## 3. Dependency rules

```
Empire Earth Launcher.exe ──> Empire_Earth_Launcher_Core.dll ──> Empire_Earth_WON.dll
          │                                │
          └──> Krypton.Toolkit             └──> BCL only (System, System.Core, System.Net.Http,
                                                System.Runtime.Serialization, System.Xml)
Empire_Earth_Mod.exe ──> Empire_Earth_Mod_Lib.dll ──> BCL only
```

- The core MUST NOT reference `System.Windows.Forms`, `System.Drawing` or Krypton, and MUST NOT show
  anything. An architecture test reads the core assembly's references and fails otherwise.
- The core returns **results, not texts**: enums, codes and the data needed for a message (paths, values,
  hashes). The UI turns them into localized text (`Texts` class). Log messages are English and written in
  the core.
- All access to Windows (registry, files, processes, mutexes, HTTP, display, Windows version) goes through
  the `Platform` interfaces. Logic classes take them through their constructors; only `Program` creates the
  Windows implementations (composition root, no static service locator).
- Every HKLM read names its view; the launcher's own bitness (AnyCPU, `Prefer32Bit` false) never matters.
- Writes are limited to the allow-list of [ADR 0007](adr/0007-registry-write-scope-and-reg-backups.md):
  HKCU game settings values, the defaults marker, the GPU preference and the HKCU compatibility layer
  values, the deletion of the eight HKCU keys of the cleanup list (4.6, since L-WP8), plus the launcher's own folder
  below `%LOCALAPPDATA%`, the folder the player chooses for an export, files the player imports into the game folders
  (or their VirtualStore folders) and the WON login files it moves into a backup. `Program` wraps the Windows registry in
  `PolicyCheckedRegistry` (since L-WP4, the first package that uses the registry), so no code path can write
  around the policy; the protected keys are refused on the canonical form first, then the allow-list is matched
  on the key as it is written (ADR 0007, implementation amendment).

## 4. Data flows

### 4.1 Start-up

1. `Program.Main`: global exception handlers, logger (trimmed `log.txt`), settings (`settings.json`,
   damaged file moved aside), UI culture (setting or Windows), single-instance check
   ([ADR 0010](adr/0010-game-start-and-mutex-probing.md); since L-WP6 before the theme and every other service, so a
   second launcher ends at once after its localized message), theme.
2. MainForm opens at once; the pages show "searching" states. Nothing blocks the window. `MainForm.OnShown` starts the
   discovery through `UiOperation` (`InstallationService.RefreshAsync` -> `InstallationDiscovery.DiscoverAsync`, thread
   pool).
3. **Discovery** (background, contract 1.4): user choice, registry records (NeoEE before EE; HKCU, HKLM64,
   HKLM32), uninstall keys by `Publisher`, "Installed From" values, launcher folder. For "Installed From" the
   **key goes before the hive** (Neo in HKCU, HKLM32, HKLM64, then SSSI in the same order), as for sources 2
   and 3 ("NeoEE before EE; per product ..."); the old locator let the hive win (forum report section 8,
   test case 8). Candidates are merged by normalized root (`WinPath`), classified (`community`,
   `community-legacy`, `foreign`), damaged ones kept; each keeps its real EE and AoC folder (ADR 0015). Every
   dropped candidate is logged with the reason (exactly one line). Refinements of L-WP4, all within the contract:
   - **Kind**: without `install.ini`, a registry record with `ContractVersion` 1 or higher also means `community`
     (the setup deletes `install.ini` at the start of a run that may break off; the integrity check then gives the
     repair advice, contract 2.5). `install.ini` wins over the record when both exist (contract 1.1).
   - **AoC folder of foreign installations**: the AoC "Installed From" values of the same product in the same hive
     and view, only if `EE-AOC.exe` is there (ADR 0015 amendment).
   - **User choice**: it selects the installation whose root, EE folder or AoC folder it is, else the one whose root
     a recognized folder (EE folder, AoC folder, install root) implies; a folder that is missing or holds no program
     stays an installation of its own, so the player sees exactly that folder. Picking an installation in the list
     saves its EE folder.
   - **Order of the list**: by the most specific source of each installation (the chosen one first), then by the
     order in which they were found; without a choice the first one is used. On 32-bit Windows a value read through
     both HKLM views is one candidate.
   - **Lobby profiles** come from the EE folder of the selected installation through the `EffectivePathResolver`
     (ADR 0016 amendment).
4. The selected installation (user choice, else first found) goes to the pages.
5. **Quick check** (background, contract 2.5): manifest and `install.ini` read; existence of every listed
   file and the hashes of the `code` files. Result: OK, Modified, Incomplete, Damaged or Unknown. Not started while
   a setup mutex exists, cancelled when one appears ("check cancelled", no findings), files opened with
   `FileShare.ReadWrite | FileShare.Delete` (ADR 0016 plan review).
   Implemented in L-WP7 (`IntegrityModel`, `IntegrityChecker`): the quick check starts whenever a search has a new
   result, so at start and after every setup, and nothing waits for it; a new check cancels the running one and only
   the latest result counts. Neither a search nor a check runs while a setup runs (the search waits, contract 4.2).
6. **Defaults first run** per game (contract 3.6), if the marker is missing, **only if the installation is
   unambiguous** for that game settings key (user choice, or the only installation found that uses the key;
   [ADR 0015](adr/0015-game-settings-target-folders-and-write-timing.md)) and the mutation guard allows it
   ([ADR 0016](adr/0016-mutation-guard-and-effective-game-paths.md)): P and GPU preference created if missing,
   D created if missing; differing D values -> one non-blocking question (info bar) "apply the recommended
   display settings?" (Yes: `.reg` backup, then overwrite); marker written. **Class S is only created at
   start**: both values of a game missing -> written for an unambiguous installation, existing values are never
   changed at start (ADR 0015 plan review); otherwise the first run waits for the first Play of that game. No defaults and no reset for an
   installation whose `ContractVersion` is higher than the launcher knows (contract 5).
   Implemented in L-WP5 (`GameDefaultsService.ApplyAtLauncherStart`, run by `GameSettingsModel` after **every**
   discovery, so a folder the player chooses gets its defaults at once): the question collects the differing D
   values of every game of every unambiguous installation; it is shown in the info bar of the Play page and on the
   Settings page, and the markers of these games are written with the answer (if the backup of "replace" fails,
   no marker is written and the question comes again). A marker lower than the contract version creates only the
   values added since; for an ambiguous installation the Settings page says why nothing was set up and offers the
   reset, which sets everything up for the selected one.
7. **Consistency checks** (contract 3.6) -> listed on the Game settings page with the offer to reset; on the
   Play page a non-modal info bar that can be hidden per value and content (ADR 0015). Implemented in L-WP5
   (`ConsistencyChecker`, read-only): bit depths, 16 bit on Windows 8 and later, rasterizer against the wrapper rule,
   window against the screen as the game sees it (ADR 0011), screen below 768 pixels, game folder without drive letter
   or outside the ANSI code page. Hidden hints are `HiddenHints` entries of `settings.json` (finding and game
   settings key, plus the values or the folder); the checkbox "Play page" on the Settings page hides or shows them.
8. Online player list polling starts (existing behaviour, now `PlayerListPoller`, L-WP6: started when the Play page
   loads, ended with it).
9. The setup-mutex watcher starts (every 2 s while the launcher runs). Since L-WP6 the main window ticks it every 500 ms
   and it probes when two seconds have passed by its `IClock`; the first probe is part of the first search
   (`InstallationService.RefreshAsync`), which waits while a setup runs (4.3).

### 4.2 Play

Click Play -> button disabled -> `GameStarter.StartAsync(installation, game)`:

1. Setup mutex `EE_Setup` or `NeoEE_Setup` exists -> refused, "a setup is running".
2. Game mutex of that game exists -> refused, "already running" (forum table 8 #14), with the program name and,
   if a process of that name exists, the hint that it may hang and how to end it in the Task Manager (the
   launcher never kills a process). The other game running -> warning with "start anyway".
3. Program file missing -> refused, "damaged" with the repair advice (`RepairAdvice`, from L-WP6 on with the fixed
   download page; the update API comes with L-WP7).
4. Class S values synchronized for the game started, computed from the **real game folder** (ADR 0015);
   written only if different after normalization; changed values are logged with old and new value. Then the
   first run of the defaults if the marker is missing.
5. `ShellExecute` of the program with the game folder as working folder (contract 3.7: compatibility
   layers and a chosen elevation apply).
6. Logged: installation, game, program, process id (`pid unknown` if `Process.Start` returns none). Findings
   of the quick check never block (contract 2.5).

Implemented in L-WP6 (`GameStarter`, `PlayModel`, [ADR 0010](adr/0010-game-start-and-mutex-probing.md) amendment):
every refusal and start error is a `StartResult` (setup running, same game running with `ProcessFound`, other game
running, chosen folder missing, `Damaged` with `RepairAdvice` also for Windows errors 2 and 3, blocked by an antivirus
225/226 with `RepairAdvice`, elevation cancelled 1223, access denied 5/1260, any other error with its number). Step 4 asks
the mutation guard: when the player starts while the other game runs, class S and the first run are blocked and logged,
and the game starts anyway. The first run of step 4 may produce the display question; it joins the info bar
(`GameSettingsModel.AddQuestion`) and never blocks the start. The Art of Conquest can only be chosen when the
installation has an AoC folder; the choice is `LastGame` in `settings.json`.

### 4.3 Setup finished

The watcher sees the setup mutex appear -> a running integrity check is cancelled, Play and every guarded change
are blocked. It sees the mutex disappear -> discovery and quick check run again -> pages update. A game start is
refused while the mutex exists.

Implemented in L-WP6 (`SetupWatcher`): the events "setup started" and "setup finished" are the hooks; the pages
disable Play and every guarded change and name the setup; `InstallationService` searches again on "finished", and a
refresh while a setup runs only waits and keeps the previous result, so `install.ini` is not read then (contract 4.2).
A refresh that itself sees the end goes on alone. Implemented in L-WP7: `IntegrityModel` cancels a running check on
"setup started" (the checker also probes the mutexes before every file and after every MiB; the result is "check
cancelled" without findings, the file closed) and runs the quick check after the search that follows "setup finished"
(ADR 0016 amendment of L-WP7).

### 4.4 Reset game settings

Mutation guard (no setup, no game of that installation running) -> confirmation -> `.reg` backup of the game
settings key with subkeys into `%LOCALAPPDATA%\Empire Earth Launcher\Backups\` (date, time, product, game in
the name), with a delete line for every value the reset will create, so that importing the file restores the
previous values exactly (ADR 0007 amendment) -> if the backup fails nothing is changed -> S, D, P and GPU
preference overwritten (a value of another type is deleted first) -> marker written -> result with the backup
path. Values outside the contract table are never touched. Refused with "update the launcher" when the
installation's `ContractVersion` is higher than the launcher knows (contract 5).

Implemented in L-WP5 (`GameDefaultsService.Reset`): the confirmation is inline on the Settings page (it names the
backup folder); the backup folder is `<yyyy-MM-dd_HHmmss>_reset-game-settings`, one file per game
`<yyyy-MM-dd_HHmmss>_<Product>_<EE|AoC>.reg` with the game settings key and its subkeys, the delete lines, and the
GPU preference and marker values (as they are, or as delete lines); every file is read back before the first
change; the markers are written after all other values. "Apply recommended display" works the same way with the
D values only (`..._display-settings`). The result names the folder, or says that nothing changed because the backup
failed, or that the change stopped halfway (then the backup restores it).

### 4.5 Repair hand-off

Damaged/Incomplete/Unknown (`community`) or the user asks -> `SetupDownloadLocator`: with an AppId
`GET https://api.empireearth.eu/setup/?product=<AppId>` (HTTPS, certificate validation, timeout, no
redirects) -> trimmed body accepted only if `UpdateUrlPolicy` allows it -> otherwise
`https://empireearth.eu/download`. The advice dialog shows the steps of contract 4.4 (close the game, same
folder, same mode, keep "Register NeoEE CDKeys", antivirus exception first, foreign installations are not
repaired) and opens the URL in the default browser, not elevated. The launcher never downloads or starts
the setup. `RepairAdvice` (the texts of contract 4.4 and the fixed page) exists from L-WP6 on; L-WP7 adds the
API request. Implemented in L-WP6: the steps are codes (`RepairStep`) that `Texts` turns into sentences with the folder
and the install mode in the words of the setup ("Install for all users", "Install for me only", the portable setup,
the task "Register NeoEE CDKeys"); the folder of a foreign installation is its EE folder, never a whole drive; the
window (`RepairAdviceDialog`) stays open after "Open download page" and shows the address to copy if the browser
cannot be opened. The game version check (`&type=game&version=`, contract 4.5) for installations with an AppId runs on
request and is not optional; the setup version check is (ADR 0008 plan review). TLS: `Program` sets
`SecurityProtocol` once, `Tls12` on Windows 7 only. Implemented in L-WP7 (ADR 0008 amendment of L-WP7): the repair
window asks the update API when it opens (`UpdateModel.LocateAsync`; "Open download page" waits for the answer, at most
10 s, and closing the window cancels it) and names the reason below the address when the fixed page is used; for
Damaged and Incomplete it shows the files (at most ten, the damaged ones first) above the steps. "Check version" on the
Play page asks the game version, "Check for updates" on the *Tools* page also the setup version (both implemented); an
outdated version opens the repair window with the hand-off. A missing answer is "could not be asked", never "up to
date" (the setup's `CheckUpdate` reads it as "no update").

### 4.6 Tools

- **Registry cleanup** (R5): scan of an explicit list of keys of old and foreign installations; each key
  shown with why it looks stale or why it is kept; the user selects; mutation guard; `.reg` backup, then delete. HKLM keys
  are only listed (read-only) with advice; protected keys can never be selected, also not through an alias
  (`WOW6432Node`, registry VirtualStore; ADR 0007 amendment). The list is the code table
  `Maintenance.CleanupCandidates` (L-WP8); `CleanupCandidatesTests` compares it with this table row by row (id, key, scope,
  evidence), and every entry needs evidence (`t=`, `p=` or `setup:`); unproven entries are not added but noted in the test
  plan:

  | Id | Key | Scope | Offered or advised when | Otherwise | Evidence |
  |---|---|---|---|---|---|
  | `hkcu-ee-ee` | `HKCU\Software\SSSI\Empire Earth` | launcher deletes | no installation of EE found, and the folder of its "Installed From" values is missing on a present, fixed, local drive | not shown | t=1036 p=4756 (SSSI keys), t=12082 p=49553, t=10577 p=46301 |
  | `hkcu-ee-aoc` | `HKCU\Software\Mad Doc Software\EE-AOC` | launcher deletes | as above (its own AoC folder) | not shown | t=1036 p=4756 (Mad Doc keys, only those of EE and AoC) |
  | `hkcu-neoee-ee` | `HKCU\Software\Neo\Empire Earth` | launcher deletes | as above, no installation of NeoEE found | not shown | setup: config_neoee.iss (game settings keys of NeoEE, contract 3.1); t=10577 p=46302 |
  | `hkcu-neoee-aoc` | `HKCU\Software\Neo\Art of Conquest` | launcher deletes | as above, no installation of NeoEE found | not shown | setup: config_neoee.iss (game settings keys of NeoEE, contract 3.1); t=10577 p=46302 |
  | `vs-sssi-ee` | `HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE\SSSI\Empire Earth` | launcher deletes | as `hkcu-ee-ee` | shown read-only | t=12082 p=49553, t=1036 p=4756 (the HKLM keys of SSSI and Mad Doc; Windows keeps the HKLM writes of a non-elevated 32-bit game in HKCU) |
  | `vs-maddoc-aoc` | `HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE\Mad Doc Software\EE-AOC` | launcher deletes | as `hkcu-ee-aoc` | shown read-only | t=12082 p=49553, t=1036 p=4756 (the HKLM keys of SSSI and Mad Doc; Windows keeps the HKLM writes of a non-elevated 32-bit game in HKCU) |
  | `vs-sssi-ee-wow64` | `HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE\WOW6432Node\SSSI\Empire Earth` | launcher deletes | as `hkcu-ee-ee` | shown read-only | t=12082 p=49553, t=1036 p=4756 (the HKLM keys of SSSI and Mad Doc; Windows keeps the HKLM writes of a non-elevated 32-bit game in HKCU) |
  | `vs-maddoc-aoc-wow64` | `HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE\WOW6432Node\Mad Doc Software\EE-AOC` | launcher deletes | as `hkcu-ee-aoc` | shown read-only | t=12082 p=49553, t=1036 p=4756 (the HKLM keys of SSSI and Mad Doc; Windows keeps the HKLM writes of a non-elevated 32-bit game in HKCU) |
  | `hklm32-sssi-ee` | `HKLM32\Software\SSSI\Empire Earth` | advice only | stale as `hkcu-ee-ee`: advice to export the key and then delete it with the Registry Editor as administrator | shown read-only ("keep" with the reason) | t=1036 p=4756 (SSSI keys), t=12082 p=49553, t=10577 p=46301 |
  | `hklm32-maddoc-aoc` | `HKLM32\Software\Mad Doc Software\EE-AOC` | advice only | stale as `hkcu-ee-aoc`: the same advice | shown read-only | t=1036 p=4756 (Mad Doc keys, only those of EE and AoC) |
  | `hklm64-sssi-ee` | `HKLM64\Software\SSSI\Empire Earth` | advice only | as `hklm32-sssi-ee` | shown read-only; on 32-bit Windows listed once, as HKLM32 | t=1036 p=4756 (SSSI keys), t=12082 p=49553, t=10577 p=46301 |
  | `hklm64-maddoc-aoc` | `HKLM64\Software\Mad Doc Software\EE-AOC` | advice only | as `hklm32-maddoc-aoc` | shown read-only; on 32-bit Windows listed once | t=1036 p=4756 (Mad Doc keys, only those of EE and AoC) |
  | `hklm32-sierra` | `HKLM32\Software\Sierra` | protected | never | shown read-only as "do not delete: contains the NeoEE CD keys", `CDKeys` only as "exists" or "missing" | t=12082 p=49553, t=10950 (CD key invalid), forum report table 8 row 7; setup: authtools.dll writes Software\Sierra\CDKeys |
  | `hklm64-sierra` | `HKLM64\Software\Sierra` | protected | never | as `hklm32-sierra`; on 32-bit Windows listed once | t=12082 p=49553, t=10950 (CD key invalid), forum report table 8 row 7; setup: authtools.dll writes Software\Sierra\CDKeys |
  | `hkcu-sierra` | `HKCU\Software\Sierra` | protected | never | as `hklm32-sierra` (the CD keys of a `user` or `portable` installation, contract 3.8) | t=12082 p=49553, t=10950 (CD key invalid), forum report table 8 row 7; setup: authtools.dll writes Software\Sierra\CDKeys |
  | `vs-sierra` | `HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE\Sierra` | protected | never | as `hklm32-sierra` | t=12082 p=49553, t=10950 (CD key invalid), forum report table 8 row 7; setup: authtools.dll writes Software\Sierra\CDKeys |
  | `vs-sierra-wow64` | `HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE\WOW6432Node\Sierra` | protected | never | as `hklm32-sierra` | t=12082 p=49553, t=10950 (CD key invalid), forum report table 8 row 7; setup: authtools.dll writes Software\Sierra\CDKeys |

  Scopes: *launcher deletes* (HKCU; offered with a check box only when stale, i.e. no installation of its product was found
  and the folder named by its own `Installed From Volume`/`Installed From Directory` is missing on a present, fixed, local
  drive; "missing" means that its parent folder can be listed without it, or the parent is missing in the same sense,
  because Windows also reports a folder the player may not look at as missing (security review); a key that names no
  folder, a folder on a missing, removable or network drive, a folder whose existence cannot be told, or an unreadable
  key is kept),
  *advice only* (HKLM: the launcher never writes HKLM and never asks for elevation, contract 4.1; when stale under the same
  conditions the advice is to export the key and delete it with the Registry Editor as administrator; only the SSSI and Mad
  Doc keys get this advice), *protected* (`Software\Sierra`: never named for deletion in any state; ADR 0007 plan review,
  tested through the canonical form of the policy). Not on the list, with the reason: the vendor roots
  (`Software\Mad Doc Software`, `Software\SSSI`; p=4756 "make sure to only get ones for ee and aoc if you have other Mad
  Doc games"), subkeys of `Software\Sierra` other than `CDKeys` (no sample names them yet), Stainless Steel Studios keys
  (p=4756 names only the vendor, no path), InstallShield leftovers (no sample); test case WP8-02 collects samples. The
  four game settings keys of contract 3.1 (`hkcu-ee-*`, `hkcu-neoee-*`) are the player's settings while an installation
  uses them, so they are not shown then.

  An empty HKCU part on a computer is a valid result: the page then says "nothing to clean up" and shows the
  read-only list, without an enabled delete button; README and CHANGELOG call R5 "cleanup of HKCU entries; HKLM
  entries are only shown, with advice" (ADR 0007 plan review).

  Implementation (L-WP8): `RegistryCleanup.Scan` gives each entry a state (missing, protected, installation found,
  folder exists, drive not fixed, no folder named, folder unknown, unreadable, stale) and `CleanupAdvice` turns it into a code with
  parameters; only the two delete codes name a deletion target, and `CleanupAdvice` refuses one that the write policy
  protects. On 32-bit Windows the HKLM64 twin of an HKLM32 entry is the same key and is listed once. `Delete` takes only
  offered items of the scan, then: mutation guard ("delete stale registry keys") -> the state of every selected key again
  (one that is no longer stale stops everything) -> one `.reg` file with all of them
  (`Backups\<time>_registry-cleanup\<time>_registry-cleanup.reg`) -> `DeleteSubKeyTree` in the order of the list. If
  the backup fails, nothing is deleted. The export refuses a tree with a symbolic registry link (`IRegistry.IsLink`, opened
  with `REG_OPTION_OPEN_LINK`; `RegistryKey` would follow it, also to `Software\Sierra\CDKeys`) or with a name a `.reg` file
  cannot hold (a control character, `]` in a key name), so such a backup fails; right before each deletion the tree is read
  once more, and a link that appeared meanwhile stops the deletion (security review, ADR 0007). The write policy allows `DeleteSubKeyTree` exactly for the eight HKCU keys of
  the list (`LauncherWritePolicy` takes them from `CleanupCandidates.WriteRules`), after the protected keys were refused
  on the canonical form. Whether each `CDKeys` key exists is logged before and after the cleanup, never a value.
- **WON login reset** (R6): `_wonkver.pub` and `_wonlogin.ks` of the EE and AoC folders and of their
  VirtualStore copies (effective paths, ADR 0016) are moved into a dated backup folder (forum p=83519); files
  listed in the manifest are never touched; mutation guard. The UI says that the backup folder contains login
  data; if the online login still fails afterwards, the hint points to the repair with the setup. Implementation
  (L-WP8): `WonLoginReset` asks the guard first ("reset the WON login"); when the manifest exists but cannot be read it
  moves nothing (it could not tell the setup's files). `FileBackup.MoveIntoBackup` copies every file into
  `Backups\<time>_won-login-reset\` (`EE\`, `EE-VirtualStore\`, `AoC\`, `AoC-VirtualStore\`), reads each copy back,
  writes `moved-files.txt` with the original paths, and only then removes the originals; a file Windows does not let it
  remove stays and is named with "access denied" (outcome `Partial`). The contents are never logged.
- **VirtualStore** (R8): for installations below `Program Files`, `Program Files (x86)`, `ProgramData` or
  the Windows folder, `%LOCALAPPDATA%\VirtualStore\<path without drive>` is listed; manifest files shadowed
  there are reported as serious (the game uses the virtual copy), runtime files as information. Lobby
  profiles, saves and the WON reset use the effective file (`EffectivePathResolver`, VirtualStore copy first),
  as the game does (ADR 0016). Implementation (L-WP8): `VirtualStoreScanner` lists only the VirtualStore copies of the
  EE and AoC folders (at most 2000 files, 16 levels); serious are files of the manifest and program files (`code`
  class), everything else is information. The registry VirtualStore (`HKCU\Software\Classes\VirtualStore\MACHINE\...`)
  is shown through the `vs-*` rows of the cleanup list.
- **Saved games and scenarios** (R10): export of `.ees` (saves, `Data\Saved Games`) and `.scn` (scenarios,
  `Data\Scenarios`) files of EE and AoC into a new folder `Empire Earth saves <yyyy-MM-dd_HHmmss>` (subfolders
  `EE\Saved Games`, `EE\Scenarios`, `AoC\...`) of a folder the player chooses, from both the game folder and its
  VirtualStore copy (the VirtualStore copy wins a name conflict, the other is listed); a target inside a game folder,
  its VirtualStore copy or the install root is refused. The export only reads the game folders, so it is not guarded.
  Import of single files into EE or AoC with checks (`.ees`/`.scn` only, plain file names of at most 200 characters,
  only characters of the ANSI code page of Windows because the game is an ANSI program, at most 64 MiB, never onto a
  file of the manifest, no two files of the same name, no overwrite without confirmation) into the folder the game
  reads (ADR 0016), behind the mutation guard ("import saved games"); the files it replaces are copied into
  `Backups\<time>_import-saved-games\` first (`FileBackup.CopyIntoBackup`; a file whose old version could not be
  backed up is not written), and
  when Windows denies writing into a game folder below `Program Files` the file goes to its VirtualStore folder, where
  the game reads it. The zip export and the zip import were dropped in L-WP8 (CHANGELOG): the folder export is the way
  the forum shares saves (t=9004 p=44629), and a zip reader would be new attack surface for files from other players.
  `NameChecks` warns for player names (`Users\<Name>` of both games and their VirtualStore copies) and lobby profile
  names with characters outside printable ASCII (t=3563 p=23879, t=2126 p=14281) and the page gives the hint that the
  host needs its ports forwarded (33334 and 33336 TCP+UDP, 33335 TCP by default, forum 4.9; the same table as the
  network check); the names are shown, never logged (only their number).
- **Network diagnostics** (R7, on request only): local adapters with IPv4 and gateway, virtual/VPN
  adapters flagged (forum 4.10), DNS resolution and status request of the NeoEE server, evaluation of
  `upnp_info.txt` when it exists (tolerant parser, "unknown format" otherwise), hints for private/CGNAT
  external addresses (100.64.0.0/10) and IPv6-only (DS-Lite) connections. **`NeoEE.cfg`** of both game
  folders (RIP hosting configuration installed by the NeoEE setup; class `mutable`, read only): `Active`,
  `Server`, `DefaultPort`, `MemberPorts`, `PortCheck`, `TryUPnP` are shown; the port forwarding table is
  derived from `DefaultPort` (33334 and 33336 TCP+UDP, 33335 TCP by default, forum t=11057 p=48100) and DNS is
  resolved for its `Server`; RIP hosting helps in most cases without port forwarding (t=5843 p=39204). No
  connection attempt to the auth or firewall ports (10002, 10003), only DNS. **Outage hint** (forum report
  table 8 row 9): DNS works and the update API answers, but the status server does not -> "probably a server
  outage, not your computer". **`CDKeyCheck`** in `WONLobby.cfg` is shown read-only (NeoEE expects `true`,
  t=10950); the launcher never offers to change it. No external "what is my IP" service. Where the game
  stores its chosen network adapter (t=32479) is unknown; finding it is a test-plan task (WP9-02).

  Implementation (L-WP9): `NetworkDiagnostics.RunAsync` runs only when the player clicks "Check network" or the link of
  the *Play* page (`DiagnosticsModel`, one check at a time). It lists the adapters through `INetworkInfo` (loopback
  skipped; type, description, state, IPv4 with prefix and gateway, IPv6 only as none, link-local only or global;
  virtual or VPN by the tunnel and PPP types or by words of the description such as `Hamachi`, `TAP-Windows`,
  `WireGuard`, `VirtualBox`, `Hyper-V`), reads `NeoEE.cfg`, `WONLobby.cfg` and `upnp_info.txt` of both game folders
  through the effective paths (the VirtualStore copy first, ADR 0016; at most 64 KiB, Latin-1, only read) and then, at
  the same time, resolves the host of the status server and the `Server` of every `NeoEE.cfg` (5 seconds each), asks
  the update API with the query of contract 4.3 (the AppId of the selected installation, else of the first one with
  an AppId; without one it is not asked) and asks the status server for the player list, as the *Play* page does. The
  verdict (`OutageHint.Evaluate`) takes three inputs: whether the status host resolves, whether the update API answered
  (or was not asked) and whether the status server answered (or is not configured); "probably a server outage, not
  your computer" needs a resolving name and an answering update API while the status server stays silent, every
  combination has its verdict and test. The port table comes from `DefaultPort` of `NeoEE.cfg` (TCP+UDP) and
  `EEFileTransferPort` (TCP) and `LobbyPort` (TCP+UDP) of `WONLobby.cfg`, else the forum's 33334, 33335 and 33336; its
  target is the private IPv4 of the only real adapter with a gateway, else "the IPv4 address of this computer". The
  hints: no IPv4 gateway (offline, or IPv6 only), connected virtual or VPN adapters, several real adapters with a
  gateway, from `upnp_info.txt` an external address that is CGNAT (100.64.0.0/10), 0.0.0.0 or private (double NAT) -
  CGNAT or 0.0.0.0 together with a global IPv6 address is reported as DS-Lite -, RIP hosting off (`Active: false`) and,
  for NeoEE only, `CDKeyCheck` not `true`. `UpnpInfoParser` is tolerant: it recognizes labelled lines (external, WAN or
  public address; local, LAN or internal address; at most ten port lines) and gives "unknown format" for anything else;
  it keeps the external address only as its class. Nothing in the game folders is written, nothing contacts the ports
  10002 and 10003 of NeoEE (`NetworkDestinationTests`).
- **Diagnostics report**: one text with launcher version, Windows version, installations, file versions of
  the game programs, integrity state and findings, defaults and consistency state, VirtualStore, network
  results; copied to the clipboard or saved, never sent anywhere. It never contains CD-key values or the
  contents of the backup folder, and replaces the user's profile paths with `%USERPROFILE%` and
  `%LOCALAPPDATA%` (it is usually posted in forums). Further privacy rules (ADR 0013 plan review): the external
  IPv4 only as its class (private, CGNAT, public), no MAC addresses, adapter GUIDs, user-chosen adapter names or
  computer name, lobby and player names only as "profile n: characters outside ASCII", the user name replaced in
  every path; golden-file and negative tests.

  Implementation (L-WP9): `DiagnosticsReport.Build` writes English text with CRLF from a `DiagnosticsInput` that
  `DiagnosticsModel.Collect` fills with the latest results of every page (no new scan): launcher and Windows version,
  screen, display adapter (`ISystemInfo.PrimaryDisplayAdapter`), UI language, every installation with its folders,
  kind, mode, contract, setup and game version, AppId and sources; for the selected one the program versions, whether a
  DirectX wrapper is installed per game (`ComputedValues.DirectXWrapper`, the wrapper rule of contract 3.3 without the
  Wine rule, with its source), the integrity state with at most 20 findings, the defaults state
  per game, the consistency findings, the VirtualStore files, the name check, whether each of the five `CDKeys` keys
  exists (never a value) and the number of cleanup keys; then the latest network check or "not checked".
  `ReportAnonymizer` applies the rules to every path and address: `%LOCALAPPDATA%` and `%USERPROFILE%` for the
  profile, `<user>` for every path segment equal to the user name or the profile folder name (also `D:\Users\<name>`
  and VirtualStore paths), `<computer>` for the own computer (also as FQDN) in UNC paths and segments, `<server>` for
  other UNC hosts, `<domain>` for the domain; public, CGNAT and special IPv4 addresses and every IPv6 address only as
  their class. The report is shown in the text box after "Copy report" (clipboard) or "Save report..." (UTF-8 with
  BOM, the file the player chooses, `Documents` suggested; refused inside an installation); the log records only that it
  was copied (with the number of lines) or the anonymized path it was saved to.

## 5. Threading

Decided in [ADR 0004](adr/0004-async-await-threading-model.md):

- One UI thread (STA, `WindowsFormsSynchronizationContext`). All controls and the theme service are used
  only there.
- Long work is `async`: the core offers `Task`-returning methods for work that can take long (hashing,
  HTTP, network diagnostics, discovery across network drives) and runs blocking parts with `Task.Run`
  internally; it uses `ConfigureAwait(false)` everywhere. Fast, pure logic (parsers, merge, value tables)
  stays synchronous.
- Every long operation takes a `CancellationToken`; operations with progress take `IProgress<T>`. Closing
  a page or the window cancels its operations.
- UI event handlers that await are `async void` **only** through `UiOperation.Run(control, ...)`: it
  disables the triggering control, catches and logs every exception, shows a localized error, and
  re-enables the control. No other `async void`.
- One operation of a kind at a time (a second click while a reset runs is ignored).
- `TaskScheduler.UnobservedTaskException` is logged.
- The online player list keeps its semantics (no I/O in constructors, one log line per outage, cancel on
  dispose, whole-exchange timeout of `NeoApiClient`); the loop becomes `async` with `Task.Delay`
  (`PlayerListPoller`, L-WP6; its events come through the context of `Start`, [ADR 0004](adr/0004-async-await-threading-model.md)
  amendment).

## 6. Error handling

Decided in [ADR 0013](adr/0013-error-handling-and-logging.md):

- **Environment problems are results, not exceptions.** A missing key, denied access, a damaged file, an
  invalid path or an unreachable server becomes a status or finding with the reason; it is logged once and
  shown with a next step. Discovery, integrity and diagnostics never fail as a whole.
- **Programming errors throw** (`ArgumentNullException`, invariant violations). They reach the global
  handlers, which log them and show "unexpected error" (existing behaviour).
- Adapters catch only the exception types an operation documents (`IOException`,
  `UnauthorizedAccessException`, `SecurityException`, `HttpRequestException`, `TaskCanceledException`, ...),
  never `catch (Exception)` except at the UI boundary (`UiOperation`) and in loops that must survive
  (player list polling).
- **Changes are all-or-nothing where possible**: backup first, abort if it fails; files written as `.tmp`
  then replaced; registry writes in a fixed order with the marker last, so an interrupted first run is
  simply repeated.
- **No silent fallbacks**: every fallback (default settings, fallback download page, unknown integrity) is
  logged and visible.

## 7. Logging

- `%LOCALAPPDATA%\Empire Earth Launcher\log.txt`, timestamped ISO 8601 lines, levels Info/Warning/Error,
  trimmed to the last 500 lines above 1 MiB with `log.txt.old` kept (existing behaviour).
- Logged: start with version, Windows version and culture; every discovery candidate and why it was taken
  or dropped; integrity findings with path, class, expected and actual hash; every registry value the
  launcher writes or deletes with old and new value (a deleted key of the cleanup with its id, the missing folder and
  the evidence; its values are in the `.reg` backup); every backup file and every moved, exported or imported file;
  game starts; HTTP requests with URL, status and duration (no bodies beyond the trimmed answer); fallbacks.
- **Never logged**: values below `Software\Sierra\CDKeys` (only "exists" / "missing"), contents of
  `_wonlogin.ks` or other WON key files, passwords, anything typed into the lobby, the player and profile names
  of the name check (only how many have characters outside printable ASCII).
- **Network diagnostics** (L-WP9): one line per adapter, lookup, file, the update API request and the status server,
  and one with the verdict, the port table and the hints, all under the privacy rules of the report (ADR 0013 plan
  review): no MAC address, adapter GUID or adapter name, no public, CGNAT or external address and no IPv6 address
  (only their class), paths anonymized. The text of the diagnostics report is never logged, only that it was copied or
  where it was saved.
- Log messages are English (support language of the forum and of the developers); UI texts are
  localized.

## 8. Files and settings

Decided in [ADR 0005](adr/0005-own-settings-file-instead-of-user-config.md):

| What | Where | Format |
|---|---|---|
| user settings: chosen folder, theme, custom theme file, UI language, last game (`LastGame`, L-WP6), hidden hints (`HiddenHints`: finding and game settings key, values or folder; L-WP5) | `%LOCALAPPDATA%\Empire Earth Launcher\settings.json` | JSON (`DataContractJsonSerializer`), `SchemaVersion`, unknown members kept, written as `.tmp` then replaced; damaged file renamed to `settings.json.damaged` and defaults used; a file that cannot be read or has a higher `SchemaVersion` is never overwritten (ADR 0005 amendment) |
| server settings: NeoEE host, port, timeout, poll interval | `Empire Earth Launcher.exe.config` next to the program | `applicationSettings` (read-only, admin-editable, as today) |
| log | `%LOCALAPPDATA%\Empire Earth Launcher\log.txt` | text |
| backups (`.reg`, moved WON files, replaced saved games) | `%LOCALAPPDATA%\Empire Earth Launcher\Backups\<yyyy-MM-dd_HHmmss>_<what>\` (L-WP5: `display-settings`, `reset-game-settings`, `remove-runasadmin`; L-WP8: `registry-cleanup`, `won-login-reset`, `import-saved-games`) | `.reg` (Windows Registry Editor 5.00, UTF-16 LE with BOM, CRLF) per game `<time>_<Product>_<EE|AoC>.reg`, `<time>_Layers.reg` or `<time>_registry-cleanup.reg`, and original files in `EE\`, `AoC\`, `EE-VirtualStore\`, `AoC-VirtualStore\` with `moved-files.txt` (UTF-8 with BOM, CRLF, one line `<copy> <- <original path>` per file) |
| exports of saved games | a new folder `Empire Earth saves <yyyy-MM-dd_HHmmss>` in the folder the user chooses (not in a game folder, its VirtualStore copy or the install root) | folder with `EE\Saved Games`, `EE\Scenarios`, `AoC\Saved Games`, `AoC\Scenarios`; no zip (dropped in L-WP8) |
| diagnostics report (L-WP9) | only when the user saves it: the file the user chooses (`Documents` and `Empire Earth Launcher report <yyyy-MM-dd_HHmm>.txt` suggested; never inside an installation) | English text, UTF-8 with BOM, CRLF |
| mod creator working data | `%LOCALAPPDATA%\Empire Earth Launcher\...` (existing) | unchanged |

`%LOCALAPPDATA%` falls back to the temporary folder when it is empty (existing fix). The launcher never
writes next to its executable (it may be below `Program Files`).

## 9. Localization

Decided in [ADR 0009](adr/0009-localization-with-resx-en-de-fr.md):

- English is the neutral language (`NeutralResourcesLanguage("en")`); German (`de`) and French (`fr`)
  are complete; every other UI culture falls back to English. Portuguese (Brazil) and Chinese are not
  machine-translated.
- Every UI text is set from code from `Properties/Resources*.resx` of the launcher (including the
  navigation, which came from `MainForm.fr.resx` before L-WP3), in one `ApplyTexts()` per window or page;
  `Texts` picks the text for a result. Designer texts are placeholders that are overwritten at start; an
  architecture test (`ApplyTextsTests`) checks that every designer text is set again. One place per text makes
  the parity test possible.
- An architecture test reads the `.resx` files of the source tree and checks the **string entries**
  (comments removed, entries with `type` or `mimetype` skipped): the same keys in `en`, `de`, `fr`; no empty
  value; the same `{n}` placeholders. A second test checks that image and file entries exist only in the
  neutral resx; further tests check the generated `Resources` class, the project items and the built satellite
  assemblies. Since the review fixes after L-WP9 the three languages have the same 371 string keys (L-WP9: 369; L-WP8: 293; L-WP7: 213; L-WP6: 154; L-WP5: 125;
  L-WP4: 74; L-WP3: 53; before: `en` and `fr` 30 each and about 50 designer-only texts,
  [ADR 0009](adr/0009-localization-with-resx-en-de-fr.md), corrected evidence).
- German is proof-read by the user in the laptop test; French texts are marked "review open" in
  `docs/TRANSLATING.md` until a French speaker has read them.
- The diagnostics report and the log are English in every UI language (the support language of the forum); the
  network section of the *Tools* page shows its verdict, hints and details in the UI language (L-WP9).
- The UI language follows Windows unless the setting `UiCulture` (Launcher page) chooses one; it is applied at
  start, before the first window, for the UI culture only (formats stay those of Windows), so a change needs a
  restart.
- The mod creator keeps English and French (no new texts in v2).
- Contributor guide: [TRANSLATING.md](TRANSLATING.md) (languages, files, placeholder rules, adding a text or a
  language, review state: French "review open").

## 10. Security and privacy

- **CD keys**: no call of `authtools.dll`; the whole subtree of `Software\Sierra\CDKeys` in HKCU, HKLM64
  and HKLM32 and every ancestor key (`Software`, `Software\Sierra`) are protected by `RegistryWritePolicy`,
  which every write and delete passes after the path was put into its canonical form (`WOW6432Node` and
  `HKCU\Software\Classes\VirtualStore\MACHINE\...` mapped to the HKLM key they stand for), unit-tested with a
  table of every alias and every operation; the diagnostics only say whether the key exists. Repairing CD
  keys = running the setup. The same rule protects the install records and the uninstall keys; an allow-list
  entry can never open a protected key, also not one that names it exactly. The registry cleanup (L-WP8) can delete
  only the eight HKCU keys of its list; no entry and no advice names `Software`, `Software\Sierra` or `CDKeys` as a
  target (tested on the canonical form, for every hive, view and alias), and `Software\Sierra` is shown as "do not
  delete: contains the CD keys". The cleanup and the backups never follow a symbolic registry link, and a `.reg` backup
  never holds a name with a line break, which could smuggle a line such as `[-HKEY_LOCAL_MACHINE\SOFTWARE\Sierra\CDKeys]`
  into the file (security review, ADR 0007).
- **No changes while a setup or game runs**: every write goes through the mutation guard
  ([ADR 0016](adr/0016-mutation-guard-and-effective-game-paths.md)).
- **No elevation**: the manifest requests `asInvoker`; the launcher never restarts itself elevated and
  never writes HKLM or other users' hives.
- **Network**: three destinations only - the NeoEE status server (configured, plain TCP, public data,
  every reply validated - existing), `api.empireearth.eu` for the setup URL and the version check (both only on
  request, since L-WP7), and DNS lookups in the network diagnostics. The network check of L-WP9 adds no destination: it
  resolves names and repeats the request of the player list and the query of contract 4.3; it asks no "what is my IP"
  service and never connects to the ports 10002 and 10003 of NeoEE. `NetworkDestinationTests` keeps it so: name lookups
  only in `WindowsNetworkInfo`, sockets only in the WON library's `NeoApiClient`, HTTP only in `HttpsClient`, no source
  names 10002 or 10003, and every URL literal of the sources is one of the allowed ones. HTTPS with certificate validation, TLS 1.2 or newer, never
  `http://`, no redirects, timeouts, `SecurityProtocol` set once (`Tls12` on Windows 7 only, no explicit `Tls13`)
  ([ADR 0008](adr/0008-https-policy-and-update-api.md)); an architecture test
  forbids every certificate-validation override (`ServerCertificateValidationCallback`,
  `ServerCertificateCustomValidationCallback`, `RemoteCertificateValidationCallback`, ...). No telemetry; the
  old "collect diagnostic data" checkbox is removed.
- **Backups contain login data** (moved WON files); the UI (the result of the reset and the "Backups" section next
  to "Open backup folder") and the README say so, the diagnostics report never includes them.
- **The diagnostics report** is made only on request, copied or saved by the player and never sent; it follows the
  privacy rules of ADR 0013 (plan review) through `ReportAnonymizer`, and so do the log lines of the network check
  (4.6, 7).
- **URLs opened in the browser**: only the fixed download page or a URL that passed `UpdateUrlPolicy` (same
  rules and test cases as the setup's `IsAllowedUpdateUrl`).
- **Files from outside**: imported saves are untrusted (plain file names only, `.ees`/`.scn` only, characters of the
  ANSI code page, 64 MiB at most, never onto a file of the manifest and nothing at all while the manifest exists but
  cannot be used, no overwrite without confirmation and a copy of the old file); there is no zip import (dropped in L-WP8); the manifest never makes the launcher open a file outside
  the install root; mod archives keep their existing limits.

## 11. Testing

Decided in [ADR 0012](adr/0012-test-strategy.md):

- **One test program**, `Empire-Earth-Launcher.Tests` (NUnit 3.14 + NUnitLite, console, exit code = failed
  tests), run by the local verify script under Mono and by CI on Windows.
- **Real-machine checks** (ADR 0012 amendment of the CI end-to-end test): a second program,
  `Empire-Earth-Launcher.RealMachineTests`, for the end-to-end workflow of the setup repository, which installs the real
  setups on a throwaway GitHub-hosted Windows runner. After each step it runs the core against the real installation as
  the launcher composes it: discovery, quick and full integrity check, status of the defaults and consistency findings,
  on request the defaults of the launcher start, a second start and the reset, and last a comparison of everything the
  launcher must leave alone (CD keys of every view, records, Inno uninstall keys, compatibility layers, HKLM settings,
  the files of the installation) with its state before. A JSON expectation file per step names what to check. The
  fixtures (category `RealMachine`) are explicit and need `EE_LAUNCHER_REAL_MACHINE_TESTS=1` on Windows on a
  GitHub-hosted runner; elsewhere the program runs only its self-tests on the fakes of the unit tests. Reading checks get
  read-only wrappers, the defaults write through the launcher's write policy and are compared with the values of
  contract 3, the core log stays in the work folder, and no output holds a hash. `RealMachineTestRulesTests` keeps the
  exception narrow (README, Tests, "Real machine").
- **Unit tests for the whole core** with fakes: `InMemoryRegistry` (hives, views, value kinds),
  `InMemoryFileSystem` (Windows path semantics, case-insensitive, attributes, read errors), fake HTTPS
  client, process starter, mutex probe, clock, system info. Because path logic is `WinPath` string logic,
  the tests run on Linux/Mono without `[Platform("Win")]`.
- **Contract tests**: discovery with all five sources, 1.7.2 installations, foreign and damaged ones,
  merging and two products in one root, key-before-hive order, 32-bit Windows (one HKLM view, no duplicates);
  manifest BOM/CRLF/LF/uppercase/binary marker/invalid lines/paths outside the root; file classes equal to
  the contract table; every defaults value and computed value (also for `C:\Games\EE`, `D:\Empire Earth` and
  under `tr-TR`); marker semantics; no write at launcher start for an ambiguous installation; reset with
  failing backup and with a higher contract version; mutation guard for every writing action; URL policy with
  every case of the setup's unit tests.
- **Architecture tests**: core references no UI assembly; every project targets v4.8 with `LangVersion` 8.0
  and `Deterministic`; resource parity (strings only, also of the built satellites); every designer text set
  again in `ApplyTexts()`; no certificate-validation override in the sources; no removed placeholder control
  name in a designer file; the registry alias table.
- **Only checkable criteria** ([ADR 0012](adr/0012-test-strategy.md) amendment): "does not block" is a test
  that the async method returns an unfinished task with a blocking fake; performance is counted (file opens,
  hashes), not timed; translation quality and layout are test-plan cases.
- **Golden files** for `.reg` output (since L-WP5: `Core/Backup/Golden/`, one file per value type, delete lines,
  escaping; `.gitattributes` marks `*.reg` binary so that the UTF-16 bytes stay exact) and the diagnostics report (since
  L-WP9: `Core/Diagnostics/Golden/DiagnosticsReport.txt`, kept with CRLF by `.gitattributes`).
- **Windows adapters** (registry, mutex, shell execute, display, HTTP) are thin and checked on real Windows
  by the German test plan `docs/TEST-PLAN.de.md`; the UI is tested manually with it. The test plan is created
  in the first work package and every package adds its cases in the same commit.
- Tests never use the network, the real registry, the real `%LOCALAPPDATA%` or show UI (the real-machine checks read the
  real registry and the installation, and write HKCU game settings, only on a GitHub-hosted runner of the setup
  repository's end-to-end workflow). The tests of the category `WinForms` create controls and pages without showing them
  and paint them into a bitmap; under Mono they need a display (`xvfb-run`), and pages whose Krypton controls call
  Windows libraries run only on Windows ([ADR 0012](adr/0012-test-strategy.md) amendment of 2026-10-03).
- **Plan review additions** ([ADR 0012](adr/0012-test-strategy.md) amendment): fixtures are synthetic only
  (`FixtureProvenanceTests`); `docs/contract-samples/` holds byte samples of `install.ini`, `files.sha256` and the
  record, shared with the setup repository; the tests run on the laptop too (`Tests\` in the laptop package,
  category `SourceTree` excluded); `TestPlanTests` checks that the test plan assigns every requirement and every
  forum test case. Implemented in L-WP5: `TestPlanTests`, and the category on every test that reads the source tree,
  enforced by `RepositoryRoot` (ADR 0012 amendment of L-WP5). Implemented in L-WP6: `TestIsolationTests` (the tests
  create no `WindowsRegistry`, no HTTP client, socket or DNS lookup, use no `Microsoft.Win32.Registry` and no file of
  the launcher's real folder) and `ProcessRulesTests` (only the shell starter calls `Process.Start`, never without the
  shell or with "runas", nothing ends a process); the laptop package with `Tests\` was built once and its tests ran
  outside the repository with `--where "cat != SourceTree"`: 2213 passed, none failed (ADR 0012 amendment of L-WP6).
  Implemented in L-WP7: `docs/contract-samples/` with the readers tested against it, `FixtureProvenanceTests` (every
  64-hex token of the fixtures and samples is a synthetic hash), `NoCertificateOverrideTests` and `TlsSettingTests`, the
  integrity and update models with fakes (`FakeHttpsClient`), and the counted cost of the checks (ADR 0012 amendment of
  L-WP7). Implemented in L-WP8: tests against real files in a temporary folder (`TemporaryDirectory`) through
  `MappedFileSystem`, which maps the drive `T:` of Windows paths onto that folder over `LocalFileSystem` (file backup,
  WON reset, export and import); a `Blocked(SetupRunning)` and a `Blocked(GameRunning)` test for every writing action;
  `CleanupCandidatesTests` compares the code table with the table of 4.6 and checks the evidence of every entry; the
  policy and advice tests run every list entry and every alias of the protected keys through the canonical form; the UI
  mapping of the cleanup (`CleanupView`), `MaintenanceModel` and the texts are tested with fakes (ADR 0012 amendment of
  L-WP8). Implemented in L-WP9: `FakeNetworkInfo` (adapters and name lookups from a table) and a fake status server;
  every combination of the outage verdict (`OutageHintTests`); the diagnostics report as a golden file of a synthetic
  computer and a negative test with a fake full of IP, IPv6 and MAC addresses, adapter GUIDs and names, computer,
  domain, user and player names and a filled `Software\Sierra\CDKeys` (`NOT-A-KEY-0000`), none of which may appear
  (`DiagnosticsReportTests`); the privacy of the log lines (`NetworkDiagnosticsTests`); `NetworkDestinationTests`; and
  `TestPlanTests` in their final form (cases for every package, nothing "offen", the table "Vertrag 7" with one row per
  launcher item of CONTRACT.md 7) with `ContractChecklistTests` for the ticked checklist of section 15. The laptop
  package of L-WP9 ran its tests outside the repository (ADR 0012 amendment of L-WP9).

## 12. Build and CI

Decided in [ADR 0001](adr/0001-target-dotnet-framework-4-8.md) and
[ADR 0002](adr/0002-keep-classic-project-files-and-packages-config.md):

- Classic (non-SDK) `.csproj` files, `packages.config`, NuGet restore into `packages/`, every project with
  `TargetFrameworkVersion` v4.8, `LangVersion` 8.0, `Deterministic` true, `Prefer32Bit` false for the
  executables. Krypton.Toolkit from its `lib/net48` folder; NUnit from `lib/net45` (the newest .NET Framework
  build of NUnit 3.14).
- **Local (Linux)**: `verify_launcher.sh` - `mono nuget.exe restore`, `xbuild` with Roslyn 4.2, Mono's
  `4.8-api` reference assemblies, then every `*Tests.exe` under Mono.
- **Local Release build (CI reproduced)**: because nothing is pushed during the v2 work, the CI artifacts
  do not exist; a local script installs `Microsoft.NETFramework.ReferenceAssemblies.net48` 1.0.3 with
  `mono nuget.exe install`, builds `/p:Configuration=Release` with `TargetFrameworkRootPath` and
  `FrameworkPathOverride` on that package (as CI does) and runs the tests from `bin/Release`. It lives in the
  scratch folder of the v2 work, not in the repository, because it depends on local paths. Two gaps of xbuild
  are bridged there and checked after the build: xbuild resolves the framework only through
  `TargetFrameworkRootPath` and cannot read the two mixed-mode stubs `System.EnterpriseServices.Thunk.dll` and
  `.Wrapper.dll` of the package (a copy without them is used; no project references them, Mono's own `4.8-api`
  lacks them too), and its `Csc` task ignores `ApplicationManifest` (the compiler wrapper adds
  `/win32manifest`, as MSBuild does). The **laptop package** (zip of the Release builds of launcher
  and mod creator with `de/`, `fr/`, `LICENSE`, `THIRD-PARTY-NOTICES.md`, `licenses/THIRD-PARTY-LICENSES.txt`,
  and of the test program in `Tests\`, plus a `.sha256` file) is made in the scratch folder, never committed.
- **CI** (`windows-latest`): NuGet restore, MSBuild Release against
  `Microsoft.NETFramework.ReferenceAssemblies.net48` 1.0.3 (independent of the image's targeting packs),
  all `*Tests.exe` (the unit tests and the self-tests of the real-machine checks, whose `RealMachine` fixtures stay
  skipped; the setup repository's end-to-end workflow builds the program and runs them), then the test builds of launcher and mod creator as artifacts with `LICENSE`,
  `THIRD-PARTY-NOTICES.md` and `licenses/THIRD-PARTY-LICENSES.txt`.
- Runtime requirement: .NET Framework 4.8 (built into Windows 10 1903 and later and Windows 11; an
  installer for Windows 7 SP1, 8.1 and older 10; **not available for Windows 8.0**). `App.config`:
  `<supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" />`.

## 13. Requirements map

| Req. | What | Core modules | Contract | Work package |
|---|---|---|---|---|
| D4 | .NET 4.8, core + thin UI, tests | all | | L-WP1, L-WP2 |
| D5 | shared contract, launcher side | Installations, Integrity, GameSettings, Repair, Play | 1 to 5 | L-WP4 to L-WP7 |
| R1 | per-user defaults on first run, class S created at start for unambiguous installations | GameSettings | 3 | L-WP5 |
| R2 | integrity manifest check | Integrity | 2 | L-WP7 |
| R3 | play EE/AoC, running instances, log, compatibility options | Play, GameSettings | 3.7, 4.2 | L-WP5, L-WP6 |
| R4 | reset with `.reg` backup | GameSettings, Backup | 3.6 | L-WP5 |
| R5 | safe registry cleanup | Maintenance, Backup | 3.8 | L-WP8 |
| R6 | WON login reset | Maintenance, Backup | | L-WP8 |
| R7 | network diagnostics; partly: the comparison with the adapter the game uses waits for where EE stores it (14, WP9-02) | Diagnostics | | L-WP9 |
| R8 | VirtualStore detection | Installations, Maintenance | | L-WP4 (effective paths), L-WP8 |
| R9 | repair hand-off, pending setup | Repair, Play | 4 | L-WP6 (`RepairAdvice`, fixed page), L-WP7 (API) |
| R10 | saves/scenarios export and import, name checks | Maintenance | | L-WP8 |
| R17 | en/de/fr | UI resources | | L-WP3 and every later package |
| R18 | docs, ADRs, README/CHANGELOG, test plan | | | every package; test plan from L-WP1, final check L-WP9 |
| Laptop test | Release package | | | script L-WP1, package L-WP9 (possible after L-WP6) |

## 14. Open points

Launcher stance on the open questions of the contract (6):

- **O1 AppIds**: not hard-coded; taken from the record or the uninstall key name (contract proposal).
- **O2 NeoEE updater**: differences of `code` files in NeoEE installations are worded neutrally ("changed
  since the installation") until clarified.
- **O3 Encoding**: the readers accept BOM, LF, CRLF; tested.
- **O4 Physical pixels**: the launcher measures the primary screen with `EnumDisplayDevices` +
  `EnumDisplaySettings` (independent of its DPI awareness, [ADR 0011](adr/0011-screen-size-in-physical-pixels.md));
  whether the setup gets the same numbers on scaled screens is a test-plan item.
- **O5 Portable**: found through the user choice, the launcher folder or the HKCU "Installed From" values.
- **O6 Mutable files**: classes as in the contract; the laptop test runs a full check after playing.
- **O8 CD keys**: existence check only.
- **O9 Launcher mods**: v2 installs no mods (the mods page stays hidden), so no attribution is needed yet.
- **O10 Launcher in the setup**: not in v2; the single-instance mutex name is chosen so that the setup can
  use it as `AppMutex` later.
- **O11 Two products in one folder**: warning and integrity state "unreliable".

Further points to settle in the work packages, on real Windows (test plan):

- the game's own network adapter setting (where EE stores it, t=32479; WP9-02 collects it) and the real format of
  `upnp_info.txt`: the parser of L-WP9 recognizes labelled lines as UPnP tools write them and says "unknown format"
  for anything else, until a real file (WP9-10, external address replaced) confirms or corrects it;
- DS-Lite is recognized only indirectly (a CGNAT or 0.0.0.0 external address in `upnp_info.txt` together with a
  global IPv6 address), because the launcher asks no external service; the real network check on Windows (adapter
  types and descriptions of VPN clients, Windows 7 and TLS 1.2 against the update API) is WP9-01 to WP9-07 and W7-06;
- the stale keys of retail, GOG and old patch installations for the cleanup: the table in 4.6 holds only entries
  with evidence (forum t=1036 p=4756, t=12082 p=49553 name Sierra, SSSI, Mad Doc, Stainless Steel Studios); further
  entries (subkeys of `Software\Sierra` other than `CDKeys`, Stainless Steel Studios, InstallShield) need a sample from
  a real computer, which test case WP8-02 collects;
- the real behaviour of the maintenance tools on Windows: the VirtualStore of a standard user below `Program Files`,
  drive kinds of USB and network drives, the Explorer opening the backup folder, and multiplayer saved games with
  umlaut names (WP8-08, WP8-04, WP8-14, WP8-10);
- Krypton's net48 build on Windows 7 SP1 and on scaled screens.

Clarifications of the contract text that this section proposed, made by contract revision 3 in both repositories
at once (contract 5, still version 1); the launcher already implemented the stated reading, so its code did not
change:

- **1.4 source 4 order**: key before hive, `Software\Neo\Empire Earth` in HKCU, HKLM32, HKLM64, then
  `Software\SSSI\Empire Earth` in the same order, like sources 2 and 3.
- **1.4 EE and AoC folders of foreign installations**: the real folders the sources name ("Installed From", ADR 0015),
  the AoC folder from the AoC key in the same hive and view or from the user choice, which may be an AoC folder.
- **1.4 Kind**: without `install.ini`, a registry record with `ContractVersion` 1 or higher also means `community`
  (L-WP4, see 4.1).
- **3.6 Launcher start** (ADR 0015 plan review): class S is only created when both values are missing, and like the
  first run only for an installation that is unambiguous for its game settings key; it is synchronized before every
  start.
- **3.6 Class S before every start** (L-WP6): "before every game start while no other game runs"; the launcher does
  not change game settings while a game runs (ADR 0016), the next start without the other game does it.
- **3.6 Display question**: the launcher asks once per start of the launcher until the question is answered (the
  markers wait for the answer), so a launcher closed without an answer asks again; the contract says "until the user
  answers" (3.2, 3.5, 3.6).
- **2.5 Modified** (L-WP7, review fixes): "no message and no repair offer; the state may be shown, the files are listed
  only in the diagnostics". The *Play* page shows the state as one informative line, "Files: OK, game data changed",
  without "Repair...", without a message and without a window, because a player who installed a mod or an HD pack
  should see that the check knows it; the files are listed on the *Tools* page and in the diagnostics report.
- **4.5 No answer of the update API** (L-WP7): "a request without an answer of HTTP 200 is no statement about the
  version". The launcher reports it as "could not be asked" (with the reason in the log), so the player is never told
  that an unchecked version is current; the setup's `CheckUpdate` asks no update question then.

Still open, or readings that need no change of the contract text:

- **3.3 upper-casing**: "ASCII letters only" would make setup and launcher byte-identical, but it changes the
  launcher's code, so it is no clarification and revision 3 left it out; until a later contract change the launcher
  keeps `ToUpperInvariant` and compares case-insensitively (ADR 0015).
- **Shared byte samples** (not a contract text change, hand-over to the setup work): `docs/contract-samples/` exists
  in the launcher since L-WP7 (`install-admin.ini`, `install-user.ini` with `[MissingAfterInstall]`,
  `install-portable.ini`, `files.sha256`, `record.reg`, all synthetic, `-text` in `.gitattributes`). The setup repository
  is to take the folder over identically: its unit tests compare the bytes of `BuildInstallIniText`,
  `BuildMissingAfterInstallText` and the manifest lines with it, and `ci/compare_contract.py` compares the folder as it
  compares the contract (ADR 0012 plan review). Until then the launcher's copy is a proposal; a change of a sample is a
  step in both repositories.
- **2.5 The message of Damaged and Incomplete** (L-WP7, not a text change): the quick check runs in the background, so
  the launcher gives the message of 2.5 as the state on the Play page with "Repair...", the explanation and the files on
  the *Tools* page and the repair window; it opens no window by itself.
- **Old Windows 7 compatibility values in HKCU**: the launcher only shows them and advises running the setup;
  letting the launcher remove them would be a contract change (ADR 0007 plan review). Since contract revision 2 it
  does not show them for an installation whose `Tasks` contain the setup's opt-in task `compatibility_legacy` (3.7
  MUST NOT, implemented in L-WP5); it offers no switches on Windows 7 although 3.7 now allows the values of that row
  (MAY), because the setup sets them on request and the plan keeps the launcher's switches to Windows 8 and later.
- **4.2 Discovery while a setup runs** (L-WP6, not a text change): the launcher implements "reads `install.ini` only
  after the mutex is gone" by not starting the search at all while a setup mutex exists and searching once it is gone.
- **Backups of class S and of the compatibility switches** (L-WP5): ADR 0007 asks for a backup before every
  overwrite. Class S is synchronized before every Play (contract 3.6) and logged with old and new value; a `.reg`
  file per start would fill the backup folder with copies of two derived values. A compatibility switch changes one
  entry the player chose on the same page and is undone by the same switch (logged with old and new value). Both
  therefore write no backup; removing `~ RUNASADMIN`, which the launcher cannot add back, and every change of D and P
  values write one (ADR 0007 amendment of L-WP5).

Platform: Windows 8.0 is not supported (.NET 4.8); Windows 7 SP1 is supported but not tested on the laptop
(optional VM case in the test plan; TLS cipher suites of Windows 7 against `api.empireearth.eu` unknown, the
fallback page always works).

## 15. Work packages

Ordered; every package ends with `verify_launcher.sh` green, its CHANGELOG/README/docs and its test-plan
cases in the same package, and no visible control without function. **MVP for the laptop test: L-WP1 to
L-WP7** (start the game safely with per-user defaults, and the integrity check with the repair hand-off, the core
of the shared contract). Parts that can be dropped if time runs out (documented in the CHANGELOG): the setup
version check in L-WP7, the zip import, the zip export (the folder export stays) and the registry VirtualStore
display in L-WP8, the `upnp_info.txt` parser in L-WP9. **Not droppable**: the game version check (L-WP7) and the
outage hint (L-WP9): lobby (113 threads), network (57) and versions (49) are the most frequent forum topics (forum
report 4). The laptop package can be built after every package from L-WP6 on and contains the test program
(ADR 0012 plan review). The plan review kept L-WP8 before L-WP9: the diagnostics report and the laptop package
of L-WP9 need the results of every tool, and L-WP8 holds the WON login reset, a lobby fix (p=83519).

| No. | Package | Main content |
|---|---|---|
| L-WP1 | Toolchain .NET 4.8, local Release build, test plan skeleton | retarget all projects, app.manifest, CI on net48, local Release build like CI, `docs/TEST-PLAN.de.md` skeleton, README platforms, CHANGELOG |
| L-WP2 | Core foundation | core project, contract names, platform interfaces and fakes (32-bit mode), `WinPath`, registry path canonical form and protected-key policy, settings.json, logging, `UiOperation`, mutation guard |
| L-WP3 | UI clean-up and localization | remove the placeholder controls first (ADR 0014 list, architecture test), `ApplyTexts()`, German and French complete, string parity test, language setting, `TRANSLATING.md` |
| L-WP4 | Installation discovery (contract 1) | five sources, key before hive, real EE/AoC folders, merge, kinds, damaged installations, selection, shared-key hint, effective paths for lobby profiles |
| L-WP5 | Game settings (contract 3) | value table, S from the real folder, first run only when unambiguous, S created at start only when missing, reset with exact `.reg` backup, consistency warnings that can be hidden (window check with the game's DPI view), compatibility options from Windows 8 on (no `WINXPSP3`, HKLM version layer respected), write policy allow-list with layer content check, `TestPlanTests` |
| L-WP6 | Play (contract 3.7, 4.2) | shell execute, mutex order, setup watcher, hanging-process hint, file versions, async player list, single instance, `RepairAdvice` with the fixed download page |
| L-WP7 | Integrity and repair hand-off (contract 2, 4) | manifest, classes, quick/full check (cancelled by a setup, `FileShare.Delete`), badge, HTTPS client (TLS set once), URL policy, download locator, game version check, quick-check hook of the setup watcher, `docs/contract-samples/`, synthetic fixtures |
| L-WP8 | Maintenance tools | registry cleanup by the table of 4.6 (HKCU only, advice never names `Software\Sierra`), WON login reset, VirtualStore, saves and scenarios, name checks, all behind the mutation guard |
| L-WP9 | Network diagnostics, report, laptop package | adapters, DNS, `NeoEE.cfg`, `upnp_info.txt`, `CDKeyCheck`, outage hint, report with the privacy rules of ADR 0013, final docs, test plan without "offen", Release zip with `Tests\` and SHA-256 |

Done: L-WP1 to L-WP9, every package of the plan (see the CHANGELOG). Implementation details of L-WP2 that refine ADR
0004, 0005, 0007 and 0016, of L-WP3 that refine ADR 0009 and 0014, of L-WP4 that refine ADR 0004, 0006, 0015 and 0016,
of L-WP5 that refine ADR 0007, 0011, 0012, 0015 and 0016, of L-WP6 that refine ADR 0004, 0005, 0010, 0012, 0014 and
0016, of L-WP7 that refine ADR 0004, 0008, 0012, 0014 and 0016, of L-WP8 that refine ADR 0007, 0012, 0013, 0014 and
0016, and of L-WP9 that refine ADR 0006, 0008, 0012, 0013 and 0014 are recorded in their amendments. From L-WP6 on the
laptop package can be built with `Tests\`. The MVP for the laptop test (L-WP1 to L-WP7) is complete; nothing of L-WP7
was dropped (the setup version check is implemented). L-WP8 dropped the zip export and the zip import (the folder
export and the import of single files stay, CHANGELOG); the registry VirtualStore display is implemented (the `vs-*`
rows of 4.6). Nothing of L-WP9 was dropped: the `upnp_info.txt` parser is implemented (tolerant, "unknown format"
otherwise), and the outage hint, which could not be dropped, links from the *Play* page. The test plan has no "offen"
left; what only real Windows can show is in its cases and in section 14.

### Launcher checklist of CONTRACT 7

The launcher items of the implementation checklist of [CONTRACT.md](CONTRACT.md) (section 7, "Launcher v2"), each in the
UI-free core library with unit tests on fakes and without network. `ContractChecklistTests` checks this table: one
ticked row per item of the contract, and every test class it names exists; the test plan assigns the same items to
real-Windows cases (table "Vertrag 7", checked by `TestPlanTests`).

| Done | Contract 7, launcher v2 | Unit tests | Test plan |
|---|---|---|---|
| [x] | discovery (1.4) with all five sources, setups up to 1.7.2, foreign and damaged installations, merging (L-WP4) | `DiscoveryContractTests`, `InstallationDiscoveryTests`, `InstallRecordReaderTests`, `InstallInfoFileTests`, `UninstallKeyScannerTests`, `InstalledFromReaderTests`, `GameFoldersTests` | Launcher 1 |
| [x] | manifest reader and checks (2): BOM, CRLF, invalid lines, paths outside the root, classes, states, the uninstall key rule of 2.5 (L-WP7) | `ManifestReaderTests`, `FileClassifierTests`, `IntegrityCheckerTests`, `ContractSampleTests` | Launcher 2 |
| [x] | defaults, marker, consistency checks and reset with backup (3) (L-WP5) | `GameSettingsTableContractTests`, `ComputedValuesTests`, `GameDefaultsServiceTests`, `DisplayQuestionTests`, `ConsistencyChecksTests`, `RegFileWriterTests`, `RegistryExportTests` | Launcher 3 |
| [x] | repair hand-off and update check (4) with the URL cases of the setup's unit tests (L-WP6, L-WP7) | `RepairAdviceTests`, `UpdateUrlPolicyTests`, `SetupDownloadLocatorTests`, `UpdateCheckerTests`, `UpdateModelTests` | Launcher 4 |
| [x] | setup and game mutexes (4.2): no game start, no reading of `install.ini` and `files.sha256` and no integrity check while a setup mutex exists (also not by the maintenance tools, review fixes), a running check cancelled, the share modes; starting the games with shell execute (L-WP6, L-WP7) | `GameStarterTests`, `SetupWatcherTests`, `InstallationServiceTests`, `IntegrityCheckerTests`, `IntegrityModelTests`, `MaintenanceModelTests`, `VirtualStoreScannerTests`, `LocalFileSystemTests`, `ShellProcessStarterTests`, `ProcessRulesTests` | Launcher 5 |

## 16. Not in v2

Kept as planned features in the README, with the reason (forum report section 8):

- **DirectX wrapper switch** (row 4): means adding or removing DLLs in the game folders; the launcher must
  not change game files (contract 2.5). The setup's custom installation switches the wrapper; the report says
  whether one is installed (the wrapper rule of contract 3.3, since L-WP9).
- **Resolution chooser with 4:3 hint** (row 6): the game has its own option; v2 offers the recommended display
  values and the warning below 768 pixels.
- **GPU driver version** (row 4): little support value for WMI or HKLM class-key reading; the report names the
  display adapter of the primary screen (`EnumDisplayDevices`, since L-WP9). Listed as planned in the README.
- **Ending a hanging game process** (row 14): the launcher explains and points to the Task Manager, it never
  kills a process (ADR 0010).
- **Checking ports from outside** (row 8): needs server support; v2 shows the forwarding table only.
- **Mods page, Discord presence, HD textures, dreXmod, skip intro, game font, ranking, file association**:
  placeholders without function (ADR 0014).
