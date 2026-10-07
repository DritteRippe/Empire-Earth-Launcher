using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.GameSettings
{
    /// <summary>The classes of the values of contract 3.2: who writes them, and when.</summary>
    public enum SettingClass
    {
        /// <summary>S: bound to the installation ("Installed From"), kept in sync with the game that is started.</summary>
        S,

        /// <summary>D: display defaults; created if missing, existing ones only overwritten with the player's consent.</summary>
        D,

        /// <summary>P: player defaults; created if missing, overwritten only by a reset.</summary>
        P
    }

    /// <summary>Where the data of a value comes from (contract 3.2 and 3.3).</summary>
    public enum SettingData
    {
        /// <summary>The same data for both games.</summary>
        Fixed,

        /// <summary>Data per game (<c>Ending Epoch</c>: the last epoch of the game).</summary>
        PerGame,

        /// <summary>The drive of the game folder (contract 3.3).</summary>
        InstalledFromVolume,

        /// <summary>The game folder without its drive (contract 3.3, ADR 0015).</summary>
        InstalledFromDirectory,

        /// <summary><c>Direct3D</c> or <c>Direct3D Hardware TnL</c> by the wrapper rule (contract 3.3).</summary>
        RasterizerName,

        /// <summary>The width of the primary screen, limited to 1024 to 1920 (contract 3.3).</summary>
        GameWindowWidth,

        /// <summary>The height of the primary screen, limited to 768 to 1200 (contract 3.3, with its wide-screen limit).</summary>
        GameWindowHeight
    }

    /// <summary>One value of the table of contract 3.2.</summary>
    public sealed class GameSetting
    {
        private readonly RegistryValue empireEarthValue;
        private readonly RegistryValue artOfConquestValue;

        private GameSetting(string name, RegistryValueType type, SettingClass settingClass, SettingData data,
            RegistryValue empireEarthValue, RegistryValue artOfConquestValue)
        {
            Name = name;
            Type = type;
            Class = settingClass;
            Data = data;
            this.empireEarthValue = empireEarthValue;
            this.artOfConquestValue = artOfConquestValue;
            int separator = name.LastIndexOf('\\');
            SubKey = separator < 0 ? string.Empty : name.Substring(0, separator);
            ValueName = separator < 0 ? name : name.Substring(separator + 1);
        }

        internal static GameSetting Computed(string name, RegistryValueType type, SettingClass settingClass, SettingData data)
        {
            return new GameSetting(name, type, settingClass, data, null, null);
        }

        internal static GameSetting Fixed(string name, SettingClass settingClass, int data)
        {
            RegistryValue value = RegistryValue.FromDWord(data);
            return new GameSetting(name, RegistryValueType.DWord, settingClass, SettingData.Fixed, value, value);
        }

        internal static GameSetting Fixed(string name, SettingClass settingClass, string data)
        {
            RegistryValue value = RegistryValue.FromString(data);
            return new GameSetting(name, RegistryValueType.String, settingClass, SettingData.Fixed, value, value);
        }

        internal static GameSetting PerGame(string name, SettingClass settingClass, int empireEarth, int artOfConquest)
        {
            return new GameSetting(name, RegistryValueType.DWord, settingClass, SettingData.PerGame,
                RegistryValue.FromDWord(empireEarth), RegistryValue.FromDWord(artOfConquest));
        }

        /// <summary>The name as the contract table writes it, relative to the game settings key: <c>Game Options\Map Type</c>.</summary>
        public string Name { get; }

        /// <summary>The subkey of the game settings key the value is in (<c>Game Options</c>), or empty for the key itself.</summary>
        public string SubKey { get; }

        /// <summary>The name of the value in its key: <c>Map Type</c>.</summary>
        public string ValueName { get; }

        /// <summary><c>REG_SZ</c> or <c>REG_DWORD</c>.</summary>
        public RegistryValueType Type { get; }

        public SettingClass Class { get; }

        public SettingData Data { get; }

        /// <summary>
        /// The contract version that added the value (contract 3.5: a marker lower than the launcher's contract version
        /// means that the values added since then are created if missing). Every value of version 1 has 1.
        /// </summary>
        public int AddedInContractVersion
        {
            get { return 1; }
        }

        /// <summary>True if the data depends on the installation or the computer (contract 3.3).</summary>
        public bool IsComputed
        {
            get { return Data != SettingData.Fixed && Data != SettingData.PerGame; }
        }

        /// <summary>The data of a value that is not computed, for <paramref name="game"/>.</summary>
        /// <exception cref="InvalidOperationException">The value is computed (<see cref="IsComputed"/>).</exception>
        public RegistryValue FixedValue(Game game)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            if (IsComputed)
                throw new InvalidOperationException(Name + " is computed (contract 3.3).");
            return game == Game.EmpireEarth ? empireEarthValue : artOfConquestValue;
        }

        /// <summary>The key of the value below the game settings key <paramref name="gameSettingsKey"/>.</summary>
        public RegistryLocation KeyIn(RegistryLocation gameSettingsKey)
        {
            if (gameSettingsKey == null)
                throw new ArgumentNullException(nameof(gameSettingsKey));
            return SubKey.Length == 0 ? gameSettingsKey : gameSettingsKey.Child(SubKey);
        }

        public override string ToString()
        {
            return Name;
        }
    }

    /// <summary>
    /// The values of contract 3.2 in the order of the contract table: name, type, data and class. The source of truth is
    /// the <c>GameSettings</c> block of the setup's <c>setup_is6.iss</c>; <c>GameSettingsTableContractTests</c> compares
    /// this table with <c>docs/CONTRACT.md</c>, which the setup's <c>ci/check_contract.py</c> compares with the script.
    /// </summary>
    public static class GameSettingsTable
    {
        /// <summary>The subkey of the game settings key with the game options (contract 3.2).</summary>
        public const string GameOptions = ContractNames.GameOptionsSubKeyName;

        /// <summary><c>Rasterizer Name</c> without a DirectX wrapper and outside Wine (contract 3.3).</summary>
        public const string HardwareTnLRasterizer = "Direct3D Hardware TnL";

        /// <summary><c>Rasterizer Name</c> under Wine or with a DirectX wrapper (contract 3.3).</summary>
        public const string Direct3DRasterizer = "Direct3D";

        public const string RasterizerName = "Rasterizer Name";
        public const string WaitForVSync = "Wait for VSync";
        public const string GameWindowWidth = "Game Window Width";
        public const string GameWindowHeight = "Game Window Height";
        public const string GameBitDepth = "Game Bit Depth";
        public const string TextureBitDepth = "Texture Bit Depth";

        /// <summary>
        /// The files of the DirectX wrappers in a game folder (contract 3.3: the setup removes them in
        /// <c>[InstallDelete]</c> before it installs a wrapper; the game ships none of them).
        /// </summary>
        public static readonly IReadOnlyList<string> DirectXWrapperFiles =
            new ReadOnlyCollection<string>(new[] { "DDraw.dll", "D3DImm.dll", "D3D8.dll", "D3D9.dll" });

        /// <summary>Every value of contract 3.2, in the order of its table.</summary>
        public static readonly IReadOnlyList<GameSetting> All = new ReadOnlyCollection<GameSetting>(new[]
        {
            GameSetting.Computed(ContractNames.InstalledFromVolumeName, RegistryValueType.String, SettingClass.S, SettingData.InstalledFromVolume),
            GameSetting.Computed(ContractNames.InstalledFromDirectoryName, RegistryValueType.String, SettingClass.S, SettingData.InstalledFromDirectory),
            GameSetting.Computed(RasterizerName, RegistryValueType.String, SettingClass.D, SettingData.RasterizerName),
            GameSetting.Fixed(WaitForVSync, SettingClass.D, 0),
            GameSetting.Computed(GameWindowWidth, RegistryValueType.DWord, SettingClass.D, SettingData.GameWindowWidth),
            GameSetting.Computed(GameWindowHeight, RegistryValueType.DWord, SettingClass.D, SettingData.GameWindowHeight),
            GameSetting.Fixed(GameBitDepth, SettingClass.D, 32),
            GameSetting.Fixed(TextureBitDepth, SettingClass.D, 32),
            GameSetting.Fixed("AutoSave In Milliseconds", SettingClass.P, 1200000),
            GameSetting.Fixed("Music Volume", SettingClass.P, 44),
            GameSetting.Fixed("Sound Volume", SettingClass.P, 60),
            GameSetting.Fixed("Take JPG Screenshots", SettingClass.P, 1),
            GameSetting.Fixed(GameOptions + @"\Map Type", SettingClass.P, "Continental"),
            GameSetting.Fixed(GameOptions + @"\Map Size", SettingClass.P, 2),
            GameSetting.Fixed(GameOptions + @"\Starting Resources", SettingClass.P, 3),
            GameSetting.Fixed(GameOptions + @"\Starting Epoch", SettingClass.P, 0),
            GameSetting.PerGame(GameOptions + @"\Ending Epoch", SettingClass.P, 13, 14),
            GameSetting.Fixed(GameOptions + @"\Game Unit Limit", SettingClass.P, 1200),
            GameSetting.Fixed(GameOptions + @"\Wonders For Victory", SettingClass.P, 0),
            GameSetting.Fixed(GameOptions + @"\Game Variant", SettingClass.P, 2),
            GameSetting.Fixed(GameOptions + @"\Difficulty Level", SettingClass.P, 0),
            GameSetting.Fixed(GameOptions + @"\Game Speed", SettingClass.P, 3),
            GameSetting.Fixed(GameOptions + @"\Reveal Map", SettingClass.P, 0),
            GameSetting.Fixed(GameOptions + @"\Allow Custom Civs", SettingClass.P, 1),
            GameSetting.Fixed(GameOptions + @"\Lock Teams", SettingClass.P, 1),
            GameSetting.Fixed(GameOptions + @"\Lock Speed", SettingClass.P, 1),
            GameSetting.Fixed(GameOptions + @"\Cheat Codes", SettingClass.P, 0),
        });

        /// <summary>The value of the table named <paramref name="name"/> (ignoring case), or null.</summary>
        public static GameSetting Find(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));
            return All.FirstOrDefault(setting => string.Equals(setting.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The values of one class, in the order of the table.</summary>
        public static IReadOnlyList<GameSetting> OfClass(SettingClass settingClass)
        {
            return All.Where(setting => setting.Class == settingClass).ToList();
        }

        /// <summary>
        /// The names of the values in the subkey <paramref name="subKey"/> of a game settings key (empty for the key
        /// itself): what the write policy allows there (ADR 0007).
        /// </summary>
        public static IReadOnlyList<string> ValueNamesIn(string subKey)
        {
            if (subKey == null)
                throw new ArgumentNullException(nameof(subKey));
            return All.Where(setting => string.Equals(setting.SubKey, subKey, StringComparison.OrdinalIgnoreCase))
                      .Select(setting => setting.ValueName)
                      .ToList();
        }
    }
}
