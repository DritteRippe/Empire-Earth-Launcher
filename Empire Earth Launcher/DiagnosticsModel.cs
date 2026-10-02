using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Diagnostics;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher
{
    /// <summary>How saving the diagnostics report ended.</summary>
    internal enum ReportSaveOutcome
    {
        Saved,

        /// <summary>The file would be inside an installation: refused (the launcher never writes into the installation).</summary>
        InsideInstallation,

        /// <summary>Windows refused the file (access denied, invalid path, disk full).</summary>
        Failed
    }

    /// <summary>The result of <see cref="DiagnosticsModel.SaveReport"/>.</summary>
    internal sealed class ReportSaveResult
    {
        public ReportSaveResult(ReportSaveOutcome outcome, string path, string problem)
        {
            Outcome = outcome;
            Path = path;
            Problem = problem;
        }

        public ReportSaveOutcome Outcome { get; }

        public string Path { get; }

        /// <summary>Why it failed; null otherwise.</summary>
        public string Problem { get; }
    }

    /// <summary>
    /// The network diagnostics and the diagnostics report of the Tools page (L-WP9, R7, ARCHITECTURE 4.6): the check runs only
    /// when the player asks for it (the button, or the link of an unavailable player list on the Play page); the report is
    /// built from the latest results of every page when the player copies or saves it, and is never sent.
    /// </summary>
    /// <remarks>Created once by <see cref="Program"/>; use it on the UI thread. <see cref="Changed"/> is raised on the UI thread.</remarks>
    internal sealed class DiagnosticsModel
    {
        private readonly NetworkDiagnostics network;
        private readonly InstallationService installations;
        private readonly Func<DiagnosticsInput> currentState;
        private readonly ReportAnonymizer anonymizer;
        private readonly IFileSystem fileSystem;
        private readonly ILogger logger;

        /// <param name="network">The network diagnostics of the core.</param>
        /// <param name="installations">The selected installation and the AppId for the update API.</param>
        /// <param name="currentState">The latest results of the other pages for the report (<see cref="Collect"/>).</param>
        /// <param name="anonymizer">The privacy rules of the report (ADR 0013 plan review).</param>
        /// <param name="fileSystem">Writes the report into the file the player chose.</param>
        /// <param name="logger">Log of the launcher.</param>
        public DiagnosticsModel(NetworkDiagnostics network, InstallationService installations, Func<DiagnosticsInput> currentState,
            ReportAnonymizer anonymizer, IFileSystem fileSystem, ILogger logger)
        {
            this.network = network ?? throw new ArgumentNullException(nameof(network));
            this.installations = installations ?? throw new ArgumentNullException(nameof(installations));
            this.currentState = currentState ?? throw new ArgumentNullException(nameof(currentState));
            this.anonymizer = anonymizer ?? throw new ArgumentNullException(nameof(anonymizer));
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>Raised when the network check starts or ends.</summary>
        public event EventHandler Changed;

        /// <summary>True while the network check runs.</summary>
        public bool IsChecking { get; private set; }

        /// <summary>The latest network check; null before the first one.</summary>
        public NetworkReport Network { get; private set; }

        /// <summary>
        /// The AppId the update API is asked with (ADR 0008): the selected installation's, else the first installation's that
        /// has one; null if none has.
        /// </summary>
        public string AppId
        {
            get
            {
                string selected = installations.Selected?.AppId;
                if (!string.IsNullOrWhiteSpace(selected))
                    return selected;
                return installations.Result?.Installations.Select(installation => installation.AppId)
                                    .FirstOrDefault(appId => !string.IsNullOrWhiteSpace(appId));
            }
        }

        /// <summary>Runs the network check once (on request); a second request while it runs is ignored.</summary>
        public async Task CheckNetworkAsync(CancellationToken cancellationToken = default)
        {
            if (IsChecking)
                return;
            IsChecking = true;
            RaiseChanged();
            try
            {
                Network = await network.RunAsync(installations.Selected, AppId, cancellationToken);
            }
            finally
            {
                IsChecking = false;
                RaiseChanged();
            }
        }

        /// <summary>The report of the current state, with the latest network check (ARCHITECTURE 4.6).</summary>
        public string BuildReport()
        {
            DiagnosticsInput input = currentState();
            input.Network = Network;
            return DiagnosticsReport.Build(input, anonymizer);
        }

        /// <summary>
        /// Saves <paramref name="report"/> as UTF-8 with BOM into <paramref name="path"/>, the file the player chose; never into
        /// an installation (its root, game folders or anything below).
        /// </summary>
        public ReportSaveResult SaveReport(string path, string report)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (report == null)
                throw new ArgumentNullException(nameof(report));
            IEnumerable<Installation> all = installations.Result?.Installations ?? Enumerable.Empty<Installation>();
            if (all.Any(installation => new[] { installation.Root, installation.EeFolder, installation.AocFolder }
                    .Where(folder => folder != null)
                    .Any(folder => WinPath.IsSameOrBelow(path, folder))))
            {
                logger.Warning("Diagnostics report: not saved into the installation (" + anonymizer.Path(path) + ").");
                return new ReportSaveResult(ReportSaveOutcome.InsideInstallation, path, null);
            }
            byte[] bytes = new UTF8Encoding(true).GetPreamble().Concat(new UTF8Encoding(false).GetBytes(report)).ToArray();
            FileSystemResult written = fileSystem.WriteAllBytesAtomically(path, bytes);
            if (!written.IsOk)
            {
                logger.Warning("Diagnostics report: " + anonymizer.Path(path) + " could not be written (" + written.Status + ").");
                return new ReportSaveResult(ReportSaveOutcome.Failed, path, written.Detail ?? written.Status.ToString());
            }
            logger.Info("Diagnostics report: saved to " + anonymizer.Path(path) + ".");
            return new ReportSaveResult(ReportSaveOutcome.Saved, path, null);
        }

        /// <summary>Logs that the report was copied (never its text).</summary>
        public void ReportCopied(string report)
        {
            logger.Info("Diagnostics report: copied to the clipboard (" +
                        (report ?? string.Empty).Split('\n').Length.ToString(CultureInfo.InvariantCulture) + " lines).");
        }

        /// <summary>The latest results of the pages for the report: the selected installation and what the pages checked last.</summary>
        internal static DiagnosticsInput Collect(string launcherVersion, ISystemInfo systemInfo, IClock clock, bool is64BitWindows,
            InstallationService installations, PlayModel play, IntegrityModel integrity, GameSettingsModel gameSettings,
            MaintenanceModel maintenance)
        {
            MaintenanceScan scan = maintenance.Scan;
            return new DiagnosticsInput
            {
                LauncherVersion = launcherVersion,
                CreatedAt = clock.Now,
                SystemInfo = systemInfo,
                Is64BitWindows = is64BitWindows,
                UiCulture = CultureInfo.CurrentUICulture.Name,
                Discovery = installations.Result,
                ProgramVersions = play.Versions,
                Integrity = integrity.Report,
                Defaults = gameSettings.Lines?.Select(line => new KeyValuePair<Game, DefaultsStatus>(line.Game, line.Status)).ToList(),
                ConsistencyFindings = gameSettings.Findings,
                VirtualStore = scan?.VirtualStore,
                Names = scan?.Names,
                Cleanup = scan?.Cleanup
            };
        }

        private void RaiseChanged()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
