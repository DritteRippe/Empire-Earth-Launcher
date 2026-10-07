using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Lobby;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_WON;

namespace Empire_Earth_Launcher.Core.Maintenance
{
    /// <summary>Where a name comes from (<see cref="NameChecks"/>).</summary>
    public enum NameSource
    {
        /// <summary>A lobby profile of the game folder (<c>_wonlobbypersistent.dat</c>).</summary>
        LobbyProfile,
        /// <summary>A player of the game: a folder <c>Users\&lt;Name&gt;</c> of the game folder or its VirtualStore copy.</summary>
        PlayerFolder
    }

    /// <summary>A name with characters outside printable ASCII.</summary>
    public sealed class NameWarning
    {
        internal NameWarning(Game game, NameSource source, string name, int number)
        {
            Game = game;
            Source = source;
            Name = name;
            Number = number;
        }

        public Game Game { get; }

        public NameSource Source { get; }

        /// <summary>The name, shown to the player on the Tools page; never logged (ADR 0013 plan review).</summary>
        public string Name { get; }

        /// <summary>The number of the profile or folder, counted from 1, for the log.</summary>
        public int Number { get; }
    }

    /// <summary>The result of <see cref="NameChecks.Check"/>.</summary>
    public sealed class NameCheckReport
    {
        internal NameCheckReport(int namesChecked, IEnumerable<NameWarning> warnings)
        {
            NamesChecked = namesChecked;
            Warnings = new ReadOnlyCollection<NameWarning>(warnings.ToList());
        }

        /// <summary>How many profile and player names were checked.</summary>
        public int NamesChecked { get; }

        public IReadOnlyList<NameWarning> Warnings { get; }
    }

    /// <summary>
    /// The name check of the Tools page (R10, forum report section 8 row 16, test case 17): lobby profiles and player names
    /// (the folders <c>Users\&lt;Name&gt;</c> of both game folders and their VirtualStore copies) with characters outside
    /// printable ASCII get a warning: with "symbols" or "characters" in a name a multiplayer saved game cannot be loaded
    /// (forum t=3563 p=23879), and "unicode text in his name" made the game crash (t=2126 p=14281). Read-only; the page also
    /// says that the host of a multiplayer saved game or scenario needs its ports forwarded (33334 and 33336 TCP+UDP, 33335 TCP by default; forum 4.9, t=9004 p=44615, t=11057 p=48100).
    /// </summary>
    /// <remarks>Names are shown to the player, never logged: the log counts them (ADR 0013 plan review).</remarks>
    public sealed class NameChecks
    {
        /// <summary>The folder of the players in a game folder.</summary>
        public const string UsersFolderName = "Users";

        private readonly IFileSystem fileSystem;
        private readonly EffectivePathResolver effectivePaths;
        private readonly LobbyProfileRepository profiles;
        private readonly ILogger logger;

        public NameChecks(IFileSystem fileSystem, EffectivePathResolver effectivePaths, LobbyProfileRepository profiles, ILogger logger)
        {
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            this.effectivePaths = effectivePaths ?? throw new ArgumentNullException(nameof(effectivePaths));
            this.profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public NameCheckReport Check(Installation installation)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            var warnings = new List<NameWarning>();
            int names = 0;
            foreach (Game game in Game.All)
            {
                string gameFolder = installation.GetGameFolder(game);
                if (gameFolder == null || !WinPath.IsFullyQualified(gameFolder))
                    continue;

                if (profiles.LoadProfiles(gameFolder, out IList<LobbyPersistentData.LobbyGlobalData.PlayerInfoGlobalData> loaded) ==
                    LobbyProfilesStatus.Loaded)
                {
                    for (int i = 0; i < loaded.Count; i++)
                    {
                        names++;
                        string name = loaded[i].Username ?? string.Empty;
                        if (!SavedGames.IsPrintableAscii(name))
                            warnings.Add(new NameWarning(game, NameSource.LobbyProfile, name, i + 1));
                    }
                }

                List<string> players = PlayerFolders(gameFolder);
                for (int i = 0; i < players.Count; i++)
                {
                    names++;
                    if (!SavedGames.IsPrintableAscii(players[i]))
                        warnings.Add(new NameWarning(game, NameSource.PlayerFolder, players[i], i + 1));
                }
            }

            foreach (NameWarning warning in warnings)
                logger.Info("Name check: " + (warning.Source == NameSource.LobbyProfile ? "lobby profile " : "player folder ") +
                            warning.Number.ToString(CultureInfo.InvariantCulture) + " of " + warning.Game.Id +
                            " has characters outside printable ASCII.");
            logger.Info("Name check of " + installation.Root + ": " + names.ToString(CultureInfo.InvariantCulture) + " name(s), " +
                        warnings.Count.ToString(CultureInfo.InvariantCulture) + " with characters outside printable ASCII.");
            return new NameCheckReport(names, warnings);
        }

        /// <summary>The names of the folders in <c>Users</c> of the game folder and of its VirtualStore copy, once each, sorted.</summary>
        private List<string> PlayerFolders(string gameFolder)
        {
            var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            string users = WinPath.Combine(gameFolder, UsersFolderName);
            foreach (string folder in new[] { users, effectivePaths.GetVirtualStorePath(users) })
            {
                if (folder == null || !fileSystem.DirectoryExists(folder))
                    continue;
                FileSystemResult<IReadOnlyList<string>> folders = fileSystem.GetDirectories(folder);
                if (!folders.IsOk)
                {
                    logger.Warning("Name check: " + folder + " cannot be listed: " + folders + ".");
                    continue;
                }
                foreach (string player in folders.Value)
                    names.Add(WinPath.GetFileName(player));
            }
            return names.ToList();
        }
    }
}
