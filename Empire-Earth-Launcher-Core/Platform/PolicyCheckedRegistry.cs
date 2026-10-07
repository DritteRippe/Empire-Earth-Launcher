using System;
using System.Collections.Generic;

namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>
    /// A change the write policy refused (ADR 0007). It is a programming error: the core never offers such a
    /// change, so it ends in the global error handlers like any other bug.
    /// </summary>
    public sealed class RegistryWriteDeniedException : InvalidOperationException
    {
        public RegistryWriteDeniedException(RegistryWriteDecision decision)
            : base("The registry write policy refused a change: " + decision)
        {
            Decision = decision;
        }

        public RegistryWriteDecision Decision { get; }
    }

    /// <summary>
    /// <see cref="IRegistry"/> that passes every change through a <see cref="RegistryWritePolicy"/> before it
    /// reaches the registry, so that no code path of the launcher can write around the policy (ADR 0007). Reads
    /// are not restricted: the diagnostics may check that the CD keys exist (contract 3.8).
    /// </summary>
    /// <remarks>
    /// For the content check of compatibility values the policy compares the written value with the current one, which
    /// this wrapper reads from the registry just before the change.
    /// </remarks>
    public sealed class PolicyCheckedRegistry : IRegistry
    {
        private readonly IRegistry registry;
        private readonly RegistryWritePolicy policy;

        public PolicyCheckedRegistry(IRegistry registry, RegistryWritePolicy policy)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        public RegistryResult ProbeKey(RegistryLocation key)
        {
            return registry.ProbeKey(key);
        }

        public RegistryResult<bool> IsLink(RegistryLocation key)
        {
            return registry.IsLink(key);
        }

        public RegistryResult<RegistryValue> GetValue(RegistryLocation key, string valueName)
        {
            return registry.GetValue(key, valueName);
        }

        public RegistryResult<IReadOnlyList<string>> GetValueNames(RegistryLocation key)
        {
            return registry.GetValueNames(key);
        }

        public RegistryResult<IReadOnlyList<string>> GetSubKeyNames(RegistryLocation key)
        {
            return registry.GetSubKeyNames(key);
        }

        /// <exception cref="RegistryWriteDeniedException">The policy refuses the change.</exception>
        public RegistryResult CreateSubKey(RegistryLocation key)
        {
            Demand(RegistryOperation.CreateSubKey, key, null, null);
            return registry.CreateSubKey(key);
        }

        /// <exception cref="RegistryWriteDeniedException">The policy refuses the change.</exception>
        public RegistryResult SetValue(RegistryLocation key, string valueName, RegistryValue value)
        {
            if (valueName == null)
                throw new ArgumentNullException(nameof(valueName));
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            Demand(RegistryOperation.SetValue, key, valueName, value);
            return registry.SetValue(key, valueName, value);
        }

        /// <exception cref="RegistryWriteDeniedException">The policy refuses the change.</exception>
        public RegistryResult DeleteValue(RegistryLocation key, string valueName)
        {
            if (valueName == null)
                throw new ArgumentNullException(nameof(valueName));
            Demand(RegistryOperation.DeleteValue, key, valueName, null);
            return registry.DeleteValue(key, valueName);
        }

        /// <exception cref="RegistryWriteDeniedException">The policy refuses the change.</exception>
        public RegistryResult DeleteSubKeyTree(RegistryLocation key)
        {
            Demand(RegistryOperation.DeleteSubKeyTree, key, null, null);
            return registry.DeleteSubKeyTree(key);
        }

        private void Demand(RegistryOperation operation, RegistryLocation key, string valueName, RegistryValue value)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            RegistryWriteDecision decision = policy.Check(operation, key, valueName, value,
                valueName == null ? (Func<RegistryResult<RegistryValue>>)null : () => registry.GetValue(key, valueName));
            if (!decision.IsAllowed)
                throw new RegistryWriteDeniedException(decision);
        }
    }
}
