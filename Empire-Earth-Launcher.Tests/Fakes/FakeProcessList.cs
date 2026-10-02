using System;
using System.Collections.Generic;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Tests.Fakes
{
    /// <summary><see cref="IProcessList"/> with a set of running program file names (compared ignoring case, like Windows).</summary>
    internal sealed class FakeProcessList : IProcessList
    {
        private readonly HashSet<string> running = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Makes processes of these programs "run" (e.g. a hanging <c>Empire Earth.exe</c>).</summary>
        public FakeProcessList With(params string[] programFileNames)
        {
            foreach (string name in programFileNames)
                running.Add(name);
            return this;
        }

        public bool IsRunning(string programFileName)
        {
            WindowsProcessList.ProcessName(programFileName); // the same argument check as the real list
            return running.Contains(programFileName);
        }
    }
}
