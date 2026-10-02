using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Integrity;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Repair
{
    /// <summary>Why the launcher sends the player to the setup (contract 4).</summary>
    public enum RepairReason
    {
        /// <summary>
        /// The program of a game is missing (contract 1.4 "Validity"; at Play or found by the discovery), typically deleted
        /// or quarantined by an antivirus (forum 4.20, t=11045, t=41147).
        /// </summary>
        ProgramMissing,

        /// <summary>The player asked for the repair advice.</summary>
        Requested,

        /// <summary>
        /// The integrity check found files missing or changed (Damaged or Incomplete, contract 2.5): the advice names the
        /// files and puts the antivirus exception first (t=11045 p=48037, t=41147 p=80317).
        /// </summary>
        IntegrityFindings,

        /// <summary>
        /// The integrity state of a community installation is Unknown because its records are missing, unusable or outdated
        /// (no manifest, an older setup ran afterwards, ...): run the current setup (contract 2.5).
        /// </summary>
        IntegrityUnknown,

        /// <summary>The version check found a newer game or setup (contract 4.5): the hand-off of contract 4.3.</summary>
        UpdateAvailable
    }

    /// <summary>
    /// One step of the advice of contract 4.4, in the order the player does them. The UI turns each step into a localized
    /// sentence with the data of the advice (install root, mode, product).
    /// </summary>
    public enum RepairStep
    {
        /// <summary>Files were deleted: add an antivirus exception for the install root first, else the setup's files go again.</summary>
        AddAntivirusException,

        /// <summary>Close the game, then run the downloaded setup; it detects the installation and offers to update or repair it.</summary>
        CloseGameAndRunSetup,

        /// <summary>Keep the same folder (the install root) and the same install mode ("for all users" if it is <c>admin</c>).</summary>
        KeepFolderAndMode,

        /// <summary>NeoEE: keep the task "Register NeoEE CDKeys" selected; this is the way to repair the CD keys.</summary>
        KeepCdKeysTask,

        /// <summary>
        /// The installation was not made by the community setup (retail CD, GOG, a copy): the setup does not repair it, it
        /// installs its own copy.
        /// </summary>
        ForeignNotRepaired
    }

    /// <summary>How opening the download page ended.</summary>
    public enum DownloadPageResult
    {
        /// <summary>The shell opened the page in the default browser.</summary>
        Opened,

        /// <summary>The shell could not open it (no browser, blocked); logged, the UI shows the address to copy.</summary>
        Failed
    }

    /// <summary>
    /// The advice of contract 4.4 for one installation: which steps the player takes and where the setup is downloaded
    /// (R9, ARCHITECTURE 4.5). The setup is the only repair tool (contract 4.1): the launcher never downloads, starts or
    /// elevates anything for the repair, it opens the download page in the browser with its own rights.
    /// </summary>
    /// <remarks>
    /// An advice starts with the fixed page of contract 4.3 step 3 (<see cref="SetupDownloadLocation.NotAsked"/>); the
    /// launcher asks the update API through <see cref="SetupDownloadLocator"/> when it shows the advice and continues with
    /// <see cref="WithLocation"/> (contract 4.3 steps 1 and 2, L-WP7).
    /// </remarks>
    public sealed class RepairAdvice
    {
        /// <summary>The fixed download page of the community setup (contract 4.3 step 3).</summary>
        public const string DownloadPageUrl = SetupDownloadLocator.FixedPageUrl;

        private RepairAdvice(Installation installation, RepairReason reason, IEnumerable<Game> missingPrograms,
            IEnumerable<RepairStep> steps, IEnumerable<IntegrityFinding> files, VersionCheckResult update,
            SetupDownloadLocation location)
        {
            Installation = installation;
            Reason = reason;
            MissingPrograms = new ReadOnlyCollection<Game>(missingPrograms.ToList());
            Steps = new ReadOnlyCollection<RepairStep>(steps.ToList());
            Files = new ReadOnlyCollection<IntegrityFinding>(files.ToList());
            Update = update;
            Location = location;
        }

        /// <summary>The installation the advice is for.</summary>
        public Installation Installation { get; }

        public RepairReason Reason { get; }

        /// <summary>The games whose program is missing (<see cref="RepairReason.ProgramMissing"/>); empty otherwise.</summary>
        public IReadOnlyList<Game> MissingPrograms { get; }

        /// <summary>
        /// The folder the advice names (the antivirus exception, "keep the folder"): the install root of a community
        /// installation, the EE folder of a foreign one, whose root can be a whole drive (<c>D:\</c>).
        /// </summary>
        public string Folder
        {
            get { return Installation.Kind == InstallationKind.Foreign ? Installation.EeFolder : Installation.Root; }
        }

        /// <summary>The steps of contract 4.4 that apply, in order.</summary>
        public IReadOnlyList<RepairStep> Steps { get; }

        /// <summary>
        /// For <see cref="RepairReason.IntegrityFindings"/>: the missing and changed files the message names (contract 2.5),
        /// damaged ones first; empty otherwise.
        /// </summary>
        public IReadOnlyList<IntegrityFinding> Files { get; }

        /// <summary>For <see cref="RepairReason.UpdateAvailable"/>: the result of the version check; null otherwise.</summary>
        public VersionCheckResult Update { get; }

        /// <summary>Where the setup is downloaded: the fixed page until the update API answered (contract 4.3).</summary>
        public SetupDownloadLocation Location { get; }

        /// <summary>The page with the setup download (contract 4.3).</summary>
        public string DownloadUrl
        {
            get { return Location.Url; }
        }

        /// <summary>True if the page is the fixed one of contract 4.3 step 3 (the update API was not asked or gave no URL).</summary>
        public bool IsFixedPage
        {
            get { return !Location.IsFromUpdateApi; }
        }

        /// <summary>
        /// The advice for <paramref name="installation"/>: community installations (also those of setups up to 1.7.2,
        /// which the current setup takes over by their AppId) get the steps of a repair run, foreign ones the note that
        /// the setup installs its own copy; an antivirus exception comes first when files were deleted.
        /// </summary>
        /// <param name="installation">The installation.</param>
        /// <param name="reason">Why the advice is given.</param>
        /// <param name="missingPrograms">With <see cref="RepairReason.ProgramMissing"/>: the games whose program is missing;
        /// null for those of <see cref="Installations.Installation.MissingPrograms"/>.</param>
        public static RepairAdvice For(Installation installation, RepairReason reason, IEnumerable<Game> missingPrograms = null)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            List<Game> missing = reason == RepairReason.ProgramMissing
                ? (missingPrograms ?? installation.MissingPrograms).Distinct().ToList()
                : new List<Game>();

            if (reason == RepairReason.IntegrityFindings || reason == RepairReason.IntegrityUnknown ||
                reason == RepairReason.UpdateAvailable)
                throw new ArgumentException("The advice of " + reason + " is made by ForIntegrity or ForUpdate.", nameof(reason));
            return new RepairAdvice(installation, reason, missing, StepsFor(installation, reason), new IntegrityFinding[0], null,
                SetupDownloadLocation.NotAsked);
        }

        /// <summary>
        /// The advice for a report that offers the repair (<see cref="IntegrityReport.OffersRepair"/>): Damaged and Incomplete
        /// name the files and put the antivirus exception first; an Unknown community installation gets the steps of a run of
        /// the current setup (contract 2.5).
        /// </summary>
        public static RepairAdvice ForIntegrity(IntegrityReport report)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));
            if (!report.OffersRepair)
                throw new ArgumentException("The report " + report + " offers no repair.", nameof(report));
            RepairReason reason = report.State == IntegrityState.Unknown ? RepairReason.IntegrityUnknown : RepairReason.IntegrityFindings;
            return new RepairAdvice(report.Installation, reason, new Game[0], StepsFor(report.Installation, reason),
                report.SeriousFindings, null, SetupDownloadLocation.NotAsked);
        }

        /// <summary>The hand-off for an available update (contract 4.5: "An available update uses the hand-off of 4.3").</summary>
        public static RepairAdvice ForUpdate(VersionCheckResult update)
        {
            if (update == null)
                throw new ArgumentNullException(nameof(update));
            if (update.Outcome != VersionCheckOutcome.UpdateAvailable)
                throw new ArgumentException("No update is available: " + update, nameof(update));
            return new RepairAdvice(update.Installation, RepairReason.UpdateAvailable, new Game[0],
                StepsFor(update.Installation, RepairReason.UpdateAvailable), new IntegrityFinding[0], update,
                SetupDownloadLocation.NotAsked);
        }

        /// <summary>The same advice with the download of <paramref name="location"/> (the answer of the update API).</summary>
        public RepairAdvice WithLocation(SetupDownloadLocation location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));
            return new RepairAdvice(Installation, Reason, MissingPrograms, Steps, Files, Update, location);
        }

        /// <summary>
        /// The steps of contract 4.4: foreign installations are not repaired; the antivirus exception comes first when files
        /// were deleted or changed; community installations (also those of setups up to 1.7.2) get the run of the setup.
        /// </summary>
        private static IEnumerable<RepairStep> StepsFor(Installation installation, RepairReason reason)
        {
            var steps = new List<RepairStep>();
            if (installation.Kind == InstallationKind.Foreign)
                steps.Add(RepairStep.ForeignNotRepaired);
            if (reason == RepairReason.ProgramMissing || reason == RepairReason.IntegrityFindings)
                steps.Add(RepairStep.AddAntivirusException);
            if (installation.Kind != InstallationKind.Foreign)
            {
                steps.Add(RepairStep.CloseGameAndRunSetup);
                steps.Add(RepairStep.KeepFolderAndMode);
                if (installation.Product == Product.NeoEE)
                    steps.Add(RepairStep.KeepCdKeysTask);
            }
            return steps;
        }

        /// <summary>
        /// Opens <see cref="DownloadUrl"/> in the default browser through <paramref name="starter"/> (shell, no verb: not
        /// elevated, contract 4.3 step 4). A failure is logged and returned, never thrown.
        /// </summary>
        public DownloadPageResult OpenDownloadPage(IProcessStarter starter, ILogger logger)
        {
            if (starter == null)
                throw new ArgumentNullException(nameof(starter));
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            logger.Info("Repair advice for " + Installation.Root + ": opening the download page " + DownloadUrl + " (" +
                        (IsFixedPage ? "the fixed page of contract 4.3: " + Location.Reason : "named by the update API") + ").");
            try
            {
                starter.OpenUrl(DownloadUrl);
                return DownloadPageResult.Opened;
            }
            catch (Exception ex) when (ex is Win32Exception || ex is FileNotFoundException || ex is InvalidOperationException)
            {
                logger.Warning("The download page " + DownloadUrl + " could not be opened.", ex);
                return DownloadPageResult.Failed;
            }
        }

        /// <summary>One line for the log.</summary>
        public override string ToString()
        {
            return Reason + " for " + Installation.Product.Id + " " + Installation.Root +
                   (MissingPrograms.Count == 0 ? string.Empty : " (missing " + string.Join(", ", MissingPrograms.Select(g => g.ProgramName)) + ")") +
                   ": " + string.Join(", ", Steps) + "; " + DownloadUrl;
        }
    }
}
