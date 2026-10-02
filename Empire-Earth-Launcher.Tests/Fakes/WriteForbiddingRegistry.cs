using System.Collections.Generic;
using Empire_Earth_Launcher.Core.Platform;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Fakes
{
    /// <summary>
    /// <see cref="IRegistry"/> that passes reads to another registry and fails the test at the first change: for code
    /// that must only read (contract 1.4, "Read-only").
    /// </summary>
    internal sealed class WriteForbiddingRegistry : IRegistry
    {
        private readonly IRegistry inner;

        public WriteForbiddingRegistry(IRegistry inner)
        {
            this.inner = inner;
        }

        public RegistryResult ProbeKey(RegistryLocation key)
        {
            return inner.ProbeKey(key);
        }

        public RegistryResult<bool> IsLink(RegistryLocation key)
        {
            return inner.IsLink(key);
        }

        public RegistryResult<RegistryValue> GetValue(RegistryLocation key, string valueName)
        {
            return inner.GetValue(key, valueName);
        }

        public RegistryResult<IReadOnlyList<string>> GetValueNames(RegistryLocation key)
        {
            return inner.GetValueNames(key);
        }

        public RegistryResult<IReadOnlyList<string>> GetSubKeyNames(RegistryLocation key)
        {
            return inner.GetSubKeyNames(key);
        }

        public RegistryResult CreateSubKey(RegistryLocation key)
        {
            throw new AssertionException("CreateSubKey " + key + " called by read-only code.");
        }

        public RegistryResult SetValue(RegistryLocation key, string valueName, RegistryValue value)
        {
            throw new AssertionException("SetValue " + key + " @\"" + valueName + "\" called by read-only code.");
        }

        public RegistryResult DeleteValue(RegistryLocation key, string valueName)
        {
            throw new AssertionException("DeleteValue " + key + " @\"" + valueName + "\" called by read-only code.");
        }

        public RegistryResult DeleteSubKeyTree(RegistryLocation key)
        {
            throw new AssertionException("DeleteSubKeyTree " + key + " called by read-only code.");
        }
    }
}
