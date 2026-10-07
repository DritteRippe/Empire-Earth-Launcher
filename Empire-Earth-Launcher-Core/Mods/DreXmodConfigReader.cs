using System;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Core.Diagnostics;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Mods
{
    /// <summary>One of the two selectors of <c>dreXmod.config</c>, <c>&lt;Mod&gt;</c> or <c>&lt;LobbyTheme&gt;</c>.</summary>
    public sealed class DreXmodSelector
    {
        /// <summary>A selector the file does not have (dreXmod 2, a damaged file).</summary>
        public static readonly DreXmodSelector Missing = new DreXmodSelector(false, null, null);

        internal DreXmodSelector(bool isPresent, bool? enabled, string name)
        {
            IsPresent = isPresent;
            Enabled = enabled;
            Name = name;
        }

        /// <summary>True if the file has the element.</summary>
        public bool IsPresent { get; }

        /// <summary><c>&lt;Enabled&gt;</c>: 1 or true is true, 0 or false is false; null if it is missing or says something else.</summary>
        public bool? Enabled { get; }

        /// <summary><c>&lt;Name&gt;</c>, the name of a folder in <c>Data\dxm\mods</c>; null if it is missing or empty.</summary>
        public string Name { get; }

        /// <summary>True if the selector is on and names <paramref name="folderName"/> (ignoring case, as Windows does).</summary>
        public bool Selects(string folderName)
        {
            return Enabled == true && Name != null && string.Equals(Name, folderName, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// The text of a <c>dreXmod.config</c> of dreXmod 3 as far as the Mods page needs it: which preset is the active mod
    /// (<c>&lt;Mod&gt;</c>, textures and sounds) and which is the active lobby theme (<c>&lt;LobbyTheme&gt;</c>). Reading only: the
    /// launcher of 1.1.0 never changes the file (ADR 0014).
    /// </summary>
    /// <remarks>
    /// The file is not valid XML as a whole (it begins with a comment before <c>&lt;config&gt;</c>, uses tabs, CRLF and comments
    /// with text that looks like elements), so it is read as text: the comments are cut out first, then the first
    /// <c>&lt;Mod&gt;</c> and the first <c>&lt;LobbyTheme&gt;</c> element are taken and their <c>&lt;Enabled&gt;</c> and <c>&lt;Name&gt;</c>.
    /// Tags are compared ignoring case, white space around a value is ignored, and anything else in the file is skipped, also
    /// in the variant without telemetry (which differs only in the <c>GoogleAnalytics</c> values).
    /// </remarks>
    public sealed class DreXmodConfig
    {
        private static readonly Regex Comment = new Regex(@"<!--.*?-->", RegexOptions.CultureInvariant | RegexOptions.Singleline);

        private DreXmodConfig(DreXmodSelector mod, DreXmodSelector lobbyTheme)
        {
            Mod = mod;
            LobbyTheme = lobbyTheme;
        }

        /// <summary>The mod (textures and sounds of the game).</summary>
        public DreXmodSelector Mod { get; }

        /// <summary>The lobby theme (fonts, skin, icons and images of the lobby).</summary>
        public DreXmodSelector LobbyTheme { get; }

        /// <summary>True if the file has a mod selector at all: dreXmod 2 has none (<c>Camera</c>, <c>LobbyExtension</c>, ...).</summary>
        public bool HasModSystem
        {
            get { return Mod.IsPresent || LobbyTheme.IsPresent; }
        }

        /// <summary>Reads the text of a <c>dreXmod.config</c>; null counts as empty.</summary>
        public static DreXmodConfig Parse(string text)
        {
            string content = Comment.Replace(text ?? string.Empty, " ");
            // A comment that is never closed runs to the end of the file, as it does for an XML reader.
            int open = content.IndexOf("<!--", StringComparison.Ordinal);
            if (open >= 0)
                content = content.Substring(0, open);
            return new DreXmodConfig(Selector(content, "Mod"), Selector(content, "LobbyTheme"));
        }

        private static DreXmodSelector Selector(string content, string element)
        {
            Match block = Regex.Match(content, "<" + element + @"\s*>(?<body>.*?)</" + element + @"\s*>",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (!block.Success)
                return DreXmodSelector.Missing;
            string body = block.Groups["body"].Value;
            string enabled = Value(body, "Enabled");
            string name = Value(body, "Name");
            return new DreXmodSelector(true, Truth(enabled), name != null && name.Length > 0 ? name : null);
        }

        /// <summary>The trimmed text of the first <paramref name="element"/> of <paramref name="body"/>; null if there is none.</summary>
        private static string Value(string body, string element)
        {
            Match match = Regex.Match(body, "<" + element + @"\s*>(?<value>[^<]*)</" + element + @"\s*>",
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
            return match.Success ? WebUtility.HtmlDecode(match.Groups["value"].Value).Trim() : null;
        }

        private static bool? Truth(string text)
        {
            if (text == "1" || string.Equals(text, "true", StringComparison.OrdinalIgnoreCase))
                return true;
            if (text == "0" || string.Equals(text, "false", StringComparison.OrdinalIgnoreCase))
                return false;
            return null;
        }
    }

    /// <summary>The <c>dreXmod.config</c> of one game folder as the game reads it (<see cref="DreXmodConfigReader"/>).</summary>
    public sealed class DreXmodConfigFile
    {
        internal DreXmodConfigFile(string path, bool isVirtualStoreCopy, ConfigFileStatus status, string problem, DreXmodConfig config)
        {
            Path = path;
            IsVirtualStoreCopy = isVirtualStoreCopy;
            Status = status;
            Problem = problem;
            Config = config;
        }

        /// <summary>The file that was read (or would have been).</summary>
        public string Path { get; }

        /// <summary>True if the copy below <c>%LOCALAPPDATA%\VirtualStore</c> was read, which the game uses (ADR 0016).</summary>
        public bool IsVirtualStoreCopy { get; }

        public ConfigFileStatus Status { get; }

        /// <summary>Why the file could not be read, for the log; null otherwise.</summary>
        public string Problem { get; }

        /// <summary>The selectors of the file; null unless <see cref="Status"/> is <see cref="ConfigFileStatus.Read"/>.</summary>
        public DreXmodConfig Config { get; }
    }

    /// <summary>
    /// Reads <c>dreXmod.config</c> of a game folder where the game reads it (the VirtualStore copy first, ADR 0016). Only reads.
    /// </summary>
    public static class DreXmodConfigReader
    {
        /// <summary>The name of the file in a game folder.</summary>
        public const string FileName = "dreXmod.config";

        /// <summary>The file is 14 KB; a larger one is not read (a damaged or foreign file).</summary>
        public const long MaxBytes = 256 * 1024;

        public static DreXmodConfigFile Read(IFileSystem fileSystem, EffectivePathResolver effectivePaths, string gameFolder)
        {
            if (fileSystem == null)
                throw new ArgumentNullException(nameof(fileSystem));
            if (effectivePaths == null)
                throw new ArgumentNullException(nameof(effectivePaths));
            if (gameFolder == null)
                throw new ArgumentNullException(nameof(gameFolder));
            EffectivePath effective = effectivePaths.Resolve(WinPath.Combine(gameFolder, FileName));
            string path = effective.Path;
            if (!fileSystem.FileExists(path))
                return new DreXmodConfigFile(path, effective.IsVirtualStoreCopy, ConfigFileStatus.Missing, null, null);
            FileSystemResult<byte[]> bytes = fileSystem.ReadAllBytes(path, MaxBytes);
            if (!bytes.IsOk)
                return new DreXmodConfigFile(path, effective.IsVirtualStoreCopy, ConfigFileStatus.Unreadable, bytes.ToString(), null);
            return new DreXmodConfigFile(path, effective.IsVirtualStoreCopy, ConfigFileStatus.Read, null,
                DreXmodConfig.Parse(Decode(bytes.Value)));
        }

        /// <summary>
        /// The text of a file of a preset or of the config: UTF-8 (with or without a byte order mark), and for a file that is
        /// not valid UTF-8 Latin-1, so that a decoding error never hides a file and a name with accents stays what the folder
        /// is called.
        /// </summary>
        internal static string Decode(byte[] data)
        {
            int start = data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? 3 : 0;
            try
            {
                return new UTF8Encoding(false, true).GetString(data, start, data.Length - start);
            }
            catch (DecoderFallbackException)
            {
                return Encoding.GetEncoding(28591).GetString(data, start, data.Length - start);
            }
        }
    }
}
