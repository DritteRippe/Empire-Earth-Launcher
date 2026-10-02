# 0005 Own settings file instead of user.config

Status: **Accepted** (2026-10-02)

## Context

The launcher keeps the user's game folder, theme and custom theme file as user-scoped .NET settings
(`Properties/Settings.settings`, `LocalFileSettingsProvider`). A damaged `user.config` made every setting
throw; the review fix `UserSettingsRecovery` moves it aside. The server settings (NeoEE host, port,
timeout, poll interval) are application-scoped and live in `Empire Earth Launcher.exe.config`.
v2 needs a few more user settings (UI language, last game) and must keep the choice of installation, which
the contract makes source 1 of the discovery.

## Decision

- User settings move to **`%LOCALAPPDATA%\Empire Earth Launcher\settings.json`**, read and written by
  `Settings.SettingsStore` in the core:
  - `DataContractJsonSerializer`, a `[DataContract]` class with `SchemaVersion` (1) and
    `IExtensibleDataObject`, so members written by a newer launcher survive a save by an older one;
  - written to `settings.json.tmp` and swapped in with `File.Replace` (or `File.Move` when there is no file
    yet), so a crash never leaves a half-written file;
  - a missing file means defaults; a damaged file is renamed to `settings.json.damaged` (replacing an older
    copy), logged, and defaults are used: the behaviour of `UserSettingsRecovery`, with its tests ported;
  - members: `GameDirectory` (user choice: install root or EE folder, kept even if it does not exist),
    `ThemeName`, `CustomThemeFile`, `UiCulture` (empty = Windows), `LastGame` (EE or AoC).
- **Server settings stay** application settings in the `.exe.config` (read-only, editable by an
  administrator, documented in the README). With no user-scoped setting left, `LocalFileSettingsProvider`
  never opens a `user.config`.
- **No migration** of an old `user.config`: the launcher was never released.
- `UserSettingsRecovery` is removed together with the last user-scoped setting; its behaviour lives on in
  `SettingsStore`.

## Evidence

- `LocalFileSettingsProvider` stores `user.config` below
  `%LOCALAPPDATA%\<company>\<exe>_<evidence>_<hash>\<version>\` (Microsoft, "Application Settings
  Architecture"). The hash comes from the evidence of the executable, for an unsigned program its path, and the
  folder contains the version: moving the launcher (a test build unzipped elsewhere, a portable copy) or a
  new version starts with empty settings unless `Upgrade()` is called. The launcher's settings must survive
  both.
- `UserSettingsRecovery.cs` documents the failure mode the new store must keep handling (half-written file
  after a crash or power loss, hand edits).
- README ("Download": not available) and `SharedAssemblyInfo.cs` ("Nothing has been released yet",
  `0.1.0-alpha`): no installed base with a `user.config` to migrate.
- The mod library already serializes with `DataContractJsonSerializer`
  (`Serialization/DataContractJsonHelper.cs`), no new dependency.

## Consequences

- Settings are found again after moving or updating the launcher.
- One small file per user; the backup folder and the log are next to it.
- The settings class is plain data and unit-testable with the in-memory file system.

## Alternatives considered

- **Keep `user.config`** with `Upgrade()`: still loses settings when the folder changes, keeps the recovery
  special case. Rejected.
- **Registry (`HKCU\Software\Empire Earth Community\Launcher`)**: the contract reserves
  `Software\Empire Earth Community` for the setup and the defaults marker; mixing the launcher's own state
  into it blurs the read-only rules. Rejected.
- **INI file**: no typed values, own parser. Rejected.
