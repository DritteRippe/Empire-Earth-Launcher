# Empire Earth Launcher

A launcher for Empire Earth 1, everything inside, no more strange setup, update, patch, manual zip manipulation.\
Coded in C# with the .NET Framework 4.8 and Krypton UI

![image](https://github.com/EE-modders/Empire-Earth-Launcher/blob/main/EEL_MainScreen.png)

## 🧾 Features

Launcher v2 is being built in work packages on branch `v2` ([docs/ARCHITECTURE.md](docs/ARCHITECTURE.md),
section 15; what is done is in the [CHANGELOG](CHANGELOG.md)). The UI shows only controls that work
([ADR 0014](docs/adr/0014-only-working-features-in-the-ui.md)): the placeholder controls of the old mock-up were
removed and the features behind them are listed below.

**Available now**

- NeoEE online player list, with the lobby profiles and friends of the game folder (also when the game keeps them
  in the VirtualStore)
- Detection of every Empire Earth installation: community setups (current and up to 1.7.2, admin, user and
  portable), retail, GOG and copies with their real folders; the *Launcher* page lists them with their state
  (a missing `Empire Earth.exe` shows as damaged) and lets the player choose one or another folder
- Themes (Krypton palette files)
- English, German and French user interface, following Windows or chosen on the *Launcher* page
  ([docs/TRANSLATING.md](docs/TRANSLATING.md))
- Mod creator for `.eem` mod packages
- Windows 7 SP1, 8.1, 10 and 11 with the .NET Framework 4.8 (see [Requirements](#requirements))

**Coming with v2** (each with its work package, ARCHITECTURE section 15)

- Play Empire Earth or The Art of Conquest safely: detection of a running game or setup, start log, file versions
- Per-user default game settings, reset with a `.reg` backup, compatibility options
- Integrity check of the installation and repair advice (CD keys are repaired by re-running the community setup;
  the launcher never touches them)
- Maintenance tools: registry cleanup of old installations, WON login reset, VirtualStore check, saved games and
  scenarios export/import
- Network diagnostics and a configuration report

**Planned, not in v2** (their placeholders were removed from the UI)

- Full mod system in the launcher: *Mods* page, browsable mods list, "mods in use", `.eem` file association
- dreXmod switch, Discord presence, HD textures, skip intro, game font customizer, lobby customizer
- Game languages (voices, lobby, campaigns) and the online ranking
- DirectX wrapper switch (DX 9/11/12) and resolution chooser: the setup's custom installation switches the wrapper,
  the launcher must not change game files ([ADR 0014](docs/adr/0014-only-working-features-in-the-ui.md))
- Auto-compatibility detector ("My game is working", "Auto-detect") and auto-update

**No telemetry**: the old checkbox "Allow us to collect diagnostic data" is gone. The launcher collects no usage or
diagnostic data; today its only connection is the request for the NeoEE player list.

## 🌐 Download
Sorry, at the moment the launcher is **ABSOLUTELY NOT** available for download in its current state.\
To use the Launcher you will only need the .NET Framework 4.8 on your computer (see [Requirements](#requirements)).

## Requirements

The launcher and the mod creator need the **.NET Framework 4.8** and run on these Windows versions:

| Windows | .NET Framework 4.8 | Status |
|---|---|---|
| Windows 11, Windows 10 version 1903 or later | built in | supported |
| Windows 10 versions 1607 to 1809 | install it once | supported |
| Windows 8.1 | install it once | supported |
| Windows 7 SP1 | install it once | supported, **not tested yet** |
| Windows 8.0, Windows 10 versions 1507 and 1511 | not available | **not supported**: update Windows (Windows 8.1 is a free update of 8.0) |
| Windows Vista, XP and older | not available | not supported |

- When the .NET Framework 4.8 is missing, Windows itself shows a message with a download link when the
  launcher starts. The installer is at https://dotnet.microsoft.com/download/dotnet-framework/net48 and needs
  administrator rights. To check what is installed, run in PowerShell
  `(Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full').Release`: 528040 or higher
  means 4.8 or newer.
- Windows 7 SP1: install the current Windows updates first. For the offline installer, Microsoft requires the
  "Microsoft Root Certificate Authority 2011" in the computer's trusted root certificates
  ([Install the .NET Framework on Windows 7 SP1](https://learn.microsoft.com/en-us/previous-versions/dotnet/framework/install/on-windows-7)).
  Windows 7 is no longer supported by Microsoft and the launcher has not been tested on it; reports are welcome.
- The launcher does not ask for administrator rights. Its window is not DPI-aware: on screens with a scaling
  above 100 % Windows enlarges it (it may look slightly blurry), the layout stays the same.

## Dev
You just need to clone the repo and open `Empire-Earth.sln` with Visual Studio **2019** or newer (with the workload ".NET desktop development", which contains the .NET Framework 4.8 targeting pack)\
Some very critical parts of the Launcher can be censored like WON and NeoEE related important operation but most of the reverse WON C# implementation is available 💪

### Building

All projects target the **.NET Framework 4.8** and are pinned to **C# 8.0** (`LangVersion` in every `.csproj`),
so every contributor compiles the same language with Visual Studio 2019 or newer, MSBuild or Mono. Do not use
newer language features or APIs that do not exist in .NET Framework 4.8. `async`/`await`, `Task.Run` and
`HttpClient` are available; how the launcher uses them is described in
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md#5-threading). The projects are classic project files with
`packages.config` (no SDK-style projects, no `Directory.Build.props`, see
[ADR 0002](docs/adr/0002-keep-classic-project-files-and-packages-config.md)); the settings they share
(`TargetFrameworkVersion` v4.8, `LangVersion` 8.0, `Deterministic`, AnyCPU with `Prefer32Bit` false for the
executables, the `lib` folders of the NuGet packages, the 4.8 runtime in `App.config`) are checked by the
architecture tests in `Empire-Earth-Launcher.Tests/Architecture/`. Change them in every project at once.

**Visual Studio (Windows)**: open `Empire-Earth.sln` and build. NuGet restores the packages into the root
`packages/` folder automatically.

**Command line (Windows, Developer PowerShell)**:

```powershell
nuget restore Empire-Earth.sln
msbuild Empire-Earth.sln /p:Configuration=Release
```

The output is in `Empire Earth Launcher\bin\Release\` and `Empire-Earth-Mod\Empire-Earth-Mod\bin\Release\`; the
launcher needs the libraries next to it (`Empire_Earth_Launcher_Core.dll`, `Empire_Earth_WON.dll`,
`Krypton.Toolkit.dll`), so copy the whole folder. If
MSBuild reports `MSB3644` (reference assemblies for `.NETFramework,Version=v4.8` not found, e.g. with the Build
Tools without the 4.8 targeting pack), build against Microsoft's reference assemblies from NuGet, exactly like
the CI does:

```powershell
nuget install Microsoft.NETFramework.ReferenceAssemblies.net48 -Version 1.0.3 -OutputDirectory $env:TEMP\refasm
$ref = "$env:TEMP\refasm\Microsoft.NETFramework.ReferenceAssemblies.net48.1.0.3\build"
msbuild Empire-Earth.sln /p:Configuration=Release "/p:TargetFrameworkRootPath=$ref" "/p:FrameworkPathOverride=$ref\.NETFramework\v4.8"
```

**Linux/macOS (Mono)**: useful as a compile check only, the WinForms/Krypton UI is meant to run on Windows.
With a Mono installation from mono-project.com (which ships `msbuild` and a Roslyn `csc`):

```sh
nuget restore Empire-Earth.sln        # or: mono nuget.exe restore Empire-Earth.sln
msbuild Empire-Earth.sln
```

Distribution packages that only ship `xbuild` and the old `mcs` compiler cannot compile C# 8. Point `xbuild`
at a Roslyn compiler from NuGet instead:

```sh
mono nuget.exe install Microsoft.Net.Compilers -Version 4.2.0 -OutputDirectory ~/.local/roslyn
printf '#!/bin/sh\nexec mono "%s/tools/csc.exe" "$@"\n' ~/.local/roslyn/Microsoft.Net.Compilers.4.2.0 \
  > ~/.local/roslyn/Microsoft.Net.Compilers.4.2.0/csc-mono
chmod +x ~/.local/roslyn/Microsoft.Net.Compilers.4.2.0/csc-mono
mono nuget.exe restore Empire-Earth.sln
xbuild Empire-Earth.sln /p:CscToolPath=$HOME/.local/roslyn/Microsoft.Net.Compilers.4.2.0 /p:CscToolExe=csc-mono
```

`xbuild` compiles against Mono's `4.8-api` reference assemblies and warns "TargetFrameworkVersion 'v4.8' not
supported by this toolset" (its own version list ends earlier; the warning is expected). It ignores
`ApplicationManifest` (the executables get the compiler's default manifest) and `FrameworkPathOverride`, so
its output is for compiling and running the tests, not for distribution; ship builds made with MSBuild.

**Tests**: `Empire-Earth-Launcher.Tests` is a console program built with the solution. It runs its NUnit
tests with NUnitLite (NUnit 3.14, the last NUnit 3 release, from its `net45` build; NUnit 4 would change the
classic assertions, see [ADR 0012](docs/adr/0012-test-strategy.md)) and exits with 0 only when every test
passed (otherwise with the number of failed tests). Run it after building:

```sh
Empire-Earth-Launcher.Tests\bin\Release\Empire-Earth-Launcher.Tests.exe          # Windows
mono Empire-Earth-Launcher.Tests/bin/Debug/Empire-Earth-Launcher.Tests.exe       # Mono
```

NUnitLite options can be passed, e.g. `--where "class =~ LobbyPersistentData"` to run some tests only or
`--result=TestResult.xml` to write an NUnit 3 result file (by default no result file is written). The tests
cover:

- the core library (`Core/`): the contract names against `docs/CONTRACT.md`, the Windows path rules
  (`WinPath`, including the manifest paths of contract 2.2), the file system and registry abstractions with their
  in-memory fakes, the canonical form of registry keys and the registry write policy, the mutex probe and the
  mutation guard, `settings.json` (including damaged, unreadable and newer files), the log format and trimming,
  the launcher's file locations, the lobby profiles, and the discovery of the installations (`Installations/`: the
  `install.ini` parser, the readers of the registry records, uninstall keys and "Installed From" values, a table
  test with a case for every rule of contract 1.4 and 1.5, real folders of foreign installations, every form of
  the user choice, one log line per dropped candidate, no write, no duplicates on 32-bit Windows, a discovery that
  does not block its caller, the VirtualStore paths, and the cases of the old game folder tests under their
  names);
- the UI helpers of the launcher (`Launcher/`): `UiOperation`, the "unexpected error" message, the logging of
  unobserved task exceptions, the texts chosen for results (`Texts`), the UI language applied at start and the
  installation service;
- the WON lobby file parser, the NeoEE protocol framing, reply parsing and request deadline (`Won/`), and the mod
  library (`Mod/`: product folders, file types, versions, the working directory of the mod creator, `.eem`
  export/import including damaged archives);
- architecture rules (`Architecture/`): the shared build settings of every project, the core's dependencies (no
  WinForms, `System.Drawing` or Krypton; only the BCL and the WON library), a table test that the registry
  write policy refuses `Software\Sierra\CDKeys`, the install records and the uninstall keys under every alias
  (`WOW6432Node`, registry VirtualStore, `/`, case) and every ancestor, for every operation, and the UI texts:
  the removed placeholder controls stay removed (`PlaceholderControlsTests`), every designer text is set again
  in `ApplyTexts()` (`ApplyTextsTests`), and English, German and French have the same texts, placeholders and
  built satellite assemblies (`ResourceParityTests`).

Architecture tests read the project files, `packages.config`, `App.config`, `app.manifest`, the core's sources,
the launcher's designer files, code and `.resx` files, `docs/CONTRACT.md`, ADR 0014, the CI workflow and the built
satellite assemblies; they find the source tree by walking up from the test program to
`Empire-Earth.sln`, so run the test program from its build folder inside the repository. The tests use fakes
(`Fakes/`: in-memory registry with both HKLM views and a 32-bit Windows mode, in-memory file system with Windows
path rules, mutex probe, clock, logger) and only write below the temporary folder; they never contact a server,
never touch the real registry or `%LOCALAPPDATA%` and never show UI. Path logic is `WinPath` string logic, so every
test also runs under Mono; no test is skipped. The core, the launcher and the WON library make their internal helpers
visible to the test assembly (`InternalsVisibleTo`).

**Continuous integration**: `.github/workflows/build.yml` restores and builds the solution in Release on
`windows-latest` for every push to `main` and every pull request, then runs every `*Tests.exe` it finds in
the `bin/Release` folders (a test program reports failure through a non-zero exit code; finding no test
program fails the build). The NUnit result files are kept as the `test-results` artifact.

**Test builds**: when all tests pass, the CI run also keeps the Release output of both applications for 30 days,
as the artifacts `Empire-Earth-Launcher-testbuild` and `Empire-Earth-Mod-Creator-testbuild` (open the run under
*Actions*, section *Artifacts*; downloading needs a GitHub login). Each zip contains the executables, their
libraries, the German and French resources (`de\`, `fr\`), the debug symbols (`.pdb`, for readable crash logs), `LICENSE`,
`THIRD-PARTY-NOTICES.md` and `THIRD-PARTY-LICENSES.txt` (from `licenses/`). Unzip and run the
`.exe`; the .NET Framework 4.8 is required (see [Requirements](#requirements)). These are prototype builds for
testing, not releases. The manual test on a real Windows computer is described in German in
[docs/TEST-PLAN.de.md](docs/TEST-PLAN.de.md).

**Versioning**: the version of all assemblies is maintained in one place, `SharedAssemblyInfo.cs`
(currently `0.1.0-alpha`, nothing has been released yet).

**Line endings**: `.gitattributes` stores text files with LF and checks C#, `.resx` and other Visual Studio
files out with CRLF, so no extra `core.autocrlf` configuration is needed.

**Code style**: `.editorconfig` defines the .NET naming conventions (shown as suggestions in Visual Studio and
Rider). Code comments are written in English.

**Localization**: every text of the launcher is a string of `Empire Earth Launcher/Properties/Resources.resx`
(English, the neutral language) with its German (`Resources.de.resx`) and French (`Resources.fr.resx`)
translation; each window sets its texts in `ApplyTexts()`, the designer texts are placeholders
([ADR 0009](docs/adr/0009-localization-with-resx-en-de-fr.md)). The build puts the translations next to the
program as `de\` and `fr\` satellite assemblies; copy them with the program. The mod creator has English and
French (`Properties/Resources*.resx` of its project). How to translate, add a text or a language:
[docs/TRANSLATING.md](docs/TRANSLATING.md).

### Project layout

```
Empire-Earth.sln                  Root solution containing every project
SharedAssemblyInfo.cs             Version information shared by all assemblies
CHANGELOG.md                      Changes of each version (Keep a Changelog)
THIRD-PARTY-NOTICES.md            Vendored code and NuGet dependencies with their licenses
docs/                             ARCHITECTURE.md (v2 target), CONTRACT.md (shared with the setup), adr/
                                  (decision records), TEST-PLAN.de.md (manual test on Windows, German),
                                  TRANSLATING.md (languages, how to translate)
Empire-Earth-Launcher-Core/       UI-free core library of the launcher (Empire_Earth_Launcher_Core.dll, ADR 0003):
│                                 only the BCL and the WON library, no WinForms, System.Drawing or Krypton
├─ Contract/                      Fixed names of docs/CONTRACT.md: products, games, keys, files, contract version
├─ Installations/                 Discovery of the installations (contract 1.4): install records, install.ini,
│                                 uninstall keys, "Installed From" values, launcher folder -> InstallationDiscovery
│                                 -> Installation with real EE/AoC folders (ADR 0015); EffectivePathResolver
│                                 (VirtualStore copies of game files, ADR 0016)
├─ Platform/                      Windows behind interfaces (ADR 0006): IRegistry/WindowsRegistry (explicit views),
│                                 RegistryLocation, RegistryValue, RegistryPath (canonical form), RegistryWritePolicy
│                                 and PolicyCheckedRegistry (protected keys, allow-list, ADR 0007), IFileSystem/
│                                 LocalFileSystem, WinPath (Windows path rules), IMutexProbe/WindowsMutexProbe, IClock
├─ Logging/                       ILogger, TraceFileLogger (log file with trimming)
├─ Settings/                      LauncherSettings, SettingsStore (settings.json, ADR 0005), LauncherPaths,
│                                 UiLanguage (the language setting, ADR 0009)
├─ Lobby/                         LobbyProfileRepository: lobby profiles and friends of the game folder (effective paths)
└─ Play/                          MutationGuard: no change while a setup or a game runs (ADR 0016)
Empire Earth Launcher/            The launcher (WinForms + Krypton UI)
├─ Program.cs                     Entry point and composition root: creates and passes on the services
├─ app.manifest                   Application manifest: asInvoker, Windows 7 to 11, not DPI-aware
├─ UiOperation.cs                 Runs the async work of event handlers (ADR 0004); UnexpectedError.cs: error message
├─ Texts.cs                       Results -> texts in the UI language (ADR 0009); each window has its ApplyTexts()
├─ Properties/Resources*.resx     All UI texts: English (neutral), German (.de), French (.fr), and the images
├─ IThemeService.cs               Theme interface (implemented by KryptonThemeService.cs)
├─ InstallationService.cs         The installations found and the selected one (runs the core's discovery)
└─ Resources/                     Images and icon used by the UI
Empire-Earth-WON/                 WON/NeoEE library, no UI (used by the launcher)
├─ NeoApiClient.cs                Client for the NeoEE lobby server
├─ NeoServerEndpoint.cs           Server address and timeout
├─ DeadlineStream.cs              One time budget for a whole request/reply exchange
└─ LobbyPersistentData.cs         Parser for the WON lobby files (_wonlobbypersistent.dat, _wonuser*.dat)
Empire-Earth-Mod/
├─ Empire-Earth-Mod-Lib/          Mod library: ModData (mod description), ModAssets (icon/banners),
│                                 ModPackageBuilder/ModArchiveReader (.eem packages), Windows version detection
│                                 (ZipStorer.cs is a vendored third-party ZIP library)
└─ Empire-Earth-Mod/              Mod creator (WinForms), uses Empire-Earth-Mod-Lib
Empire-Earth-Launcher.Tests/      Unit tests (NUnitLite console program), one folder per tested project:
├─ Architecture/                  Rules for the whole solution (project settings, core dependencies, registry aliases,
│                                 placeholder controls, ApplyTexts, resource parity)
├─ Core/                          The core library, one folder per area
├─ Launcher/                      UI helpers, installation service
├─ Won/                           WON lobby files, NeoEE protocol
├─ Mod/                           Mod library and .eem archives
├─ Fakes/                         In-memory registry and file system, mutex probe, clock, logger (with tests)
└─ TestSupport/                   Temporary folders, chunked streams, repository root, project files
packages/                         NuGet packages, restored on build (not committed)
.github/workflows/build.yml       CI build and test run
```

### Configuration and files

- **Installations** ([contract 1.4](docs/CONTRACT.md#14-discovery-by-the-launcher)): when its window is shown, the
  launcher searches every installation of Empire Earth in the background, from five sources: the folder chosen on
  the *Launcher* page; the install records of community setups since v2
  (`Software\Empire Earth Community\Installations\<NeoEE|EE>`, HKCU, then HKLM 64-bit, then 32-bit view); the
  uninstall keys `{<GUID>}_is1` whose publisher is exactly `Empire Earth Community` or
  `Empire Earth Community & NeoEE` (every community setup, also 1.7.2); the `Installed From Volume` and
  `Installed From Directory` values of `Software\Neo\Empire Earth`, then `Software\SSSI\Empire Earth` (retail, GOG
  and older installations use the SSSI key), each in HKCU, HKLM 32-bit, HKLM 64-bit view (the key comes before
  the hive); and the folder of the launcher or its parent. Entries for the same install folder are one
  installation; `_setupdata_<Product>\install.ini` tells community setups since v2 (also portable ones) apart from
  setups up to 1.7.2 and from other installations. Each installation keeps its real game folders, e.g. `C:\Games\EE`
  of a copy or `D:\Empire Earth` directly below a drive. The *Launcher* page lists them (product, install folder,
  Empire Earth folder, type, state) and uses the chosen one, else the first one found. An installation whose
  `Empire Earth.exe` (or `EE-AOC.exe`) is missing is listed as damaged, never as "not found" (antivirus programs
  often delete or quarantine game files). A hint says when several installations share one set of game settings
  (all EE installations, also retail and GOG, use `Software\SSSI\Empire Earth`), when EE and NeoEE are installed in
  the same folder, and when a setup is newer than the launcher. The search only reads; the log names every
  candidate and why it was used or left out.
- **Game folder**: the folder chosen with "..." on the *Launcher* page (or by picking an installation of the list)
  is saved in `settings.json` (`GameDirectory`, the Empire Earth folder of the installation); it may be the install
  folder, the Empire Earth folder or the Art of Conquest folder, and it stays chosen even if it no longer exists, so
  that the player sees it. *Auto-detect* removes the choice. The launcher reads the WON lobby files from the
  Empire Earth folder of the selected installation; for a game folder below `Program Files`, `ProgramData` or the
  Windows folder it reads the copy in `%LOCALAPPDATA%\VirtualStore\...` first when one exists, because the game
  (a program without a manifest) reads and writes there, the launcher not.
- **Contract with the setup**: [docs/CONTRACT.md](docs/CONTRACT.md) (shared with the Empire Earth Setup
  repository, draft) specifies the install record, the integrity manifest, the per-user default game settings
  and the repair hand-off that launcher v2 is built on.
- **Architecture of v2**: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) describes the target structure (UI-free
  core library, thin WinForms UI on .NET Framework 4.8), data flows, threading, error handling, logging,
  localization and tests; the decisions behind it are recorded in [docs/adr/](docs/adr/README.md). Until v2 is
  complete, this README describes what exists today.
- **User settings** (game folder, theme, custom theme file, UI language) are kept in
  `%LOCALAPPDATA%\Empire Earth Launcher\settings.json` (UTF-8 JSON with a `SchemaVersion`,
  [ADR 0005](docs/adr/0005-own-settings-file-instead-of-user-config.md)), so they survive moving or updating
  the launcher. The file is written as `settings.json.tmp` first and then swapped in, so a crash never leaves
  half a file. A missing file means the defaults. A damaged file (cut off, hand-edited, not JSON) is renamed
  to `settings.json.damaged` (replacing an older copy), logged, and the defaults are used. A file that
  cannot be read, or that a newer launcher wrote, is never overwritten: the launcher uses the defaults and
  changes apply to the running launcher only (logged). Members the launcher does not know are kept when it
  saves. Test builds before v2 kept these settings in .NET's `user.config`; they are not taken over.
- **Server settings**: `NeoServerHost`, `NeoServerPort`, `NeoTimeoutMilliseconds` and
  `PlayerListPollIntervalMilliseconds` are application settings in `Empire Earth Launcher.exe.config`
  (generated from `App.config`) and can be changed there without rebuilding. `NeoTimeoutMilliseconds` limits
  the connect and, separately, the whole request/reply exchange (not only each read). The NeoEE status
  service is plain TCP without TLS or server authentication (a property of the server); the launcher only reads public status
  data from it and validates every reply.
- **Language**: the launcher shows English, German or French: the language of Windows (any other Windows language
  shows English) or the one chosen on the *Launcher* page (`UiCulture` in `settings.json`: empty for Windows,
  `en`, `de` or `fr`), used from the next start on. Only the texts change; numbers and dates keep the Windows
  format. The log names the language in use.
- **Themes**: Krypton palette files (`*.xml`) in the `themes` folder next to the executable, or any file chosen
  with *Custom*. No theme files are shipped yet; without them the designer colors are used (a missing default
  theme `Light` is only logged as information).
- **Log**: `%LOCALAPPDATA%\Empire Earth Launcher\log.txt` (the installation folder may be read-only). English
  messages with ISO time stamps; it never contains CD-key values or WON login data (ADR 0013). Errors of
  background tasks that nobody handled are logged there as well.
- **Registry**: the core opens every HKLM key with an explicit view (64- or 32-bit) and never depends on the
  launcher's own bitness (contract 0). Every change of the registry passes the write policy of
  [ADR 0007](docs/adr/0007-registry-write-scope-and-reg-backups.md): only HKCU keys of an allow-list, and never
  `Software\Sierra\CDKeys` (the NeoEE CD keys), the install records or the uninstall keys, in no hive, view or
  alias. The launcher does not change the registry yet; the game settings follow in a later work package.

## 🔨 Contributing
Pull requests are welcome.\
For major changes, please open an issue first to discuss what you would like to change or discuss with us on Discord.

## 📖 License
[GNU General Public License v3.0](https://github.com/EE-modders/Empire-Earth-Launcher/blob/main/LICENSE)