using System;
using System.IO;

namespace Empire_Earth_Launcher.Tests.TestSupport
{
    /// <summary>
    /// The root folder of the source tree: the first folder above the test assembly that contains
    /// <see cref="SolutionFileName"/>. Architecture tests read project, configuration and resource files from it.
    /// </summary>
    /// <remarks>
    /// The test program is always run from its build output inside the repository (CI, the verify script and
    /// Visual Studio all do that), so the solution is found by walking up from
    /// <c>Empire-Earth-Launcher.Tests\bin\&lt;Configuration&gt;\</c>. A copy of the test program outside the
    /// source tree cannot check the sources; every test that uses this class then fails with an explanation.
    /// </remarks>
    internal static class RepositoryRoot
    {
        /// <summary>File name of the solution that marks the root folder.</summary>
        public const string SolutionFileName = "Empire-Earth.sln";

        private static readonly Lazy<string> root = new Lazy<string>(Find);

        /// <summary>Full path of the root folder, without a trailing separator.</summary>
        /// <exception cref="InvalidOperationException">No folder above the test assembly contains the solution.</exception>
        public static string Path
        {
            get { return root.Value; }
        }

        /// <summary>Full path of the solution file.</summary>
        public static string SolutionFile
        {
            get { return GetFullPath(SolutionFileName); }
        }

        /// <summary>
        /// Full path of a path relative to the root folder. Both <c>\</c> (as written in solution and project
        /// files) and <c>/</c> are accepted as separators, so the tests run on Windows and under Mono.
        /// </summary>
        public static string GetFullPath(string relativePath)
        {
            return CombineRelative(Path, relativePath);
        }

        /// <summary>
        /// Full path of <paramref name="relativePath"/> relative to <paramref name="baseDirectory"/>, with both
        /// <c>\</c> and <c>/</c> accepted as separators.
        /// </summary>
        public static string CombineRelative(string baseDirectory, string relativePath)
        {
            if (baseDirectory == null)
                throw new ArgumentNullException(nameof(baseDirectory));
            if (relativePath == null)
                throw new ArgumentNullException(nameof(relativePath));

            string local = relativePath.Replace('\\', System.IO.Path.DirectorySeparatorChar)
                                       .Replace('/', System.IO.Path.DirectorySeparatorChar);
            return System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDirectory, local));
        }

        /// <summary>
        /// Path of <paramref name="fullPath"/> relative to the root folder, with <c>/</c> as separator (for
        /// messages and comparisons).
        /// </summary>
        public static string ToRelativePath(string fullPath)
        {
            string prefix = Path + System.IO.Path.DirectorySeparatorChar;
            string full = System.IO.Path.GetFullPath(fullPath);
            if (!full.StartsWith(prefix, StringComparison.Ordinal))
                throw new ArgumentException("The path is outside the repository: " + fullPath, nameof(fullPath));
            return full.Substring(prefix.Length).Replace(System.IO.Path.DirectorySeparatorChar, '/');
        }

        private static string Find()
        {
            string start = System.IO.Path.GetDirectoryName(typeof(RepositoryRoot).Assembly.Location);
            for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
            {
                if (File.Exists(System.IO.Path.Combine(directory.FullName, SolutionFileName)))
                    return directory.FullName.TrimEnd(System.IO.Path.DirectorySeparatorChar);
            }

            throw new InvalidOperationException(
                SolutionFileName + " was not found in " + start + " or a folder above it. The architecture tests " +
                "read the source tree and must run from a build output inside the repository.");
        }
    }
}
