# Changelog

All notable changes to Empire Earth Launcher, its libraries and the mod creator are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html). Nothing has been released yet (all assemblies carry
`0.1.0-alpha`, see `SharedAssemblyInfo.cs`). The fixes of the code review that preceded v2 (branch
`refactor/quality-fixes`) are described in the git history.

## [Unreleased]

Launcher v2 is built on branch `v2` in work packages ([docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), section 15).

### Added

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
- README: requirements per Windows version, Windows build commands for 4.8, the limits of the Mono build; the
  rule "no `async`/`await`" of the 4.0 build is gone.

### Removed

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
