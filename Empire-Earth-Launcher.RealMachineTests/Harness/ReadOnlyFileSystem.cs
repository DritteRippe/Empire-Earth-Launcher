using System.Collections.Generic;
using System.IO;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.RealMachineTests.Harness
{
    /// <summary>
    /// <see cref="IFileSystem"/> that passes reads to another file system and refuses every change
    /// (<see cref="HarnessViolations"/>): the game files of an installation are only ever read.
    /// </summary>
    internal sealed class ReadOnlyFileSystem : IFileSystem
    {
        private readonly IFileSystem inner;
        private readonly HarnessViolations violations;

        public ReadOnlyFileSystem(IFileSystem inner, HarnessViolations violations)
        {
            this.inner = inner ?? throw new System.ArgumentNullException(nameof(inner));
            this.violations = violations ?? throw new System.ArgumentNullException(nameof(violations));
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
            throw violations.Add("CreateDirectory " + path + " by read-only code");
        }

        public FileSystemResult WriteAllBytes(string path, byte[] data)
        {
            throw violations.Add("WriteAllBytes " + path + " by read-only code");
        }

        public FileSystemResult Replace(string source, string destination)
        {
            throw violations.Add("Replace " + destination + " by read-only code");
        }

        public FileSystemResult Move(string source, string destination)
        {
            throw violations.Add("Move " + source + " by read-only code");
        }

        public FileSystemResult DeleteFile(string path)
        {
            throw violations.Add("DeleteFile " + path + " by read-only code");
        }

        public DriveKind GetDriveKind(string path)
        {
            return inner.GetDriveKind(path);
        }
    }
}
