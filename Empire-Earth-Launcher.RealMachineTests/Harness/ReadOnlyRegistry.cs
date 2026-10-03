using System.Collections.Generic;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.RealMachineTests.Harness
{
    /// <summary>
    /// <see cref="IRegistry"/> that passes reads to another registry and refuses every change (<see cref="HarnessViolations"/>):
    /// for the discovery, the integrity check, the consistency check and the snapshots, which must only read (contract 1.4,
    /// 2.5).
    /// </summary>
    internal sealed class ReadOnlyRegistry : IRegistry
    {
        private readonly IRegistry inner;
        private readonly HarnessViolations violations;

        public ReadOnlyRegistry(IRegistry inner, HarnessViolations violations)
        {
            this.inner = inner ?? throw new System.ArgumentNullException(nameof(inner));
            this.violations = violations ?? throw new System.ArgumentNullException(nameof(violations));
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
            throw violations.Add("CreateSubKey " + key + " by read-only code");
        }

        public RegistryResult SetValue(RegistryLocation key, string valueName, RegistryValue value)
        {
            throw violations.Add("SetValue " + key + " @\"" + valueName + "\" by read-only code");
        }

        public RegistryResult DeleteValue(RegistryLocation key, string valueName)
        {
            throw violations.Add("DeleteValue " + key + " @\"" + valueName + "\" by read-only code");
        }

        public RegistryResult DeleteSubKeyTree(RegistryLocation key)
        {
            throw violations.Add("DeleteSubKeyTree " + key + " by read-only code");
        }
    }
}
