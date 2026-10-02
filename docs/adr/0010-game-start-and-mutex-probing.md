# 0010 Game start, mutex probing and single instance

Status: **Accepted** (2026-10-02), amended 2026-10-02 (design review, see the Amendment section)

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
