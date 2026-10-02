using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Lobby;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
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

        // --- Game settings (L-WP5, contract 3) -----------------------------------------------------------------------

        /// <summary>The name of a game in the UI language: "Empire Earth" or "The Art of Conquest".</summary>
        internal static string GameName(Game game)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            return game == Game.EmpireEarth ? Resources.GameEmpireEarth : Resources.GameArtOfConquest;
        }

        /// <summary>The installation the Game settings page shows; "not found" without one.</summary>
        internal static string GameSettingsInstallation(Installation installation)
        {
            return installation == null
                ? Resources.GameDirectoryNotFound
                : string.Format(CultureInfo.CurrentCulture, Resources.GameSettingsInstallationFormat, installation.Product.AppName,
                    installation.EeFolder);
        }

        /// <summary>The state of the defaults of one game (contract 3.5, ADR 0015).</summary>
        internal static string DefaultsStatus(Game game, DefaultsStatus status)
        {
            string format;
            switch (status)
            {
                case Core.GameSettings.DefaultsStatus.Applied:
                    format = Resources.DefaultsStatusAppliedFormat;
                    break;
                case Core.GameSettings.DefaultsStatus.AppliedByNewerVersion:
                    format = Resources.DefaultsStatusAppliedByNewerFormat;
                    break;
                case Core.GameSettings.DefaultsStatus.WaitingForPlay:
                    format = Resources.DefaultsStatusWaitingForPlayFormat;
                    break;
                case Core.GameSettings.DefaultsStatus.NewerContract:
                    format = Resources.DefaultsStatusNewerContractFormat;
                    break;
                default:
                    format = Resources.DefaultsStatusPendingFormat;
                    break;
            }
            return string.Format(CultureInfo.CurrentCulture, format, GameName(game));
        }

        /// <summary>Why a change was refused by the mutation guard (ADR 0016); null if it was not.</summary>
        internal static string Block(MutationCheck check)
        {
            if (check == null || check.IsAllowed)
                return null;
            return check.Block == MutationBlock.SetupRunning
                ? string.Format(CultureInfo.CurrentCulture, Resources.BlockedBySetupFormat, check.Setup.AppName)
                : string.Format(CultureInfo.CurrentCulture, Resources.BlockedByGameFormat, check.Game.ProgramName);
        }

        /// <summary>The display question of the first run (contract 3.6 step 3) with every differing value.</summary>
        internal static string DisplayQuestion(DisplayQuestion question)
        {
            if (question == null)
                throw new ArgumentNullException(nameof(question));
            IEnumerable<string> differences = question.Items.SelectMany(item => item.Differences.Select(difference =>
                string.Format(CultureInfo.CurrentCulture, Resources.DisplayDifferenceFormat, GameName(item.Game),
                    difference.Setting.ValueName, ConsistencyFinding.Data(difference.Current),
                    ConsistencyFinding.Data(difference.Recommended))));
            return string.Format(CultureInfo.CurrentCulture, Resources.DisplayQuestionFormat, string.Join("; ", differences));
        }

        /// <summary>The text of a hint of the consistency checks (contract 3.6, ADR 0011 plan review).</summary>
        internal static string Finding(ConsistencyFinding finding)
        {
            if (finding == null)
                throw new ArgumentNullException(nameof(finding));
            string game = finding.Game == null ? string.Empty : GameName(finding.Game);
            string Value(int index) => index < finding.Values.Count ? ConsistencyFinding.Data(finding.Values[index].Value) : string.Empty;
            string WindowSize() => Value(0) + "x" + Value(1);
            switch (finding.Code)
            {
                case FindingCode.BitDepthMismatch:
                    return string.Format(CultureInfo.CurrentCulture, Resources.FindingBitDepthMismatchFormat, game, Value(0), Value(1));
                case FindingCode.SixteenBitOnWindows8:
                    return string.Format(CultureInfo.CurrentCulture, Resources.FindingSixteenBitFormat, game);
                case FindingCode.RasterizerMismatch:
                    return string.Format(CultureInfo.CurrentCulture, Resources.FindingRasterizerFormat, game, Value(0), finding.Recommended);
                case FindingCode.WindowLargerThanScreen:
                    return string.Format(CultureInfo.CurrentCulture, Resources.FindingWindowLargerFormat, game, WindowSize(),
                        finding.GameScreen, finding.Recommended);
                case FindingCode.WindowFitsOnlyWithHighDpiAware:
                    return string.Format(CultureInfo.CurrentCulture, Resources.FindingWindowHighDpiFormat, game, WindowSize(),
                        finding.GameScreen);
                case FindingCode.ScreenTooLow:
                    return string.Format(CultureInfo.CurrentCulture, Resources.FindingScreenTooLowFormat, finding.GameScreen.Height);
                case FindingCode.InstalledFromNotOnADrive:
                    return string.Format(CultureInfo.CurrentCulture, Resources.FindingNotOnADriveFormat, game,
                        finding.Installation.GetGameFolder(finding.Game));
                case FindingCode.FolderOutsideAnsiCodePage:
                    return string.Format(CultureInfo.CurrentCulture, Resources.FindingFolderNotAnsiFormat, game,
                        finding.Installation.GetGameFolder(finding.Game));
                default:
                    throw new ArgumentOutOfRangeException(nameof(finding), finding.Code, "Unknown finding.");
            }
        }

        /// <summary>The result of the reset, of the display settings or of the answer to the question.</summary>
        internal static string GameSettingsResult(GameSettingsResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            switch (result.Outcome)
            {
                case GameSettingsOutcome.Done:
                    return result.BackupFolder == null
                        ? Resources.ResultDone
                        : string.Format(CultureInfo.CurrentCulture, Resources.ResultDoneFormat, result.BackupFolder);
                case GameSettingsOutcome.Blocked:
                    return Block(result.Block);
                case GameSettingsOutcome.NewerContract:
                    return Resources.ResultNewerContract;
                case GameSettingsOutcome.BackupFailed:
                    return Resources.ResultBackupFailed;
                default:
                    return result.BackupFolder == null
                        ? Resources.ResultFailed
                        : string.Format(CultureInfo.CurrentCulture, Resources.ResultFailedFormat, result.BackupFolder);
            }
        }

        /// <summary>The result of a change of a compatibility option.</summary>
        internal static string CompatibilityResult(CompatibilityResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            switch (result.Outcome)
            {
                case GameSettingsOutcome.Done:
                    return result.BackupFile == null
                        ? Resources.ResultDone
                        : string.Format(CultureInfo.CurrentCulture, Resources.ResultDoneFormat, result.BackupFile);
                case GameSettingsOutcome.Blocked:
                    return Block(result.Block);
                case GameSettingsOutcome.BackupFailed:
                    return Resources.ResultBackupFailed;
                default:
                    return Resources.ResultFailed;
            }
        }

        /// <summary>The text of the check box of a compatibility entry (contract 3.7); the entry name stays in brackets.</summary>
        internal static string CompatibilityOption(string entry)
        {
            switch (entry)
            {
                case CompatibilityLayers.Dwm8And16BitMitigation:
                    return Resources.CompatibilityDwmOption;
                case CompatibilityLayers.HighDpiAware:
                    return Resources.CompatibilityHighDpiOption;
                case CompatibilityLayers.HeapClearAllocation:
                    return Resources.CompatibilityHeapOption;
                case CompatibilityLayers.Windows7Mode:
                    return Resources.CompatibilityWindows7Option;
                default:
                    throw new ArgumentOutOfRangeException(nameof(entry), entry, "Not an entry of the launcher.");
            }
        }

        /// <summary>
        /// The explanations below the compatibility options, one per line: where they apply, the read-only HKLM values, a
        /// Windows version mode that is set, Windows 7 and Wine, the old values of an older setup, and RUNASADMIN.
        /// </summary>
        internal static string CompatibilityInfo(CompatibilityState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            var lines = new List<string>();
            if (state.SwitchesOffered)
                lines.Add(string.Format(CultureInfo.CurrentCulture, Resources.CompatibilityAppliesToFormat,
                    string.Join(", ", state.Programs.Select(program => program.Game.ProgramName))));
            else
                lines.Add(Resources.CompatibilityNotOffered);
            foreach (ProgramLayers program in state.Programs.Where(program => program.LocalMachine != null))
                lines.Add(string.Format(CultureInfo.CurrentCulture, Resources.CompatibilityAllUsersFormat,
                    program.Game.ProgramName + ": " + program.LocalMachine));
            if (state.Entries.Any(entry => entry.Unavailable == EntryUnavailable.VersionModeSet))
                lines.Add(Resources.CompatibilityVersionModeSet);
            foreach (LegacyLayerValue legacy in state.LegacyValues)
                lines.Add(string.Format(CultureInfo.CurrentCulture, Resources.CompatibilityLegacyFormat,
                    (legacy.Key.Hive == Microsoft.Win32.RegistryHive.CurrentUser ? "HKCU" : "HKLM") + ", " +
                    WinPath.GetFileName(legacy.ProgramPath) + ": " + legacy.Value));
            if (state.RunAsAdminRemovable)
                lines.Add(Resources.CompatibilityRunAsAdminHint);
            return string.Join(Environment.NewLine, lines);
        }
    }
}
