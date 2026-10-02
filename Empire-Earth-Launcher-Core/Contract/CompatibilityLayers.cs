using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace Empire_Earth_Launcher.Core.Contract
{
    /// <summary>
    /// The entries of a compatibility value below <c>AppCompatFlags\Layers</c> (contract 3.7) that the launcher knows:
    /// which it may switch, which it may remove, and the old values it only shows (ADR 0007 plan review).
    /// </summary>
    /// <remarks>
    /// A value is <c>~</c> and entries separated by spaces, e.g. <c>~ DWM8And16BitMitigation HIGHDPIAWARE
    /// HeapClearAllocation WIN7RTM</c> (<see cref="CompatibilityLayerValue"/>). The launcher may add or remove only
    /// <see cref="LauncherEntries"/>, the rows <c>compatibility</c> and <c>compatibility_windows</c> of contract 3.7, and only
    /// from Windows 8 on; it never adds <see cref="RunAsAdmin"/> or a Windows version other than <c>WIN7RTM</c>, and keeps
    /// every other entry as it is. The registry write policy checks this on the content of every written value.
    /// </remarks>
    public static class CompatibilityLayers
    {
        /// <summary>The first entry of the values Windows and the setup write.</summary>
        public const string Prefix = "~";

        /// <summary>Run as administrator: opt-in through the setup only, never offered by the launcher (contract 3.7).</summary>
        public const string RunAsAdmin = "RUNASADMIN";

        /// <summary>Row <c>compatibility</c> of contract 3.7.</summary>
        public const string Dwm8And16BitMitigation = "DWM8And16BitMitigation";

        /// <summary>Row <c>compatibility</c> of contract 3.7; the game then sees physical pixels (O4).</summary>
        public const string HighDpiAware = "HIGHDPIAWARE";

        /// <summary>Row <c>compatibility</c> of contract 3.7.</summary>
        public const string HeapClearAllocation = "HeapClearAllocation";

        /// <summary>Row <c>compatibility_windows</c> of contract 3.7: the Windows 7 compatibility mode.</summary>
        public const string Windows7Mode = "WIN7RTM";

        /// <summary>
        /// The value setups up to 1.7.2 wrote into HKCU by default; the launcher may remove it if the value is exactly
        /// this (contract 3.7, like the setup's <c>RemoveLegacyRunAsAdmin</c>).
        /// </summary>
        public const string LegacyRunAsAdminValue = Prefix + " " + RunAsAdmin;

        /// <summary>
        /// The entries the launcher may add and remove, in the order of contract 3.7 (the order of the setup's values), on
        /// Windows 8 and later only.
        /// </summary>
        public static readonly IReadOnlyList<string> LauncherEntries = new ReadOnlyCollection<string>(new[]
        {
            Dwm8And16BitMitigation, HighDpiAware, HeapClearAllocation, Windows7Mode
        });

        /// <summary>
        /// The values earlier setups wrote on Windows Vista and 7 and that every setup run there removes (contract 3.7,
        /// the setup's <c>IsLegacyVistaCompatValue</c>). The launcher only shows them, read-only, and advises running the
        /// setup; removing them itself would extend the contract (ARCHITECTURE 14).
        /// </summary>
        public static readonly IReadOnlyList<string> LegacyVistaValues = new ReadOnlyCollection<string>(new[]
        {
            "~ WINXPSP3",
            "~ RUNASADMIN WINXPSP3",
            "~ DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation",
            "~ DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation WINXPSP3",
            "~ RUNASADMIN DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation",
            "~ RUNASADMIN DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation WINXPSP3",
        });

        /// <summary>
        /// The Windows version modes of the compatibility tab (Windows 95 to Windows 8, also the server and service pack
        /// variants): an entry such as <c>WIN95</c>, <c>WIN2000</c>, <c>VISTASP2</c>, <c>WIN7RTM</c> or <c>WIN8RTM</c>.
        /// </summary>
        private static readonly Regex WindowsVersionMode = new Regex(
            "^(?:WIN95|WIN98|WIN2000|NT4SP[0-9]|WINXP(?:SP[0-9])?|WINSRV[0-9]{2}(?:SP[0-9])?|VISTA(?:RTM|SP[0-9])|WIN[0-9]+RTM)$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        /// <summary>True if <paramref name="value"/> is exactly one of <see cref="LegacyVistaValues"/> (ordinal, like the setup).</summary>
        public static bool IsLegacyVistaValue(string value)
        {
            return value != null && LegacyVistaValues.Contains(value, StringComparer.Ordinal);
        }

        /// <summary>True if <paramref name="entry"/> is a Windows version mode (see <see cref="WindowsVersionMode"/>).</summary>
        public static bool IsWindowsVersionMode(string entry)
        {
            return entry != null && WindowsVersionMode.IsMatch(entry);
        }

        /// <summary>True if the launcher may add or remove <paramref name="entry"/> (ignoring case).</summary>
        public static bool IsLauncherEntry(string entry)
        {
            return entry != null && LauncherEntries.Contains(entry, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Whether changing a value from <paramref name="current"/> (null if it does not exist) to <paramref name="next"/>
        /// only adds or removes <paramref name="switchable"/> entries: every other entry is kept in the same order and
        /// spelling, and a <c>~</c> prefix is never removed (ADR 0007 plan review).
        /// </summary>
        public static bool IsAllowedChange(string current, string next, IEnumerable<string> switchable)
        {
            if (next == null)
                throw new ArgumentNullException(nameof(next));
            if (switchable == null)
                throw new ArgumentNullException(nameof(switchable));
            string[] allowed = switchable.ToArray();
            CompatibilityLayerValue before = CompatibilityLayerValue.Parse(current ?? string.Empty);
            CompatibilityLayerValue after = CompatibilityLayerValue.Parse(next);
            if (before.HasPrefix && !after.HasPrefix)
                return false;
            return Others(before, allowed).SequenceEqual(Others(after, allowed), StringComparer.Ordinal);
        }

        /// <summary>
        /// Whether the value <paramref name="current"/> may be deleted: it does not exist, it is exactly
        /// <see cref="LegacyRunAsAdminValue"/>, or it holds no entry other than <paramref name="switchable"/> ones.
        /// </summary>
        public static bool IsAllowedDeletion(string current, IEnumerable<string> switchable)
        {
            if (switchable == null)
                throw new ArgumentNullException(nameof(switchable));
            if (current == null || string.Equals(current, LegacyRunAsAdminValue, StringComparison.Ordinal))
                return true;
            return !Others(CompatibilityLayerValue.Parse(current), switchable.ToArray()).Any();
        }

        private static IEnumerable<string> Others(CompatibilityLayerValue value, string[] switchable)
        {
            return value.Entries.Where(entry => !switchable.Contains(entry, StringComparer.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// A compatibility value as entries: an optional leading <c>~</c> and the entries separated by white space. Immutable.
    /// </summary>
    public sealed class CompatibilityLayerValue
    {
        private readonly ReadOnlyCollection<string> entries;

        private CompatibilityLayerValue(bool hasPrefix, IList<string> entries)
        {
            HasPrefix = hasPrefix;
            this.entries = new ReadOnlyCollection<string>(entries);
        }

        /// <summary>True if the first entry is <c>~</c>.</summary>
        public bool HasPrefix { get; }

        /// <summary>The entries after the prefix, in their order and spelling.</summary>
        public IReadOnlyList<string> Entries
        {
            get { return entries; }
        }

        /// <summary>True if there is no entry (an empty value or just <c>~</c>).</summary>
        public bool IsEmpty
        {
            get { return entries.Count == 0; }
        }

        /// <summary>Reads a value; null and white space give an empty value.</summary>
        public static CompatibilityLayerValue Parse(string value)
        {
            string[] tokens = (value ?? string.Empty).Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            bool prefix = tokens.Length > 0 && tokens[0] == CompatibilityLayers.Prefix;
            return new CompatibilityLayerValue(prefix, tokens.Skip(prefix ? 1 : 0).ToList());
        }

        /// <summary>True if the value contains <paramref name="entry"/> (ignoring case).</summary>
        public bool Contains(string entry)
        {
            return entries.Contains(entry, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The value with <paramref name="entry"/>, one of <see cref="CompatibilityLayers.LauncherEntries"/>, inserted at the
        /// place of its row in contract 3.7 (before the first entry of the launcher that comes later in the table); every
        /// other entry stays. A new value gets the prefix <c>~</c>.
        /// </summary>
        public CompatibilityLayerValue With(string entry)
        {
            int rank = Rank(entry);
            if (rank < 0)
                throw new ArgumentException("The launcher only adds the entries of contract 3.7: " + entry, nameof(entry));
            if (Contains(entry))
                return this;
            var list = entries.ToList();
            int position = list.FindIndex(existing => Rank(existing) > rank);
            if (position < 0)
            {
                int lastLower = list.FindLastIndex(existing => Rank(existing) >= 0);
                position = lastLower < 0 ? list.Count : lastLower + 1;
            }
            list.Insert(position, CompatibilityLayers.LauncherEntries[rank]);
            return new CompatibilityLayerValue(HasPrefix || IsEmpty, list);
        }

        /// <summary>The value without <paramref name="entry"/> (ignoring case); every other entry stays.</summary>
        public CompatibilityLayerValue Without(string entry)
        {
            return new CompatibilityLayerValue(HasPrefix,
                entries.Where(existing => !string.Equals(existing, entry, StringComparison.OrdinalIgnoreCase)).ToList());
        }

        /// <summary><c>~ A B</c>: the prefix and the entries separated by one space.</summary>
        public override string ToString()
        {
            return string.Join(" ", (HasPrefix ? new[] { CompatibilityLayers.Prefix } : new string[0]).Concat(entries));
        }

        private static int Rank(string entry)
        {
            for (int i = 0; i < CompatibilityLayers.LauncherEntries.Count; i++)
            {
                if (string.Equals(CompatibilityLayers.LauncherEntries[i], entry, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }
    }
}
