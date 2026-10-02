using System;
using Empire_Earth_Launcher.Core.Platform;
using Microsoft.Win32;

namespace Empire_Earth_Launcher.Core.Installations
{
    /// <summary>Typed reads of single registry values for the readers of the discovery.</summary>
    internal static class RegistryReads
    {
        /// <summary>The HKLM views in the order "64-bit, then 32-bit" (sources 2 and 3 of contract 1.4).</summary>
        internal static readonly RegistryView[] LocalMachine64Then32 = { RegistryView.Registry64, RegistryView.Registry32 };

        /// <summary>
        /// A string value (REG_SZ or REG_EXPAND_SZ, not expanded); <see cref="RegistryStatus.Missing"/> also for a value
        /// of another type, with the type in the detail.
        /// </summary>
        internal static RegistryResult<string> GetString(IRegistry registry, RegistryLocation key, string valueName)
        {
            RegistryResult<RegistryValue> value = registry.GetValue(key, valueName);
            if (!value.IsOk)
                return RegistryResult<string>.Failure(value.Status, value.Detail);
            if (!value.Value.IsString)
                return RegistryResult<string>.Failure(RegistryStatus.Missing,
                    "The value \"" + valueName + "\" of " + key + " is " + value.Value.Type + ", not a string.");
            return RegistryResult<string>.Success(value.Value.StringValue);
        }

        /// <summary>A string value, or null if it is missing, of another type or cannot be read.</summary>
        internal static string GetStringOrNull(IRegistry registry, RegistryLocation key, string valueName)
        {
            RegistryResult<string> value = GetString(registry, key, valueName);
            return value.IsOk ? value.Value : null;
        }

        /// <summary>A REG_DWORD value, or null if it is missing, of another type or cannot be read.</summary>
        internal static int? GetDWordOrNull(IRegistry registry, RegistryLocation key, string valueName)
        {
            RegistryResult<RegistryValue> value = registry.GetValue(key, valueName);
            return value.IsOk && value.Value.Type == RegistryValueType.DWord ? value.Value.DWordValue : (int?)null;
        }

        /// <summary>
        /// The full path of an install root as the setup wrote it (with or without trailing backslash), in the normal
        /// form of <see cref="WinPath"/>; null if it is empty or not fully qualified.
        /// </summary>
        internal static string ToRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !WinPath.IsFullyQualified(path))
                return null;
            return WinPath.Normalize(path);
        }

        /// <summary>The hive and view of a key for log messages and install modes.</summary>
        internal static bool IsCurrentUser(RegistryLocation key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            return key.Hive == RegistryHive.CurrentUser;
        }
    }
}
