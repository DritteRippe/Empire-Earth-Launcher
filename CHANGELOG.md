# Changelog

All notable changes to Empire Earth Launcher, its libraries and the mod creator are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html). Nothing has been released yet (all assemblies carry
`0.1.0-alpha`, see `SharedAssemblyInfo.cs`). The fixes of the code review that preceded v2 (branch
`refactor/quality-fixes`) are described in the git history.

## [Unreleased]

Launcher v2 is built on branch `v2` in work packages ([docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), section 15).

### Added

- Discovery of every Empire Earth installation (contract 1.4, `Empire-Earth-Launcher-Core/Installations`), in place of
  the old game folder detection. Five sources: the folder chosen on the *Launcher* page, the install records of setups
  since v2 (HKCU, HKLM 64-bit, HKLM 32-bit view; NeoEE before EE), the uninstall keys `{<GUID>}_is1` of community
  setups (only the two exact publishers of the contract, `Inno Setup: App Path` before `InstallLocation`), the
  "Installed From" values (key before hive: `Software\Neo\Empire Earth` in HKCU, HKLM32, HKLM64, then
  `Software\SSSI\Empire Earth`; the old detection let HKCU win over the key, so an old retail entry beat a NeoEE
  installation, forum report section 8 test case 8) and the launcher folder or its parent. Entries of the same
  install folder are one installation (the most specific source wins); `install.ini` (BOM, LF/CRLF, unknown sections
  and keys, names ignoring case) decides between community setups since v2, setups up to 1.7.2 and other
  installations (NeoEE if `neoee.dll` is there). Each installation carries its real Empire Earth and Art of Conquest
  folder ([ADR 0015](docs/adr/0015-game-settings-target-folders-and-write-timing.md)): `C:\Games\EE` stays
  `C:\Games\EE`, `D:\Empire Earth` has the root `D:\`. A missing `Empire Earth.exe` (or `EE-AOC.exe` with the
  component `gameaoc`) means damaged, not "not found"; a chosen folder that does not exist stays chosen. Two products
  in one folder use the `install.ini` written last (O11); a `ContractVersion` above the launcher's is flagged
  (contract 5). A source that cannot be read drops only that candidate, with one log line; the discovery only reads
  and runs in the background.
- *Launcher* page: a list of the installations found (product, install folder, Empire Earth folder, type, state, with
  tooltips); picking one makes it the choice. Below the list a hint when several installations share one set of game
  settings (ADR 0015), when EE and NeoEE share a folder, when a setup is newer than the launcher, and the antivirus
  advice for a damaged installation. "..." now also accepts the install folder or the Art of Conquest folder without a
  question. 21 new texts in English, German and French.
- Effective game paths ([ADR 0016](docs/adr/0016-mutation-guard-and-effective-game-paths.md)): in a game folder below
  `Program Files`, `Program Files (x86)`, `ProgramData` or the Windows folder the launcher reads the copy of a file in
  `%LOCALAPPDATA%\VirtualStore` first, as the game does; the lobby profiles and friends use it.
- Test plan: cases WP4-01 to WP4-18 (installations of setup 1.7.2 and v2 for all users and for one user, a second
  Windows account, retail/GOG or a copy with another folder name, key before hive, damaged installations, the choice of
  root, game folder or AoC folder, VirtualStore, the page in three languages, the log).
- Tests: a table test with a case for every rule of contract 1.4 and every row of 1.5 (it reads the rules from
  `docs/CONTRACT.md`), the readers of every source, 32-bit Windows without duplicates, no write (fakes that fail on a
  write), a discovery with a blocking registry that returns at once, the effective paths. The cases of the old
  `GameDirectoryLocatorTests` live on under their names in `GameDirectoryLocatorPortTests`; no test is skipped under
  Mono any more.
- German user interface: `Properties/Resources.de.resx` translates all 50 texts of the launcher
  ([ADR 0009](docs/adr/0009-localization-with-resx-en-de-fr.md)); the build puts it next to the program as
  `de\Empire Earth Launcher.resources.dll`, like the French `fr\`. The launcher uses it when the Windows display
  language is German (or the language setting chooses it). The German texts address the player formally ("Sie"),
  as the setup does.
- Language setting on the *Launcher* page: Windows language (default), English, Deutsch or Français. It is saved
  as `UiCulture` in `settings.json` (an optional member of schema 1) and applied when the launcher starts, before
  the first window; a hint says so after a change. Only the texts change, number and date formats stay those of
  Windows. An unknown value in the file is logged and means the Windows language. The log names the language
  in use (`UI language: de (launcher setting)`).
- `docs/TRANSLATING.md`: the languages of the launcher, where the texts are, the rules for placeholders, line
  breaks and lengths, how to add a text or a language, how to test a translation, and the review state (German:
  proof-reading in the laptop test; French: review open for all texts but the two navigation texts of the
  original authors).
- Test plan: cases WP3-01 to WP3-17 (removed placeholders, every page in German, English and French, proof-reading
  the German texts, the language setting, the satellite folders, scaling with German texts).
- `ResourceParityTests`: English, German and French have the same string keys (comments and entries with a
  `type` or `mimetype` are not compared), no empty text and the same `{n}` placeholders; images and file
  references exist only in the neutral `Resources.resx`; `Resources.Designer.cs` has a property for every text;
  the launcher project embeds every translation; and the built `de\` and `fr\` satellite assemblies hold exactly
  the texts of their resx files.
- The UI-free core library `Empire-Earth-Launcher-Core` (`Empire_Earth_Launcher_Core.dll`, next to the launcher;
  [ADR 0003](docs/adr/0003-ui-free-core-library.md)). It references only the BCL and the WON library, which an
  architecture test checks, and holds:
  - the fixed names of the shared contract (products, games, keys, files, mutexes, contract version), tested
    against `docs/CONTRACT.md`;
  - platform abstractions with fakes for the tests ([ADR 0006](docs/adr/0006-platform-abstractions-and-windows-path-logic.md)):
    registry access with an explicit HKLM view, a file system, a clock, a mutex probe, and `WinPath`, the Windows
    path rules as string logic (also the check of manifest paths, so that the manifest can never point outside the
    install root);
  - the registry write policy ([ADR 0007](docs/adr/0007-registry-write-scope-and-reg-backups.md)): every key is put
    into a canonical form first (case, `/`, `WOW6432Node`, registry VirtualStore), then `Software\Sierra\CDKeys`,
    the install records and the uninstall keys are refused with their subtrees and ancestors in every hive and
    view, then only listed HKCU keys are allowed; a table test covers every alias and operation;
  - the mutation guard ([ADR 0016](docs/adr/0016-mutation-guard-and-effective-game-paths.md)): no change while a
    setup or a game runs (setup and game mutexes);
  - `settings.json` (see Changed).
- `UiOperation` in the launcher: the one place for asynchronous work of event handlers
  ([ADR 0004](docs/adr/0004-async-await-threading-model.md)); unobserved exceptions of background tasks are logged.
- Test plan: cases WP2-01 to WP2-11 (`settings.json`, the core library in the package, the log).
- The launcher has its own application manifest (`app.manifest`). As before, it runs without elevation
  (`asInvoker`, which also keeps UAC virtualization off) and is not DPI-aware; new is the list of supported
  Windows versions (7 to 11), so Windows 8.1 and later report their real version to the launcher instead of
  Windows 8 ([ADR 0011](docs/adr/0011-screen-size-in-physical-pixels.md)).
- Architecture tests (`Empire-Earth-Launcher.Tests/Architecture/ProjectConventionsTests.cs`) check the settings
  every project shares: .NET Framework 4.8, C# 8.0, deterministic builds, AnyCPU without `Prefer32Bit` for the
  executables, the `lib` folder of each NuGet package, `packages.config`, the 4.8 runtime in `App.config`, no
  elevation in an application manifest, the launcher manifest, the target framework of the built assemblies
  and the reference assemblies of the CI build.
- `docs/TEST-PLAN.de.md`: the German test plan for the manual test on a real Windows computer (prerequisites,
  getting and checking the test package, building on Windows, where the launcher keeps its files, the cases of
  each work package; optional Windows 7 SP1 cases).
- This changelog.

### Changed

- `docs/CONTRACT.md`, revision 2 (identical to the copy in the setup repository; contract version still 1, draft):
  tables of the window size limits (3.3) and of the GPU preference values (3.4); the setup's opt-in task
  `compatibility_legacy` with the flags on Windows 7, without a Windows version layer, and the marker `(opt-in)` in
  the table of 3.7 (the launcher MAY offer these flags on Windows 7 now, and must not call such a value a leftover
  of an earlier setup when `Tasks` contains `compatibility_legacy`); while `EE_Setup` or `NeoEE_Setup` exists the
  launcher must neither read `install.ini` and `files.sha256` nor run an integrity check, and it opens these files
  with at least `FileShare.Read | FileShare.Delete` (4.2, as planned in ADR 0016); a manifest the setup could not
  replace means Unknown, through the missing `Empire Earth Community: ContractVersion` (1.3, 2.1, 2.5). The launcher
  code does not change with this revision; its packages implement the new rules.
- The launcher wraps the Windows registry in the write policy of ADR 0007 (`PolicyCheckedRegistry`); the discovery only
  reads. It starts once the main window is shown, through `UiOperation`; until it has finished the pages show
  "Searching for Empire Earth installations...".
- The lobby profiles come from the Empire Earth folder of the selected installation, through the file system
  abstraction of the core.
- The rows of the *Launcher* page moved up to make room for the list of installations; the theme list has the width
  of the other lists.
- Every text of the launcher's windows comes from `Properties/Resources.resx` and is set in one `ApplyTexts()`
  method per window or page ([ADR 0009](docs/adr/0009-localization-with-resx-en-de-fr.md)); about 20 texts that
  existed only in the designer (navigation, page headings, labels, buttons, the compatibility warning, the
  buttons of the launcher's message dialog) are now resources with a translator comment. The French navigation
  texts moved from `MainForm.fr.resx` (removed) into `Resources.fr.resx`, and French translations of the new
  texts were added. The launcher declares English as its neutral language. `Texts` turns results (player state,
  lobby profiles and friends, origin of the game folder) into texts. An architecture test (`ApplyTextsTests`)
  checks that every designer text with a letter is set again in `ApplyTexts()`, that hand-written code assigns
  no literal text and that no window has a resx of its own per language. Some English texts were adjusted:
  "Game" instead of "Game Settings" above the game choice (the group holds only the choice now), "Profile:"
  instead of "User:", "Player" for the player column, "Empire Earth folder:" without the space before the
  colon and "Custom file..." for the first item of the theme list; the message dialog shows its title also in
  the taskbar instead of "LauncherDialog".
- The logger, the launcher's file locations and the lobby profile reader moved unchanged into the core library
  (namespaces `Empire_Earth_Launcher.Core.Logging`, `.Settings`, `.Lobby`); their tests moved with them.
- The launcher keeps its user settings (game folder, theme, custom theme file) in
  `%LOCALAPPDATA%\Empire Earth Launcher\settings.json` instead of .NET's `user.config`
  ([ADR 0005](docs/adr/0005-own-settings-file-instead-of-user-config.md)): they survive moving or updating the
  launcher; the file is written through `settings.json.tmp`; a damaged file is renamed to
  `settings.json.damaged` and the defaults are used; a file that cannot be read or that a newer launcher wrote
  is never overwritten; members a newer launcher added are kept. Settings of earlier test builds are not taken
  over. The server settings stay in `Empire Earth Launcher.exe.config`.
- All projects (launcher, WON library, mod library, mod creator, tests) target the **.NET Framework 4.8**
  instead of 4.0 ([ADR 0001](docs/adr/0001-target-dotnet-framework-4-8.md)). The launcher and the mod creator
  need the .NET Framework 4.8 at run time; where it is missing, Windows offers to install it. `async`/`await`,
  `Task.Run` and `HttpClient` may now be used.
- Krypton.Toolkit 5.550 is referenced from its .NET Framework 4.8 build (same version and assembly identity),
  NUnit and NUnitLite 3.14 from their .NET Framework 4.5 builds.
- The executables set `Prefer32Bit` to false explicitly (they stay AnyCPU and run as 64-bit processes on 64-bit
  Windows, as before); the mod library now builds deterministically like the other projects.
- CI builds against `Microsoft.NETFramework.ReferenceAssemblies.net48` 1.0.3 instead of the net40 package.
- README: the feature list says what works, what comes with v2 and what is planned but not in v2 (the removed
  placeholders), and that the launcher has no telemetry; localization, settings and tests describe the three
  languages.
- README: requirements per Windows version, Windows build commands for 4.8, the limits of the Mono build; the
  rule "no `async`/`await`" of the 4.0 build is gone.

### Removed

- `GameDirectoryLocator` and `GameDirectoryService` of the launcher (replaced by the discovery of the core and
  `InstallationService`) and their tests, which ran on Windows only (the cases are ported).
- The placeholder controls that had no function ([ADR 0014](docs/adr/0014-only-working-features-in-the-ui.md),
  exact list in its amendment): on the *Launcher* page the checkbox "Allow us to collect diagnostic data" (the
  launcher sends no telemetry), the `.eem` file association and the "When starting/closing the game" choices; on
  the *Settings* page the "Magic Button" group (Repair CD-Keys, Reset the Game, Clear Registry) and the
  Compatibility, Windows, DirectX and Advanced Settings groups (compatibility mode, heap, 8/16 bit, DirectX
  wrapper, dgVoodoo, resolution, monitor, game font, dreXmod, NeoEE, Discord presence, HD textures, skip intro);
  on the *Play* page the game language and online ranking groups and "No mods in use"; the *Mods* navigation
  button. The working compatibility warning of the *Settings* page stays. Removed are the fields, the designer
  code and the resources; none of them had a handler. An architecture test (`PlaceholderControlsTests`) keeps
  the names out of the launcher's designer files, code and resources. Features that v2 implements come back as
  working controls with new names in their work packages (reset, compatibility options, repair advice, ...).
- `UserSettingsRecovery` (recovery from a damaged `user.config`): the launcher has no user-scoped .NET settings
  left; the behaviour lives on in `SettingsStore` with its tests.
- Support for Windows XP, Vista, Windows 8.0 and Windows 10 versions 1507 and 1511, which cannot run the
  .NET Framework 4.8 (Windows 8.0 users can update to 8.1 for free). Windows 7 SP1 stays supported but has not
  been tested yet.
