using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Tests.TestSupport
{
    /// <summary>
    /// The real file system (<see cref="LocalFileSystem"/>) behind a drive letter: <c>T:\Games\EE\a.ees</c> is the file
    /// <c>Games/EE/a.ees</c> below a <see cref="TemporaryDirectory"/>. The core works with Windows paths (<see cref="WinPath"/>),
    /// so this lets its file operations run against real files under Windows and Mono alike (the maintenance tests of
    /// L-WP8: moving, exporting and importing real files). Other drives do not exist; the mapped drive is fixed.
    /// </summary>
    /// <remarks>Names keep their case on the way to the disk; the tests spell every path the same way.</remarks>
    internal sealed class MappedFileSystem : IFileSystem
    {
        private readonly LocalFileSystem inner = new LocalFileSystem();
        private readonly string drive;
        private readonly string hostRoot;

        /// <param name="drive">The drive letter with colon, e.g. <c>T:</c>.</param>
        /// <param name="hostRoot">The folder the drive stands for (a <see cref="TemporaryDirectory"/>).</param>
        public MappedFileSystem(string drive, string hostRoot)
        {
            this.drive = drive ?? throw new ArgumentNullException(nameof(drive));
            this.hostRoot = hostRoot ?? throw new ArgumentNullException(nameof(hostRoot));
        }

        /// <summary>The host path of a Windows path on the mapped drive; null for any other path.</summary>
        public string ToHost(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (!WinPath.IsFullyQualified(path) || !string.Equals(WinPath.GetDrive(path), drive, StringComparison.OrdinalIgnoreCase))
                return null;
            string relative = WinPath.Normalize(path).Substring(drive.Length).TrimStart('\\');
            return relative.Length == 0
                ? hostRoot
                : Path.Combine(new[] { hostRoot }.Concat(relative.Split('\\')).ToArray());
        }

        private string ToWindows(string hostPath)
        {
            string relative = hostPath.Substring(hostRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return drive + @"\" + relative.Replace(Path.DirectorySeparatorChar, '\\');
        }

        public bool FileExists(string path)
        {
            string host = ToHost(path);
            return host != null && inner.FileExists(host);
        }

        public bool DirectoryExists(string path)
        {
            string host = ToHost(path);
            return host != null && inner.DirectoryExists(host);
        }

        public FileSystemResult<FileEntry> GetFileInfo(string path)
        {
            string host = ToHost(path);
            return host == null ? FileSystemResult<FileEntry>.Failure(FileSystemStatus.NotFound, "no such drive") : inner.GetFileInfo(host);
        }

        public FileSystemResult<Stream> OpenRead(string path)
        {
            string host = ToHost(path);
            return host == null ? FileSystemResult<Stream>.Failure(FileSystemStatus.NotFound, "no such drive") : inner.OpenRead(host);
        }

        public FileSystemResult<IReadOnlyList<string>> GetFiles(string directory)
        {
            return Map(directory, inner.GetFiles);
        }

        public FileSystemResult<IReadOnlyList<string>> GetDirectories(string directory)
        {
            return Map(directory, inner.GetDirectories);
        }

        public FileSystemResult CreateDirectory(string path)
        {
            string host = ToHost(path);
            return host == null ? FileSystemResult.Failure(FileSystemStatus.NotFound, "no such drive") : inner.CreateDirectory(host);
        }

        public FileSystemResult WriteAllBytes(string path, byte[] data)
        {
            string host = ToHost(path);
            return host == null ? FileSystemResult.Failure(FileSystemStatus.NotFound, "no such drive") : inner.WriteAllBytes(host, data);
        }

        public FileSystemResult Replace(string source, string destination)
        {
            string from = ToHost(source);
            string to = ToHost(destination);
            return from == null || to == null ? FileSystemResult.Failure(FileSystemStatus.NotFound, "no such drive") : inner.Replace(from, to);
        }

        public FileSystemResult Move(string source, string destination)
        {
            string from = ToHost(source);
            string to = ToHost(destination);
            return from == null || to == null ? FileSystemResult.Failure(FileSystemStatus.NotFound, "no such drive") : inner.Move(from, to);
        }

        public FileSystemResult DeleteFile(string path)
        {
            string host = ToHost(path);
            return host == null ? FileSystemResult.Failure(FileSystemStatus.NotFound, "no such drive") : inner.DeleteFile(host);
        }

        public DriveKind GetDriveKind(string path)
        {
            return ToHost(path) == null ? DriveKind.NotFound : DriveKind.Fixed;
        }

        private FileSystemResult<IReadOnlyList<string>> Map(string directory, Func<string, FileSystemResult<IReadOnlyList<string>>> list)
        {
            string host = ToHost(directory);
            if (host == null)
                return FileSystemResult<IReadOnlyList<string>>.Failure(FileSystemStatus.NotFound, "no such drive");
            FileSystemResult<IReadOnlyList<string>> result = list(host);
            if (!result.IsOk)
                return result;
            return FileSystemResult<IReadOnlyList<string>>.Success(result.Value.Select(ToWindows).ToList());
        }
    }
}
