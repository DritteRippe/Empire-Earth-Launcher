# Changelog

All notable changes to Empire Earth Launcher, its libraries and the mod creator are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html). Nothing has been released yet (all assemblies carry
`0.1.0-alpha`, see `SharedAssemblyInfo.cs`). The fixes of the code review that preceded v2 (branch
`refactor/quality-fixes`) are described in the git history.

## [Unreleased]

Launcher v2 is built on branch `v2` in work packages ([docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), section 15).

### Added

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

- Support for Windows XP, Vista, Windows 8.0 and Windows 10 versions 1507 and 1511, which cannot run the
  .NET Framework 4.8 (Windows 8.0 users can update to 8.1 for free). Windows 7 SP1 stays supported but has not
  been tested yet.
