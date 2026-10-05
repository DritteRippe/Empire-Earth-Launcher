using System;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Play;

namespace Empire_Earth_Launcher
{
    /// <summary>
    /// What the running launcher does when a second launcher hands it a product (contract 1.4, revision 4): the window comes to
    /// the front and, while no game start is in progress, <see cref="InstallationService.SelectProductForSession"/> selects
    /// the first installation of that product for this session. Nothing is saved.
    /// </summary>
    /// <remarks>Used on the UI thread (the message window lives there). No window is touched here, so it is tested without one.</remarks>
    internal sealed class LauncherInstanceTarget : IInstanceTarget
    {
        private readonly InstallationService installations;
        private readonly Func<bool> isStarting;
        private readonly Action bringToFront;
        private readonly ILogger logger;

        /// <param name="installations">Selects the product for the session.</param>
        /// <param name="isStarting">True while a game start is in progress (<see cref="PlayModel.IsStarting"/>).</param>
        /// <param name="bringToFront">Brings the main window to the front (<see cref="ForegroundWindow.BringToFront"/>).</param>
        /// <param name="logger">Log of the launcher.</param>
        public LauncherInstanceTarget(InstallationService installations, Func<bool> isStarting, Action bringToFront,
            ILogger logger)
        {
            this.installations = installations ?? throw new ArgumentNullException(nameof(installations));
            this.isStarting = isStarting ?? throw new ArgumentNullException(nameof(isStarting));
            this.bringToFront = bringToFront ?? throw new ArgumentNullException(nameof(bringToFront));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool IsIdle
        {
            get { return !isStarting(); }
        }

        public void SelectProduct(Product product)
        {
            if (!installations.SelectProductForSession(product))
                logger.Info("The selection did not change to " + product.Id + " (no installation of that product, or the search has not finished; " +
                            "a finished search applies it).");
        }

        public void BringToFront()
        {
            bringToFront();
        }
    }
}
