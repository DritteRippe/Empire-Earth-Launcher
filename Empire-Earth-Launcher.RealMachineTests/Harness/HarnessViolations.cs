using System;
using System.Collections.Generic;
using System.Linq;

namespace Empire_Earth_Launcher.RealMachineTests.Harness
{
    /// <summary>
    /// Every change the harness refused (a write of read-only code, a write outside the work folder, a deleted registry tree):
    /// the decorators add it here and throw <see cref="HarnessViolationException"/>, and <c>MachineStateTests</c> fails at the
    /// end if the list is not empty, also when the core caught the exception.
    /// </summary>
    internal sealed class HarnessViolations
    {
        private readonly List<string> items = new List<string>();
        private readonly object sync = new object();

        public IReadOnlyList<string> Items
        {
            get
            {
                lock (sync)
                    return items.ToList();
            }
        }

        /// <summary>Records <paramref name="what"/> and returns the exception to throw.</summary>
        public HarnessViolationException Add(string what)
        {
            if (what == null)
                throw new ArgumentNullException(nameof(what));
            lock (sync)
                items.Add(what);
            return new HarnessViolationException(what);
        }
    }

    /// <summary>A change the harness refused (<see cref="HarnessViolations"/>).</summary>
    internal sealed class HarnessViolationException : InvalidOperationException
    {
        public HarnessViolationException(string message)
            : base("The real-machine harness refused a change: " + message)
        {
        }
    }
}
