using System;
using System.Collections.Generic;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Maintenance;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.GameSettings
{
    /// <summary>
    /// The allow-list of the launcher for <see cref="RegistryWritePolicy"/> (ADR 0007), narrowed in L-WP5 from whole keys to
    /// the value names of the contract tables, the program paths and the content of the compatibility values.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>the game settings keys of contract 3.1: the values of table 3.2 in the key and in <c>Game Options</c>
    /// (<see cref="GameSettingsTable"/>);</item>
    /// <item>the defaults markers <c>GameDefaults\&lt;Product&gt;</c>: the values <c>EE</c> and <c>AoC</c> (contract 3.5);</item>
    /// <item><c>UserGpuPreferences</c>: value names that are full paths of a game program (contract 3.4);</item>
    /// <item>HKCU <c>AppCompatFlags\Layers</c>: value names that are program paths; a written value may only add or remove
    /// the entries of the rows <c>compatibility</c> and <c>compatibility_windows</c> (Windows 8 and later), every other entry
    /// stays; a value may be deleted when nothing else is in it or it is exactly <c>~ RUNASADMIN</c> (contract 3.7, ADR 0007
    /// plan review). On Windows 7 and under Wine no entry may be switched (<see cref="WithoutLayerEntries"/>).</item>
    /// <item>the HKCU keys of the cleanup list the launcher deletes (<see cref="CleanupCandidates"/>, L-WP8): only
    /// <c>DeleteSubKeyTree</c> of exactly these keys, which are the game settings keys of contract 3.1 and the registry
    /// VirtualStore copies of the SSSI and Mad Doc keys.</item>
    /// </list>
    /// The game settings rules allow setting and deleting these values and creating the key; no other key is ever deleted. The
    /// policy cannot know which installations the discovery found: the services write only the program paths of discovered
    /// installations (tests), the policy checks that a name is a program path at all; likewise the cleanup deletes a key only
    /// when no installation of its product was found and its folder is gone (<see cref="RegistryCleanup"/>), the policy allows
    /// exactly the listed keys and still refuses every protected key first.
    /// </remarks>
    public static class LauncherWritePolicy
    {
        private static readonly RegistryOperation[] ValueAndKeyCreation =
        {
            RegistryOperation.SetValue, RegistryOperation.DeleteValue, RegistryOperation.CreateSubKey
        };

        /// <summary>The policy of the launcher on Windows 8 and later.</summary>
        public static RegistryWritePolicy Default { get; } = new RegistryWritePolicy(AllowList(CompatibilityLayers.LauncherEntries));

        /// <summary>The policy on Windows 7 and under Wine: no compatibility entry may be switched (contract 3.7).</summary>
        public static RegistryWritePolicy WithoutLayerEntries { get; } = new RegistryWritePolicy(AllowList(new string[0]));

        /// <summary>The policy for this computer: <see cref="Default"/> from Windows 8 on and outside Wine.</summary>
        public static RegistryWritePolicy For(ISystemInfo systemInfo)
        {
            if (systemInfo == null)
                throw new ArgumentNullException(nameof(systemInfo));
            return AreLayerEntriesAllowed(systemInfo) ? Default : WithoutLayerEntries;
        }

        /// <summary>
        /// True if the launcher may switch compatibility entries on this computer: Windows 8 (NT 6.2) or later and not Wine,
        /// the Windows versions of contract 3.7 (the setup offers no task under Wine either).
        /// </summary>
        public static bool AreLayerEntriesAllowed(ISystemInfo systemInfo)
        {
            if (systemInfo == null)
                throw new ArgumentNullException(nameof(systemInfo));
            return systemInfo.IsWindows8OrLater() && !systemInfo.IsWine;
        }

        private static IEnumerable<RegistryWriteRule> AllowList(IEnumerable<string> layerEntries)
        {
            foreach (Product product in Product.All)
            {
                foreach (Game game in Game.All)
                {
                    string settingsKey = product.GetGameSettingsKey(game);
                    yield return new RegistryWriteRule(settingsKey, GameSettingsTable.ValueNamesIn(string.Empty), ValueAndKeyCreation);
                    yield return new RegistryWriteRule(settingsKey + @"\" + GameSettingsTable.GameOptions,
                        GameSettingsTable.ValueNamesIn(GameSettingsTable.GameOptions), ValueAndKeyCreation);
                }
                yield return new RegistryWriteRule(product.DefaultsMarkerKey, Game.All.Select(game => game.Id), ValueAndKeyCreation);
            }
            yield return RegistryWriteRule.ForProgramPaths(ContractNames.GpuPreferencesKey, ValueAndKeyCreation);
            yield return RegistryWriteRule.ForCompatibilityLayers(ContractNames.CompatibilityLayersKey, layerEntries.ToList(),
                ValueAndKeyCreation);
            foreach (RegistryWriteRule rule in CleanupCandidates.WriteRules(CleanupCandidates.All))
                yield return rule;
        }
    }
}
