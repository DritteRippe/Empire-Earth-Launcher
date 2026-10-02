# 0003 One UI-free core library, thin WinForms UI

Status: **Accepted** (2026-10-02)

## Context

Today the launcher project mixes UI and logic (game folder detection, lobby profiles, settings recovery,
logging live in the WinForms executable), and the tests reference the executable through
`InternalsVisibleTo`. v2 adds discovery, integrity, defaults, reset, cleanup, diagnostics and the repair
hand-off, which together are far more logic than UI. D4 requires a UI-free core with unit tests.

## Decision

- New project **`Empire-Earth-Launcher-Core`** (assembly `Empire_Earth_Launcher_Core`, root namespace
  `Empire_Earth_Launcher.Core`, one namespace per area: `Contract`, `Platform`, `Logging`, `Settings`,
  `Installations`, `Integrity`, `GameSettings`, `Backup`, `Play`, `Repair`, `Maintenance`, `Diagnostics`,
  `Lobby`).
- The UI-free classes of the launcher move into it (`ILogger`, `TraceFileLogger`, `LauncherPaths`,
  `LobbyProfileRepository`, the game folder detection, which `Installations` replaces).
- **`Empire-Earth-WON`** and **`Empire-Earth-Mod-Lib`** stay separate libraries; the core references the WON
  library (status request, lobby files), not the mod library (no mod installation in v2).
- The **launcher executable** keeps only UI: composition root, forms, pages, dialogs, the theme service, the
  mapping of core results to localized texts and the async event-handler helper.
- Rules (checked by architecture tests, ADR 0012): the core references no `System.Windows.Forms`,
  `System.Drawing` or Krypton; the core returns results, not user texts; the UI contains no registry, file
  system, process or HTTP calls of its own.
- **Presenters** are not introduced as a separate layer: each page calls core services and renders their
  results; decisions (what to ask, which values to write, which state to show) are made by the core and
  returned as data, so they are tested there.

## Evidence

- Existing UI-free code already works this way and is tested (`LobbyProfileRepository`, `GameDirectoryLocator`,
  `UserSettingsRecovery`, the WON and mod libraries; 235 tests on `v2`).
- The mod creator (`Empire-Earth-Mod.csproj`) references only `Empire-Earth-Mod-Lib`; moving the mod library
  into the core would make the mod creator depend on launcher code.
- `Empire-Earth-WON` has its own history and documented protocol (`NeoApiClient.cs` remarks); it stays as is.
- The contract's checklist (CONTRACT.md 7) asks for discovery, manifest, defaults, repair and mutexes "in the
  UI-free core library with unit tests (fake registry and file system, no network)".

## Consequences

- One more assembly to ship (`Empire_Earth_Launcher_Core.dll` next to the executable; the CI packages the
  whole `bin\Release` folder, so nothing changes there).
- The tests reference the core directly; the executable needs `InternalsVisibleTo` only for UI helpers.
- Namespaces of moved classes change once (`Empire_Earth_Launcher` -> `Empire_Earth_Launcher.Core.<Area>`).

## Alternatives considered

- **One assembly per area**: clearer boundaries but many classic project files that all need the platform
  abstractions; no consumer needs an area on its own. Rejected.
- **Keep the logic in the executable and test through `InternalsVisibleTo`**: no enforced boundary, UI
  assemblies loaded in every test. Rejected.
- **MVP/MVVM framework**: more structure than a handful of pages needs; core results already carry the
  decisions. Rejected.
