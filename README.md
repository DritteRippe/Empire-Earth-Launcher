# Empire Earth Launcher

A launcher for Empire Earth 1, everything inside, no more strange setup, update, patch, manual zip manipulation.\
Coded in C# with the .NET Framework 4.8 and Krypton UI

![image](https://github.com/EE-modders/Empire-Earth-Launcher/blob/main/EEL_MainScreen.png)

## 🧾 (Planned) Features
Full Mod System (.eem)\
NeoEE Support & Monitoring\
dreXmod Support\
Discord Presence Support\
Browsable Mods List  
Better Compatibility (DX 9/11/12)\
Auto-Compatibility detector\
Good configuration reporter\
Repair CD-Keys\
Lobby Customizer\
Game Font Customizer\
Game Languages (Voices, Lobby, Campaigns)\
Windows 7 SP1, 8.1, 10 and 11 Support\
Auto-Update

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

The output is in `Empire Earth Launcher\bin\Release\` and `Empire-Earth-Mod\Empire-Earth-Mod\bin\Release\`. If
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
cover the WON lobby file parser, the NeoEE protocol framing, reply parsing and request deadline, the mod
library (product folders, file types, versions, the working directory of the mod creator, `.eem`
export/import including damaged archives) and the log trimming, game folder detection, lobby profile loading
and the user settings file `settings.json` of the launcher (including damaged files). Architecture tests
(`Architecture/`) read the project files, `packages.config`, `App.config` and `app.manifest` files of the source tree and the CI
workflow and check the shared build settings; they find the source tree by walking up from the test program
to `Empire-Earth.sln`, so run the test program from its build folder inside the repository. The tests only
write below the temporary folder, never contact a server, never read the registry and never show UI. Tests
that depend on Windows path semantics are marked `[Platform(Include = "Win")]` and reported as skipped under
Mono. The launcher makes its internal helpers visible to the test assembly (`InternalsVisibleTo`), and so does
the WON library.

**Continuous integration**: `.github/workflows/build.yml` restores and builds the solution in Release on
`windows-latest` for every push to `main` and every pull request, then runs every `*Tests.exe` it finds in
the `bin/Release` folders (a test program reports failure through a non-zero exit code; finding no test
program fails the build). The NUnit result files are kept as the `test-results` artifact.

**Test builds**: when all tests pass, the CI run also keeps the Release output of both applications for 30 days,
as the artifacts `Empire-Earth-Launcher-testbuild` and `Empire-Earth-Mod-Creator-testbuild` (open the run under
*Actions*, section *Artifacts*; downloading needs a GitHub login). Each zip contains the executables, their
libraries, the French resources, the debug symbols (`.pdb`, for readable crash logs), `LICENSE`,
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

**Localization**: texts set from code live in `Properties/Resources.resx` (English) and
`Properties/Resources.fr.resx` (French) of each application; the launcher's navigation buttons are translated
in `MainForm.fr.resx`. Add a language by adding `*.<culture>.resx` files next to them.

### Project layout

```
Empire-Earth.sln                  Root solution containing every project
SharedAssemblyInfo.cs             Version information shared by all assemblies
CHANGELOG.md                      Changes of each version (Keep a Changelog)
THIRD-PARTY-NOTICES.md            Vendored code and NuGet dependencies with their licenses
docs/                             ARCHITECTURE.md (v2 target), CONTRACT.md (shared with the setup), adr/
                                  (decision records), TEST-PLAN.de.md (manual test on Windows, German)
Empire Earth Launcher/            The launcher (WinForms + Krypton UI)
├─ Program.cs                     Entry point and composition root: creates and passes on the services
├─ app.manifest                   Application manifest: asInvoker, Windows 7 to 11, not DPI-aware
├─ ILogger.cs                     Logging interface (implemented by TraceFileLogger.cs)
├─ IThemeService.cs               Theme interface (implemented by KryptonThemeService.cs)
├─ GameDirectory*.cs              Detection of the Empire Earth folder
├─ LobbyProfileRepository.cs      Lobby profiles and friends of the game folder, without UI
├─ LauncherPaths.cs               File locations of the launcher
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
├─ Architecture/                  Rules for the whole solution (shared project settings)
├─ Launcher/                      Log trimming, game folder detection, lobby profiles, settings recovery
├─ Won/                           WON lobby files, NeoEE protocol
├─ Mod/                           Mod library and .eem archives
└─ TestSupport/                   Temporary folders, chunked streams, repository root
packages/                         NuGet packages, restored on build (not committed)
.github/workflows/build.yml       CI build and test run
```

### Configuration and files

- **Game folder**: the launcher reads the WON lobby files from the Empire Earth folder. It uses the folder
  chosen on the *Launcher* page, otherwise the installation registered by the Empire Earth setups
  (`Installed From Volume` + `Installed From Directory` below `Software\Neo\Empire Earth` or
  `Software\SSSI\Empire Earth`, HKCU before HKLM), otherwise its own folder if it contains `Empire Earth.exe`.
- **Contract with the setup**: [docs/CONTRACT.md](docs/CONTRACT.md) (shared with the Empire Earth Setup
  repository, draft) specifies the install record, the integrity manifest, the per-user default game settings
  and the repair hand-off that launcher v2 is built on.
- **Architecture of v2**: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) describes the target structure (UI-free
  core library, thin WinForms UI on .NET Framework 4.8), data flows, threading, error handling, logging,
  localization and tests; the decisions behind it are recorded in [docs/adr/](docs/adr/README.md). Until v2 is
  complete, this README describes what exists today.
- **User settings** (game folder, theme, custom theme file) are kept in
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
- **Themes**: Krypton palette files (`*.xml`) in the `themes` folder next to the executable, or any file chosen
  with *Custom*. No theme files are shipped yet; without them the designer colors are used (a missing default
  theme `Light` is only logged as information).
- **Log**: `%LOCALAPPDATA%\Empire Earth Launcher\log.txt` (the installation folder may be read-only).

## 🔨 Contributing
Pull requests are welcome.\
For major changes, please open an issue first to discuss what you would like to change or discuss with us on Discord.

## 📖 License
[GNU General Public License v3.0](https://github.com/EE-modders/Empire-Earth-Launcher/blob/main/LICENSE)