namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>
    /// Starts the games and opens web pages through the Windows shell (<c>ShellExecute</c>), with the rights of the launcher
    /// (ADR 0010, contract 3.7 and 4.3). The launcher never asks for elevation: a program is started without a verb, so an
    /// elevation is only requested if the compatibility layers of the program ask for it (<c>RUNASADMIN</c>), and then by
    /// Windows, not by the launcher.
    /// </summary>
    /// <remarks>
    /// The methods throw what <see cref="System.Diagnostics.Process.Start(System.Diagnostics.ProcessStartInfo)"/> throws
    /// (<see cref="System.ComponentModel.Win32Exception"/> with the Windows error code, e.g. 1223 when the user cancelled
    /// the UAC prompt, 2 or 3 for a missing file, 5 for denied access; <see cref="System.IO.FileNotFoundException"/>;
    /// <see cref="System.InvalidOperationException"/>); the callers in the core turn them into results (ADR 0013).
    /// </remarks>
    public interface IProcessStarter
    {
        /// <summary>
        /// Starts <paramref name="programPath"/> with shell execute semantics (<c>UseShellExecute = true</c>), with
        /// <paramref name="workingDirectory"/> as working folder and without arguments, so that every compatibility layer
        /// of the program applies (contract 3.7).
        /// </summary>
        /// <param name="programPath">Full path of the program.</param>
        /// <param name="workingDirectory">Full path of the folder the program runs in (the real game folder).</param>
        /// <returns>
        /// The process id, or null if none is known: <c>Process.Start</c> returned no process (an existing process took
        /// over the request) or the id of the process cannot be read (ADR 0010 amendment).
        /// </returns>
        int? StartProgram(string programPath, string workingDirectory);

        /// <summary>
        /// Opens <paramref name="url"/> (an absolute <c>https://</c> URL) in the default browser, through the shell and
        /// without a verb, i.e. with the rights of the launcher and not elevated (contract 4.3 step 4).
        /// </summary>
        void OpenUrl(string url);
    }
}
