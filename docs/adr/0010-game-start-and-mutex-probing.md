# 0010 Game start, mutex probing and single instance

Status: **Accepted** (2026-10-02), amended 2026-10-02 (design review; implementation in L-WP6), 2026-10-05 (launcher
1.0.0, the suite of contract revision 4) and 2026-10-06 (launcher 1.1.0, the foreground goes to the game), see the
Amendment sections

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

## Amendment 2026-10-05 (launcher 1.0.0, contract revision 4)

The suite installer "Empire Earth Community" (setup decision record 0013) runs the EE and NeoEE setups one after the other
and installs the launcher. Contract revision 4 adds optional parts that touch this decision:

- **The suite mutex is a setup mutex** (contract 4.2): `EmpireEarthCommunity_Suite`, held by the suite and its uninstaller for
  their whole run, also between the two product setups, where neither `EE_Setup` nor `NeoEE_Setup` exists. The running setup
  is therefore not a `Product` any more but a `SetupKind` (NeoEE, EE, Suite; `Core/Contract/SetupKind`), looked at in that
  order by `RunningGameDetector.FindRunningSetup`, `MutationGuard`, `SetupWatcher`, `GameStarter` and, through the watcher,
  `InstallationService` and the integrity checker. Nothing else changes: no game start, no search, no read of `install.ini`
  and `files.sha256`, no integrity check and no guarded change while any of the three exists; the watcher raises one "setup
  finished" for the whole run of the suite, because the suite mutex bridges the gap. The launcher mutex
  `EmpireEarthCommunityLauncher` stays no setup mutex.
- **Second launcher with `--product`**: the single-instance check (above) stays. A launcher that does not get the mutex and was
  started with `--product=EE|NeoEE` first tries to hand the product to the running one: `InstanceForwarder` sends
  `WM_COPYDATA` (`dwData` "EEL1", UTF-8 `product=EE` or `product=NeoEE`, at most 64 bytes) to the message-only window
  `EmpireEarthCommunityLauncher.<Windows session id>` (`WindowsInstanceChannel`, `AllowSetForegroundWindow` first), five times
  200 ms apart because the running launcher creates the window a moment after the mutex. If that works the second launcher ends
  without a message; if not (no window, an old launcher, an elevated launcher that drops the message of a non-elevated one),
  it shows the usual message. The running launcher (`InstanceMessageWindow`, `InstanceReceiver`, `LauncherInstanceTarget`)
  accepts exactly that text, brings its window to the front and selects the product unless a game start is in progress.
  Rejected: a named pipe or a file in `%LOCALAPPDATA%` (more code and rights questions for the same effect), and starting the
  game from the second launcher (the first one owns the window and the start).
- **Evidence**: `Core/Play/InstanceForwardingTests` (round trip in one process through a fake channel), `LauncherArgumentsTests`,
  `SetupKindTests`, `SetupWatcherTests`, `MutationGuardTests`, `GameStarterTests`, `RunningGameDetectorTests`,
  `Launcher/InstallationServiceTests`, `LauncherInstanceTargetTests`, `SingleInstanceStartupTests`; the window and the foreground
  right are checked on Windows by the test plan (WP10-03, WP10-04).

## Amendment 2026-10-06 (launcher 1.1.0, the foreground goes to the game)

Report 1: after a start from the launcher the mouse is dead in the game until the player minimizes and restores its window.
The probable cause is activation: Empire Earth acquires its DirectInput mouse with the foreground cooperative level while it
creates its window, and the launcher is still the foreground application then, so the game is never activated (a minimize
and restore activates it). The tests T0/T1 on the laptop decide whether this is the cause; the hand-over is harmless if it
is not. The start itself does not change (shell execute, no arguments, no verb):

- **Before the start**: `GameStarter` calls `IWindowSystem.AllowSetForegroundWindow(ASFW_ANY)` right before
  `IProcessStarter.StartProgram` (after class S and the first run), while the launcher still owns the foreground right,
  so that the game may take the foreground when it shows its window. A refusal is logged by the adapter; the start goes on.
- **After the start**: if the start returned a process id, `GameWindowActivator` (core, `Play`) runs on the thread pool
  (started by `PlayModel`, cancelled when the main window closes). It polls every 100 ms, for at most 60 s, for the first
  visible top-level window without an owner of that process (since the amendment of 2026-10-07 below: no tool window).
  If the launcher (or the game) owns the foreground it calls
  `SetForegroundWindow` on it. About 2 s later it looks again (a wrapper such as dgVoodoo switches the display mode
  late, and Windows may give the foreground back to the launcher): while the launcher owns the foreground it hands the
  window over again, at most 3 times; if the game owns it, it stops; if another process owns it (the player switched)
  it stops at once and changes nothing. The launcher never steals the foreground, never minimizes, hides, closes or moves a
  window and never ends a process. Without a process id ("pid unknown") nothing is polled; no window within 60 s is logged.
- **One log line** says what happened, for example `Game window 0x1234 of Empire Earth.exe (pid 4242) brought to the
  foreground after 800 ms (the foreground was pid 100 (the launcher)).`, `... found after 300 ms, not brought to the
  foreground: skipped, user switched to pid 777.` or `... SetForegroundWindow was refused ...`; a repeated hand-over and the
  end (`giving up`) are logged too.
- **Platform**: `Platform.IWindowSystem` (`GetForegroundProcessId`, `FindVisibleTopLevelWindow`, `SetForegroundWindow`,
  `AllowSetForegroundWindow`) with the adapter `WindowsWindowSystem` (`user32.dll`; a missing `user32.dll` under Mono gives
  neutral answers and one warning). `ForegroundRulesTests` keeps the imports of the foreground functions in this adapter,
  in `WindowsInstanceChannel` (the right for the running launcher, 1.0.0) and in `ForegroundWindow` (the launcher's own
  window); `ProcessRulesTests` still finds no code that ends a process.
- **Limits**: a game that runs elevated (a `RUNASADMIN` layer) may refuse the foreground from the non-elevated launcher
  (logged, three more tries, then `giving up`); NeoEE may create its window late or in another process (then the log says
  "no window"). Both are test plan cases (WP6-18); no other way of focusing (`AttachThreadInput`, simulated key presses) is
  used.
- **Evidence**: `Core/Play/GameWindowActivatorTests` (fake window system and clock: the window after N polls, the player
  switched, no window within 60 s, no process id, three hand-overs at most, a background thread, cancellation),
  `GameStarterTests` (the right before the start), `Launcher/PlayModelTests`, `Core/Platform/WindowsWindowSystemTests`,
  `Architecture/ForegroundRulesTests`; test plan WP6-18.

## Amendment 2026-10-07 (launcher 1.1.0, contract revision 6: one launcher for four games)

Suite 1.1.0 creates one icon, `Empire Earth Community`, which starts the launcher without an argument; the player may start it
again while it runs (a second click, the start menu). Contract 1.4 (revision 6) allows the second launcher to ask the running
one to come to the front. Changes to the second-start rules above:

- **Without `--product` the second launcher sends the message `show`** (`InstanceMessage.ShowText`, `Encode(null)`; the
  same `WM_COPYDATA` of at most 64 bytes, `dwData` "EEL1", UTF-8 `show`), and ends without a message when it was delivered.
  The running launcher (`InstanceReceiver.Handle`) brings its window to the front and changes nothing else: no product is
  selected, so the choice of the player and the page shown stay. If it cannot be reached (no window, an old launcher, an
  elevated launcher that drops the message) the second launcher shows the usual "already running" message, as before.
  `--product=EE|NeoEE` is sent and handled as before and also brings the window to the front.
- **No message box on success**: the one icon is meant to be clicked again, and a box for each click would be noise; the front
  window is the answer. A launcher 1.0.0 that receives `show` ignores it (it accepts exactly the two product texts), so the
  second launcher reports "already running" there.
- **A running game**: coming to the front takes the foreground from a game and so minimizes it (README FAQ); the second start is
  not blocked while a game runs, because the message is the same as with `--product` in 1.0.0.

Evidence: `Core/Play/InstanceForwardingTests` (`show` round trip, `TryDecode`, the second launcher without an argument),
`Launcher/LauncherInstanceTargetTests`; test plan WP13-06 and WP10-03.

## Amendment 2026-10-07 (launcher 1.1.0, A1: the main window, nothing to do if the game is in front, a read-only watch)

The assessment of run 5d (the dead mouse with dgVoodoo, laptop matrix) found that the hand-over above is harmless but cannot be
the fix: the direct start of the game without the launcher is just as dead, and `SetForegroundWindow` on a window that is in
front sends no `WM_ACTIVATE` or `WM_ACTIVATEAPP`, so the game's own resume of its DirectInput devices does not run. Four
changes make the hand-over and its log say what happens, and add the measurement that is missing:

- **(a) The main window.** `FindVisibleTopLevelWindow` skips tool windows (`WS_EX_TOOLWINDOW`, `WindowRules.IsMainWindowCandidate`:
  visible, no owner, no tool window). The first visible window of Empire Earth is the splash `Loading Game Window`, a tool window
  that lives until the graphics are initialised; the hand-over and every log line now refer to the window of the class
  `SSSI Empire Earth`.
- **(b) The game is in front already.** Then the launcher logs `... found after <N> ms, already in the foreground, nothing to do.`
  and does not call `SetForegroundWindow`.
- **(c) A watch of 60 seconds, read-only.** After the hand-over `GameWindowActivator` reads, every 250 ms for 60 s, the foreground
  window (`GetForegroundWindow`) and the rectangle and the styles (`GetWindowRect`, `GWL_STYLE`, `GWL_EXSTYLE`) of the main window
  (`IWindowSystem.GetForegroundWindow`, `ReadWindow`, `WindowState`), and logs the first state and every change with the time
  since the start (`Watch t+10.0 s: foreground window changed from ... to ...`, `main window 0x... rectangle changed from ... to
  ...`). It changes nothing: it calls no function that changes a window (`ForegroundRulesTests` still allows the foreground
  imports in the adapter only) and no process is ended. It is a measurement of when a wrapper such as dgVoodoo switches the display
  mode and whether the game loses the foreground; no watch follows when the game showed no window.
- **(d) A1 does not fix the dgVoodoo case.** The hand-over covers another failure: the launcher keeps the foreground, for
  example when the player clicks into it while the game loads. It cannot repair a mouse that a late display-mode switch of a
  wrapper left unacquired. The contingent A1b (a one-time `WM_ACTIVATE` to the main window after a quiet time) is **not** part of
  1.1.0: it needs its own validation on the laptop and its own amendment.
- **A missing foreground window is nobody's.** Windows has no foreground window for a moment while a window is created or the
  display mode switches; a foreground process id of 0 is looked at again three times, 100 ms apart, and counts as nobody's
  (the launcher then hands the window the foreground, as it does when it owns it itself), no longer as "the player switched".
  Another process in front still ends the hand-over at once.

Evidence: `Core/Play/GameWindowActivatorTests` (the splash is skipped, the game in front is left alone, the missing foreground,
the lines of the watch from a fake clock, no change by the watch, cancellation during the watch), `Core/Platform/WindowRulesTests`,
`WindowStateTests`, `WindowsWindowSystemTests`, `Architecture/ForegroundRulesTests`; test plan WP6-18.

## Amendment 2026-10-07 (launcher 1.1.0, A1b: one activation signal to the settled main window)

**Context.** The laptop round of runs 5d and 5e: with every dgVoodoo 2.87.5 setting that keeps the multiplayer lobby and Alt+Tab
working (fake fullscreen, real fullscreen at the panel resolution) the mouse is dead from the start, also during the intro (a
click cannot skip it) and also with a direct start of the game program; Alt+Tab revives it. Only a real display mode change at
the start gave the game a usable activation. Empire Earth acquires its DirectInput devices (exclusive, foreground) while it
starts and re-acquires them only on an activation (`WM_ACTIVATE` that is not `WA_INACTIVE` and not minimized, or
`WM_ACTIVATEAPP(TRUE)`). The statements of the amendment above ("A1 does not fix the dgVoodoo case", "A1b is not part of 1.1.0")
are superseded by this decision of the user.

**Decision.** After the launcher started a game it posts the game's main window **one** `WM_ACTIVATE` (`WA_ACTIVE`) as soon as
that window has been the foreground window with the same rectangle and styles for a short settle time, at most once per start,
never later than 180 seconds after the start.

- **State machine** (`Core/Play/ActivationSignal`, pure: it decides, it calls nothing and has no clock): armed only when the
  hand-over ended with `GameInForeground`; then a look every 250 ms (the interval of the watch) decides in this order: the
  deadline (180 s: `NotSettled`); no main window (the quiet time starts again, 10 s in a row: `WindowGone`); no foreground
  window; a foreground window that is not the main window (the lobby popup, the splash tool window, a dialog of the game,
  another program, the launcher: the quiet time starts again); a minimized or hidden main window; a main window whose handle,
  rectangle or styles differ from the last quiet state (the quiet time starts now); the quiet time of 5 s: `Send`.
- **5 seconds** settle time: dgVoodoo does its window work in one burst (real fullscreen inside `SetCooperativeLevel` and
  `SetDisplayMode`, fake fullscreen at the first presented frame, with a resolution change "when needed"); a display mode
  switch with its monitor resync takes up to 2 to 3 s on laptop panels and over HDMI. Five quiet seconds put the signal after
  that burst with margin, so it is not spent before the last change (a signal is sent once). The game creates its DirectInput
  devices right after its graphics initialization, so five seconds after the window has settled is after that, also from a slow
  disk. And it is short enough for the intro: the logos last 9 s (Sierra) and 8 s (SSSI), so the signal arrives during a logo and
  a click skips the 98 s movie at the latest; 3 s would win two seconds but leave no margin on slow machines.
- **180 seconds** deadline: the user's bound of about three minutes; the longest normal way to the main menu is the window search
  (at most 60 s) plus the intro (115 s). Later the player is in the menu, the lobby or a match, where a synthetic activation (the
  game centres its cursor once) would only disturb. **10 seconds** for a missing window: a window is briefly invisible while a
  wrapper restyles it; ten seconds without it mean the game has ended.
- **Guards.** Only for a game this launcher started (the process id of the start result) and only after the hand-over ended
  with `GameInForeground` (after `UserSwitched` Windows activates the game when the player returns to it, after `GaveUp` the
  player's click does); only if `GetForegroundWindow()` is exactly the main window (never while the lobby popup, the splash tool
  window or another program is in front); at most once (`Complete` throws on a second call and a look that decided never decides
  again); never minimize, hide, move, resize or close a window and never end a process (`WindowMessageRulesTests`,
  `ProcessRulesTests`); every decision is logged (`Activation signal for ...: armed`, `not armed, ...`, `activation signal waits:
  ...` when the reason changes, `sent`, `failed`, `not sent`, `The activation signal for ... was not sent: the launcher is
  closing.`); if `PostMessage` fails (a game that runs as administrator: error 5, User Interface Privilege Isolation) it is a
  warning with the way out (Alt+Tab), without a retry.
- **The platform.** `IWindowSystem.PostActivateMessage` is the only message the launcher posts to a window of another program:
  `PostMessage(window, WM_ACTIVATE, WA_ACTIVE, 0)` to a window that exists. `WindowMessageRulesTests` keeps the imports of the
  functions that post, send, show, move or close a window to the three adapters that have a rule (`PostMessage` in
  `WindowsWindowSystem`, `SendMessageTimeout` in `WindowsInstanceChannel`, `ShowWindow` in `ForegroundWindow`), the one call
  with `WM_ACTIVATE` and `WA_ACTIVE` and no other message constant, and the call only in the activator.
- **The watch stays** (read-only, A1) and now lasts at least 60 s and until the signal is decided, never beyond 180 s after the
  start; its last line says the real length.

**Evidence.** `Core/Play/ActivationSignalTests`, `Core/Play/GameWindowActivatorTests`, `Architecture/WindowMessageRulesTests`,
`Core/Platform/WindowsWindowSystemTests`, `Launcher/PlayModelTests`; test plan WP6-21 and setup test plan TP-25 (to be recorded
on the laptop). The signal itself is a hypothesis on this laptop until WP6-21 ran.

**Consequences.** A start through the launcher (the desktop icon of the suite starts it) gets a live mouse after about five
seconds of a quiet window; closing the launcher earlier cancels the signal; a direct start of the game program still needs one
Alt+Tab (README); a game started as administrator refuses the message (logged, Alt+Tab); the game centres its cursor once on the
signal, as on every real activation. If the mouse stays dead after `activation signal sent`, the watch lines show whether
something changed after the signal; the next step would be `WM_ACTIVATEAPP(TRUE)` as well (an amendment).

**Alternatives considered.** `WM_ACTIVATEAPP(TRUE)` (the user chose `WM_ACTIVATE`; the game's handler of both resumes the devices);
minimize and restore (changes the window: forbidden); `SetForegroundWindow` on a window that is in front (sends nothing);
`AttachThreadInput` or simulated key presses (input faking, forbidden by `ForegroundRulesTests` in spirit); a patched game
program (never).
