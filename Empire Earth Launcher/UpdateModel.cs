using System;
using System.Threading;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Repair;

namespace Empire_Earth_Launcher
{
    /// <summary>
    /// The update API for the Play and Tools pages and the repair advice (contract 4.3, 4.5, ADR 0008, L-WP7): where the
    /// setup is downloaded, opening that page in the browser, and the version check of the selected installation.
    /// </summary>
    /// <remarks>
    /// Created once by <see cref="Program"/>; use it on the UI thread. Every request runs only when the player asks for it
    /// (the repair advice is shown, a version check is clicked), never at start (ADR 0008). The game version check is
    /// always asked; the setup version check only on the Tools page. The results belong to the installation they were
    /// asked for and are dropped when another one is selected.
    /// </remarks>
    internal sealed class UpdateModel
    {
        private readonly SetupDownloadLocator locator;
        private readonly UpdateChecker checker;
        private readonly InstallationService installations;
        private readonly IProcessStarter shell;
        private readonly ILogger logger;

        /// <param name="locator">Asks the update API for the setup download (contract 4.3).</param>
        /// <param name="checker">Asks the update API for the versions (contract 4.5).</param>
        /// <param name="installations">The selected installation.</param>
        /// <param name="shell">Opens the download page in the default browser, not elevated (contract 4.3 step 4).</param>
        /// <param name="logger">Log of the launcher.</param>
        public UpdateModel(SetupDownloadLocator locator, UpdateChecker checker, InstallationService installations,
            IProcessStarter shell, ILogger logger)
        {
            this.locator = locator ?? throw new ArgumentNullException(nameof(locator));
            this.checker = checker ?? throw new ArgumentNullException(nameof(checker));
            this.installations = installations ?? throw new ArgumentNullException(nameof(installations));
            this.shell = shell ?? throw new ArgumentNullException(nameof(shell));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            installations.Changed += (sender, e) => OnInstallationsChanged();
        }

        /// <summary>Raised when a version check starts or ends, or its results were dropped.</summary>
        public event EventHandler Changed;

        /// <summary>True while a version check runs.</summary>
        public bool IsChecking { get; private set; }

        /// <summary>The installation of <see cref="GameResult"/> and <see cref="SetupResult"/>; null before the first check.</summary>
        public Installation CheckedInstallation { get; private set; }

        /// <summary>The result of the game version check of the selected installation; null if it was not asked yet.</summary>
        public VersionCheckResult GameResult { get; private set; }

        /// <summary>The result of the setup version check (Tools page only); null if it was not asked.</summary>
        public VersionCheckResult SetupResult { get; private set; }

        /// <summary>True if a version check can start: an installation is selected and no check runs.</summary>
        public bool CanCheck
        {
            get { return installations.Selected != null && !IsChecking; }
        }

        /// <summary>
        /// The hand-off of an available update (contract 4.5: "An available update uses the hand-off of 4.3"): for the game
        /// version first, else for the setup version; null if neither is outdated.
        /// </summary>
        public RepairAdvice UpdateAdvice
        {
            get
            {
                if (GameResult?.Outcome == VersionCheckOutcome.UpdateAvailable)
                    return RepairAdvice.ForUpdate(GameResult);
                if (SetupResult?.Outcome == VersionCheckOutcome.UpdateAvailable)
                    return RepairAdvice.ForUpdate(SetupResult);
                return null;
            }
        }

        /// <summary>
        /// Asks the update API about the game version of the selected installation (contract 4.5, not optional) and, with
        /// <paramref name="includeSetup"/>, about its setup version.
        /// </summary>
        /// <returns>The result of the game version check.</returns>
        public async Task<VersionCheckResult> CheckAsync(bool includeSetup, CancellationToken cancellationToken = default)
        {
            Installation installation = installations.Selected ??
                                        throw new InvalidOperationException("No installation is selected.");
            if (IsChecking)
                throw new InvalidOperationException("A version check is running.");
            IsChecking = true;
            CheckedInstallation = installation;
            GameResult = null;
            SetupResult = null;
            RaiseChanged();
            try
            {
                VersionCheckResult game = await checker.CheckAsync(installation, VersionKind.Game, cancellationToken);
                VersionCheckResult setup = includeSetup
                    ? await checker.CheckAsync(installation, VersionKind.Setup, cancellationToken)
                    : null;
                GameResult = game;
                SetupResult = setup;
                return game;
            }
            finally
            {
                IsChecking = false;
                // Another installation may have been selected meanwhile: then the results are dropped.
                OnInstallationsChanged();
                RaiseChanged();
            }
        }

        /// <summary>
        /// Asks the update API where the current setup of the advice's installation is downloaded (contract 4.3 steps 1 to
        /// 3): the answer if <see cref="UpdateUrlPolicy"/> allows it, else the fixed page with the reason; never throws for
        /// network problems.
        /// </summary>
        public async Task<RepairAdvice> LocateAsync(RepairAdvice advice, CancellationToken cancellationToken = default)
        {
            if (advice == null)
                throw new ArgumentNullException(nameof(advice));
            SetupDownloadLocation location = await locator.LocateAsync(advice.Installation.AppId, cancellationToken);
            return advice.WithLocation(location);
        }

        /// <summary>Opens the download page of the advice in the default browser (contract 4.3 step 4).</summary>
        public DownloadPageResult OpenDownloadPage(RepairAdvice advice)
        {
            if (advice == null)
                throw new ArgumentNullException(nameof(advice));
            return advice.OpenDownloadPage(shell, logger);
        }

        /// <summary>The results belong to one installation: another selection drops them.</summary>
        private void OnInstallationsChanged()
        {
            Installation selected = installations.Selected;
            if (CheckedInstallation == null || IsChecking)
                return;
            if (selected != null && WinPath.IsSamePath(selected.Root, CheckedInstallation.Root) &&
                selected.Product == CheckedInstallation.Product)
                return;
            CheckedInstallation = null;
            GameResult = null;
            SetupResult = null;
            RaiseChanged();
        }

        private void RaiseChanged()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
