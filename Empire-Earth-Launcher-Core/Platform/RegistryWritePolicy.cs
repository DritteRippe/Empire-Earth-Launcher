using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Microsoft.Win32;

namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>A change of the registry that the write policy decides about.</summary>
    public enum RegistryOperation
    {
        SetValue,
        DeleteValue,
        CreateSubKey,
        DeleteSubKeyTree
    }

    /// <summary>Why <see cref="RegistryWritePolicy"/> refused a change; <see cref="None"/> if it allowed it.</summary>
    public enum RegistryWriteDenial
    {
        /// <summary>Allowed.</summary>
        None,

        /// <summary>
        /// <c>Software\Sierra\CDKeys</c>, a key below it or an ancestor of it, in any hive, view or alias (registry
        /// VirtualStore included): the NeoEE CD keys (contract 3.8, briefing D6).
        /// </summary>
        CdKeys,

        /// <summary>The registry record of the setup, a key below it or an ancestor of it (contract 1.1, read-only).</summary>
        InstallRecord,

        /// <summary>The uninstall keys, a key below them or an ancestor of them (contract 1.3, read-only).</summary>
        UninstallKey,

        /// <summary>Not HKCU: the launcher never writes HKLM or another account's hive (contract 3.6).</summary>
        NotCurrentUser,

        /// <summary>Not a key and operation of the allow-list.</summary>
        NotInAllowList,

        /// <summary>
        /// A key of the allow-list, but not a value name of its rule: not a value of the contract tables (3.2, 3.5), not the
        /// full path of a game program (3.4, 3.7).
        /// </summary>
        ValueNotAllowed,

        /// <summary>
        /// A compatibility value whose content would change other than by the entries the launcher may switch on this
        /// Windows (contract 3.7, ADR 0007 plan review): e.g. a Windows version mode or <c>RUNASADMIN</c> added, another
        /// entry removed, or the current value unknown.
        /// </summary>
        LayerContent
    }

    /// <summary>
    /// One entry of the allow-list: an exact HKCU key, the operations allowed on it and, for value operations, the value
    /// names (any, a list, or the full paths of the game programs) and for compatibility values a check of the content.
    /// </summary>
    public sealed class RegistryWriteRule
    {
        private readonly ReadOnlyCollection<string> valueNames;
        private readonly ReadOnlyCollection<string> layerEntries;

        /// <summary>A rule for any value name of the key (the cleanup lists and the tests use it).</summary>
        /// <param name="currentUserKey">Path below HKCU, spelled as the key is written (case does not matter).</param>
        /// <param name="operations">The allowed operations on that key (not on its subkeys).</param>
        public RegistryWriteRule(string currentUserKey, params RegistryOperation[] operations)
            : this(currentUserKey, null, false, null, operations)
        {
        }

        /// <summary>A rule for the values <paramref name="valueNames"/> of the key (compared ignoring case).</summary>
        public RegistryWriteRule(string currentUserKey, IEnumerable<string> valueNames, params RegistryOperation[] operations)
            : this(currentUserKey, (valueNames ?? throw new ArgumentNullException(nameof(valueNames))).ToList(), false, null,
                operations)
        {
        }

        private RegistryWriteRule(string currentUserKey, IList<string> valueNames, bool programPaths, IList<string> layerEntries,
            RegistryOperation[] operations)
        {
            if (currentUserKey == null)
                throw new ArgumentNullException(nameof(currentUserKey));
            if (operations == null || operations.Length == 0)
                throw new ArgumentException("A rule needs at least one operation.", nameof(operations));
            Key = RegistryLocation.CurrentUser(currentUserKey);
            if (Key.IsRoot)
                throw new ArgumentException("HKCU itself can never be changed.", nameof(currentUserKey));
            Operations = new ReadOnlyCollection<RegistryOperation>(operations.Distinct().ToList());
            this.valueNames = valueNames == null ? null : new ReadOnlyCollection<string>(valueNames);
            OnlyProgramPaths = programPaths;
            this.layerEntries = layerEntries == null ? null : new ReadOnlyCollection<string>(layerEntries);
        }

        /// <summary>A rule whose value names are full paths of a game program (contract 3.4: the GPU preference).</summary>
        public static RegistryWriteRule ForProgramPaths(string currentUserKey, params RegistryOperation[] operations)
        {
            return new RegistryWriteRule(currentUserKey, null, true, null, operations);
        }

        /// <summary>
        /// A rule for compatibility values (contract 3.7): the value names are full paths of a game program, a written value
        /// may differ from the current one only by <paramref name="switchableEntries"/>, and a value may be deleted only if
        /// it holds nothing else or is exactly <c>~ RUNASADMIN</c> (<see cref="CompatibilityLayers"/>).
        /// </summary>
        public static RegistryWriteRule ForCompatibilityLayers(string currentUserKey, IEnumerable<string> switchableEntries,
            params RegistryOperation[] operations)
        {
            if (switchableEntries == null)
                throw new ArgumentNullException(nameof(switchableEntries));
            return new RegistryWriteRule(currentUserKey, null, true, switchableEntries.ToList(), operations);
        }

        /// <summary>The HKCU key.</summary>
        public RegistryLocation Key { get; }

        public IReadOnlyList<RegistryOperation> Operations { get; }

        /// <summary>The value names of the rule; null if any name (or <see cref="OnlyProgramPaths"/>) is allowed.</summary>
        public IReadOnlyList<string> ValueNames
        {
            get { return valueNames; }
        }

        /// <summary>True if the value names must be full paths of a game program (<see cref="IsProgramPath"/>).</summary>
        public bool OnlyProgramPaths { get; }

        /// <summary>
        /// The compatibility entries a written value may add or remove; null if the rule does not check the content.
        /// </summary>
        public IReadOnlyList<string> LayerEntries
        {
            get { return layerEntries; }
        }

        /// <summary>
        /// True for the full path of a game program in normal form (<see cref="WinPath"/>): <c>Empire Earth.exe</c> or
        /// <c>EE-AOC.exe</c> in a folder with a drive or on a share, as the setup writes the value names of contract 3.4
        /// and 3.7. Which installation it belongs to is up to the caller (only discovered installations).
        /// </summary>
        public static bool IsProgramPath(string valueName)
        {
            if (string.IsNullOrWhiteSpace(valueName) || !WinPath.IsFullyQualified(valueName) ||
                !string.Equals(WinPath.Normalize(valueName), valueName, StringComparison.Ordinal))
                return false;
            string fileName = WinPath.GetFileName(valueName);
            return Game.All.Any(game => string.Equals(game.ProgramName, fileName, StringComparison.OrdinalIgnoreCase));
        }

        internal bool Matches(RegistryOperation operation, RegistryLocation key)
        {
            return Operations.Contains(operation) && Key.Equals(key);
        }

        internal bool AllowsValueName(string valueName)
        {
            if (valueName == null)
                return true; // key operations
            if (OnlyProgramPaths)
                return IsProgramPath(valueName);
            return valueNames == null || valueNames.Contains(valueName, StringComparer.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            string names = OnlyProgramPaths ? "program paths" : valueNames == null ? "any value" : valueNames.Count + " values";
            return Key + " (" + string.Join(", ", Operations) + "; " + names +
                   (layerEntries == null ? string.Empty : "; layers " + string.Join(" ", layerEntries)) + ")";
        }
    }

    /// <summary>The decision of <see cref="RegistryWritePolicy.Check"/>.</summary>
    public sealed class RegistryWriteDecision
    {
        internal RegistryWriteDecision(RegistryOperation operation, RegistryLocation key, string valueName,
            RegistryLocation canonicalKey, RegistryWriteDenial denial)
        {
            Operation = operation;
            Key = key;
            ValueName = valueName;
            CanonicalKey = canonicalKey;
            Denial = denial;
        }

        public RegistryOperation Operation { get; }

        /// <summary>The key as the caller named it.</summary>
        public RegistryLocation Key { get; }

        /// <summary>The value of <see cref="RegistryOperation.SetValue"/>/<see cref="RegistryOperation.DeleteValue"/>, else null.</summary>
        public string ValueName { get; }

        /// <summary>The key in canonical form (<see cref="RegistryPath.Canonicalize"/>).</summary>
        public RegistryLocation CanonicalKey { get; }

        public RegistryWriteDenial Denial { get; }

        public bool IsAllowed
        {
            get { return Denial == RegistryWriteDenial.None; }
        }

        public override string ToString()
        {
            string target = Key + (ValueName == null ? string.Empty : " @\"" + ValueName + "\"");
            return Operation + " " + target + (IsAllowed
                ? " allowed"
                : " refused: " + Denial + " (canonical " + CanonicalKey + ")");
        }
    }

    /// <summary>
    /// What the launcher may change in the registry (ADR 0007): first every protected key is refused after the
    /// key was put into its canonical form, then only HKCU keys of the allow-list are allowed ("deny after
    /// canonicalization, then allow").
    /// </summary>
    /// <remarks>
    /// <para>
    /// Refused, for every operation, in every hive and view and under every alias (<see cref="RegistryPath"/>):
    /// the whole subtree of <c>Software\Sierra\CDKeys</c> and every ancestor of it, including the registry
    /// VirtualStore folder that holds virtualized copies of HKLM (<c>HKCU\Software\Classes\VirtualStore</c>) and
    /// its ancestors; the registry records (<c>Software\Empire Earth Community\Installations</c>) and the
    /// uninstall keys (<c>Software\Microsoft\Windows\CurrentVersion\Uninstall</c>), each with its subtree and its
    /// ancestors. These rules do not depend on the allow-list: a protected key stays refused even if an allow-list
    /// names it.
    /// </para>
    /// <para>
    /// Then the key itself must be in HKCU and be named exactly (ignoring case) by a rule of the allow-list for that
    /// operation. The allow-list is matched against the key as it will be written, not against its canonical
    /// form, so an alias (<c>WOW6432Node</c>, <c>/</c>, a VirtualStore copy) never reaches a listed key unless the
    /// list names that alias itself.
    /// </para>
    /// <para>
    /// For value operations a matching rule must also allow the value name (<see cref="RegistryWriteDenial.ValueNotAllowed"/>),
    /// and a rule for compatibility values checks the content of the written value against the current one
    /// (<see cref="RegistryWriteDenial.LayerContent"/>). The launcher's allow-list, narrowed to the value names of the
    /// contract tables, the program paths and the layer content, is <c>GameSettings.LauncherWritePolicy</c> (ADR 0007).
    /// </para>
    /// </remarks>
    public sealed class RegistryWritePolicy
    {
        /// <summary>Keys whose subtree and ancestors are refused, with the reason (all canonical).</summary>
        private static readonly IReadOnlyList<Tuple<RegistryLocation, RegistryWriteDenial>> ProtectedKeys = BuildProtectedKeys();

        private readonly IReadOnlyList<RegistryWriteRule> allowList;

        /// <param name="allowList">The HKCU keys that may be changed, and how.</param>
        public RegistryWritePolicy(IEnumerable<RegistryWriteRule> allowList)
        {
            if (allowList == null)
                throw new ArgumentNullException(nameof(allowList));
            this.allowList = new ReadOnlyCollection<RegistryWriteRule>(allowList.ToList());
            if (this.allowList.Any(rule => rule == null))
                throw new ArgumentException("The allow-list contains null.", nameof(allowList));
        }

        /// <summary>The allow-list of this policy.</summary>
        public IReadOnlyList<RegistryWriteRule> AllowList
        {
            get { return allowList; }
        }

        /// <summary>Decides about one change; never throws for any location.</summary>
        /// <param name="operation">The change.</param>
        /// <param name="key">The key that is changed (for <see cref="RegistryOperation.CreateSubKey"/> the new key).</param>
        /// <param name="valueName">The value of a value operation; null for key operations.</param>
        /// <param name="newValue">The value <see cref="RegistryOperation.SetValue"/> writes (for the content check).</param>
        /// <param name="readCurrentValue">
        /// Reads the current value, for the content check of compatibility values; without it such a change is refused.
        /// </param>
        public RegistryWriteDecision Check(RegistryOperation operation, RegistryLocation key, string valueName = null,
            RegistryValue newValue = null, Func<RegistryResult<RegistryValue>> readCurrentValue = null)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            RegistryLocation canonical = RegistryPath.Canonicalize(key);
            RegistryWriteDenial denial = FindProtection(key, canonical);
            if (denial == RegistryWriteDenial.None && key.Hive != RegistryHive.CurrentUser)
                denial = RegistryWriteDenial.NotCurrentUser;
            if (denial == RegistryWriteDenial.None)
            {
                List<RegistryWriteRule> matching = allowList.Where(rule => rule.Matches(operation, key)).ToList();
                RegistryWriteRule rule = matching.FirstOrDefault(candidate => candidate.AllowsValueName(valueName));
                if (matching.Count == 0)
                    denial = RegistryWriteDenial.NotInAllowList;
                else if (rule == null)
                    denial = RegistryWriteDenial.ValueNotAllowed;
                else if (rule.LayerEntries != null && valueName != null &&
                         !IsAllowedLayerChange(operation, rule.LayerEntries, newValue, readCurrentValue))
                    denial = RegistryWriteDenial.LayerContent;
            }
            return new RegistryWriteDecision(operation, key, valueName, canonical, denial);
        }

        /// <summary>The content check of a compatibility value (contract 3.7, ADR 0007 plan review).</summary>
        private static bool IsAllowedLayerChange(RegistryOperation operation, IReadOnlyList<string> switchable,
            RegistryValue newValue, Func<RegistryResult<RegistryValue>> readCurrentValue)
        {
            if (readCurrentValue == null)
                return false;
            RegistryResult<RegistryValue> current = readCurrentValue();
            string currentText;
            if (current.Status == RegistryStatus.Missing)
                currentText = null;
            else if (current.IsOk && current.Value.Type == RegistryValueType.String)
                currentText = current.Value.StringValue;
            else
                return false; // unreadable, or not a string: nothing may be assumed about its entries

            if (operation == RegistryOperation.DeleteValue)
                return CompatibilityLayers.IsAllowedDeletion(currentText, switchable);
            return newValue != null && newValue.Type == RegistryValueType.String &&
                   CompatibilityLayers.IsAllowedChange(currentText, newValue.StringValue, switchable);
        }

        private static RegistryWriteDenial FindProtection(RegistryLocation key, RegistryLocation canonical)
        {
            foreach (var protectedKey in ProtectedKeys)
            {
                RegistryLocation root = protectedKey.Item1;
                if (root.Hive != canonical.Hive || root.View != canonical.View)
                    continue;
                // Below the protected key, the key itself, or an ancestor of it.
                if (RegistryPath.StartsWith(canonical.Segments, root.Segments) ||
                    RegistryPath.StartsWith(root.Segments, canonical.Segments))
                    return protectedKey.Item2;
            }

            // HKCU\Software\Classes\VirtualStore and its ancestors contain the virtualized copy of HKLM, CD keys
            // included. Below ...\VirtualStore\MACHINE the canonical form is in HKLM already (handled above).
            if (canonical.Hive == RegistryHive.CurrentUser &&
                RegistryPath.StartsWith(new[] { "SOFTWARE", "CLASSES", "VIRTUALSTORE", "MACHINE" }, canonical.Segments))
                return RegistryWriteDenial.CdKeys;
            return RegistryWriteDenial.None;
        }

        private static IReadOnlyList<Tuple<RegistryLocation, RegistryWriteDenial>> BuildProtectedKeys()
        {
            var keys = new List<Tuple<RegistryLocation, RegistryWriteDenial>>();
            void Add(string path, RegistryWriteDenial denial)
            {
                foreach (RegistryLocation location in new[]
                         {
                             RegistryLocation.CurrentUser(path), RegistryLocation.LocalMachine64(path),
                             RegistryLocation.LocalMachine32(path)
                         })
                {
                    keys.Add(Tuple.Create(RegistryPath.Canonicalize(location), denial));
                }
            }

            Add(ContractNames.CdKeysKey, RegistryWriteDenial.CdKeys);
            Add(ContractNames.InstallRecordsKey, RegistryWriteDenial.InstallRecord);
            Add(ContractNames.UninstallKey, RegistryWriteDenial.UninstallKey);
            return keys.AsReadOnly();
        }
    }
}
