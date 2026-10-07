# 0002 Keep classic project files and packages.config

Status: **Accepted** (2026-10-02)

## Context

The projects use classic (non-SDK) `.csproj` files with `packages.config` and a solution-level `packages/`
folder. SDK-style projects with `PackageReference` are the current Visual Studio default. The local build
(`verify_launcher.sh`) must keep working, and so must the CI on `windows-latest`.

## Decision

Keep **classic project files** and **`packages.config`**. New projects (the core library) are created in the
same format, with the same property set as the existing ones (`TargetFrameworkVersion`, `LangVersion`,
`Deterministic`, configurations Debug/Release, `AnyCPU`). Shared settings stay per project; an architecture
test checks that they agree (ADR 0012) instead of a `Directory.Build.props` that xbuild does not import.

## Evidence

- The local toolchain has neither `msbuild` nor `dotnet` (`which msbuild dotnet` prints nothing); it builds
  with Mono's `xbuild` and Roslyn 4.2 (`verify_launcher.sh`), and `xbuild` cannot build SDK-style projects
  or restore `PackageReference` (it has no NuGet restore target); restore is done by `mono nuget.exe restore`,
  which handles `packages.config`.
- The spike of ADR 0001 built the retargeted classic projects with that toolchain.
- MSBuild on Windows (CI, Visual Studio 2019/2022, Rider) builds classic projects unchanged.

## Consequences

- Adding a file means adding a `<Compile Include=...>` line; the architecture test cannot catch a
  forgotten line, but the build of a test that uses the class does.
- Package updates edit `packages.config` and the `HintPath`s; `THIRD-PARTY-NOTICES.md` is updated in the same
  commit.
- A later move to SDK-style projects needs a new ADR and a new local toolchain (Mono `msbuild` or .NET SDK).

## Alternatives considered

- **SDK-style projects targeting net48**: shorter project files and `PackageReference`, but not buildable by
  the local verify script; rejected for now.
- **`Directory.Build.props` for shared properties**: imported by MSBuild 15+, not by xbuild, so the two
  builds would differ silently; rejected.
