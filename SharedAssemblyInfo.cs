// Version information shared by every assembly of Empire-Earth.sln
// (Empire Earth Launcher, Empire-Earth-WON, Empire-Earth-Mod-Lib and
// Empire-Earth-Mod).
//
// The projects live in one repository and are released together, so they
// carry one version number. Change it here and only here; every project
// links this file as Properties\SharedAssemblyInfo.cs.
//
// 1.1.0 is the launcher that the suite installer "Empire Earth Community"
// 1.1.0 (setup repository, folder suite/) packages: one launcher for the four
// games, the Graphics and Mods pages and the optional additions of contract
// revision 6. 1.0.0 was the first launcher of the suite (contract revision 4:
// --product, suite mutex, suite record).
// "Empire Earth Launcher v3" in the git history means the third rewrite of
// the launcher, not a release number.
//
//   AssemblyVersion               Major.Minor.0.0 - identity used by the CLR
//                                 for binding; change it only with Major/Minor.
//   AssemblyFileVersion           Major.Minor.Patch.0 - Windows file properties.
//   AssemblyInformationalVersion  Full SemVer (a pre-release tag if any). Shown as
//                                 Application.ProductVersion (launcher log)
//                                 and written into .eem packages.

using System.Reflection;

[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]
[assembly: AssemblyInformationalVersion("1.1.0")]
