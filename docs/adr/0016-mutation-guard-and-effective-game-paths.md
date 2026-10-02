# 0016 Mutation guard and effective game paths

Status: **Accepted** (2026-10-02), amended 2026-10-02 (implementation in L-WP2 and L-WP4; plan review;
implementation in L-WP5), see the Amendment sections

## Context

The first design blocked only Play while a setup or a game runs (ADR 0010). Reset, the defaults, compatibility
options, the WON login reset, importing saves and the registry cleanup could still run while the game writes
its settings on exit and holds its files, or while a setup writes the same game settings values
(`setup_is6.iss` lines 300-301: `SetupMutex`, `AppMutex`).

The launcher gets an `asInvoker` manifest (ADR 0011) and is therefore not virtualized; the game, a legacy
program without a manifest, is. For installations below `Program Files` without write rights for users (foreign
ones; community setups grant `authusers-modify` on `Data`), the game reads and writes copies below
`%LOCALAPPDATA%\VirtualStore\...`. Only the lobby profile reader of the first design used that effective
copy; export would miss saves the game wrote there, an import into the real folder would be invisible to the
game or fail with access denied, and a WON reset would miss the files the game really uses (forum 4.12, forum
report section 8 row 2).

## Decision

- **`MutationGuard`** in the core: every action that changes game settings, the marker, compatibility values
  or files in the game folders (first run, reset, apply display defaults, compatibility options, WON login
  reset, save/scenario import, registry cleanup) asks the guard first. If `EE_Setup`/`NeoEE_Setup` or the mutex
  of a game of that installation exists (probe of ADR 0010), the action returns `Blocked(SetupRunning)` or
  `Blocked(GameRunning)` and changes nothing; the UI explains it. Read-only actions (checks, export,
  diagnostics) are not blocked. Every guarded action has a fake test for both blocked cases.
- **`EffectivePathResolver`** in the core is the only source of game file paths for the lobby profiles, save
  and scenario export and import, and the WON login reset: if the game folder is virtualizable (below
  `Program Files`, `Program Files (x86)`, `ProgramData` or the Windows folder) and the VirtualStore copy of a
  file exists, that copy is the effective file.
  - **Export** merges both places; on a name conflict the VirtualStore copy wins and the other is listed.
  - **Import** writes where the game reads: into the VirtualStore folder if the game folder is virtualizable
    and not writable for the user, else into the game folder.
  - **WON login reset** moves the files from both places.

## Evidence

- CONTRACT.md 4.2 (running setup), 2.5 (no change of game files; imports are player files, not manifest
  files), 3.6.
- `Empire-Earth-Setup/setup_is6.iss` lines 300-301; `[Dirs]`/`[Files]` permissions `authusers-modify` (forum
  report section 8 row 2).
- Forum 4.12 (version jump only without admin rights on Vista/7: VirtualStore), forum report section 8 row 2
  and test case 1; p=83519 (WON login files).

## Consequences

- No write of the launcher races with the game or a setup; the test plan has "reset while the game runs".
- Players of foreign installations below `Program Files` see and keep the saves the game really uses.

## Alternatives considered

- **Block only Play** (first design): see context. Rejected.
- **Write imports always into the real folder**: invisible to the game when a VirtualStore copy exists, and
  access denied for foreign installations. Rejected.

## Amendment 2026-10-02 (implementation, L-WP2)

`Play.MutationGuard` and the probes exist in the core (`Platform.IMutexProbe`, `Platform.WindowsMutexProbe`, the
test fake `FakeMutexProbe`); the `EffectivePathResolver` follows with the discovery (L-WP4). Refinements made while
implementing, keeping the decision:

- **All four mutexes, whatever the installation**: the names are fixed per product and game, not per installation
  (contract 0), so "a game of that installation" cannot be told apart from the same game of another installation.
  The guard checks `NeoEE_Setup`, `EE_Setup`, `StainlessSteelStudiosPresentsEmpireEarth` and
  `MadDocSoftwarePresentsEmpireEarthExpansion` in this order and blocks on the first it finds: a running game of
  any installation may write the shared game settings keys on exit, and both setups write the GPU preference and
  the compatibility values. A setup is reported before a game.
- **The answer carries the reason**: `MutationCheck` with `Block` (`None`, `SetupRunning`, `GameRunning`), the
  product or game and the mutex name, so that the UI can name the program (ADR 0010 amendment); every block is
  logged with the action the caller named.
- **Probe errors count as "exists"**: `WindowsMutexProbe` treats `UnauthorizedAccessException` as an existing mutex
  (ADR 0010) and also an unexpected Win32 error (`IOException`, logged), the safe side for every caller.

Evidence: `Core/Play/MutationGuardTests` (both blocked cases for every mutex, order, setup before game, names that
must not block), `Core/Platform/WindowsMutexProbeTests` (real named mutexes with random names, also under Mono).

## Amendment 2026-10-02 (implementation, L-WP4)

`Installations.EffectivePathResolver` exists in the core, and the lobby profiles and friends are read through it
(`LobbyProfileRepository` on `IFileSystem`). Details decided while implementing, keeping the decision:

- **Which folders are virtualizable** is given by the composition root: `Program` passes the folders of Windows
  (`ProgramFiles`, `ProgramFilesX86`, `CommonApplicationData`, `Windows`) and `%LOCALAPPDATA%\VirtualStore`; the core
  has no environment access of its own and the tests pass Windows paths. Without `%LOCALAPPDATA%` nothing is
  virtualizable. Only paths with a drive below one of these folders are looked up in the VirtualStore; everything
  else (`C:\Games\EE`, `%LOCALAPPDATA%\Programs\...` of a user installation, network paths) never is, so a stale copy
  of another folder is never used.
- **The VirtualStore path** is the path without its drive below the VirtualStore folder
  (`C:\Program Files (x86)\X\f` -> `%LOCALAPPDATA%\VirtualStore\Program Files (x86)\X\f`); a copy counts also when the
  original does not exist (the game created the file while it was virtualized).
- **One answer for both places**: `Resolve` returns the game path, the VirtualStore path (or null) and which one is
  effective, so that the export (both places) and the import (where the game reads) of L-WP8 use the same rule.
- Reading a VirtualStore copy is logged (`The game uses the VirtualStore copy ... of ...`), so the test plan can see it
  (`docs/TEST-PLAN.de.md`, WP4-16).

Evidence: `Core/Installations/EffectivePathResolverTests`, `Core/Lobby/LobbyProfileRepositoryTests` (VirtualStore cases).

## Amendment 2026-10-02 (plan review)

Reading game files can disturb a setup too. `LocalFileSystem.OpenRead` shares the file for reading and writing but
not for deleting; a setup started from the repair advice while a quick or full check still hashes a file (slow
disk, antivirus scan) fails to delete or replace it with a sharing violation (`DeleteFile failed; code 32`).

- `LocalFileSystem.OpenRead` opens with `FileShare.ReadWrite | FileShare.Delete` (an adapter test checks the share
  mode), so deleting or renaming a file that is being read is not refused.
- A file whose deletion is pending cannot be replaced under the same name until the launcher closes it, so the
  integrity check also gives way: it does not start while `EE_Setup` or `NeoEE_Setup` exists, and the setup-mutex
  watcher (ADR 0010) cancels a running quick or full check through its `CancellationToken` as soon as one of them
  appears. The result is "check cancelled" without findings; when the setup has ended, the quick check runs again
  (ARCHITECTURE 4.3). A setup holds its mutex from its first window on, long before it copies files.
- Tests: a fake mutex that appears while a file is hashed -> cancelled, no findings, file closed; no start while
  the mutex exists. Test plan: start a full check, then the setup; the setup runs without an error dialog.

## Amendment 2026-10-02 (implementation, L-WP5)

The game settings are the first actions behind the guard. Every writing action asks it first and returns
`Blocked(SetupRunning)` or `Blocked(GameRunning)` without any change and without a backup: the defaults at launcher
start, the first run before Play, the answer to the display question (both answers), the synchronization of class S,
"Apply recommended display", the reset, switching a compatibility entry and removing `~ RUNASADMIN`. The start writes
nothing at all while the guard blocks (one log line), and the next discovery tries again. The guard is asked once per
action, before the backup; the registry work of an action takes milliseconds.

Evidence: `GameDefaultsServiceTests.EveryWritingAction_IsBlockedBySetupAndGame` (7 actions x setup and game mutex,
14 cases: no change, no backup folder), `CompatibilityOptionsTests.EveryChange_IsBlockedBySetupAndGame` (4 cases),
`Launcher/GameSettingsModelTests` (the start blocked by a setup).
