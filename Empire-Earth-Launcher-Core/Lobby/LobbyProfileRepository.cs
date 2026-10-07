using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_WON;

namespace Empire_Earth_Launcher.Core.Lobby
{
    /// <summary>Outcome of <see cref="LobbyProfileRepository.LoadProfiles"/>.</summary>
    public enum LobbyProfilesStatus
    {
        /// <summary>At least one profile was read.</summary>
        Loaded,
        /// <summary>No game folder is known.</summary>
        GameDirectoryNotFound,
        /// <summary>The game folder has no lobby profile file (the lobby was never used).</summary>
        NoProfileFile,
        /// <summary>The profile file exists but cannot be read or is damaged (logged).</summary>
        Unreadable,
        /// <summary>The profile file is valid but contains no profile.</summary>
        NoProfiles
    }

    /// <summary>Outcome of <see cref="LobbyProfileRepository.LoadFriends"/>.</summary>
    public enum LobbyFriendsStatus
    {
        /// <summary>The friend list of the profile was read.</summary>
        Loaded,
        /// <summary>The profile has no user file (yet); there is nothing to show.</summary>
        NoUserFile,
        /// <summary>The user file exists but cannot be read or is damaged (logged).</summary>
        Unreadable
    }

    /// <summary>
    /// Reads the WON lobby profiles of the game folder and the friends of a profile. Missing or damaged files
    /// never throw: they are logged and reported as a status, so the launcher keeps working (korr-S1). Kept
    /// out of GeneralUserControl, so that it can be tested without UI.
    /// </summary>
    /// <remarks>
    /// The lobby files are runtime files of the game. In a game folder that UAC virtualizes (below Program Files) the
    /// game reads and writes them in the VirtualStore, so they are read through the <see cref="EffectivePathResolver"/>:
    /// the VirtualStore copy first (ADR 0016).
    /// </remarks>
    public sealed class LobbyProfileRepository
    {
        private static readonly ReadOnlyCollection<LobbyPersistentData.LobbyGlobalData.PlayerInfoGlobalData> NoProfiles =
            new ReadOnlyCollection<LobbyPersistentData.LobbyGlobalData.PlayerInfoGlobalData>(
                new LobbyPersistentData.LobbyGlobalData.PlayerInfoGlobalData[0]);

        private readonly ILogger logger;
        private readonly IFileSystem fileSystem;
        private readonly EffectivePathResolver effectivePaths;

        /// <param name="logger">Log of the launcher.</param>
        /// <param name="fileSystem">The file system the lobby files are read from.</param>
        /// <param name="effectivePaths">Finds the copy of a lobby file the game really uses (VirtualStore first).</param>
        public LobbyProfileRepository(ILogger logger, IFileSystem fileSystem, EffectivePathResolver effectivePaths)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            this.effectivePaths = effectivePaths ?? throw new ArgumentNullException(nameof(effectivePaths));
        }

        /// <summary>
        /// Reads the profiles of <paramref name="gameDirectory"/>, most recently used first.
        /// </summary>
        /// <param name="gameDirectory">Game folder (the EE folder of the selected installation), or null if none is
        /// known.</param>
        /// <param name="profiles">The profiles; empty (never null) unless the status is <see cref="LobbyProfilesStatus.Loaded"/>.</param>
        public LobbyProfilesStatus LoadProfiles(string gameDirectory,
            out IList<LobbyPersistentData.LobbyGlobalData.PlayerInfoGlobalData> profiles)
        {
            profiles = NoProfiles;
            if (gameDirectory == null)
                return LobbyProfilesStatus.GameDirectoryNotFound;

            string globalDataFile = Resolve(gameDirectory, LobbyPersistentData.GlobalDataFileName);
            if (globalDataFile == null || !fileSystem.FileExists(globalDataFile))
            {
                logger.Warning("No lobby profiles found (" + (globalDataFile ?? gameDirectory) + " does not exist).");
                return LobbyProfilesStatus.NoProfileFile;
            }

            LobbyPersistentData.LobbyGlobalData globalData;
            FileSystemResult<Stream> opened = fileSystem.OpenRead(globalDataFile);
            if (!opened.IsOk)
            {
                logger.Error("Unable to read the lobby profiles from " + globalDataFile + ": " + opened + ".");
                return LobbyProfilesStatus.Unreadable;
            }
            try
            {
                using (Stream input = opened.Value)
                    globalData = new LobbyPersistentData.LobbyGlobalData(input);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException)
            {
                logger.Error("Unable to read the lobby profiles from " + globalDataFile, ex);
                return LobbyProfilesStatus.Unreadable;
            }

            if (globalData.PlayerInfos.Count == 0)
                return LobbyProfilesStatus.NoProfiles;
            profiles = globalData.PlayerInfos.OrderByDescending(profile => profile.LastUse).ToList().AsReadOnly();
            return LobbyProfilesStatus.Loaded;
        }

        /// <summary>
        /// Reads the friends of <paramref name="profile"/> from its user file in <paramref name="gameDirectory"/>.
        /// </summary>
        /// <param name="friends">The friends (name and WON ID); empty (never null) unless the status is
        /// <see cref="LobbyFriendsStatus.Loaded"/>.</param>
        public LobbyFriendsStatus LoadFriends(string gameDirectory,
            LobbyPersistentData.LobbyGlobalData.PlayerInfoGlobalData profile, out IDictionary<string, uint> friends)
        {
            if (gameDirectory == null)
                throw new ArgumentNullException(nameof(gameDirectory));
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            friends = new Dictionary<string, uint>();
            string userDataFile = Resolve(gameDirectory, LobbyPersistentData.GetUserDataFileName(profile.FileID));
            if (userDataFile == null || !fileSystem.FileExists(userDataFile))
                return LobbyFriendsStatus.NoUserFile;

            FileSystemResult<Stream> opened = fileSystem.OpenRead(userDataFile);
            if (!opened.IsOk)
            {
                logger.Error("Unable to read the lobby user data from " + userDataFile + ": " + opened + ".");
                return LobbyFriendsStatus.Unreadable;
            }
            try
            {
                using (Stream input = opened.Value)
                    friends = new LobbyPersistentData.LobbyUserData(input).Friends;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException)
            {
                logger.Error("Unable to read the lobby user data from " + userDataFile, ex);
                return LobbyFriendsStatus.Unreadable;
            }
            return LobbyFriendsStatus.Loaded;
        }

        /// <summary>
        /// The file the game uses for <paramref name="fileName"/> in <paramref name="gameDirectory"/> (VirtualStore copy
        /// first); null if the folder is not a full path.
        /// </summary>
        private string Resolve(string gameDirectory, string fileName)
        {
            if (!WinPath.IsFullyQualified(gameDirectory))
                return null;
            EffectivePath effective = effectivePaths.Resolve(WinPath.Combine(gameDirectory, fileName));
            if (effective.IsVirtualStoreCopy)
                logger.Info("The game uses the VirtualStore copy " + effective.Path + " of " + effective.GamePath + ".");
            return effective.Path;
        }
    }
}
