# Launcher v2 architecture

Target architecture of Empire Earth Launcher v2: what it consists of, how data flows through it, and the
rules every change has to follow. The decisions behind it are recorded as ADRs in [adr/](adr/README.md);
what the launcher may read and change on a computer is specified in [CONTRACT.md](CONTRACT.md), the
contract shared with the Empire Earth Setup.

| | |
|---|---|
| Status | **Target**: describes v2 as it is being built on branch `v2`; the README describes what exists today |
| Based on | branch `v2` at `2dc6c43` (refactor/quality-fixes plus the contract), contract version 1 (draft); revised after the design review (ADR amendments of 2026-10-02, ADR 0015, 0016) |
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
│  │                   classes, contract version
│  ├─ Platform/        abstractions + Windows implementations (ADR 0006):
│  │                   IRegistry / WindowsRegistry (hive + view always explicit), IFileSystem /
│  │                   LocalFileSystem, WinPath (Windows path rules as pure string logic), IProcessStarter,
│  │                   IMutexProbe, ISystemInfo (Windows version, Wine, primary screen in physical pixels),
│  │                   IClock, IHttpsClient (ADR 0008), INetworkInfo; RegistryPath (canonical form:
│  │                   WOW6432Node, registry VirtualStore), RegistryWritePolicy and PolicyCheckedRegistry
│  │                   (the IRegistry wrapper every change passes, ADR 0007)
│  ├─ Logging/         ILogger, LogLevel, TraceFileLogger, log trimming (moved from the launcher)
│  ├─ Settings/        LauncherSettings, SettingsStore (settings.json, ADR 0005), LauncherPaths
│  ├─ Installations/   install record, install.ini, uninstall keys, "Installed From", launcher folder,
│  │                   user choice -> InstallationDiscovery -> Installation with real EE/AoC folders
│  │                   (contract 1, ADR 0015); EffectivePathResolver (VirtualStore, ADR 0016)
│  ├─ Integrity/       ManifestReader, FileClassifier, IntegrityChecker (quick/full), IntegrityReport
│  │                   (contract 2)
│  ├─ GameSettings/    default value table, computed values, defaults marker, DefaultsService (first run,
│  │                   before start, reset), consistency checks, GPU preference, compatibility layers
│  │                   (contract 3)
│  ├─ Backup/          RegFileWriter (.reg export), FileBackup (move files into a dated backup folder),
│  │                   backup locations (ADR 0007)
│  ├─ Play/            GameStarter, running-game and running-setup detection, MutationGuard
│  │                   (contract 4.2, 3.7, ADR 0010, ADR 0016)
│  ├─ Repair/          UpdateUrlPolicy (port of the setup's IsAllowedUpdateUrl), SetupDownloadLocator,
│  │                   UpdateChecker, RepairAdvice (contract 4)
│  ├─ Maintenance/     RegistryCleanup, WonLoginReset, VirtualStoreScanner, SavedGames (export, import,
│  │                   name checks)
│  ├─ Diagnostics/     NetworkDiagnostics (adapters, DNS, NeoEE status, upnp_info.txt, NeoEE.cfg),
│  │                   WONLobby.cfg reader (CDKeyCheck, read-only), DiagnosticsReport
│  └─ Lobby/           LobbyProfileRepository (moved), PlayerListPoller (async replacement of the
│                      BackgroundWorker loop)
├─ Empire-Earth-WON/                      Empire_Earth_WON.dll - unchanged role: NeoEE status protocol
│                                         (NeoApiClient, DeadlineStream), WON lobby files
├─ Empire-Earth-Mod/
│  ├─ Empire-Earth-Mod-Lib/               Empire_Earth_Mod_Lib.dll - unchanged role: .eem format
│  └─ Empire-Earth-Mod/                   mod creator (WinForms) - retargeted only
├─ Empire Earth Launcher/                 Empire Earth Launcher.exe - thin UI:
│                                         Program (composition root), MainForm, pages, dialogs,
│                                         Texts (core results -> localized strings), UiOperation (async
│                                         event handler helper), KryptonThemeService, app.manifest
└─ Empire-Earth-Launcher.Tests/           one NUnitLite program: Core/, Launcher/, Won/, Mod/,
                                          Architecture/ (dependency, project and resource rules),
                                          Fakes/ (in-memory registry and file system, fake HTTP, process,
                                          mutex, clock), TestSupport/
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
| **Game settings** | defaults state, consistency warnings, apply recommended display settings, reset (with backup), compatibility options (HKCU only, HKLM read-only), screen warning below 768 pixels | R1, R4 |
| **Tools** | integrity details and full check, repair advice, registry cleanup, WON login reset, VirtualStore check, saved games and scenarios, network diagnostics, "copy diagnostics report", open backup folder | R2, R5 to R10 |
| **Launcher** | installations found and the user's choice, hint when several installations share one game settings key, theme, language (system, English, German, French) | R1, R17 |

The placeholders of the old designer are removed from the UI until they are implemented, as the first step
of the localization work, so that none of them is translated
([ADR 0014](adr/0014-only-working-features-in-the-ui.md), amendment: the exact list of control names, which
an architecture test checks; it includes the "collect diagnostic data" checkbox, which contradicts "no
telemetry"). The README keeps them as planned features.

## 3. Dependency rules

```
Empire Earth Launcher.exe ──> Empire_Earth_Launcher_Core.dll ──> Empire_Earth_WON.dll
          │                                │
          └──> Krypton.Toolkit             └──> BCL only (System, System.Core, System.Net.Http,
                                                System.Runtime.Serialization, System.Xml,
                                                System.IO.Compression)
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
  values, plus the launcher's own folder below `%LOCALAPPDATA%`, files the player imports into the game
  folders and the WON login files it moves into a backup. `Program` wraps the Windows registry in
  `PolicyCheckedRegistry` (from the first package that uses the registry, L-WP4), so no code path can write
  around the policy; the protected keys are refused on the canonical form first, then the allow-list is matched
  on the key as it is written (ADR 0007, implementation amendment).

## 4. Data flows

### 4.1 Start-up

1. `Program.Main`: global exception handlers, logger (trimmed `log.txt`), settings (`settings.json`,
   damaged file moved aside), UI culture (setting or Windows), theme, single-instance check
   ([ADR 0010](adr/0010-game-start-and-mutex-probing.md)).
2. MainForm opens at once; the pages show "searching" states. Nothing blocks the window.
3. **Discovery** (background, contract 1.4): user choice, registry records (NeoEE before EE; HKCU, HKLM64,
   HKLM32), uninstall keys by `Publisher`, "Installed From" values, launcher folder. For "Installed From" the
   **key goes before the hive** (Neo in HKCU, HKLM32, HKLM64, then SSSI in the same order), as for sources 2
   and 3 ("NeoEE before EE; per product ..."); the old locator let the hive win (forum report section 8,
   test case 8). Candidates are merged by normalized root (`WinPath`), classified (`community`,
   `community-legacy`, `foreign`), damaged ones kept; each keeps its real EE and AoC folder (ADR 0015). Every
   dropped candidate is logged with the reason.
4. The selected installation (user choice, else first found) goes to the pages.
5. **Quick check** (background, contract 2.5): manifest and `install.ini` read; existence of every listed
   file and the hashes of the `code` files. Result: OK, Modified, Incomplete, Damaged or Unknown.
6. **Defaults first run** per game (contract 3.6), if the marker is missing, **only if the installation is
   unambiguous** for that game settings key (user choice, or the only installation found that uses the key;
   [ADR 0015](adr/0015-game-settings-target-folders-and-write-timing.md)) and the mutation guard allows it
   ([ADR 0016](adr/0016-mutation-guard-and-effective-game-paths.md)): P and GPU preference created if missing,
   D created if missing; differing D values -> one non-blocking question (info bar) "apply the recommended
   display settings?" (Yes: `.reg` backup, then overwrite); marker written. **Class S is never written at
   start**; otherwise the first run waits for the first Play of that game. No defaults and no reset for an
   installation whose `ContractVersion` is higher than the launcher knows (contract 5).
7. **Consistency checks** (contract 3.6) -> listed on the Game settings page with the offer to reset; on the
   Play page a non-modal info bar that can be hidden per value and content (ADR 0015).
8. Online player list polling starts (existing behaviour, now `PlayerListPoller`).
9. The setup-mutex watcher starts (every 2 s while the launcher runs).

### 4.2 Play

Click Play -> button disabled -> `GameStarter.StartAsync(installation, game)`:

1. Setup mutex `EE_Setup` or `NeoEE_Setup` exists -> refused, "a setup is running".
2. Game mutex of that game exists -> refused, "already running" (forum table 8 #14), with the program name and,
   if a process of that name exists, the hint that it may hang and how to end it in the Task Manager (the
   launcher never kills a process). The other game running -> warning with "start anyway".
3. Program file missing -> refused, "damaged" with the repair advice.
4. Class S values synchronized for the game started, computed from the **real game folder** (ADR 0015);
   written only if different after normalization; changed values are logged with old and new value. Then the
   first run of the defaults if the marker is missing.
5. `ShellExecute` of the program with the game folder as working folder (contract 3.7: compatibility
   layers and a chosen elevation apply).
6. Logged: installation, game, program, process id (`pid unknown` if `Process.Start` returns none). Findings
   of the quick check never block (contract 2.5).

### 4.3 Setup finished

The watcher sees the setup mutex disappear -> discovery and quick check run again -> pages update. A game
start is refused while the mutex exists.

### 4.4 Reset game settings

Mutation guard (no setup, no game of that installation running) -> confirmation -> `.reg` backup of the game
settings key with subkeys into `%LOCALAPPDATA%\Empire Earth Launcher\Backups\` (date, time, product, game in
the name), with a delete line for every value the reset will create, so that importing the file restores the
previous values exactly (ADR 0007 amendment) -> if the backup fails nothing is changed -> S, D, P and GPU
preference overwritten (a value of another type is deleted first) -> marker written -> result with the backup
path. Values outside the contract table are never touched. Refused with "update the launcher" when the
installation's `ContractVersion` is higher than the launcher knows (contract 5).

### 4.5 Repair hand-off

Damaged/Incomplete/Unknown (`community`) or the user asks -> `SetupDownloadLocator`: with an AppId
`GET https://api.empireearth.eu/setup/?product=<AppId>` (HTTPS, certificate validation, timeout, no
redirects) -> trimmed body accepted only if `UpdateUrlPolicy` allows it -> otherwise
`https://empireearth.eu/download`. The advice dialog shows the steps of contract 4.4 (close the game, same
folder, same mode, keep "Register NeoEE CDKeys", antivirus exception first, foreign installations are not
repaired) and opens the URL in the default browser, not elevated. The launcher never downloads or starts
the setup.

### 4.6 Tools

- **Registry cleanup** (R5): scan of an explicit list of keys of old and foreign installations; each key
  shown with why it looks stale; the user selects; mutation guard; `.reg` backup, then delete. HKLM and
  InstallShield leftovers are only listed (read-only) with advice; protected keys can never be selected, also
  not through an alias (`WOW6432Node`, registry VirtualStore; ADR 0007 amendment). Starting list (kept
  current by the cleanup work package; every entry needs evidence, unproven entries are not added but noted
  in the test plan):

  | Key | Hive | Evidence | Offered when | Otherwise |
  |---|---|---|---|---|
  | `Software\SSSI\Empire Earth` | HKCU | p=4756 (SSSI keys), p=49553 | no EE installation found, and its "Installed From" folder is missing on a present, fixed, local drive | not shown |
  | `Software\Mad Doc Software\EE-AOC` | HKCU | p=4756 (Mad Doc keys) | as above, for AoC | not shown |
  | `Software\Neo\Empire Earth`, `Software\Neo\Art of Conquest` | HKCU | setup: `config_neoee.iss` (game settings keys of NeoEE) | as above, no NeoEE installation found | not shown |
  | `Software\Classes\VirtualStore\MACHINE\SOFTWARE\[WOW6432Node\]SSSI\Empire Earth`, `...\Mad Doc Software\EE-AOC` | HKCU | p=49553 (the HKLM keys; these are their per-user virtualized copies) | no installation of that game found | shown read-only |
  | `...\VirtualStore\MACHINE\SOFTWARE\[WOW6432Node\]Sierra\...` | HKCU | p=4756, p=49553 | never (`Sierra\CDKeys` and its ancestors are protected) | shown read-only, CD keys only as "exists" |
  | `Software\Sierra`, `Software\SSSI\Empire Earth`, `Software\Mad Doc Software` | HKLM64, HKLM32 | p=49553 | never (HKLM) | shown read-only with the advice to remove them with the Registry Editor as administrator, never `Sierra\CDKeys` |
  | Stainless Steel Studios keys | HKCU, HKLM | p=4756 names only the vendor, no path | not until a sample confirms the path (test plan) | not shown |

  An empty HKCU part on a computer is a valid result ("nothing to clean").
- **WON login reset** (R6): `_wonkver.pub` and `_wonlogin.ks` of the EE and AoC folders and of their
  VirtualStore copies (effective paths, ADR 0016) are moved into a dated backup folder (forum p=83519); files
  listed in the manifest are never touched; mutation guard. The UI says that the backup folder contains login
  data; if the online login still fails afterwards, the hint points to the repair with the setup.
- **VirtualStore** (R8): for installations below `Program Files`, `Program Files (x86)`, `ProgramData` or
  the Windows folder, `%LOCALAPPDATA%\VirtualStore\<path without drive>` is listed; manifest files shadowed
  there are reported as serious (the game uses the virtual copy), runtime files as information. Lobby
  profiles, saves and the WON reset use the effective file (`EffectivePathResolver`, VirtualStore copy first),
  as the game does (ADR 0016).
- **Saved games and scenarios** (R10): export of `.ees` (saves, `Data\Saved Games`) and `.scn` (scenarios,
  `Data\Scenarios`) files of EE or AoC into a zip or a folder, from both the game folder and its VirtualStore
  copy (the VirtualStore copy wins a name conflict, the other is listed); import of such files or zips with
  checks (allowed extensions, plain file names, no path in zip entries, size limit, no overwrite without
  confirmation) into the folder the game reads (ADR 0016), behind the mutation guard; warning for player and
  profile names with characters outside printable ASCII (t=3563 p=23879, t=2126 p=14281) and the hint that
  the host needs ports 33334 to 33336 (forum 4.9).
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
  stores its chosen network adapter (t=32479) is unknown; finding it is a test-plan task.
- **Diagnostics report**: one text with launcher version, Windows version, installations, file versions of
  the game programs, integrity state and findings, defaults and consistency state, VirtualStore, network
  results; copied to the clipboard or saved, never sent anywhere. It never contains CD-key values or the
  contents of the backup folder, and replaces the user's profile paths with `%USERPROFILE%` and
  `%LOCALAPPDATA%` (it is usually posted in forums).

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
  dispose, whole-exchange timeout of `NeoApiClient`); the loop becomes `async` with `Task.Delay`.

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
  launcher writes or deletes with old and new value; every backup file; game starts; HTTP requests with
  URL, status and duration (no bodies beyond the trimmed answer); fallbacks.
- **Never logged**: values below `Software\Sierra\CDKeys` (only "exists" / "missing"), contents of
  `_wonlogin.ks` or other WON key files, passwords, anything typed into the lobby.
- Log messages are English (support language of the forum and of the developers); UI texts are
  localized.

## 8. Files and settings

Decided in [ADR 0005](adr/0005-own-settings-file-instead-of-user-config.md):

| What | Where | Format |
|---|---|---|
| user settings: chosen folder, theme, custom theme file, UI language, last game, hidden warnings (value name + value) | `%LOCALAPPDATA%\Empire Earth Launcher\settings.json` | JSON (`DataContractJsonSerializer`), `SchemaVersion`, unknown members kept, written as `.tmp` then replaced; damaged file renamed to `settings.json.damaged` and defaults used; a file that cannot be read or has a higher `SchemaVersion` is never overwritten (ADR 0005 amendment) |
| server settings: NeoEE host, port, timeout, poll interval | `Empire Earth Launcher.exe.config` next to the program | `applicationSettings` (read-only, admin-editable, as today) |
| log | `%LOCALAPPDATA%\Empire Earth Launcher\log.txt` | text |
| backups (`.reg`, moved WON files) | `%LOCALAPPDATA%\Empire Earth Launcher\Backups\<yyyy-MM-dd_HHmmss>_<what>\` | `.reg` (Windows Registry Editor 5.00, UTF-16 LE) and original files |
| exports of saved games | chosen by the user | zip or folder |
| mod creator working data | `%LOCALAPPDATA%\Empire Earth Launcher\...` (existing) | unchanged |

`%LOCALAPPDATA%` falls back to the temporary folder when it is empty (existing fix). The launcher never
writes next to its executable (it may be below `Program Files`).

## 9. Localization

Decided in [ADR 0009](adr/0009-localization-with-resx-en-de-fr.md):

- English is the neutral language (`NeutralResourcesLanguage("en")`); German (`de`) and French (`fr`)
  are complete; every other UI culture falls back to English. Portuguese (Brazil) and Chinese are not
  machine-translated.
- Every UI text is set from code from `Properties/Resources*.resx` of the launcher (including the
  navigation, which today comes from `MainForm.fr.resx`); designer texts are placeholders that are
  overwritten at start. One place per text makes the parity test possible.
- An architecture test reads the `.resx` files of the source tree and checks the **string entries**
  (comments removed, entries with `type` or `mimetype` skipped): the same keys in `en`, `de`, `fr`; no empty
  value; the same `{n}` placeholders. A second test checks that image and file entries exist only in the
  neutral resx. Today `en` and `fr` have the same 30 string keys; the gap is about 50 designer-only texts
  ([ADR 0009](adr/0009-localization-with-resx-en-de-fr.md), corrected evidence).
- German is proof-read by the user in the laptop test; French texts are marked "review open" in
  `docs/TRANSLATING.md` until a French speaker has read them.
- The UI language follows Windows unless the setting chooses one (needs a restart).
- The mod creator keeps English and French (no new texts in v2).
- Contributor guide for new languages: `docs/TRANSLATING.md` (to be written with the first new texts).

## 10. Security and privacy

- **CD keys**: no call of `authtools.dll`; the whole subtree of `Software\Sierra\CDKeys` in HKCU, HKLM64
  and HKLM32 and every ancestor key (`Software`, `Software\Sierra`) are protected by `RegistryWritePolicy`,
  which every write and delete passes after the path was put into its canonical form (`WOW6432Node` and
  `HKCU\Software\Classes\VirtualStore\MACHINE\...` mapped to the HKLM key they stand for), unit-tested with a
  table of every alias and every operation; the diagnostics only say whether the key exists. Repairing CD
  keys = running the setup. The same rule protects the install records and the uninstall keys; an allow-list
  entry can never open a protected key, also not one that names it exactly.
- **No changes while a setup or game runs**: every write goes through the mutation guard
  ([ADR 0016](adr/0016-mutation-guard-and-effective-game-paths.md)).
- **No elevation**: the manifest requests `asInvoker`; the launcher never restarts itself elevated and
  never writes HKLM or other users' hives.
- **Network**: three destinations only - the NeoEE status server (configured, plain TCP, public data,
  every reply validated - existing), `api.empireearth.eu` for the setup URL and the optional update check,
  and DNS lookups in the network diagnostics. HTTPS with certificate validation, TLS 1.2 or newer, never
  `http://`, no redirects, timeouts ([ADR 0008](adr/0008-https-policy-and-update-api.md)); an architecture test
  forbids every certificate-validation override (`ServerCertificateValidationCallback`,
  `ServerCertificateCustomValidationCallback`, `RemoteCertificateValidationCallback`, ...). No telemetry; the
  old "collect diagnostic data" checkbox is removed.
- **Backups contain login data** (moved WON files); the UI and the README say so, the diagnostics report
  never includes them.
- **URLs opened in the browser**: only the fixed download page or a URL that passed `UpdateUrlPolicy` (same
  rules and test cases as the setup's `IsAllowedUpdateUrl`).
- **Files from outside**: imported saves and zips are untrusted (no paths, allowed extensions only, size
  limits); the manifest never makes the launcher open a file outside the install root; mod archives keep
  their existing limits.

## 11. Testing

Decided in [ADR 0012](adr/0012-test-strategy.md):

- **One test program**, `Empire-Earth-Launcher.Tests` (NUnit 3.14 + NUnitLite, console, exit code = failed
  tests), run by the local verify script under Mono and by CI on Windows.
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
  and `Deterministic`; resource parity (strings only); no certificate-validation override in the sources; no
  removed placeholder control name in a designer file; the registry alias table.
- **Only checkable criteria** ([ADR 0012](adr/0012-test-strategy.md) amendment): "does not block" is a test
  that the async method returns an unfinished task with a blocking fake; performance is counted (file opens,
  hashes), not timed; translation quality and layout are test-plan cases.
- **Golden files** for `.reg` output and the diagnostics report.
- **Windows adapters** (registry, mutex, shell execute, display, HTTP) are thin and checked on real Windows
  by the German test plan `docs/TEST-PLAN.de.md`; the UI is tested manually with it. The test plan is created
  in the first work package and every package adds its cases in the same commit.
- Tests never use the network, the real registry, the real `%LOCALAPPDATA%` or UI.

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
  plus a `.sha256` file) is made in the scratch folder, never committed.
- **CI** (`windows-latest`): NuGet restore, MSBuild Release against
  `Microsoft.NETFramework.ReferenceAssemblies.net48` 1.0.3 (independent of the image's targeting packs),
  all `*Tests.exe`, then the test builds of launcher and mod creator as artifacts with `LICENSE`,
  `THIRD-PARTY-NOTICES.md` and `licenses/THIRD-PARTY-LICENSES.txt`.
- Runtime requirement: .NET Framework 4.8 (built into Windows 10 1903 and later and Windows 11; an
  installer for Windows 7 SP1, 8.1 and older 10; **not available for Windows 8.0**). `App.config`:
  `<supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" />`.

## 13. Requirements map

| Req. | What | Core modules | Contract | Work package |
|---|---|---|---|---|
| D4 | .NET 4.8, core + thin UI, tests | all | | L-WP1, L-WP2 |
| D5 | shared contract, launcher side | Installations, Integrity, GameSettings, Repair, Play | 1 to 5 | L-WP4 to L-WP7 |
| R1 | per-user defaults on first run | GameSettings | 3 | L-WP5 |
| R2 | integrity manifest check | Integrity | 2 | L-WP7 |
| R3 | play EE/AoC, running instances, log, compatibility options | Play, GameSettings | 3.7, 4.2 | L-WP5, L-WP6 |
| R4 | reset with `.reg` backup | GameSettings, Backup | 3.6 | L-WP5 |
| R5 | safe registry cleanup | Maintenance, Backup | 3.8 | L-WP8 |
| R6 | WON login reset | Maintenance, Backup | | L-WP8 |
| R7 | network diagnostics | Diagnostics | | L-WP9 |
| R8 | VirtualStore detection | Installations, Maintenance | | L-WP4 (effective paths), L-WP8 |
| R9 | repair hand-off, pending setup | Repair, Play | 4 | L-WP6, L-WP7 |
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

- the game's own network adapter setting (where EE stores it, t=32479) and the format of `upnp_info.txt` -
  the diagnostics show "unknown" until a sample confirms the format;
- the stale keys of retail, GOG and old patch installations for the cleanup: the starting table in 4.6 holds
  only entries with evidence (forum t=1036 p=4756, t=12082 p=49553 name Sierra, SSSI, Mad Doc, Stainless Steel
  Studios); further entries need a sample from a real computer;
- Krypton's net48 build on Windows 7 SP1 and on scaled screens.

Proposed clarifications of the contract text, to be made in both repositories at once at the next contract
change (contract 5); the launcher already implements the stated reading:

- **1.4 source 4 order**: "per key HKCU, then HKLM32, then HKLM64; `Software\Neo\Empire Earth` before
  `Software\SSSI\Empire Earth`" (key before hive), like sources 2 and 3.
- **3.3 upper-casing**: "ASCII letters only" would make setup and launcher byte-identical; until then the
  launcher keeps `ToUpperInvariant` and compares case-insensitively (ADR 0015).
- **1.4 EE folder of foreign installations**: say explicitly that the EE and AoC folders of `foreign`
  installations are the real folders named by "Installed From" (ADR 0015).

Platform: Windows 8.0 is not supported (.NET 4.8); Windows 7 SP1 is supported but not tested on the laptop
(optional VM case in the test plan; TLS cipher suites of Windows 7 against `api.empireearth.eu` unknown, the
fallback page always works).

## 15. Work packages

Ordered; every package ends with `verify_launcher.sh` green, its CHANGELOG/README/docs and its test-plan
cases in the same package, and no visible control without function. **MVP for the laptop test: L-WP1 to
L-WP6** (start the game safely with per-user defaults). L-WP7 to L-WP9 each name a part that can be dropped
if time runs out (documented in the CHANGELOG): the update check in L-WP7, the zip import and the registry
VirtualStore display in L-WP8, the `upnp_info.txt` parser and the outage hint in L-WP9. The laptop package can
be built after every package from L-WP6 on.

| No. | Package | Main content |
|---|---|---|
| L-WP1 | Toolchain .NET 4.8, local Release build, test plan skeleton | retarget all projects, app.manifest, CI on net48, local Release build like CI, `docs/TEST-PLAN.de.md` skeleton, README platforms, CHANGELOG |
| L-WP2 | Core foundation | core project, contract names, platform interfaces and fakes (32-bit mode), `WinPath`, registry path canonical form and protected-key policy, settings.json, logging, `UiOperation`, mutation guard |
| L-WP3 | UI clean-up and localization | remove the placeholder controls first (ADR 0014 list, architecture test), `ApplyTexts()`, German and French complete, string parity test, language setting, `TRANSLATING.md` |
| L-WP4 | Installation discovery (contract 1) | five sources, key before hive, real EE/AoC folders, merge, kinds, damaged installations, selection, shared-key hint, effective paths for lobby profiles |
| L-WP5 | Game settings (contract 3) | value table, S from the real folder, first run only when unambiguous, reset with exact `.reg` backup, consistency warnings that can be hidden, compatibility options (HKLM version layer respected), write policy allow-list |
| L-WP6 | Play (contract 3.7, 4.2) | shell execute, mutex order, setup watcher, hanging-process hint, file versions, async player list, single instance |
| L-WP7 | Integrity and repair hand-off (contract 2, 4) | manifest, classes, quick/full check, badge, HTTPS client, URL policy, download locator, repair advice, quick-check hook of the setup watcher |
| L-WP8 | Maintenance tools | registry cleanup by the table of 4.6, WON login reset, VirtualStore, saves and scenarios, name checks, all behind the mutation guard |
| L-WP9 | Network diagnostics, report, laptop package | adapters, DNS, `NeoEE.cfg`, `upnp_info.txt`, `CDKeyCheck`, outage hint, anonymized report, final docs, test plan completeness, Release zip with SHA-256 |

Done so far: L-WP1 and L-WP2 (see the CHANGELOG). Implementation details of L-WP2 that refine ADR 0004, 0005, 0007
and 0016 are recorded in their amendments.

## 16. Not in v2

Kept as planned features in the README, with the reason (forum report section 8):

- **DirectX wrapper switch** (row 4): means adding or removing DLLs in the game folders; the launcher must
  not change game files (contract 2.5). The setup's custom installation switches the wrapper.
- **Resolution chooser with 4:3 hint** (row 6): the game has its own option; v2 offers the recommended display
  values and the warning below 768 pixels.
- **GPU driver version** (row 4): little support value for WMI or HKLM class-key reading; the report names the
  display adapter.
- **Ending a hanging game process** (row 14): the launcher explains and points to the Task Manager, it never
  kills a process (ADR 0010).
- **Checking ports from outside** (row 8): needs server support; v2 shows the forwarding table only.
- **Mods page, Discord presence, HD textures, dreXmod, skip intro, game font, ranking, file association**:
  placeholders without function (ADR 0014).
