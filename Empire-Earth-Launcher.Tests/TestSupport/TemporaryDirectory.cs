using System;
using System.IO;

namespace Empire_Earth_Launcher.Tests.TestSupport
{
    /// <summary>
    /// A new, empty folder below the temporary folder of the user, deleted with everything in it on
    /// <see cref="Dispose"/>. Tests never write anywhere else.
    /// </summary>
    internal sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "Empire-Earth-Launcher.Tests-" + Guid.NewGuid().ToString("N"));
            // The full path of the existing folder: the temporary folder may be given as a Windows short
            // (8.3) path, which Path.GetFullPath expands, and the code under test calls GetFullPath.
            Path = Directory.CreateDirectory(path).FullName;
        }

        /// <summary>Full path of the folder.</summary>
        public string Path { get; }

        /// <summary>Full path of <paramref name="relativePath"/> inside the folder.</summary>
        public string Combine(string relativePath)
        {
            return System.IO.Path.Combine(Path, relativePath);
        }

        /// <summary>Creates a file (and its folders) inside the folder.</summary>
        /// <returns>Full path of the file.</returns>
        public string CreateFile(string relativePath, string content = "")
        {
            string fullPath = Combine(relativePath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, content);
            return fullPath;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, true);
        }
    }
}
