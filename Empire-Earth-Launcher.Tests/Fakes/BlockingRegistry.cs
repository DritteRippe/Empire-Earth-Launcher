using System;
using System.Collections.Generic;
using System.Threading;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Tests.Fakes
{
    /// <summary>
    /// <see cref="IRegistry"/> whose reads block until the test releases them, like a registry or network drive that
    /// hangs: for the check that asynchronous code does not block its caller (ADR 0004, ADR 0012 amendment).
    /// </summary>
    internal sealed class BlockingRegistry : IRegistry, IDisposable
    {
        private readonly IRegistry inner;
        private readonly ManualResetEventSlim released = new ManualResetEventSlim(false);
        private readonly ManualResetEventSlim entered = new ManualResetEventSlim(false);

        public BlockingRegistry(IRegistry inner)
        {
            this.inner = inner;
        }

        /// <summary>Set as soon as the first read waits.</summary>
        public WaitHandle Entered
        {
            get { return entered.WaitHandle; }
        }

        /// <summary>Lets every waiting and later read through.</summary>
        public void Release()
        {
            released.Set();
        }

        public void Dispose()
        {
            released.Set();
            released.Dispose();
            entered.Dispose();
        }

        private void Block()
        {
            entered.Set();
            if (!released.Wait(TimeSpan.FromSeconds(30)))
                throw new TimeoutException("The test did not release the blocking registry.");
        }

        public RegistryResult ProbeKey(RegistryLocation key)
        {
            Block();
            return inner.ProbeKey(key);
        }

        public RegistryResult<bool> IsLink(RegistryLocation key)
        {
            Block();
            return inner.IsLink(key);
        }

        public RegistryResult<RegistryValue> GetValue(RegistryLocation key, string valueName)
        {
            Block();
            return inner.GetValue(key, valueName);
        }

        public RegistryResult<IReadOnlyList<string>> GetValueNames(RegistryLocation key)
        {
            Block();
            return inner.GetValueNames(key);
        }

        public RegistryResult<IReadOnlyList<string>> GetSubKeyNames(RegistryLocation key)
        {
            Block();
            return inner.GetSubKeyNames(key);
        }

        public RegistryResult CreateSubKey(RegistryLocation key)
        {
            return inner.CreateSubKey(key);
        }

        public RegistryResult SetValue(RegistryLocation key, string valueName, RegistryValue value)
        {
            return inner.SetValue(key, valueName, value);
        }

        public RegistryResult DeleteValue(RegistryLocation key, string valueName)
        {
            return inner.DeleteValue(key, valueName);
        }

        public RegistryResult DeleteSubKeyTree(RegistryLocation key)
        {
            return inner.DeleteSubKeyTree(key);
        }
    }
}
