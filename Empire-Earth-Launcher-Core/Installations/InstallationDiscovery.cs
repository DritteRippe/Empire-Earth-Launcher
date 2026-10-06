using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Installations
{
    /// <summary>
    /// Finds every installation of Empire Earth (contract 1.4): the five sources, the merge by install root, the kinds,
    /// damaged installations, two products in one root, the AoC folder, the default selection. Replaces the old
    /// <c>GameDirectoryLocator</c> of the launcher. Read-only: it never writes, repairs or deletes anything.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Sources: 1 the user choice (it only selects), 2 the registry records (<see cref="InstallRecordReader"/>), 3 the
    /// uninstall keys (<see cref="UninstallKeyScanner"/>; not the suite's own key: marker, or the suite root of the suite
    /// record, contract revision 5), 4 the "Installed From" values, key before hive
    /// (<see cref="InstalledFromReader"/>), 5 the launcher folder or its parent.
    /// </para>
    /// <para>
    /// Errors are results (ADR 0013): a missing key or value, denied access or an invalid path drops only that
    /// candidate, with exactly one log line; the discovery never fails as a whole. Programming errors (null arguments)
    /// throw, and a cancellation ends it with <see cref="OperationCanceledException"/>.
    /// </para>
    /// </remarks>
    public sealed class InstallationDiscovery
    {
        private readonly IRegistry registry;
        private readonly IFileSystem fileSystem;
        private readonly ILogger logger;

        public InstallationDiscovery(IRegistry registry, IFileSystem fileSystem, ILogger logger)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Runs <see cref="Discover"/> on a thread of the pool: registry and file system calls can block (network drives,
        /// a busy disk), and the caller, the UI thread, must not wait for them (ADR 0004).
        /// </summary>
        public Task<DiscoveryResult> DiscoverAsync(string userChoice, string launcherFolder,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Discover(userChoice, launcherFolder, cancellationToken), cancellationToken);
        }

        /// <summary>Finds the installations.</summary>
        /// <param name="userChoice">The folder chosen in the launcher settings (install root, EE folder or AoC folder);
        /// null or white space for automatic detection.</param>
        /// <param name="launcherFolder">The folder of the launcher (source 5); null to skip it.</param>
        /// <param name="cancellationToken">Ends the discovery between two candidates.</param>
        public DiscoveryResult Discover(string userChoice, string launcherFolder, CancellationToken cancellationToken = default)
        {
            var run = new Run(this, cancellationToken);
            return run.Execute(string.IsNullOrWhiteSpace(userChoice) ? null : userChoice.Trim(), launcherFolder);
        }

        /// <summary>A candidate of one source: an install root, and the folders the source names.</summary>
        private sealed class Candidate
        {
            public InstallationSource Source;
            public int Sequence;
            public string Root;

            /// <summary>The EE folder the source names explicitly; null for "&lt;root&gt;\Empire Earth".</summary>
            public string EeFolder;

            /// <summary>The AoC folder the source names explicitly; null if none.</summary>
            public string AocFolder;

            public InstallRecord Record;
            public UninstallEntry Uninstall;
            public InstalledFromEntry InstalledFrom;

            /// <summary>The folder the user chose (user choice only).</summary>
            public string ChosenFolder;

            /// <summary>
            /// True if the chosen folder exists and is an EE folder, an AoC folder or an install root (user choice only):
            /// only then does the root it implies say which installation it belongs to.
            /// </summary>
            public bool Recognized;
        }

        /// <summary>A readable <c>install.ini</c> of a root.</summary>
        private sealed class InstallInfo
        {
            public Product Product;
            public InstallInfoFile File;
            public DateTime LastWriteTimeUtc;
        }

        /// <summary>The state of one discovery.</summary>
        private sealed class Run
        {
            private readonly InstallationDiscovery owner;
            private readonly CancellationToken cancellationToken;
            private readonly List<List<Candidate>> groups = new List<List<Candidate>>();
            private readonly Dictionary<string, List<InstallInfo>> installInfos =
                new Dictionary<string, List<InstallInfo>>(WinPath.Comparer);
            private readonly HashSet<string> sharedRootsLogged = new HashSet<string>(WinPath.Comparer);
            private int sequence;

            public Run(InstallationDiscovery owner, CancellationToken cancellationToken)
            {
                this.owner = owner;
                this.cancellationToken = cancellationToken;
            }

            private IFileSystem FileSystem
            {
                get { return owner.fileSystem; }
            }

            private ILogger Logger
            {
                get { return owner.logger; }
            }

            public DiscoveryResult Execute(string userChoice, string launcherFolder)
            {
                foreach (InstallRecord record in new InstallRecordReader(owner.registry, Logger).Read(cancellationToken))
                {
                    AddIfFolderExists(new Candidate
                    {
                        Source = InstallationSource.RegistryRecord, Root = record.Root, Record = record
                    }, record.Root, "the install record " + record.Key);
                }

                // The suite record is no source: it only lets source 3 skip the uninstall key of a suite built before
                // contract revision 5 (no marker).
                SuiteRecord suite = new SuiteRecordReader(owner.registry, Logger).Read();
                foreach (UninstallEntry entry in new UninstallKeyScanner(owner.registry, Logger, suite).Scan(cancellationToken))
                {
                    AddIfFolderExists(new Candidate
                    {
                        Source = InstallationSource.UninstallKey, Root = entry.Root, Uninstall = entry
                    }, entry.Root, "the uninstall key " + entry.Key);
                }

                foreach (InstalledFromEntry entry in new InstalledFromReader(owner.registry, Logger).Read(cancellationToken))
                {
                    // Source 4 names the EE folder; it must exist (contract 1.4, "Validity").
                    AddIfFolderExists(new Candidate
                    {
                        Source = InstallationSource.InstalledFrom, Root = entry.Root, EeFolder = entry.EeFolder,
                        AocFolder = entry.AocFolder, InstalledFrom = entry
                    }, entry.EeFolder, "the \"Installed From\" values of " + entry.Key);
                }

                cancellationToken.ThrowIfCancellationRequested();
                Candidate launcher = FromLauncherFolder(launcherFolder);
                if (launcher != null)
                    Add(launcher, "the launcher folder " + launcherFolder);

                var installations = new List<Installation>();
                foreach (List<Candidate> group in groups)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    installations.Add(Build(group));
                }

                int selected = userChoice == null ? -1 : SelectUserChoice(userChoice, installations);

                // Ordered by the sources (user choice first), then by the order in which they were found.
                List<int> order = Enumerable.Range(0, installations.Count)
                    .OrderBy(index => installations[index].Sources.Min())
                    .ThenBy(index => groups[index].Min(candidate => candidate.Sequence))
                    .ToList();
                var ordered = order.Select(index => installations[index]).ToList();
                Installation selection = selected >= 0 ? installations[selected] : ordered.FirstOrDefault();

                foreach (Installation installation in ordered)
                    Logger.Info("Discovery: installation " + installation + ".");
                if (selection != null)
                {
                    Logger.Info("Discovery: " + ordered.Count + " installation(s) found, selected " + selection.Root +
                                (selected >= 0 ? " (chosen by the user)." : " (the first one found)."));
                }
                return new DiscoveryResult(ordered, selection, selected >= 0, userChoice);
            }

            private void AddIfFolderExists(Candidate candidate, string folder, string where)
            {
                if (!FileSystem.DirectoryExists(folder))
                {
                    Logger.Warning("Discovery: " + where + " names " + folder + ", which does not exist; ignored.");
                    return;
                }
                Add(candidate, where);
            }

            private void Add(Candidate candidate, string where)
            {
                candidate.Sequence = sequence++;
                Logger.Info("Discovery: found the install root " + candidate.Root + " through " + where + ".");
                List<Candidate> group = groups.FirstOrDefault(existing => WinPath.IsSamePath(existing[0].Root, candidate.Root));
                if (group == null)
                    groups.Add(new List<Candidate> { candidate });
                else
                    group.Add(candidate);
            }

            /// <summary>Source 5: the launcher folder or its parent, if it is an EE folder or an install root.</summary>
            private Candidate FromLauncherFolder(string launcherFolder)
            {
                if (string.IsNullOrWhiteSpace(launcherFolder) || !WinPath.IsFullyQualified(launcherFolder))
                    return null;
                string folder = WinPath.Normalize(launcherFolder);
                foreach (string candidate in new[] { folder, WinPath.GetParent(folder) })
                {
                    if (candidate == null)
                        continue;
                    if (GameFolders.IsEmpireEarthFolder(FileSystem, candidate) && WinPath.GetParent(candidate) != null)
                    {
                        return new Candidate
                        {
                            Source = InstallationSource.LauncherFolder, Root = WinPath.GetParent(candidate), EeFolder = candidate
                        };
                    }
                    if (GameFolders.IsInstallRoot(FileSystem, candidate))
                        return new Candidate { Source = InstallationSource.LauncherFolder, Root = candidate };
                }
                return null;
            }

            /// <summary>
            /// Source 1: selects the installation the chosen folder belongs to (its root, EE folder or AoC folder, or the
            /// root the folder implies); a folder that belongs to none becomes an installation of its own, also when it
            /// does not exist (contract 1.4: the choice is kept so that the user sees it).
            /// </summary>
            /// <returns>The index of the selected installation in <paramref name="installations"/>.</returns>
            private int SelectUserChoice(string choice, List<Installation> installations)
            {
                int index = installations.FindIndex(installation => installation.HasFolder(choice));
                if (index >= 0)
                {
                    installations[index] = installations[index].WithUserChoice();
                    Logger.Info("Discovery: the chosen folder " + choice + " selects the installation in " +
                                installations[index].Root + ".");
                    return index;
                }

                // A folder that is not recognized (missing, or without a program) implies no root: it stays an installation
                // of its own, so that the user sees exactly the folder they chose.
                Candidate user = FromUserChoice(choice);
                index = user.Recognized ? groups.FindIndex(group => WinPath.IsSamePath(group[0].Root, user.Root)) : -1;
                if (index >= 0)
                {
                    groups[index].Add(user);
                    installations[index] = Build(groups[index]);
                    Logger.Info("Discovery: the chosen folder " + choice + " belongs to the installation in " +
                                installations[index].Root + ".");
                    return index;
                }

                groups.Add(new List<Candidate> { user });
                installations.Add(Build(groups[groups.Count - 1]));
                if (FileSystem.DirectoryExists(user.ChosenFolder))
                    Logger.Info("Discovery: the chosen folder " + choice + " is used as the installation in " + user.Root + ".");
                else
                    Logger.Warning("Discovery: the chosen folder " + choice + " does not exist; it stays selected.");
                return installations.Count - 1;
            }

            private Candidate FromUserChoice(string choice)
            {
                string folder = WinPath.IsFullyQualified(choice) ? WinPath.Normalize(choice) : choice;
                var candidate = new Candidate
                {
                    Source = InstallationSource.UserChoice, Sequence = sequence++, ChosenFolder = folder
                };
                string parent = WinPath.IsFullyQualified(folder) ? WinPath.GetParent(folder) ?? folder : folder;
                GameFolderKind kind = FileSystem.DirectoryExists(folder) ? GameFolders.Classify(FileSystem, folder) : GameFolderKind.None;
                candidate.Recognized = kind != GameFolderKind.None;
                switch (kind)
                {
                    case GameFolderKind.InstallRoot:
                        candidate.Root = folder;
                        break;
                    case GameFolderKind.ArtOfConquestFolder:
                        candidate.Root = parent;
                        candidate.AocFolder = folder;
                        break;
                    default:
                        // The EE folder, as the launcher stored it before v2; also a folder without the program (damaged)
                        // and a folder that does not exist.
                        candidate.Root = parent;
                        candidate.EeFolder = folder;
                        break;
                }
                return candidate;
            }

            /// <summary>One installation from the candidates of one root (contract 1.4, "Merge", "Kind", "AoC folder").</summary>
            private Installation Build(List<Candidate> group)
            {
                // The data of the most specific source wins (2 before 3 before 4 before 5); the user choice only selects,
                // so its folders are used last.
                List<Candidate> bySource = group
                    .OrderBy(candidate => candidate.Source == InstallationSource.UserChoice ? int.MaxValue : (int)candidate.Source)
                    .ThenBy(candidate => candidate.Sequence)
                    .ToList();
                string root = bySource[0].Root;
                List<InstallInfo> infos = ReadInstallInfos(root);
                List<InstallRecord> records = bySource.Where(c => c.Record != null).Select(c => c.Record).ToList();
                List<UninstallEntry> uninstalls = bySource.Where(c => c.Uninstall != null).Select(c => c.Uninstall).ToList();

                InstallInfo info = ChooseInstallInfo(infos, root);
                Product product = info?.Product ?? records.FirstOrDefault()?.Product ?? uninstalls.FirstOrDefault()?.Product;
                InstallRecord record = records.FirstOrDefault(candidate => candidate.Product == product);
                UninstallEntry uninstall = uninstalls.FirstOrDefault(candidate => candidate.Product == product);

                InstallationKind kind;
                if (info != null ? info.File.ContractVersion >= 1 : record != null && record.ContractVersion >= 1)
                    kind = InstallationKind.Community;
                else if (uninstall != null)
                    kind = InstallationKind.CommunityLegacy;
                else
                    kind = InstallationKind.Foreign;

                string standardEeFolder = WinPath.Combine(root, Game.EmpireEarth.FolderName);
                string eeFolder = kind == InstallationKind.Foreign
                    ? bySource.Select(candidate => candidate.EeFolder).FirstOrDefault(folder => folder != null) ?? standardEeFolder
                    : standardEeFolder;
                if (kind == InstallationKind.Foreign)
                {
                    // Contract 1.4: NeoEE if the EE folder contains neoee.dll, else EE.
                    product = WinPath.IsFullyQualified(eeFolder) &&
                              FileSystem.FileExists(WinPath.Combine(eeFolder, Product.NeoEE.ProductOnlyFileName))
                        ? Product.NeoEE
                        : Product.EE;
                    record = null;
                    uninstall = null;
                }

                SetupNameList components = kind == InstallationKind.Foreign ? info?.File.Components
                    : info != null ? info.File.Components : uninstall?.Components;
                SetupNameList tasks = kind == InstallationKind.Foreign ? info?.File.Tasks
                    : info != null ? info.File.Tasks : uninstall?.Tasks;
                string aocFolder = FindAocFolder(root, kind, components, bySource);

                var installation = new Installation(product, root, eeFolder, aocFolder, kind,
                    kind == InstallationKind.Foreign ? InstallMode.Unknown : ModeOf(info, record, uninstall),
                    group.Select(candidate => candidate.Source))
                {
                    Components = components,
                    Tasks = tasks,
                    InstallInfo = info?.File,
                    RecordKey = record?.Key,
                    UninstallKey = uninstall?.Key,
                    InstalledFromKey = bySource.FirstOrDefault(candidate => candidate.InstalledFrom != null)?.InstalledFrom.Key,
                    OtherProductInRoot = infos.Select(i => i.Product)
                                              .Concat(records.Select(r => r.Product))
                                              .Concat(uninstalls.Select(u => u.Product))
                                              .FirstOrDefault(other => other != product)
                };
                if (kind != InstallationKind.Foreign)
                {
                    installation.AppId = info?.File.AppId ?? record?.AppId ?? uninstall?.AppId;
                    installation.GameVersion = info?.File.GameVersion ?? record?.GameVersion ?? uninstall?.GameVersion;
                    installation.SetupVersion = info?.File.SetupVersion ?? record?.SetupVersion ?? uninstall?.SetupVersion;
                    installation.SetupBuild = info?.File.SetupBuild ?? record?.SetupBuild;
                }
                if (kind == InstallationKind.Community)
                    installation.ContractVersion = info?.File.ContractVersion ?? record.ContractVersion;
                SetState(installation, group);
                return installation;
            }

            /// <summary>
            /// Contract 1.4, "AoC folder": <c>&lt;root&gt;\Empire Earth - The Art of Conquest</c> if <c>EE-AOC.exe</c> is
            /// there or the components contain <c>gameaoc</c>; for foreign installations also the folder of the AoC
            /// "Installed From" values (or of the user's choice) if <c>EE-AOC.exe</c> is there.
            /// </summary>
            private string FindAocFolder(string root, InstallationKind kind, SetupNameList components, List<Candidate> bySource)
            {
                string standard = WinPath.Combine(root, Game.ArtOfConquest.FolderName);
                if (GameFolders.ContainsProgram(FileSystem, standard, Game.ArtOfConquest) || components?.HasArtOfConquest == true)
                    return standard;
                if (kind != InstallationKind.Foreign)
                    return null;
                return bySource.Select(candidate => candidate.AocFolder)
                               .FirstOrDefault(folder => folder != null &&
                                                         GameFolders.ContainsProgram(FileSystem, folder, Game.ArtOfConquest));
            }

            private static InstallMode ModeOf(InstallInfo info, InstallRecord record, UninstallEntry uninstall)
            {
                if (info != null && info.File.InstallMode != InstallMode.Unknown)
                    return info.File.InstallMode;
                if (record != null)
                {
                    if (record.InstallMode != InstallMode.Unknown)
                        return record.InstallMode;
                    return RegistryReads.IsCurrentUser(record.Key) ? InstallMode.User : InstallMode.Admin;
                }
                return uninstall?.InstallMode ?? InstallMode.Unknown;
            }

            private void SetState(Installation installation, List<Candidate> group)
            {
                if (group.All(candidate => candidate.Source == InstallationSource.UserChoice) &&
                    !FileSystem.DirectoryExists(group[0].ChosenFolder))
                {
                    installation.State = InstallationState.FolderMissing;
                    return;
                }

                // A missing program means damaged, never "not found" (contract 1.4, "Validity"): an antivirus deletion must
                // lead to the repair advice.
                var missing = new List<Game>();
                if (!GameFolders.ContainsProgram(FileSystem, installation.EeFolder, Game.EmpireEarth))
                    missing.Add(Game.EmpireEarth);
                if (installation.AocFolder != null &&
                    !GameFolders.ContainsProgram(FileSystem, installation.AocFolder, Game.ArtOfConquest))
                    missing.Add(Game.ArtOfConquest);
                installation.MissingPrograms = missing;
                installation.State = missing.Count == 0 ? InstallationState.Ok : InstallationState.Damaged;
            }

            /// <summary>The readable <c>install.ini</c> files of a root, NeoEE first; each file is read once per discovery.</summary>
            private List<InstallInfo> ReadInstallInfos(string root)
            {
                if (installInfos.TryGetValue(root, out List<InstallInfo> cached))
                    return cached;

                var infos = new List<InstallInfo>();
                if (WinPath.IsFullyQualified(root))
                {
                    foreach (Product product in Product.All)
                    {
                        string path = WinPath.Combine(root, product.SetupDataFolderName + WinPath.Separator +
                                                            ContractNames.InstallInfoFileName);
                        InstallInfo info = ReadInstallInfo(product, path);
                        if (info != null)
                            infos.Add(info);
                    }
                }
                installInfos.Add(root, infos);
                return infos;
            }

            private InstallInfo ReadInstallInfo(Product product, string path)
            {
                if (!FileSystem.FileExists(path))
                    return null;
                FileSystemResult<FileEntry> entry = FileSystem.GetFileInfo(path);
                FileSystemResult<byte[]> content = entry.IsOk
                    ? FileSystem.ReadAllBytes(path, InstallInfoFile.MaxFileBytes)
                    : FileSystemResult<byte[]>.Failure(entry.Status, entry.Detail);
                if (!content.IsOk)
                {
                    Logger.Warning("Discovery: " + path + " cannot be read and is ignored: " + content + ".");
                    return null;
                }

                InstallInfoFile file = InstallInfoFile.Parse(content.Value);
                if (file.HasInvalidContractVersion)
                    Logger.Warning("Discovery: " + path + " has the invalid " + ContractNames.ContractVersionName + " \"" +
                                   file.ContractVersionText + "\", which counts as 0.");
                if (file.ProductId != null && !string.Equals(file.ProductId, product.Id, StringComparison.OrdinalIgnoreCase))
                    Logger.Warning("Discovery: " + path + " names the product \"" + file.ProductId + "\"; its folder, " +
                                   product.SetupDataFolderName + ", decides.");
                return new InstallInfo { Product = product, File = file, LastWriteTimeUtc = entry.Value.LastWriteTimeUtc };
            }

            /// <summary>
            /// The <c>install.ini</c> an installation uses: the only one, or of two products in one root the one modified
            /// last (contract 1.4, "Two products in one root"; NeoEE on a tie).
            /// </summary>
            private InstallInfo ChooseInstallInfo(List<InstallInfo> infos, string root)
            {
                if (infos.Count <= 1)
                    return infos.FirstOrDefault();
                InstallInfo chosen = infos.OrderByDescending(info => info.LastWriteTimeUtc).First();
                if (sharedRootsLogged.Add(root))
                    Logger.Warning("Discovery: EE and NeoEE are both installed in " + root + "; the launcher uses " +
                                   chosen.Product.Id + ", installed last, and the integrity check of the root is unreliable.");
                return chosen;
            }
        }
    }
}
