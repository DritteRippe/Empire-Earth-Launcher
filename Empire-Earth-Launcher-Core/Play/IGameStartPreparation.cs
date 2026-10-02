using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;

namespace Empire_Earth_Launcher.Core.Play
{
    /// <summary>
    /// The game settings work before a game starts (contract 3.6), implemented by <see cref="GameDefaultsService"/>:
    /// class S synchronized for the game started, then the first run if the defaults marker is missing. Both ask the
    /// mutation guard themselves.
    /// </summary>
    public interface IGameStartPreparation
    {
        /// <summary>Class S of <paramref name="game"/> from its real folder, written only if it differs (contract 3.6).</summary>
        GameSettingsResult SynchronizeInstalledFrom(Installation installation, Game game);

        /// <summary>The first run of <paramref name="game"/> if its marker is missing or lower (contract 3.5, 3.6).</summary>
        DefaultsAtStart ApplyDefaultsIfNeeded(Installation installation, Game game, out DisplayQuestion question);
    }
}
