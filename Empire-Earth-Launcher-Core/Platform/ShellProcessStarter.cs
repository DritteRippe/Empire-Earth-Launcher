using System;
using System.Diagnostics;

namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>
    /// <see cref="IProcessStarter"/> with <see cref="Process.Start(ProcessStartInfo)"/> and <c>UseShellExecute = true</c>
    /// (ADR 0010). A plain <c>CreateProcess</c> (without the shell) would ignore the compatibility layers of
    /// the game and fail with error 740 when one of them asks for elevation (contract 3.7).
    /// </summary>
    /// <remarks>
    /// Only the <see cref="ProcessStartInfo"/> is built here (<see cref="CreateProgramStartInfo"/>,
    /// <see cref="CreateUrlStartInfo"/>, unit-tested); starting is checked on real Windows by the test plan (WP6 cases).
    /// <see cref="Process.Start(ProcessStartInfo)"/> calls <c>ShellExecuteEx</c> on an STA thread of its own when the
    /// caller is not on one, so the core may start a game from the thread pool.
    /// </remarks>
    public sealed class ShellProcessStarter : IProcessStarter
    {
        public int? StartProgram(string programPath, string workingDirectory)
        {
            using (Process process = Process.Start(CreateProgramStartInfo(programPath, workingDirectory)))
                return TryGetId(process);
        }

        public void OpenUrl(string url)
        {
            using (Process.Start(CreateUrlStartInfo(url)))
            {
                // The browser runs on its own; the launcher keeps no handle.
            }
        }

        /// <summary>
        /// The start of a game: the program through the shell, the game folder as working folder, no arguments, no verb
        /// (no "runas": the launcher never asks for elevation itself) and no error dialog of the shell (the launcher shows
        /// its own localized message).
        /// </summary>
        public static ProcessStartInfo CreateProgramStartInfo(string programPath, string workingDirectory)
        {
            if (!WinPath.IsFullyQualified(programPath))
                throw new ArgumentException("A full path of the program is required: " + programPath, nameof(programPath));
            if (!WinPath.IsFullyQualified(workingDirectory))
                throw new ArgumentException("A full path of the working folder is required: " + workingDirectory,
                    nameof(workingDirectory));
            return new ProcessStartInfo
            {
                FileName = programPath,
                WorkingDirectory = workingDirectory,
                Arguments = string.Empty,
                UseShellExecute = true,
                Verb = string.Empty,
                ErrorDialog = false
            };
        }

        /// <summary>
        /// The opening of a web page: an absolute <c>https://</c> URL through the shell, no verb, no error dialog. Any other
        /// scheme is refused, so a value from outside can never start a program (contract 4.3: never <c>http://</c>).
        /// </summary>
        public static ProcessStartInfo CreateUrlStartInfo(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw new ArgumentException("Only an absolute https URL can be opened: " + url, nameof(url));
            return new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true,
                Verb = string.Empty,
                ErrorDialog = false
            };
        }

        /// <summary>The id of the started process; null if there is none or it cannot be read (ADR 0010 amendment).</summary>
        private static int? TryGetId(Process process)
        {
            if (process == null)
                return null;
            try
            {
                return process.Id;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }
        }
    }
}
