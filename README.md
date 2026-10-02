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

- Play Empire Earth or The Art of Conquest of the selected installation: the launcher waits while a setup runs, does
  not start a game twice (and explains how to end a hanging one in the Task Manager), asks before starting the second
  game, keeps the "Installed From" values of the game in step, starts the program through Windows so that its
  compatibility settings apply (also "Run as administrator"), shows the file versions of both programs and logs every
  start
- Integrity check of community installations since setup v2: at every start and after a setup, in the background, the
  launcher checks that every file of the setup's list is there and that the program files are unchanged; the *Play*
  page shows the state ("Files: OK", "damaged", "incomplete", ...), the new *Tools* page explains it, lists every
  missing or changed file and runs the full check of all game data on request (with progress and cancel). It only
  reads: it never changes, deletes, restores or downloads a game file, never blocks Play and gives way to a running
  setup (forum: antivirus programs that delete game files)
- Repair advice when a program is missing, files are damaged or an update is available: what to do with the community
  setup (antivirus exception first, same folder and install mode, keep the NeoEE CD-key task), the files concerned, and
  the download of the current setup as the community's update API names it (only `https` addresses of the project,
  else `https://empireearth.eu/download`, with the reason); the launcher never downloads, starts or elevates the setup
  itself
- Version check on request (*Play* page: the game; *Tools* page: game and setup) against the update API of the
  community setup, for community installations also of setups up to 1.7.2 (forum: version conflicts in multiplayer)
- One launcher at a time (a second start says so and ends)
- NeoEE online player list, with the lobby profiles and friends of the game folder (also when the game keeps them
  in the VirtualStore)
- Detection of every Empire Earth installation: community setups (current and up to 1.7.2, admin, user and
  portable), retail, GOG and copies with their real folders; the *Launcher* page lists them with their state
  (a missing `Empire Earth.exe` shows as damaged) and lets the player choose one or another folder
- Game settings for your Windows account: the recommended defaults of the community setup for a second Windows
  account or after a manual install, one question before your display settings are replaced, "Apply recommended
  display", a reset with a `.reg` backup that restores your previous settings on a double-click, hints when settings
  do not fit together (bit depths, 16 bit, renderer, window larger than the screen at 150 % scaling, screen lower
  than 768 pixels, a folder name the game cannot read), and the compatibility options for your account from Windows 8
  on (*Settings* page; the *Play* page shows a hint bar)
- Themes (Krypton palette files)
- English, German and French user interface, following Windows or chosen on the *Launcher* page
  ([docs/TRANSLATING.md](docs/TRANSLATING.md))
- Mod creator for `.eem` mod packages
- Windows 7 SP1, 8.1, 10 and 11 with the .NET Framework 4.8 (see [Requirements](#requirements))

**Coming with v2** (each with its work package, ARCHITECTURE section 15)

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
- Writing the GPU driver version into the log (the diagnostics report names the display adapter)

**No telemetry**: the old checkbox "Allow us to collect diagnostic data" is gone. The launcher collects no usage or
diagnostic data. Its connections are the request for the NeoEE player list and, only when you ask for it (repair
advice, version check), an HTTPS request to `api.empireearth.eu` that sends nothing but the AppId of the installation
and the version; the download page opens in your browser only when you click its button. CD keys are repaired by
re-running the community setup; the launcher never touches them.

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
  names), the `.reg` writer (golden files of every value type in `Core/Backup/Golden/`) and the backup folder, and the
  game settings (`GameSettings/`: the value table against contract 3.2, the computed values, the marker, the first
  run, the display question, the reset and the restore of its backup by import, the consistency checks at 100 % and
  150 % scaling, the hidden hints, the compatibility options and the launcher's write policy, every writing action
  blocked by a running setup and game), Play (`Play/`: the order of `GameStarter` with fakes and with the real game
  settings, every refusal and start error as a result, the setup watcher with a fake clock, the single instance, the
  file versions), the integrity check (`Integrity/`: the manifest reader with BOM, CRLF, uppercase hex, the binary
  marker and every unsafe path, the file classes against the table of contract 2.4, the state table of 2.5 with the
  uninstall key rule, NeoEE wording, two products in one folder, the counted cost of the quick and full check, a
  setup that starts during a check, and that the check never writes), the repair hand-off (`Repair/`: the advice, the
  URL policy with the 13 cases of the setup's `TestIsAllowedUpdateUrl` and the launcher's own, the download locator
  with every fallback, the version check), the byte samples of `docs/contract-samples/` read by the launcher's
  readers (`Contract/`), the HTTPS client's settings, the online player list poller (`Lobby/`) and the start
  information of the shell starter;
- the UI helpers of the launcher (`Launcher/`): `UiOperation`, the "unexpected error" message, the logging of
  unobserved task exceptions, the texts chosen for results (`Texts`), the UI language applied at start, the
  installation service (also while a setup runs), the models of the game settings pages (`GameSettingsModel`), of
  the Play page (`PlayModel`), of the integrity check (`IntegrityModel`: the quick check after every search and after a
  setup, never while one runs, cancel) and of the update API (`UpdateModel`), and the message of a second launcher;
- the WON lobby file parser, the NeoEE protocol framing, reply parsing and request deadline (`Won/`), and the mod
  library (`Mod/`: product folders, file types, versions, the working directory of the mod creator, `.eem`
  export/import including damaged archives);
- architecture rules (`Architecture/`): the shared build settings of every project, the core's dependencies (no
  WinForms, `System.Drawing` or Krypton; only the BCL and the WON library), a table test that the registry
  write policy refuses `Software\Sierra\CDKeys`, the install records and the uninstall keys under every alias
  (`WOW6432Node`, registry VirtualStore, `/`, case) and every ancestor, for every operation, and the UI texts:
  the removed placeholder controls stay removed (`PlaceholderControlsTests`), every designer text is set again
  in `ApplyTexts()` (`ApplyTextsTests`), and English, German and French have the same texts, placeholders and
  built satellite assemblies (`ResourceParityTests`); and the test plan (`TestPlanTests`): unique case IDs, the
  cases of the current work package, every case named in the documents exists, and the mapping of the requirements
  and forum test cases is complete; the tests themselves touch no registry, network or launcher file
  (`TestIsolationTests`), and only the shell starter starts programs, never without the shell or elevated, and
  nothing ends a process (`ProcessRulesTests`); no source overrides the certificate check
  (`NoCertificateOverrideTests`), names an old or explicit TLS version or sets it outside `Program`
  (`TlsSettingTests`), and every SHA-256 in the fixtures and the contract samples is the hash of a synthetic text
  (`FixtureProvenanceTests`).

Architecture tests read the project files, `packages.config`, `App.config`, `app.manifest`, the core's sources,
the launcher's designer files, code and `.resx` files, `docs/CONTRACT.md`, ADR 0014, the test plan and the other
documents, the CI workflow and the built satellite assemblies (the tests that read the documents carry the category
`SourceTree`); they find the source tree by walking up from the test program to
`Empire-Earth.sln`, so run the test program from its build folder inside the repository; a copy outside it (the
`Tests\` folder of the laptop package) runs the others with `--where "cat != SourceTree"`. The tests use fakes
(`Fakes/`: in-memory registry with both HKLM views and a 32-bit Windows mode, in-memory file system with Windows
path rules, mutex probe and owner, clock, logger, system information with Windows version, Wine, screen and code
page, process starter, process list, file versions, an HTTPS client that answers from a table) and
only write below the temporary folder; they never contact a server, never touch the real registry or
`%LOCALAPPDATA%` and never show UI. Path logic is `WinPath` string logic, so every
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
                                  TRANSLATING.md (languages, how to translate), contract-samples/ (byte samples
                                  of install.ini, files.sha256 and the install record, shared with the setup)
Empire-Earth-Launcher-Core/       UI-free core library of the launcher (Empire_Earth_Launcher_Core.dll, ADR 0003):
│                                 only the BCL and the WON library, no WinForms, System.Drawing or Krypton
├─ Contract/                      Fixed names of docs/CONTRACT.md: products, games, keys, files, contract version;
│                                 CompatibilityLayers (the entries of contract 3.7 and the old values)
├─ Backup/                        RegFileWriter (.reg files like regedit's), RegistryExport, BackupLocations
│                                 (%LOCALAPPDATA%\Empire Earth Launcher\Backups, ADR 0007)
├─ GameSettings/                  Contract 3: GameSettingsTable, ComputedValues, GameDefaultsService (marker, first
│                                 run, display question, Installed From, reset), ConsistencyChecker, HintVisibility,
│                                 CompatibilityOptions, LauncherWritePolicy (the launcher's allow-list)
├─ Integrity/                     Contract 2: ManifestReader (files.sha256), FileClassifier (code, mutable, data),
│                                 IntegrityChecker (quick and full check, cancelled by a setup), IntegrityReport
├─ Installations/                 Discovery of the installations (contract 1.4): install records, install.ini,
│                                 uninstall keys, "Installed From" values, launcher folder -> InstallationDiscovery
│                                 -> Installation with real EE/AoC folders (ADR 0015); EffectivePathResolver
│                                 (VirtualStore copies of game files, ADR 0016)
├─ Platform/                      Windows behind interfaces (ADR 0006): IRegistry/WindowsRegistry (explicit views),
│                                 RegistryLocation, RegistryValue, RegistryPath (canonical form), RegistryWritePolicy
│                                 and PolicyCheckedRegistry (protected keys, allow-list, ADR 0007), IFileSystem/
│                                 LocalFileSystem, WinPath (Windows path rules), IMutexProbe/WindowsMutexProbe,
│                                 IMutexOwner/WindowsMutexOwner (single instance), IClock, ISystemInfo/WindowsSystemInfo
│                                 (Windows version, Wine, screen size, code page), IProcessStarter/ShellProcessStarter
│                                 (shell execute), IProcessList, IFileVersionReader, IHttpsClient/HttpsClient
│                                 (no redirects, 10 s, 4 KiB, certificate check of Windows, ADR 0008)
├─ Logging/                       ILogger, TraceFileLogger (log file with trimming)
├─ Settings/                      LauncherSettings, SettingsStore (settings.json, ADR 0005), LauncherPaths,
│                                 UiLanguage (the language setting, ADR 0009)
├─ Lobby/                         LobbyProfileRepository: lobby profiles and friends of the game folder (effective paths);
│                                 PlayerListPoller: the online player list (async loop, ADR 0004)
├─ Play/                          GameStarter (ADR 0010: setup, game, program, Installed From, first run, shell start),
│                                 RunningGameDetector, SetupWatcher (setup mutexes every 2 s), ProgramVersions,
│                                 SingleInstance, MutationGuard (no change while a setup or a game runs, ADR 0016)
└─ Repair/                        RepairAdvice (the steps of contract 4.4), UpdateUrlPolicy (the setup's
                                  IsAllowedUpdateUrl), SetupDownloadLocator (contract 4.3), UpdateChecker (4.5)
Empire Earth Launcher/            The launcher (WinForms + Krypton UI)
├─ Program.cs                     Entry point and composition root: creates and passes on the services
├─ app.manifest                   Application manifest: asInvoker, Windows 7 to 11, not DPI-aware
├─ UiOperation.cs                 Runs the async work of event handlers (ADR 0004); UnexpectedError.cs: error message
├─ Texts.cs                       Results -> texts in the UI language (ADR 0009); each window has its ApplyTexts()
├─ Properties/Resources*.resx     All UI texts: English (neutral), German (.de), French (.fr), and the images
├─ IThemeService.cs               Theme interface (implemented by KryptonThemeService.cs)
├─ InstallationService.cs         The installations found and the selected one (runs the core's discovery)
├─ GameSettingsModel.cs           State and actions of the game settings for the Settings and Play pages
├─ PlayModel.cs                   State and actions of the Play page (game choice, versions, start)
├─ IntegrityModel.cs              The integrity check of the selected installation (quick check, full check)
├─ UpdateModel.cs                 The update API: version check, download of the repair advice
├─ ToolsUserControl.cs            The Tools page (integrity, repair advice, version check)
├─ RepairAdviceDialog.cs          The repair advice window (built in code, wraps every language)
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
│                                 placeholder controls, ApplyTexts, resource parity, test plan, test isolation,
│                                 process starts, TLS and certificate rules, synthetic fixture hashes)
├─ Core/                          The core library, one folder per area
├─ Launcher/                      UI helpers, installation service, page models, start of a second launcher
├─ Won/                           WON lobby files, NeoEE protocol
├─ Mod/                           Mod library and .eem archives
├─ Fakes/                         In-memory registry and file system, mutex probe, clock, logger, process starter,
│                                 process list, file versions, HTTPS client (with tests)
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
  and the repair hand-off that launcher v2 is built on. `docs/contract-samples/` holds synthetic byte samples of
  `install.ini` (admin, user with `[MissingAfterInstall]`, portable), `files.sha256` and the install record as a `.reg`
  file, which the launcher's readers are tested against; the setup repository is to take the same folder over.
- **Integrity check** ([contract 2](docs/CONTRACT.md#2-integrity-manifest)): after every search of the installations
  the launcher reads `_setupdata_<Product>iles.sha256` and `install.ini` of the selected community installation (setup
  v2 or later) and checks in the background that every listed file exists and that the program files (`exe dll asi
  ...`, the class `code` of contract 2.4) have their SHA-256; "Check all files" on the *Tools* page also hashes the game
  data. Changed `cfg ini conf config log` files are never reported. The states: OK; Modified (only information: mods,
  HD packs); Incomplete (a data file missing); Damaged (a program file missing or changed; for NeoEE worded "changed
  since the installation", because the NeoEE updater may replace files); Unknown (installed by a setup up to 1.7.2, a
  newer setup, an older setup ran later, the setup could not write its list); a foreign installation is not checked.
  The check opens every file at most once with sharing that lets a setup delete and rename it, does not start while a
  setup runs and stops when one starts; it never blocks Play and logs every finding with path, class, expected and
  actual hash.
- **Update API** ([contract 4.3, 4.5](docs/CONTRACT.md#4-repair-hand-off),
  [ADR 0008](docs/adr/0008-https-policy-and-update-api.md)): only on request, `GET
  https://api.empireearth.eu/setup/?product=<AppId>` (plus `&type=game|setup&version=<version>` for the version check)
  over HTTPS with the certificate check of Windows, no redirects, 10 seconds and at most 4 KiB; on Windows 7 TLS 1.2 is
  requested explicitly, elsewhere Windows chooses. A download address is used only if it passes the setup's own URL
  check; every failure gives `https://empireearth.eu/download`, and the window and the log say why.
- **Architecture of v2**: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) describes the target structure (UI-free
  core library, thin WinForms UI on .NET Framework 4.8), data flows, threading, error handling, logging,
  localization and tests; the decisions behind it are recorded in [docs/adr/](docs/adr/README.md). Until v2 is
  complete, this README describes what exists today.
- **Play** ([ADR 0010](docs/adr/0010-game-start-and-mutex-probing.md)): *Play* starts the chosen game of the selected
  installation (`LastGame` in `settings.json`) in this order: no setup may run (`EE_Setup`, `NeoEE_Setup`), the same
  game may not run (its mutex), the other game running asks first, the program must exist (else the repair advice),
  then the "Installed From" values are synchronized and the first run of the defaults is done, then the program
  starts through the Windows shell in its game folder. The log has one line per start with the process id. While a
  setup runs (checked every two seconds), nothing is started, changed or searched; the installations are searched
  again when it has ended. Only one launcher runs per Windows session (mutex `EmpireEarthCommunityLauncher`).
- **User settings** (game folder, theme, custom theme file, UI language, hints hidden from the *Play* page, last game) are kept in
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
  [ADR 0007](docs/adr/0007-registry-write-scope-and-reg-backups.md): only HKCU keys of an allow-list with their
  value names (`LauncherWritePolicy`: the game settings keys of contract 3.1 with the values of 3.2, the defaults
  marker, the GPU preference and the compatibility values of the game programs), and never
  `Software\Sierra\CDKeys` (the NeoEE CD keys), the install records or the uninstall keys, in no hive, view or
  alias. Nothing is changed while a setup or a game runs (ADR 0016). HKLM is only read.
- **Game settings** ([contract 3](docs/CONTRACT.md#3-per-user-default-game-settings)): after each search the launcher
  sets up the recommended game settings for the Windows account that runs it, once per game and account (the
  marker `HKCU\Software\Empire Earth Community\GameDefaults\<NeoEE|EE>`, values `EE` and `AoC`), but only for an
  installation that is the only one using its settings key; it creates missing values only, and asks before it
  replaces display settings that differ. Values outside the table of the contract (player names and the like) are
  never touched. The *Settings* page shows the state, the hints and the compatibility options; its reset and
  "Apply recommended display" write a backup first.
- **Backups**: `%LOCALAPPDATA%\Empire Earth Launcher\Backups\<yyyy-MM-dd_HHmmss>_<action>\` with one `.reg` file per
  game (`<time>_<NeoEE|EE>_<EE|AoC>.reg`, or `<time>_Layers.reg` for the compatibility values). Double-click a file
  (or `reg import <file>`) to restore the settings exactly as they were before, including the removal of values the
  action created. The launcher never deletes backups.

## 🔨 Contributing
Pull requests are welcome.\
For major changes, please open an issue first to discuss what you would like to change or discuss with us on Discord.

## 📖 License
[GNU General Public License v3.0](https://github.com/EE-modders/Empire-Earth-Launcher/blob/main/LICENSE)