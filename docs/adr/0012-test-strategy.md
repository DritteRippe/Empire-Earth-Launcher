# 0012 Test strategy

Status: **Accepted** (2026-10-02), amended 2026-10-02 (design review, see the Amendment section)

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

## Amendment 2026-10-02 (design review)

- **Laptop package**: the last work package builds Release locally with the CI mechanism (ADR 0001
  amendment) and packs launcher and mod creator with satellites and licenses into a zip plus `.sha256` in the
  scratch folder, never in the repository. `docs/TEST-PLAN.de.md` says how the package gets onto the laptop and
  how to build it on Windows with Visual Studio/MSBuild instead.
- **The test plan grows with the code**: the first work package creates `docs/TEST-PLAN.de.md` (prerequisites,
  getting the package, where log, settings and backups are); every later package adds its cases in the same
  commit. That is an acceptance criterion of each package.
- **Only checkable acceptance criteria**:
  - "does not block the window" -> a test that `DiscoverAsync` with a blocking fake registry call returns an
    unfinished task at once;
  - performance -> counted, not timed: the quick check opens every listed file at most once and hashes no
    `data` file (counting fake file system);
  - "no designer layout lost" -> a script check that the designer diff contains only removed placeholder
    controls (with their field declarations and `Controls.Add` lines) and text assignments, plus screenshots in
    the test plan;
  - "every cleanup entry has evidence" -> unit test on the `Evidence` field (ADR 0007 amendment);
  - "no placeholders" -> architecture test with the list of removed control names (ADR 0014 amendment).
- **More architecture and contract tests**: the broader certificate test (ADR 0008 amendment); comparisons and
  upper-casing under `CurrentCulture` `tr-TR`; `InMemoryRegistry` has a "32-bit Windows" mode (HKLM64 is
  HKLM32) and discovery yields no duplicates there; the registry alias table (ADR 0007 amendment).
