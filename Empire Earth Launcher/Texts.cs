using System.Globalization;
using Empire_Earth_Launcher.Core.Lobby;
using Empire_Earth_Launcher.Properties;
using Empire_Earth_WON;

namespace Empire_Earth_Launcher
{
    /// <summary>
    /// Turns results of the core and of the launcher's services into texts in the UI language (ADR 0009: the core
    /// returns codes and data, the UI chooses the text). Every text comes from <c>Properties/Resources*.resx</c>;
    /// names, paths and numbers are format arguments, never parts of a translated sentence.
    /// </summary>
    internal static class Texts
    {
        /// <summary>Game state of an online player, with a fallback for states the launcher does not know.</summary>
        internal static string PlayerGameState(NeoApiClient.ConnectedPlayersMessage.PlayerInfo.PlayerGameState state)
        {
            switch (state)
            {
                case NeoApiClient.ConnectedPlayersMessage.PlayerInfo.PlayerGameState.Lobby:
                    return Resources.PlayerStateLobby;
                case NeoApiClient.ConnectedPlayersMessage.PlayerInfo.PlayerGameState.Room:
                    return Resources.PlayerStateRoom;
                case NeoApiClient.ConnectedPlayersMessage.PlayerInfo.PlayerGameState.Playing:
                    return Resources.PlayerStatePlaying;
                default:
                    return string.Format(CultureInfo.CurrentCulture, Resources.PlayerStateUnknownFormat, (int)state);
            }
        }

        /// <summary>
        /// Why no lobby profile can be shown; null for <see cref="LobbyProfilesStatus.Loaded"/>, which needs no text.
        /// </summary>
        internal static string LobbyProfilesProblem(LobbyProfilesStatus status)
        {
            switch (status)
            {
                case LobbyProfilesStatus.Loaded:
                    return null;
                case LobbyProfilesStatus.GameDirectoryNotFound:
                    return Resources.GameDirectoryNotFound;
                case LobbyProfilesStatus.Unreadable:
                    return Resources.LobbyProfilesUnreadable;
                default:
                    return Resources.NoLobbyProfileFound;
            }
        }

        /// <summary>The friends of the selected lobby profile: their number, why they are missing, or nothing.</summary>
        /// <param name="status">Outcome of reading the friends.</param>
        /// <param name="friendCount">Number of friends, used for <see cref="LobbyFriendsStatus.Loaded"/>.</param>
        internal static string LobbyFriends(LobbyFriendsStatus status, int friendCount)
        {
            switch (status)
            {
                case LobbyFriendsStatus.Loaded:
                    return string.Format(CultureInfo.CurrentCulture, Resources.FriendsFormat, friendCount);
                case LobbyFriendsStatus.Unreadable:
                    return Resources.FriendsUnreadable;
                default:
                    return string.Empty;
            }
        }

        /// <summary>Where the game folder shown on the Launcher page comes from.</summary>
        /// <param name="source">Source of the folder.</param>
        /// <param name="folderExists">Whether the folder exists; only matters for a folder chosen by the user.</param>
        internal static string GameDirectoryOrigin(GameDirectorySource source, bool folderExists)
        {
            switch (source)
            {
                case GameDirectorySource.UserSetting:
                    return folderExists ? Resources.GameDirectorySourceUser : Resources.GameDirectorySourceUserMissing;
                case GameDirectorySource.Registry:
                    return Resources.GameDirectorySourceRegistry;
                case GameDirectorySource.LauncherFolder:
                    return Resources.GameDirectorySourceLauncherFolder;
                default:
                    return string.Format(CultureInfo.CurrentCulture, Resources.GameDirectoryNotFoundHintFormat,
                        GameDirectoryLocator.GameExecutableName);
            }
        }
    }
}
