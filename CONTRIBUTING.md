# Contributing to Empire Earth Launcher

Thank you for helping! This project is maintained by volunteers for the Empire Earth community. Reports, tests on real
Windows computers, translations and code are all welcome.

> [!NOTE]
> Just want to play? You do not need this repository: the package
> [Empire Earth Community](https://github.com/DritteRippe/Empire-Earth-Community/releases/latest) installs the games
> and the launcher. Questions about the package go to its
> [issues](https://github.com/DritteRippe/Empire-Earth-Community/issues).

## Ways to help

| You want to ... | Go to |
|---|---|
| Report a bug of the launcher or the mod creator | [New issue](https://github.com/DritteRippe/Empire-Earth-Launcher/issues/new/choose) (the form asks for the logs; see [What do I send with a report?](README.md#-faq-and-known-issues)) |
| Report a bug of the setups or the suite installer | [Empire-Earth-Setup issues](https://github.com/DritteRippe/Empire-Earth-Setup/issues) |
| Report a security problem | **Privately**, see [SECURITY.md](SECURITY.md), never in a public issue |
| Test on a real Windows computer | The German [test plan](docs/TEST-PLAN.de.md); section 4.1 lists the cases that still need a run |
| Improve a translation | [docs/TRANSLATING.md](docs/TRANSLATING.md) (German proof-reading and a French review are open) |
| Change code or documentation | Read on |

For a larger change, please open an issue first, so that we can agree on the approach before you invest the time.

## Before you start

Read [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) (sections 1 to 3) and skim the
[decision records](docs/adr/README.md). The rules that matter most:

- **.NET Framework 4.8 and C# 8.0**, classic project files with `packages.config`
  ([ADR 0001](docs/adr/0001-target-dotnet-framework-4-8.md), [ADR 0002](docs/adr/0002-keep-classic-project-files-and-packages-config.md)).
  No language features or APIs beyond that.
- **The core library has no UI** (`Empire-Earth-Launcher-Core`, [ADR 0003](docs/adr/0003-ui-free-core-library.md)):
  all access to Windows goes through its `Platform` interfaces, so that it can be tested with in-memory fakes.
- **Only working features in the UI** ([ADR 0014](docs/adr/0014-only-working-features-in-the-ui.md)): no placeholders.
- **The launcher never changes game files**, never touches the CD keys (`Software\Sierra\CDKeys`), writes the registry
  only through its allow-list ([ADR 0007](docs/adr/0007-registry-write-scope-and-reg-backups.md)), never asks for
  administrator rights and never downloads or starts a setup.
- **No new network destination** without a decision record: `NetworkDestinationTests` lists every address
  ([ADR 0008](docs/adr/0008-https-policy-and-update-api.md)). No telemetry.
- **Every UI text in English, German and French** (`Resources*.resx`, [docs/TRANSLATING.md](docs/TRANSLATING.md)).
- **What the launcher reads from an installation** is specified in the contract with the setup,
  [docs/CONTRACT.md](docs/CONTRACT.md). It exists identically in the setup repository; a change of it is one change
  in both repositories.
- **Third-party code**: a new NuGet package or vendored file needs an entry in
  [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) and, if it ships, its license text in
  `licenses/THIRD-PARTY-LICENSES.txt`.

## Workflow

1. Fork the repository and create a short-lived branch from `main` (for example `fix/repair-advice-text`).
2. Make the change, with tests (see below) and the documentation it touches.
3. Run the local checks.
4. Open a pull request into `main`. The template asks for what and why, the checks and the docs.
5. The CI workflow (*Build*) builds the solution and runs every test on Windows. A pull request is merged only with a
   green build.

`main` is never rewritten (no force push, no rebase of published commits): the end-to-end workflow of the setup
repository pins a commit of `main`, and the tags of the releases point into it.

## Local checks

Build and test as described in the README, section [Building](README.md#building):

```powershell
nuget restore Empire-Earth.sln
msbuild Empire-Earth.sln /p:Configuration=Release
.\Empire-Earth-Launcher.Tests\bin\Release\Empire-Earth-Launcher.Tests.exe   # exit code 0 = every test passed
```

- Run the test program from its build folder inside the repository: the architecture tests read the sources and the
  documents (the test plan, the README, the CHANGELOG, the ADRs and the contract).
- On Linux or macOS, Mono gives you a compile check and most tests; the WinForms tests and everything that needs real
  Windows run in the CI only.
- New behaviour gets a test in `Empire-Earth-Launcher.Tests` (one folder per project, fakes in `Fakes/`). What only a
  real Windows computer can show gets a case in the German [test plan](docs/TEST-PLAN.de.md); a case id that a
  document names must exist there (`TestPlanTests`).

## Commits

- English, one logical change per commit.
- Subject in the imperative, often with the area first, for example
  `Repair advice: send an installation of the suite to its package` or `Tests: run the scroll position test on Windows`.
- The body explains what changed and why: the problem, the cause, the decision and how it is tested.

## Changelog and documentation

- Every change that a player or a developer notices gets an entry under `## [Unreleased]` in
  [CHANGELOG.md](CHANGELOG.md), in the category where a reader looks for it (*Added*, *Changed*, *Deprecated*, *Removed*,
  *Fixed*, *Security*; [Keep a Changelog](https://keepachangelog.com/en/1.1.0/)).
- Keep the README, [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) and the test plan in step with the code. A decision
  gets a new ADR; a refinement of an accepted one gets a dated *Amendment* section (rules in
  [docs/adr/README.md](docs/adr/README.md)).
- Releases follow the checklist in [docs/RELEASING.md](docs/RELEASING.md).

## License

By contributing you agree that your contribution is licensed under the
[GNU General Public License v3.0](LICENSE), like the rest of the project.

Please be friendly and patient with each other: everybody here gives their free time to an old game they love.
