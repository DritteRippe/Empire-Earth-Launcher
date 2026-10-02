# 0010 Game start, mutex probing and single instance

Status: **Accepted** (2026-10-02), amended 2026-10-02 (design review; implementation in L-WP6), see the Amendment
sections

## Context

R3 asks the launcher to start EE or AoC, detect running instances and log the start; R9 asks it to detect a
running setup. The setup refuses to install while a game runs (`AppMutex`) and holds `SetupMutex` while it
runs; the contract (4.2) forbids starting a game while a setup runs. Compatibility layers and an elevation
chosen by the user only apply when the program is started through the shell (contract 3.7).

## Decision

- **Start** with `ProcessStartInfo { FileName = <game folder>\<program>, WorkingDirectory = <game folder>,
  UseShellExecute = true }` (`Platform.ShellProcessStarter`). Programs: `Empire Earth.exe` in the EE folder,
  `EE-AOC.exe` in the AoC folder. No arguments.
- **Order before a start**: setup mutex -> game mutex -> program exists -> class S sync (contract 3.6) ->
  start -> log (installation, game, path, process id). A failing start (`Win32Exception`, e.g. cancelled
  elevation 1223) is reported, not thrown.
- **Mutex probing** (`Platform.WindowsMutexProbe`): `Mutex.TryOpenExisting(name, MutexRights.Synchronize)`;
  `true` = exists; `UnauthorizedAccessException` = **exists** (another account or integrity level created it
  without access for us); `false` = does not exist. Names without prefix (the session namespace, as the setup
  and the games create them) and with `Global\` (in case a future setup uses it).
  - Setups: `EE_Setup`, `NeoEE_Setup` -> no game start, "setup is running"; a WinForms timer probes every 2 s
    and runs discovery and the quick check again when the mutex is gone.
  - Games: `StainlessSteelStudiosPresentsEmpireEarth` (EE), `MadDocSoftwarePresentsEmpireEarthExpansion`
    (AoC) -> the same game: "already running", no second start; the other game: warning with "start anyway".
- **Single instance**: the launcher creates the session mutex `EmpireEarthCommunityLauncher` at start; a
  second instance shows a localized message and exits. The name is reserved for a future `AppMutex` of the
  setup (contract O10).

## Evidence

- Setup `setup_is6.iss` lines 300-301: `SetupMutex={#InstallType}_Setup`,
  `AppMutex=StainlessSteelStudiosPresentsEmpireEarth,MadDocSoftwarePresentsEmpireEarthExpansion`.
- Inno Setup source (`Main.pas`, setup start: `CheckForMutexes(ExpandedSetupMutex)`, then
  `CreateMutexes(ExpandedSetupMutex)`; `CmnFunc2.pas` `CreateMutex`: created with a null DACL so that every
  user can open it, name used as given, i.e. in the session namespace).
- CONTRACT.md 4.2 (no game start while a setup mutex exists; rediscovery afterwards), 3.7 (shell execute,
  game folder as working folder, plain `CreateProcess` fails with error 740 for an elevation layer).
- Forum report table 8 row 14 (hanging `Empire Earth.exe`, t=2815, t=5859).

## Consequences

- The launcher never kills a process; it explains and lets the user act.
- Tests use `FakeMutexProbe` and `FakeProcessStarter`; the real probe is checked on Windows (test plan:
  setup running, game running, game started elevated by layer).

## Alternatives considered

- **`Process.GetProcessesByName`** for running games: misses renamed copies, finds unrelated programs with the
  same name, and needs no mutex knowledge; the mutexes are what the setup itself uses. Kept only as extra
  information in the diagnostics report.
- **`UseShellExecute = false`**: breaks compatibility layers with `RUNASADMIN` (error 740). Rejected.

## Amendment 2026-10-02 (design review)

- **Running games and setups also block changes, not only Play**: every action that writes game settings,
  layers, the marker or files in the game folders goes through the mutation guard of ADR 0016.
- **Hanging game** (forum report table 8 row 14, t=2815, t=5859): when the game mutex exists, the message
  names the program and, if `Process.GetProcessesByName` finds a process of that name, says that it may hang
  and how to end it in the Task Manager. The launcher still never kills a process.
- **Process id**: `Process.Start` with `UseShellExecute = true` may return `null` (an existing process took
  over the request) or a process whose id is not readable; the start is then logged with `pid unknown` and
  counts as started. `FakeProcessStarter` covers both cases.
- **Version display**: the Play page shows the file version (`FileVersionInfo`) of `Empire Earth.exe` and
  `EE-AOC.exe` of the selected installation (forum report table 8 row 1, version conflicts of forum 4.12); the
  diagnostics report contains it too. No reference list is compared (the integrity manifest does that for
  `community` installations).

## Amendment 2026-10-02 (implementation, L-WP6)

`Play.GameStarter`, `Play.RunningGameDetector`, `Play.SetupWatcher`, `Play.SingleInstance`, `Play.ProgramVersions` and
the adapters `ShellProcessStarter`, `WindowsProcessList`, `WindowsFileVersionReader` and `WindowsMutexOwner` exist.
Details decided while implementing, keeping the decision:

- **The order** is setup mutex (`NeoEE_Setup`, then `EE_Setup`) -> mutex of the game -> mutex of the other game ->
  folder and program -> class S -> first run of the defaults if the marker is missing (contract 3.6) -> start -> log.
  A test with fakes records every step in one journal and compares it; a second test runs it with the real game
  settings and checks that class S is written before the shell is called.
- **Results, not exceptions**: `StartOutcome` is `Started`, `SetupRunning`, `AlreadyRunning` (with `ProcessFound`),
  `OtherGameRunning` (the page asks and starts again with `startEvenIfOtherGameRuns`), `FolderMissing` (a chosen folder
  that is gone), `Damaged` (the program is missing before the start, or Windows reports error 2 or 3: with
  `RepairAdvice`), `BlockedByAntivirus` (225, 226: with `RepairAdvice`), `ElevationCancelled` (1223, logged as
  information), `AccessDenied` (5, 1260) and `Failed` (any other error, with its number and message). Only programming
  errors throw (no installation, The Art of Conquest of an installation without it).
- **The game settings never stop a start.** If the player starts while the other game runs, the mutation guard blocks
  class S and the first run (the running game may write the shared settings on exit, ADR 0016); the start goes on and
  the log says that "Installed From" was not synchronized. A display question of the first run joins the info bar.
- **Start information**: `ShellProcessStarter.CreateProgramStartInfo` sets `UseShellExecute = true`, the real game folder
  as working folder, no arguments, an empty verb and no shell error dialog; `CreateUrlStartInfo` accepts only absolute
  `https` URLs. Both are unit-tested; the start itself is a test plan case (WP6-01, WP6-09). `ProcessRulesTests` keeps
  `Process.Start` in this one class, without `UseShellExecute = false`, without "runas", and finds no code that ends a
  process.
- **Process id**: a start without a process object, or with an unreadable id, is logged as `pid unknown` and counts as
  started.
- **Hanging game**: `IProcessList.IsRunning` counts the processes of the program name (`Empire Earth.exe` ->
  `Empire Earth`) and disposes them at once; a failure to list them is logged and means "no process".
- **File versions** are the four numbers of the version resource (what Explorer shows as "File version"), not the
  version string; 0.0.0.0 means "no version information".
- **Setup watcher**: probes when two seconds have passed by its clock; its events "setup started" and "setup finished"
  are the hooks. `InstallationService` registers "finished" and searches again; while a setup runs a refresh only waits
  and keeps the previous result, so `install.ini` is not read then (contract 4.2). The pages disable Play and every
  guarded change and say why.
- **Single instance**: the mutex is created through `IMutexOwner` (`new Mutex(false, name, out createdNew)`, never
  acquired); `UnauthorizedAccessException` means another launcher (an elevated one of the same session). Any other
  error starts the launcher anyway (logged): two windows are better than none. The check runs after the UI language
  is applied and before any window or service exists; `Program.ClaimSingleInstance` is tested with `FakeMutexProbe`.
- **UI**: the refusals and errors are Windows message boxes (their text wraps in every language); the repair advice is
  `RepairAdviceDialog`, built in code with labels that wrap at a fixed width, and opens the page through
  `PlayModel.OpenDownloadPage` without closing (the steps stay readable).

Evidence: `Core/Play/GameStarterTests`, `RunningGameDetectorTests`, `SetupWatcherTests`, `SingleInstanceTests`,
`ProgramVersionsTests`, `Core/Platform/ShellProcessStarterTests`, `WindowsMutexOwnerTests`,
`WindowsFileVersionReaderTests`, `WindowsProcessListTests`, `Launcher/PlayModelTests`, `SingleInstanceStartupTests`,
`InstallationServiceTests` (a running setup), `Architecture/ProcessRulesTests`; test plan WP6-01 to WP6-15.
