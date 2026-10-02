# Empire Earth Launcher

A launcher for Empire Earth 1, everything inside, no more strange setup, update, patch, manual zip manipulation.\
Coded in C# with the .NET Framework 4 and Krypton UI

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
Windows 98 to 11 Support\
Auto-Update

## 🌐 Download
Sorry, at the moment the launcher is **ABSOLUTELY NOT** available for download in its current state.\
To use the Launcher you will only need the .NET Framework >= 4 on your computer.

## Dev
You just need to clone the repo and open `Empire-Earth.sln` with Visual Studio **2019** or newer (and C#/.NET 4 installed in Visual Studio Installer)\
Some very critical parts of the Launcher can be censored like WON and NeoEE related important operation but most of the reverse WON C# implementation is available 💪

### Building

All projects target the **.NET Framework 4.0** (to keep old Windows versions supported) and are pinned to
**C# 8.0** (`LangVersion` in every `.csproj`), so every contributor compiles the same language with Visual
Studio 2019 or newer, MSBuild or Mono. Do not use newer language features or APIs that do not exist in
.NET 4.0 (no `async`/`await`, no `Task.Run`, ...).

**Visual Studio (Windows)**: open `Empire-Earth.sln` and build. NuGet restores the packages into the root
`packages/` folder automatically.

**Command line (Windows, Developer PowerShell)**:

```powershell
nuget restore Empire-Earth.sln
msbuild Empire-Earth.sln /p:Configuration=Release
```

Visual Studio 2022 no longer installs the .NET Framework 4.0 targeting pack. If MSBuild reports `MSB3644`
(reference assemblies for `.NETFramework,Version=v4.0` not found), build against Microsoft's reference
assemblies from NuGet, exactly like the CI does:

```powershell
nuget install Microsoft.NETFramework.ReferenceAssemblies.net40 -Version 1.0.3 -OutputDirectory $env:TEMP\refasm
$ref = "$env:TEMP\refasm\Microsoft.NETFramework.ReferenceAssemblies.net40.1.0.3\build"
msbuild Empire-Earth.sln /p:Configuration=Release "/p:TargetFrameworkRootPath=$ref" "/p:FrameworkPathOverride=$ref\.NETFramework\v4.0"
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

**Tests**: `Empire-Earth-Launcher.Tests` is a console program built with the solution. It runs its NUnit
tests with NUnitLite (NUnit 3.14, the last NUnit 3 release; NUnit 4 needs .NET Framework 4.6.2) and exits
with 0 only when every test passed (otherwise with the number of failed tests). Run it after building:

```sh
Empire-Earth-Launcher.Tests\bin\Release\Empire-Earth-Launcher.Tests.exe          # Windows
mono Empire-Earth-Launcher.Tests/bin/Debug/Empire-Earth-Launcher.Tests.exe       # Mono
```

NUnitLite options can be passed, e.g. `--where "class =~ LobbyPersistentData"` to run some tests only or
`--result=TestResult.xml` to write an NUnit 3 result file (by default no result file is written). The tests
cover the WON lobby file parser, the NeoEE protocol framing, reply parsing and request deadline, the mod
library (product folders, file types, versions, the working directory of the mod creator, `.eem`
export/import including damaged archives) and the log trimming, game folder detection, lobby profile loading
and recovery from a damaged `user.config` of the launcher. They only use folders below the temporary folder,
never contact a server, never read the registry and never show UI. Tests that depend on Windows path semantics are
marked `[Platform(Include = "Win")]` and reported as skipped under Mono. The launcher makes its internal
helpers visible to the test assembly (`InternalsVisibleTo`), and so does the WON library.

**Continuous integration**: `.github/workflows/build.yml` restores and builds the solution in Release on
`windows-latest` for every push to `main` and every pull request, then runs every `*Tests.exe` it finds in
the `bin/Release` folders (a test program reports failure through a non-zero exit code; finding no test
program fails the build). The NUnit result files are kept as the `test-results` artifact.

**Test builds**: when all tests pass, the CI run also keeps the Release output of both applications for 30 days,
as the artifacts `Empire-Earth-Launcher-testbuild` and `Empire-Earth-Mod-Creator-testbuild` (open the run under
*Actions*, section *Artifacts*; downloading needs a GitHub login). Each zip contains the executables, their
libraries, the French resources, the debug symbols (`.pdb`, for readable crash logs), `LICENSE`,
`THIRD-PARTY-NOTICES.md` and `THIRD-PARTY-LICENSES.txt` (from `licenses/`). Unzip and run the
`.exe`; .NET Framework 4 or newer is required. These are prototype builds for testing, not releases.

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
THIRD-PARTY-NOTICES.md            Vendored code and NuGet dependencies with their licenses
Empire Earth Launcher/            The launcher (WinForms + Krypton UI)
├─ Program.cs                     Entry point and composition root: creates and passes on the services
├─ ILogger.cs                     Logging interface (implemented by TraceFileLogger.cs)
├─ IThemeService.cs               Theme interface (implemented by KryptonThemeService.cs)
├─ GameDirectory*.cs              Detection of the Empire Earth folder
├─ LobbyProfileRepository.cs      Lobby profiles and friends of the game folder, without UI
├─ UserSettingsRecovery.cs        Start-up recovery from a damaged user.config
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
├─ Launcher/                      Log trimming, game folder detection, lobby profiles, settings recovery
├─ Won/                           WON lobby files, NeoEE protocol
├─ Mod/                           Mod library and .eem archives
└─ TestSupport/                   Temporary folders, chunked streams
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
- **User settings** (game folder, theme) are saved by .NET in the user's `user.config` below
  `%LOCALAPPDATA%`. If that file is damaged (e.g. after a crash while saving), the launcher renames it to
  `user.config.damaged`, logs it and starts with the default settings.
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