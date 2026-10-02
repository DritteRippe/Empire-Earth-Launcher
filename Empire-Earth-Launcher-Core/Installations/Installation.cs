using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Installations
{
    /// <summary>
    /// One installation of Empire Earth found by the discovery (contract 1.4): product, install root, the real EE folder
    /// and AoC folder (ADR 0015), install mode, AppId, versions, sources and kind. Created by the discovery, read-only
    /// for everyone else.
    /// </summary>
    /// <remarks>
    /// The EE and AoC folders are the folders the games really run from: for community installations
    /// <c>&lt;root&gt;\Empire Earth</c> and <c>&lt;root&gt;\Empire Earth - The Art of Conquest</c>, for foreign ones the
    /// folders named by the "Installed From" values or chosen by the user (e.g. <c>C:\Games\EE</c>, or
    /// <c>D:\Empire Earth</c> with the root <c>D:\</c>). Everything that later works with the game folders (default
    /// settings, Play, lobby profiles) uses these folders, never a folder name of its own.
    /// </remarks>
    public sealed class Installation
    {
        private readonly ReadOnlyCollection<InstallationSource> sources;
        private ReadOnlyCollection<Game> missingPrograms;

        internal Installation(Product product, string root, string eeFolder, string aocFolder, InstallationKind kind,
            InstallMode mode, IEnumerable<InstallationSource> sources)
        {
            Product = product ?? throw new ArgumentNullException(nameof(product));
            Root = root ?? throw new ArgumentNullException(nameof(root));
            EeFolder = eeFolder ?? throw new ArgumentNullException(nameof(eeFolder));
            AocFolder = aocFolder;
            Kind = kind;
            Mode = mode;
            this.sources = new ReadOnlyCollection<InstallationSource>(
                (sources ?? throw new ArgumentNullException(nameof(sources))).Distinct().OrderBy(source => source).ToList());
            if (this.sources.Count == 0)
                throw new ArgumentException("An installation has at least one source.", nameof(sources));
            missingPrograms = new ReadOnlyCollection<Game>(new Game[0]);
        }

        private Installation(Installation other, IEnumerable<InstallationSource> sources)
            : this(other.Product, other.Root, other.EeFolder, other.AocFolder, other.Kind, other.Mode, sources)
        {
            AppId = other.AppId;
            GameVersion = other.GameVersion;
            SetupVersion = other.SetupVersion;
            SetupBuild = other.SetupBuild;
            ContractVersion = other.ContractVersion;
            Components = other.Components;
            Tasks = other.Tasks;
            InstallInfo = other.InstallInfo;
            RecordKey = other.RecordKey;
            UninstallKey = other.UninstallKey;
            InstalledFromKey = other.InstalledFromKey;
            OtherProductInRoot = other.OtherProductInRoot;
            State = other.State;
            missingPrograms = other.missingPrograms;
        }

        /// <summary>EE or NeoEE. It decides the game settings keys (contract 3.1).</summary>
        public Product Product { get; }

        /// <summary>The install root, a full path in the normal form of <see cref="WinPath"/> (a drive root keeps its backslash).</summary>
        public string Root { get; }

        /// <summary>The real EE folder (the folder of <c>Empire Earth.exe</c>).</summary>
        public string EeFolder { get; }

        /// <summary>The real AoC folder (the folder of <c>EE-AOC.exe</c>); null if The Art of Conquest is not installed.</summary>
        public string AocFolder { get; }

        /// <summary>Community setup since v2, community setup up to 1.7.2, or foreign.</summary>
        public InstallationKind Kind { get; }

        /// <summary>Install mode, as far as a source names it.</summary>
        public InstallMode Mode { get; }

        /// <summary>Every source that found this installation, in the order of the contract (user choice first).</summary>
        public IReadOnlyList<InstallationSource> Sources
        {
            get { return sources; }
        }

        /// <summary>
        /// The most specific source of the data (2 before 3 before 4 before 5); <see cref="InstallationSource.UserChoice"/>
        /// only if nothing else found it.
        /// </summary>
        public InstallationSource Origin
        {
            get
            {
                InstallationSource[] found = sources.Where(source => source != InstallationSource.UserChoice).ToArray();
                return found.Length == 0 ? InstallationSource.UserChoice : found.Min();
            }
        }

        /// <summary>AppId without braces (record, <c>install.ini</c> or uninstall key name); null for foreign installations.</summary>
        public string AppId { get; internal set; }

        /// <summary>Game version of the setup; null if unknown.</summary>
        public string GameVersion { get; internal set; }

        /// <summary>Version of the setup; null if unknown.</summary>
        public string SetupVersion { get; internal set; }

        /// <summary>Build identifier of the setup; null if unknown or not set.</summary>
        public string SetupBuild { get; internal set; }

        /// <summary>Contract version of the setup run (<c>install.ini</c>, else the record); 0 for older and foreign installations.</summary>
        public int ContractVersion { get; internal set; }

        /// <summary>
        /// True if the installation was made by a setup of a newer contract version than this launcher knows (contract 5):
        /// only root, product and AppId are used, no defaults and no reset, and the user is advised to update the launcher.
        /// </summary>
        public bool HasNewerContract
        {
            get { return ContractVersion > ContractNames.ContractVersion; }
        }

        /// <summary>Components of the last setup run (<c>install.ini</c>, else the uninstall key); null if unknown.</summary>
        public SetupNameList Components { get; internal set; }

        /// <summary>Tasks of the last setup run (<c>install.ini</c>, else the uninstall key); null if unknown.</summary>
        public SetupNameList Tasks { get; internal set; }

        /// <summary>The <c>install.ini</c> the installation uses; null if there is none.</summary>
        public InstallInfoFile InstallInfo { get; internal set; }

        /// <summary>The registry record that found it (contract 1.1); null if none did.</summary>
        public RegistryLocation RecordKey { get; internal set; }

        /// <summary>The uninstall key that found it (contract 1.3, needed for the rule of contract 2.5); null if none did.</summary>
        public RegistryLocation UninstallKey { get; internal set; }

        /// <summary>The settings key whose "Installed From" values found it; null if none did.</summary>
        public RegistryLocation InstalledFromKey { get; internal set; }

        /// <summary>
        /// The other product, if EE and NeoEE are both installed in this root (contract 1.4, "Two products in one root",
        /// O11): the launcher uses the product installed last and the integrity state is unreliable. Null otherwise.
        /// </summary>
        public Product OtherProductInRoot { get; internal set; }

        /// <summary>Whether the programs are there (contract 1.4, "Validity").</summary>
        public InstallationState State { get; internal set; }

        /// <summary>The games whose program is missing (<see cref="InstallationState.Damaged"/>); empty otherwise.</summary>
        public IReadOnlyList<Game> MissingPrograms
        {
            get { return missingPrograms; }
            internal set { missingPrograms = new ReadOnlyCollection<Game>(value.ToList()); }
        }

        /// <summary>True if The Art of Conquest belongs to the installation.</summary>
        public bool HasArtOfConquest
        {
            get { return AocFolder != null; }
        }

        /// <summary>The real folder of <paramref name="game"/>; null for The Art of Conquest if it is not installed.</summary>
        public string GetGameFolder(Game game)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            return game == Game.EmpireEarth ? EeFolder : AocFolder;
        }

        /// <summary>The HKCU game settings key of <paramref name="game"/> (contract 3.1).</summary>
        public string GetGameSettingsKey(Game game)
        {
            return Product.GetGameSettingsKey(game);
        }

        /// <summary>True if <paramref name="path"/> is the install root, the EE folder or the AoC folder.</summary>
        public bool HasFolder(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            return WinPath.IsSamePath(path, Root) || WinPath.IsSamePath(path, EeFolder) ||
                   (AocFolder != null && WinPath.IsSamePath(path, AocFolder));
        }

        /// <summary>A copy with the user choice added to its sources.</summary>
        internal Installation WithUserChoice()
        {
            return sources.Contains(InstallationSource.UserChoice)
                ? this
                : new Installation(this, sources.Concat(new[] { InstallationSource.UserChoice }));
        }

        /// <summary>One line for the log.</summary>
        public override string ToString()
        {
            string state = State == InstallationState.Damaged
                ? "damaged (missing " + string.Join(", ", missingPrograms.Select(game => game.ProgramName)) + ")"
                : State == InstallationState.FolderMissing ? "folder missing" : "ok";
            return string.Format(CultureInfo.InvariantCulture,
                "{0} {1} ({2}), root {3}, EE folder {4}, AoC folder {5}, state {6}, contract {7}, sources {8}{9}",
                Product.Id, KindName(Kind), Mode.ToString().ToLowerInvariant(), Root, EeFolder, AocFolder ?? "none", state,
                ContractVersion, string.Join(",", sources.Select(source => ((int)source).ToString(CultureInfo.InvariantCulture))),
                OtherProductInRoot == null ? string.Empty : ", shares its root with " + OtherProductInRoot.Id);
        }

        private static string KindName(InstallationKind kind)
        {
            switch (kind)
            {
                case InstallationKind.Community:
                    return "community";
                case InstallationKind.CommunityLegacy:
                    return "community-legacy";
                default:
                    return "foreign";
            }
        }
    }
}
