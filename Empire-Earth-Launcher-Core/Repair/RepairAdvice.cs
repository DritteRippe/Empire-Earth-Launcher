using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
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
        Requested
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
    /// Until L-WP7 adds the update API (<c>SetupDownloadLocator</c>, contract 4.3 steps 1 and 2), the download page is
    /// always the fixed page of contract 4.3 step 3, <see cref="DownloadPageUrl"/>.
    /// </remarks>
    public sealed class RepairAdvice
    {
        /// <summary>The fixed download page of the community setup (contract 4.3 step 3).</summary>
        public const string DownloadPageUrl = "https://empireearth.eu/download";

        private RepairAdvice(Installation installation, RepairReason reason, IEnumerable<Game> missingPrograms,
            IEnumerable<RepairStep> steps, string downloadUrl)
        {
            Installation = installation;
            Reason = reason;
            MissingPrograms = new ReadOnlyCollection<Game>(missingPrograms.ToList());
            Steps = new ReadOnlyCollection<RepairStep>(steps.ToList());
            DownloadUrl = downloadUrl;
        }

        /// <summary>The installation the advice is for.</summary>
        public Installation Installation { get; }

        public RepairReason Reason { get; }

        /// <summary>The games whose program is missing (<see cref="RepairReason.ProgramMissing"/>); empty otherwise.</summary>
        public IReadOnlyList<Game> MissingPrograms { get; }

        /// <summary>The steps of contract 4.4 that apply, in order.</summary>
        public IReadOnlyList<RepairStep> Steps { get; }

        /// <summary>The page with the setup download (contract 4.3).</summary>
        public string DownloadUrl { get; }

        /// <summary>True while the page is the fixed one of contract 4.3 step 3 (always until L-WP7).</summary>
        public bool IsFixedPage
        {
            get { return DownloadUrl == DownloadPageUrl; }
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

            var steps = new List<RepairStep>();
            if (installation.Kind == InstallationKind.Foreign)
                steps.Add(RepairStep.ForeignNotRepaired);
            if (reason == RepairReason.ProgramMissing)
                steps.Add(RepairStep.AddAntivirusException);
            if (installation.Kind != InstallationKind.Foreign)
            {
                steps.Add(RepairStep.CloseGameAndRunSetup);
                steps.Add(RepairStep.KeepFolderAndMode);
                if (installation.Product == Product.NeoEE)
                    steps.Add(RepairStep.KeepCdKeysTask);
            }
            return new RepairAdvice(installation, reason, missing, steps, DownloadPageUrl);
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
            logger.Info("Repair advice for " + Installation.Root + ": opening the download page " + DownloadUrl +
                        (IsFixedPage ? " (the fixed page of contract 4.3; the update API is not asked)." : "."));
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
