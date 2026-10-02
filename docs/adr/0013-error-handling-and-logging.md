# 0013 Error handling and logging

Status: **Accepted** (2026-10-02)

## Context

The quality review found crashes and silent failures caused by missing files, `null` returns and swallowed
exceptions (korr-S1, korr-S3, korr-S8, wart-S6); the fixes return status values (`LobbyProfilesStatus`,
`NeoApiClient.Reply`) and log once per outage. v2 reads many sources that can be missing or damaged on any
computer (registry records, uninstall keys, INI and manifest files, VirtualStore, network).

## Decision

- **Environment problems are results**: discovery candidates, integrity findings, defaults actions, cleanup
  candidates, diagnostics steps each carry a status and a reason. Nothing in the core throws for a missing
  key or file, denied access, a damaged file, an invalid path or an unreachable server.
- **Programming errors throw** (`ArgumentNullException`, `InvalidOperationException` for broken invariants)
  and end in the global handlers (logged, "unexpected error" dialog, existing behaviour).
- **Narrow catches**: adapters catch only documented exception types and translate them; `catch (Exception)`
  only in `UiOperation` (UI boundary) and in loops that must survive one failed iteration (player list).
- **Safe changes**: backup first and abort if it fails; temporary file then replace; marker last; every write
  logged with old and new value.
- **Logging** through `ILogger` (core), implemented by `TraceFileLogger` (existing: ISO timestamps, trimming,
  `.old` copy). English messages with the data needed to support a player (paths, values, hashes,
  durations). Never logged: CD-key values, WON key file contents, credentials (contract 3.8, O8).
- **No silent fallback**: every fallback is logged and shown in the UI or the diagnostics report.

## Evidence

- `findings-Empire-Earth-Launcher.json` (korr-S1, korr-S3, korr-S8, wart-S6) and their fixes on `v2`
  (`LobbyProfileRepository`, `NeoApiClient.Reply`, `GeneralUserControl.ShowPlayerListUnavailable`).
- CONTRACT.md 1.4 ("discovery never fails as a whole"), 2.5 (log every finding with path, class, expected and
  actual hash), 3.6 (backup or nothing), 3.8 (never log CD-key values).

## Consequences

- Result types are part of the core API and of the tests.
- Support can ask for `log.txt` or the diagnostics report instead of screenshots.

## Alternatives considered

- **Exceptions for environment problems**: every caller needs try/catch, easy to forget (the crashes of the
  review). Rejected.
