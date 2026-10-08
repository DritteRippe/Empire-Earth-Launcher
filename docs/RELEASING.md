# Releasing the launcher

How a version of Empire Earth Launcher (and the mod creator, which shares its version) is released, from the last
merge to the package that players download. Work through it top to bottom; every box is one step.

Background in one paragraph: the launcher has no download of its own. A release here is a **tag** on `main` with
release notes and no binaries. The binaries are built from that tag and go into the suite installer of the
[setup repository](https://github.com/DritteRippe/Empire-Earth-Setup), which goes into the package
[Empire Earth Community](https://github.com/DritteRippe/Empire-Earth-Community/releases/latest). Versions follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html); all assemblies carry one version (`SharedAssemblyInfo.cs`).

| Step | Where | Result |
|---|---|---|
| [1. Ready to release?](#1-ready-to-release) | this repository | the scope and the tests are clear |
| [2. Version commit](#2-version-commit) | a release branch | every file names the new version |
| [3. Merge and a green build](#3-merge-and-a-green-build) | `main` | a commit with a green run of *Build* |
| [4. Tag](#4-tag) | `main` | `vX.Y.Z` on exactly that commit |
| [5. Release notes](#5-release-notes) | GitHub *Releases* | the release page of the tag |
| [6. Release binaries](#6-release-binaries) | a Windows computer | the output folders, built from the tag |
| [7. Hand-over to the setup and the package](#7-hand-over-to-the-setup-and-the-package) | setup and package repositories | the suite installer and the package |
| [8. After the release](#8-after-the-release) | all three repositories | the record, the pins, the next `[Unreleased]` |

## 1. Ready to release?

- [ ] Every change of the version is merged into `main`, and `## [Unreleased]` of [CHANGELOG.md](../CHANGELOG.md) lists
      it in the right category (Added, Changed, Fixed, Security, ...).
- [ ] The test on real Windows ran: the cases of [TEST-PLAN.de.md](TEST-PLAN.de.md), section 4.1, for this version.
      Releasing with cases that did not run is a decision of the maintainer; it is then written into the test plan
      (section 4.1), the README status and the introduction of the version in the CHANGELOG, case by case, as for 1.1.0.
- [ ] Every new or changed UI text exists in English, German and French; [TRANSLATING.md](TRANSLATING.md) says what is
      not reviewed yet.
- [ ] [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md) and `licenses/THIRD-PARTY-LICENSES.txt` match the shipped
      components and their versions.
- [ ] If the version completes a revision of [CONTRACT.md](CONTRACT.md): both copies are identical
      (`python ci/compare_contract.py <launcher clone>` in the setup repository), and the setup releases its part at the
      same time.

## 2. Version commit

On a short-lived branch (for example `release/1.1.1`), one commit "Set the launcher version to X.Y.Z" that changes
every place with the version number:

| File | What changes | For 1.1.1 |
|---|---|---|
| `SharedAssemblyInfo.cs` | `AssemblyFileVersion` Major.Minor.Patch.0 and `AssemblyInformationalVersion` X.Y.Z; `AssemblyVersion` (the identity the CLR binds to) only with a new Major or Minor; the comment above names what the version is | `1.1.0.0` stays, `1.1.1.0`, `1.1.1` |
| `Empire-Earth-Launcher-Core/Diagnostics/DiagnosticsReport.cs` | the example in the comment of the launcher version | `<c>1.1.1</c>` |
| `docs/TEST-PLAN.de.md` | WP1-04 (`Starting Empire Earth Launcher vX.Y.Z`), WP1-06 (file and product version), WP2-10 | `v1.1.1`, `1.1.1.0`, `1.1.1` |
| `README.md` | the status paragraph, "the launcher is version ..." under Features, "Versioning" under Dev | 1.1.1 |
| `docs/ARCHITECTURE.md` | the row "Status" | 1.1.1 |
| `docs/TRANSLATING.md` | "In short (launcher X.Y.Z)" with the count of texts | 1.1.1 |
| `docs/CONTRACT.md` | only when a revision is released with it: the row of the revision in the history (the tags instead of "(planned)") and the row "Status", in both repositories at once | revision 7 |
| `.github/ISSUE_TEMPLATE/bug_report.yml` | the example of the version | 1.1.1 |

Then a second commit "Document launcher X.Y.Z as released in the CHANGELOG and the README":

- [ ] `## [Unreleased]` becomes `## [X.Y.Z] - YYYY-MM-DD` with a short introduction, and a new empty `## [Unreleased]`
      goes above it. **The date is the day of the tag**, not the day the number was set (1.0.0 first carried the
      wrong one).
- [ ] The links at the end of the CHANGELOG: `[Unreleased]` compares `vX.Y.Z...HEAD`, `[X.Y.Z]` compares the previous
      tag with `vX.Y.Z`.
- [ ] The README status says what is released and what ran on real hardware.

Build and test locally before you push (see [Building](../README.md#building)); the architecture tests read the
README, the CHANGELOG and the test plan.

## 3. Merge and a green build

- [ ] Open a pull request from the release branch into `main`. Merge it with a **merge commit**, not with squash or
      rebase: `docs/CONTRACT.md` ("Based on") and the end-to-end workflow of the setup repository (`LAUNCHER_COMMIT`)
      name commits that must stay on `main`.
- [ ] The push to `main` starts the workflow *Build*. Wait until its run for exactly the merge commit is green
      (*Actions* > *Build*, the run of that commit, all tests passed).

> [!IMPORTANT]
> **Tags do not start a build** (`build.yml` runs for pushes to `main`, pull requests and by hand). Only tag a commit
> that has a green run of *Build* on `main`.

## 4. Tag

```sh
git fetch origin
git switch --detach <merge commit with the green run>
git tag -a vX.Y.Z -m "Empire Earth Launcher X.Y.Z"
git push origin vX.Y.Z
```

- Tags are named `vX.Y.Z` (`v1.0.0`, `v1.1.0`). Annotated tags (`-a`, or `-s` to sign) carry the date and the person
  who tagged; the tags up to `v1.1.0` are lightweight.
- **A published tag is never moved or deleted.** A mistake after the tag gets a new patch version. The package and
  `Quellcode.txt` name the tag and its commit as the source of the binaries.
- `main` is never rewritten either (no force push).

## 5. Release notes

*Releases* > *Draft a new release*, choose the tag `vX.Y.Z`, title `Empire Earth Launcher X.Y.Z`:

- [ ] The notes say what the version is for and where players get it: the package release page.
- [ ] **No branch names.** Branches are deleted after the merge (the notes of 1.1.0 pointed to the branch `v2`, which is
      gone). Link the tag, its commit or a file at the tag, for example
      `https://github.com/DritteRippe/Empire-Earth-Launcher/blob/vX.Y.Z/CHANGELOG.md`.
- [ ] No binaries are attached: the launcher ships in the package.
- [ ] *Set as the latest release*, then *Publish release*.

A template:

```markdown
Empire Earth Launcher X.Y.Z is the launcher of the package "Empire Earth Community" X.Y.Z.

**Players:** get it with the package: https://github.com/DritteRippe/Empire-Earth-Community/releases/latest

Highlights:
- ...

All changes: [CHANGELOG at vX.Y.Z](https://github.com/DritteRippe/Empire-Earth-Launcher/blob/vX.Y.Z/CHANGELOG.md).
Tested on real Windows: see the status in the [README at vX.Y.Z](https://github.com/DritteRippe/Empire-Earth-Launcher/blob/vX.Y.Z/README.md).
Source code: this tag (commit <full SHA>), GPL-3.0.
```

## 6. Release binaries

Build on Windows from the tag, in a clean clone, exactly like the CI (same reference assemblies):

```powershell
git switch --detach vX.Y.Z
git status --porcelain            # must print nothing
nuget restore Empire-Earth.sln
nuget install Microsoft.NETFramework.ReferenceAssemblies.net48 -Version 1.0.3 -OutputDirectory $env:TEMP\refasm
$ref = "$env:TEMP\refasm\Microsoft.NETFramework.ReferenceAssemblies.net48.1.0.3\build"
msbuild Empire-Earth.sln /p:Configuration=Release "/p:TargetFrameworkRootPath=$ref" "/p:FrameworkPathOverride=$ref\.NETFramework\v4.8"
.\Empire-Earth-Launcher.Tests\bin\Release\Empire-Earth-Launcher.Tests.exe   # exit code 0
```

- [ ] `Empire Earth Launcher.exe` > *Properties* > *Details*: file version X.Y.Z.0, product version X.Y.Z (WP1-06).
- [ ] The launcher is `Empire Earth Launcher\bin\Release\` without the `.pdb` files: the program, its `.exe.config`,
      `Empire_Earth_Launcher_Core.dll`, `Empire_Earth_WON.dll`, `Krypton.Toolkit.dll` and the folders `de\` and `fr\`.
      The mod creator is `Empire-Earth-Mod\Empire-Earth-Mod\bin\Release\`.
- [ ] Keep the `.pdb` files of the build: they make the stack traces of a crash report readable.

The test builds of the CI (`Empire-Earth-Launcher-testbuild`) are for testing; a release is built from the tag.

## 7. Hand-over to the setup and the package

The suite installer of the setup repository packages the launcher (setup README, "Suite build script"):

- [ ] `suite\build_suite.ps1 ... -LauncherDir <launcher output> -ModCreatorDir <mod creator output> -LicenseDir <dir>
      -LauncherCommit <full commit of vX.Y.Z>`. The license folder holds `LICENSE`, `THIRD-PARTY-NOTICES.md` and
      `licenses\` of this repository **at the tag**, and the `Quellcode.txt` of the package.
- [ ] `Quellcode.txt` of the package names this repository, the tag `vX.Y.Z` and its commit, never a branch.
- [ ] `BUILD-INFO.txt` of the suite names the same launcher commit.
- [ ] The package "Empire Earth Community" is released after the launcher and the suite: its README and `LIES-MICH.txt`
      name the launcher version and commit. Its release becomes the page that the launcher's "Open release page" and
      the repair advice of a suite installation open (`releases/latest`).

## 8. After the release

- [ ] The test plan (section 4.1), the README status and the CHANGELOG introduction say what ran on real hardware.
- [ ] Delete the release branch.
- [ ] Setup repository: move `LAUNCHER_COMMIT` in `.github/workflows/e2e-realdata.yml` to the commit of the tag (it must
      be on `main`), so that the end-to-end test runs the checks of the released launcher.
- [ ] The description of the repository (*About*) names no branch and no outdated state.
- [ ] Anything left for the next version goes under `## [Unreleased]`.

## What is pinned on purpose

| Pin | Where | Moved by |
|---|---|---|
| NuGet packages | `packages.config` of each project | by hand, with `THIRD-PARTY-NOTICES.md` and the architecture tests |
| .NET Framework 4.8 reference assemblies 1.0.3 | `.github/workflows/build.yml`, README, this file | by hand, everywhere at once |
| GitHub Actions (commit SHA with the version as a comment) | `.github/workflows/build.yml` | Dependabot, one pull request a month |
| The launcher commit of the end-to-end test | `LAUNCHER_COMMIT` in the setup repository | a commit in the setup repository (step 8) |
| The commits of the contract | "Based on" in `docs/CONTRACT.md` (both copies) | a contract revision |
