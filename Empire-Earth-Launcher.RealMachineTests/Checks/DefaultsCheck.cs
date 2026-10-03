using System;
using System.Collections.Generic;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.RealMachineTests.Expectations;
using Empire_Earth_Launcher.RealMachineTests.Harness;

namespace Empire_Earth_Launcher.RealMachineTests.Checks
{
    /// <summary>
    /// The defaults of the launcher start and the reset on the real computer (contract 3.4 to 3.6, ADR 0015): what they did per
    /// game, which values they wrote, and that the values are the recommended ones afterwards.
    /// </summary>
    internal static class DefaultsCheck
    {
        /// <summary>What <see cref="GameDefaultsService.ApplyAtLauncherStart"/> did, against the expectation per installation.</summary>
        public static IReadOnlyList<string> CompareStart(Expectation expectation, DefaultsStartup startup)
        {
            if (expectation == null)
                throw new ArgumentNullException(nameof(expectation));
            if (startup == null)
                throw new ArgumentNullException(nameof(startup));
            var problems = new List<string>();
            if (startup.Block != null)
            {
                problems.Add("the defaults at the start were blocked: " + startup.Block +
                             " (a setup, its uninstaller or a game still runs)");
                return problems;
            }
            foreach (InstallationExpectation expected in expectation.Installations)
            {
                List<GameDefaultsAtStart> games = startup.Games.Where(game => WinPath.IsSamePath(game.Installation.Root, expected.Root)).ToList();
                foreach (Game game in expected.DefaultsAtStart?.Games ?? new Game[0])
                {
                    GameDefaultsAtStart result = games.FirstOrDefault(entry => entry.Game == game);
                    if (result == null)
                        problems.Add(expected + " " + game.Id + ": the start did not handle this game");
                    else if (result.Defaults != expected.DefaultsAtStart[game])
                        problems.Add(expected + " " + game.Id + ": the defaults at the start are " + result.Defaults + ", expected " +
                                     expected.DefaultsAtStart[game]);
                }
                foreach (Game game in expected.InstalledFromAtStart?.Games ?? new Game[0])
                {
                    GameDefaultsAtStart result = games.FirstOrDefault(entry => entry.Game == game);
                    if (result == null)
                        problems.Add(expected + " " + game.Id + ": the start did not handle this game");
                    else if (result.InstalledFrom != expected.InstalledFromAtStart[game])
                        problems.Add(expected + " " + game.Id + ": \"Installed From\" at the start is " + result.InstalledFrom + ", expected " +
                                     expected.InstalledFromAtStart[game]);
                }
            }
            return problems;
        }

        /// <summary>
        /// The changes in <paramref name="writes"/> that the defaults may not make: anything outside HKCU, and in HKCU anything but
        /// the values of the table in the game settings keys of the installations' games, their defaults markers and the GPU
        /// preference of their programs (contract 3.1, 3.4, 3.5; a narrower list than the launcher's write policy, which also
        /// allows the cleanup and the compatibility options).
        /// </summary>
        public static IReadOnlyList<string> UnexpectedWrites(IEnumerable<RegistryWrite> writes, IEnumerable<Installation> installations)
        {
            if (writes == null)
                throw new ArgumentNullException(nameof(writes));
            if (installations == null)
                throw new ArgumentNullException(nameof(installations));
            List<Installation> all = installations.ToList();
            return writes.Where(write => !IsAllowed(write, all)).Select(write => "a change the defaults may not make: " + write).ToList();
        }

        private static bool IsAllowed(RegistryWrite write, IReadOnlyList<Installation> installations)
        {
            if (write.Key.Hive != Microsoft.Win32.RegistryHive.CurrentUser || write.Operation == RegistryOperation.DeleteSubKeyTree)
                return false;
            foreach (Installation installation in installations)
            {
                foreach (Game game in GameDefaultsService.GamesOf(installation))
                {
                    RegistryLocation settings = GameDefaultsService.SettingsKey(installation, game);
                    foreach (string subKey in new[] { string.Empty, GameSettingsTable.GameOptions })
                    {
                        RegistryLocation key = subKey.Length == 0 ? settings : settings.Child(subKey);
                        if (write.Key.Equals(key) && (write.ValueName == null ||
                                                      GameSettingsTable.ValueNamesIn(subKey).Contains(write.ValueName, StringComparer.OrdinalIgnoreCase)))
                            return true;
                    }
                    if (write.Key.Equals(GameDefaultsService.MarkerKey(installation.Product)) &&
                        (write.ValueName == null || string.Equals(write.ValueName, game.Id, StringComparison.OrdinalIgnoreCase)))
                        return true;
                    if (write.Key.Equals(RegistryLocation.CurrentUser(ContractNames.GpuPreferencesKey)) &&
                        (write.ValueName == null || WinPath.IsSamePath(write.ValueName, GameDefaultsService.ProgramPath(installation, game))))
                        return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The values of every game of <paramref name="installation"/> that are not the recommended ones (contract 3.2, 3.3):
        /// every value of the table, the marker of the launcher's contract version, and the GPU preference exactly when it applies
        /// (3.4).
        /// </summary>
        public static IReadOnlyList<string> CompareWithRecommended(IRegistry registry, Installation installation,
            GameDefaultsService defaults, ISystemInfo systemInfo, IFileSystem fileSystem)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            var problems = new List<string>();
            foreach (Game game in GameDefaultsService.GamesOf(installation))
            {
                RecommendedValues recommended = RecommendedValues.For(installation, game, systemInfo, fileSystem);
                RegistryLocation key = GameDefaultsService.SettingsKey(installation, game);
                foreach (GameSetting setting in GameSettingsTable.All)
                {
                    RegistryValue wanted = recommended.ValueOf(setting);
                    if (wanted != null)
                        Expect(problems, installation, game, setting.Name, registry.GetValue(setting.KeyIn(key), setting.ValueName), wanted);
                }
                Expect(problems, installation, game, ExpectationFile.MarkerName,
                    registry.GetValue(GameDefaultsService.MarkerKey(installation.Product), game.Id), RegistryValue.FromDWord(ContractNames.ContractVersion));
                Expect(problems, installation, game, ExpectationFile.GpuPreferenceName,
                    registry.GetValue(RegistryLocation.CurrentUser(ContractNames.GpuPreferencesKey), GameDefaultsService.ProgramPath(installation, game)),
                    defaults.AppliesGpuPreference(installation) ? RegistryValue.FromString(ContractNames.GpuPreferenceData) : null);
            }
            return problems;
        }

        private static void Expect(List<string> problems, Installation installation, Game game, string name,
            RegistryResult<RegistryValue> actual, RegistryValue wanted)
        {
            string actualText = SettingValues.Describe(actual);
            string wantedText = SettingValues.Describe(wanted);
            if (!string.Equals(actualText, wantedText, StringComparison.Ordinal))
                problems.Add(installation.Product.Id + " in " + installation.Root + " " + game.Id + " \"" + name + "\" is " + actualText +
                             ", recommended " + wantedText);
        }
    }
}
