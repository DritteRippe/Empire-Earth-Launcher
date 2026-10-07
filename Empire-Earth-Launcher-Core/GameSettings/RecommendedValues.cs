using System;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.GameSettings
{
    /// <summary>
    /// The data of every value of contract 3.2 for one game of an installation on this computer: the fixed data of the
    /// table and the computed values of contract 3.3 ("Installed From" from the real game folder, ADR 0015; the
    /// rasterizer by the wrapper rule; the window size in physical pixels).
    /// </summary>
    public sealed class RecommendedValues
    {
        private RecommendedValues(Installation installation, Game game, InstalledFromValues installedFrom,
            RasterizerRecommendation rasterizer, ScreenSize gameWindow)
        {
            Installation = installation;
            Game = game;
            InstalledFrom = installedFrom;
            Rasterizer = rasterizer;
            GameWindow = gameWindow;
        }

        public Installation Installation { get; }

        public Game Game { get; }

        /// <summary>The class S values; null if the game folder is not on a drive letter (contract 3.3: no values, a warning).</summary>
        public InstalledFromValues InstalledFrom { get; }

        public RasterizerRecommendation Rasterizer { get; }

        /// <summary>The recommended window size (clamped).</summary>
        public ScreenSize GameWindow { get; }

        /// <summary>The recommended values of <paramref name="game"/> of <paramref name="installation"/>.</summary>
        /// <exception cref="ArgumentException">The installation has no folder for the game (AoC not installed).</exception>
        public static RecommendedValues For(Installation installation, Game game, ISystemInfo systemInfo, IFileSystem fileSystem)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            string folder = installation.GetGameFolder(game);
            if (folder == null)
                throw new ArgumentException(installation.Root + " has no folder of " + game.Id + ".", nameof(game));
            InstalledFromValues.TryCompute(folder, out InstalledFromValues installedFrom);
            return new RecommendedValues(installation, game, installedFrom,
                ComputedValues.Rasterizer(installation, game, systemInfo, fileSystem), ComputedValues.GameWindow(systemInfo));
        }

        /// <summary>
        /// The data of <paramref name="setting"/>; null only for the class S values of a game folder without a drive letter.
        /// </summary>
        public RegistryValue ValueOf(GameSetting setting)
        {
            if (setting == null)
                throw new ArgumentNullException(nameof(setting));
            switch (setting.Data)
            {
                case SettingData.InstalledFromVolume:
                    return InstalledFrom == null ? null : RegistryValue.FromString(InstalledFrom.Volume);
                case SettingData.InstalledFromDirectory:
                    return InstalledFrom == null ? null : RegistryValue.FromString(InstalledFrom.Directory);
                case SettingData.RasterizerName:
                    return RegistryValue.FromString(Rasterizer.Name);
                case SettingData.GameWindowWidth:
                    return RegistryValue.FromDWord(GameWindow.Width);
                case SettingData.GameWindowHeight:
                    return RegistryValue.FromDWord(GameWindow.Height);
                default:
                    return setting.FixedValue(Game);
            }
        }
    }
}
