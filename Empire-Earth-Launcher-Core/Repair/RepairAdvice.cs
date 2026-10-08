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
        /// (no manifest, an older setup ran afterwards, ...): run the current setup (contract 2.5), for an installation of the
        /// suite the suite of the package.
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

        /// <summary>
        /// The suite "Empire Earth Community" installed the product (contract 1.6, revision 4) and its folder is still there:
        /// close the game, then run <c>Empire Earth Community Setup.exe</c> from that folder again; it repairs or updates the
        /// products it installed. Replaces <see cref="CloseGameAndRunSetup"/>; the download of the package stays the second
        /// option (contract 4.4; up to launcher 1.1.0 the download of the product setup).
        /// </summary>
        RunSuiteSetupAgain,

        /// <summary>
        /// The suite "Empire Earth Community" installed the product, but the folder it was started from is gone (since launcher
        /// 1.1.1): close the game, download the package again from its release page, unpack it and run
        /// <c>Empire Earth Community Setup.exe</c>. Replaces <see cref="CloseGameAndRunSetup"/>: the product setup of the
        /// community website is not the one the package installed and, with the same AppId, would replace it.
        /// </summary>
        DownloadPackageAndRunSuite,

        /// <summary>
        /// An available update of an installation of the suite (<see cref="RepairReason.UpdateAvailable"/>, since launcher
        /// 1.1.1): the update API reports a newer version of the setups of the community website, which the suite does not
        /// install; the package gets newer versions only as a new release, so the player looks on its release page whether
        /// there is one. Replaces <see cref="RunSuiteSetupAgain"/>, which would install the same versions again, and
        /// <see cref="CloseGameAndRunSetup"/>, whose setup would replace the installation of the package.
        /// </summary>
        UpdateWithNewPackage,

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
    /// The download page is the one of the installation's product (<see cref="SetupDownloadPage.For"/>, contract 4.3), or the
    /// release page of the package for an installation of the suite (<see cref="InstalledBySuite"/>): it is known from the
    /// start and needs no request to the update API.
    /// </remarks>
    public sealed class RepairAdvice
    {
        private RepairAdvice(Installation installation, RepairReason reason, IEnumerable<Game> missingPrograms,
            IEnumerable<IntegrityFinding> files, VersionCheckResult update, SuitePackage suite)
        {
            suite = SuitePackageOf(installation, suite);
            InstalledBySuite = suite != null;
            // The suite of the folder installs the versions it embeds; for an update it has nothing to offer.
            SuiteFolder = reason == RepairReason.UpdateAvailable ? null : suite?.Folder;
            Installation = installation;
            Reason = reason;
            MissingPrograms = new ReadOnlyCollection<Game>(missingPrograms.ToList());
            Steps = new ReadOnlyCollection<RepairStep>(StepsFor(installation, reason, suite).ToList());
            Files = new ReadOnlyCollection<IntegrityFinding>(files.ToList());
            Update = update;
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
        /// True if the suite "Empire Earth Community" installed the installation (its record lists the product, contract 1.6;
        /// <see cref="SuitePackage"/>): the advice leads to the package, with or without its folder, and
        /// <see cref="DownloadUrl"/> is the release page of the package instead of the page of the product setup.
        /// </summary>
        public bool InstalledBySuite { get; }

        /// <summary>
        /// The folder to run the suite from again (<see cref="RepairStep.RunSuiteSetupAgain"/>, contract 4.4): the
        /// <c>SourceDir</c> of the suite record, which lists the product and exists; null if the advice is the download only,
        /// and for an available update, which the suite of that folder cannot install. The launcher may open the folder in the
        /// Explorer and never starts a program from it (contract 4.1).
        /// </summary>
        public string SuiteFolder { get; }

        /// <summary>
        /// For <see cref="RepairReason.IntegrityFindings"/>: the missing and changed files the message names (contract 2.5),
        /// damaged ones first; empty otherwise.
        /// </summary>
        public IReadOnlyList<IntegrityFinding> Files { get; }

        /// <summary>For <see cref="RepairReason.UpdateAvailable"/>: the result of the version check; null otherwise.</summary>
        public VersionCheckResult Update { get; }

        /// <summary>
        /// The page with the setup download: the one of the product of the installation (contract 4.3), or, for an
        /// installation of the suite, the release page of the package (<see cref="SetupDownloadPage.PackageRelease"/>). The
        /// product page of the community website leads to the official product setup (in October 2026 version 1.7.2, contract
        /// 4.3 point 3), another build with the same AppId, which would replace the setups and fixes of the package; the advice
        /// of a suite installation never leads there.
        /// </summary>
        public string DownloadUrl
        {
            get { return InstalledBySuite ? SetupDownloadPage.PackageRelease : SetupDownloadPage.For(Installation); }
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
        /// <param name="suite">The package whose suite installed the installation, with the folder it can be run from again
        /// (<see cref="SuiteRepairLocator.PackageFor"/>); null if the record of the suite does not list the product or there
        /// is none.</param>
        public static RepairAdvice For(Installation installation, RepairReason reason, IEnumerable<Game> missingPrograms = null,
            SuitePackage suite = null)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            List<Game> missing = reason == RepairReason.ProgramMissing
                ? (missingPrograms ?? installation.MissingPrograms).Distinct().ToList()
                : new List<Game>();

            if (reason == RepairReason.IntegrityFindings || reason == RepairReason.IntegrityUnknown ||
                reason == RepairReason.UpdateAvailable)
                throw new ArgumentException("The advice of " + reason + " is made by ForIntegrity or ForUpdate.", nameof(reason));
            return new RepairAdvice(installation, reason, missing, new IntegrityFinding[0], null, suite);
        }

        /// <summary>
        /// The advice for a report that offers the repair (<see cref="IntegrityReport.OffersRepair"/>): Damaged and Incomplete
        /// name the files and put the antivirus exception first; an Unknown community installation gets the steps of a run of
        /// the current setup (contract 2.5).
        /// </summary>
        /// <param name="report">The report that offers the repair.</param>
        /// <param name="suite">As for <see cref="For"/>.</param>
        public static RepairAdvice ForIntegrity(IntegrityReport report, SuitePackage suite = null)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));
            if (!report.OffersRepair)
                throw new ArgumentException("The report " + report + " offers no repair.", nameof(report));
            RepairReason reason = report.State == IntegrityState.Unknown ? RepairReason.IntegrityUnknown : RepairReason.IntegrityFindings;
            return new RepairAdvice(report.Installation, reason, new Game[0], report.SeriousFindings, null, suite);
        }

        /// <summary>
        /// The hand-off for an available update (contract 4.5: "An available update uses the hand-off of 4.3"). For an
        /// installation of the suite it is the release page of the package with <see cref="RepairStep.UpdateWithNewPackage"/>:
        /// the update API knows the setups of the community website, not the package (since launcher 1.1.1).
        /// </summary>
        /// <param name="update">The result of the version check.</param>
        /// <param name="suite">As for <see cref="For"/>.</param>
        public static RepairAdvice ForUpdate(VersionCheckResult update, SuitePackage suite = null)
        {
            if (update == null)
                throw new ArgumentNullException(nameof(update));
            if (update.Outcome != VersionCheckOutcome.UpdateAvailable)
                throw new ArgumentException("No update is available: " + update, nameof(update));
            return new RepairAdvice(update.Installation, RepairReason.UpdateAvailable, new Game[0], new IntegrityFinding[0], update,
                suite);
        }

        /// <summary>The suite package that counts: none for a foreign installation, which the setups do not repair.</summary>
        private static SuitePackage SuitePackageOf(Installation installation, SuitePackage suite)
        {
            return installation.Kind == InstallationKind.Foreign ? null : suite;
        }

        /// <summary>
        /// The steps of contract 4.4: foreign installations are not repaired; the antivirus exception comes first when files
        /// were deleted or changed; community installations (also those of setups up to 1.7.2) get the run of the setup, and
        /// since revision 4 the run of the suite from its folder first, when the suite record lists the product (the download of
        /// the package stays as the second option). An installation of the suite whose folder is gone gets the download of the
        /// package, and an available update of it the hint that the package updates only with a new release (both since launcher
        /// 1.1.1), never the product setup of the community website.
        /// </summary>
        private static IEnumerable<RepairStep> StepsFor(Installation installation, RepairReason reason, SuitePackage suite)
        {
            var steps = new List<RepairStep>();
            if (installation.Kind == InstallationKind.Foreign)
                steps.Add(RepairStep.ForeignNotRepaired);
            if (reason == RepairReason.ProgramMissing || reason == RepairReason.IntegrityFindings)
                steps.Add(RepairStep.AddAntivirusException);
            if (installation.Kind != InstallationKind.Foreign)
            {
                steps.Add(SetupStepFor(reason, suite));
                steps.Add(RepairStep.KeepFolderAndMode);
                if (installation.Product == Product.NeoEE)
                    steps.Add(RepairStep.KeepCdKeysTask);
            }
            return steps;
        }

        /// <summary>The step that runs a setup: the product setup, the suite from its folder, or the package.</summary>
        private static RepairStep SetupStepFor(RepairReason reason, SuitePackage suite)
        {
            if (suite == null)
                return RepairStep.CloseGameAndRunSetup;
            if (reason == RepairReason.UpdateAvailable)
                return RepairStep.UpdateWithNewPackage;
            return suite.Folder != null ? RepairStep.RunSuiteSetupAgain : RepairStep.DownloadPackageAndRunSuite;
        }

        /// <summary>
        /// Opens <see cref="DownloadUrl"/> in the default browser through <paramref name="starter"/> (shell, no verb: not
        /// elevated, contract 4.3 point 2). No request is made; the browser follows the redirect of the website to the setup.
        /// A failure is logged and returned, never thrown (<see cref="SetupDownloadPage.Open"/>).
        /// </summary>
        public DownloadPageResult OpenDownloadPage(IProcessStarter starter, ILogger logger)
        {
            if (starter == null)
                throw new ArgumentNullException(nameof(starter));
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            logger.Info("Repair advice for " + Installation.Root + ": opening the download page " + DownloadUrl +
                        " (contract 4.3, " + SetupDownloadPage.NameOf(DownloadUrl) + " page).");
            return SetupDownloadPage.Open(DownloadUrl, starter, logger);
        }

        /// <summary>
        /// Opens <see cref="SuiteFolder"/> in the Explorer through <paramref name="starter"/> (contract 4.4: the folder, never a
        /// program of it; the path ends with a separator, so the shell takes it as a folder only). A failure is logged and
        /// returned, never thrown.
        /// </summary>
        /// <returns>True if the Explorer was started; false if the advice has no suite folder or the shell failed.</returns>
        public bool OpenSuiteFolder(IProcessStarter starter, ILogger logger)
        {
            if (starter == null)
                throw new ArgumentNullException(nameof(starter));
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (SuiteFolder == null)
                return false;
            logger.Info("Repair advice for " + Installation.Root + ": opening the folder of the suite setup " + SuiteFolder + ".");
            try
            {
                starter.OpenFolder(SuiteFolder);
                return true;
            }
            catch (Exception ex) when (ex is Win32Exception || ex is FileNotFoundException || ex is InvalidOperationException ||
                                       ex is ArgumentException)
            {
                logger.Warning("The folder " + SuiteFolder + " could not be opened.", ex);
                return false;
            }
        }

        /// <summary>One line for the log.</summary>
        public override string ToString()
        {
            return Reason + " for " + Installation.Product.Id + " " + Installation.Root +
                   (MissingPrograms.Count == 0 ? string.Empty : " (missing " + string.Join(", ", MissingPrograms.Select(g => g.ProgramName)) + ")") +
                   ": " + string.Join(", ", Steps) + "; " + DownloadUrl + (SuiteFolder == null ? string.Empty : "; suite folder " + SuiteFolder);
        }
    }
}
