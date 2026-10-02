using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>
    /// <see cref="IRegistry"/> on the Windows registry. A thin adapter (ADR 0006): every key is opened through
    /// <see cref="RegistryKey.OpenBaseKey"/> with the hive and the explicit view of its
    /// <see cref="RegistryLocation"/> (contract 0), and the documented exceptions become
    /// <see cref="RegistryStatus"/> values. On 32-bit Windows both views of HKLM are the same; Windows returns the
    /// 32-bit view for a request of the 64-bit view there.
    /// </summary>
    /// <remarks>
    /// It does not check the write policy; the launcher wraps it in <see cref="PolicyCheckedRegistry"/>. Checked
    /// on real Windows by the test plan (docs/TEST-PLAN.de.md), not by the unit tests.
    /// </remarks>
    public sealed class WindowsRegistry : IRegistry
    {
        private const int ErrorSuccess = 0;
        private const int ErrorFileNotFound = 2;
        private const int ErrorPathNotFound = 3;
        private const int ErrorAccessDenied = 5;
        private const int RegOptionOpenLink = 0x8;
        private const int KeyQueryValue = 0x1;
        private const int KeyWow6464Key = 0x100;
        private const int KeyWow6432Key = 0x200;
        private const int RegLink = 6;
        private const string SymbolicLinkValueName = "SymbolicLinkValue";

        public RegistryResult ProbeKey(RegistryLocation key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            return Run(() =>
            {
                using (RegistryKey opened = Open(key, false))
                    return opened == null ? Missing("The key does not exist: " + key) : RegistryResult.Success;
            });
        }

        /// <remarks>
        /// <see cref="RegistryKey"/> always follows links, so the key is opened with <c>RegOpenKeyEx</c> and
        /// <c>REG_OPTION_OPEN_LINK</c>, which opens a link itself; a link has the value <c>SymbolicLinkValue</c> of type
        /// <c>REG_LINK</c>. Only the last name of the path is looked at this way: the callers walk a tree from its root.
        /// </remarks>
        public RegistryResult<bool> IsLink(RegistryLocation key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (key.IsRoot)
                return RegistryResult<bool>.Success(false);
            return Run(() =>
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(key.Hive, key.View))
                {
                    int access = KeyQueryValue | (key.View == RegistryView.Registry64 ? KeyWow6464Key
                        : key.View == RegistryView.Registry32 ? KeyWow6432Key : 0);
                    int error = NativeMethods.RegOpenKeyEx(baseKey.Handle, key.Path, RegOptionOpenLink, access, out IntPtr opened);
                    if (error == ErrorFileNotFound || error == ErrorPathNotFound)
                        return RegistryResult<bool>.Failure(RegistryStatus.Missing, "The key does not exist: " + key);
                    if (error != ErrorSuccess)
                        return RegistryResult<bool>.Failure(error == ErrorAccessDenied ? RegistryStatus.AccessDenied : RegistryStatus.IoError,
                            key + ": " + new Win32Exception(error).Message);
                    using (var handle = new SafeRegistryHandle(opened, true))
                    {
                        int size = 0;
                        int queried = NativeMethods.RegQueryValueEx(handle, SymbolicLinkValueName, IntPtr.Zero, out int type,
                            IntPtr.Zero, ref size);
                        return RegistryResult<bool>.Success(queried == ErrorSuccess && type == RegLink);
                    }
                }
            });
        }

        public RegistryResult<RegistryValue> GetValue(RegistryLocation key, string valueName)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (valueName == null)
                throw new ArgumentNullException(nameof(valueName));
            return Run(() =>
            {
                using (RegistryKey opened = Open(key, false))
                {
                    if (opened == null)
                        return RegistryResult<RegistryValue>.Failure(RegistryStatus.Missing, "The key does not exist: " + key);
                    object data = opened.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                    if (data == null)
                        return RegistryResult<RegistryValue>.Failure(RegistryStatus.Missing,
                            "The value \"" + valueName + "\" does not exist in " + key);
                    return RegistryResult<RegistryValue>.Success(ToValue(opened, valueName, data));
                }
            });
        }

        public RegistryResult<IReadOnlyList<string>> GetValueNames(RegistryLocation key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            return List(key, opened => opened.GetValueNames());
        }

        public RegistryResult<IReadOnlyList<string>> GetSubKeyNames(RegistryLocation key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            return List(key, opened => opened.GetSubKeyNames());
        }

        public RegistryResult CreateSubKey(RegistryLocation key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (key.IsRoot)
                throw new ArgumentException("A hive cannot be created.", nameof(key));
            return Run(() =>
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(key.Hive, key.View))
                using (baseKey.CreateSubKey(key.Path))
                    return RegistryResult.Success;
            });
        }

        public RegistryResult SetValue(RegistryLocation key, string valueName, RegistryValue value)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (valueName == null)
                throw new ArgumentNullException(nameof(valueName));
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            return Run(() =>
            {
                using (RegistryKey opened = Open(key, true))
                {
                    if (opened == null)
                        return Missing("The key does not exist: " + key);
                    switch (value.Type)
                    {
                        case RegistryValueType.String:
                            opened.SetValue(valueName, value.StringValue, RegistryValueKind.String);
                            break;
                        case RegistryValueType.ExpandString:
                            opened.SetValue(valueName, value.StringValue, RegistryValueKind.ExpandString);
                            break;
                        case RegistryValueType.DWord:
                            opened.SetValue(valueName, value.DWordValue, RegistryValueKind.DWord);
                            break;
                        case RegistryValueType.QWord:
                            opened.SetValue(valueName, value.QWordValue, RegistryValueKind.QWord);
                            break;
                        case RegistryValueType.MultiString:
                            opened.SetValue(valueName, value.MultiStringValue.ToArray(), RegistryValueKind.MultiString);
                            break;
                        case RegistryValueType.Binary:
                            opened.SetValue(valueName, value.GetBytes(), RegistryValueKind.Binary);
                            break;
                        case RegistryValueType.None:
                            opened.SetValue(valueName, value.GetBytes(), RegistryValueKind.None);
                            break;
                        default:
                            // The launcher only writes the types of the contract (REG_SZ, REG_DWORD).
                            throw new NotSupportedException("Writing registry values of type " + (int)value.Type +
                                                            " is not supported.");
                    }
                    return RegistryResult.Success;
                }
            });
        }

        public RegistryResult DeleteValue(RegistryLocation key, string valueName)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (valueName == null)
                throw new ArgumentNullException(nameof(valueName));
            return Run(() =>
            {
                using (RegistryKey opened = Open(key, true))
                {
                    if (opened == null)
                        return Missing("The key does not exist: " + key);
                    if (!opened.GetValueNames().Contains(valueName, StringComparer.OrdinalIgnoreCase))
                        return Missing("The value \"" + valueName + "\" does not exist in " + key);
                    opened.DeleteValue(valueName, false);
                    return RegistryResult.Success;
                }
            });
        }

        public RegistryResult DeleteSubKeyTree(RegistryLocation key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (key.IsRoot)
                throw new ArgumentException("A hive cannot be deleted.", nameof(key));
            return Run(() =>
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(key.Hive, key.View))
                {
                    using (RegistryKey existing = baseKey.OpenSubKey(key.Path, false))
                    {
                        if (existing == null)
                            return Missing("The key does not exist: " + key);
                    }
                    baseKey.DeleteSubKeyTree(key.Path, false);
                    return RegistryResult.Success;
                }
            });
        }

        /// <summary>The key, or null if it does not exist.</summary>
        private static RegistryKey Open(RegistryLocation key, bool writable)
        {
            RegistryKey baseKey = RegistryKey.OpenBaseKey(key.Hive, key.View);
            if (key.IsRoot)
                return baseKey;
            using (baseKey)
                return baseKey.OpenSubKey(key.Path, writable);
        }

        private static RegistryResult<IReadOnlyList<string>> List(RegistryLocation key, Func<RegistryKey, string[]> names)
        {
            return Run(() =>
            {
                using (RegistryKey opened = Open(key, false))
                {
                    if (opened == null)
                        return RegistryResult<IReadOnlyList<string>>.Failure(RegistryStatus.Missing, "The key does not exist: " + key);
                    IReadOnlyList<string> sorted = names(opened).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
                    return RegistryResult<IReadOnlyList<string>>.Success(sorted);
                }
            });
        }

        private static RegistryValue ToValue(RegistryKey key, string valueName, object data)
        {
            RegistryValueKind kind = key.GetValueKind(valueName);
            switch (kind)
            {
                case RegistryValueKind.String:
                    return RegistryValue.FromString((string)data);
                case RegistryValueKind.ExpandString:
                    return RegistryValue.FromExpandString((string)data);
                case RegistryValueKind.DWord:
                    return RegistryValue.FromDWord((int)data);
                case RegistryValueKind.QWord:
                    return RegistryValue.FromQWord((long)data);
                case RegistryValueKind.MultiString:
                    return RegistryValue.FromMultiString((string[])data);
                case RegistryValueKind.Binary:
                    return RegistryValue.FromBinary((byte[])data);
                case RegistryValueKind.None:
                    return RegistryValue.FromRaw(RegistryValueType.None, data as byte[] ?? new byte[0]);
                default:
                    // RegistryValueKind.Unknown hides the real type number (e.g. 8, REG_RESOURCE_LIST); a backup has
                    // to keep it (ADR 0007, hex(n)), so it is read from Windows directly.
                    return RegistryValue.FromRaw((RegistryValueType)QueryValueType(key, valueName), data as byte[] ?? new byte[0]);
            }
        }

        private static int QueryValueType(RegistryKey key, string valueName)
        {
            int size = 0;
            int error = NativeMethods.RegQueryValueEx(key.Handle, valueName, IntPtr.Zero, out int type, IntPtr.Zero, ref size);
            if (error != ErrorSuccess)
                throw new IOException("Unable to read the type of the registry value \"" + valueName + "\".", new Win32Exception(error));
            return type;
        }

        private static RegistryResult Missing(string detail)
        {
            return RegistryResult.Failure(RegistryStatus.Missing, detail);
        }

        private static RegistryResult Run(Func<RegistryResult> action)
        {
            try
            {
                return action();
            }
            catch (Exception ex) when (IsRegistryError(ex))
            {
                return RegistryResult.Failure(ToStatus(ex), ex.Message);
            }
        }

        private static RegistryResult<T> Run<T>(Func<RegistryResult<T>> action)
        {
            try
            {
                return action();
            }
            catch (Exception ex) when (IsRegistryError(ex))
            {
                return RegistryResult<T>.Failure(ToStatus(ex), ex.Message);
            }
        }

        /// <summary>The exceptions <see cref="RegistryKey"/> documents for its operations.</summary>
        private static bool IsRegistryError(Exception ex)
        {
            return ex is SecurityException || ex is UnauthorizedAccessException || ex is IOException ||
                   ex is ArgumentException;
        }

        private static RegistryStatus ToStatus(Exception ex)
        {
            if (ex is SecurityException || ex is UnauthorizedAccessException)
                return RegistryStatus.AccessDenied;
            if (ex is ArgumentException)
                return RegistryStatus.InvalidName;
            return RegistryStatus.IoError;
        }

        private static class NativeMethods
        {
            [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegQueryValueExW")]
            public static extern int RegQueryValueEx(SafeRegistryHandle key, string valueName, IntPtr reserved,
                out int type, IntPtr data, ref int dataSize);

            [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegOpenKeyExW")]
            public static extern int RegOpenKeyEx(SafeRegistryHandle key, string subKey, int options, int samDesired,
                out IntPtr result);
        }
    }
}
