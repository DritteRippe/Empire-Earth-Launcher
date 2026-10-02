using System;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Installations
{
    /// <summary>What a folder of an installation is, judged by the files in it.</summary>
    public enum GameFolderKind
    {
        /// <summary>None of the kinds below.</summary>
        None,

        /// <summary>An EE folder: it contains <c>Empire Earth.exe</c>.</summary>
        EmpireEarthFolder,

        /// <summary>An AoC folder: it contains <c>EE-AOC.exe</c> (and no <c>Empire Earth.exe</c>).</summary>
        ArtOfConquestFolder,

        /// <summary>
        /// An install root: <c>Empire Earth\Empire Earth.exe</c> or a setup data folder (<c>_setupdata_EE</c>,
        /// <c>_setupdata_NeoEE</c>) is below it.
        /// </summary>
        InstallRoot
    }

    /// <summary>Recognizes the folders of an installation (contract 0, "Folders, programs and mutexes").</summary>
    public static class GameFolders
    {
        /// <summary>True if <paramref name="folder"/> contains <c>Empire Earth.exe</c>; false for null, empty or invalid paths.</summary>
        public static bool IsEmpireEarthFolder(IFileSystem fileSystem, string folder)
        {
            return ContainsProgram(fileSystem, folder, Game.EmpireEarth);
        }

        /// <summary>True if <paramref name="folder"/> contains the program of <paramref name="game"/>.</summary>
        public static bool ContainsProgram(IFileSystem fileSystem, string folder, Game game)
        {
            if (fileSystem == null)
                throw new ArgumentNullException(nameof(fileSystem));
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            return !string.IsNullOrWhiteSpace(folder) && WinPath.IsFullyQualified(folder) &&
                   fileSystem.FileExists(WinPath.Combine(folder, game.ProgramName));
        }

        /// <summary>
        /// True if <paramref name="folder"/> is an install root: <c>Empire Earth\Empire Earth.exe</c> or a setup data
        /// folder is below it.
        /// </summary>
        public static bool IsInstallRoot(IFileSystem fileSystem, string folder)
        {
            if (fileSystem == null)
                throw new ArgumentNullException(nameof(fileSystem));
            if (string.IsNullOrWhiteSpace(folder) || !WinPath.IsFullyQualified(folder))
                return false;
            return IsEmpireEarthFolder(fileSystem, WinPath.Combine(folder, Game.EmpireEarth.FolderName)) ||
                   Product.All.Any(product => fileSystem.DirectoryExists(WinPath.Combine(folder, product.SetupDataFolderName)));
        }

        /// <summary>What <paramref name="folder"/> is: EE folder, AoC folder, install root, or none of them.</summary>
        public static GameFolderKind Classify(IFileSystem fileSystem, string folder)
        {
            if (IsEmpireEarthFolder(fileSystem, folder))
                return GameFolderKind.EmpireEarthFolder;
            if (ContainsProgram(fileSystem, folder, Game.ArtOfConquest))
                return GameFolderKind.ArtOfConquestFolder;
            if (IsInstallRoot(fileSystem, folder))
                return GameFolderKind.InstallRoot;
            return GameFolderKind.None;
        }
    }
}
