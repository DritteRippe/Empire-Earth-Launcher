using System;
using System.Collections.Generic;
using System.Linq;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Tests.Fakes
{
    /// <summary>
    /// <see cref="IProcessStarter"/> that starts nothing: it records every program start and every URL, answers with a
    /// configurable process id (null = "pid unknown", ADR 0010 amendment) or throws a configured exception, as
    /// <c>Process.Start</c> would.
    /// </summary>
    internal sealed class FakeProcessStarter : IProcessStarter
    {
        private readonly List<Tuple<string, string>> started = new List<Tuple<string, string>>();
        private readonly List<string> openedUrls = new List<string>();

        /// <summary>The process id the next starts return; null for "no process" (default 4242).</summary>
        public int? ProcessId { get; set; } = 4242;

        /// <summary>Thrown by <see cref="StartProgram"/> instead of starting, if set.</summary>
        public Exception StartException { get; set; }

        /// <summary>Thrown by <see cref="OpenUrl"/> instead of opening, if set.</summary>
        public Exception OpenException { get; set; }

        /// <summary>Called first by every method with "start &lt;path&gt;" or "open &lt;url&gt;" (order tests).</summary>
        public Action<string> OnCall { get; set; }

        /// <summary>Every program start: program path and working folder, in order.</summary>
        public IReadOnlyList<Tuple<string, string>> Started
        {
            get { return started.ToList(); }
        }

        /// <summary>Every URL opened, in order.</summary>
        public IReadOnlyList<string> OpenedUrls
        {
            get { return openedUrls.ToList(); }
        }

        public int? StartProgram(string programPath, string workingDirectory)
        {
            OnCall?.Invoke("start " + programPath);
            // The same checks as the real starter, so that a wrong path fails in the tests too.
            ShellProcessStarter.CreateProgramStartInfo(programPath, workingDirectory);
            if (StartException != null)
                throw StartException;
            started.Add(Tuple.Create(programPath, workingDirectory));
            return ProcessId;
        }

        public void OpenUrl(string url)
        {
            OnCall?.Invoke("open " + url);
            ShellProcessStarter.CreateUrlStartInfo(url);
            if (OpenException != null)
                throw OpenException;
            openedUrls.Add(url);
        }
    }
}
