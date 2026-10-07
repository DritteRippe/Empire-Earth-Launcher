using System;
using System.Collections.Generic;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Integrity;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.RealMachineTests.Expectations
{
    /// <summary>
    /// What one step of a CI scenario expects of the launcher core on the runner (<see cref="ExpectationFile"/>, schema 1):
    /// the installations the discovery finds, their integrity, the state of their game settings, and whether the defaults of
    /// the launcher start are applied in this step. Only what is given is checked; nothing here is a hash or game data.
    /// </summary>
    internal sealed class Expectation
    {
        /// <summary>The only schema this harness reads.</summary>
        public const int SchemaVersion = 1;

        /// <summary>Free text for the log, e.g. <c>A</c>.</summary>
        public string Scenario { get; set; }

        /// <summary>Free text for the log, e.g. <c>installed</c>.</summary>
        public string Step { get; set; }

        /// <summary>The folder chosen in the launcher settings (source 1 of contract 1.4); null for automatic detection.</summary>
        public string UserChoice { get; set; }

        /// <summary>True (the default): the discovery finds the expected installations and no other.</summary>
        public bool ExactInstallations { get; set; } = true;

        /// <summary>The root of the installation the discovery selects; null if not checked.</summary>
        public string SelectedRoot { get; set; }

        /// <summary>
        /// Folders whose files the harness must leave as they are, in addition to the roots of the installations (e.g. the root
        /// of an installation that was just uninstalled).
        /// </summary>
        public IReadOnlyList<string> WatchRoots { get; set; } = new string[0];

        public IReadOnlyList<InstallationExpectation> Installations { get; set; } = new InstallationExpectation[0];

        /// <summary>The defaults of the launcher start are applied in this step (it writes HKCU); null: they are not.</summary>
        public DefaultsExpectation Defaults { get; set; }

        /// <summary>
        /// Every folder the harness watches (<see cref="Harness.MachineSnapshot"/>): the roots of the installations, the selected
        /// root and <see cref="WatchRoots"/>, each once.
        /// </summary>
        public IReadOnlyList<string> Roots
        {
            get
            {
                return Installations.Select(installation => installation.Root)
                                    .Concat(SelectedRoot == null ? new string[0] : new[] { SelectedRoot })
                                    .Concat(WatchRoots)
                                    .Distinct(WinPath.Comparer)
                                    .ToList();
            }
        }

        /// <summary>The expected installation in <paramref name="root"/>, or null.</summary>
        public InstallationExpectation ForRoot(string root)
        {
            return Installations.FirstOrDefault(installation => WinPath.IsSamePath(installation.Root, root));
        }
    }

    /// <summary>One installation the discovery must find (contract 1.4), and what is checked about it.</summary>
    internal sealed class InstallationExpectation
    {
        public Product Product { get; set; }

        /// <summary>The install root; the installation is found by it.</summary>
        public string Root { get; set; }

        public InstallationKind? Kind { get; set; }

        public InstallMode? Mode { get; set; }

        /// <summary>The AppId without braces, compared ignoring case.</summary>
        public string AppId { get; set; }

        public int? ContractVersion { get; set; }

        public bool? HasArtOfConquest { get; set; }

        public InstallationState? State { get; set; }

        /// <summary>The games whose program is missing; null if not checked.</summary>
        public IReadOnlyList<Game> MissingPrograms { get; set; }

        /// <summary>Exactly these sources; null if not checked.</summary>
        public IReadOnlyList<InstallationSource> Sources { get; set; }

        /// <summary>At least these sources; null if not checked.</summary>
        public IReadOnlyList<InstallationSource> SourcesInclude { get; set; }

        /// <summary>True if <see cref="OtherProductInRoot"/> is checked (the member is given, also as null).</summary>
        public bool ChecksOtherProductInRoot { get; set; }

        public Product OtherProductInRoot { get; set; }

        public string GameVersion { get; set; }

        public string SetupVersion { get; set; }

        /// <summary>The quick check (contract 2.5); null if not run in this step.</summary>
        public CheckExpectation Quick { get; set; }

        /// <summary>The full check (contract 2.5); null if not run in this step.</summary>
        public CheckExpectation Full { get; set; }

        /// <summary><c>GameDefaultsService.GetStatus</c> per game before anything is applied; null if not checked.</summary>
        public PerGame<DefaultsStatus> DefaultsStatus { get; set; }

        /// <summary>The consistency findings of the game settings (contract 3.3, 3.7); null if not checked.</summary>
        public ConsistencyExpectation Consistency { get; set; }

        /// <summary>What the defaults at the launcher start do per game (only with <see cref="Expectation.Defaults"/>).</summary>
        public PerGame<DefaultsAtStart> DefaultsAtStart { get; set; }

        /// <summary>What happens to class S at the launcher start per game (only with <see cref="Expectation.Defaults"/>).</summary>
        public PerGame<InstalledFromAtStart> InstalledFromAtStart { get; set; }

        public override string ToString()
        {
            return Product.Id + " in " + Root;
        }
    }

    /// <summary>The expected result of one integrity check (contract 2.5).</summary>
    internal sealed class CheckExpectation
    {
        public IntegrityState State { get; set; }

        /// <summary>Checked if given.</summary>
        public UnknownReason? UnknownReason { get; set; }

        /// <summary>Checked if given.</summary>
        public CancelReason? CancelReason { get; set; }

        /// <summary>Exactly these findings (path and kind, the path compared ignoring case); null if not checked.</summary>
        public IReadOnlyList<FindingExpectation> Findings { get; set; }

        /// <summary>Checked if given.</summary>
        public bool? OffersRepair { get; set; }
    }

    /// <summary>A finding of an integrity check: the manifest path and what is wrong, never a hash.</summary>
    internal sealed class FindingExpectation
    {
        public FindingExpectation(string path, FindingKind kind)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
            Kind = kind;
        }

        /// <summary>The path as the manifest writes it, with <c>/</c>.</summary>
        public string Path { get; }

        public FindingKind Kind { get; }

        public override string ToString()
        {
            return Path + "|" + Kind;
        }
    }

    /// <summary>The consistency codes that must and may appear (contract 3.3, 3.7; the screen of the runner is not known).</summary>
    internal sealed class ConsistencyExpectation
    {
        public IReadOnlyList<FindingCode> Expected { get; set; } = new FindingCode[0];

        public IReadOnlyList<FindingCode> Allowed { get; set; } = new FindingCode[0];
    }

    /// <summary>The defaults of the launcher start in this step (contract 3.5, 3.6; ADR 0015).</summary>
    internal sealed class DefaultsExpectation
    {
        /// <summary>Before the start: the game settings the setup wrote are saved to this file below the work folder.</summary>
        public string RecordSetupValuesTo { get; set; }

        /// <summary>The start writes nothing (an installation whose setup wrote the marker for this account).</summary>
        public bool ExpectNoWrites { get; set; }

        /// <summary>After the start every value of the table, the GPU preference and the marker are the recommended ones.</summary>
        public bool ExpectRecommendedValues { get; set; }

        /// <summary>After the start the values equal those saved by an earlier step (<see cref="RecordSetupValuesTo"/>).</summary>
        public string CompareWithSetupValuesFrom { get; set; }

        /// <summary>Setting names (or <c>Marker</c>, <c>GpuPreference</c>) that may differ from the saved values.</summary>
        public IReadOnlyList<string> AllowedDifferences { get; set; } = new string[0];

        /// <summary>True (the default): a second start changes nothing.</summary>
        public bool SecondStartChangesNothing { get; set; } = true;

        /// <summary>After the starts, the reset of every expected installation (contract 3.6, R4) with its backup.</summary>
        public bool Reset { get; set; }
    }

    /// <summary>A value per game; a game without a value is not checked.</summary>
    internal sealed class PerGame<T>
        where T : struct
    {
        private readonly Dictionary<Game, T> values = new Dictionary<Game, T>();

        public IReadOnlyList<Game> Games
        {
            get { return Game.All.Where(values.ContainsKey).ToList(); }
        }

        public T? this[Game game]
        {
            get { return values.TryGetValue(game, out T value) ? value : (T?)null; }
        }

        public void Set(Game game, T value)
        {
            values[game] = value;
        }
    }
}
