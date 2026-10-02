using System;
using System.Collections.Generic;
using System.Linq;
using Empire_Earth_Launcher.Core.Platform;
using Microsoft.Win32;

namespace Empire_Earth_Launcher.Tests.Fakes
{
    /// <summary>
    /// <see cref="IRegistry"/> in memory with the views of Windows (ADR 0006, contract 0): HKLM has a 32-bit and a
    /// 64-bit view, every other hive is the same in both views; key and value names are compared ignoring case and
    /// keep the spelling they were created with.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>64-bit Windows</b> (default): HKLM64 and HKLM32 are separate trees, and <c>Software\WOW6432Node\...</c> in
    /// either view is the key <c>Software\...</c> of the 32-bit view, as on Windows (registry redirector).
    /// <b>32-bit Windows</b> (<see cref="Is32BitWindows"/>): there is one HKLM; both views name it, and
    /// <c>WOW6432Node</c> is an ordinary name.
    /// </para>
    /// <para>
    /// Tests can make every operation on a key and its subkeys fail (<see cref="SetFault"/>), and see every change
    /// that went through the <see cref="IRegistry"/> methods (<see cref="Changes"/>). <see cref="Seed"/> sets up
    /// data without faults and without being recorded.
    /// </para>
    /// </remarks>
    internal sealed class InMemoryRegistry : IRegistry
    {
        private sealed class Key
        {
            public Key(string name)
            {
                Name = name;
            }

            public string Name { get; }

            public Dictionary<string, Key> SubKeys { get; } = new Dictionary<string, Key>(StringComparer.OrdinalIgnoreCase);

            public Dictionary<string, Tuple<string, RegistryValue>> Values { get; } =
                new Dictionary<string, Tuple<string, RegistryValue>>(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class Fault
        {
            public Key Tree;
            public string[] Segments;
            public RegistryStatus Status;
            public bool WritesOnly;
        }

        private readonly Key currentUser = new Key("HKCU");
        private readonly Key localMachine64 = new Key("HKLM64");
        private readonly Key localMachine32;
        private readonly Dictionary<RegistryHive, Key> otherHives = new Dictionary<RegistryHive, Key>();
        private readonly List<Fault> faults = new List<Fault>();
        private readonly List<string> changes = new List<string>();

        /// <param name="is32BitWindows">true: one HKLM for both views, as on 32-bit Windows.</param>
        public InMemoryRegistry(bool is32BitWindows = false)
        {
            Is32BitWindows = is32BitWindows;
            localMachine32 = is32BitWindows ? localMachine64 : new Key("HKLM32");
        }

        /// <summary>True if both views of HKLM are one tree.</summary>
        public bool Is32BitWindows { get; }

        /// <summary>Every successful change through <see cref="IRegistry"/>, e.g. <c>SetValue HKCU\Software\X @"Name"</c>.</summary>
        public IReadOnlyList<string> Changes
        {
            get { return changes.ToList(); }
        }

        /// <summary>Creates the key (and its parents) and sets the value; for test setup.</summary>
        public void Seed(RegistryLocation key, string valueName, RegistryValue value)
        {
            Key created = SeedKeyNode(key);
            created.Values[valueName] = Tuple.Create(valueName, value);
        }

        /// <summary>Creates the key and its parents; for test setup.</summary>
        public void SeedKey(RegistryLocation key)
        {
            SeedKeyNode(key);
        }

        /// <summary>
        /// From now on every operation on <paramref name="key"/> and its subkeys (also through an alias that names
        /// the same key) fails with <paramref name="status"/>; with <paramref name="writesOnly"/> only changes do.
        /// </summary>
        public void SetFault(RegistryLocation key, RegistryStatus status, bool writesOnly = false)
        {
            if (status == RegistryStatus.Ok)
                throw new ArgumentException("A fault needs a failure status.", nameof(status));
            Resolve(key, out Key tree, out string[] segments);
            faults.Add(new Fault { Tree = tree, Segments = segments, Status = status, WritesOnly = writesOnly });
        }

        public RegistryResult ProbeKey(RegistryLocation key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            RegistryResult fault = CheckFault(key, false);
            if (!fault.IsOk)
                return fault;
            return Find(key) == null ? Missing(key) : RegistryResult.Success;
        }

        public RegistryResult<RegistryValue> GetValue(RegistryLocation key, string valueName)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (valueName == null)
                throw new ArgumentNullException(nameof(valueName));
            RegistryResult fault = CheckFault(key, false);
            if (!fault.IsOk)
                return RegistryResult<RegistryValue>.Failure(fault.Status, fault.Detail);
            Key found = Find(key);
            if (found == null)
                return RegistryResult<RegistryValue>.Failure(RegistryStatus.Missing, "The key does not exist: " + key);
            return found.Values.TryGetValue(valueName, out Tuple<string, RegistryValue> value)
                ? RegistryResult<RegistryValue>.Success(value.Item2)
                : RegistryResult<RegistryValue>.Failure(RegistryStatus.Missing, "The value \"" + valueName + "\" does not exist in " + key);
        }

        public RegistryResult<IReadOnlyList<string>> GetValueNames(RegistryLocation key)
        {
            return List(key, found => found.Values.Values.Select(value => value.Item1));
        }

        public RegistryResult<IReadOnlyList<string>> GetSubKeyNames(RegistryLocation key)
        {
            return List(key, found => found.SubKeys.Values.Select(subKey => subKey.Name));
        }

        public RegistryResult CreateSubKey(RegistryLocation key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (key.IsRoot)
                throw new ArgumentException("A hive cannot be created.", nameof(key));
            RegistryResult fault = CheckFault(key, true);
            if (!fault.IsOk)
                return fault;
            if (Find(key) == null)
            {
                SeedKeyNode(key);
                changes.Add("CreateSubKey " + key);
            }
            return RegistryResult.Success;
        }

        public RegistryResult SetValue(RegistryLocation key, string valueName, RegistryValue value)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (valueName == null)
                throw new ArgumentNullException(nameof(valueName));
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            RegistryResult fault = CheckFault(key, true);
            if (!fault.IsOk)
                return fault;
            Key found = Find(key);
            if (found == null)
                return Missing(key);
            string name = found.Values.TryGetValue(valueName, out Tuple<string, RegistryValue> existing) ? existing.Item1 : valueName;
            found.Values[valueName] = Tuple.Create(name, value);
            changes.Add("SetValue " + key + " @\"" + valueName + "\" = " + value);
            return RegistryResult.Success;
        }

        public RegistryResult DeleteValue(RegistryLocation key, string valueName)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (valueName == null)
                throw new ArgumentNullException(nameof(valueName));
            RegistryResult fault = CheckFault(key, true);
            if (!fault.IsOk)
                return fault;
            Key found = Find(key);
            if (found == null || !found.Values.Remove(valueName))
                return RegistryResult.Failure(RegistryStatus.Missing, "The value \"" + valueName + "\" does not exist in " + key);
            changes.Add("DeleteValue " + key + " @\"" + valueName + "\"");
            return RegistryResult.Success;
        }

        public RegistryResult DeleteSubKeyTree(RegistryLocation key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (key.IsRoot)
                throw new ArgumentException("A hive cannot be deleted.", nameof(key));
            RegistryResult fault = CheckFault(key, true);
            if (!fault.IsOk)
                return fault;
            Resolve(key, out Key tree, out string[] segments);
            Key parent = Walk(tree, segments.Take(segments.Length - 1));
            if (parent == null || !parent.SubKeys.Remove(segments[segments.Length - 1]))
                return Missing(key);
            changes.Add("DeleteSubKeyTree " + key);
            return RegistryResult.Success;
        }

        private RegistryResult<IReadOnlyList<string>> List(RegistryLocation key, Func<Key, IEnumerable<string>> names)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            RegistryResult fault = CheckFault(key, false);
            if (!fault.IsOk)
                return RegistryResult<IReadOnlyList<string>>.Failure(fault.Status, fault.Detail);
            Key found = Find(key);
            if (found == null)
                return RegistryResult<IReadOnlyList<string>>.Failure(RegistryStatus.Missing, "The key does not exist: " + key);
            IReadOnlyList<string> sorted = names(found).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
            return RegistryResult<IReadOnlyList<string>>.Success(sorted);
        }

        private Key Find(RegistryLocation location)
        {
            Resolve(location, out Key tree, out string[] segments);
            return Walk(tree, segments);
        }

        private static Key Walk(Key tree, IEnumerable<string> segments)
        {
            Key current = tree;
            foreach (string segment in segments)
            {
                if (!current.SubKeys.TryGetValue(segment, out current))
                    return null;
            }
            return current;
        }

        private Key SeedKeyNode(RegistryLocation location)
        {
            Resolve(location, out Key current, out string[] segments);
            foreach (string segment in segments)
            {
                if (!current.SubKeys.TryGetValue(segment, out Key next))
                {
                    next = new Key(segment);
                    current.SubKeys.Add(segment, next);
                }
                current = next;
            }
            return current;
        }

        /// <summary>The tree and path the location names, after the redirection of <c>WOW6432Node</c>.</summary>
        private void Resolve(RegistryLocation location, out Key tree, out string[] segments)
        {
            var path = location.Segments.ToList();
            switch (location.Hive)
            {
                case RegistryHive.CurrentUser:
                    tree = currentUser;
                    break;
                case RegistryHive.LocalMachine:
                    tree = location.View == RegistryView.Registry32 ? localMachine32 : localMachine64;
                    if (!Is32BitWindows)
                    {
                        bool redirected = false;
                        while (path.Count >= 2 && IsName(path[0], "Software") && IsName(path[1], "WOW6432Node"))
                        {
                            path.RemoveAt(1);
                            redirected = true;
                        }
                        if (redirected)
                            tree = localMachine32;
                    }
                    break;
                default:
                    if (!otherHives.TryGetValue(location.Hive, out tree))
                    {
                        tree = new Key(location.Hive.ToString());
                        otherHives.Add(location.Hive, tree);
                    }
                    break;
            }
            segments = path.ToArray();
        }

        private RegistryResult CheckFault(RegistryLocation location, bool isWrite)
        {
            Resolve(location, out Key tree, out string[] segments);
            foreach (Fault fault in faults)
            {
                if (fault.Tree != tree || (fault.WritesOnly && !isWrite) || fault.Segments.Length > segments.Length)
                    continue;
                if (fault.Segments.Select((segment, i) => IsName(segment, segments[i])).All(same => same))
                    return RegistryResult.Failure(fault.Status, "Injected fault for " + location);
            }
            return RegistryResult.Success;
        }

        private static bool IsName(string first, string second)
        {
            return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
        }

        private static RegistryResult Missing(RegistryLocation key)
        {
            return RegistryResult.Failure(RegistryStatus.Missing, "The key does not exist: " + key);
        }
    }
}
