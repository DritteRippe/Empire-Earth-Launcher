using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.GameSettings
{
    /// <summary>The findings of the consistency checks (contract 3.6), each with its own text and advice in the UI.</summary>
    public enum FindingCode
    {
        /// <summary>
        /// <c>Game Bit Depth</c> differs from <c>Texture Bit Depth</c>: the main menu is white and unreadable (comment of the
        /// setup's <c>GameSettings</c> block). Advice: the recommended display settings.
        /// </summary>
        BitDepthMismatch,

        /// <summary>
        /// 16 bit on Windows 8 and later: freezes (t=10931 p=47182, t=11042 p=48016); some players run it on purpose
        /// (t=5848 p=81313), so the hint can be hidden.
        /// </summary>
        SixteenBitOnWindows8,

        /// <summary>
        /// <c>Rasterizer Name</c> does not match the wrapper rule of contract 3.3; "Direct3D" without a wrapper can be a
        /// deliberate choice against FPS problems (t=3935 p=48202), so the hint can be hidden.
        /// </summary>
        RasterizerMismatch,

        /// <summary>The game window is larger than the screen as the game sees it, also physically. Advice: the recommended display settings.</summary>
        WindowLargerThanScreen,

        /// <summary>
        /// The game window fits the screen in physical pixels but not in the logical size the game sees without
        /// <c>HIGHDPIAWARE</c> (contract O4, ADR 0011 plan review). Advice: switch on the compatibility option or set the
        /// scaling to 100 % - not a reset, which would write the same values again.
        /// </summary>
        WindowFitsOnlyWithHighDpiAware,

        /// <summary>The primary screen is lower than 768 pixels: the game's menu does not fit (t=3863, R13).</summary>
        ScreenTooLow,

        /// <summary>The game folder is not on a drive letter: the "Installed From" values cannot name it (contract 3.3).</summary>
        InstalledFromNotOnADrive,

        /// <summary>
        /// The game folder has characters outside the ANSI code page of Windows: the game, a non-Unicode program, may not
        /// open it (forum report section 8, test case 20; ADR 0015). Information only.
        /// </summary>
        FolderOutsideAnsiCodePage
    }

    /// <summary>One finding: what, for which game, the values it is about and what is recommended.</summary>
    public sealed class ConsistencyFinding
    {
        internal ConsistencyFinding(FindingCode code, Installation installation, Game game,
            IEnumerable<KeyValuePair<string, RegistryValue>> values, string recommended, ScreenSize gameScreen)
        {
            Code = code;
            Installation = installation;
            Game = game;
            Values = new ReadOnlyCollection<KeyValuePair<string, RegistryValue>>(
                (values ?? Enumerable.Empty<KeyValuePair<string, RegistryValue>>()).ToList());
            Recommended = recommended;
            GameScreen = gameScreen;
        }

        public FindingCode Code { get; }

        public Installation Installation { get; }

        /// <summary>The game; null for a finding about the computer (<see cref="FindingCode.ScreenTooLow"/>).</summary>
        public Game Game { get; }

        /// <summary>The values the finding is about, with their current data (value name -> value).</summary>
        public IReadOnlyList<KeyValuePair<string, RegistryValue>> Values { get; }

        /// <summary>The recommended data as text (<c>32</c>, <c>Direct3D Hardware TnL</c>, <c>1920x1080</c>), or null.</summary>
        public string Recommended { get; }

        /// <summary>For the window findings: the screen as the game sees it; else <see cref="ScreenSize.Empty"/>.</summary>
        public ScreenSize GameScreen { get; }

        /// <summary>
        /// What the hint is about, for hiding it (ADR 0015): the finding and the game settings key (<c>screen</c> for the
        /// screen), e.g. <c>BitDepthMismatch HKCU\Software\Neo\Empire Earth</c>.
        /// </summary>
        public string HintKey
        {
            get
            {
                string key = Game == null ? "screen" : "HKCU\\" + Installation.GetGameSettingsKey(Game);
                return Code + " " + key;
            }
        }

        /// <summary>
        /// The values as text, e.g. <c>Game Bit Depth=16; Texture Bit Depth=32</c>: a hidden hint shows again when they
        /// change (ADR 0015, "per value and content").
        /// </summary>
        public string HintValues
        {
            get
            {
                if (Code == FindingCode.ScreenTooLow)
                    return GameScreen.ToString();
                if (Code == FindingCode.InstalledFromNotOnADrive || Code == FindingCode.FolderOutsideAnsiCodePage)
                    return Installation.GetGameFolder(Game);
                return string.Join("; ", Values.Select(value => value.Key + "=" + Data(value.Value))) +
                       (GameScreen.IsEmpty ? string.Empty : "; screen=" + GameScreen);
            }
        }

        /// <summary>The data of a value without its type, for <see cref="HintValues"/> and the UI.</summary>
        public static string Data(RegistryValue value)
        {
            if (value == null)
                return string.Empty;
            switch (value.Type)
            {
                case RegistryValueType.String:
                case RegistryValueType.ExpandString:
                    return value.StringValue;
                case RegistryValueType.DWord:
                    return value.DWordValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                default:
                    return value.ToString();
            }
        }

        public override string ToString()
        {
            return HintKey + ": " + HintValues + (Recommended == null ? string.Empty : " (recommended " + Recommended + ")");
        }
    }

    /// <summary>
    /// The consistency checks of contract 3.6, computed at every start and shown with an offer, never fixed by themselves:
    /// bit depths that differ, 16 bit on Windows 8 and later, a rasterizer against the wrapper rule, a window larger than
    /// the screen as the game sees it (ADR 0011 plan review), a screen lower than 768 pixels (R13), a game folder that
    /// "Installed From" cannot name, and a game folder with characters outside the ANSI code page (ADR 0015). Only reads.
    /// </summary>
    public sealed class ConsistencyChecker
    {
        private readonly IRegistry registry;
        private readonly IFileSystem fileSystem;
        private readonly ISystemInfo systemInfo;

        public ConsistencyChecker(IRegistry registry, IFileSystem fileSystem, ISystemInfo systemInfo)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            this.systemInfo = systemInfo ?? throw new ArgumentNullException(nameof(systemInfo));
        }

        /// <summary>The findings of every game of the installation, then the screen.</summary>
        public IReadOnlyList<ConsistencyFinding> Check(Installation installation)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            var findings = new List<ConsistencyFinding>();
            foreach (Game game in GameDefaultsService.GamesOf(installation))
                findings.AddRange(Check(installation, game));
            if (ComputedValues.IsScreenTooLow(systemInfo))
                findings.Add(new ConsistencyFinding(FindingCode.ScreenTooLow, installation, null, null,
                    ComputedValues.MinGameWindowWidth + "x" + ComputedValues.MinGameWindowHeight, systemInfo.PrimaryScreen));
            return findings;
        }

        /// <summary>The findings of one game.</summary>
        public IReadOnlyList<ConsistencyFinding> Check(Installation installation, Game game)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            if (game == null)
                throw new ArgumentNullException(nameof(game));
            var findings = new List<ConsistencyFinding>();
            RegistryLocation key = GameDefaultsService.SettingsKey(installation, game);

            string folder = installation.GetGameFolder(game);
            if (!InstalledFromValues.TryCompute(folder, out _))
                findings.Add(new ConsistencyFinding(FindingCode.InstalledFromNotOnADrive, installation, game, null, null, ScreenSize.Empty));
            if (folder != null && !systemInfo.IsInAnsiCodePage(folder))
                findings.Add(new ConsistencyFinding(FindingCode.FolderOutsideAnsiCodePage, installation, game, null, null, ScreenSize.Empty));

            int? gameBits = DWord(key, GameSettingsTable.GameBitDepth);
            int? textureBits = DWord(key, GameSettingsTable.TextureBitDepth);
            KeyValuePair<string, RegistryValue>[] bitValues = Pairs(
                Tuple.Create(GameSettingsTable.GameBitDepth, gameBits), Tuple.Create(GameSettingsTable.TextureBitDepth, textureBits));
            if (gameBits.HasValue && textureBits.HasValue && gameBits != textureBits)
                findings.Add(new ConsistencyFinding(FindingCode.BitDepthMismatch, installation, game, bitValues, "32", ScreenSize.Empty));
            if (systemInfo.IsWindows8OrLater() && (gameBits == 16 || textureBits == 16))
                findings.Add(new ConsistencyFinding(FindingCode.SixteenBitOnWindows8, installation, game, bitValues, "32", ScreenSize.Empty));

            RegistryValue rasterizer = Value(key, GameSettingsTable.RasterizerName);
            if (rasterizer != null && rasterizer.IsString)
            {
                RasterizerRecommendation recommended = ComputedValues.Rasterizer(installation, game, systemInfo, fileSystem);
                if (!string.Equals(rasterizer.StringValue.Trim(), recommended.Name, StringComparison.OrdinalIgnoreCase))
                    findings.Add(new ConsistencyFinding(FindingCode.RasterizerMismatch, installation, game,
                        new[] { new KeyValuePair<string, RegistryValue>(GameSettingsTable.RasterizerName, rasterizer) }, recommended.Name,
                        ScreenSize.Empty));
            }

            ConsistencyFinding window = CheckWindow(installation, game, key);
            if (window != null)
                findings.Add(window);
            return findings;
        }

        /// <summary>
        /// True if the compatibility layer <c>HIGHDPIAWARE</c> applies to the program of the game: an entry of its value
        /// below <c>AppCompatFlags\Layers</c> in HKCU or HKLM (both views), contract 3.7, O4.
        /// </summary>
        public bool IsHighDpiAwareEffective(Installation installation, Game game)
        {
            string program = GameDefaultsService.ProgramPath(installation, game);
            if (program == null)
                return false;
            foreach (RegistryLocation key in CompatibilityOptions.LayerKeys)
            {
                RegistryResult<RegistryValue> value = registry.GetValue(key, program);
                if (value.IsOk && value.Value.IsString &&
                    CompatibilityLayerValue.Parse(value.Value.StringValue).Contains(CompatibilityLayers.HighDpiAware))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// The window against the screen as the game sees it (ADR 0011 plan review): physical pixels with an effective
        /// <c>HIGHDPIAWARE</c>, else the size a DPI-unaware program sees. If it fits only physically, the finding is
        /// <see cref="FindingCode.WindowFitsOnlyWithHighDpiAware"/>.
        /// </summary>
        private ConsistencyFinding CheckWindow(Installation installation, Game game, RegistryLocation key)
        {
            int? width = DWord(key, GameSettingsTable.GameWindowWidth);
            int? height = DWord(key, GameSettingsTable.GameWindowHeight);
            ScreenSize physical = systemInfo.PrimaryScreen;
            if (!width.HasValue || !height.HasValue || physical.IsEmpty)
                return null;

            bool highDpiAware = IsHighDpiAwareEffective(installation, game);
            ScreenSize unaware = systemInfo.PrimaryScreenUnaware.IsEmpty ? physical : systemInfo.PrimaryScreenUnaware;
            ScreenSize gameScreen = highDpiAware ? physical : unaware;
            if (width <= gameScreen.Width && height <= gameScreen.Height)
                return null;

            KeyValuePair<string, RegistryValue>[] values = Pairs(
                Tuple.Create(GameSettingsTable.GameWindowWidth, width), Tuple.Create(GameSettingsTable.GameWindowHeight, height));
            bool fitsPhysically = width <= physical.Width && height <= physical.Height;
            ScreenSize recommended = ComputedValues.GameWindow(systemInfo);
            return new ConsistencyFinding(
                !highDpiAware && fitsPhysically ? FindingCode.WindowFitsOnlyWithHighDpiAware : FindingCode.WindowLargerThanScreen,
                installation, game, values, recommended.ToString(), gameScreen);
        }

        private RegistryValue Value(RegistryLocation key, string name)
        {
            RegistryResult<RegistryValue> value = registry.GetValue(key, name);
            return value.IsOk ? value.Value : null;
        }

        private int? DWord(RegistryLocation key, string name)
        {
            RegistryValue value = Value(key, name);
            return value != null && value.Type == RegistryValueType.DWord ? value.DWordValue : (int?)null;
        }

        private static KeyValuePair<string, RegistryValue>[] Pairs(params Tuple<string, int?>[] values)
        {
            return values.Where(value => value.Item2.HasValue)
                         .Select(value => new KeyValuePair<string, RegistryValue>(value.Item1, RegistryValue.FromDWord(value.Item2.Value)))
                         .ToArray();
        }
    }
}
