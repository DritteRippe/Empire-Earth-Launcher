using System.Collections.Generic;
using System.IO;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Tests.Fakes
{
    /// <summary>
    /// <see cref="IFileSystem"/> that passes everything to another file system and counts the files that were written, so that a
    /// test can tell how often something was saved (<c>settings.json</c> is written through a temporary file once per save).
    /// </summary>
    internal sealed class CountingFileSystem : IFileSystem
    {
        private readonly IFileSystem inner;
        private readonly List<string> written = new List<string>();

        public CountingFileSystem(IFileSystem inner)
        {
            this.inner = inner;
        }

        /// <summary>The paths of every <see cref="WriteAllBytes"/> so far, in order.</summary>
        public IReadOnlyList<string> Written
        {
            get { return written; }
        }

        /// <summary>How often <paramref name="path"/> was saved through the temporary file of an atomic write.</summary>
        public int Saves(string path)
        {
            string temporary = path + ".tmp";
            int count = 0;
            foreach (string file in written)
            {
                if (string.Equals(file, temporary, System.StringComparison.OrdinalIgnoreCase))
                    count++;
            }
            return count;
        }

        public bool FileExists(string path)
        {
            return inner.FileExists(path);
        }

        public bool DirectoryExists(string path)
        {
            return inner.DirectoryExists(path);
        }

        public FileSystemResult<FileEntry> GetFileInfo(string path)
        {
            return inner.GetFileInfo(path);
        }

        public FileSystemResult<Stream> OpenRead(string path)
        {
            return inner.OpenRead(path);
        }

        public FileSystemResult<IReadOnlyList<string>> GetFiles(string directory)
        {
            return inner.GetFiles(directory);
        }

        public FileSystemResult<IReadOnlyList<string>> GetDirectories(string directory)
        {
            return inner.GetDirectories(directory);
        }

        public FileSystemResult CreateDirectory(string path)
        {
            return inner.CreateDirectory(path);
        }

        public FileSystemResult WriteAllBytes(string path, byte[] data)
        {
            written.Add(path);
            return inner.WriteAllBytes(path, data);
        }

        public FileSystemResult Replace(string source, string destination)
        {
            return inner.Replace(source, destination);
        }

        public FileSystemResult Move(string source, string destination)
        {
            return inner.Move(source, destination);
        }

        public FileSystemResult DeleteFile(string path)
        {
            return inner.DeleteFile(path);
        }

        public DriveKind GetDriveKind(string path)
        {
            return inner.GetDriveKind(path);
        }
    }
}
