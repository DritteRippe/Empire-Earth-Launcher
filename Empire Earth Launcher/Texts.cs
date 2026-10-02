using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Integrity;
using Empire_Earth_Launcher.Core.Lobby;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Core.Repair;
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

        // --- Play (L-WP6, ADR 0010, contract 4.2 and 4.4) ----------------------------------------------------------------

        /// <summary>The programs of the selected installation with their file versions, one per line (forum report 8 row 1).</summary>
        internal static string ProgramVersions(IReadOnlyList<ProgramVersion> versions)
        {
            if (versions == null)
                throw new ArgumentNullException(nameof(versions));
            return string.Join(Environment.NewLine, versions.Select(version =>
                !version.Exists
                    ? string.Format(CultureInfo.CurrentCulture, Resources.PlayVersionMissingFormat, version.Game.ProgramName)
                    : version.Version == null
                        ? string.Format(CultureInfo.CurrentCulture, Resources.PlayVersionUnknownFormat, version.Game.ProgramName)
                        : string.Format(CultureInfo.CurrentCulture, Resources.PlayVersionFormat, version.Game.ProgramName,
                            version.Version)));
        }

        /// <summary>That a setup runs and what that blocks (contract 4.2); null if none runs.</summary>
        internal static string SetupRunning(Product setup)
        {
            return setup == null ? null : string.Format(CultureInfo.CurrentCulture, Resources.SetupRunningFormat, setup.AppName);
        }

        /// <summary>
        /// The message of a start: the short line of the Play page for <see cref="StartOutcome.Started"/>, the question for
        /// <see cref="StartOutcome.OtherGameRunning"/>, the explanation of a refusal or a start error otherwise (the first
        /// line of the repair advice for a damaged installation).
        /// </summary>
        internal static string StartMessage(StartResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            string program = result.Game.ProgramName;
            switch (result.Outcome)
            {
                case StartOutcome.Started:
                    return string.Format(CultureInfo.CurrentCulture, Resources.PlayStartedFormat, GameName(result.Game));
                case StartOutcome.SetupRunning:
                    return SetupRunning(result.RunningSetup);
                case StartOutcome.AlreadyRunning:
                    string running = string.Format(CultureInfo.CurrentCulture, Resources.StartAlreadyRunningFormat, program);
                    return result.ProcessFound
                        ? running + Environment.NewLine + Environment.NewLine +
                          string.Format(CultureInfo.CurrentCulture, Resources.StartHangingHintFormat, program)
                        : running;
                case StartOutcome.OtherGameRunning:
                    return string.Format(CultureInfo.CurrentCulture, Resources.StartOtherGameRunningFormat,
                        result.OtherGame.ProgramName, GameName(result.Game));
                case StartOutcome.FolderMissing:
                    return string.Format(CultureInfo.CurrentCulture, Resources.StartFolderMissingFormat,
                        result.Installation.GetGameFolder(result.Game));
                case StartOutcome.Damaged:
                    return string.Format(CultureInfo.CurrentCulture, Resources.StartDamagedFormat, result.ProgramPath);
                case StartOutcome.BlockedByAntivirus:
                    return string.Format(CultureInfo.CurrentCulture, Resources.StartBlockedByAntivirusFormat, result.ProgramPath,
                        result.ErrorCode);
                case StartOutcome.ElevationCancelled:
                    return Resources.StartElevationCancelled;
                case StartOutcome.AccessDenied:
                    return string.Format(CultureInfo.CurrentCulture, Resources.StartAccessDeniedFormat, result.ProgramPath,
                        result.ErrorCode);
                case StartOutcome.Failed:
                    return string.Format(CultureInfo.CurrentCulture, Resources.StartFailedFormat, result.ProgramPath,
                        result.ErrorCode, result.ErrorMessage);
                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result.Outcome, "Unknown start outcome.");
            }
        }

        /// <summary>One step of the repair advice (contract 4.4) with the folder and the install mode of the installation.</summary>
        internal static string RepairStep(RepairAdvice advice, RepairStep step)
        {
            if (advice == null)
                throw new ArgumentNullException(nameof(advice));
            switch (step)
            {
                case Core.Repair.RepairStep.AddAntivirusException:
                    return string.Format(CultureInfo.CurrentCulture, Resources.RepairStepAntivirusFormat, advice.Folder);
                case Core.Repair.RepairStep.CloseGameAndRunSetup:
                    return Resources.RepairStepRunSetup;
                case Core.Repair.RepairStep.KeepFolderAndMode:
                    string format;
                    switch (advice.Installation.Mode)
                    {
                        case InstallMode.Admin:
                            format = Resources.RepairStepKeepFolderAllUsersFormat;
                            break;
                        case InstallMode.User:
                            format = Resources.RepairStepKeepFolderCurrentUserFormat;
                            break;
                        case InstallMode.Portable:
                            format = Resources.RepairStepKeepFolderPortableFormat;
                            break;
                        default:
                            format = Resources.RepairStepKeepFolderFormat;
                            break;
                    }
                    return string.Format(CultureInfo.CurrentCulture, format, advice.Folder);
                case Core.Repair.RepairStep.KeepCdKeysTask:
                    return Resources.RepairStepCdKeys;
                case Core.Repair.RepairStep.ForeignNotRepaired:
                    return string.Format(CultureInfo.CurrentCulture, Resources.RepairStepForeignFormat, advice.Folder);
                default:
                    throw new ArgumentOutOfRangeException(nameof(step), step, "Unknown repair step.");
            }
        }

        /// <summary>The steps of the repair advice, numbered, one per line.</summary>
        internal static string RepairSteps(RepairAdvice advice)
        {
            if (advice == null)
                throw new ArgumentNullException(nameof(advice));
            return string.Join(Environment.NewLine, advice.Steps.Select((step, index) =>
                (index + 1).ToString(CultureInfo.CurrentCulture) + ". " + RepairStep(advice, step)));
        }

        /// <summary>
        /// Why the repair advice is shown, above its steps: the files of an integrity report (contract 2.5), the missing
        /// programs of a damaged installation, the available version (contract 4.5); null for the advice on request.
        /// </summary>
        /// <param name="advice">The advice.</param>
        /// <param name="report">The report the advice was made from (<see cref="RepairReason.IntegrityFindings"/>,
        /// <see cref="RepairReason.IntegrityUnknown"/>); null otherwise.</param>
        internal static string RepairReasonText(RepairAdvice advice, IntegrityReport report)
        {
            if (advice == null)
                throw new ArgumentNullException(nameof(advice));
            switch (advice.Reason)
            {
                case RepairReason.IntegrityFindings:
                case RepairReason.IntegrityUnknown:
                    if (report == null)
                        throw new ArgumentNullException(nameof(report), "The advice of a report needs the report.");
                    return IntegrityAdviceReason(report);
                case RepairReason.ProgramMissing:
                    Game first = advice.MissingPrograms.Count > 0 ? advice.MissingPrograms[0] : Game.EmpireEarth;
                    return string.Format(CultureInfo.CurrentCulture, Resources.InstallationDamagedFormat,
                        ProgramNames(advice.MissingPrograms), advice.Installation.GetGameFolder(first));
                case RepairReason.UpdateAvailable:
                    return VersionResult(advice.Update);
                default:
                    return null;
            }
        }

        // --- Integrity (L-WP7, contract 2.5 and 2.6) ------------------------------------------------------------------

        /// <summary>
        /// The short integrity state of the Play page: "checking" while a check runs, the state of the report, "unreliable"
        /// for two products in one folder (O11); empty without a report and for a foreign installation, which gets no message
        /// (contract 2.5). A community installation of a setup up to 1.7.2 gets its own badge and no dialog.
        /// </summary>
        internal static string IntegrityBadge(IntegrityReport report, bool checking)
        {
            if (checking)
                return Resources.IntegrityBadgeChecking;
            if (report == null || report.State == IntegrityState.NotChecked)
                return string.Empty;
            string badge;
            switch (report.State)
            {
                case IntegrityState.Ok:
                    badge = Resources.IntegrityBadgeOk;
                    break;
                case IntegrityState.Modified:
                    badge = Resources.IntegrityBadgeModified;
                    break;
                case IntegrityState.Incomplete:
                    badge = Resources.IntegrityBadgeIncomplete;
                    break;
                case IntegrityState.Damaged:
                    badge = IsNeutralChange(report) ? Resources.IntegrityBadgeChanged : Resources.IntegrityBadgeDamaged;
                    break;
                case IntegrityState.Unknown:
                    badge = report.UnknownReason == UnknownReason.LegacySetup
                        ? Resources.IntegrityBadgeLegacy
                        : Resources.IntegrityBadgeUnknown;
                    break;
                default:
                    badge = Resources.IntegrityBadgeCancelled;
                    break;
            }
            return report.IsUnreliable
                ? string.Format(CultureInfo.CurrentCulture, Resources.IntegrityBadgeUnreliableFormat, badge)
                : badge;
        }

        /// <summary>
        /// The explanation of an integrity report for the Tools page and the repair advice (contract 2.5): what the state
        /// means and what to do, plus the "unreliable" line of two products in one folder (O11). The files are listed by
        /// <see cref="IntegrityFiles"/>.
        /// </summary>
        internal static string IntegrityExplanation(IntegrityReport report)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));
            string text;
            switch (report.State)
            {
                case IntegrityState.NotChecked:
                    return report.Installation.State == InstallationState.FolderMissing
                        ? Resources.GameDirectorySourceUserMissing
                        : Resources.IntegrityNotChecked;
                case IntegrityState.Ok:
                    text = string.Format(CultureInfo.CurrentCulture,
                        report.Kind == IntegrityCheckKind.Full ? Resources.IntegrityOkFullFormat : Resources.IntegrityOkQuickFormat,
                        report.ListedFiles, report.HashedFiles);
                    break;
                case IntegrityState.Modified:
                    text = string.Format(CultureInfo.CurrentCulture, Resources.IntegrityModifiedFormat,
                        report.Findings.Count(finding => finding.State == IntegrityState.Modified));
                    break;
                case IntegrityState.Incomplete:
                case IntegrityState.Damaged:
                    text = IsNeutralChange(report) ? Resources.IntegrityChangedIntro : Resources.IntegrityFilesIntro;
                    break;
                case IntegrityState.Unknown:
                    text = UnknownExplanation(report.UnknownReason);
                    break;
                default:
                    text = report.CancelReason == CancelReason.SetupRunning
                        ? Resources.IntegrityCancelledSetup
                        : Resources.IntegrityCancelledRequested;
                    break;
            }
            if (report.IsUnreliable)
                text += Environment.NewLine + string.Format(CultureInfo.CurrentCulture, Resources.IntegrityUnreliableFormat,
                    report.Installation.OtherProductInRoot.AppName, report.Installation.Product.AppName);
            return text;
        }

        private static string UnknownExplanation(UnknownReason reason)
        {
            switch (reason)
            {
                case UnknownReason.LegacySetup:
                    return Resources.IntegrityUnknownLegacy;
                case UnknownReason.NewerContract:
                    return Resources.IntegrityUnknownNewerContract;
                case UnknownReason.OlderSetupRanAfter:
                    return Resources.IntegrityUnknownOlderSetup;
                case UnknownReason.FilesUnreadable:
                    return Resources.IntegrityUnknownUnreadable;
                default:
                    // NoInstallInfo, NoManifest, ManifestUnreadable, InvalidManifest: the records of the setup are missing
                    // or unusable.
                    return Resources.IntegrityUnknownNoRecords;
            }
        }

        /// <summary>
        /// True if the findings are only NeoEE program files with another checksum: the NeoEE updater may have replaced them,
        /// so they are worded neutrally (contract 2.6, O2).
        /// </summary>
        private static bool IsNeutralChange(IntegrityReport report)
        {
            return report.UsesNeutralWording && report.SeriousFindings.All(finding =>
                finding.Kind == FindingKind.HashDiffers && finding.Class == FileClass.Code);
        }

        /// <summary>One file of the findings with what is wrong with it; NeoEE program files are worded neutrally (O2).</summary>
        internal static string IntegrityFile(IntegrityFinding finding, bool neutral)
        {
            if (finding == null)
                throw new ArgumentNullException(nameof(finding));
            string format;
            switch (finding.Kind)
            {
                case FindingKind.Missing:
                    format = Resources.IntegrityFileMissingFormat;
                    break;
                case FindingKind.MissingAfterInstall:
                    format = Resources.IntegrityFileMissingAfterInstallFormat;
                    break;
                case FindingKind.HashDiffers:
                    format = finding.Class != FileClass.Code ? Resources.IntegrityFileModifiedFormat
                        : neutral ? Resources.IntegrityFileChangedFormat : Resources.IntegrityFileDamagedFormat;
                    break;
                default:
                    format = Resources.IntegrityFileUnreadableFormat;
                    break;
            }
            return string.Format(CultureInfo.CurrentCulture, format, finding.Path);
        }

        /// <summary>
        /// The files of <paramref name="report"/>, one per line, the missing and damaged ones first; at most
        /// <paramref name="maximum"/> lines and then "and n more files". Empty without findings.
        /// </summary>
        internal static string IntegrityFiles(IntegrityReport report, int maximum = int.MaxValue)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));
            bool neutral = report.UsesNeutralWording;
            IReadOnlyList<IntegrityFinding> serious = report.SeriousFindings;
            var seriousSet = new HashSet<IntegrityFinding>(serious);
            List<IntegrityFinding> files = serious.Concat(report.Findings.Where(finding => !seriousSet.Contains(finding))).ToList();
            var lines = files.Take(maximum).Select(finding => IntegrityFile(finding, neutral)).ToList();
            if (files.Count > maximum)
                lines.Add(string.Format(CultureInfo.CurrentCulture, Resources.IntegrityMoreFilesFormat, files.Count - maximum));
            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>The most files the repair advice lists; the Tools page lists all of them.</summary>
        internal const int MaxFilesInAdvice = 10;

        /// <summary>Why the repair advice of an integrity report is shown: the explanation and up to ten files (contract 2.5).</summary>
        internal static string IntegrityAdviceReason(IntegrityReport report)
        {
            string files = IntegrityFiles(report, MaxFilesInAdvice);
            return files.Length == 0
                ? IntegrityExplanation(report)
                : IntegrityExplanation(report) + Environment.NewLine + Environment.NewLine + files;
        }

        /// <summary>The progress line of the full check.</summary>
        internal static string IntegrityProgress(IntegrityProgress progress)
        {
            if (progress == null)
                throw new ArgumentNullException(nameof(progress));
            return string.Format(CultureInfo.CurrentCulture, Resources.IntegrityProgressFormat, progress.CheckedFiles,
                progress.TotalFiles);
        }

        // --- Update API (L-WP7, contract 4.3 and 4.5) ---------------------------------------------------------------

        /// <summary>The result of a version check in one line (contract 4.5).</summary>
        internal static string VersionResult(VersionCheckResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            bool game = result.Kind == VersionKind.Game;
            switch (result.Outcome)
            {
                case VersionCheckOutcome.UpToDate:
                    return string.Format(CultureInfo.CurrentCulture,
                        game ? Resources.VersionGameUpToDateFormat : Resources.VersionSetupUpToDateFormat, result.InstalledVersion);
                case VersionCheckOutcome.UpdateAvailable:
                    return string.Format(CultureInfo.CurrentCulture,
                        game ? Resources.VersionGameUpdateFormat : Resources.VersionSetupUpdateFormat, result.InstalledVersion,
                        result.LatestVersion);
                case VersionCheckOutcome.NotPossible:
                    return Resources.VersionNotPossible;
                default:
                    return string.Format(CultureInfo.CurrentCulture, Resources.VersionFailedFormat, Failure(result.Failure));
            }
        }

        /// <summary>The results of the version checks that ran, one per line; empty if none ran.</summary>
        internal static string VersionResults(VersionCheckResult game, VersionCheckResult setup)
        {
            var lines = new List<string>();
            if (game != null)
                lines.Add(VersionResult(game));
            if (setup != null && !(setup.Outcome == VersionCheckOutcome.NotPossible && game?.Outcome == VersionCheckOutcome.NotPossible))
                lines.Add(VersionResult(setup));
            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>Why the update API gave no usable answer, as a part of a sentence.</summary>
        internal static string Failure(FallbackReason reason)
        {
            switch (reason)
            {
                case FallbackReason.Timeout:
                    return Resources.FailureTimeout;
                case FallbackReason.TlsError:
                    return Resources.FailureTls;
                case FallbackReason.StatusNotOk:
                    return Resources.FailureStatus;
                case FallbackReason.UrlRejected:
                    return Resources.FailureUrlRejected;
                default:
                    return Resources.FailureNetwork;
            }
        }

        /// <summary>
        /// The note of the repair advice when the fixed download page is used because the update API gave no address
        /// (contract 4.3 step 3, "no silent fallbacks"); null for an address of the API, for an installation without an
        /// AppId (the fixed page is its page) and before the API was asked.
        /// </summary>
        internal static string DownloadFallback(SetupDownloadLocation location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));
            if (location.IsFromUpdateApi || location.Reason == FallbackReason.NoAppId || location.Reason == FallbackReason.NotAsked)
                return null;
            return string.Format(CultureInfo.CurrentCulture, Resources.RepairFallbackFormat, Failure(location.Reason));
        }
    }
}
