using System;
using System.Collections.Generic;
using System.IO;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.RealMachineTests.Harness
{
    /// <summary>
    /// <see cref="IFileSystem"/> that reads everywhere and changes files only below the work folder of the step (the
    /// <c>.reg</c> backups of the reset, the saved setup values); any other change is refused
    /// (<see cref="HarnessViolations"/>). The work folder lies outside every installation and is never uploaded.
    /// </summary>
    internal sealed class WorkFolderFileSystem : IFileSystem
    {
        private readonly IFileSystem inner;
        private readonly HarnessViolations violations;

        public WorkFolderFileSystem(IFileSystem inner, string workFolder, HarnessViolations violations)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.violations = violations ?? throw new ArgumentNullException(nameof(violations));
            if (workFolder == null)
                throw new ArgumentNullException(nameof(workFolder));
            if (!WinPath.IsFullyQualified(workFolder))
                throw new ArgumentException("The work folder must be a full path: " + workFolder, nameof(workFolder));
            WorkFolder = WinPath.Normalize(workFolder);
        }

        public string WorkFolder { get; }

        /// <summary>True if <paramref name="path"/> is the work folder or below it.</summary>
        public bool IsInWorkFolder(string path)
        {
            return path != null && WinPath.IsSameOrBelow(path, WorkFolder);
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
            Demand("CreateDirectory", path);
            return inner.CreateDirectory(path);
        }

        public FileSystemResult WriteAllBytes(string path, byte[] data)
        {
            Demand("WriteAllBytes", path);
            return inner.WriteAllBytes(path, data);
        }

        public FileSystemResult Replace(string source, string destination)
        {
            Demand("Replace", source);
            Demand("Replace", destination);
            return inner.Replace(source, destination);
        }

        public FileSystemResult Move(string source, string destination)
        {
            Demand("Move", source);
            Demand("Move", destination);
            return inner.Move(source, destination);
        }

        public FileSystemResult DeleteFile(string path)
        {
            Demand("DeleteFile", path);
            return inner.DeleteFile(path);
        }

        public DriveKind GetDriveKind(string path)
        {
            return inner.GetDriveKind(path);
        }

        private void Demand(string operation, string path)
        {
            if (!IsInWorkFolder(path))
                throw violations.Add(operation + " " + path + " outside the work folder " + WorkFolder);
        }
    }
}
