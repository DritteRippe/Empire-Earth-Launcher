# 0012 Test strategy

Status: **Accepted** (2026-10-02), amended 2026-10-02 (design review; plan review; implementation in L-WP5), see the
Amendment sections

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

## Amendment 2026-10-02 (plan review)

- **Synthetic fixtures only** (briefing D6). Golden files, contract samples and other test data contain only
  synthetic file names (`file0001.dll`, the program and folder names of the contract), hashes that are the SHA-256
  of the ASCII text `sample-<n>` (`n` from 0 to 9999) or of a file the test itself creates, and values that are
  obviously no keys (`NOT-A-KEY-0000`). Nothing comes from the reconstructed game data, the official installers or
  their dumps. `FixtureProvenanceTests` checks that every token of 64 hex digits in the fixture folders of the test
  project and in `docs/contract-samples/` is such a synthetic hash; every package that adds fixtures names this in
  its review.
- **Shared contract samples.** `docs/contract-samples/` holds `install.ini` of the three install modes (one with
  `[MissingAfterInstall]`), `files.sha256` and the registry record as a `.reg` file, with exactly the bytes that
  contract 1.1, 1.2, 2.2 and O3 describe (ASCII; the manifest with LF, `install.ini` with CRLF; `.gitattributes`
  keeps the bytes of the folder unchanged). The launcher's readers are tested against exactly these files, and
  the record sample is compared with the `RegFileWriter` export of the record seeded by the discovery tests. The
  folder is meant to exist identically in the setup repository, whose unit tests produce the same bytes with the
  real writer functions and whose `ci/compare_contract.py` compares the folder as it compares the contract;
  changing a sample is a step in both repositories, like a contract change. The launcher adds the folder first
  (L-WP7) and hands it to the setup work (ARCHITECTURE 14).
- **The tests also run on real Windows.** The laptop package contains `Tests\` (the test program and its
  libraries, no sources). The tests that read the source tree (`RepositoryRoot`) are in the category
  `SourceTree`; the test-plan case runs the rest with `--where "cat != SourceTree"` and a `--result` file, and
  expects `Failed: 0` and exit code 0. This runs the serializer, `File.Replace`, the mutex probe and the satellite
  assemblies on .NET Framework 4.8 instead of Mono. It is safe: the tests use only temporary folders and mutexes
  with random names (checked: the test project constructs no `WindowsRegistry`, no HTTP client and no socket, and
  never uses the real `%LOCALAPPDATA%`); an architecture test keeps it so. Smoke tests of the registry adapter
  against a private hive (`RegLoadAppKey`) are not planned for v2; the test plan covers the adapters.
- **Test plan coverage test.** `TestPlanTests` (category `SourceTree`, from L-WP5) reads `docs/TEST-PLAN.de.md`:
  case ids are unique; its mapping table assigns every requirement R1 to R10 and R17 and every forum test case 1
  to 22 (forum report section 8) to case ids, to "offen (L-WPn)" until that package, or to "Setup"/"entfällt" with
  a reason; every case id it names exists. From L-WP9 on, no "offen" is allowed.

## Amendment 2026-10-02 (implementation, L-WP5)

- **`TestPlanTests`** (`Architecture/`, category `SourceTree`) implements the coverage test of the plan review: the
  case ids (`WPn-nn`, `W7-nn`) of `docs/TEST-PLAN.de.md` are unique; the package named in the plan's "Stand" line has
  its own section with cases; every case id named in the plan, the README, the CHANGELOG, `ARCHITECTURE.md`,
  `TRANSLATING.md` and the ADRs exists; the mapping table of section 7 has every requirement R1 to R10 and R17 and
  every forum test case 1 to 22 exactly once; each of its assignments is a case id, "offen (L-WPn)" for a package after
  the plan's state (none from L-WP9 on), or "Setup: ..." / "entfällt: ..." with a reason. A self-test runs the rules
  against broken samples.
- **Category `SourceTree` for every test that reads the source tree.** The plan review gave the category to the tests
  that use `RepositoryRoot`; L-WP5 adds it to the existing ones too (project conventions, resource parity,
  `ApplyTexts`, contract names as whole fixtures; the core dependency, placeholder and discovery rule tests only on the
  methods that read files, so their checks of the built assemblies still run on the laptop). `RepositoryRoot` fails a
  test method that reads the source tree without the category (on the method or its fixture); test case sources and
  one-time set-ups are not checked by it, which is why fixtures that read files there carry the category as a whole.
  A copy of the test program outside the repository runs `--where "cat != SourceTree"` with 2073 passed tests and no
  failure (Mono, L-WP5).
- **Golden files** for the `.reg` writer are in `Core/Backup/Golden/` (copied to the output folder); they hold only
  synthetic names and values (`FixtureProvenanceTests` comes with L-WP7, when the first hashes are added).
- **Value table against the contract.** `GameSettingsTableContractTests` reads the table of contract 3.2 and compares
  name, key, type, class and data of every row with `GameSettingsTable`; `CompatibilityLayersTests` reads the rows and
  the old values of 3.7. In addition, a one-off check outside the repository compared the table with the
  `GameSettings` block of the setup's `setup_is6.iss` (27 values, flags, data per game, the window limits, the
  rasterizer lines, the "Installed From" formula and the settings keys of `config_ee.iss`/`config_neoee.iss`): no
  difference.
- **Windows adapters** (`WindowsSystemInfo`, the `.reg` import by double-click, the UI) are covered by the test plan
  cases WP5-01 to WP5-20.
