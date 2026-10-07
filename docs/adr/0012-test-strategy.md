# 0012 Test strategy

Status: **Accepted** (2026-10-02), amended 2026-10-02 (design review; plan review; implementation in L-WP5, L-WP6,
L-WP7, L-WP8 and L-WP9), 2026-10-03 (CI end-to-end test; WinForms tests) and 2026-10-06 (geometry tests; resizable layout;
Play and Launcher pages) and 2026-10-07 (the Graphics and Mods pages), see the
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

## Amendment 2026-10-02 (implementation, L-WP6)

- **`TestIsolationTests`** (`Architecture/`, category `SourceTree`) implements the check of the plan review: it reads
  every source of the test project and fails on `new WindowsRegistry(`, a direct use of `Microsoft.Win32.Registry`
  (`Registry.CurrentUser`, `Registry.GetValue`, ...), `RegistryKey`/`OpenBaseKey`, an HTTP client or request, a socket,
  a DNS lookup, a request to the NeoEE status server, `SpecialFolder.LocalApplicationData` and a store or logger
  created on the launcher's real files (`LauncherPaths.SettingsFile`, ...). Self-tests run every rule against a
  forbidden and against allowed samples (`new SocketException(...)`, reading `LauncherPaths.LogFile` for an
  assertion, a fake named `Registry`).
- **`ProcessRulesTests`** (category `SourceTree`) checks the sources of the launcher and the core for the rules of
  ADR 0010 (see its amendment of L-WP6).
- **The laptop package with `Tests\`** was built once in the scratch folder from the local Release build (like CI):
  `Empire-Earth-Launcher\`, `Empire-Earth-Mod-Creator\`, `Tests\` (the test program and its libraries, no sources),
  `LICENSE`, `THIRD-PARTY-NOTICES.md`, `THIRD-PARTY-LICENSES.txt`, plus a `.sha256` file. Unpacked outside the
  repository, `mono Tests/Empire-Earth-Launcher.Tests.exe --where "cat != SourceTree"` passed 2213 tests with no
  failure (2360 tests in the repository); the tests of the category `SourceTree` fail there, as intended.
- New fakes: `FakeProcessStarter` (records starts and URLs, returns a process id or null, throws a configured
  exception), `FakeProcessList`, `FakeFileVersionReader`; `FakeMutexProbe` is also an `IMutexOwner`, and it and
  `InMemoryFileSystem` can report every probe and existence check into a journal, for the order test of the start.

## Amendment 2026-10-02 (implementation, L-WP7)

- **`FixtureProvenanceTests`** (`Architecture/`, category `SourceTree`) implements the rule of the plan review: every
  token of exactly 64 hex digits (not part of a longer run) in the files of the test project (sources, golden files,
  other fixtures; not the build folders, the project and configuration files) and in `docs/contract-samples/` must be
  the SHA-256 of `sample-<n>` with `n` from 0 to 9999 (`TestSupport/SampleHashes`); `.reg` files are read as UTF-16 LE.
  Its self-tests show that the rule finds another hash and accepts the samples in any case. A local negative test
  (a real hash of a game file in a fixture, not committed) made it fail. The integrity tests compute every other hash
  at run time from synthetic file contents.
- **`docs/contract-samples/`** exists (see the plan review amendment): `install-admin.ini`, `install-user.ini` (with
  `[MissingAfterInstall]`), `install-portable.ini` (without the optional `SetupBuild`), `files.sha256` and `record.reg`
  (the admin record). `.gitattributes` marks the folder `-text`, so git keeps every byte (`git check-attr` shows
  `text: unset`). The test project copies the samples into its output folder, so `ContractSampleTests` (bytes and
  encodings, the readers on every sample, the record sample byte for byte equal to the `RegFileWriter` export of the
  record the discovery tests seed, a computer built from the samples that checks OK) also runs in the laptop package:
  a copy of the test program outside the repository passed 2503 tests with `--where "cat != SourceTree"` and none
  failed (Mono), the sample tests among them; only the test that compares the copied folder with the source tree is
  in the category `SourceTree`. The hand-over to the setup repository is a point of ARCHITECTURE 14.
- **Counted, not timed**: the quick check opens `install.ini`, the manifest and each `code` file once and no `data` file
  (`QuickCheck_Ok_OpensEachFileAtMostOnce_AndHashesNoDataFile`, counting `InMemoryFileSystem`); "does not block" is a
  check whose file read waits on an event while the search, a game start and the window go on
  (`CheckAsync_RunsInTheBackground_AndNeverBlocksAGameStart`, `TheQuickCheck_NeverDelaysTheSearch`).
- New fake: `FakeHttpsClient` (answers from a table, records every URL, can hold a request until it is released or
  cancelled); `TestIsolationTests` keeps the real `HttpsClient` out of the tests. The HTTPS client's handler and limits
  are tested on the objects it creates, without a request.
- The UI pages (*Tools*, the state and version check of *Play*, the repair window asking the update API) are covered by
  the test plan cases WP7-01 to WP7-15.

## Amendment 2026-10-02 (implementation, L-WP8)

- **Real files in a temporary folder.** The maintenance tools move, copy, export and import files, so besides the
  in-memory tests a test of each runs against real files: `TestSupport/MappedFileSystem` puts the real file system
  (`LocalFileSystem`) behind a drive letter, `T:\...` standing for a `TemporaryDirectory` that the test deletes at its
  end, so the core keeps its Windows paths under Windows and Mono alike (`FileBackupTests.MovesRealFiles`,
  `WonLoginResetTests.Reset_MovesRealFiles`, `SavedGamesTests.ExportAndImport_RealFiles` with a saved game whose name
  has an umlaut). No test touches a file outside its temporary folder; `TestIsolationTests` still passes. There is no
  zip test because the zip export and import were dropped.
- **Both blocked cases for every writing action** (ADR 0016): `RegistryCleanupTests.Delete_IsBlockedBySetupAndGame`,
  `WonLoginResetTests.Reset_IsBlockedBySetupAndGame` and `SavedGamesTests.Import_IsBlockedBySetupAndGame` run with each
  setup mutex and each game mutex and check that nothing changed, no backup either; `Export_IsNotBlocked` checks that
  the read-only export is not guarded.
- **Documents checked by tests**: `CleanupCandidatesTests.TheCodeTable_EqualsTheTableOfArchitecture_4_6` (category
  `SourceTree`) parses the table of ARCHITECTURE 4.6 and compares it with the code table row by row (id, key, scope,
  evidence); `TestPlanTests` now requires the cases WP8-xx and accepts no "offen (L-WP8)".
- **UI mapping without a window**: what the registry cleanup shows (`Launcher/CleanupViewTests`: "nothing to clean up",
  the read-only list, the delete button), the model of the maintenance tools (`MaintenanceModelTests`: scans after every
  search and action, no change while a setup runs, "Open backup folder" through the fake shell) and the texts of real
  core results (`MaintenanceTextsTests`, English) are tested with fakes. The page itself, the real VirtualStore of a
  standard user, drive kinds and the Explorer are test plan cases WP8-01 to WP8-16.
- Local negative tests (not committed): letting the cleanup go on after a failed backup file, moving a manifest file in
  the WON reset and enabling the delete button without an offered key made one test fail each.

## Amendment 2026-10-02 (implementation, L-WP9)

- **Fakes for the network**: `FakeNetworkInfo` (adapters and name lookups from a table, every lookup recorded) and a
  fake NeoEE status server (`FakeNeoStatusServer` in `NetworkDiagnosticsTests`) together with `FakeHttpsClient`; the
  network diagnostics, the outage verdict, the hints, the port table and the reading of `NeoEE.cfg`, `WONLobby.cfg` and
  `upnp_info.txt` (also the VirtualStore copy, damaged and oversized files, unknown formats) run on them. The verdict
  has a table test of every combination of name lookup, update API and status server (`OutageHintTests`).
- **Golden file and negative test of the diagnostics report** (ADR 0013 plan review): `DiagnosticsReportTests` builds
  the report of a synthetic computer with the real core components over fakes and compares it with
  `Core/Diagnostics/Golden/DiagnosticsReport.txt` (CRLF, kept by `.gitattributes`); the negative test fills the fakes
  with public, CGNAT and IPv6 addresses, MAC addresses, adapter GUIDs and names, computer, domain, user and player names
  (also in `D:\Users\<name>`, VirtualStore and UNC paths) and a filled `Software\Sierra\CDKeys` (`NOT-A-KEY-0000`),
  and checks that none of them appears in the report. `NetworkDiagnosticsTests.TheLogLines_KeepThePrivacyRules` does the
  same for the log lines. Both were seen failing in local negative tests (not committed): an adapter description with
  the user-chosen name, and an anonymizer that returned paths unchanged.
- **The test plan in its final form**: `TestPlanTests` requires cases for every package up to the one of the line
  "Stand", no "offen (L-WPn)" and no "wird mit L-WPn" anywhere once the plan reached L-WP9, and the table "Vertrag 7"
  with exactly one row per launcher item of CONTRACT.md section 7, each with existing case ids only.
  `ContractChecklistTests` checks the ticked checklist of ARCHITECTURE 15: one ticked row per launcher item, and every
  test class it names exists in the test program. Local negative tests (not committed): an "offen (L-WP9)" in the
  prose, a missing row "Launcher 5", an unknown case id in that table, the WP2 cases renamed, an unticked checklist row,
  an unknown test class and a missing checklist row made the expected test fail each.
- **Network destinations**: `NetworkDestinationTests` (ADR 0008 amendment of L-WP9); `TestIsolationTests` leaves out
  its rule samples as it leaves out its own.
- **The laptop package of L-WP9** (`Empire-Earth-Launcher-v2-L-WP9.zip` with `.sha256`, built in the scratch folder from
  the local Release build like CI, never committed) was checked with `sha256sum -c`, unpacked outside the repository,
  and its `Tests\` ran with `--where "cat != SourceTree"`: 2893 passed, none failed (3071 tests in the repository;
  the 178 others read the source tree). After the package was built, `git status` showed no file in the repository.

## Amendment 2026-10-03 (CI end-to-end test)

The setup repository gets a GitHub Actions workflow that builds the real-data setups on a `windows-latest` runner,
installs, checks and uninstalls them silently in several scenarios and reports green or red in the pull request; the
runner is thrown away after the job, and nothing with game data leaves it. The user tests on GitHub instead of a laptop.
The launcher's part of that job is to run its core against the real installation after each step.

- **A second test program, `Empire-Earth-Launcher.RealMachineTests`.** "A second test project for the core" was rejected
  above because it doubles the work for no gain; this program has another purpose and another safety class: it reads the
  real registry and the real installation, and in some steps writes the game settings of the current Windows account.
  Inside `Empire-Earth-Launcher.Tests` that would break `TestIsolationTests` (the unit tests touch nothing of the
  computer), put code that writes HKCU into the `Tests\` folder of the laptop package (WP1-11), and show the fixtures as
  skipped where the plan expects `Skipped: 0`. As its own program it references only the core, nothing references it,
  and its name ends with `Tests`, so CI and the verify script run it without a filter.
- **Two locks.** The fixtures of the category `RealMachine` are explicit (a run without a filter skips them) and open a
  gate first: ignored unless `EE_LAUNCHER_REAL_MACHINE_TESTS=1`, failed when the switch is set outside Windows or outside a
  GitHub-hosted runner (`RUNNER_ENVIRONMENT` is not `github-hosted`). The self-tests (category `SelfTest`) run the same
  checks on the in-memory fakes of the unit tests, which the program compiles in as links.
- **What it checks, per step, from an expectation file** (strict JSON, schema 1, no hash and no game data in it; README,
  Tests, "Real machine"): the discovery (contract 1.4), the quick and full integrity check (2.5), the status of the
  defaults and the consistency findings (3.3, 3.5, 3.7), on request the defaults of the launcher start, a second start
  and the reset (3.4 to 3.6; R1, R4), and last that the CD keys of every view, the install records, the Inno uninstall
  keys, the compatibility layers, the HKLM game settings and every file of the watched roots are as before the first
  check. Reading checks get read-only wrappers; the defaults write through the launcher's write policy over a recording
  registry and may only change the values of contract 3 of the installations' games in HKCU; files change only in the
  work folder of the step.
- **No hash in the report.** The integrity checker logs every finding with its hashes (contract 2.5), so the core log goes
  to `core.log` in the work folder (never uploaded) and not to the console, findings are printed as `path|class|kind`,
  and every message is redacted; the program contains no 64-hex token. `RealMachineTestRulesTests` (category
  `SourceTree`) keeps the exception narrow: the rules of `TestIsolationTests` hold for every source of the program
  except one line in `Harness/RealAdapters.cs` that creates the real registry adapter; only `RealMachine/` and
  `Program.cs` use the real adapters; every fixture there is explicit, in the category and opens the gate first; no
  source starts a program, asks a server, uses the CD-key registration or a maintenance action, reads a hash out or
  writes to the console outside `Program.cs`; the README example is the one the self-test reads.
- **A pinned commit, not a branch.** The setup workflow checks out a full commit of this repository (`LAUNCHER_COMMIT`,
  or a full commit given by hand) and refuses one that is not on its `LAUNCHER_BRANCH`. The program runs as administrator
  on the runner next to the game data and the built installers, so what runs there must be reviewed code, and a run must
  be reproducible: with a branch, a push here would change every later run of the setup repository without its review.
  A change of the checks takes effect there only when the setup repository updates the pin; the branch is never
  rewritten.
- **Not covered** and still for a real Windows client (the test plan): the UI, a second Windows account, game starts,
  Windows 7 to 11 clients, scaling and the network; the runner is Windows Server without a GPU.

## Amendment 2026-10-03 (WinForms tests, bug report of the red X)

- **Category `WinForms`.** The bug report of 2026-10-03 (red X instead of the status lines of the *Play* page after a
  Windows setting change) was a failure of a control's paint, which no test reached. Tests of the new category create
  WinForms and Krypton controls and pages without showing them and call their `OnPaint` with the graphics of a bitmap, as
  WinForms does for `WM_PAINT` (`TestSupport/WinForms`); an exception there is what makes WinForms draw the red X. No
  window is shown and no message loop runs, so "the tests show no UI" still holds.
- **Where they run.** In CI (Windows), from the `Tests\` folder of the laptop package (they are not `SourceTree`) and with
  the local verify and release scripts, which run the test programs under `xvfb-run` because WinForms on Mono needs a
  display; without one the tests are ignored. Under Mono the pages whose Krypton controls call Windows libraries
  (`uxtheme.dll`, GDI, `user32.dll`) cannot be created, so their cases are ignored there and run on Windows; the *Game
  settings* page and the tests of the label itself run everywhere. The case that documents the cause on .NET Framework
  (`KryptonWrapLabel` keeps the font the palette disposed) is excluded on Mono, whose `Control.Font` takes every new font.
- **What they check.** `LauncherWrapLabelTests` (a private palette instance whose fonts are renewed through the public
  `BaseFontSize`, a palette font disposed later or already disposed, a disposed font set from outside, a failing paint
  with its fallback and its one report, the measuring of the page layouts), `WrapLabelPaintTests` (every wrapping label
  of every page after the global palette renewed its fonts and after the page palette's font was disposed; each must
  draw with its own font, not with the fallback) and `WrapLabelRulesTests` (no `KryptonWrapLabel` and no text measured
  with another control's font in the sources).

## Amendment 2026-10-06 (geometry tests of the pages, bug report with screenshots)

- **Why.** The report of 2026-10-06 (screenshots of the normal and the maximized window) showed the header, the description
  and the "NeoEE in ..." line of the *Game settings* page on top of each other, the book picture of the compatibility
  warning over the two buttons, and content that stays at the left when the window is maximized. No test measured a
  layout, and the stacking arithmetic of the page is correct on its own. The cause was found by driving the page the way
  the launcher does: the main window creates its pages hidden and fills them before it is shown, and `SettingsUserControl`
  skipped every control it had not set visible itself, because `Control.Visible` reads false then. The header, the
  installation line and the two buttons kept the places of the designer, the rest was stacked from the top (the buttons ended
  up under the book picture). The fix is one line (`IsShown`); the tests below fail on all states of the page without it.
- **Geometry tests are not UI automation.** "UI automation tests" stay rejected: nothing is clicked, no window is shown, no
  message loop runs. `PageLayoutTests` creates each of the six pages hidden in a window that is not shown
  (`LauncherPages.Host`, as `MainForm` holds them), gives it the page sizes of the window sizes minimum (554 x 380), 800 x 500,
  1024 x 640 and 1920 x 1080, in English, German and French (`TestUiLanguage`) with the Krypton fonts of the system and 50 %
  larger (`LauncherPages.ScaleFonts`, as Windows "Text size"), and checks the rules of `LayoutChecker` on the bounds of the
  controls after `PerformLayout`: (1) no two visible siblings intersect (a named list of intentional overlays, today the
  link over the empty player list), (2) no child sticks out of its parent (a scrolling parent keeps the scroll bar free),
  (3) every `LauncherWrapLabel` is as high as `TextHeight(Width)`, (4) Krypton buttons, check boxes and labels are as wide as
  their preferred width, (5) every control that fills half of its parent grows with the page, or stays centered. The rules
  compare the page with itself (its own text heights and preferred sizes), never with pixel values of a font, and use the flag
  the control itself carries (`LayoutChecker.IsSelfVisible`), never `Control.Visible`.
- **The Game settings page is driven through its real model.** `SettingsPageWorld` builds `GameSettingsModel`,
  `InstallationService` and `SetupWatcher` on the fake computer of the other tests (`GameSettingsWorld`) and puts the page into
  the states searching, warning shown, question and hint, options (warning confirmed), confirmation (reset clicked) and setup
  running; asynchronous work runs on the test thread (`UiThread`). `GameSettingsPage_HiddenOrVisibleWhileFilled_HasTheSameLayout`
  states the cause of the report directly. The other three pages are created as the designer made them; their states
  follow with the layout work.
- **Self-tests of the rules.** `LayoutCheckerTests` runs every rule against small layouts that break it, so that a rule cannot
  silently find nothing.
- **Mono is a logic check, Windows is the truth.** Only the *Game settings* page can be created under Mono (the other pages
  need `uxtheme.dll` and GDI), with the fonts of Mono and a designer scaling of 7 x 14 instead of 6 x 13, so a page is never made
  smaller than it is after construction there. For that run `PageLayoutTests.KnownDefects` lists the rules the page breaks
  until the layout work of 1.1.0 (resizable main window, pages that stack by text height) has fixed it, each with its reason; a
  listed rule must still be broken in some scenario (`KnownDefects_AreStillBroken`), so the entry is removed with the fix.
  **On Windows no rule is excused**: the first run there is expected to be red for the pages not yet reworked, and shows which
  of the listed defects are real on the fonts of Windows.
- **Page pictures in CI.** `PageScreenshotTests` shows each page for a moment in a borderless window and saves PNG files
  (`DrawToBitmap` and a copy from the screen) of the *Game settings* page in every state, English and German, at the smallest
  size and at 1024 x 640 and once with larger fonts, and of the other pages as designed. The tests do nothing unless the
  environment variable `EE_LAUNCHER_PAGE_PNG_DIR` names a folder and the program runs on Windows, so the laptop package and the
  local run show no window; the build workflow sets it in the step "Render page pictures" (before the tests, never failing
  the build) and uploads the folder as the artifact `page-pictures`. The pictures replace a player's screenshot when a layout
  has to be judged by eye.

## Amendment 2026-10-06 (resizable layout, [ADR 0017](0017-resizable-layout.md))

- **The list of known defects is gone.** With the layout work of 1.1.0 the *Game settings* page keeps every rule of
  `LayoutChecker` at every size, state, language and font under Mono, so `PageLayoutTests.KnownDefects` and
  `KnownDefects_AreStillBroken` were removed; the rules hold without exception. The other pages that can be created on Windows
  only (Tools, Play, Launcher) are measured there; Play and Launcher keep the rules with the amendment below.
- **Two refinements of the rules**, both for controls that must not grow with the window: rule 5 (grows with the page) ignores
  buttons, which have the width their text needs, and ignores the controls of a parent whose width a text of one line dictates
  (a check box or label cannot wrap; then the page scrolls sideways, which rule 2 allows for exactly that case, where the
  widest such control is as wide as its text needs).
- **`ScrollPageLayoutTests`** runs the layout class of the two scrolling pages on a small panel with the kinds of controls the
  pages hold (widths, heights, hidden controls, rows of buttons that wrap, the second pass for a text that cannot wrap, the
  same bounds when it runs twice, the scroll position), because the Tools page cannot be created under Mono. Mutation check: the
  tests for the row wrap and the second pass fail if either is switched off.
- **`MainWindowLayoutTests`** (category `SourceTree`) checks on the sources of `MainForm` what Mono cannot create: the
  navigation buttons are in the panel docked at the left, the six pages are `Dock = Fill` and added before the panel, the
  minimum size is set from the opening size and nothing fixes the window size.

## Amendment 2026-10-06 (Play and Launcher pages in the geometry tests, [ADR 0017](0017-resizable-layout.md))

- **States for the two pages.** `PlayPageWorld` and `LauncherPageWorld` fill a page the way the launcher fills it (created hidden
  in a window that is not shown, then given the texts of a state) with the real texts of the language of the test, so that a
  German translation is as long as the player sees it. `PlayPageState`: designer, long texts of the game group, info bar with
  the display question, both together, state line of the player list; `LauncherPageState`: designer, long game folder, origin,
  hints and restart note. The worlds drive the page through its own methods (`SetText`, `SetShown`, `LayoutPage`) by name, as
  `SettingsPageWorld` does, and fail loudly where a name changed; the models behind the pages are not needed for the geometry.
  `PageScreenshotTests` saves a picture of every state (`page-pictures`).
- **Rules.** Rule 4 (wide enough for the text) covers radio buttons; rule 5 (grows with the page) ignores radio buttons like
  buttons. `LayoutChecker.NarrowByDesign` names the two controls the rule does not apply to, each with its reason (the Play
  button, whose picture Krypton counts twice; the link of the player list, one line in a narrow column), as
  `IntentionalOverlays` names the one overlay; `LayoutCheckerTests` pins both lists.
- **The first layout.** `LauncherPages.Resize` goes through another size when the page already has the requested one: a page
  lays itself out when its size changes, and a page that is created at its size has no change to report. Without this the
  Tools page, which the launcher lays out through its state, was never laid out in the test.
- **Mono.** The Play and Launcher cases need the Krypton combo box, text box and lists and are ignored under Mono (they run
  on Windows: build and laptop). They can be run under Mono with the Windows libraries that Krypton asks for (`uxtheme`, `gdi32`,
  `user32`, `dwmapi`) mapped to a library of stubs that return 0 (`<dllmap>` in `Krypton.Toolkit.dll.config`); every measure is then
  Mono's, so that run proves the logic of the layout and nothing about the fonts of Windows.

## Amendment 2026-10-07 (the Graphics page, launcher 1.1.0)

- **What the page decides is tested without the page.** The list of the Graphics page is a Krypton combo box, which Mono cannot
  create (`CreateCompatibleDC`), so the whole page would be ignored there. `GraphicsView.Of` therefore computes everything the page
  shows (texts, shown and enabled controls, the sizes of the list and the selected one) from `GraphicsModel`, and the page only
  assigns it; `GraphicsViewTests`, `GraphicsModelTests` and `GraphicsTextsTests` run under Mono on the fake computer
  (`GraphicsModelWorld`: `GraphicsModel` on the real `InstallationService`, `SetupWatcher` and `GameSettingsModel`), in English
  and for German and French where the words differ. The model tests also check what must not happen: the files of the game folder
  (`dgVoodoo.conf` included) are the same after a change of the window size, nothing is written while a setup or a game runs, and
  a read that is under way disables the change.
- **The page in the geometry tests.** `GraphicsPageWorld` fills the page through its real model in four states (searching,
  native, dgVoodoo on a scaled screen with the conf from the VirtualStore, the missing conf of the second game and the result of
  a change, setup running); `PageLayoutTests.GraphicsPage_...` runs the rules of `LayoutChecker` on them in the three languages with
  both font sizes, `PageScreenshotTests.GraphicsPage_EveryState_IsSavedAsPicture` writes the pictures of the CI run. Both need
  Windows; under Mono they run with the Windows libraries stubbed (the local aid of the Play and Launcher pages), which proves the
  logic of the layout only.
- **Architecture tests.** `ApplyTextsTests` lists `GraphicsUserControl`, `MainWindowLayoutTests` the fifth page and navigation
  button; `PlaceholderControlsTests` keep the names of the removed placeholders (`resolutionKryptonComboBox`, ...) out of the new
  page.

## Amendment 2026-10-07 (the Mods page, launcher 1.1.0)

- **The state of the page is tested without the page.** `ModsModel` (the read of the presets and of `dreXmod.config`, the
  availability of the page, the two buttons) and `ModsView` (what the page shows) run under Mono on the fake computer
  (`ModsModelWorld`: `ModsModel` on the real `InstallationService` and `SetupWatcher`, a file system that fails the test at the
  first change, a process starter that records what is opened). The readers of the core use synthetic text only, never a copy
  of the data of dreXmod: `CreditsParserTests` (free text, missing lines, `dd/mm/yyyy`), `DreXmodConfigReaderTests` (a comment
  before the first element, tabs, CRLF, LF and CR, element names inside comments, the variant without telemetry, entities,
  missing elements, dreXmod 2, an unclosed comment, UTF-8 and Latin-1), `ModFolderScannerTests` (credits, size, template, limits),
  `DreXmodInfoTests` (the components decide) and `ShellProcessStarterTests` (`OpenFile` opens only `.config`, `.conf`, `.txt`, `.ini` and `.log`
  files and retries with "Open with" when no program is registered).
- **The page in the geometry tests.** `ModsPageWorld` fills the page through its real model in four states (searching, the
  presets of the setup, every text at its longest with the template shown, a config in the VirtualStore that names a folder that
  does not exist and a game without config and folder, and setup running); `PageLayoutTests.ModsPage_...` runs the rules of
  `LayoutChecker` on them in the three languages with both font sizes (the page has no Krypton combo box, so it runs under Mono
  as well), `PageScreenshotTests.ModsPage_EveryState_IsSavedAsPicture` writes the pictures of the CI run.
- **Architecture tests.** `ModsPageRulesTests` keep the sources of the page free of every call that writes and of the paths of
  other files, and allow the model only `OpenFolder` and `OpenFile` of the shell; `ApplyTextsTests` lists `ModsUserControl`,
  `MainWindowLayoutTests` the sixth page and navigation button; `NavigationStackTests` check where the buttons sit when the
  button of the page comes and goes.
