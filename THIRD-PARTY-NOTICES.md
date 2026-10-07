# Third-party components

Empire Earth Launcher itself is licensed under the [GNU General Public License v3.0](LICENSE).
It uses the following third-party code, which keeps its own license.

## Vendored source code

### ZipStorer

| | |
|---|---|
| File | `Empire-Earth-Mod/Empire-Earth-Mod-Lib/ZipStorer.cs` |
| Author | Jaime Olivares |
| Upstream | https://github.com/jaime-olivares/zipstorer |
| License | MIT (Copyright (c) 2016 Jaime Olivares) |
| Imported | commit `01d0ed6` (2022-06-07, "wip dll for mod management") |
| Upstream version | Not recorded at import time. The file differs from the current upstream `master`, so it is an older release. |

Local modifications (keep this list up to date when touching the file):

- `65c9de2`: added `using System.Threading;`.

The class keeps the upstream namespace `System.IO.Compression`. Treat the file as third-party code:
do not refactor it in place; update it from upstream and record the version here instead.

Known issue of this version: `ExtractFile` keeps reading until the size declared in the central directory
is reached and never stops when the input ends early, so a damaged or crafted archive makes it loop forever.
`ModArchiveReader` therefore extracts into a bounded stream that turns this case into an
`InvalidDataException`. Keep that guard (or check the upstream fix) when updating the file.

## NuGet packages

Restored into `packages/` at build time, not committed. The projects target the .NET Framework 4.8 and
reference the `lib/net48` build of Krypton.Toolkit and the `lib/net45` builds of NUnit and NUnitLite.

| Package | Version | License | Project |
|---|---|---|---|
| Krypton.Toolkit | 5.550.2108.1 | BSD-3-Clause | https://github.com/Krypton-Suite/Standard-Toolkit |

Tests only (`Empire-Earth-Launcher.Tests`, not shipped):

| Package | Version | License | Project |
|---|---|---|---|
| NUnit | 3.14.0 | MIT | https://nunit.org/ |
| NUnitLite | 3.14.0 | MIT | https://nunit.org/ |

Build-time only (CI and the documented command-line build, not shipped):
`Microsoft.NETFramework.ReferenceAssemblies.net48` 1.0.3, the .NET Framework 4.8 reference assemblies by
Microsoft (license named in the package: https://github.com/Microsoft/dotnet/blob/master/LICENSE). Nothing of
it ends up in the build output.

## License texts in binary distributions

Binary copies (including the CI test builds) must carry the license texts of the shipped components:
`licenses/THIRD-PARTY-LICENSES.txt` contains the BSD-3-Clause text of Krypton.Toolkit (with the copyright
line of the package's nuspec) and the MIT text of ZipStorer. The CI copies it next to the executables together
with `LICENSE` and this file. Update it when a shipped component or its version changes.

## Not included: dgVoodoo

The launcher neither contains nor downloads dgVoodoo (dgVoodoo's terms do not allow bundling it in launchers); the community
setup installs it into the game folders. The *Graphics* page only reads its `dgVoodoo.conf`. See the
[THIRD-PARTY-NOTICES.md of the setup](https://github.com/EE-modders/Empire-Earth-Setup/blob/v2/THIRD-PARTY-NOTICES.md) for
the terms.
