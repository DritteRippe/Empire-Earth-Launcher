# 0012 Test strategy

Status: **Accepted** (2026-10-02)

## Context

`Empire-Earth-Launcher.Tests` is a console program (NUnit 3.14 + NUnitLite) with 235 tests on `v2`; CI and
the local verify script run every `*Tests.exe`. v2 adds a large core whose correctness depends on the
contract and on Windows behaviour. Tests must not use the network, the real registry, real user folders or
UI (briefing D6, contract 7).

## Decision

- **Keep one NUnitLite test program** and NUnit 3.14 (now from `lib/net45`). NUnit 4 is possible on 4.8 but
  changes the classic assertions; not worth it now.
- **Layout**: `Core/<Area>/` for the core, `Launcher/` (UI helpers such as `Texts`), `Won/`, `Mod/`,
  `Architecture/`, `Fakes/`, `TestSupport/`.
- **Fakes, not mocks**: hand-written fakes of the platform interfaces (ADR 0006); no mocking library.
- **Contract-driven tests**: every MUST of CONTRACT.md that the launcher implements has at least one test,
  named after the section (e.g. `Discovery_1_4_UninstallKeyByPublisher_...`). The URL policy contains every
  case of the setup's `TestIsAllowedUpdateUrl`. The file class table is compared with the contract list.
- **Golden files** (small text files in the test project, copied to the output) for `.reg` export and the
  diagnostics report.
- **Architecture tests** (run on Mono, read project files and assemblies):
  - the core assembly references no `System.Windows.Forms`, `System.Drawing`, `Krypton.Toolkit`;
  - every `.csproj`: `TargetFrameworkVersion` v4.8, `LangVersion` 8.0, `Deterministic` true;
  - resx parity of `en`/`de`/`fr` (ADR 0009);
  - no source file assigns `ServerCertificateValidationCallback` (ADR 0008);
  - the registry write policy refuses every protected key (ADR 0007).
  The repository root is found by walking up from the test assembly to `Empire-Earth.sln`.
- **Existing tests stay** (WON, mod library, log trimming, lobby profiles, settings recovery semantics);
  tests of moved classes move with them; the four Windows-only tests become platform-independent with `WinPath`.
- **Windows behaviour** that cannot be faked (real registry views, mutex namespaces, shell execute with
  layers, `EnumDisplaySettings`, TLS on Windows 7, Krypton rendering) is covered by `docs/TEST-PLAN.de.md`,
  run by the user on a real laptop.
- Every work package ends with `verify_launcher.sh` green (build of the solution + all tests).

## Evidence

- `verify_launcher.sh` on `v2`: 235 tests, 231 passed, 4 skipped (Windows-only path tests).
- `Empire-Earth-Launcher.Tests/Program.cs`: exit code = number of failed tests, `--noresult` by default;
  CI writes NUnit result files with `--result`.
- README (Tests): "NUnit 4 needs .NET Framework 4.6.2" was the reason for 3.14 under 4.0; with 4.8 the
  choice is free, 3.14 remains supported on net45+.

## Consequences

- The test program grows to cover the core; it stays one executable for CI and the verify script.
- Golden files are text; their line endings are fixed in `.gitattributes` when they are added.

## Alternatives considered

- **A second test project for the core**: two programs to run and maintain, no gain. Rejected.
- **Moq/NSubstitute**: a new dependency; fakes are simpler for stateful registry and file system behaviour.
  Rejected.
- **UI automation tests**: not runnable under Mono, brittle; manual test plan instead. Rejected.
