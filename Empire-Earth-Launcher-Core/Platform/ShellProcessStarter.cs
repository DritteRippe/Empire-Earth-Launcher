using System;
using System.Collections.Generic;
using System.ComponentModel;
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
        /// <summary>
        /// The only documents <see cref="CreateFileStartInfo"/> opens: text and configuration files. An allow-list, not a list of
        /// what Windows runs: the shell runs far more than a list of programs can name (and takes <c>.exe.</c> or <c>.exe </c>
        /// for <c>.exe</c>), so anything that is not named here is refused.
        /// </summary>
        private static readonly HashSet<string> OpenableExtensions = new HashSet<string>(StringComparer.Ordinal)
        {
            ".config", ".conf", ".txt", ".ini", ".log"
        };

        /// <summary>
        /// <c>ERROR_NO_ASSOCIATION</c> (1155): no program is registered for the extension, as for <c>.config</c> on a stock
        /// Windows. The shell then reports it as a <see cref="Win32Exception"/> and shows no dialog (<c>ErrorDialog</c> is off).
        /// </summary>
        private const int NoAssociation = 1155;

        /// <summary>The verb of the "Open with" dialog, the way out when no program is registered for a document.</summary>
        public const string OpenAsVerb = "openas";

        private readonly Func<ProcessStartInfo, Process> start;

        public ShellProcessStarter()
            : this(StartWithShell)
        {
        }

        /// <summary>The one call of <see cref="Process.Start(ProcessStartInfo)"/> of the launcher (checked by ProcessRulesTests).</summary>
        private static Process StartWithShell(ProcessStartInfo info)
        {
            return Process.Start(info);
        }

        /// <param name="start">What starts a process; the tests replace it to see the start information and to fail like Windows.</param>
        internal ShellProcessStarter(Func<ProcessStartInfo, Process> start)
        {
            this.start = start ?? throw new ArgumentNullException(nameof(start));
        }

        public int? StartProgram(string programPath, string workingDirectory)
        {
            using (Process process = start(CreateProgramStartInfo(programPath, workingDirectory)))
                return TryGetId(process);
        }

        public void OpenUrl(string url)
        {
            using (start(CreateUrlStartInfo(url)))
            {
                // The browser runs on its own; the launcher keeps no handle.
            }
        }

        public void OpenFolder(string folder)
        {
            using (start(CreateFolderStartInfo(folder)))
            {
                // The Explorer runs on its own; the launcher keeps no handle.
            }
        }

        public void OpenFile(string file)
        {
            try
            {
                using (start(CreateFileStartInfo(file)))
                {
                    // The program of the document runs on its own; the launcher keeps no handle.
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == NoAssociation)
            {
                // No program is registered (a stock Windows has none for .config): let the player choose one in the dialog of
                // Windows instead of ending with an error message.
                using (start(CreateFileStartInfo(file, OpenAsVerb)))
                {
                    // The dialog and the program run on their own.
                }
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
        /// The opening of a document: the full path of the file through the shell, no arguments, no error dialog, and no verb
        /// unless <paramref name="verb"/> is <see cref="OpenAsVerb"/> (the retry when no program is registered). Only a text or
        /// configuration file is opened (<c>.config</c>, <c>.conf</c>, <c>.txt</c>, <c>.ini</c>, <c>.log</c>); anything else, a
        /// program, a script, a shortcut or a file of an unknown kind, is refused: programs are started by
        /// <see cref="CreateProgramStartInfo"/> with the compatibility layers of the game, and nothing else is ever run.
        /// </summary>
        public static ProcessStartInfo CreateFileStartInfo(string file, string verb = "")
        {
            if (!WinPath.IsFullyQualified(file))
                throw new ArgumentException("A full path of the file is required: " + file, nameof(file));
            if (verb != string.Empty && verb != OpenAsVerb)
                throw new ArgumentException("Only no verb or " + OpenAsVerb + " is allowed: " + verb, nameof(verb));
            string normalized = WinPath.Normalize(file);
            if (!OpenableExtensions.Contains(WinPath.GetExtension(normalized).ToLowerInvariant()))
                throw new ArgumentException("Only a text or configuration file can be opened, not a program, a script or another " +
                                            "kind of file: " + file, nameof(file));
            return new ProcessStartInfo
            {
                FileName = normalized,
                Arguments = string.Empty,
                UseShellExecute = true,
                Verb = verb,
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
