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
        NotInAllowList
    }

    /// <summary>One entry of the allow-list: an exact HKCU key and the operations allowed on it.</summary>
    public sealed class RegistryWriteRule
    {
        /// <param name="currentUserKey">Path below HKCU, spelled as the key is written (case does not matter).</param>
        /// <param name="operations">The allowed operations on that key (not on its subkeys).</param>
        public RegistryWriteRule(string currentUserKey, params RegistryOperation[] operations)
        {
            if (currentUserKey == null)
                throw new ArgumentNullException(nameof(currentUserKey));
            if (operations == null || operations.Length == 0)
                throw new ArgumentException("A rule needs at least one operation.", nameof(operations));
            Key = RegistryLocation.CurrentUser(currentUserKey);
            if (Key.IsRoot)
                throw new ArgumentException("HKCU itself can never be changed.", nameof(currentUserKey));
            Operations = new ReadOnlyCollection<RegistryOperation>(operations.Distinct().ToList());
        }

        /// <summary>The HKCU key.</summary>
        public RegistryLocation Key { get; }

        public IReadOnlyList<RegistryOperation> Operations { get; }

        internal bool Allows(RegistryOperation operation, RegistryLocation key)
        {
            return Operations.Contains(operation) && Key.Equals(key);
        }

        public override string ToString()
        {
            return Key + " (" + string.Join(", ", Operations) + ")";
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
    /// <see cref="Default"/> holds the keys of the contract (game settings keys and their <c>Game Options</c> of
    /// 3.1, defaults markers of 3.5, GPU preference of 3.4, compatibility layers of 3.7), at key level; restricting
    /// them to the value names of the contract tables comes with the game settings work package.
    /// </para>
    /// </remarks>
    public sealed class RegistryWritePolicy
    {
        private static readonly RegistryOperation[] ValueAndKeyCreation =
        {
            RegistryOperation.SetValue, RegistryOperation.DeleteValue, RegistryOperation.CreateSubKey
        };

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

        /// <summary>The policy of the launcher: the contract keys of ADR 0007 (see the remarks).</summary>
        public static RegistryWritePolicy Default { get; } = new RegistryWritePolicy(ContractAllowList());

        /// <summary>The allow-list of this policy.</summary>
        public IReadOnlyList<RegistryWriteRule> AllowList
        {
            get { return allowList; }
        }

        /// <summary>Decides about one change; never throws for any location.</summary>
        /// <param name="operation">The change.</param>
        /// <param name="key">The key that is changed (for <see cref="RegistryOperation.CreateSubKey"/> the new key).</param>
        /// <param name="valueName">The value of a value operation; null for key operations.</param>
        public RegistryWriteDecision Check(RegistryOperation operation, RegistryLocation key, string valueName = null)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            RegistryLocation canonical = RegistryPath.Canonicalize(key);
            RegistryWriteDenial denial = FindProtection(key, canonical);
            if (denial == RegistryWriteDenial.None && key.Hive != RegistryHive.CurrentUser)
                denial = RegistryWriteDenial.NotCurrentUser;
            if (denial == RegistryWriteDenial.None && !allowList.Any(rule => rule.Allows(operation, key)))
                denial = RegistryWriteDenial.NotInAllowList;
            return new RegistryWriteDecision(operation, key, valueName, canonical, denial);
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

        private static IEnumerable<RegistryWriteRule> ContractAllowList()
        {
            foreach (Product product in Product.All)
            {
                foreach (Game game in Game.All)
                {
                    string settingsKey = product.GetGameSettingsKey(game);
                    yield return new RegistryWriteRule(settingsKey, ValueAndKeyCreation);
                    yield return new RegistryWriteRule(settingsKey + @"\" + ContractNames.GameOptionsSubKeyName, ValueAndKeyCreation);
                }
                yield return new RegistryWriteRule(product.DefaultsMarkerKey, ValueAndKeyCreation);
            }
            yield return new RegistryWriteRule(ContractNames.GpuPreferencesKey, ValueAndKeyCreation);
            yield return new RegistryWriteRule(ContractNames.CompatibilityLayersKey, ValueAndKeyCreation);
        }
    }
}
