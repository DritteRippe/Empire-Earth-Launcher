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

## NuGet packages

Restored into `packages/` at build time, not committed.

| Package | Version | License | Project |
|---|---|---|---|
| Krypton.Toolkit | 5.550.2108.1 | BSD-3-Clause | https://github.com/Krypton-Suite/Standard-Toolkit |

Tests only (`Empire-Earth-Launcher.Tests`, not shipped):

| Package | Version | License | Project |
|---|---|---|---|
| NUnit | 3.14.0 | MIT | https://nunit.org/ |
| NUnitLite | 3.14.0 | MIT | https://nunit.org/ |

Build-time only (CI, not shipped): `Microsoft.NETFramework.ReferenceAssemblies.net40` 1.0.3,
.NET Framework 4.0 reference assemblies by Microsoft.
