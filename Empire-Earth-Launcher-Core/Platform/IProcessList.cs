namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>
    /// Tells whether a process of a program runs, for the hint about a hanging game (ADR 0010 amendment, forum report
    /// section 8 row 14, t=2815, t=5859). Only an extra information: whether a game runs is decided by its mutex. The
    /// launcher never ends a process.
    /// </summary>
    public interface IProcessList
    {
        /// <summary>
        /// True if a process of the program <paramref name="programFileName"/> (e.g. <c>Empire Earth.exe</c>) runs in any
        /// session; false if none runs or the list cannot be read.
        /// </summary>
        bool IsRunning(string programFileName);
    }
}
