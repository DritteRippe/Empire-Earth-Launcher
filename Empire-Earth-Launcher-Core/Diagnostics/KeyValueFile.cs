using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Diagnostics
{
    /// <summary>How reading a configuration file of the game ended (the files are only read, never changed).</summary>
    public enum ConfigFileStatus
    {
        /// <summary>The file was read.</summary>
        Read,

        /// <summary>The file does not exist (normal for <c>NeoEE.cfg</c> in an EE installation without NeoEE).</summary>
        Missing,

        /// <summary>The file exists but could not be read (access denied, in use, too large).</summary>
        Unreadable
    }

    /// <summary>
    /// The text of a small configuration file of the game (<c>NeoEE.cfg</c>, <c>WONLobby.cfg</c>, <c>upnp_info.txt</c>), read
    /// where the game reads it (the VirtualStore copy first, ADR 0016), and its <c>Key: value</c> lines.
    /// </summary>
    internal sealed class KeyValueFile
    {
        /// <summary>The files are a few hundred bytes; a larger one is not read (a damaged or foreign file).</summary>
        public const long MaxBytes = 64 * 1024;

        private static readonly Encoding Latin1 = Encoding.GetEncoding(28591);

        private KeyValueFile(string path, bool isVirtualStoreCopy, ConfigFileStatus status, string problem, string text)
        {
            Path = path;
            IsVirtualStoreCopy = isVirtualStoreCopy;
            Status = status;
            Problem = problem;
            Text = text ?? string.Empty;
        }

        /// <summary>The file that was read (or would have been).</summary>
        public string Path { get; }

        /// <summary>True if the copy below <c>%LOCALAPPDATA%\VirtualStore</c> was read (ADR 0016).</summary>
        public bool IsVirtualStoreCopy { get; }

        public ConfigFileStatus Status { get; }

        /// <summary>Why the file could not be read, for the log; null otherwise.</summary>
        public string Problem { get; }

        /// <summary>The text (ASCII; other bytes as Latin-1, never a decoding error); empty unless read.</summary>
        public string Text { get; }

        /// <summary>Reads <paramref name="fileName"/> of <paramref name="gameFolder"/> where the game reads it.</summary>
        public static KeyValueFile Read(IFileSystem fileSystem, EffectivePathResolver effectivePaths, string gameFolder, string fileName)
        {
            if (fileSystem == null)
                throw new ArgumentNullException(nameof(fileSystem));
            if (effectivePaths == null)
                throw new ArgumentNullException(nameof(effectivePaths));
            if (gameFolder == null)
                throw new ArgumentNullException(nameof(gameFolder));
            EffectivePath effective = effectivePaths.Resolve(WinPath.Combine(gameFolder, fileName));
            string path = effective.Path;
            if (!fileSystem.FileExists(path))
                return new KeyValueFile(path, effective.IsVirtualStoreCopy, ConfigFileStatus.Missing, null, null);
            FileSystemResult<byte[]> bytes = fileSystem.ReadAllBytes(path, MaxBytes);
            if (!bytes.IsOk)
                return new KeyValueFile(path, effective.IsVirtualStoreCopy, ConfigFileStatus.Unreadable, bytes.ToString(), null);
            byte[] data = bytes.Value;
            int start = data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? 3 : 0;
            return new KeyValueFile(path, effective.IsVirtualStoreCopy, ConfigFileStatus.Read, null,
                Latin1.GetString(data, start, data.Length - start));
        }

        /// <summary>A file read from <paramref name="text"/> (for the parsers' tests).</summary>
        internal static KeyValueFile FromText(string text)
        {
            return new KeyValueFile("test", false, ConfigFileStatus.Read, null, text);
        }

        /// <summary>
        /// The <c>Key: value</c> lines: the key before the first colon (trimmed, compared ignoring case), the value up to the
        /// first white space or comment (<paramref name="commentMarkers"/>, e.g. <c>#</c> or <c>//</c>). Lines without a colon
        /// and comment lines are skipped; the first line of a key wins.
        /// </summary>
        public IReadOnlyDictionary<string, string> Values(params string[] commentMarkers)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string rawLine in Text.Split('\n'))
            {
                string line = StripComment(rawLine.TrimEnd('\r'), commentMarkers).Trim();
                int colon = line.IndexOf(':');
                if (colon <= 0)
                    continue;
                string key = line.Substring(0, colon).Trim();
                string rest = line.Substring(colon + 1).Trim();
                int space = rest.IndexOfAny(new[] { ' ', '\t' });
                string value = space < 0 ? rest : rest.Substring(0, space);
                if (key.Length > 0 && !values.ContainsKey(key))
                    values[key] = value;
            }
            return values;
        }

        private static string StripComment(string line, string[] commentMarkers)
        {
            int cut = line.Length;
            foreach (string marker in commentMarkers)
            {
                int index = line.IndexOf(marker, StringComparison.Ordinal);
                if (index >= 0 && index < cut)
                    cut = index;
            }
            return line.Substring(0, cut);
        }

        /// <summary><c>true</c>/<c>false</c> (any case) or <c>1</c>/<c>0</c>; null for anything else or a missing key.</summary>
        public static bool? Boolean(IReadOnlyDictionary<string, string> values, string key)
        {
            if (!values.TryGetValue(key, out string text))
                return null;
            if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) || text == "1")
                return true;
            if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase) || text == "0")
                return false;
            return null;
        }

        /// <summary>A port from 1 to 65535; null for anything else or a missing key.</summary>
        public static int? Port(IReadOnlyDictionary<string, string> values, string key)
        {
            if (!values.TryGetValue(key, out string text))
                return null;
            return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int port) && port >= 1 && port <= 65535
                ? port
                : (int?)null;
        }

        /// <summary>True if <paramref name="key"/> exists but its value is not what <paramref name="parsed"/> accepts.</summary>
        public static bool IsInvalid<T>(IReadOnlyDictionary<string, string> values, string key, T? parsed) where T : struct
        {
            return values.ContainsKey(key) && parsed == null;
        }
    }
}
