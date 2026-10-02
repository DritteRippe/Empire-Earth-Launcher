# 0001 Target .NET Framework 4.8

Status: **Accepted** (2026-10-02), amended 2026-10-02 (design review, see the Amendment section)

## Context

All projects target .NET Framework 4.0 (`TargetFrameworkVersion` v4.0 in every `.csproj`), chosen to keep
very old Windows versions. 4.0 has no `async`/`await` support in the BCL, no `HttpClient`, no TLS 1.2 by
default and no `ZipArchive`; the README forbids `Task.Run` for that reason. The setup supports Windows 7 SP1
to 11. The v2 briefing (D4) fixes .NET Framework 4.8 with Krypton.Toolkit 5.550; this record documents the
evidence that the toolchain supports it and what changes.

## Decision

Every project (launcher, core, WON library, mod library, mod creator, tests) targets **.NET Framework 4.8**,
C# `LangVersion` **8.0** stays explicit, no API beyond .NET 4.8 is used. Krypton.Toolkit 5.550.2108.1 is
referenced from its `lib/net48` folder, NUnit and NUnitLite 3.14.0 from `lib/net45`. `App.config` of both
executables declares `<supportedRuntime version="v4.0" sku=".NETFramework,Version=v4.8" />`. The executables
are `AnyCPU` with `Prefer32Bit` false (as today: registry views are always explicit, see ADR 0006).

## Evidence

- .NET Framework 4.8 runs on Windows 7 SP1, 8.1, 10 and 11 and is part of Windows 10 1903 and later and of
  Windows 11 (Microsoft ".NET Framework versions and dependencies"). This matches the setup's range.
- `packages/Krypton.Toolkit.5.550.2108.1/lib/` contains `net48`; the nuspec declares an empty dependency
  group for `.NETFramework4.8`; the net48 assembly has the same identity (`Krypton.Toolkit` 5.550.2108.0) and
  references only `mscorlib`, `System`, `System.Core`, `System.Design`, `System.Drawing`, `System.Windows.Forms`
  and `System.Xml` (`monodis --assemblyref`).
- `packages/NUnit.3.14.0/lib/` and `packages/NUnitLite.3.14.0/lib/` contain `net45`, which NuGet selects for
  net48.
- Mono 6.8 on the build machine has `/usr/lib/mono/4.8-api` and `/usr/lib/mono/xbuild-frameworks/.NETFramework/v4.8`.
- Spike (2026-10-02, scratch copy of `v2` at `2dc6c43`, not committed): all `.csproj` retargeted to v4.8,
  Krypton net48, NUnit net45, `packages.config` `targetFramework="net48"`, plus a probe file using
  `async`/`await`, `Task.Run`, `HttpClient`, `SecurityProtocolType.Tls12` and `.Tls13`. `verify_launcher.sh`:
  `PASS build Empire-Earth.sln (0 C# warnings)`, `Test Count: 235, Passed: 231, Skipped: 4` (the same four
  Windows-only tests as before), generated `TargetFrameworkAttribute(".NETFramework,Version=v4.8")`.
- NuGet has `Microsoft.NETFramework.ReferenceAssemblies.net48` 1.0.3 (flat container index), the 4.8
  counterpart of the net40 package the CI uses today.

## Consequences

- `async`/`await`, `Task.Run`, `HttpClient`, `IProgress<T>`, `ZipArchive`, TLS 1.2/1.3 are available; ADR 0004
  and ADR 0008 build on them. The README's "no async" rule is removed.
- Users of Windows 7 SP1, 8.1 and Windows 10 before 1903 may have to install .NET Framework 4.8 once; the
  runtime shows its own prompt because of `supportedRuntime`. The README says so.
- Windows XP and Vista are no longer supported by the launcher (they were not supported by the setup either).
- The mod creator gets the same target, nothing else changes for it.

## Alternatives considered

- **Stay on 4.0**: no async, no TLS 1.2 by default, no `HttpClient`; every new feature would need workarounds.
- **.NET 6/8 (WinForms on .NET)**: not installed on Windows by default, needs a runtime or a large
  self-contained build, SDK-style projects that the local toolchain cannot build (ADR 0002), and drops
  Windows 7 for .NET 7 and later.
- **4.6.2 or 4.7.2**: same toolchain effort, but not built into current Windows, and 4.8 is the last
  .NET Framework version with the longest support.

## Amendment 2026-10-02 (design review)

The review of the v2 design checked the platform claims against the vendor requirements and the build path
to the user's test laptop.

- **Windows 8.0 is not supported.** .NET Framework 4.8 supports Windows 7 SP1, 8.1, 10 (1607 and later)
  and 11, but not Windows 8.0 (Microsoft, ".NET Framework system requirements"). The setup still starts on
  Windows 8.0 (`MinVersion` 6.1 SP1, `setup_is6.iss` lines 361-366), so the briefing's "same Windows range as
  the setup" holds except for 8.0. The README says: Windows 7 SP1, 8.1, 10, 11; Windows 8.0 users upgrade to
  8.1 (free) to run the launcher.
- **Windows 7 SP1 is "supported, untested".** The laptop test runs on Windows 10/11. Installing .NET 4.8 on
  Windows 7 SP1 has prerequisites that the README lists from Microsoft's installation page (checked when the
  README is written, not assumed here). TLS: Windows 7 SChannel offers TLS 1.2 only on request (ADR 0008) and
  a smaller set of cipher suites than current Windows; whether `api.empireearth.eu` accepts one of them is
  unknown. Because every HTTPS failure ends in the fixed download page (ADR 0008), the launcher stays usable.
  The test plan has an optional Windows 7 VM case (start, Krypton rendering, API request or fallback).
- **The CI path is reproduced locally.** Commits are never pushed in this work (briefing D8), so
  `build.yml` cannot produce the test builds. The first work package therefore adds a local Release build
  that uses the same mechanism as CI: `mono nuget.exe install Microsoft.NETFramework.ReferenceAssemblies.net48
  -Version 1.0.3`, then xbuild `/p:Configuration=Release` with `FrameworkPathOverride` pointing at its
  `v4.8` folder, then the tests from `bin/Release`. The last work package packs the Release builds of launcher
  and mod creator (with `de/` and `fr/` satellite folders, `LICENSE`, `THIRD-PARTY-NOTICES.md`,
  `licenses/THIRD-PARTY-LICENSES.txt`) into a zip with a SHA-256 file for the laptop test (ADR 0012).
