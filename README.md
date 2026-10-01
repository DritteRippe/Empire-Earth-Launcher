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
Some very critical parts of the Launcher can be censored like WON and NeoEE related important operation but most of the reverse WON C# implementation is availaible 💪

### Building

All projects target the **.NET Framework 4.0** (to keep old Windows versions supported) and are pinned to
**C# 8.0** (`LangVersion` in every `.csproj`), the newest language version Visual Studio 2019 understands.
Do not use newer language features or APIs that do not exist in .NET 4.0 (no `async`/`await`, no `Task.Run`, ...).

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

**Continuous integration**: `.github/workflows/build.yml` restores and builds the solution in Release on
`windows-latest` for every push to `main` and every pull request, then runs every `*Tests.exe` it finds in
the `bin/Release` folders (a test program reports failure through a non-zero exit code).

**Versioning**: the version of all assemblies is maintained in one place, `SharedAssemblyInfo.cs`
(currently `0.1.0-alpha`, nothing has been released yet).

**Line endings**: `.gitattributes` stores text files with LF and checks C#, `.resx` and other Visual Studio
files out with CRLF, so no extra `core.autocrlf` configuration is needed.

### Project layout

```
Empire-Earth.sln                  Root solution containing every project
SharedAssemblyInfo.cs             Version information shared by all assemblies
THIRD-PARTY-NOTICES.md            Vendored code and NuGet dependencies with their licenses
Empire Earth Launcher/            The launcher (WinForms + Krypton UI)
├─ WON/NeoAPI.cs                  Client for the NeoEE lobby server
├─ WON/LobbyPersistentData.cs     Parser for _wonlobbypersistent.dat
├─ Utils/                         UI helpers
└─ Resources/                     Images and icon used by the UI
Empire-Earth-Mod/
├─ Empire-Earth-Mod-Lib/          Mod library: mod data model, .eem packages, Windows version detection
│                                 (ZipStorer.cs is a vendored third-party ZIP library)
└─ Empire-Earth-Mod/              Mod creator (WinForms), uses Empire-Earth-Mod-Lib
packages/                         NuGet packages, restored on build (not committed)
.github/workflows/build.yml       CI build
```

## 🔨 Contributing
Pull requests are welcome.\
For major changes, please open an issue first to discuss what you would like to change or discuss with us on Discord.

## 📖 License
[GNU General Public License v3.0](https://github.com/EE-modders/Empire-Earth-Launcher/blob/main/LICENSE)