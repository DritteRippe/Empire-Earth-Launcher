using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Lobby;
using Empire_Earth_Launcher.Core.Settings;
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

        /// <summary>
        /// Name of a choice of the language list: "Windows language" in the UI language, the languages in their own
        /// language, as language lists usually show them, so that a player finds their language in any UI language.
        /// These names are not translated and are therefore not resources.
        /// </summary>
        /// <param name="language">One of <see cref="UiLanguage.Choices"/>.</param>
        internal static string UiLanguageName(string language)
        {
            switch (language)
            {
                case UiLanguage.Windows:
                    return Resources.UiLanguageWindows;
                case "en":
                    return "English";
                case "de":
                    return "Deutsch";
                case "fr":
                    return "Français";
                default:
                    throw new ArgumentOutOfRangeException(nameof(language), language, "Not a UI language of the launcher.");
            }
        }

        /// <summary>Where the installation shown on the Launcher page comes from.</summary>
        /// <param name="selected">The selected installation; null if none was found.</param>
        /// <param name="chosenByUser">Whether the user chose it (source 1).</param>
        internal static string InstallationOrigin(Installation selected, bool chosenByUser)
        {
            if (selected == null)
                return string.Format(CultureInfo.CurrentCulture, Resources.GameDirectoryNotFoundHintFormat,
                    Game.EmpireEarth.ProgramName);
            if (chosenByUser)
                return selected.State == InstallationState.FolderMissing
                    ? Resources.GameDirectorySourceUserMissing
                    : Resources.GameDirectorySourceUser;
            return selected.Origin == InstallationSource.LauncherFolder
                ? Resources.GameDirectorySourceLauncherFolder
                : Resources.GameDirectorySourceRegistry;
        }

        /// <summary>Short name of the kind of an installation, for the list of the Launcher page.</summary>
        internal static string InstallationKindName(InstallationKind kind)
        {
            switch (kind)
            {
                case InstallationKind.Community:
                    return Resources.InstallationKindCommunity;
                case InstallationKind.CommunityLegacy:
                    return Resources.InstallationKindCommunityLegacy;
                default:
                    return Resources.InstallationKindForeign;
            }
        }

        /// <summary>What the kind of an installation means (tooltip of the list).</summary>
        internal static string InstallationKindHint(InstallationKind kind)
        {
            switch (kind)
            {
                case InstallationKind.Community:
                    return Resources.InstallationKindCommunityHint;
                case InstallationKind.CommunityLegacy:
                    return Resources.InstallationKindCommunityLegacyHint;
                default:
                    return Resources.InstallationKindForeignHint;
            }
        }

        /// <summary>The state of an installation for the list: OK, damaged or not found.</summary>
        internal static string InstallationStateName(Installation installation)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            switch (installation.State)
            {
                case InstallationState.Ok:
                    return Resources.InstallationStateOk;
                case InstallationState.Damaged:
                    return Resources.InstallationStateDamaged;
                default:
                    return Resources.InstallationStateFolderMissing;
            }
        }

        /// <summary>Details of the state (tooltip of the list): the missing programs, or the missing folder; else empty.</summary>
        internal static string InstallationStateHint(Installation installation)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            switch (installation.State)
            {
                case InstallationState.Damaged:
                    return string.Format(CultureInfo.CurrentCulture, Resources.InstallationMissingProgramsFormat,
                        ProgramNames(installation.MissingPrograms));
                case InstallationState.FolderMissing:
                    return Resources.GameDirectorySourceUserMissing;
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// The hints below the list of installations, one per line (ADR 0015, contract 1.4 and 5): installations that share
        /// their game settings, two products in one folder, a setup newer than the launcher, and a damaged selected
        /// installation. Empty if there is nothing to say.
        /// </summary>
        internal static string InstallationHints(DiscoveryResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            var lines = new List<string>();
            foreach (Product product in result.ProductsWithSharedSettings)
            {
                lines.Add(string.Format(CultureInfo.CurrentCulture, Resources.InstallationsSharedSettingsFormat,
                    result.Installations.Count(installation => installation.Product == product), product.AppName,
                    @"HKCU\" + product.GetGameSettingsKey(Game.EmpireEarth)));
            }
            foreach (Installation installation in result.Installations.Where(installation => installation.OtherProductInRoot != null))
            {
                lines.Add(string.Format(CultureInfo.CurrentCulture, Resources.InstallationSharedRootFormat, installation.Root,
                    installation.Product.AppName));
            }
            foreach (Installation installation in result.Installations.Where(installation => installation.HasNewerContract))
                lines.Add(string.Format(CultureInfo.CurrentCulture, Resources.InstallationNewerSetupFormat, installation.Root));
            Installation selected = result.Selected;
            if (selected != null && selected.State == InstallationState.Damaged)
            {
                Game first = selected.MissingPrograms[0];
                lines.Add(string.Format(CultureInfo.CurrentCulture, Resources.InstallationDamagedFormat,
                    ProgramNames(selected.MissingPrograms), selected.GetGameFolder(first)));
            }
            return string.Join(Environment.NewLine, lines);
        }

        private static string ProgramNames(IEnumerable<Game> games)
        {
            return string.Join(", ", games.Select(game => game.ProgramName));
        }
    }
}
