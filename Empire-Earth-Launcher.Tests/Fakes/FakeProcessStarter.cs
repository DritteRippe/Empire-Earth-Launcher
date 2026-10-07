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
        private readonly List<string> openedFolders = new List<string>();
        private readonly List<string> openedFiles = new List<string>();

        /// <summary>The process id the next starts return; null for "no process" (default 4242).</summary>
        public int? ProcessId { get; set; } = 4242;

        /// <summary>Thrown by <see cref="StartProgram"/> instead of starting, if set.</summary>
        public Exception StartException { get; set; }

        /// <summary>Thrown by <see cref="OpenUrl"/>, <see cref="OpenFolder"/> and <see cref="OpenFile"/> instead of opening, if set.</summary>
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

        /// <summary>Every folder opened in the Explorer, in order.</summary>
        public IReadOnlyList<string> OpenedFolders
        {
            get { return openedFolders.ToList(); }
        }

        /// <summary>Every document opened with its program, in order.</summary>
        public IReadOnlyList<string> OpenedFiles
        {
            get { return openedFiles.ToList(); }
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

        public void OpenFolder(string folder)
        {
            OnCall?.Invoke("folder " + folder);
            ShellProcessStarter.CreateFolderStartInfo(folder);
            if (OpenException != null)
                throw OpenException;
            openedFolders.Add(folder);
        }

        public void OpenFile(string file)
        {
            OnCall?.Invoke("file " + file);
            ShellProcessStarter.CreateFileStartInfo(file);
            if (OpenException != null)
                throw OpenException;
            openedFiles.Add(file);
        }
    }
}
