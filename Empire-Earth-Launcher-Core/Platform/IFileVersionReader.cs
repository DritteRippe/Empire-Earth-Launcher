namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>
    /// Reads the file version of a program (the version resource that Explorer shows as "File version"), for the Play page
    /// and the diagnostics report (ADR 0010 amendment, forum report section 8 row 1).
    /// </summary>
    public interface IFileVersionReader
    {
        /// <summary>
        /// The file version of <paramref name="path"/> as <c>major.minor.build.revision</c> (the numbers of the version
        /// resource, e.g. <c>2.0.0.2949</c>); null if the file does not exist, cannot be read or has no version.
        /// </summary>
        string GetFileVersion(string path);
    }
}
