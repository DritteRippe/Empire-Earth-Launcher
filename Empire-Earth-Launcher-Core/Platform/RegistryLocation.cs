using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>
    /// A registry key: hive, view and path below the hive (ADR 0006). HKLM always names its view (contract 0,
    /// "Registry views": the launcher never depends on its own bitness); every other hive is the same in both
    /// views and is stored with <see cref="RegistryView.Default"/>.
    /// </summary>
    /// <remarks>
    /// The path is kept as written, except that empty segments (doubled, leading or trailing backslashes) are
    /// removed, which Windows ignores as well. Equality compares hive, view and path ignoring case; it does not
    /// resolve aliases such as <c>WOW6432Node</c> (see <see cref="RegistryPath.Canonicalize"/> for that).
    /// </remarks>
    public sealed class RegistryLocation : IEquatable<RegistryLocation>
    {
        private static readonly RegistryHive[] SupportedHives =
        {
            RegistryHive.CurrentUser, RegistryHive.LocalMachine, RegistryHive.Users, RegistryHive.ClassesRoot,
            RegistryHive.CurrentConfig
        };

        private static readonly Dictionary<string, Tuple<RegistryHive, RegistryView>> Prefixes =
            new Dictionary<string, Tuple<RegistryHive, RegistryView>>(StringComparer.OrdinalIgnoreCase)
            {
                { "HKCU", Tuple.Create(RegistryHive.CurrentUser, RegistryView.Default) },
                { "HKEY_CURRENT_USER", Tuple.Create(RegistryHive.CurrentUser, RegistryView.Default) },
                { "HKLM64", Tuple.Create(RegistryHive.LocalMachine, RegistryView.Registry64) },
                { "HKLM32", Tuple.Create(RegistryHive.LocalMachine, RegistryView.Registry32) },
                { "HKU", Tuple.Create(RegistryHive.Users, RegistryView.Default) },
                { "HKEY_USERS", Tuple.Create(RegistryHive.Users, RegistryView.Default) },
                { "HKCR", Tuple.Create(RegistryHive.ClassesRoot, RegistryView.Default) },
                { "HKEY_CLASSES_ROOT", Tuple.Create(RegistryHive.ClassesRoot, RegistryView.Default) },
                { "HKCC", Tuple.Create(RegistryHive.CurrentConfig, RegistryView.Default) },
                { "HKEY_CURRENT_CONFIG", Tuple.Create(RegistryHive.CurrentConfig, RegistryView.Default) },
            };

        /// <param name="hive">One of HKCU, HKLM, HKU, HKCR, HKCC.</param>
        /// <param name="view">For HKLM <see cref="RegistryView.Registry64"/> or <see cref="RegistryView.Registry32"/>;
        /// ignored for the other hives.</param>
        /// <param name="path">Path below the hive with <c>\</c> as separator; empty for the hive itself.</param>
        /// <exception cref="ArgumentException">HKLM without an explicit view.</exception>
        public RegistryLocation(RegistryHive hive, RegistryView view, string path)
        {
            if (Array.IndexOf(SupportedHives, hive) < 0)
                throw new ArgumentOutOfRangeException(nameof(hive), hive, "Unsupported registry hive.");
            if (view != RegistryView.Default && view != RegistryView.Registry32 && view != RegistryView.Registry64)
                throw new ArgumentOutOfRangeException(nameof(view), view, "Unknown registry view.");
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (hive == RegistryHive.LocalMachine && view == RegistryView.Default)
                throw new ArgumentException("HKLM keys are always opened with an explicit view (contract 0).", nameof(view));

            Hive = hive;
            View = hive == RegistryHive.LocalMachine ? view : RegistryView.Default;
            Path = string.Join(@"\", path.Split('\\').Where(segment => segment.Length > 0));
        }

        /// <summary>A key below HKCU.</summary>
        public static RegistryLocation CurrentUser(string path)
        {
            return new RegistryLocation(RegistryHive.CurrentUser, RegistryView.Default, path);
        }

        /// <summary>A key below the 64-bit view of HKLM (on 32-bit Windows the only view).</summary>
        public static RegistryLocation LocalMachine64(string path)
        {
            return new RegistryLocation(RegistryHive.LocalMachine, RegistryView.Registry64, path);
        }

        /// <summary>A key below the 32-bit view of HKLM (<c>WOW6432Node</c> on 64-bit Windows).</summary>
        public static RegistryLocation LocalMachine32(string path)
        {
            return new RegistryLocation(RegistryHive.LocalMachine, RegistryView.Registry32, path);
        }

        public RegistryHive Hive { get; }

        public RegistryView View { get; }

        /// <summary>Path below the hive, without leading, trailing or doubled backslashes; empty for the hive.</summary>
        public string Path { get; }

        /// <summary>True for the hive itself.</summary>
        public bool IsRoot
        {
            get { return Path.Length == 0; }
        }

        /// <summary>The names of the path, top first; empty for the hive.</summary>
        public IReadOnlyList<string> Segments
        {
            get { return IsRoot ? new string[0] : Path.Split('\\'); }
        }

        /// <summary>The key one level up, or null for the hive.</summary>
        public RegistryLocation Parent
        {
            get
            {
                if (IsRoot)
                    return null;
                int separator = Path.LastIndexOf('\\');
                return new RegistryLocation(Hive, View, separator < 0 ? string.Empty : Path.Substring(0, separator));
            }
        }

        /// <summary>The subkey <paramref name="relativePath"/> (one or more names separated by <c>\</c>).</summary>
        public RegistryLocation Child(string relativePath)
        {
            if (relativePath == null)
                throw new ArgumentNullException(nameof(relativePath));
            return new RegistryLocation(Hive, View, IsRoot ? relativePath : Path + @"\" + relativePath);
        }

        /// <summary>
        /// Reads a location written like <see cref="ToString"/>: <c>HKCU\...</c>, <c>HKLM64\...</c>, <c>HKLM32\...</c>,
        /// <c>HKU\...</c>, <c>HKCR\...</c>, <c>HKCC\...</c> or the long names (<c>HKEY_CURRENT_USER\...</c>). HKLM
        /// without a view is refused.
        /// </summary>
        public static bool TryParse(string text, out RegistryLocation location)
        {
            location = null;
            if (string.IsNullOrWhiteSpace(text))
                return false;
            string trimmed = text.Trim();
            int separator = trimmed.IndexOf('\\');
            string prefix = separator < 0 ? trimmed : trimmed.Substring(0, separator);
            if (!Prefixes.TryGetValue(prefix, out Tuple<RegistryHive, RegistryView> root))
                return false;
            location = new RegistryLocation(root.Item1, root.Item2, separator < 0 ? string.Empty : trimmed.Substring(separator + 1));
            return true;
        }

        /// <summary>Like <see cref="TryParse"/>, but throws <see cref="FormatException"/>.</summary>
        public static RegistryLocation Parse(string text)
        {
            if (!TryParse(text, out RegistryLocation location))
                throw new FormatException("Not a registry location with a known hive (and a view for HKLM): " + text);
            return location;
        }

        /// <summary><c>HKCU\Software\...</c>, <c>HKLM64\...</c>, <c>HKLM32\...</c>, <c>HKU\...</c>, ...</summary>
        public override string ToString()
        {
            string root;
            switch (Hive)
            {
                case RegistryHive.CurrentUser:
                    root = "HKCU";
                    break;
                case RegistryHive.LocalMachine:
                    root = View == RegistryView.Registry32 ? "HKLM32" : "HKLM64";
                    break;
                case RegistryHive.Users:
                    root = "HKU";
                    break;
                case RegistryHive.ClassesRoot:
                    root = "HKCR";
                    break;
                default:
                    root = "HKCC";
                    break;
            }
            return IsRoot ? root : root + @"\" + Path;
        }

        public bool Equals(RegistryLocation other)
        {
            return other != null && Hive == other.Hive && View == other.View &&
                   string.Equals(Path, other.Path, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as RegistryLocation);
        }

        public override int GetHashCode()
        {
            return ((int)Hive * 397) ^ ((int)View * 31) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(Path);
        }
    }
}
