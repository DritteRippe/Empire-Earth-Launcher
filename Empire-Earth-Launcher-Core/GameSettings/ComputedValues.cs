using System;
using System.Linq;
using System.Text;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.GameSettings
{
    /// <summary>
    /// The class S values "Installed From Volume" and "Installed From Directory" of a game folder (contract 3.3, ADR 0015):
    /// the games read them from HKCU, and The Art of Conquest starts with them without starting Empire Earth first
    /// (t=2825 p=19423).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Computed from the <b>real game folder</b> (ADR 0015): the volume is its first two characters (<c>C:</c>); the
    /// directory is the parent without its drive, upper-cased with the invariant culture, exactly one <c>\</c>, the name of
    /// the game folder as it is (not upper-cased) and <c>\</c>. For a community installation this is byte-identical with
    /// the formula of contract 3.3 (<c>\PROGRAM FILES (X86)\NEO EMPIRE EARTH\Empire Earth\</c>); for
    /// <c>D:\Empire Earth</c> it is <c>\Empire Earth\</c>.
    /// </para>
    /// <para>
    /// Comparisons ignore case (ordinal) after normalizing separators and doubled backslashes (contract 3.3): the setup
    /// upper-cases only ASCII letters, the launcher with the invariant culture, and both spellings name the same folder.
    /// Nothing here depends on the current culture (<c>tr-TR</c> included).
    /// </para>
    /// </remarks>
    public sealed class InstalledFromValues
    {
        private InstalledFromValues(string volume, string directory)
        {
            Volume = volume;
            Directory = directory;
        }

        /// <summary>The drive of the game folder, e.g. <c>C:</c>.</summary>
        public string Volume { get; }

        /// <summary>The game folder without its drive, e.g. <c>\GAMES\EE\</c> for <c>C:\Games\EE</c>.</summary>
        public string Directory { get; }

        /// <summary>
        /// The values for <paramref name="gameFolder"/>, or false for a folder that does not start with
        /// <c>&lt;drive letter&gt;:\</c> (a network path, contract 3.3: "writes neither value and warns") or that is a
        /// drive root.
        /// </summary>
        public static bool TryCompute(string gameFolder, out InstalledFromValues values)
        {
            if (gameFolder == null)
                throw new ArgumentNullException(nameof(gameFolder));
            values = null;
            if (string.IsNullOrWhiteSpace(gameFolder) || WinPath.GetDrive(gameFolder.Trim()) == null ||
                !WinPath.IsFullyQualified(gameFolder))
                return false;

            string folder = WinPath.Normalize(gameFolder);
            string name = WinPath.GetFileName(folder);
            string parent = WinPath.GetParent(folder);
            if (name.Length == 0 || parent == null)
                return false;

            string parentWithoutDrive = parent.Substring(2).TrimEnd(WinPath.Separator);
            values = new InstalledFromValues(folder.Substring(0, 2),
                parentWithoutDrive.ToUpperInvariant() + WinPath.Separator + name + WinPath.Separator);
            return true;
        }

        /// <summary>
        /// True if <paramref name="volume"/> and <paramref name="directory"/> (as read from the registry) name the same
        /// folder as these values: ignoring case, after normalizing <c>/</c> and doubled backslashes (contract 3.3).
        /// </summary>
        public bool IsSameAs(string volume, string directory)
        {
            return IsSameVolume(volume) && IsSameDirectory(directory);
        }

        /// <summary>True if <paramref name="volume"/> is this volume (ignoring case and surrounding white space).</summary>
        public bool IsSameVolume(string volume)
        {
            return volume != null && string.Equals(volume.Trim(), Volume, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>True if <paramref name="directory"/> is this directory after <see cref="NormalizeDirectory"/>.</summary>
        public bool IsSameDirectory(string directory)
        {
            return directory != null &&
                   string.Equals(NormalizeDirectory(directory), NormalizeDirectory(Directory), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary><c>/</c> as <c>\</c>, runs of backslashes as one (contract 3.3); case and everything else kept.</summary>
        public static string NormalizeDirectory(string directory)
        {
            if (directory == null)
                throw new ArgumentNullException(nameof(directory));
            var normalized = new StringBuilder(directory.Length);
            foreach (char c in directory.Replace('/', WinPath.Separator))
            {
                if (c == WinPath.Separator && normalized.Length > 0 && normalized[normalized.Length - 1] == WinPath.Separator)
                    continue;
                normalized.Append(c);
            }
            return normalized.ToString();
        }

        public override string ToString()
        {
            return Volume + " " + Directory;
        }
    }

    /// <summary>What decided the <c>Rasterizer Name</c> of a game (contract 3.3), for the log and the hints.</summary>
    public enum RasterizerReason
    {
        /// <summary>Wine: <c>Direct3D</c>.</summary>
        Wine,

        /// <summary>The components of <c>install.ini</c> name a DirectX wrapper: <c>Direct3D</c>.</summary>
        WrapperInInstallInfo,

        /// <summary>The components of the uninstall key name a DirectX wrapper: <c>Direct3D</c>.</summary>
        WrapperInUninstallKey,

        /// <summary>Without component information, a wrapper file is in the game folder: <c>Direct3D</c>.</summary>
        WrapperFile,

        /// <summary>The components of <c>install.ini</c> name no wrapper: <c>Direct3D Hardware TnL</c>.</summary>
        NoWrapperInInstallInfo,

        /// <summary>The components of the uninstall key name no wrapper: <c>Direct3D Hardware TnL</c>.</summary>
        NoWrapperInUninstallKey,

        /// <summary>Without component information, no wrapper file is in the game folder: <c>Direct3D Hardware TnL</c>.</summary>
        NoWrapperFile
    }

    /// <summary>The recommended <c>Rasterizer Name</c> and why.</summary>
    public sealed class RasterizerRecommendation
    {
        internal RasterizerRecommendation(string name, RasterizerReason reason, string wrapperFile)
        {
            Name = name;
            Reason = reason;
            WrapperFile = wrapperFile;
        }

        /// <summary><see cref="GameSettingsTable.Direct3DRasterizer"/> or <see cref="GameSettingsTable.HardwareTnLRasterizer"/>.</summary>
        public string Name { get; }

        public RasterizerReason Reason { get; }

        /// <summary>The wrapper file found (<see cref="RasterizerReason.WrapperFile"/>), else null.</summary>
        public string WrapperFile { get; }

        public override string ToString()
        {
            return Name + " (" + Reason + (WrapperFile == null ? string.Empty : ", " + WrapperFile) + ")";
        }
    }

    /// <summary>The computed values of contract 3.3 for one game of an installation on this computer.</summary>
    public static class ComputedValues
    {
        public const int MinGameWindowWidth = 1024;
        public const int MaxGameWindowWidth = 1920;
        public const int MinGameWindowHeight = 768;
        public const int MaxGameWindowHeight = 1080;

        /// <summary>
        /// <c>Rasterizer Name</c> (contract 3.3): <c>Direct3D</c> under Wine or with a DirectX wrapper, else
        /// <c>Direct3D Hardware TnL</c>. The wrapper comes from the components of <c>install.ini</c>, else of the uninstall
        /// key, and only without component information from the wrapper files in the game folder.
        /// </summary>
        public static RasterizerRecommendation Rasterizer(Installation installation, Game game, ISystemInfo systemInfo,
            IFileSystem fileSystem)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            if (systemInfo == null)
                throw new ArgumentNullException(nameof(systemInfo));
            if (fileSystem == null)
                throw new ArgumentNullException(nameof(fileSystem));

            if (systemInfo.IsWine)
                return new RasterizerRecommendation(GameSettingsTable.Direct3DRasterizer, RasterizerReason.Wine, null);

            SetupNameList components = installation.Components;
            if (components != null && components.Names.Count > 0)
            {
                bool fromInstallInfo = installation.InstallInfo != null;
                if (components.HasDirectXWrapper)
                    return new RasterizerRecommendation(GameSettingsTable.Direct3DRasterizer,
                        fromInstallInfo ? RasterizerReason.WrapperInInstallInfo : RasterizerReason.WrapperInUninstallKey, null);
                return new RasterizerRecommendation(GameSettingsTable.HardwareTnLRasterizer,
                    fromInstallInfo ? RasterizerReason.NoWrapperInInstallInfo : RasterizerReason.NoWrapperInUninstallKey, null);
            }

            string folder = installation.GetGameFolder(game);
            string file = folder == null || !WinPath.IsFullyQualified(folder)
                ? null
                : GameSettingsTable.DirectXWrapperFiles.FirstOrDefault(name => fileSystem.FileExists(WinPath.Combine(folder, name)));
            return file != null
                ? new RasterizerRecommendation(GameSettingsTable.Direct3DRasterizer, RasterizerReason.WrapperFile, file)
                : new RasterizerRecommendation(GameSettingsTable.HardwareTnLRasterizer, RasterizerReason.NoWrapperFile, null);
        }

        /// <summary>
        /// <c>Game Window Width</c> and <c>Game Window Height</c> (contract 3.3): the primary screen in physical pixels,
        /// each dimension limited on its own to 1024 to 1920 and 768 to 1080. If the physical size is unknown, the size a
        /// DPI-unaware program sees is used, and without any size the minimum.
        /// </summary>
        public static ScreenSize GameWindow(ISystemInfo systemInfo)
        {
            if (systemInfo == null)
                throw new ArgumentNullException(nameof(systemInfo));
            ScreenSize screen = !systemInfo.PrimaryScreen.IsEmpty ? systemInfo.PrimaryScreen : systemInfo.PrimaryScreenUnaware;
            if (screen.IsEmpty)
                return new ScreenSize(MinGameWindowWidth, MinGameWindowHeight);
            return new ScreenSize(Clamp(screen.Width, MinGameWindowWidth, MaxGameWindowWidth),
                Clamp(screen.Height, MinGameWindowHeight, MaxGameWindowHeight));
        }

        /// <summary>
        /// True if the primary screen is lower than 768 physical pixels: the game's menu does not fit (t=3863), the launcher
        /// warns (contract 3.3, R13). False if the size is unknown.
        /// </summary>
        public static bool IsScreenTooLow(ISystemInfo systemInfo)
        {
            if (systemInfo == null)
                throw new ArgumentNullException(nameof(systemInfo));
            ScreenSize screen = systemInfo.PrimaryScreen;
            return !screen.IsEmpty && screen.Height < MinGameWindowHeight;
        }

        private static int Clamp(int value, int min, int max)
        {
            return Math.Min(Math.Max(value, min), max);
        }
    }
}
