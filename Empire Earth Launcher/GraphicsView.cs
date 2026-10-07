using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Properties;

namespace Empire_Earth_Launcher
{
    /// <summary>
    /// What the Graphics page shows for the state of its <see cref="GraphicsModel"/> in the UI language: the texts, which
    /// controls are shown and enabled, and the sizes of the list with the one that is selected. The page only assigns it
    /// (<see cref="GraphicsUserControl"/>), so that everything that decides is tested without a window (the list of the page is
    /// a Krypton combo box, which Mono cannot create).
    /// </summary>
    internal sealed class GraphicsView
    {
        private GraphicsView()
        {
        }

        /// <summary>The installation the page is about, or that the search or a setup is under way.</summary>
        public string Installation { get; private set; }

        /// <summary>The window size of each game, "checking" while the state is read; empty without an installation.</summary>
        public string CurrentSizes { get; private set; }

        /// <summary>True if the label, the list and the button of the size are shown (the state of the installation is read).</summary>
        public bool ShowChooser { get; private set; }

        /// <summary>The sizes of the list, in its order.</summary>
        public IReadOnlyList<ResolutionOption> Options { get; private set; }

        /// <summary>The texts of <see cref="Options"/>.</summary>
        public IReadOnlyList<string> OptionTexts { get; private set; }

        /// <summary>The index of the size that is selected, or -1: the choice of the player, else the size the first game has now.</summary>
        public int SelectedIndex { get; private set; }

        public bool ChooserEnabled { get; private set; }

        /// <summary>True if a size is selected that is not the window size of every game yet, and a change is possible.</summary>
        public bool ApplyEnabled { get; private set; }

        /// <summary>The line of a running setup, else the result of the last change of the size; empty if there is none.</summary>
        public string Result { get; private set; }

        /// <summary>The note about the scaling of the screen; empty on an unscaled screen.</summary>
        public string Scaling { get; private set; }

        /// <summary>"Installed: ..." (empty until the state is read).</summary>
        public string WrapperInstalled { get; private set; }

        /// <summary>The <c>dgVoodoo.conf</c> lines of the games; empty if there is nothing to show.</summary>
        public string WrapperConfig { get; private set; }

        /// <summary>The view of <paramref name="model"/>; <paramref name="chosen"/> is the size the player chose in the list, if any.</summary>
        public static GraphicsView Of(GraphicsModel model, SetupWatcher setupWatcher, ScreenSize chosen)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            if (setupWatcher == null)
                throw new ArgumentNullException(nameof(setupWatcher));
            Installation selected = model.Selected;
            GraphicsSnapshot snapshot = model.Snapshot;
            bool ready = selected != null && snapshot != null && snapshot.Installation == selected;

            var view = new GraphicsView
            {
                Installation = !model.HasResult
                    ? model.IsWaitingForSetup ? Resources.InstallationsWaitingForSetup : Resources.InstallationsSearching
                    : Texts.GameSettingsInstallation(selected),
                CurrentSizes = ready
                    ? string.Join(Environment.NewLine, snapshot.Windows.Select(line => Texts.WindowSize(line.Game, line.Size)))
                    : selected != null ? Resources.ToolsChecking : string.Empty,
                ShowChooser = ready,
                Options = ready ? snapshot.Options : new ResolutionOption[0],
                SelectedIndex = -1,
            };
            view.OptionTexts = view.Options.Select(Texts.ResolutionChoice).ToList();
            if (ready)
            {
                ScreenSize wanted = chosen.IsEmpty ? snapshot.Windows.FirstOrDefault()?.Size ?? ScreenSize.Empty : chosen;
                view.SelectedIndex = view.Options.ToList().FindIndex(option => option.Size == wanted);
            }
            view.ChooserEnabled = ready && model.CanChange;
            view.ApplyEnabled = ready && model.CanChange && view.SelectedIndex >= 0 &&
                                snapshot.Changes(view.Options[view.SelectedIndex].Size);

            string setup = Texts.SetupRunning(setupWatcher.RunningSetup);
            view.Result = setup ?? (model.LastResult == null ? string.Empty : Texts.WindowSizeResult(model.LastResult, model.LastSize));
            // The scaling of the screen (ADR 0011): the game sees a smaller screen than the list is made for.
            view.Scaling = ready && snapshot.ScalingPercent > 100
                ? string.Format(CultureInfo.CurrentCulture, Resources.GraphicsWindowSizeScalingFormat, snapshot.ScalingPercent)
                : string.Empty;
            view.WrapperInstalled = ready ? Texts.WrapperInstalled(snapshot.Wrapper) : string.Empty;
            view.WrapperConfig = ready ? Texts.WrapperConfigs(snapshot.Configs) : string.Empty;
            return view;
        }
    }
}
