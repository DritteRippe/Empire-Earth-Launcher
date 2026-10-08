## What and why

<!-- What does this pull request change, and why? Link the issue if there is one ("Fixes #123"). -->

## Checks

- [ ] The build is green: locally (`msbuild` in Release, then `Empire-Earth-Launcher.Tests.exe` with exit code 0) or in the CI of this pull request
- [ ] New or changed behaviour has tests, or the reason why not is written above
- [ ] New or changed UI texts exist in English, German and French (`Resources*.resx`)
- [ ] No new network destination, registry write or program start outside the rules of `docs/ARCHITECTURE.md`

## Changelog and documentation

- [ ] `CHANGELOG.md`: an entry under `[Unreleased]` (not needed for a change nobody notices)
- [ ] README, `docs/ARCHITECTURE.md`, ADRs and the test plan describe the change where it matters

See [CONTRIBUTING.md](https://github.com/DritteRippe/Empire-Earth-Launcher/blob/main/CONTRIBUTING.md) for the details.
