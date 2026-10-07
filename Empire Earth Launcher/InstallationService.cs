using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Core.Settings;

namespace Empire_Earth_Launcher
{
    /// <summary>
    /// The installations of Empire Earth the launcher knows and the one it works with: the result of the core's
    /// <see cref="InstallationDiscovery"/> (contract 1.4) with the folders chosen on the Launcher page as source 1 (one per
    /// product since revision 6, <see cref="ProductChoices"/>, settings.json) and the one installation every page works with, the
    /// <see cref="Selected"/> one: the installation of the product of the game the player chose last. Replaces the
    /// GameDirectoryService and GameDirectoryLocator of the launcher before v2.
    /// </summary>
    /// <remarks>
    /// Created once by <see cref="Program"/>; use it on the UI thread. The discovery runs on the thread pool
    /// (<see cref="InstallationDiscovery.DiscoverAsync"/>), so the window never waits for the registry or a slow drive
    /// (ADR 0004); <see cref="Changed"/> is raised on the thread that started the refresh, the UI thread. Pages start a
    /// refresh through <see cref="UiOperation"/>.
    /// <para>
    /// While a setup runs, no discovery starts (contract 4.2: <c>install.ini</c> is not read while a setup mutex exists):
    /// the refresh waits (<see cref="IsWaitingForSetup"/>), the previous result stays, and when the
    /// <see cref="SetupWatcher"/> sees the setup end, the installations are searched again (L-WP6).
    /// </para>
    /// </remarks>
    internal sealed class InstallationService
    {
        private readonly ILogger logger;
        private readonly SettingsStore settings;
        private readonly InstallationDiscovery discovery;
        private readonly IFileSystem fileSystem;
        private readonly string launcherFolder;
        private readonly SetupWatcher setupWatcher;

        /// <summary>Counts the refreshes, so that only the result of the latest one is used.</summary>
        private int generation;

        private int running;

        /// <summary>The product of <c>--product=</c> for this session (contract 1.4); null if none. Never saved.</summary>
        private Product sessionProduct;

        /// <summary>
        /// What the latest discovery found, before the product of the selection was applied: every selection starts from it, so
        /// that switching the product back and forth never loses a choice.
        /// </summary>
        private DiscoveryResult discovered;

        /// <summary>True if the latest discovery made a folder chosen without a product the choice of its product (rule 3).</summary>
        private bool migratedChoice;

        /// <summary>True while <see cref="RefreshAsync"/> probes the setup mutexes, so that the end it sees does not start a second refresh.</summary>
        private bool probingSetup;

        /// <param name="logger">Log of the launcher.</param>
        /// <param name="settings">User settings; the chosen folder is saved there.</param>
        /// <param name="discovery">The discovery of the core.</param>
        /// <param name="fileSystem">The file system, to judge a folder the user picks.</param>
        /// <param name="launcherFolder">The folder of the launcher (source 5); null to skip it.</param>
        /// <param name="setupWatcher">Watches the setup mutexes (contract 4.2); null to search regardless of a setup.</param>
        public InstallationService(ILogger logger, SettingsStore settings, InstallationDiscovery discovery,
            IFileSystem fileSystem, string launcherFolder, SetupWatcher setupWatcher = null)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            this.discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            this.launcherFolder = launcherFolder;
            this.setupWatcher = setupWatcher;
            if (setupWatcher != null)
                setupWatcher.SetupFinished += OnSetupFinished;
        }

        /// <summary>Raised when a refresh starts or ends, and when <see cref="Result"/> changed.</summary>
        public event EventHandler Changed;

        /// <summary>True while a discovery runs.</summary>
        public bool IsSearching
        {
            get { return running > 0; }
        }

        /// <summary>
        /// True while a refresh waits for the end of a running setup (contract 4.2); the pages say so, and the search starts
        /// by itself when the setup has ended.
        /// </summary>
        public bool IsWaitingForSetup { get; private set; }

        /// <summary>The refresh started by the end of a setup (<see cref="SetupWatcher.SetupFinished"/>); null before the first.</summary>
        public Task RefreshAfterSetup { get; private set; }

        /// <summary>
        /// The result of the latest discovery with the selection of every page (<see cref="ApplySelection"/>); null until the
        /// first one has finished. A new object only when the selection changed.
        /// </summary>
        public DiscoveryResult Result { get; private set; }

        /// <summary>The installation every page works with; null if none was found (or none is known yet).</summary>
        public Installation Selected
        {
            get { return Result?.Selected; }
        }

        /// <summary>
        /// The product the session was started or switched to with <c>--product=</c> (contract 1.4, revision 4); null if none, or
        /// after the user chose an installation (the user's choice wins over the argument).
        /// </summary>
        public Product SessionProduct
        {
            get { return sessionProduct; }
        }

        /// <summary>
        /// Selects the installation of <paramref name="product"/> for this session only (contract 1.4, "Default selection"): the
        /// folder chosen for it, else its first installation. The saved choices and the settings stay as they are, and every
        /// later search applies the same selection. Without an installation of that product the argument is ignored (logged) and
        /// the normal selection stays. Before the first search has finished it only remembers the product, which the search then
        /// applies.
        /// </summary>
        /// <returns>True if the selected installation is of that product afterwards.</returns>
        public bool SelectProductForSession(Product product)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));
            if (discovered != null && !discovered.Has(product))
            {
                // Contract 1.4: without an installation of the product the argument is ignored; the earlier one stays.
                logger.Warning("No " + product.Id + " installation was found; " + ContractNames.ProductArgumentName + "=" +
                               product.Id + " is ignored and the selection stays.");
                return false;
            }
            sessionProduct = product;
            if (discovered == null)
                return false;
            Apply(ApplySelection(discovered));
            return Selected?.Product == product;
        }

        /// <summary>
        /// The player chose a game of <paramref name="product"/> on the Play page: its installation becomes the selected one of
        /// every page, and the product is saved as the product chosen last (<see cref="ProductChoices.ChooseProduct"/>; the
        /// choice of the player ends the session product). No discovery runs: the installations are known. The
        /// <see cref="Changed"/> event is raised if the selection changed.
        /// </summary>
        /// <returns>False, and nothing changes, if no installation of that product is known (or none was searched yet).</returns>
        public bool SelectProduct(Product product)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));
            if (discovered == null || !discovered.Has(product))
                return false;
            EndSessionProduct();
            ProductChoices.ChooseProduct(settings.Current, product);
            // If saving fails (logged by the store), the choice is still used for this session.
            settings.Save();
            DiscoveryResult applied = ApplySelection(discovered);
            Apply(applied);
            return true;
        }

        /// <summary>
        /// Runs the discovery again with the folder chosen in the settings; while a setup runs, it only marks the refresh as
        /// waiting (<see cref="IsWaitingForSetup"/>) and keeps the previous result.
        /// </summary>
        public async Task RefreshAsync(CancellationToken cancellationToken = default)
        {
            int current = ++generation;
            if (setupWatcher != null)
            {
                SetupKind setup;
                probingSetup = true;
                try
                {
                    setup = setupWatcher.ProbeNow();
                }
                finally
                {
                    probingSetup = false;
                }
                if (setup != null)
                {
                    if (!IsWaitingForSetup)
                        logger.Info("The installations are searched when the " + setup.Id +
                                    " setup has ended (install.ini is not read while a setup runs, contract 4.2).");
                    IsWaitingForSetup = true;
                    Changed?.Invoke(this, EventArgs.Empty);
                    return;
                }
            }
            IsWaitingForSetup = false;
            running++;
            Changed?.Invoke(this, EventArgs.Empty);
            try
            {
                DiscoveryResult result = await discovery.DiscoverChoicesAsync(ProductChoices.UserChoices(settings.Current),
                    launcherFolder, cancellationToken);
                if (current != generation)
                    return; // a later refresh (another choice) has started; its result counts
                discovered = result;
                Result = ApplySelection(result);
                LogSelection(Result);
            }
            finally
            {
                running--;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>The setup has ended: the installations are searched again (ARCHITECTURE 4.3).</summary>
        private void OnSetupFinished(object sender, SetupStateEventArgs e)
        {
            if (probingSetup)
                return; // RefreshAsync saw the end itself and goes on with the discovery
            Task refresh = RefreshAsync();
            RefreshAfterSetup = refresh;
            // The discovery returns environment problems as results (ADR 0013); a fault here is a programming error, which
            // is logged like an unobserved task exception.
            refresh.ContinueWith(task => logger.Error("The search for the installations after the setup failed.", task.Exception),
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }

        /// <summary>
        /// Saves <paramref name="folder"/> (an install root, EE folder or AoC folder) as the user's choice, source 1 of the
        /// discovery, and runs the discovery. The folder is saved without a product; once the discovery has found out which
        /// installation it belongs to, it is the folder chosen for that product and that product is the one chosen last
        /// (contract 1.4 revision 6, rule 3 of ADR 0005), which is saved a second time, the only double save.
        /// </summary>
        public async Task ChooseFolderAsync(string folder)
        {
            EndSessionProduct();
            settings.Current.GameDirectory = string.IsNullOrWhiteSpace(folder) ? string.Empty : folder.Trim();
            // If saving fails (logged by the store), the choice is still used for this session.
            settings.Save();
            migratedChoice = false;
            await RefreshAsync().ConfigureAwait(true);
            if (migratedChoice)
                settings.Save();
        }

        /// <summary>
        /// Chooses an installation of the list: its EE folder is saved as the folder chosen for its product, which is the
        /// product chosen last, so every page and the Play page follow (contract 1.4 revision 6).
        /// </summary>
        /// <remarks>
        /// The EE folder, not the root: for a foreign installation with another folder name (<c>C:\Games\EE</c>) only the
        /// EE folder says where the game is, also when its "Installed From" values change later.
        /// </remarks>
        public Task SelectAsync(Installation installation)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            EndSessionProduct();
            ProductChoices.Choose(settings.Current, installation.Product, installation.EeFolder);
            // If saving fails (logged by the store), the choice is still used for this session.
            settings.Save();
            return RefreshAsync();
        }

        /// <summary>
        /// Removes the folder chosen for the current product: its first installation found is used. The product stays, and so do
        /// the folders chosen for the other product.
        /// </summary>
        public Task UseAutomaticDetectionAsync()
        {
            EndSessionProduct();
            Product current = Selected?.Product ?? ProductChoices.LastProduct(settings.Current);
            if (current != null)
                ProductChoices.ClearFolder(settings.Current, current);
            else
                settings.Current.GameDirectory = string.Empty;
            settings.Save();
            return RefreshAsync();
        }

        /// <summary>What a folder the user picked is (EE folder, AoC folder, install root or none of them).</summary>
        public GameFolderKind ClassifyFolder(string folder)
        {
            return GameFolders.Classify(fileSystem, folder);
        }

        /// <summary>The choice of the player ends <c>--product=</c> for this session (the choice wins over the argument).</summary>
        private void EndSessionProduct()
        {
            if (sessionProduct != null)
                logger.Info("The choice of the user replaces the product of the command line (" + sessionProduct.Id + ").");
            sessionProduct = null;
        }

        /// <summary>Takes <paramref name="applied"/> as the result if it selects something else than the current one, and tells the pages.</summary>
        private void Apply(DiscoveryResult applied)
        {
            if (Result != null && applied.Selected == Result.Selected && applied.IsSelectedByUser == Result.IsSelectedByUser)
                return;
            Result = applied;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// The result with the installation every page works with (contract 1.4, "Default selection", revision 6): the product of
        /// <c>--product=</c> for this session if there is an installation of it, else the product chosen last, else the product of
        /// the installation the default selection prefers; and of that product the folder chosen for it, else its first installation.
        /// </summary>
        /// <remarks>
        /// A <see cref="LauncherSettings.GameDirectory"/> that no product was chosen for (a file of launcher 1.0.0, or the "Browse"
        /// button) becomes, in memory, the folder chosen for the product of its installation, which is then the product chosen
        /// last (ADR 0005 amendment, rule 3); it is written with the next save.
        /// </remarks>
        private DiscoveryResult ApplySelection(DiscoveryResult result)
        {
            LauncherSettings current = settings.Current;
            if (result.Selected != null && ProductChoices.HasChoiceOfOlderLauncher(current))
            {
                Product product = result.Selected.Product;
                logger.Info("settings.json: the folder chosen by an older launcher, " + current.GameDirectory + ", is the choice for " +
                            product.Id + ".");
                ProductChoices.Choose(current, product, current.GameDirectory);
                migratedChoice = true;
            }

            Product last = ProductChoices.LastProduct(current);
            Product chosen;
            string reason;
            if (sessionProduct != null && result.Has(sessionProduct))
            {
                chosen = sessionProduct;
                reason = ContractNames.ProductArgumentName + "=" + sessionProduct.Id;
            }
            else
            {
                if (sessionProduct != null)
                    logger.Warning("No " + sessionProduct.Id + " installation was found; " + ContractNames.ProductArgumentName + "=" +
                                   sessionProduct.Id + " is ignored and the usual selection applies.");
                chosen = last != null && result.Has(last) ? last : result.Selected?.Product;
                reason = chosen == last ? "the product chosen last" : "the default selection";
            }
            if (chosen == null)
                return result;

            DiscoveryResult applied = result.ForProduct(chosen);
            if (sessionProduct != null && chosen == sessionProduct && applied != result)
                logger.Info("Selected the " + sessionProduct.Id + " installation " + applied.Selected.Root + " for this session (" +
                            ContractNames.ProductArgumentName + "=" + sessionProduct.Id + "; nothing is saved).");
            string how = applied.IsSelectedByUser ? "the folder chosen for it" : "the first one found";
            logger.Info("Selection: " + chosen.Id + " installation " + applied.Selected.Root + " for every page (" + reason + ", " +
                        how + ").");
            return applied;
        }

        private void LogSelection(DiscoveryResult result)
        {
            if (result.Selected == null)
                logger.Warning("No Empire Earth installation found. Choose the game folder in the launcher settings.");
            else
                logger.Info("Empire Earth folder: " + result.Selected.EeFolder + " (" +
                            (result.IsSelectedByUser ? "chosen by the user" : "source " + (int)result.Selected.Origin) + ").");
        }
    }
}
