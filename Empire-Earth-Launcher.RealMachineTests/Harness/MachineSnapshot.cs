using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.RealMachineTests.Harness
{
    /// <summary>
    /// What the launcher core must leave alone, read before and after the checks of a step: the CD keys
    /// (<c>Software\Sierra\CDKeys</c>, every view; briefing D6), the install records, the uninstall keys of Inno Setup
    /// installations (<c>*_is1</c>), the compatibility layers, the game settings keys in HKLM, and the files and folders below
    /// the roots of the expectation (size, time, attributes). Values are compared in memory; a difference names the key, value
    /// or file, never the data, and of the CD keys not even a value name.
    /// </summary>
    /// <remarks>
    /// Only the uninstall keys whose name ends with <c>_is1</c> are compared: other programs of the runner image (browsers with
    /// their own updaters) may change theirs while a step runs, and the community setups are Inno Setup installations.
    /// </remarks>
    internal sealed class MachineSnapshot
    {
        private const int MaxDepth = 32;
        private const int MaxLinesPerArea = 25;

        /// <summary>The suffix of the uninstall keys of Inno Setup installations.</summary>
        internal const string InnoSetupKeySuffix = "_is1";

        /// <summary>The registry trees, each walked completely (a symbolic link is noted, never followed).</summary>
        internal static readonly IReadOnlyList<ProtectedTree> ProtectedTrees = BuildTrees();

        private readonly Dictionary<string, Area> areas;

        private MachineSnapshot(Dictionary<string, Area> areas)
        {
            this.areas = areas;
        }

        /// <summary>One registry tree or one folder: its entries, relative name to a fingerprint of the content.</summary>
        private sealed class Area
        {
            public Area(string name, bool sensitive)
            {
                Name = name;
                Sensitive = sensitive;
            }

            public string Name { get; }

            /// <summary>True for the CD keys: a difference is reported without any name.</summary>
            public bool Sensitive { get; }

            public SortedDictionary<string, string> Entries { get; } = new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>A registry tree of the snapshot.</summary>
        internal sealed class ProtectedTree
        {
            public ProtectedTree(RegistryLocation location, bool sensitive = false, Func<string, bool> includesSubKey = null)
            {
                Location = location;
                Sensitive = sensitive;
                IncludesSubKey = includesSubKey ?? (name => true);
            }

            public RegistryLocation Location { get; }

            /// <summary>True for the CD keys: a difference is reported without any name.</summary>
            public bool Sensitive { get; }

            /// <summary>Which subkeys directly below <see cref="Location"/> are compared.</summary>
            public Func<string, bool> IncludesSubKey { get; }
        }

        private static IReadOnlyList<ProtectedTree> BuildTrees()
        {
            var trees = new List<ProtectedTree>();
            foreach (Func<string, RegistryLocation> view in new Func<string, RegistryLocation>[]
                         { RegistryLocation.CurrentUser, RegistryLocation.LocalMachine64, RegistryLocation.LocalMachine32 })
            {
                trees.Add(new ProtectedTree(view(ContractNames.CdKeysKey), true));
                trees.Add(new ProtectedTree(view(ContractNames.InstallRecordsKey)));
                trees.Add(new ProtectedTree(view(ContractNames.UninstallKey), false,
                    name => name.EndsWith(InnoSetupKeySuffix, StringComparison.OrdinalIgnoreCase)));
            }
            trees.Add(new ProtectedTree(RegistryLocation.CurrentUser(ContractNames.CompatibilityLayersKey)));
            trees.Add(new ProtectedTree(RegistryLocation.LocalMachine64(ContractNames.CompatibilityLayersKey)));
            foreach (Func<string, RegistryLocation> view in new Func<string, RegistryLocation>[]
                         { RegistryLocation.LocalMachine64, RegistryLocation.LocalMachine32 })
            {
                foreach (string key in Product.All.SelectMany(product => Game.All.Select(product.GetGameSettingsKey)).Distinct())
                    trees.Add(new ProtectedTree(view(key)));
            }
            return trees;
        }

        /// <summary>Reads the protected trees of <paramref name="registry"/> and the folders <paramref name="roots"/>.</summary>
        public static MachineSnapshot Take(IRegistry registry, IFileSystem fileSystem, IEnumerable<string> roots)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));
            if (fileSystem == null)
                throw new ArgumentNullException(nameof(fileSystem));
            if (roots == null)
                throw new ArgumentNullException(nameof(roots));
            var areas = new Dictionary<string, Area>(StringComparer.OrdinalIgnoreCase);
            foreach (ProtectedTree tree in ProtectedTrees)
            {
                var area = new Area(tree.Location.ToString(), tree.Sensitive);
                WalkKey(registry, tree.Location, string.Empty, area, 0, tree.IncludesSubKey);
                areas[area.Name] = area;
            }
            foreach (string root in roots.Distinct(WinPath.Comparer))
            {
                var area = new Area(WinPath.Normalize(root), false);
                if (fileSystem.DirectoryExists(root))
                {
                    area.Entries[string.Empty] = "folder";
                    WalkFolder(fileSystem, area.Name, string.Empty, area, 0);
                }
                else
                    area.Entries[string.Empty] = fileSystem.FileExists(root) ? "a file" : "absent";
                areas[area.Name] = area;
            }
            return new MachineSnapshot(areas);
        }

        /// <summary>The number of entries read (keys, values, folders, files), for the log.</summary>
        public int Count
        {
            get { return areas.Values.Sum(area => area.Entries.Count); }
        }

        /// <summary>What differs in <paramref name="after"/>, one line per entry (at most 25 per area), never a value.</summary>
        public IReadOnlyList<string> DifferencesTo(MachineSnapshot after)
        {
            if (after == null)
                throw new ArgumentNullException(nameof(after));
            var lines = new List<string>();
            foreach (Area before in areas.Values.OrderBy(area => area.Name, StringComparer.OrdinalIgnoreCase))
            {
                after.areas.TryGetValue(before.Name, out Area now);
                SortedDictionary<string, string> nowEntries = now?.Entries ?? new SortedDictionary<string, string>();
                List<string> changed = before.Entries.Keys.Union(nowEntries.Keys, StringComparer.OrdinalIgnoreCase)
                                             .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                                             .Where(name => !Equals(Get(before.Entries, name), Get(nowEntries, name)))
                                             .ToList();
                if (changed.Count == 0)
                    continue;
                if (before.Sensitive)
                {
                    lines.Add(before.Name + ": changed (" + changed.Count.ToString(CultureInfo.InvariantCulture) +
                              " entries; names and values are not shown)");
                    continue;
                }
                foreach (string name in changed.Take(MaxLinesPerArea))
                {
                    string old = Get(before.Entries, name);
                    string current = Get(nowEntries, name);
                    string what = old == null ? "added" : current == null ? "removed" : "changed";
                    lines.Add(before.Name + (name.Length == 0 ? string.Empty : @"\" + name) + ": " + what);
                }
                if (changed.Count > MaxLinesPerArea)
                    lines.Add(before.Name + ": and " + (changed.Count - MaxLinesPerArea).ToString(CultureInfo.InvariantCulture) + " more");
            }
            return lines;
        }

        private static string Get(IDictionary<string, string> entries, string name)
        {
            return entries.TryGetValue(name, out string value) ? value : null;
        }

        private static void WalkKey(IRegistry registry, RegistryLocation key, string relative, Area area, int depth,
            Func<string, bool> includesSubKey = null)
        {
            // Entries of keys end with "\", entries of values are "<key>\@<name>".
            string keyEntry = relative.Length == 0 ? string.Empty : relative + @"\";
            RegistryResult probe = registry.ProbeKey(key);
            if (!probe.IsOk)
            {
                area.Entries[keyEntry] = probe.Status == RegistryStatus.Missing ? "absent" : "unreadable " + probe.Status;
                return;
            }
            RegistryResult<bool> link = registry.IsLink(key);
            if (link.IsOk && link.Value)
            {
                area.Entries[keyEntry] = "link";
                return;
            }
            area.Entries[keyEntry] = "key";
            if (depth >= MaxDepth)
                return;

            RegistryResult<IReadOnlyList<string>> names = registry.GetValueNames(key);
            if (!names.IsOk)
                area.Entries[keyEntry + "@"] = "values unreadable " + names.Status;
            else
            {
                foreach (string name in names.Value)
                {
                    RegistryResult<RegistryValue> value = registry.GetValue(key, name);
                    // The whole value (type and data) in memory only; DifferencesTo never prints it.
                    area.Entries[keyEntry + "@" + name] = value.IsOk ? value.Value.ToString() : "unreadable " + value.Status;
                }
            }

            RegistryResult<IReadOnlyList<string>> subKeys = registry.GetSubKeyNames(key);
            if (!subKeys.IsOk)
            {
                area.Entries[keyEntry + "*"] = "subkeys unreadable " + subKeys.Status;
                return;
            }
            foreach (string name in subKeys.Value.Where(includesSubKey ?? (subKey => true)))
                WalkKey(registry, key.Child(name), keyEntry + name, area, depth + 1);
        }

        private static void WalkFolder(IFileSystem fileSystem, string folder, string relative, Area area, int depth)
        {
            string prefix = relative.Length == 0 ? string.Empty : relative + @"\";
            FileSystemResult<IReadOnlyList<string>> files = fileSystem.GetFiles(folder);
            if (!files.IsOk)
                area.Entries[prefix + "*"] = "files unreadable " + files.Status;
            else
            {
                foreach (string file in files.Value)
                {
                    FileSystemResult<FileEntry> info = fileSystem.GetFileInfo(file);
                    area.Entries[prefix + WinPath.GetFileName(file)] = info.IsOk
                        ? info.Value.Length.ToString(CultureInfo.InvariantCulture) + " " +
                          info.Value.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture) + " " + (int)info.Value.Attributes
                        : "unreadable " + info.Status;
                }
            }

            FileSystemResult<IReadOnlyList<string>> folders = fileSystem.GetDirectories(folder);
            if (!folders.IsOk)
            {
                area.Entries[prefix + "\\"] = "folders unreadable " + folders.Status;
                return;
            }
            foreach (string child in folders.Value)
            {
                string name = prefix + WinPath.GetFileName(child);
                area.Entries[name + @"\"] = "folder";
                if (depth < MaxDepth)
                    WalkFolder(fileSystem, child, name, area, depth + 1);
            }
        }
    }
}
