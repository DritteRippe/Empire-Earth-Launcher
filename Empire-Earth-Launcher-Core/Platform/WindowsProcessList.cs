using System;
using System.ComponentModel;
using System.Diagnostics;
using Empire_Earth_Launcher.Core.Logging;

namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>
    /// <see cref="IProcessList"/> with <see cref="Process.GetProcessesByName(string)"/> (the file name without
    /// <c>.exe</c>). The processes are only counted, never opened for anything else, and their handles are released at once.
    /// </summary>
    public sealed class WindowsProcessList : IProcessList
    {
        private const string ProgramExtension = ".exe";

        private readonly ILogger logger;

        public WindowsProcessList(ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool IsRunning(string programFileName)
        {
            string name = ProcessName(programFileName);
            Process[] processes;
            try
            {
                processes = Process.GetProcessesByName(name);
            }
            catch (InvalidOperationException ex)
            {
                logger.Warning("Unable to list the processes named " + name + ".", ex);
                return false;
            }
            catch (Win32Exception ex)
            {
                logger.Warning("Unable to list the processes named " + name + ".", ex);
                return false;
            }

            foreach (Process process in processes)
                process.Dispose();
            return processes.Length > 0;
        }

        /// <summary>The process name Windows uses: the file name without the extension <c>.exe</c>.</summary>
        public static string ProcessName(string programFileName)
        {
            if (string.IsNullOrEmpty(programFileName) || programFileName.IndexOfAny(new[] { '\\', '/' }) >= 0)
                throw new ArgumentException("A file name of a program is required: " + programFileName, nameof(programFileName));
            return programFileName.EndsWith(ProgramExtension, StringComparison.OrdinalIgnoreCase)
                ? programFileName.Substring(0, programFileName.Length - ProgramExtension.Length)
                : programFileName;
        }
    }
}
