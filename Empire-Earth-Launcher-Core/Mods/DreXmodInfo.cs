using System;
using System.Linq;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Mods
{
    /// <summary>Which dreXmod the setup installed, as far as the components of the setup tell.</summary>
    public enum DreXmodVersion
    {
        /// <summary>No dreXmod.</summary>
        None,

        /// <summary>The component <c>additional\drexmod\v2</c>: no mod system, no presets.</summary>
        Version2,

        /// <summary>The component <c>additional\drexmod\v3</c>: presets in <c>Data\dxm\mods</c>, chosen in <c>dreXmod.config</c>.</summary>
        Version3
    }

    /// <summary>
    /// The dreXmod of an installation for the Mods page: from the components of <c>install.ini</c>, else of the uninstall key,
    /// and only without component information from the folder of the presets in the game folder. Only reads; dreXmod comes with
    /// the setup and the launcher never adds, removes or switches it (contract 2.5, ADR 0014).
    /// </summary>
    /// <remarks>
    /// The component names are those of the setup (<c>setup_is6.iss</c>: <c>additional\drexmod\v2</c> and <c>v3</c>, exclusive).
    /// They are not part of the contract text, which names only the wrapper and language components.
    /// </remarks>
    public static class DreXmodInfo
    {
        public const string Version2Component = @"additional\drexmod\v2";

        public const string Version3Component = @"additional\drexmod\v3";

        public static DreXmodVersion Describe(Installation installation, IFileSystem fileSystem)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            if (fileSystem == null)
                throw new ArgumentNullException(nameof(fileSystem));
            SetupNameList components = installation.Components;
            if (components != null)
            {
                if (components.Contains(Version3Component))
                    return DreXmodVersion.Version3;
                return components.Contains(Version2Component) ? DreXmodVersion.Version2 : DreXmodVersion.None;
            }
            // No component information (a foreign installation): the folder of the presets says that dreXmod 3 is there.
            return new[] { installation.EeFolder, installation.AocFolder }
                .Any(folder => folder != null && WinPath.IsFullyQualified(folder) &&
                               fileSystem.DirectoryExists(ModFolderScanner.ModsFolder(folder)))
                ? DreXmodVersion.Version3
                : DreXmodVersion.None;
        }
    }
}
