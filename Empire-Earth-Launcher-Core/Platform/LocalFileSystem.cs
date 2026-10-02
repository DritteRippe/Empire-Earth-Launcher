using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;

namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>
    /// <see cref="IFileSystem"/> on the real file system (<see cref="System.IO"/>). A thin adapter: it only
    /// translates calls and the documented exceptions into <see cref="FileSystemStatus"/> values.
    /// </summary>
    public sealed class LocalFileSystem : IFileSystem
    {
        /// <summary>
        /// The sharing of <see cref="OpenRead"/>: others may read, write, delete and rename the file while the launcher reads
        /// it (ADR 0016 plan review, contract 4.2). The game may hold its files open, and a setup that starts while the
        /// integrity check hashes a file must be able to delete or replace it (otherwise "DeleteFile failed; code 32").
        /// </summary>
        internal const FileShare ReadShare = FileShare.ReadWrite | FileShare.Delete;

        public bool FileExists(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            return File.Exists(path);
        }

        public bool DirectoryExists(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            return Directory.Exists(path);
        }

        public FileSystemResult<FileEntry> GetFileInfo(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists)
                    return FileSystemResult<FileEntry>.Failure(FileSystemStatus.NotFound, "The file does not exist.");
                return FileSystemResult<FileEntry>.Success(
                    new FileEntry(info.Length, info.LastWriteTimeUtc, info.Attributes));
            }
            catch (Exception ex) when (IsFileSystemError(ex))
            {
                return FileSystemResult<FileEntry>.Failure(ToStatus(ex), ex.Message);
            }
        }

        public FileSystemResult<Stream> OpenRead(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            try
            {
                Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, ReadShare);
                return FileSystemResult<Stream>.Success(stream);
            }
            catch (Exception ex) when (IsFileSystemError(ex))
            {
                return FileSystemResult<Stream>.Failure(ToStatus(ex), ex.Message);
            }
        }

        public FileSystemResult<IReadOnlyList<string>> GetFiles(string directory)
        {
            if (directory == null)
                throw new ArgumentNullException(nameof(directory));
            return List(() => Directory.GetFiles(directory));
        }

        public FileSystemResult<IReadOnlyList<string>> GetDirectories(string directory)
        {
            if (directory == null)
                throw new ArgumentNullException(nameof(directory));
            return List(() => Directory.GetDirectories(directory));
        }

        public FileSystemResult CreateDirectory(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            return Run(() => Directory.CreateDirectory(path));
        }

        public FileSystemResult WriteAllBytes(string path, byte[] data)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            return Run(() => File.WriteAllBytes(path, data));
        }

        public FileSystemResult Replace(string source, string destination)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            return Run(() => File.Replace(source, destination, null));
        }

        public FileSystemResult Move(string source, string destination)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            return Run(() => File.Move(source, destination));
        }

        public FileSystemResult DeleteFile(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            return Run(() =>
            {
                // File.Delete does not report a missing file.
                if (!File.Exists(path))
                    throw new FileNotFoundException("The file does not exist.", path);
                File.Delete(path);
            });
        }

        private static FileSystemResult<IReadOnlyList<string>> List(Func<string[]> list)
        {
            try
            {
                IReadOnlyList<string> entries = list().OrderBy(entry => entry, StringComparer.OrdinalIgnoreCase).ToList();
                return FileSystemResult<IReadOnlyList<string>>.Success(entries);
            }
            catch (Exception ex) when (IsFileSystemError(ex))
            {
                return FileSystemResult<IReadOnlyList<string>>.Failure(ToStatus(ex), ex.Message);
            }
        }

        private static FileSystemResult Run(Action action)
        {
            try
            {
                action();
                return FileSystemResult.Success;
            }
            catch (Exception ex) when (IsFileSystemError(ex))
            {
                return FileSystemResult.Failure(ToStatus(ex), ex.Message);
            }
        }

        /// <summary>The exceptions <see cref="System.IO"/> documents for its file operations.</summary>
        private static bool IsFileSystemError(Exception ex)
        {
            return ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException ||
                   ex is ArgumentException || ex is NotSupportedException;
        }

        private static FileSystemStatus ToStatus(Exception ex)
        {
            switch (ex)
            {
                case FileNotFoundException _:
                case DirectoryNotFoundException _:
                case DriveNotFoundException _:
                    return FileSystemStatus.NotFound;
                case UnauthorizedAccessException _:
                case SecurityException _:
                    return FileSystemStatus.AccessDenied;
                case PathTooLongException _:
                case ArgumentException _:
                case NotSupportedException _:
                    return FileSystemStatus.InvalidPath;
                default:
                    return FileSystemStatus.IoError;
            }
        }
    }
}
