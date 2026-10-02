using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;

namespace Empire_Earth_Launcher.Core.Integrity
{
    /// <summary>Which check ran (contract 2.5).</summary>
    public enum IntegrityCheckKind
    {
        /// <summary>At every start and after a setup, in the background: every listed file exists, the <c>code</c> files are hashed.</summary>
        Quick,

        /// <summary>On request: also the <c>data</c> files are hashed.</summary>
        Full
    }

    /// <summary>The integrity state of an installation (contract 2.5), worst first after <see cref="Unknown"/>.</summary>
    public enum IntegrityState
    {
        /// <summary>
        /// No check: a <c>foreign</c> installation (contract 2.5: "no check and no message"), or a folder that does not exist.
        /// </summary>
        NotChecked,

        /// <summary>Every listed file exists and every hashed file has its hash.</summary>
        Ok,

        /// <summary>A <c>data</c> file has another hash (mods, HD packs, edited civilizations): informative only.</summary>
        Modified,

        /// <summary>A <c>data</c> or <c>mutable</c> file is missing, or listed in <c>[MissingAfterInstall]</c>.</summary>
        Incomplete,

        /// <summary>A <c>code</c> file is missing, has another hash, or is listed in <c>[MissingAfterInstall]</c>.</summary>
        Damaged,

        /// <summary>No usable manifest; <see cref="IntegrityReport.UnknownReason"/> says why.</summary>
        Unknown,

        /// <summary>
        /// The check was cancelled or not started (a setup runs, contract 4.2, or the player cancelled it): no findings.
        /// </summary>
        Cancelled
    }

    /// <summary>Why the state is <see cref="IntegrityState.Unknown"/> (contract 2.5, 5).</summary>
    public enum UnknownReason
    {
        None,

        /// <summary>Kind <c>community-legacy</c>: installed by a setup up to 1.7.2, which writes no manifest.</summary>
        LegacySetup,

        /// <summary><c>ContractVersion</c> higher than the launcher knows (contract 5): update the launcher.</summary>
        NewerContract,

        /// <summary>
        /// Kind <c>community</c> without a usable <c>install.ini</c> (missing, unreadable or without a contract version): the
        /// last setup run did not finish.
        /// </summary>
        NoInstallInfo,

        /// <summary>No manifest: the last setup run did not finish, or the setup could not write it (e.g. a path not in ASCII).</summary>
        NoManifest,

        /// <summary>The manifest exists but cannot be read (denied, in use, larger than 16 MiB).</summary>
        ManifestUnreadable,

        /// <summary>The manifest or <c>[MissingAfterInstall]</c> has an invalid line or an unsafe path.</summary>
        InvalidManifest,

        /// <summary>
        /// The uninstall key of the installation names its root but lacks <c>Empire Earth Community: ContractVersion</c>: an
        /// older setup ran afterwards, or the last setup could not replace its records (contract 2.5).
        /// </summary>
        OlderSetupRanAfter,

        /// <summary>No other finding, but files could not be read (in use, denied): the check could not finish.</summary>
        FilesUnreadable
    }

    /// <summary>Why a check is <see cref="IntegrityState.Cancelled"/>.</summary>
    public enum CancelReason
    {
        None,

        /// <summary>A setup mutex exists (contract 4.2): not started, or cancelled when it appeared.</summary>
        SetupRunning,

        /// <summary>The player or the launcher cancelled it (button, closing, another installation).</summary>
        Requested
    }

    /// <summary>What is wrong with one file.</summary>
    public enum FindingKind
    {
        /// <summary>The file listed in the manifest does not exist.</summary>
        Missing,

        /// <summary>The file has another SHA-256 than the manifest says.</summary>
        HashDiffers,

        /// <summary>The setup found the file gone after the installation (<c>[MissingAfterInstall]</c> of <c>install.ini</c>).</summary>
        MissingAfterInstall,

        /// <summary>The file exists but could not be read (in use, access denied); its state is not known.</summary>
        Unreadable
    }

    /// <summary>One finding of the check, with everything the log and the message need (contract 2.5).</summary>
    public sealed class IntegrityFinding
    {
        internal IntegrityFinding(string path, string fullPath, FileClass fileClass, FindingKind kind, string expectedHash,
            string actualHash, string detail)
        {
            Path = path;
            FullPath = fullPath;
            Class = fileClass;
            Kind = kind;
            ExpectedHash = expectedHash;
            ActualHash = actualHash;
            Detail = detail;
        }

        /// <summary>The path as the manifest or <c>[MissingAfterInstall]</c> writes it (<c>/</c> as separator).</summary>
        public string Path { get; }

        /// <summary>The full Windows path below the install root.</summary>
        public string FullPath { get; }

        public FileClass Class { get; }

        public FindingKind Kind { get; }

        /// <summary>The hash of the manifest; null for <see cref="FindingKind.MissingAfterInstall"/> (not in the manifest).</summary>
        public string ExpectedHash { get; }

        /// <summary>The hash of the file; null unless <see cref="FindingKind.HashDiffers"/>.</summary>
        public string ActualHash { get; }

        /// <summary>For <see cref="FindingKind.Unreadable"/>: the error, for the log; else null.</summary>
        public string Detail { get; }

        /// <summary>The state this finding alone gives (contract 2.5); <see cref="IntegrityState.Ok"/> if it gives none.</summary>
        public IntegrityState State
        {
            get
            {
                switch (Kind)
                {
                    case FindingKind.Missing:
                    case FindingKind.MissingAfterInstall:
                        return Class == FileClass.Code ? IntegrityState.Damaged : IntegrityState.Incomplete;
                    case FindingKind.HashDiffers:
                        return Class == FileClass.Code ? IntegrityState.Damaged
                            : Class == FileClass.Data ? IntegrityState.Modified : IntegrityState.Ok;
                    default:
                        return IntegrityState.Ok;
                }
            }
        }

        /// <summary>The log line of the finding: path, class, kind, expected and actual hash (contract 2.5).</summary>
        public override string ToString()
        {
            string expected = ExpectedHash ?? "none (not in the manifest)";
            string actual;
            switch (Kind)
            {
                case FindingKind.HashDiffers:
                    actual = ActualHash;
                    break;
                case FindingKind.Unreadable:
                    actual = "unknown (" + Detail + ")";
                    break;
                default:
                    actual = "none (missing)";
                    break;
            }
            return Path + " (" + FileClassifier.Name(Class) + "): " + KindName(Kind) + "; expected " + expected + ", actual " +
                   actual;
        }

        private static string KindName(FindingKind kind)
        {
            switch (kind)
            {
                case FindingKind.Missing:
                    return "missing";
                case FindingKind.HashDiffers:
                    return "hash differs";
                case FindingKind.MissingAfterInstall:
                    return "missing after the installation ([MissingAfterInstall] of install.ini)";
                default:
                    return "cannot be read";
            }
        }
    }

    /// <summary>The result of an integrity check of one installation (contract 2.5).</summary>
    public sealed class IntegrityReport
    {
        private IntegrityReport(Installation installation, IntegrityCheckKind kind, IntegrityState state,
            UnknownReason unknownReason, CancelReason cancelReason, IList<IntegrityFinding> findings, int listedFiles,
            int hashedFiles)
        {
            Installation = installation;
            Kind = kind;
            State = state;
            UnknownReason = unknownReason;
            CancelReason = cancelReason;
            Findings = new ReadOnlyCollection<IntegrityFinding>(findings);
            ListedFiles = listedFiles;
            HashedFiles = hashedFiles;
        }

        public Installation Installation { get; }

        public IntegrityCheckKind Kind { get; }

        public IntegrityState State { get; }

        /// <summary>Why the state is <see cref="IntegrityState.Unknown"/>; <see cref="Integrity.UnknownReason.None"/> otherwise.</summary>
        public UnknownReason UnknownReason { get; }

        /// <summary>Why the state is <see cref="IntegrityState.Cancelled"/>; <see cref="Integrity.CancelReason.None"/> otherwise.</summary>
        public CancelReason CancelReason { get; }

        /// <summary>
        /// The findings in the order of the manifest, then those of <c>[MissingAfterInstall]</c>; changed <c>mutable</c> files
        /// are never findings. Empty for <see cref="IntegrityState.Cancelled"/>, <see cref="IntegrityState.NotChecked"/> and
        /// an Unknown without a usable manifest.
        /// </summary>
        public IReadOnlyList<IntegrityFinding> Findings { get; }

        /// <summary>The files of the manifest.</summary>
        public int ListedFiles { get; }

        /// <summary>The files whose hash was computed.</summary>
        public int HashedFiles { get; }

        /// <summary>
        /// True if EE and NeoEE are installed in the same root (contract 1.4, O11): the manifest of one product cannot vouch
        /// for files the other's setup replaced, so the state is "unreliable" whatever it says.
        /// </summary>
        public bool IsUnreliable
        {
            get { return Installation.HasUnreliableIntegrity; }
        }

        /// <summary>
        /// True if the message of a <c>code</c> file with another hash must be neutral (contract 2.6, O2): "changed since the
        /// installation" instead of "damaged", because the NeoEE updater may replace NeoEE files.
        /// </summary>
        public bool UsesNeutralWording
        {
            get
            {
                return Installation.Product == Product.NeoEE &&
                       Findings.Any(finding => finding.Kind == FindingKind.HashDiffers && finding.Class == FileClass.Code);
            }
        }

        /// <summary>
        /// True if the launcher offers the repair with the setup (contract 2.5): Damaged, Incomplete, and Unknown of a
        /// <c>community</c> installation whose records are missing, unusable or outdated. Not for legacy installations (only
        /// the badge says to run the current setup), a newer contract (update the launcher), unreadable files (check again)
        /// and foreign installations.
        /// </summary>
        public bool OffersRepair
        {
            get
            {
                if (State == IntegrityState.Damaged || State == IntegrityState.Incomplete)
                    return true;
                if (State != IntegrityState.Unknown)
                    return false;
                switch (UnknownReason)
                {
                    case UnknownReason.NoInstallInfo:
                    case UnknownReason.NoManifest:
                    case UnknownReason.ManifestUnreadable:
                    case UnknownReason.InvalidManifest:
                    case UnknownReason.OlderSetupRanAfter:
                        return true;
                    default:
                        return false;
                }
            }
        }

        /// <summary>
        /// The findings behind <see cref="State"/> for the message of contract 2.5 (Damaged, Incomplete): missing files and
        /// files with another hash, worst first, in manifest order within a state.
        /// </summary>
        public IReadOnlyList<IntegrityFinding> SeriousFindings
        {
            get
            {
                return Findings.Where(finding => finding.State == IntegrityState.Damaged || finding.State == IntegrityState.Incomplete)
                               .OrderBy(finding => finding.State == IntegrityState.Damaged ? 0 : 1)
                               .ToList();
            }
        }

        /// <summary>A report without a check.</summary>
        internal static IntegrityReport NotChecked(Installation installation, IntegrityCheckKind kind)
        {
            return new IntegrityReport(installation, kind, IntegrityState.NotChecked, UnknownReason.None, CancelReason.None,
                new IntegrityFinding[0], 0, 0);
        }

        /// <summary>An Unknown report without findings.</summary>
        internal static IntegrityReport Unknown(Installation installation, IntegrityCheckKind kind, UnknownReason reason)
        {
            return new IntegrityReport(installation, kind, IntegrityState.Unknown, reason, CancelReason.None,
                new IntegrityFinding[0], 0, 0);
        }

        /// <summary>A cancelled check: no findings (contract 4.2).</summary>
        internal static IntegrityReport Cancelled(Installation installation, IntegrityCheckKind kind, CancelReason reason)
        {
            return new IntegrityReport(installation, kind, IntegrityState.Cancelled, UnknownReason.None, reason,
                new IntegrityFinding[0], 0, 0);
        }

        /// <summary>
        /// A finished check: the worst finding gives the state (Damaged, then Incomplete, then Modified, then OK); unreadable
        /// files without another finding make it Unknown (<see cref="Integrity.UnknownReason.FilesUnreadable"/>).
        /// </summary>
        internal static IntegrityReport Finished(Installation installation, IntegrityCheckKind kind,
            IList<IntegrityFinding> findings, int listedFiles, int hashedFiles)
        {
            IntegrityState state = Worst(findings.Select(finding => finding.State));
            UnknownReason reason = UnknownReason.None;
            if (state == IntegrityState.Ok && findings.Any(finding => finding.Kind == FindingKind.Unreadable))
            {
                state = IntegrityState.Unknown;
                reason = UnknownReason.FilesUnreadable;
            }
            return new IntegrityReport(installation, kind, state, reason, CancelReason.None, findings, listedFiles, hashedFiles);
        }

        /// <summary>The worst of <paramref name="states"/> by contract 2.5; <see cref="IntegrityState.Ok"/> for none.</summary>
        public static IntegrityState Worst(IEnumerable<IntegrityState> states)
        {
            if (states == null)
                throw new ArgumentNullException(nameof(states));
            IntegrityState worst = IntegrityState.Ok;
            foreach (IntegrityState state in states)
            {
                if (Rank(state) > Rank(worst))
                    worst = state;
            }
            return worst;
        }

        private static int Rank(IntegrityState state)
        {
            switch (state)
            {
                case IntegrityState.Damaged:
                    return 3;
                case IntegrityState.Incomplete:
                    return 2;
                case IntegrityState.Modified:
                    return 1;
                case IntegrityState.Ok:
                    return 0;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, "Only the states of findings are ranked.");
            }
        }

        /// <summary>One line for the log.</summary>
        public override string ToString()
        {
            string detail = State == IntegrityState.Unknown ? " (" + UnknownReason + ")"
                : State == IntegrityState.Cancelled ? " (" + CancelReason + ")" : string.Empty;
            return Kind.ToString().ToLowerInvariant() + " check of " + Installation.Root + ": " + State + detail + ", " +
                   ListedFiles.ToString(CultureInfo.InvariantCulture) + " files listed, " +
                   HashedFiles.ToString(CultureInfo.InvariantCulture) + " hashed, " +
                   Findings.Count.ToString(CultureInfo.InvariantCulture) + " findings" +
                   (IsUnreliable ? ", unreliable (" + Installation.OtherProductInRoot.Id + " shares the root)" : string.Empty);
        }
    }
}
