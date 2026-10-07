using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>
    /// <see cref="IFileVersionReader"/> with <see cref="FileVersionInfo.GetVersionInfo(string)"/>: the four numbers of the
    /// fixed version resource, as Explorer shows them on the Details tab. The file is only read, never executed.
    /// </summary>
    public sealed class WindowsFileVersionReader : IFileVersionReader
    {
        public string GetFileVersion(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("A path is required.", nameof(path));
            try
            {
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
                return Format(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                // A path with characters Windows does not allow.
                return null;
            }
        }

        /// <summary>The version as text; null for 0.0.0.0, which is what a file without a version resource gives.</summary>
        public static string Format(int major, int minor, int build, int revision)
        {
            if (major == 0 && minor == 0 && build == 0 && revision == 0)
                return null;
            return string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}.{3}", major, minor, build, revision);
        }
    }
}
