using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Diagnostics;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Graphics;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Backup;
using Empire_Earth_Launcher.Core.Integrity;
using Empire_Earth_Launcher.Core.Lobby;
using Empire_Earth_Launcher.Core.Maintenance;
using Empire_Earth_Launcher.Core.Mods;
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
        internal static string SetupRunning(SetupKind setup)
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
                case Core.Repair.RepairStep.RunSuiteSetupAgain:
                    return string.Format(CultureInfo.CurrentCulture, Resources.RepairStepRunSuiteFormat, advice.SuiteFolder);
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
        internal static string Failure(UpdateApiFailure reason)
        {
            switch (reason)
            {
                case UpdateApiFailure.Timeout:
                    return Resources.FailureTimeout;
                case UpdateApiFailure.TlsError:
                    return Resources.FailureTls;
                case UpdateApiFailure.StatusNotOk:
                    return Resources.FailureStatus;
                default:
                    return Resources.FailureNetwork;
            }
        }

        // --- Maintenance tools (L-WP8) -------------------------------------------------------------------------------

        /// <summary>A registry key as the Registry Editor names it (<c>HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\...</c>).</summary>
        internal static string RegistryKeyName(RegistryLocation key)
        {
            return RegFileWriter.KeyName(key);
        }

        /// <summary>A key the player can select in the registry cleanup, with the folder that no longer exists.</summary>
        internal static string CleanupOffered(CleanupItem item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            return string.Format(CultureInfo.CurrentCulture, Resources.CleanupOfferedFormat, RegistryKeyName(item.Entry.Key),
                item.Folder);
        }

        /// <summary>The line of a key of the read-only list: its advice (ADR 0007 plan review).</summary>
        internal static string CleanupAdvice(CleanupItem item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            CleanupAdvice advice = item.Advice;
            string key = RegistryKeyName(advice.Key);
            switch (advice.Code)
            {
                case CleanupAdviceCode.LauncherCanDelete:
                    return CleanupOffered(item);
                case CleanupAdviceCode.ExportThenDeleteAsAdministrator:
                    return string.Format(CultureInfo.CurrentCulture, Resources.CleanupAdviceExportFormat, key, advice.Folder);
                case CleanupAdviceCode.DoNotDeleteContainsCdKeys:
                    string cdKeys = advice.CdKeysExist == true ? Resources.CleanupCdKeysExist
                        : advice.CdKeysExist == false ? Resources.CleanupCdKeysMissing : Resources.CleanupCdKeysUnknown;
                    return string.Format(CultureInfo.CurrentCulture, Resources.CleanupAdviceDoNotDeleteFormat, key, cdKeys);
                case CleanupAdviceCode.KeepInstallationFound:
                    return string.Format(CultureInfo.CurrentCulture, Resources.CleanupKeepInstallationFormat, key,
                        item.Entry.Product.AppName);
                case CleanupAdviceCode.KeepFolderExists:
                    return string.Format(CultureInfo.CurrentCulture, Resources.CleanupKeepFolderFormat, key, advice.Folder);
                case CleanupAdviceCode.KeepDriveNotFixed:
                    return string.Format(CultureInfo.CurrentCulture, Resources.CleanupKeepDriveFormat, key, advice.Folder);
                case CleanupAdviceCode.KeepNoFolderNamed:
                    return string.Format(CultureInfo.CurrentCulture, Resources.CleanupKeepNoFolderFormat, key);
                case CleanupAdviceCode.KeepFolderUnknown:
                    return string.Format(CultureInfo.CurrentCulture, Resources.CleanupKeepFolderUnknownFormat, key, advice.Folder);
                default:
                    return string.Format(CultureInfo.CurrentCulture, Resources.CleanupKeepUnreadableFormat, key);
            }
        }

        /// <summary>The confirmation of the registry cleanup: the keys and the backup folder.</summary>
        internal static string CleanupConfirm(IEnumerable<CleanupItem> selection, string backupFolder)
        {
            return string.Format(CultureInfo.CurrentCulture, Resources.CleanupConfirmFormat,
                string.Join(Environment.NewLine, selection.Select(item => RegistryKeyName(item.Entry.Key))), backupFolder);
        }

        /// <summary>The result line of the registry cleanup.</summary>
        internal static string CleanupResult(CleanupResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            switch (result.Outcome)
            {
                case CleanupOutcome.Done:
                    return string.Format(CultureInfo.CurrentCulture, Resources.CleanupDoneFormat, result.Deleted.Count, result.BackupFolder);
                case CleanupOutcome.Blocked:
                    return Block(result.Block);
                case CleanupOutcome.NoLongerStale:
                    return Resources.CleanupNoLongerStale;
                case CleanupOutcome.BackupFailed:
                    return Resources.ResultBackupFailed;
                default:
                    return string.Format(CultureInfo.CurrentCulture, Resources.CleanupFailedFormat, result.BackupFile);
            }
        }

        /// <summary>The WON login files found, or that there are none.</summary>
        internal static string WonFiles(WonLoginFiles files)
        {
            if (files == null || files.ToMove.Count == 0)
                return Resources.WonNoFiles;
            return string.Format(CultureInfo.CurrentCulture, Resources.WonFilesFormat,
                string.Join(", ", files.ToMove.Select(file => file.Path)));
        }

        /// <summary>The result of the WON login reset; it names the backup folder, which contains login data.</summary>
        internal static string WonResult(WonResetResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            switch (result.Outcome)
            {
                case WonResetOutcome.Done:
                    return string.Format(CultureInfo.CurrentCulture, Resources.WonDoneFormat, result.Files.Count, result.BackupFolder);
                case WonResetOutcome.Partial:
                    return string.Format(CultureInfo.CurrentCulture, Resources.WonPartialFormat, result.BackupFolder,
                        string.Join(Environment.NewLine, result.Files.Where(file => file.Outcome != FileMoveOutcome.Moved)
                                                                     .Select(file => file.Source)));
                case WonResetOutcome.NothingToReset:
                    return Resources.WonNothingToReset;
                case WonResetOutcome.Blocked:
                    return Block(result.Block);
                case WonResetOutcome.ManifestUnusable:
                    return Resources.WonManifestUnusable;
                default:
                    return Resources.ResultBackupFailed;
            }
        }

        /// <summary>The state of the VirtualStore check: not affected, none, serious copies, other copies.</summary>
        internal static string VirtualStoreState(VirtualStoreReport report)
        {
            if (report == null)
                return Resources.ToolsChecking;
            if (!report.IsVirtualizable)
                return Resources.VirtualStoreNotVirtualized;
            if (report.Findings.Count == 0)
                return Resources.VirtualStoreNone;
            var lines = new List<string>();
            int serious = report.Findings.Count(finding => finding.IsSerious);
            if (serious > 0)
                lines.Add(string.Format(CultureInfo.CurrentCulture, Resources.VirtualStoreSeriousFormat, serious));
            if (serious < report.Findings.Count)
                lines.Add(string.Format(CultureInfo.CurrentCulture, Resources.VirtualStoreRuntimeFormat, report.Findings.Count - serious));
            if (report.ShadowingWrapperConfigs.Count > 0)
                lines.Add(string.Format(CultureInfo.CurrentCulture, Resources.VirtualStoreWrapperConfigFormat,
                    string.Join(", ", report.ShadowingWrapperConfigs.Select(finding => finding.VirtualStorePath))));
            if (report.Truncated)
                lines.Add(string.Format(CultureInfo.CurrentCulture, Resources.VirtualStoreTruncatedFormat, VirtualStoreScanner.MaxFiles));
            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>The copies of the VirtualStore check, serious ones first with the file they stand for.</summary>
        internal static string VirtualStoreFiles(VirtualStoreReport report)
        {
            if (report == null)
                return string.Empty;
            return string.Join(Environment.NewLine, report.Findings.Select(finding => finding.IsSerious
                ? string.Format(CultureInfo.CurrentCulture, Resources.VirtualStoreSeriousLineFormat, finding.VirtualStorePath, finding.GamePath)
                : finding.VirtualStorePath));
        }

        /// <summary>The number of saved games and scenarios, and the files a VirtualStore copy hides.</summary>
        internal static string SavedGamesState(IReadOnlyList<SavedGameFile> files)
        {
            if (files == null)
                return Resources.ToolsChecking;
            string count = string.Format(CultureInfo.CurrentCulture, Resources.SavesCountFormat,
                files.Count(file => file.Kind == SavedGameKind.SavedGame), files.Count(file => file.Kind == SavedGameKind.Scenario));
            List<string> hidden = files.Where(file => file.ShadowedPath != null).Select(file => file.ShadowedPath).ToList();
            return hidden.Count == 0
                ? count
                : count + Environment.NewLine + string.Format(CultureInfo.CurrentCulture, Resources.SavesShadowedFormat, string.Join(", ", hidden));
        }

        /// <summary>The result of the export.</summary>
        internal static string ExportResult(ExportResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            switch (result.Outcome)
            {
                case ExportOutcome.Done:
                    return string.Format(CultureInfo.CurrentCulture, Resources.ExportDoneFormat, result.Exported.Count, result.Folder);
                case ExportOutcome.Partial:
                    return string.Format(CultureInfo.CurrentCulture, Resources.ExportPartialFormat, result.Exported.Count, result.Folder,
                        string.Join(", ", result.Skipped.Select(skipped => skipped.Item1.Name)));
                case ExportOutcome.NothingToExport:
                    return Resources.ExportNothing;
                case ExportOutcome.TargetInsideGameFolder:
                case ExportOutcome.InvalidTarget:
                    return Resources.ExportInsideGameFolder;
                default:
                    return Resources.ExportFailed;
            }
        }

        /// <summary>The confirmation of an import that replaces files.</summary>
        internal static string ImportConfirm(ImportPlan plan)
        {
            return string.Format(CultureInfo.CurrentCulture, Resources.ImportConfirmFormat,
                string.Join(Environment.NewLine, plan.Importable.Where(file => file.Overwrites).Select(file => file.Target)));
        }

        /// <summary>
        /// The result of an import: the number, every file that was not imported with the reason, the note about names outside
        /// ASCII (multiplayer needs the same name), and the backup of replaced files.
        /// </summary>
        internal static string ImportResult(ImportResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (result.IsBlocked)
                return Block(result.Block);
            var lines = new List<string>
            {
                string.Format(CultureInfo.CurrentCulture, Resources.ImportResultFormat, result.ImportedCount, result.Files.Count)
            };
            foreach (var file in result.Files.Where(file => file.Item2 != ImportFileOutcome.Imported))
                lines.Add(string.Format(CultureInfo.CurrentCulture, Resources.ImportFileProblemFormat, file.Item1.Name, ImportProblem(file.Item1, file.Item2)));
            foreach (var file in result.Files.Where(file => file.Item2 == ImportFileOutcome.Imported && file.Item1.NameOutsideAscii))
                lines.Add(string.Format(CultureInfo.CurrentCulture, Resources.ImportNameNoteFormat, file.Item1.Name));
            if (result.BackupFolder != null)
                lines.Add(string.Format(CultureInfo.CurrentCulture, Resources.ImportBackupFormat, result.BackupFolder));
            return string.Join(Environment.NewLine, lines);
        }

        private static string ImportProblem(ImportCandidate file, ImportFileOutcome outcome)
        {
            switch (outcome)
            {
                case ImportFileOutcome.NotConfirmed:
                    return Resources.ImportNotConfirmed;
                case ImportFileOutcome.AccessDenied:
                    return Resources.ImportAccessDenied;
                case ImportFileOutcome.Failed:
                    return Resources.ImportFailed;
            }
            switch (file.Check)
            {
                case ImportCheck.WrongExtension:
                    return Resources.ImportCheckWrongExtension;
                case ImportCheck.NotAPlainName:
                    return Resources.ImportCheckNotAPlainName;
                case ImportCheck.NameOutsideAnsiCodePage:
                    return Resources.ImportCheckNameOutsideAnsi;
                case ImportCheck.TooLarge:
                    return Resources.ImportCheckTooLarge;
                case ImportCheck.AlreadyInPlace:
                    return Resources.ImportCheckAlreadyInPlace;
                case ImportCheck.ManifestFile:
                    return Resources.ImportCheckManifestFile;
                case ImportCheck.DuplicateName:
                    return Resources.ImportCheckDuplicateName;
                case ImportCheck.ManifestUnusable:
                    return Resources.ImportCheckManifestUnusable;
                default:
                    return Resources.ImportCheckNotFound;
            }
        }

        /// <summary>The result of the name check: none, all plain, or the names with characters outside printable ASCII.</summary>
        internal static string NameCheck(NameCheckReport report)
        {
            if (report == null)
                return Resources.ToolsChecking;
            if (report.NamesChecked == 0)
                return Resources.NamesNone;
            if (report.Warnings.Count == 0)
                return string.Format(CultureInfo.CurrentCulture, Resources.NamesOkFormat, report.NamesChecked);
            var lines = new List<string> { string.Format(CultureInfo.CurrentCulture, Resources.NamesWarningFormat, report.Warnings.Count) };
            lines.AddRange(report.Warnings.Select(warning => string.Format(CultureInfo.CurrentCulture,
                warning.Source == NameSource.LobbyProfile ? Resources.NameLobbyProfileFormat : Resources.NamePlayerFormat,
                GameName(warning.Game), warning.Name)));
            return string.Join(Environment.NewLine, lines);
        }

        // --- Network diagnostics and diagnostics report (L-WP9) ------------------------------------------------------

        /// <summary>The outage verdict of the network check (forum report section 8 row 9), as the state line of the section.</summary>
        internal static string NetworkVerdict(NetworkReport report)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));
            switch (report.Verdict)
            {
                case OutageVerdict.ServerAnswers:
                    return string.Format(CultureInfo.CurrentCulture, Resources.NetworkVerdictServerAnswersFormat, report.OnlinePlayers ?? 0);
                case OutageVerdict.ProbablyServerOutage:
                    return Resources.NetworkVerdictOutage;
                case OutageVerdict.ServerNameNotResolved:
                    return string.Format(CultureInfo.CurrentCulture, Resources.NetworkVerdictNameNotResolvedFormat,
                        report.Lookups.Count > 0 ? report.Lookups[0].Host : "?");
                case OutageVerdict.NoConnection:
                    return Resources.NetworkVerdictNoConnection;
                case OutageVerdict.NoServerReached:
                    return Resources.NetworkVerdictNoServerReached;
                case OutageVerdict.Undetermined:
                    return Resources.NetworkVerdictUndetermined;
                default:
                    return Resources.NetworkVerdictNotConfigured;
            }
        }

        /// <summary>The hints of the network check, one paragraph each; empty without hints.</summary>
        internal static string NetworkHints(NetworkReport report)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));
            return string.Join(Environment.NewLine + Environment.NewLine, report.Hints.Select(NetworkHintText));
        }

        /// <summary>One hint of the network check.</summary>
        internal static string NetworkHintText(NetworkHint hint)
        {
            switch (hint.Code)
            {
                case NetworkHintCode.NoConnection:
                    return Resources.NetworkHintNoConnection;
                case NetworkHintCode.IPv6Only:
                    return Resources.NetworkHintIPv6Only;
                case NetworkHintCode.VirtualAdapters:
                    return string.Format(CultureInfo.CurrentCulture, Resources.NetworkHintVirtualAdaptersFormat, hint.Count);
                case NetworkHintCode.SeveralAdapters:
                    return string.Format(CultureInfo.CurrentCulture, Resources.NetworkHintSeveralAdaptersFormat, hint.Count);
                case NetworkHintCode.CgnatAddress:
                    return Resources.NetworkHintCgnat;
                case NetworkHintCode.DsLite:
                    return Resources.NetworkHintDsLite;
                case NetworkHintCode.NoExternalIPv4:
                    return Resources.NetworkHintNoExternalIPv4;
                case NetworkHintCode.PrivateExternalAddress:
                    return Resources.NetworkHintPrivateExternal;
                case NetworkHintCode.RipHostingOff:
                    return string.Format(CultureInfo.CurrentCulture, Resources.NetworkHintRipOffFormat, GameName(hint.Game));
                default:
                    return string.Format(CultureInfo.CurrentCulture, Resources.NetworkHintCdKeyCheckFormat, GameName(hint.Game));
            }
        }

        /// <summary>
        /// The details of the network check, one line each: adapters (type, driver name, IPv4 where it may be shown, IPv6 as a
        /// class; ADR 0013 plan review), name lookups, update server, status server, the files of the game folders (read only)
        /// and the port forwarding table.
        /// </summary>
        internal static string NetworkDetails(NetworkReport report)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));
            var lines = new List<string>();
            if (report.Adapters.Problem != null)
                lines.Add(Resources.NetworkAdaptersUnreadable);
            lines.AddRange(report.Adapters.Adapters.Select(NetworkAdapterLine));
            lines.AddRange(report.Lookups.Select(NetworkLookup));
            lines.Add(NetworkUpdateApi(report));
            lines.Add(report.StatusEndpoint == null ? Resources.NetworkStatusNotConfigured
                : report.StatusServer == StatusServerAnswer.Answered
                    ? string.Format(CultureInfo.CurrentCulture, Resources.NetworkStatusAnsweredFormat, report.StatusEndpoint, report.OnlinePlayers ?? 0)
                    : string.Format(CultureInfo.CurrentCulture, Resources.NetworkStatusNoAnswerFormat, report.StatusEndpoint));
            lines.AddRange(report.NeoEeConfigs.Select(NetworkNeoEeConfig));
            lines.AddRange(report.WonLobbyConfigs.Select(NetworkWonLobbyConfig));
            lines.AddRange(report.UpnpInfos.Select(NetworkUpnpInfo));
            string target = report.ForwardingTarget == null
                ? Resources.NetworkPortsThisComputer
                : NetworkAddress(report.ForwardingTarget);
            PortForwarding first = report.PortForwarding.FirstOrDefault();
            foreach (PortForwarding table in report.PortForwarding)
            {
                if (table != first && table.SamePortsAs(first))
                    continue;
                string games = table == first && report.PortForwarding.All(other => other.SamePortsAs(first))
                    ? string.Join(", ", report.PortForwarding.Select(other => GameName(other.Game)))
                    : GameName(table.Game);
                lines.Add(string.Format(CultureInfo.CurrentCulture, Resources.NetworkPortsFormat, games, table, target));
            }
            return string.Join(Environment.NewLine, lines);
        }

        private static string NetworkAdapterLine(NetworkAdapter adapter)
        {
            List<string> ipv4 = adapter.Addresses.Where(address => address.IsIPv4).Select(address => NetworkAddress(address.Address) +
                (AddressClassifier.MayShow(address.Address) && address.PrefixLength > 0
                    ? "/" + address.PrefixLength.ToString(CultureInfo.InvariantCulture)
                    : string.Empty)).ToList();
            List<string> gateways = adapter.Gateways.Where(gateway => gateway.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                .Select(NetworkAddress).ToList();
            string text = string.Format(CultureInfo.CurrentCulture, Resources.NetworkAdapterFormat, NetworkKind(adapter.Kind),
                adapter.Description, ipv4.Count == 0 ? Resources.NetworkNone : string.Join(" ", ipv4),
                gateways.Count == 0 ? Resources.NetworkNone : string.Join(" ", gateways),
                NetworkIPv6(AddressClassifier.IPv6ClassOf(adapter.Addresses.Select(address => address.Address))));
            if (!adapter.IsUp)
                text += Resources.NetworkAdapterDisconnected;
            if (NetworkDiagnostics.IsVirtual(adapter))
                text += Resources.NetworkAdapterVirtual;
            return text;
        }

        /// <summary>An IPv4 address as the page may show it: the value if private or link-local, else its class (ADR 0013).</summary>
        internal static string NetworkAddress(System.Net.IPAddress address)
        {
            if (address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                return NetworkIPv6(IPv6Class.Global);
            return AddressClassifier.MayShow(address) ? address.ToString() : NetworkClass(AddressClassifier.ClassOf(address));
        }

        internal static string NetworkClass(IPv4Class addressClass)
        {
            switch (addressClass)
            {
                case IPv4Class.Private:
                    return Resources.NetworkClassPrivate;
                case IPv4Class.LinkLocal:
                    return Resources.NetworkClassLinkLocal;
                case IPv4Class.Cgnat:
                    return Resources.NetworkClassCgnat;
                case IPv4Class.Public:
                    return Resources.NetworkClassPublic;
                case IPv4Class.Unspecified:
                    return Resources.NetworkClassUnspecified;
                default:
                    return Resources.NetworkClassSpecial;
            }
        }

        private static string NetworkIPv6(IPv6Class addressClass)
        {
            switch (addressClass)
            {
                case IPv6Class.None:
                    return Resources.NetworkIPv6None;
                case IPv6Class.LinkLocalOnly:
                    return Resources.NetworkIPv6LinkLocal;
                default:
                    return Resources.NetworkIPv6Global;
            }
        }

        private static string NetworkKind(NetworkAdapterKind kind)
        {
            switch (kind)
            {
                case NetworkAdapterKind.Ethernet:
                    return Resources.NetworkKindEthernet;
                case NetworkAdapterKind.Wireless:
                    return Resources.NetworkKindWireless;
                case NetworkAdapterKind.Tunnel:
                    return Resources.NetworkKindTunnel;
                case NetworkAdapterKind.Ppp:
                    return Resources.NetworkKindPpp;
                case NetworkAdapterKind.MobileBroadband:
                    return Resources.NetworkKindMobile;
                default:
                    return Resources.NetworkKindOther;
            }
        }

        private static string NetworkLookup(DnsLookup lookup)
        {
            switch (lookup.Outcome)
            {
                case DnsOutcome.Resolved:
                    return string.Format(CultureInfo.CurrentCulture, Resources.NetworkDnsResolvedFormat, lookup.Host);
                case DnsOutcome.NotFound:
                    return string.Format(CultureInfo.CurrentCulture, Resources.NetworkDnsNotFoundFormat, lookup.Host);
                case DnsOutcome.Timeout:
                    return string.Format(CultureInfo.CurrentCulture, Resources.NetworkDnsTimeoutFormat, lookup.Host);
                default:
                    return string.Format(CultureInfo.CurrentCulture, Resources.NetworkDnsFailedFormat, lookup.Host);
            }
        }

        private static string NetworkUpdateApi(NetworkReport report)
        {
            HttpsResponse response = report.UpdateApiResponse;
            if (response == null)
                return Resources.NetworkApiNotAsked;
            if (response.Outcome == HttpsOutcome.Answered)
                return string.Format(CultureInfo.CurrentCulture, Resources.NetworkApiAnsweredFormat, response.StatusCode);
            UpdateApiFailure reason = response.Outcome == HttpsOutcome.Timeout ? UpdateApiFailure.Timeout
                : response.Outcome == HttpsOutcome.TlsError ? UpdateApiFailure.TlsError : UpdateApiFailure.NetworkError;
            return string.Format(CultureInfo.CurrentCulture, Resources.NetworkApiNoAnswerFormat, Failure(reason));
        }

        private static string NetworkFileState(Game game, string fileName, ConfigFileStatus status)
        {
            return string.Format(CultureInfo.CurrentCulture,
                status == ConfigFileStatus.Missing ? Resources.NetworkFileMissingFormat : Resources.NetworkFileUnreadableFormat,
                GameName(game), fileName);
        }

        private static string OnOff(bool? value)
        {
            return value == null ? "?" : value.Value ? Resources.NetworkOn : Resources.NetworkOff;
        }

        private static string Number(int? value)
        {
            return value == null ? "?" : value.Value.ToString(CultureInfo.CurrentCulture);
        }

        private static string NetworkNeoEeConfig(NeoEeConfig config)
        {
            if (config.Status != ConfigFileStatus.Read)
                return NetworkFileState(config.Game, NeoEeConfigReader.FileName, config.Status);
            return string.Format(CultureInfo.CurrentCulture, Resources.NetworkNeoEeCfgFormat, GameName(config.Game), OnOff(config.Active),
                config.Server ?? "?", Number(config.DefaultPort), Number(config.MemberPorts), OnOff(config.PortCheck), OnOff(config.TryUpnp));
        }

        private static string NetworkWonLobbyConfig(WonLobbyConfig config)
        {
            if (config.Status != ConfigFileStatus.Read)
                return NetworkFileState(config.Game, WonLobbyConfigReader.FileName, config.Status);
            string cdKeyCheck = config.CdKeyCheckInvalid ? Resources.NetworkInvalid
                : config.CdKeyCheck == null ? "?" : config.CdKeyCheck.Value ? "true" : "false";
            return string.Format(CultureInfo.CurrentCulture, Resources.NetworkWonLobbyFormat, GameName(config.Game), cdKeyCheck,
                Number(config.FileTransferPort), Number(config.LobbyPort));
        }

        private static string NetworkUpnpInfo(UpnpInfo info)
        {
            switch (info.Status)
            {
                case UpnpInfoStatus.Missing:
                    return string.Format(CultureInfo.CurrentCulture, Resources.NetworkUpnpMissingFormat, GameName(info.Game));
                case UpnpInfoStatus.Unreadable:
                    return NetworkFileState(info.Game, UpnpInfoParser.FileName, ConfigFileStatus.Unreadable);
                case UpnpInfoStatus.UnknownFormat:
                    return string.Format(CultureInfo.CurrentCulture, Resources.NetworkUpnpUnknownFormat, GameName(info.Game));
                default:
                    return string.Format(CultureInfo.CurrentCulture, Resources.NetworkUpnpFormat, GameName(info.Game),
                        info.ExternalAddressClass == null ? Resources.NetworkNone : NetworkClass(info.ExternalAddressClass.Value),
                        info.LocalAddress == null ? Resources.NetworkNone : NetworkAddress(info.LocalAddress));
            }
        }

        /// <summary>The result line of saving the diagnostics report.</summary>
        internal static string ReportSaved(ReportSaveResult result)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            switch (result.Outcome)
            {
                case ReportSaveOutcome.Saved:
                    return string.Format(CultureInfo.CurrentCulture, Resources.ReportSavedFormat, result.Path);
                case ReportSaveOutcome.InsideInstallation:
                    return Resources.ReportSaveRefused;
                default:
                    return string.Format(CultureInfo.CurrentCulture, Resources.ReportSaveFailedFormat, result.Problem);
            }
        }

        // --- Graphics page (launcher 1.1.0, ADR 0014) -------------------------------------------------------------------

        /// <summary>
        /// A size of the list of the graphics page: <c>1600x900 (16:9, recommended)</c>; the shape only for the usual ones
        /// (they are ratios, not words), "recommended" in the UI language for the size that fits this computer best.
        /// </summary>
        internal static string ResolutionChoice(ResolutionOption option)
        {
            if (option == null)
                throw new ArgumentNullException(nameof(option));
            var tags = new List<string>();
            switch (option.Aspect)
            {
                case AspectKind.FourByThree:
                    tags.Add("4:3");
                    break;
                case AspectKind.FiveByFour:
                    tags.Add("5:4");
                    break;
                case AspectKind.SixteenByTen:
                    tags.Add("16:10");
                    break;
                case AspectKind.SixteenByNine:
                    tags.Add("16:9");
                    break;
            }
            if (option.IsRecommended)
                tags.Add(Resources.GraphicsRecommendedTag);
            return tags.Count == 0 ? option.Size.ToString() : option.Size + " (" + string.Join(", ", tags) + ")";
        }

        /// <summary>The window size of one game, or "not set yet" if the registry has none.</summary>
        internal static string WindowSize(Game game, ScreenSize size)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            return size.IsEmpty
                ? string.Format(CultureInfo.CurrentCulture, Resources.GraphicsWindowSizeUnknownFormat, GameName(game))
                : string.Format(CultureInfo.CurrentCulture, Resources.GraphicsWindowSizeCurrentFormat, GameName(game), size);
        }

        /// <summary>The result of "Use this size": what was set (and where the backup is), that nothing changed, or why not.</summary>
        internal static string WindowSizeResult(GameSettingsResult result, ScreenSize size)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (!result.IsDone)
                return GameSettingsResult(result);
            return result.Changes.Count == 0 || result.BackupFolder == null
                ? string.Format(CultureInfo.CurrentCulture, Resources.GraphicsWindowSizeUnchangedFormat, size)
                : string.Format(CultureInfo.CurrentCulture, Resources.GraphicsWindowSizeDoneFormat, size, result.BackupFolder);
        }

        /// <summary>The name of the wrapper the setup installed, worded like the setup's own component captions.</summary>
        internal static string WrapperName(WrapperInfo wrapper)
        {
            if (wrapper == null)
                throw new ArgumentNullException(nameof(wrapper));
            switch (wrapper.Kind)
            {
                case WrapperKind.None:
                    return Resources.GraphicsWrapperNone;
                case WrapperKind.DirectX7:
                    return Resources.GraphicsWrapperDirectX7;
                case WrapperKind.DirectX9:
                    return Resources.GraphicsWrapperDirectX9;
                case WrapperKind.DgVoodoo:
                    return string.Format(CultureInfo.CurrentCulture, Resources.GraphicsWrapperDgVoodooFormat, wrapper.DirectXVersion,
                        wrapper.ApiLevel);
                default:
                    return wrapper.WrapperFile != null
                        ? string.Format(CultureInfo.CurrentCulture, Resources.GraphicsWrapperFileFormat, wrapper.WrapperFile)
                        : wrapper.Component != null
                            ? string.Format(CultureInfo.CurrentCulture, Resources.GraphicsWrapperUnknownFormat, wrapper.Component)
                            : Resources.GraphicsWrapperUnnamed;
            }
        }

        /// <summary>"Installed: ..." and, if the answer was judged from files, the remark that no setup record names it.</summary>
        internal static string WrapperInstalled(WrapperInfo wrapper)
        {
            if (wrapper == null)
                throw new ArgumentNullException(nameof(wrapper));
            string text = string.Format(CultureInfo.CurrentCulture, Resources.GraphicsWrapperInstalledFormat, WrapperName(wrapper));
            return wrapper.Source == WrapperSource.GameFolder ? text + Environment.NewLine + Resources.GraphicsWrapperGuessed : text;
        }

        /// <summary>
        /// The <c>dgVoodoo.conf</c> of each game as lines of the output API and the screen mode keys (<c>FullScreenMode = true</c>);
        /// a key the file does not have is "not set". Empty without a game to show.
        /// </summary>
        internal static string WrapperConfigs(IReadOnlyList<WrapperConfLine> configs)
        {
            if (configs == null)
                throw new ArgumentNullException(nameof(configs));
            var blocks = new List<string>();
            foreach (WrapperConfLine line in configs)
            {
                string game = GameName(line.Game);
                var lines = new List<string>();
                switch (line.File.Status)
                {
                    case ConfigFileStatus.Missing:
                        lines.Add(string.Format(CultureInfo.CurrentCulture, Resources.GraphicsConfMissingFormat, game));
                        break;
                    case ConfigFileStatus.Unreadable:
                        lines.Add(string.Format(CultureInfo.CurrentCulture, Resources.GraphicsConfUnreadableFormat, game));
                        break;
                    default:
                        lines.Add(string.Format(CultureInfo.CurrentCulture, Resources.GraphicsConfHeadingFormat, game));
                        if (line.File.IsVirtualStoreCopy)
                            lines.Add(Resources.GraphicsConfVirtualStore);
                        foreach (string key in new[] { DgVoodooConf.OutputApiKey }.Concat(DgVoodooConf.ScreenModeKeys))
                        {
                            DgVoodooConfEntry entry = line.File.Conf.Find(key);
                            lines.Add(key + " = " + (entry == null ? Resources.GraphicsConfNotSet : entry.Value));
                        }
                        break;
                }
                blocks.Add(string.Join(Environment.NewLine, lines));
            }
            return string.Join(Environment.NewLine + Environment.NewLine, blocks);
        }

        // --- Mods page (launcher 1.1.0) -----------------------------------------------------------------------------------

        /// <summary>The line of a running setup on the Mods page; null if none runs. The page reads and opens nothing then (contract 4.2).</summary>
        internal static string ModsSetupRunning(SetupKind setup)
        {
            return setup == null ? null : string.Format(CultureInfo.CurrentCulture, Resources.ModsSetupRunningFormat, setup.AppName);
        }

        /// <summary>
        /// The active mod and the active lobby theme that <c>dreXmod.config</c> of a game folder names, or why it tells nothing (not
        /// found, not readable, no selectors); a selected folder that does not exist and the VirtualStore copy get a line of their own.
        /// </summary>
        internal static string ModsSelection(GameModsLine line)
        {
            if (line == null)
                throw new ArgumentNullException(nameof(line));
            DreXmodConfigFile file = line.Config;
            switch (file.Status)
            {
                case ConfigFileStatus.Missing:
                    return string.Format(CultureInfo.CurrentCulture, Resources.ModsConfigMissingFormat, file.Path);
                case ConfigFileStatus.Unreadable:
                    return Resources.ModsConfigUnreadable;
            }
            if (!file.Config.HasModSystem)
                return Resources.ModsConfigNoSelectors;
            var lines = new List<string>
            {
                string.Format(CultureInfo.CurrentCulture, Resources.ModsActiveModFormat, ModsSelector(file.Config.Mod)),
                string.Format(CultureInfo.CurrentCulture, Resources.ModsActiveLobbyFormat, ModsSelector(file.Config.LobbyTheme)),
            };
            // A name that is no folder: dreXmod finds no preset to load (what it does then is not verified).
            foreach (DreXmodSelector selector in new[] { file.Config.Mod, file.Config.LobbyTheme })
            {
                if (selector.Enabled == true && selector.Name != null && line.Scan.Status == ConfigFileStatus.Read &&
                    !line.Scan.Presets.Any(preset => selector.Selects(preset.FolderName)))
                {
                    string missing = string.Format(CultureInfo.CurrentCulture, Resources.ModsSelectedMissingFormat, selector.Name);
                    if (!lines.Contains(missing))
                        lines.Add(missing);
                }
            }
            if (file.IsVirtualStoreCopy)
                lines.Add(Resources.ModsConfigVirtualStore);
            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>What a selector of <c>dreXmod.config</c> says: the folder it names, "none" if it is switched off, else "unknown".</summary>
        private static string ModsSelector(DreXmodSelector selector)
        {
            if (selector.Enabled == false)
                return Resources.ModsSelectorOff;
            return selector.Enabled == true && selector.Name != null ? selector.Name : Resources.ModsSelectorUnknown;
        }

        /// <summary>
        /// The presets of the folder <c>Data\dxm\mods</c> of a game, one block of one or two lines each; the folder <c>template</c> only if
        /// <paramref name="showTemplates"/> is set. If there is none, why.
        /// </summary>
        internal static string ModsPresets(GameModsLine line, bool showTemplates)
        {
            if (line == null)
                throw new ArgumentNullException(nameof(line));
            switch (line.Scan.Status)
            {
                case ConfigFileStatus.Missing:
                    return string.Format(CultureInfo.CurrentCulture, Resources.ModsFolderMissingFormat, line.Scan.Path);
                case ConfigFileStatus.Unreadable:
                    return string.Format(CultureInfo.CurrentCulture, Resources.ModsFolderUnreadableFormat, line.Scan.Path);
            }
            DreXmodConfig config = line.Config.Config;
            List<string> presets = line.Scan.Presets.Where(preset => showTemplates || !preset.IsTemplate)
                                       .Select(preset => ModsPreset(preset, config)).ToList();
            return presets.Count == 0 ? Resources.ModsNoPresets : string.Join(Environment.NewLine, presets);
        }

        /// <summary>
        /// One preset: its name (the name of the folder, which is what <c>dreXmod.config</c> names, with the name of its credits if
        /// that says more), the badges "active mod" and "active lobby theme" (<paramref name="config"/> may be null), and, in a
        /// second line, the last edit, the author and the size, as far as the files tell them.
        /// </summary>
        internal static string ModsPreset(ModPreset preset, DreXmodConfig config)
        {
            if (preset == null)
                throw new ArgumentNullException(nameof(preset));
            string name = preset.FolderName;
            string creditsName = preset.CreditsName;
            if (preset.IsTemplate)
                creditsName = null;
            else if (creditsName != null)
                name = creditsName.StartsWith(preset.FolderName, StringComparison.OrdinalIgnoreCase)
                    ? creditsName
                    : string.Format(CultureInfo.CurrentCulture, Resources.ModsPresetNameFormat, preset.FolderName, creditsName);

            var badges = new List<string>();
            if (preset.IsTemplate)
                badges.Add(Resources.ModsBadgeTemplate);
            if (config != null && config.Mod.Selects(preset.FolderName))
                badges.Add(Resources.ModsBadgeMod);
            if (config != null && config.LobbyTheme.Selects(preset.FolderName))
                badges.Add(Resources.ModsBadgeLobby);
            string first = badges.Count == 0
                ? name
                : string.Format(CultureInfo.CurrentCulture, Resources.ModsPresetBadgesFormat, name, string.Join(", ", badges));

            var details = new List<string>();
            ModCredits credits = preset.Credits;
            if (credits?.LastEdit != null)
                details.Add(string.Format(CultureInfo.CurrentCulture, Resources.ModsPresetLastEditFormat,
                    credits.LastEditDate.HasValue ? credits.LastEditDate.Value.ToString("d", CultureInfo.CurrentCulture) : credits.LastEdit));
            if (credits?.CreatedBy != null)
                details.Add(string.Format(CultureInfo.CurrentCulture, Resources.ModsPresetCreatedByFormat, credits.CreatedBy));
            details.Add(string.Format(CultureInfo.CurrentCulture, Resources.ModsPresetSizeFormat, ModsSize(preset)));
            return first + Environment.NewLine + "    " + string.Join("; ", details);
        }

        /// <summary>The size of a preset in KB or MB; "more than ..." if the scan did not follow every file.</summary>
        internal static string ModsSize(ModPreset preset)
        {
            if (preset == null)
                throw new ArgumentNullException(nameof(preset));
            const double Megabyte = 1024d * 1024d;
            string size = preset.SizeBytes < Megabyte
                ? string.Format(CultureInfo.CurrentCulture, Resources.ModsSizeKilobytesFormat,
                    Math.Max(1, Math.Round(preset.SizeBytes / 1024d)).ToString("0", CultureInfo.CurrentCulture))
                : string.Format(CultureInfo.CurrentCulture, Resources.ModsSizeMegabytesFormat,
                    (preset.SizeBytes / Megabyte).ToString("0.0", CultureInfo.CurrentCulture));
            return preset.IsSizeComplete ? size : string.Format(CultureInfo.CurrentCulture, Resources.ModsSizeMoreThanFormat, size);
        }
    }
}
