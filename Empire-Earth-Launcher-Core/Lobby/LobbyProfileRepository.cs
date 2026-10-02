using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Empire_Earth_Launcher.Core.Logging;
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
    public sealed class LobbyProfileRepository
    {
        private static readonly ReadOnlyCollection<LobbyPersistentData.LobbyGlobalData.PlayerInfoGlobalData> NoProfiles =
            new ReadOnlyCollection<LobbyPersistentData.LobbyGlobalData.PlayerInfoGlobalData>(
                new LobbyPersistentData.LobbyGlobalData.PlayerInfoGlobalData[0]);

        private readonly ILogger logger;

        public LobbyProfileRepository(ILogger logger)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            this.logger = logger;
        }

        /// <summary>
        /// Reads the profiles of <paramref name="gameDirectory"/>, most recently used first.
        /// </summary>
        /// <param name="gameDirectory">Game folder, or null if none is known.</param>
        /// <param name="profiles">The profiles; empty (never null) unless the status is <see cref="LobbyProfilesStatus.Loaded"/>.</param>
        public LobbyProfilesStatus LoadProfiles(string gameDirectory,
            out IList<LobbyPersistentData.LobbyGlobalData.PlayerInfoGlobalData> profiles)
        {
            profiles = NoProfiles;
            if (gameDirectory == null)
                return LobbyProfilesStatus.GameDirectoryNotFound;

            string globalDataFile = Path.Combine(gameDirectory, LobbyPersistentData.GlobalDataFileName);
            if (!File.Exists(globalDataFile))
            {
                logger.Warning("No lobby profiles found (" + globalDataFile + " does not exist).");
                return LobbyProfilesStatus.NoProfileFile;
            }

            LobbyPersistentData.LobbyGlobalData globalData;
            try
            {
                globalData = new LobbyPersistentData.LobbyGlobalData(globalDataFile);
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
            string userDataFile = Path.Combine(gameDirectory, LobbyPersistentData.GetUserDataFileName(profile.FileID));
            if (!File.Exists(userDataFile))
                return LobbyFriendsStatus.NoUserFile;

            try
            {
                friends = new LobbyPersistentData.LobbyUserData(userDataFile).Friends;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException)
            {
                logger.Error("Unable to read the lobby user data from " + userDataFile, ex);
                return LobbyFriendsStatus.Unreadable;
            }
            return LobbyFriendsStatus.Loaded;
        }
    }
}
