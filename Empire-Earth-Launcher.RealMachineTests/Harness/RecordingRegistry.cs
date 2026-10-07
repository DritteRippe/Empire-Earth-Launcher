using System;
using System.Collections.Generic;
using System.Linq;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.RealMachineTests.Harness
{
    /// <summary>One change of the registry that went through <see cref="RecordingRegistry"/>.</summary>
    internal sealed class RegistryWrite
    {
        public RegistryWrite(RegistryOperation operation, RegistryLocation key, string valueName)
        {
            Operation = operation;
            Key = key ?? throw new ArgumentNullException(nameof(key));
            ValueName = valueName;
        }

        public RegistryOperation Operation { get; }

        public RegistryLocation Key { get; }

        /// <summary>The value name; null for <see cref="RegistryOperation.CreateSubKey"/>.</summary>
        public string ValueName { get; }

        public override string ToString()
        {
            return Operation + " " + Key + (ValueName == null ? string.Empty : " @\"" + ValueName + "\"");
        }
    }

    /// <summary>
    /// <see cref="IRegistry"/> that records every change before passing it on, below the launcher's write policy
    /// (<c>PolicyCheckedRegistry</c> outside, so only allowed changes reach it): the defaults checks compare the record with
    /// what the launcher start may write. A deleted registry tree is refused (<see cref="HarnessViolations"/>): the defaults
    /// never delete a key.
    /// </summary>
    internal sealed class RecordingRegistry : IRegistry
    {
        private readonly IRegistry inner;
        private readonly HarnessViolations violations;
        private readonly List<RegistryWrite> writes = new List<RegistryWrite>();
        private readonly object sync = new object();

        public RecordingRegistry(IRegistry inner, HarnessViolations violations)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.violations = violations ?? throw new ArgumentNullException(nameof(violations));
        }

        /// <summary>Every change so far, in order.</summary>
        public IReadOnlyList<RegistryWrite> Writes
        {
            get
            {
                lock (sync)
                    return writes.ToList();
            }
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
            Record(RegistryOperation.CreateSubKey, key, null);
            return inner.CreateSubKey(key);
        }

        public RegistryResult SetValue(RegistryLocation key, string valueName, RegistryValue value)
        {
            Record(RegistryOperation.SetValue, key, valueName);
            return inner.SetValue(key, valueName, value);
        }

        public RegistryResult DeleteValue(RegistryLocation key, string valueName)
        {
            Record(RegistryOperation.DeleteValue, key, valueName);
            return inner.DeleteValue(key, valueName);
        }

        public RegistryResult DeleteSubKeyTree(RegistryLocation key)
        {
            throw violations.Add("DeleteSubKeyTree " + key + " (the game defaults never delete a key)");
        }

        private void Record(RegistryOperation operation, RegistryLocation key, string valueName)
        {
            lock (sync)
                writes.Add(new RegistryWrite(operation, key, valueName));
        }
    }
}
