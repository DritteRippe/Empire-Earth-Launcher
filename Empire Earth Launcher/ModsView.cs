using System;
using System.Collections.Generic;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Diagnostics;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Mods;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Properties;

namespace Empire_Earth_Launcher
{
    /// <summary>What the Mods page shows for one game: its heading, the choice of <c>dreXmod.config</c>, the presets and the buttons.</summary>
    internal sealed class ModsGameView
    {
        public ModsGameView(Game game, string heading, string selection, string presets, bool openFolderEnabled, bool openConfigEnabled)
        {
            Game = game;
            Heading = heading;
            Selection = selection;
            Presets = presets;
            OpenFolderEnabled = openFolderEnabled;
            OpenConfigEnabled = openConfigEnabled;
        }

        public Game Game { get; }

        /// <summary>The name of the game.</summary>
        public string Heading { get; }

        /// <summary>The active mod and the active lobby theme of <c>dreXmod.config</c>, or why the file tells nothing.</summary>
        public string Selection { get; }

        /// <summary>The presets of <c>Data\dxm\mods</c>, one block per preset, or why there are none.</summary>
        public string Presets { get; }

        /// <summary>True if the folder of the presets exists and may be opened now.</summary>
        public bool OpenFolderEnabled { get; }

        /// <summary>True if <c>dreXmod.config</c> exists and may be opened now.</summary>
        public bool OpenConfigEnabled { get; }
    }

    /// <summary>
    /// What the Mods page shows for the state of its <see cref="ModsModel"/> in the UI language: the texts, which controls are
    /// shown and enabled. The page only assigns it (<see cref="ModsUserControl"/>), so that everything that decides is tested
    /// without a window.
    /// </summary>
    internal sealed class ModsView
    {
        private ModsView()
        {
        }

        /// <summary>The installation the page is about, or that the search or a setup is under way.</summary>
        public string Installation { get; private set; }

        /// <summary>The line of a running setup, or "checking" while the state is read; empty otherwise.</summary>
        public string Status { get; private set; }

        /// <summary>True if the blocks of the games are shown (the state of an installation with dreXmod 3 is read).</summary>
        public bool ShowGames { get; private set; }

        /// <summary>The games to show, in the order of the installation (Empire Earth, then The Art of Conquest); at most two.</summary>
        public IReadOnlyList<ModsGameView> Games { get; private set; }

        /// <summary>True if the check box of the template folder is shown: only if a game folder has one.</summary>
        public bool ShowTemplateSwitch { get; private set; }

        /// <summary>The state of the check box of the template folder.</summary>
        public bool TemplatesShown { get; private set; }

        public static ModsView Of(ModsModel model, SetupWatcher setupWatcher)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            if (setupWatcher == null)
                throw new ArgumentNullException(nameof(setupWatcher));
            Installation selected = model.Selected;
            ModsSnapshot snapshot = model.Snapshot;
            bool ready = model.IsAvailable;

            string setup = Texts.ModsSetupRunning(setupWatcher.RunningSetup);
            var view = new ModsView
            {
                Installation = !model.HasResult
                    ? model.IsWaitingForSetup ? Resources.InstallationsWaitingForSetup : Resources.InstallationsSearching
                    : Texts.GameSettingsInstallation(selected),
                Status = setup ?? (selected != null && !ready ? Resources.ToolsChecking : string.Empty),
                ShowGames = ready,
                TemplatesShown = model.ShowTemplates,
            };
            var games = new List<ModsGameView>();
            if (ready)
            {
                foreach (GameModsLine line in snapshot.Games.Take(2))
                {
                    games.Add(new ModsGameView(line.Game, Texts.GameName(line.Game), Texts.ModsSelection(line),
                        Texts.ModsPresets(line, model.ShowTemplates),
                        model.CanOpen && line.Scan.Status != ConfigFileStatus.Missing,
                        model.CanOpen && line.Config.Status != ConfigFileStatus.Missing));
                }
            }
            view.Games = games;
            view.ShowTemplateSwitch = ready && snapshot.Games.Any(line => line.Scan.Presets.Any(preset => preset.IsTemplate));
            return view;
        }
    }
}
