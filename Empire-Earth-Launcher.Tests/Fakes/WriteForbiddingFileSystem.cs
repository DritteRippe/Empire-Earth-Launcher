using System.Collections.Generic;
using System.IO;
using Empire_Earth_Launcher.Core.Platform;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Fakes
{
    /// <summary>
    /// <see cref="IFileSystem"/> that passes reads to another file system and fails the test at the first change: for
    /// code that must only read (contract 1.4, "Read-only").
    /// </summary>
    internal sealed class WriteForbiddingFileSystem : IFileSystem
    {
        private readonly IFileSystem inner;

        public WriteForbiddingFileSystem(IFileSystem inner)
        {
            this.inner = inner;
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
            throw new AssertionException("CreateDirectory " + path + " called by read-only code.");
        }

        public FileSystemResult WriteAllBytes(string path, byte[] data)
        {
            throw new AssertionException("WriteAllBytes " + path + " called by read-only code.");
        }

        public FileSystemResult Replace(string source, string destination)
        {
            throw new AssertionException("Replace " + destination + " called by read-only code.");
        }

        public FileSystemResult Move(string source, string destination)
        {
            throw new AssertionException("Move " + source + " called by read-only code.");
        }

        public FileSystemResult DeleteFile(string path)
        {
            throw new AssertionException("DeleteFile " + path + " called by read-only code.");
        }

        public DriveKind GetDriveKind(string path)
        {
            return inner.GetDriveKind(path);
        }
    }
}
