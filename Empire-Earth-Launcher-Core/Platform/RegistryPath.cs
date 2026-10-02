using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>
    /// The canonical form of registry keys (ADR 0007, amendment): every spelling and alias of a key is mapped to
    /// one location, so that the write policy recognizes a protected key however it is written.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Canonicalize"/> applies, in this order:
    /// </para>
    /// <list type="number">
    /// <item>names are upper-cased with the invariant culture (the registry ignores case, independent of the
    /// user's culture), <c>/</c> is treated as <c>\</c>, and empty segments (doubled, leading or trailing
    /// backslashes) are dropped. A <c>/</c> is a valid character of a key name, so this merges more spellings than
    /// Windows does; for protection that is the safe direction;</item>
    /// <item><c>HKCU\Software\Classes\VirtualStore\MACHINE\&lt;rest&gt;</c>, where Windows keeps the HKLM writes of
    /// virtualized (non-elevated 32-bit legacy) programs, becomes <c>HKLM\&lt;rest&gt;</c> in the 64-bit view;</item>
    /// <item><c>WOW6432Node</c> segments directly after <c>Software</c> are removed and make an HKLM location one of
    /// the 32-bit view (the registry redirector maps <c>HKLM64\Software\WOW6432Node\X</c> to
    /// <c>HKLM32\Software\X</c>); in HKCU, which has no separate views, they are just removed.</item>
    /// </list>
    /// </remarks>
    public static class RegistryPath
    {
        private const string Software = "SOFTWARE";
        private const string Wow6432Node = "WOW6432NODE";

        private static readonly string[] VirtualStoreMachine = { Software, "CLASSES", "VIRTUALSTORE", "MACHINE" };

        /// <summary>The canonical location of <paramref name="location"/> (see the remarks of <see cref="RegistryPath"/>).</summary>
        public static RegistryLocation Canonicalize(RegistryLocation location)
        {
            if (location == null)
                throw new ArgumentNullException(nameof(location));

            List<string> segments = Spell(location.Path);
            RegistryHive hive = location.Hive;
            RegistryView view = location.View;

            if (hive == RegistryHive.CurrentUser && StartsWith(segments, VirtualStoreMachine))
            {
                hive = RegistryHive.LocalMachine;
                view = RegistryView.Registry64;
                segments.RemoveRange(0, VirtualStoreMachine.Length);
            }

            if (segments.Count >= 2 && segments[0] == Software && segments[1] == Wow6432Node)
            {
                while (segments.Count >= 2 && segments[1] == Wow6432Node)
                    segments.RemoveAt(1);
                if (hive == RegistryHive.LocalMachine)
                    view = RegistryView.Registry32;
            }

            return new RegistryLocation(hive, view, string.Join(@"\", segments));
        }

        /// <summary>True if both locations name the same key after <see cref="Canonicalize"/>.</summary>
        public static bool IsSameKey(RegistryLocation first, RegistryLocation second)
        {
            return Canonicalize(first).Equals(Canonicalize(second));
        }

        /// <summary>
        /// True if <paramref name="key"/> is <paramref name="ancestor"/> or below it, both in canonical form (same
        /// hive and view, path segments compared ignoring case).
        /// </summary>
        public static bool IsSameOrBelow(RegistryLocation key, RegistryLocation ancestor)
        {
            RegistryLocation canonicalKey = Canonicalize(key);
            RegistryLocation canonicalAncestor = Canonicalize(ancestor);
            if (canonicalKey.Hive != canonicalAncestor.Hive || canonicalKey.View != canonicalAncestor.View)
                return false;
            return StartsWith(canonicalKey.Segments, canonicalAncestor.Segments);
        }

        /// <summary>
        /// The names of <paramref name="path"/> in the canonical spelling: upper case (invariant), <c>/</c> as a
        /// separator, no empty names. No aliases are resolved.
        /// </summary>
        internal static List<string> Spell(string path)
        {
            return path.Replace('/', '\\')
                       .Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries)
                       .Select(segment => segment.ToUpperInvariant())
                       .ToList();
        }

        /// <summary>True if <paramref name="prefix"/> is the start of <paramref name="segments"/> (ignoring case).</summary>
        internal static bool StartsWith(IReadOnlyList<string> segments, IReadOnlyList<string> prefix)
        {
            if (prefix.Count > segments.Count)
                return false;
            for (int i = 0; i < prefix.Count; i++)
            {
                if (!string.Equals(segments[i], prefix[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }
    }
}
