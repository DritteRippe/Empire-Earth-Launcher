# Launcher v2 architecture

Target architecture of Empire Earth Launcher v2: what it consists of, how data flows through it, and the
rules every change has to follow. The decisions behind it are recorded as ADRs in [adr/](adr/README.md);
what the launcher may read and change on a computer is specified in [CONTRACT.md](CONTRACT.md), the
contract shared with the Empire Earth Setup.

| | |
|---|---|
| Status | **Target**: describes v2 as it is being built on branch `v2`; the README describes what exists today |
| Based on | branch `v2` at `2dc6c43` (refactor/quality-fixes plus the contract), contract version 1 (draft) |
| Scope | the launcher, its UI-free core library, the WON library, the mod library and mod creator, the tests, the build |

Contents: [1. Goals and constraints](#1-goals-and-constraints) · [2. Module map](#2-module-map) ·
[3. Dependency rules](#3-dependency-rules) · [4. Data flows](#4-data-flows) ·
[5. Threading](#5-threading) · [6. Error handling](#6-error-handling) · [7. Logging](#7-logging) ·
[8. Files and settings](#8-files-and-settings) · [9. Localization](#9-localization) ·
[10. Security and privacy](#10-security-and-privacy) · [11. Testing](#11-testing) ·
[12. Build and CI](#12-build-and-ci) · [13. Requirements map](#13-requirements-map) ·
[14. Open points](#14-open-points)

## 1. Goals and constraints

The launcher is the player's tool for an installed Empire Earth (EE) or NeoEE: start the game, keep its
per-user settings sane, recognize a damaged installation and send the player to the setup for the repair,
and explain the problems the save-ee.com support forum saw again and again (network, antivirus,
VirtualStore, old registry entries, saved games).

Fixed constraints (decided before this document, see the v2 briefing):

- **.NET Framework 4.8**, WinForms with Krypton.Toolkit 5.550, C# `LangVersion` 8.0, no API beyond .NET 4.8
  ([ADR 0001](adr/0001-target-dotnet-framework-4-8.md)). Same Windows range as the setup: Windows 7 SP1 to 11.
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
│  │                   IClock, IHttpsClient (ADR 0008)
│  ├─ Logging/         ILogger, LogLevel, TraceFileLogger, log trimming (moved from the launcher)
│  ├─ Settings/        LauncherSettings, SettingsStore (settings.json, ADR 0005), LauncherPaths
│  ├─ Installations/   install record, install.ini, uninstall keys, "Installed From", launcher folder,
│  │                   user choice -> InstallationDiscovery -> Installation (contract 1)
│  ├─ Integrity/       ManifestReader, FileClassifier, IntegrityChecker (quick/full), IntegrityReport
│  │                   (contract 2)
│  ├─ GameSettings/    default value table, computed values, defaults marker, DefaultsService (first run,
│  │                   before start, reset), consistency checks, GPU preference, compatibility layers
│  │                   (contract 3)
│  ├─ Backup/          RegFileWriter (.reg export), FileBackup (move files into a dated backup folder),
│  │                   backup locations (ADR 0007)
│  ├─ Play/            GameStarter, running-game and running-setup detection (contract 4.2, 3.7,
│  │                   ADR 0010)
│  ├─ Repair/          UpdateUrlPolicy (port of the setup's IsAllowedUpdateUrl), SetupDownloadLocator,
│  │                   UpdateChecker, RepairAdvice (contract 4)
│  ├─ Maintenance/     RegistryCleanup, WonLoginReset, VirtualStoreScanner, SavedGames (export, import,
│  │                   name checks)
│  ├─ Diagnostics/     NetworkDiagnostics (adapters, DNS, NeoEE status, upnp_info.txt), DiagnosticsReport
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
| **Play** | selected installation (product, folder, kind, integrity state), choice EE / AoC (AoC only if installed), Play, "setup is running" and "game is running" states, lobby profiles and online player list (existing) | R2, R3 |
| **Game settings** | defaults state, consistency warnings, apply recommended display settings, reset (with backup), compatibility options (HKCU only, HKLM read-only), screen warning below 768 pixels | R1, R4 |
| **Tools** | integrity details and full check, repair advice, registry cleanup, WON login reset, VirtualStore check, saved games and scenarios, network diagnostics, "copy diagnostics report", open backup folder | R2, R5 to R10 |
| **Launcher** | installations found and the user's choice, theme, language (system, English, German, French) | R1, R17 |

The placeholders of the old designer (dreXmod, Discord presence, HD textures, skip intro, game font,
voices/lobby language, ranking, mods page, file association, "when closing the game") are removed from the
UI until they are implemented ([ADR 0014](adr/0014-only-working-features-in-the-ui.md)); the README
keeps them as planned features.

## 3. Dependency rules

```
Empire Earth Launcher.exe ──> Empire_Earth_Launcher_Core.dll ──> Empire_Earth_WON.dll
          │                                │
          └──> Krypton.Toolkit             └──> BCL only (System, System.Core, System.Net.Http,
                                                System.Runtime.Serialization, System.IO.Compression)
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
  folders and the WON login files it moves into a backup.

## 4. Data flows

### 4.1 Start-up

1. `Program.Main`: global exception handlers, logger (trimmed `log.txt`), settings (`settings.json`,
   damaged file moved aside), UI culture (setting or Windows), theme, single-instance check
   ([ADR 0010](adr/0010-game-start-and-mutex-probing.md)).
2. MainForm opens at once; the pages show "searching" states. Nothing blocks the window.
3. **Discovery** (background, contract 1.4): user choice, registry records (NeoEE before EE; HKCU, HKLM64,
   HKLM32), uninstall keys by `Publisher`, "Installed From" values, launcher folder. Candidates are merged
   by normalized root (`WinPath`), classified (`community`, `community-legacy`, `foreign`), damaged ones
   kept. Every dropped candidate is logged with the reason.
4. The selected installation (user choice, else first found) goes to the pages.
5. **Quick check** (background, contract 2.5): manifest and `install.ini` read; existence of every listed
   file and the hashes of the `code` files. Result: OK, Modified, Incomplete, Damaged or Unknown.
6. **Defaults first run** per game (contract 3.6), if the marker is missing: class S, P and GPU preference
   created if missing, D created if missing; differing D values -> one non-blocking question (info bar)
   "apply the recommended display settings?" (Yes: `.reg` backup, then overwrite); marker written.
7. **Consistency checks** (contract 3.6) -> warnings with the offer to reset.
8. Online player list polling starts (existing behaviour, now `PlayerListPoller`).
9. The setup-mutex watcher starts (every 2 s while the launcher runs).

### 4.2 Play

Click Play -> button disabled -> `GameStarter.StartAsync(installation, game)`:

1. Setup mutex `EE_Setup` or `NeoEE_Setup` exists -> refused, "a setup is running".
2. Game mutex of that game exists -> refused, "already running" (forum table 8 #14).
3. Program file missing -> refused, "damaged" with the repair advice.
4. Class S values synchronized for the game started; changed values are logged with old and new value.
5. `ShellExecute` of the program with the game folder as working folder (contract 3.7: compatibility
   layers and a chosen elevation apply).
6. Logged: installation, game, program, process id. Findings of the quick check never block (contract 2.5).

### 4.3 Setup finished

The watcher sees the setup mutex disappear -> discovery and quick check run again -> pages update. A game
start is refused while the mutex exists.

### 4.4 Reset game settings

Confirmation -> `.reg` backup of the game settings key with subkeys into
`%LOCALAPPDATA%\Empire Earth Launcher\Backups\` (date, time, product, game in the name) -> if the backup
fails nothing is changed -> S, D, P and GPU preference overwritten (a value of another type is deleted first)
-> marker written -> result with the backup path. Values outside the contract table are never touched.

### 4.5 Repair hand-off

Damaged/Incomplete/Unknown (`community`) or the user asks -> `SetupDownloadLocator`: with an AppId
`GET https://api.empireearth.eu/setup/?product=<AppId>` (HTTPS, certificate validation, timeout, no
redirects) -> trimmed body accepted only if `UpdateUrlPolicy` allows it -> otherwise
`https://empireearth.eu/download`. The advice dialog shows the steps of contract 4.4 (close the game, same
folder, same mode, keep "Register NeoEE CDKeys", antivirus exception first, foreign installations are not
repaired) and opens the URL in the default browser, not elevated. The launcher never downloads or starts
the setup.

### 4.6 Tools

- **Registry cleanup** (R5): scan of an explicit list of HKCU keys of old and foreign installations; each
  key shown with why it looks stale; the user selects; `.reg` backup, then delete. HKLM and InstallShield
  leftovers are only listed (read-only) with advice; protected keys can never be selected.
- **WON login reset** (R6): `_wonkver.pub` and `_wonlogin.ks` of the EE and AoC folders and of their
  VirtualStore copies are moved into a dated backup folder (forum p=83519); files listed in the manifest are
  never touched.
- **VirtualStore** (R8): for installations below `Program Files`, `Program Files (x86)`, `ProgramData` or
  the Windows folder, `%LOCALAPPDATA%\VirtualStore\<path without drive>` is listed; manifest files shadowed
  there are reported as serious (the game uses the virtual copy), runtime files as information. The lobby
  profile reader uses the effective file (VirtualStore copy first), as the game does.
- **Saved games and scenarios** (R10): export of `.ees` (saves, `Data\Saved Games`) and `.scn` (scenarios,
  `Data\Scenarios`) files of EE or AoC into a zip or a folder; import of such files or zips with checks (allowed
  extensions, plain file names, no path in zip entries, size limit, no overwrite without confirmation);
  warning for player and profile names with characters outside printable ASCII (t=3563 p=23879,
  t=2126 p=14281) and the hint that the host needs ports 33334 to 33336 (forum 4.9).
- **Network diagnostics** (R7, on request only): local adapters with IPv4 and gateway, virtual/VPN
  adapters flagged (forum 4.10), DNS resolution and status request of the NeoEE server, evaluation of
  `upnp_info.txt` when it exists, hints for private/CGNAT external addresses (100.64.0.0/10) and IPv6-only
  (DS-Lite) connections, the port forwarding table (33334 and 33336 TCP+UDP, 33335 TCP) with the current
  local IPv4. No external "what is my IP" service.
- **Diagnostics report**: one text with launcher version, Windows version, installations, integrity state
  and findings, defaults and consistency state, VirtualStore, network results; copied to the clipboard or
  saved, never sent anywhere. It never contains CD-key values.

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
| user settings: chosen folder, theme, custom theme file, UI language, last game | `%LOCALAPPDATA%\Empire Earth Launcher\settings.json` | JSON (`DataContractJsonSerializer`), `SchemaVersion`, unknown members kept, written as `.tmp` then replaced; damaged file renamed to `settings.json.damaged` and defaults used |
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
- An architecture test reads the `.resx` files of the source tree and checks: the same keys in `en`, `de`,
  `fr`; no empty value; the same `{n}` placeholders.
- The UI language follows Windows unless the setting chooses one (needs a restart).
- The mod creator keeps English and French (no new texts in v2).
- Contributor guide for new languages: `docs/TRANSLATING.md` (to be written with the first new texts).

## 10. Security and privacy

- **CD keys**: no call of `authtools.dll`; `Software\Sierra\CDKeys` in HKCU, HKLM64 and HKLM32 and every
  ancestor key (`Software`, `Software\Sierra`) are protected by a guard in `RegistryCleanup` that is
  unit-tested; the diagnostics only say whether the key exists. Repairing CD keys = running the setup.
- **No elevation**: the manifest requests `asInvoker`; the launcher never restarts itself elevated and
  never writes HKLM or other users' hives.
- **Network**: three destinations only - the NeoEE status server (configured, plain TCP, public data,
  every reply validated - existing), `api.empireearth.eu` for the setup URL and the optional update check,
  and DNS lookups in the network diagnostics. HTTPS with certificate validation, TLS 1.2 or newer, never
  `http://`, no redirects, timeouts ([ADR 0008](adr/0008-https-policy-and-update-api.md)). No telemetry.
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
  merging and two products in one root; manifest BOM/CRLF/LF/uppercase/binary marker/invalid lines/paths
  outside the root; file classes equal to the contract table; every defaults value and computed value;
  marker semantics; reset with failing backup; URL policy with every case of the setup's unit tests.
- **Architecture tests**: core references no UI assembly; every project targets v4.8 with `LangVersion` 8.0
  and `Deterministic`; resource parity; no `ServerCertificateValidationCallback` assignment in the sources.
- **Golden files** for `.reg` output and the diagnostics report.
- **Windows adapters** (registry, mutex, shell execute, display, HTTP) are thin and checked on real Windows
  by the German test plan `docs/TEST-PLAN.de.md`; the UI is tested manually with it.
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
- **CI** (`windows-latest`): NuGet restore, MSBuild Release against
  `Microsoft.NETFramework.ReferenceAssemblies.net48` 1.0.3 (independent of the image's targeting packs),
  all `*Tests.exe`, then the test builds of launcher and mod creator as artifacts with `LICENSE`,
  `THIRD-PARTY-NOTICES.md` and `licenses/THIRD-PARTY-LICENSES.txt`.
- Runtime requirement: .NET Framework 4.8 (built into Windows 10 1903 and later and Windows 11; an
  installer for Windows 7 SP1, 8.1 and older 10). `App.config`:
  `<supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" />`.

## 13. Requirements map

| Req. | What | Core modules | Contract | Work package |
|---|---|---|---|---|
| D4 | .NET 4.8, core + thin UI, tests | all | | L-WP1, L-WP2 |
| D5 | shared contract, launcher side | Installations, Integrity, GameSettings, Repair, Play | 1 to 5 | L-WP3 to L-WP6 |
| R1 | per-user defaults on first run | GameSettings | 3 | L-WP4 |
| R2 | integrity manifest check | Integrity | 2 | L-WP5 |
| R3 | play EE/AoC, running instances, log, compatibility options | Play, GameSettings | 3.7, 4.2 | L-WP4, L-WP6 |
| R4 | reset with `.reg` backup | GameSettings, Backup | 3.6 | L-WP4 |
| R5 | safe registry cleanup | Maintenance, Backup | 3.8 | L-WP7 |
| R6 | WON login reset | Maintenance, Backup | | L-WP7 |
| R7 | network diagnostics | Diagnostics | | L-WP8 |
| R8 | VirtualStore detection | Maintenance | | L-WP7 |
| R9 | repair hand-off, pending setup | Repair, Play | 4 | L-WP5, L-WP6 |
| R10 | saves/scenarios export and import, name checks | Maintenance | | L-WP7 |
| R17 | en/de/fr | UI resources | | L-WP2 and every later package |
| R18 | docs, ADRs, README/CHANGELOG, test plan | | | this commit, every package, L-WP8 |

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

- the game's own network adapter setting (where EE stores it) and the format of `upnp_info.txt` - the
  diagnostics show "unknown" until a sample confirms the format;
- the exact list of stale HKCU keys of retail, GOG and old patch installations for the cleanup (forum
  t=1036 p=4756, t=12082 p=49553 name Sierra, SSSI, Mad Doc, Stainless Steel Studios); every entry needs
  evidence;
- Krypton's net48 build on Windows 7 SP1 and on scaled screens.
