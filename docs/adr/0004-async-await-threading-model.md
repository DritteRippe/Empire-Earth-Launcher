# 0004 async/await threading model

Status: **Accepted** (2026-10-02), amended 2026-10-02 (implementation in L-WP2, L-WP4, L-WP6 and L-WP7, see the
Amendment sections)

## Context

The launcher has one background activity today: a `BackgroundWorker` in `GeneralUserControl` that polls the
online player list (started in `OnLoad`, cancelled on dispose, one log line per outage; review findings
korr-S5, wart-S5 fixed I/O in the constructor and a missing connect timeout). v2 adds long operations:
hashing an installation (a full check reads every game file), HTTP requests to the update API, network
diagnostics, discovery that may touch slow or network drives. The UI must stay responsive, and a closed
window must not receive results.

## Decision

- **One UI thread** (STA, `WindowsFormsSynchronizationContext`); controls and the theme service are used only
  there.
- **async/await** for everything that can take longer than a few milliseconds. The core exposes
  `Task`/`Task<T>` methods for those (`IntegrityChecker.CheckAsync`, `SetupDownloadLocator.LocateAsync`,
  `NetworkDiagnostics.RunAsync`, `InstallationDiscovery.DiscoverAsync`); blocking work inside them (file
  hashing, registry scans, the synchronous `NeoApiClient`) runs on the thread pool through `Task.Run`. The
  core uses `ConfigureAwait(false)` throughout. Fast pure logic stays synchronous.
- **Cancellation and progress**: every long method takes a `CancellationToken`; methods with progress take
  `IProgress<T>` (created on the UI thread, so reports arrive there). Pages own a `CancellationTokenSource`
  that is cancelled on dispose.
- **Event handlers**: `async void` exists only inside `UiOperation.Run(Control trigger, Func<Task> work)` in
  the UI project. It disables the trigger, awaits the work, catches every exception (logs it, shows a
  localized error), and re-enables the trigger. A second start of the same operation while it runs is
  ignored.
- **Player list**: the polling loop moves to `Lobby.PlayerListPoller` (core): an async loop of
  `Task.Run(() => client.TryGetConnectedPlayers(...))` and `Task.Delay(interval, token)`, reporting through
  `IProgress<T>`; it keeps every behaviour of the current loop (no I/O before `Start`, one log line per outage
  and one when the list is back, survives any exception of a single request, stops on cancel).
- **Timers**: the setup-mutex watcher uses a WinForms `Timer` (UI thread) that calls a fast core probe.
- `TaskScheduler.UnobservedTaskException` is logged in `Program`.

## Evidence

- ADR 0001: .NET 4.8 has `async`/`await`, `Task.Run`, `IProgress<T>`, `CancellationTokenSource`; the spike
  compiled such code with the local toolchain.
- `GeneralUserControl.cs` (v2 at `2dc6c43`): the existing loop already separates the request
  (`RequestConnectedPlayers`, which never throws) from the UI update (`ProgressChanged`); the async version
  keeps that split.
- `NeoApiClient` limits the connect and the whole exchange (`DeadlineStream`, finding korr-S3/S4 fixes), so
  running it in `Task.Run` cannot hang a thread-pool thread forever.
- NUnit 3.14 runs `async Task` test methods, so async core code is unit-testable without a UI thread.

## Consequences

- No `BackgroundWorker`, no `Control.Invoke` in new code; results arrive on the UI thread through `await`.
- A page that is closed during an operation gets an `OperationCanceledException`, which `UiOperation`
  treats as normal.
- Core tests await the methods directly with fakes; timing-dependent tests use a fake clock or very short
  intervals with an overall test timeout.

## Alternatives considered

- **Keep `BackgroundWorker`** for everything: verbose, no composition, error handling in
  `RunWorkerCompleted` only. Rejected.
- **Synchronous core, UI calls `Task.Run`**: spreads threading decisions across the UI and makes tests of
  cancellation impossible in the core. Rejected.
- **Reactive Extensions**: a new dependency for a few operations. Rejected.

## Amendment 2026-10-02 (implementation, L-WP2)

`UiOperation` and the logging of unobserved task exceptions exist in the launcher project. Details decided while
implementing, keeping the decision:

- **An instance, not a static class**: `UiOperation` needs the logger, and the launcher has no global logger
  (ADR 0013), so the composition root creates one instance and passes it to the pages that start asynchronous work;
  the call stays `uiOperation.Run(trigger, work)`. Its logic is `RunAsync(trigger, name, setEnabled, work, report)`
  without WinForms types, which the tests drive with a fake trigger: disabled during the work, enabled afterwards
  also after an exception thrown before the first `await`, one error report per failure, a failing report logged,
  `OperationCanceledException` logged as information only, a second start from the same trigger ignored, other
  triggers independent.
- **The error text is the existing "unexpected error" message** of the global handlers (`UnexpectedError`, shared
  with `Program`): an exception that reaches the UI boundary is a programming error by ADR 0013, because the core
  returns environment problems as results. The texts exist in English and French; German comes with the complete
  German resources of L-WP3. No new user-visible text is added.
- **`TaskScheduler.UnobservedTaskException`** is subscribed right after the logger is created; the handler logs the
  exception as an error and calls `SetObserved()` (`Program.LogUnobservedTaskException`, with a test).
- The first user of `UiOperation` is the asynchronous discovery of L-WP4; until then no page needs it.

## Amendment 2026-10-02 (L-WP4)

The installation discovery is the first asynchronous work of the launcher. `InstallationDiscovery.DiscoverAsync` runs
the discovery on the thread pool (`Task.Run`) and takes a `CancellationToken`; a test shows that with a registry whose
reads block it returns an unfinished task at once. The launcher's `InstallationService` awaits it on the UI thread,
raises `Changed` when a refresh starts and ends, and uses only the result of the latest refresh. The main window starts
it in `OnShown` through `UiOperation.Run` (the Auto-detect button is the trigger and is disabled meanwhile); the pages
show "searching" until the result is there.

## Amendment 2026-10-02 (implementation, L-WP6)

`Lobby.PlayerListPoller` replaces the worker loop of the Play page; Play and the setup watcher use the model as decided.
Details decided while implementing, keeping the decision:

- **Events instead of `IProgress<T>`**: the poller raises `Updated` (one result: available with the players,
  unavailable with the error, or stopped) and `AvailabilityChanged` (the first result, the start of an outage, the
  return of the list: the hook for the outage hint of L-WP9) through the `SynchronizationContext` of the thread that
  called `Start`, so the page gets them on the UI thread, and directly on the polling thread in the tests. Two
  subscribers (the page now, the outage hint later) are easier with events than with one `IProgress<T>`.
- **The behaviour of the old loop stays and is tested** (`Core/Lobby/PlayerListPollerTests`): no request before
  `Start` (review finding wart-S5); every request on the thread pool, a request that fails or throws is "unavailable"
  and the loop goes on (korr-S3, wart-S6); one log line when an outage starts and one when the list is back, with the
  texts of before; no result after `Dispose` or the cancel of the token passed to `Start`, also not of a request that
  was still running (korr-S5); the delay is the configured interval. A handler that throws is logged and the other
  handlers still run; an exception of the loop itself (a bug) ends it with one log line and the state "stopped", which
  the page shows as "see the log" (the old `RunWorkerCompleted` path). The delay is injectable for the tests.
- **Program creates the poller** from the server settings (null when they are invalid); the page starts it when it
  loads and disposes it with itself.
- **The setup watcher** (`Play.SetupWatcher`) is ticked by a WinForms timer of the main window every 500 ms on the UI
  thread; it probes only when two seconds have passed by its `IClock`, so the tests tick it by hand with a fake clock.
  Its events run on the UI thread.
- **A start runs on the thread pool** (`GameStarter.StartAsync`): the shell waits while Windows shows the elevation
  prompt of a `RUNASADMIN` layer, and `Process.Start` calls `ShellExecuteEx` on an STA thread of its own. The Play page
  is the trigger of `UiOperation`, so the game choice cannot change during a start.

Evidence: `Core/Lobby/PlayerListPollerTests`, `Core/Play/SetupWatcherTests`, `Core/Play/GameStarterTests`
(`StartAsync_StartsOnTheThreadPool`).

## Amendment 2026-10-02 (implementation, L-WP7)

- **The integrity check** runs on the thread pool (`IntegrityChecker.CheckAsync` wraps the synchronous check in
  `Task.Run`; a cancelled token gives a `Cancelled` report, not a cancelled task). `IntegrityModel` starts the quick
  check from the `Changed` event of `InstallationService` when a search has a new result, so nobody awaits it: neither
  the search, nor the window, nor Play. One check at a time: a new check (another installation, the full check)
  cancels the running one, and a generation counter drops the result of a check that was overtaken. A fault of the
  check (a bug; environment problems are results) is logged like an unobserved task exception.
- **Progress** of the full check comes through a `Progress<IntegrityProgress>` created on the UI thread, so the page
  gets it there; the *Tools* page shows it and its "Cancel check" calls `CancelCheck`. Closing the main window cancels a
  running check (the operations of a window end with it).
- **Requests to the update API** run only through `UiOperation` (version check: the clicked button is the trigger; the
  repair window: its "Open download page" is the trigger while the download is asked, and closing the window cancels the
  request through a `CancellationTokenSource` of the window; the cancellation ends as `Canceled` in `UiOperation`, no
  error message).

Evidence: `Launcher/IntegrityModelTests` (`TheQuickCheck_NeverDelaysTheSearch`,
`AnotherInstallation_CancelsTheRunningCheck_AndOnlyTheLatestReportCounts`, `CancelCheck_EndsTheFullCheck_WithoutFindings_AndTheFileIsClosed`),
`Launcher/UpdateModelTests` (`Contract_4_3_ClosingTheAdvice_CancelsTheRequest`), `Core/Integrity/IntegrityCheckerTests`
(`CheckAsync_WithACancelledToken_ReturnsACancelledReport`).
