# 0013 Error handling and logging

Status: **Accepted** (2026-10-02), amended 2026-10-02 (plan review; implementation in L-WP8), see the Amendment
sections

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

## Amendment 2026-10-02 (plan review)

**Privacy of the diagnostics report.** The report is meant to be posted in the forum, and the network part and the
name checks bring personal data close to it. Rules, for the report and for the log lines of the network
diagnostics:

- The external or public IPv4 address (from `upnp_info.txt` or elsewhere) only as its class: private, CGNAT
  (100.64.0.0/10) or public, never the value. Local private and link-local IPv4 addresses and the gateway may be
  shown. IPv6 only as "none", "link-local only" or "global".
- No MAC addresses, adapter GUIDs, adapter names chosen by the user, computer name or domain; an adapter is shown
  by its type and description (driver name).
- Lobby profile and player names only as "profile n: contains characters outside printable ASCII".
- The Windows user name is replaced wherever it is a path segment (not only below `%USERPROFILE%`: also
  `D:\Users\<name>` or VirtualStore paths), a computer name in a UNC path by `<computer>`.
- A golden-file test runs with fakes full of such values, and a negative test checks that none of them appears in
  the text.

## Amendment 2026-10-02 (implementation, L-WP8)

- **Results of the maintenance tools**: `CleanupOutcome`, `WonResetOutcome`, `FileBackupOutcome`, `ExportOutcome`,
  `ImportCheck` and `ImportFileOutcome` carry every environment problem (a key or folder that cannot be read, access
  denied, a drive that is not there, a backup that cannot be written, a file that is too large) with its reason for
  the log; `Texts` turns them into the localized line of the page. Only programming errors throw: a selection that is
  not an offered item of the scan, two files with the same backup target, a cleanup entry that breaks the rules of the
  list, advice that would name a protected key.
- **Narrow catches at the UI boundary**: "Open backup folder" catches only the exceptions of a shell start
  (`Win32Exception`, `FileNotFoundException`, `InvalidOperationException`) and shows the message; a scan that fails in
  the background is logged once (`The scan of the maintenance tools failed.`).
- **Never logged, in addition**: the player and profile names of the name check (only their number and game, the rule
  of the plan review applied to the log; the page shows them to the player); the contents of the moved WON files and of
  backed-up saved games (only paths); the values of the cleaned keys (they are in the `.reg` backup) and of `CDKeys`
  (only whether it exists, before and after a cleanup).

Evidence: `Core/Maintenance/NameChecksTests` ("names are never logged"), `Core/Backup/FileBackupTests`
(`TheContents_AreNeverLogged`), `Launcher/CleanupViewTests` (no CD-key value in the read-only list),
`Launcher/MaintenanceModelTests` (`OpenBackupFolder_ReportsAnError`).
