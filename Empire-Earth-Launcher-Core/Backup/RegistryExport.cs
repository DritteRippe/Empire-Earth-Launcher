using System;
using System.Collections.Generic;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Backup
{
    /// <summary>
    /// Reads a registry key with its subkeys into <see cref="RegFileKey"/>s for a <c>.reg</c> backup (ADR 0007). Only
    /// reads; a key or value that cannot be read makes the whole export fail, so that a backup is complete or not
    /// written at all ("if the backup fails, nothing is changed", contract 3.6).
    /// </summary>
    public static class RegistryExport
    {
        /// <summary>Deeper keys than this are not exported (the game settings keys have one level of subkeys).</summary>
        public const int MaxDepth = 32;

        /// <summary>
        /// The key <paramref name="root"/> and all its subkeys, parents before their subkeys, subkeys and values in the
        /// order of the registry (sorted ignoring case). A missing root gives an empty list.
        /// </summary>
        /// <returns><see cref="RegistryStatus.Ok"/> with the keys, or the first failure with the key or value in the detail.</returns>
        public static RegistryResult<IReadOnlyList<RegFileKey>> ReadTree(IRegistry registry, RegistryLocation root)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));
            if (root == null)
                throw new ArgumentNullException(nameof(root));

            var keys = new List<RegFileKey>();
            RegistryResult probe = registry.ProbeKey(root);
            if (probe.Status == RegistryStatus.Missing)
                return RegistryResult<IReadOnlyList<RegFileKey>>.Success(keys);
            if (!probe.IsOk)
                return RegistryResult<IReadOnlyList<RegFileKey>>.Failure(probe.Status, root + ": " + probe.Detail);

            RegistryResult result = Read(registry, root, 0, keys);
            return result.IsOk
                ? RegistryResult<IReadOnlyList<RegFileKey>>.Success(keys)
                : RegistryResult<IReadOnlyList<RegFileKey>>.Failure(result.Status, result.Detail);
        }

        private static RegistryResult Read(IRegistry registry, RegistryLocation key, int depth, List<RegFileKey> keys)
        {
            if (depth > MaxDepth)
                return RegistryResult.Failure(RegistryStatus.IoError, key + ": deeper than " + MaxDepth + " levels");

            var exported = new RegFileKey(key);
            keys.Add(exported);

            RegistryResult<IReadOnlyList<string>> names = registry.GetValueNames(key);
            if (!names.IsOk)
                return RegistryResult.Failure(names.Status, key + ": " + names.Detail);
            foreach (string name in names.Value)
            {
                RegistryResult<RegistryValue> value = registry.GetValue(key, name);
                if (!value.IsOk)
                    return RegistryResult.Failure(value.Status, key + " @\"" + name + "\": " + value.Detail);
                exported.Add(name, value.Value);
            }

            RegistryResult<IReadOnlyList<string>> subKeys = registry.GetSubKeyNames(key);
            if (!subKeys.IsOk)
                return RegistryResult.Failure(subKeys.Status, key + ": " + subKeys.Detail);
            foreach (string subKey in subKeys.Value)
            {
                RegistryResult result = Read(registry, key.Child(subKey), depth + 1, keys);
                if (!result.IsOk)
                    return result;
            }
            return RegistryResult.Success;
        }
    }
}
