# 0004 async/await threading model

Status: **Accepted** (2026-10-02)

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
