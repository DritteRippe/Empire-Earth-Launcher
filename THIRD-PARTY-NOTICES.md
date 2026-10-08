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
| Upstream version | 3.7.0. The imported file matches `src/ZipStorer.cs` of upstream commit `8443b65` (2021-04-30), released as ZipStorer 3.7.0 and unchanged in 3.8.0 (2021-11-25), except for the preprocessor header (see the local modifications). |

Local modifications (keep this list up to date when touching the file; the header of `ZipStorer.cs` lists the same):

- `01d0ed6` (import): the preprocessor header. `NOASYNC` is defined unless `NET45_OR_GREATER`, which the classic
  project files do not define, so the synchronous code path is compiled (`AddStreamAsync` and `ExtractFileAsync` are
  private); the `using` directives moved below it.
- `65c9de2`: added `using System.Threading;`.
- `7823a01`: `ReadExtraInfo` reads only the extra field of its own entry. Backport of the upstream fix of issue #71
  (commit `1948dcf`, ZipStorer 4.x): before, some archives that ZipStorer had written itself could not be read, and
  entries got the times of other entries.
- `133391b`: `ReadFileInfo` rejects an archive whose central directory does not lie before its end records (a damaged or
  crafted size was allocated as given, up to 2 GB).

Tests: `ZipStorerTests` has one test per local fix, and `ZipStorerUseTests` keeps the use of the class to what is safe
(see below).

The class keeps the upstream namespace `System.IO.Compression`. Treat the file as third-party code:
do not refactor it in place; update it from upstream and record the version here instead.

Known issues of this version, and how the mod library deals with them (keep the guards, or check the upstream fixes,
when updating the file):

- `ExtractFile` keeps reading until the size declared in the central directory is reached and never stops when the
  input ends early, so a damaged or crafted archive makes it loop forever. `ModArchiveReader` therefore extracts into a
  bounded stream that turns this case into an `InvalidDataException`.
- `ExtractFile(entry, path)` writes to any path it is given (no protection against "zip slip"). Only `ModArchiveReader`
  extracts, into memory, and it checks the paths of the mod files (`EemFormat.IsValidFilePath`); `ZipStorerUseTests`
  fails on any other caller.
- `RemoveEntries` deletes the archive before it moves the new copy into place and turns every error into `false`, so a
  failure can lose the archive. Nothing uses it (`ZipStorerUseTests`).
- `ReadCentralDir` throws `ArgumentException` for damaged records; `ModArchiveReader` turns it into
  `InvalidDataException`.
- Sizes and offsets are written in 32 bits, so files and archives of 4 GB or more come out damaged.
  `ModPackageBuilder.MaxArchiveBytes` refuses them.
- The CRC-32 of an entry is not checked when it is extracted.

Upstream `master` (4.x, commit `fcc839b` of June 2025) still has the endless loop, the unchecked allocation in
`ReadFileInfo` and the behaviour of `RemoveEntries`.

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
[THIRD-PARTY-NOTICES.md of the setup](https://github.com/DritteRippe/Empire-Earth-Setup/blob/main/THIRD-PARTY-NOTICES.md) for
the terms.
