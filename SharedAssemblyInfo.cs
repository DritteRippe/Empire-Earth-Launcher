// Version information shared by every assembly of Empire-Earth.sln
// (Empire Earth Launcher, Empire-Earth-Mod-Lib and Empire-Earth-Mod).
//
// The projects live in one repository and are released together, so they
// carry one version number. Change it here and only here; every project
// links this file as Properties\SharedAssemblyInfo.cs.
//
// Nothing has been released yet ("Empire Earth Launcher v3" in the git
// history means the third rewrite of the launcher, not a release), so the
// version stays below 1.0 and carries a SemVer pre-release tag.
//
//   AssemblyVersion               Major.Minor.0.0 - identity used by the CLR
//                                 for binding; change it only with Major/Minor.
//   AssemblyFileVersion           Major.Minor.Patch.0 - Windows file properties.
//   AssemblyInformationalVersion  Full SemVer incl. pre-release tag. Shown as
//                                 Application.ProductVersion (launcher log)
//                                 and written into .eem packages.

using System.Reflection;

[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]
[assembly: AssemblyInformationalVersion("0.1.0-alpha")]
