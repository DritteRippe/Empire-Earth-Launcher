using System;
using System.Collections.Generic;
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
        /// <summary>What the shell runs when asked to open it: <see cref="CreateFileStartInfo"/> refuses these.</summary>
        private static readonly HashSet<string> RunnableExtensions = new HashSet<string>(StringComparer.Ordinal)
        {
            ".exe", ".com", ".scr", ".pif", ".bat", ".cmd", ".msi", ".msp", ".lnk", ".url", ".ps1", ".psm1", ".vbs", ".vbe", ".js",
            ".jse", ".wsf", ".wsh", ".hta", ".cpl", ".msc", ".reg", ".jar", ".dll", ".appref-ms", ".application", ".gadget"
        };

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

        public void OpenFolder(string folder)
        {
            using (Process.Start(CreateFolderStartInfo(folder)))
            {
                // The Explorer runs on its own; the launcher keeps no handle.
            }
        }

        public void OpenFile(string file)
        {
            using (Process.Start(CreateFileStartInfo(file)))
            {
                // The program of the document runs on its own; the launcher keeps no handle.
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

        /// <summary>
        /// The opening of a folder in the Explorer: the full path of the folder through the shell, no verb, no arguments, no
        /// error dialog. The path must end with a separator, so that the shell can only take it as a folder and never as a
        /// program of that name; anything but a full path is refused.
        /// </summary>
        public static ProcessStartInfo CreateFolderStartInfo(string folder)
        {
            if (!WinPath.IsFullyQualified(folder))
                throw new ArgumentException("A full path of the folder is required: " + folder, nameof(folder));
            return new ProcessStartInfo
            {
                FileName = WinPath.Normalize(folder).TrimEnd(WinPath.Separator) + WinPath.Separator,
                Arguments = string.Empty,
                UseShellExecute = true,
                Verb = string.Empty,
                ErrorDialog = false
            };
        }

        /// <summary>
        /// The opening of a document: the full path of the file through the shell, no verb, no arguments, no error dialog. A
        /// file that Windows runs (a program, a script, a shortcut, a registry file) is refused: programs are started by
        /// <see cref="CreateProgramStartInfo"/> with the compatibility layers of the game, and nothing else is ever run.
        /// </summary>
        public static ProcessStartInfo CreateFileStartInfo(string file)
        {
            if (!WinPath.IsFullyQualified(file))
                throw new ArgumentException("A full path of the file is required: " + file, nameof(file));
            string normalized = WinPath.Normalize(file);
            if (RunnableExtensions.Contains(WinPath.GetExtension(normalized).ToLowerInvariant()))
                throw new ArgumentException("Only a document can be opened, not a program or a script: " + file, nameof(file));
            return new ProcessStartInfo
            {
                FileName = normalized,
                Arguments = string.Empty,
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
