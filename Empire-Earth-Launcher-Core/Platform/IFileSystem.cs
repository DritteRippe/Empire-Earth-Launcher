using System;
using System.Collections.Generic;
using System.IO;

namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>Outcome of a file system operation (ADR 0013: environment problems are results).</summary>
    public enum FileSystemStatus
    {
        /// <summary>The operation succeeded.</summary>
        Ok,
        /// <summary>The file, folder or drive does not exist.</summary>
        NotFound,
        /// <summary>Access denied (permissions, read-only file, a folder where a file was expected).</summary>
        AccessDenied,
        /// <summary>Any other I/O error (file in use, disk full, target already exists, device error).</summary>
        IoError,
        /// <summary>The path is not valid (characters, length, format).</summary>
        InvalidPath,
        /// <summary>The file is larger than the caller allows (<see cref="FileSystemExtensions.ReadAllBytes"/>).</summary>
        TooLarge
    }

    /// <summary>Status of a file system operation that returns no value.</summary>
    public readonly struct FileSystemResult
    {
        private FileSystemResult(FileSystemStatus status, string detail)
        {
            Status = status;
            Detail = detail;
        }

        /// <summary>A successful operation.</summary>
        public static FileSystemResult Success
        {
            get { return new FileSystemResult(FileSystemStatus.Ok, null); }
        }

        public FileSystemStatus Status { get; }

        /// <summary>The message of the underlying error, for the log; null on success.</summary>
        public string Detail { get; }

        public bool IsOk
        {
            get { return Status == FileSystemStatus.Ok; }
        }

        /// <summary>A failed operation.</summary>
        public static FileSystemResult Failure(FileSystemStatus status, string detail)
        {
            if (status == FileSystemStatus.Ok)
                throw new ArgumentException("A failure needs a status other than Ok.", nameof(status));
            return new FileSystemResult(status, detail);
        }

        public override string ToString()
        {
            return Detail == null ? Status.ToString() : Status + " (" + Detail + ")";
        }
    }

    /// <summary>Status and value of a file system operation.</summary>
    public readonly struct FileSystemResult<T>
    {
        private FileSystemResult(FileSystemStatus status, T value, string detail)
        {
            Status = status;
            Value = value;
            Detail = detail;
        }

        public FileSystemStatus Status { get; }

        /// <summary>The value; the default of <typeparamref name="T"/> unless <see cref="IsOk"/>.</summary>
        public T Value { get; }

        /// <summary>The message of the underlying error, for the log; null on success.</summary>
        public string Detail { get; }

        public bool IsOk
        {
            get { return Status == FileSystemStatus.Ok; }
        }

        public static FileSystemResult<T> Success(T value)
        {
            return new FileSystemResult<T>(FileSystemStatus.Ok, value, null);
        }

        public static FileSystemResult<T> Failure(FileSystemStatus status, string detail)
        {
            if (status == FileSystemStatus.Ok)
                throw new ArgumentException("A failure needs a status other than Ok.", nameof(status));
            return new FileSystemResult<T>(status, default, detail);
        }

        /// <summary>The status without the value.</summary>
        public FileSystemResult WithoutValue()
        {
            return IsOk ? FileSystemResult.Success : FileSystemResult.Failure(Status, Detail);
        }

        public override string ToString()
        {
            return Detail == null ? Status.ToString() : Status + " (" + Detail + ")";
        }
    }

    /// <summary>
    /// The kind of drive a path is on (<see cref="IFileSystem.GetDriveKind"/>). The registry cleanup offers the game settings
    /// of an installation only when its folder is missing from a present, fixed, local drive: a missing USB stick or network
    /// drive is not a removed installation (ADR 0007 amendment of the design review).
    /// </summary>
    public enum DriveKind
    {
        /// <summary>The drive does not exist or is not ready (no medium, disconnected).</summary>
        NotFound,
        /// <summary>A local hard disk or SSD.</summary>
        Fixed,
        /// <summary>A USB stick, a memory card or another removable drive.</summary>
        Removable,
        /// <summary>A network drive or a UNC path (<c>\\server\share</c>).</summary>
        Network,
        /// <summary>A CD, DVD or Blu-ray drive.</summary>
        Optical,
        /// <summary>A RAM disk.</summary>
        Ram,
        /// <summary>Windows does not know the kind, or it cannot be read.</summary>
        Unknown
    }

    /// <summary>Size, time and attributes of a file.</summary>
    public sealed class FileEntry
    {
        public FileEntry(long length, DateTime lastWriteTimeUtc, FileAttributes attributes)
        {
            Length = length;
            LastWriteTimeUtc = lastWriteTimeUtc;
            Attributes = attributes;
        }

        public long Length { get; }

        public DateTime LastWriteTimeUtc { get; }

        public FileAttributes Attributes { get; }

        public bool IsReadOnly
        {
            get { return (Attributes & FileAttributes.ReadOnly) != 0; }
        }
    }

    /// <summary>
    /// The file operations the core needs (ADR 0006), so that its logic runs against an in-memory file system
    /// in the tests. Paths are full paths; the core builds paths of installations with <see cref="WinPath"/>.
    /// </summary>
    /// <remarks>
    /// Nothing here throws for a missing file, denied access, an invalid path or an I/O error: those are
    /// returned as <see cref="FileSystemStatus"/> with the error message as detail (ADR 0013). Null arguments
    /// are programming errors and throw.
    /// </remarks>
    public interface IFileSystem
    {
        /// <summary>True if <paramref name="path"/> is an existing file; false for folders and on any error.</summary>
        bool FileExists(string path);

        /// <summary>True if <paramref name="path"/> is an existing folder; false on any error.</summary>
        bool DirectoryExists(string path);

        /// <summary>Size, last write time and attributes of a file.</summary>
        FileSystemResult<FileEntry> GetFileInfo(string path);

        /// <summary>
        /// Opens a file for reading; others may keep reading, writing, deleting and renaming it (the game may hold its
        /// files open, and a setup that starts meanwhile must be able to replace them, contract 4.2). The caller disposes
        /// the stream.
        /// </summary>
        FileSystemResult<Stream> OpenRead(string path);

        /// <summary>Full paths of the files directly in <paramref name="directory"/>, sorted ignoring case.</summary>
        FileSystemResult<IReadOnlyList<string>> GetFiles(string directory);

        /// <summary>Full paths of the folders directly in <paramref name="directory"/>, sorted ignoring case.</summary>
        FileSystemResult<IReadOnlyList<string>> GetDirectories(string directory);

        /// <summary>Creates the folder and its missing parents; succeeds if it exists already.</summary>
        FileSystemResult CreateDirectory(string path);

        /// <summary>
        /// Writes <paramref name="data"/> to a file, replacing it if it exists. The folder must exist. Use
        /// <see cref="FileSystemExtensions.WriteAllBytesAtomically"/> for files that must never be half written.
        /// </summary>
        FileSystemResult WriteAllBytes(string path, byte[] data);

        /// <summary>
        /// Replaces the existing file <paramref name="destination"/> by <paramref name="source"/> (which is moved),
        /// like <see cref="File.Replace(string, string, string)"/> without a backup.
        /// </summary>
        FileSystemResult Replace(string source, string destination);

        /// <summary>Moves or renames a file; fails if <paramref name="destination"/> exists.</summary>
        FileSystemResult Move(string source, string destination);

        /// <summary>Deletes a file; <see cref="FileSystemStatus.NotFound"/> if it does not exist.</summary>
        FileSystemResult DeleteFile(string path);

        /// <summary>
        /// The kind of the drive <paramref name="path"/> is on (its root; the path itself need not exist): network for a UNC
        /// path, <see cref="DriveKind.NotFound"/> for a drive that does not exist or is not ready. Never throws for a path.
        /// </summary>
        DriveKind GetDriveKind(string path);
    }
}
