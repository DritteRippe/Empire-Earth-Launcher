using System;
using System.Collections.Generic;
using System.Linq;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Tests.Fakes
{
    /// <summary>
    /// <see cref="IMutexProbe"/> with a set of existing mutex names (case-sensitive, like Windows kernel object names).
    /// Records every name it was asked about.
    /// </summary>
    internal sealed class FakeMutexProbe : IMutexProbe
    {
        private readonly HashSet<string> existing = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> probed = new List<string>();

        /// <summary>Called with "mutex &lt;name&gt;" by every <see cref="Exists"/> (order tests).</summary>
        public Action<string> OnProbe { get; set; }

        /// <summary>Every name passed to <see cref="Exists"/>, in order.</summary>
        public IReadOnlyList<string> Probed
        {
            get { return probed.ToList(); }
        }

        /// <summary>Makes the mutexes exist (a setup or game "starts").</summary>
        public FakeMutexProbe With(params string[] names)
        {
            foreach (string name in names)
                existing.Add(name);
            return this;
        }

        /// <summary>The mutex no longer exists (the setup or game ended).</summary>
        public void Remove(string name)
        {
            existing.Remove(name);
        }

        public bool Exists(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("A mutex name is required.", nameof(name));
            OnProbe?.Invoke("mutex " + name);
            probed.Add(name);
            return existing.Contains(name);
        }
    }
}
