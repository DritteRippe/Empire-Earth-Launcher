using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Tests.Fakes
{
    /// <summary>Operations of <see cref="InMemoryFileSystem"/> that a test can make fail.</summary>
    internal enum FileSystemOperation
    {
        GetInfo,
        Read,
        Enumerate,
        CreateDirectory,
        Write,
        Replace,
        Move,
        Delete
    }

    /// <summary>
    /// <see cref="IFileSystem"/> in memory with the path rules of Windows (ADR 0006): full paths only
    /// (<c>C:\...</c> or <c>\\server\share\...</c>), <c>\</c> and <c>/</c> as separators, names compared ignoring
    /// case, files and folders kept with the spelling they were created with.
    /// </summary>
    /// <remarks>
    /// Behaves like NTFS where the core relies on it: writing needs an existing folder, a read-only file cannot
    /// be written, replaced, moved over or deleted, <see cref="Move"/> fails if the target exists,
    /// <see cref="Replace"/> needs an existing target. Tests can inject a failure of any operation on a path
    /// (<see cref="FailOn"/>) and count how often a file was opened (<see cref="OpenCount"/>). Drive <c>C:</c>
    /// exists from the start; other drives exist once something is created on them or <see cref="AddDrive"/> is
    /// called. Every drive is fixed unless <see cref="AddDrive"/> names another kind; UNC paths are on the network.
    /// </remarks>
    internal sealed class InMemoryFileSystem : IFileSystem
    {
        private sealed class FileData
        {
            public string Path;
            public byte[] Content;
            public DateTime LastWriteTimeUtc;
            public bool ReadOnly;
        }

        private static readonly char[] InvalidCharacters = { '<', '>', '"', '|', '?', '*' };

        private readonly IClock clock;
        private readonly Dictionary<string, FileData> files = new Dictionary<string, FileData>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> directories = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> openCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DriveKind> driveKinds = new Dictionary<string, DriveKind>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Tuple<string, FileSystemOperation, FileSystemStatus>> faults =
            new List<Tuple<string, FileSystemOperation, FileSystemStatus>>();
        private int openStreams;

        public InMemoryFileSystem(IClock clock = null)
        {
            this.clock = clock ?? new FakeClock();
            AddDrive("C:");
        }

        /// <summary>Called with "exists &lt;path&gt;" by every <see cref="FileExists"/> (order tests).</summary>
        public Action<string> OnFileExists { get; set; }

        /// <summary>
        /// Called with the path before every <see cref="Stream.Read(byte[], int, int)"/> of a stream of <see cref="OpenRead"/>,
        /// on the reading thread: a test can let a setup start or block while a file is read.
        /// </summary>
        public Action<string> OnRead { get; set; }

        /// <summary>Streams of <see cref="OpenRead"/> that are not disposed yet.</summary>
        public int OpenStreamCount
        {
            get { return openStreams; }
        }

        /// <summary>Number of <see cref="OpenRead"/> calls that returned a stream, over all files.</summary>
        public int TotalOpenCount
        {
            get { return openCounts.Values.Sum(); }
        }

        /// <summary>Full paths of all files, in their original spelling, sorted.</summary>
        public IReadOnlyList<string> AllFiles
        {
            get { return files.Values.Select(f => f.Path).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList(); }
        }

        /// <summary>Makes a drive (e.g. <c>D:</c>) exist, a fixed one unless <paramref name="kind"/> says otherwise.</summary>
        public void AddDrive(string drive, DriveKind kind = DriveKind.Fixed)
        {
            string root = WinPath.Normalize(drive);
            directories[root] = root;
            driveKinds[root] = kind;
        }

        /// <summary>Creates a folder and its parents (test setup, never fails).</summary>
        public void AddDirectory(string path)
        {
            string normalized = Key(path);
            for (string current = normalized; current != null; current = WinPath.GetParent(current))
            {
                if (!directories.ContainsKey(current))
                    directories[current] = current;
            }
        }

        /// <summary>Creates or overwrites a file with its folders (test setup, never fails).</summary>
        public void AddFile(string path, byte[] content, bool readOnly = false)
        {
            string normalized = Key(path);
            AddDirectory(WinPath.GetParent(normalized));
            files[normalized] = new FileData
            {
                Path = normalized, Content = (byte[])content.Clone(), LastWriteTimeUtc = clock.UtcNow, ReadOnly = readOnly
            };
        }

        /// <summary>Creates or overwrites a file with UTF-8 text without BOM.</summary>
        public void AddFile(string path, string content, bool readOnly = false)
        {
            AddFile(path, new UTF8Encoding(false).GetBytes(content), readOnly);
        }

        /// <summary>The content of a file, or null if it does not exist.</summary>
        public byte[] GetContent(string path)
        {
            return files.TryGetValue(Key(path), out FileData file) ? (byte[])file.Content.Clone() : null;
        }

        /// <summary>The content of a file as UTF-8 text, or null if it does not exist.</summary>
        public string GetText(string path)
        {
            byte[] content = GetContent(path);
            return content == null ? null : new UTF8Encoding(false).GetString(content);
        }

        /// <summary>Sets or clears the read-only attribute of an existing file.</summary>
        public void SetReadOnly(string path, bool readOnly)
        {
            files[Key(path)].ReadOnly = readOnly;
        }

        /// <summary>
        /// From now on <paramref name="operation"/> fails with <paramref name="status"/> for
        /// <paramref name="path"/> and everything below it (as source or destination).
        /// </summary>
        public void FailOn(string path, FileSystemOperation operation, FileSystemStatus status)
        {
            if (status == FileSystemStatus.Ok)
                throw new ArgumentException("A fault needs a failure status.", nameof(status));
            faults.Add(Tuple.Create(Key(path), operation, status));
        }

        /// <summary>Removes all injected faults.</summary>
        public void ClearFaults()
        {
            faults.Clear();
        }

        /// <summary>How often <paramref name="path"/> was opened for reading.</summary>
        public int OpenCount(string path)
        {
            return openCounts.TryGetValue(Key(path), out int count) ? count : 0;
        }

        public bool FileExists(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            OnFileExists?.Invoke("exists " + path);
            return TryKey(path, out string key) && files.ContainsKey(key);
        }

        public bool DirectoryExists(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            return TryKey(path, out string key) && directories.ContainsKey(key);
        }

        public FileSystemResult<FileEntry> GetFileInfo(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            FileSystemResult check = Check(path, FileSystemOperation.GetInfo, out string key);
            if (!check.IsOk)
                return FileSystemResult<FileEntry>.Failure(check.Status, check.Detail);
            if (!files.TryGetValue(key, out FileData file))
                return FileSystemResult<FileEntry>.Failure(FileSystemStatus.NotFound, "Could not find file " + path);
            var attributes = file.ReadOnly ? FileAttributes.ReadOnly : FileAttributes.Normal;
            return FileSystemResult<FileEntry>.Success(new FileEntry(file.Content.Length, file.LastWriteTimeUtc, attributes));
        }

        public FileSystemResult<Stream> OpenRead(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            FileSystemResult check = Check(path, FileSystemOperation.Read, out string key);
            if (!check.IsOk)
                return FileSystemResult<Stream>.Failure(check.Status, check.Detail);
            if (directories.ContainsKey(key))
                return FileSystemResult<Stream>.Failure(FileSystemStatus.AccessDenied, "Access to the path " + path + " is denied.");
            if (!files.TryGetValue(key, out FileData file))
                return FileSystemResult<Stream>.Failure(FileSystemStatus.NotFound, "Could not find file " + path);
            openCounts[key] = OpenCount(key) + 1;
            System.Threading.Interlocked.Increment(ref openStreams);
            return FileSystemResult<Stream>.Success(new TrackedStream(this, file.Path, file.Content));
        }

        public FileSystemResult<IReadOnlyList<string>> GetFiles(string directory)
        {
            return List(directory, files.Values.Select(f => f.Path));
        }

        public FileSystemResult<IReadOnlyList<string>> GetDirectories(string directory)
        {
            return List(directory, directories.Values);
        }

        public FileSystemResult CreateDirectory(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            FileSystemResult check = Check(path, FileSystemOperation.CreateDirectory, out string key);
            if (!check.IsOk)
                return check;
            for (string current = key; current != null; current = WinPath.GetParent(current))
            {
                if (files.ContainsKey(current))
                    return FileSystemResult.Failure(FileSystemStatus.IoError, "A file with the name " + current + " exists.");
            }
            string root = RootOf(key);
            if (!directories.ContainsKey(root))
                return FileSystemResult.Failure(FileSystemStatus.NotFound, "Could not find the drive " + root);
            AddDirectory(key);
            return FileSystemResult.Success;
        }

        public FileSystemResult WriteAllBytes(string path, byte[] data)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            FileSystemResult check = Check(path, FileSystemOperation.Write, out string key);
            if (!check.IsOk)
                return check;
            FileSystemResult writable = CheckWritableTarget(key);
            if (!writable.IsOk)
                return writable;
            files[key] = new FileData
            {
                Path = files.TryGetValue(key, out FileData existing) ? existing.Path : key,
                Content = (byte[])data.Clone(),
                LastWriteTimeUtc = clock.UtcNow
            };
            return FileSystemResult.Success;
        }

        public FileSystemResult Replace(string source, string destination)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            FileSystemResult check = Check(source, FileSystemOperation.Replace, out string sourceKey);
            if (check.IsOk)
                check = Check(destination, FileSystemOperation.Replace, out _);
            if (!check.IsOk)
                return check;
            string destinationKey = Key(destination);
            if (!files.TryGetValue(sourceKey, out FileData sourceFile))
                return FileSystemResult.Failure(FileSystemStatus.NotFound, "Could not find file " + source);
            if (!files.TryGetValue(destinationKey, out FileData destinationFile))
                return FileSystemResult.Failure(FileSystemStatus.NotFound, "Could not find file " + destination);
            if (destinationFile.ReadOnly || sourceFile.ReadOnly)
                return FileSystemResult.Failure(FileSystemStatus.AccessDenied, "Access to the path " + destination + " is denied.");
            files.Remove(sourceKey);
            files[destinationKey] = new FileData
            {
                Path = destinationFile.Path, Content = sourceFile.Content, LastWriteTimeUtc = sourceFile.LastWriteTimeUtc
            };
            return FileSystemResult.Success;
        }

        public FileSystemResult Move(string source, string destination)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (destination == null)
                throw new ArgumentNullException(nameof(destination));
            FileSystemResult check = Check(source, FileSystemOperation.Move, out string sourceKey);
            if (check.IsOk)
                check = Check(destination, FileSystemOperation.Move, out _);
            if (!check.IsOk)
                return check;
            string destinationKey = Key(destination);
            if (!files.TryGetValue(sourceKey, out FileData sourceFile))
                return FileSystemResult.Failure(FileSystemStatus.NotFound, "Could not find file " + source);
            if (files.ContainsKey(destinationKey) && !string.Equals(sourceKey, destinationKey, StringComparison.OrdinalIgnoreCase))
                return FileSystemResult.Failure(FileSystemStatus.IoError, "Cannot create a file when that file already exists.");
            FileSystemResult writable = CheckWritableTarget(destinationKey);
            if (!writable.IsOk)
                return writable;
            files.Remove(sourceKey);
            files[destinationKey] = new FileData
            {
                Path = destinationKey, Content = sourceFile.Content, LastWriteTimeUtc = sourceFile.LastWriteTimeUtc,
                ReadOnly = sourceFile.ReadOnly
            };
            return FileSystemResult.Success;
        }

        public FileSystemResult DeleteFile(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            FileSystemResult check = Check(path, FileSystemOperation.Delete, out string key);
            if (!check.IsOk)
                return check;
            if (!files.TryGetValue(key, out FileData file))
                return FileSystemResult.Failure(FileSystemStatus.NotFound, "Could not find file " + path);
            if (file.ReadOnly)
                return FileSystemResult.Failure(FileSystemStatus.AccessDenied, "Access to the path " + path + " is denied.");
            files.Remove(key);
            return FileSystemResult.Success;
        }

        public DriveKind GetDriveKind(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (!TryKey(path, out string key))
                return DriveKind.Unknown;
            string root = RootOf(key);
            if (root.StartsWith(@"\\", StringComparison.Ordinal))
                return DriveKind.Network;
            if (driveKinds.TryGetValue(root, out DriveKind kind))
                return kind;
            return directories.ContainsKey(root) ? DriveKind.Fixed : DriveKind.NotFound;
        }

        private FileSystemResult<IReadOnlyList<string>> List(string directory, IEnumerable<string> candidates)
        {
            if (directory == null)
                throw new ArgumentNullException(nameof(directory));
            FileSystemResult check = Check(directory, FileSystemOperation.Enumerate, out string key);
            if (!check.IsOk)
                return FileSystemResult<IReadOnlyList<string>>.Failure(check.Status, check.Detail);
            if (!directories.ContainsKey(key))
                return FileSystemResult<IReadOnlyList<string>>.Failure(FileSystemStatus.NotFound,
                    "Could not find a part of the path " + directory);
            IReadOnlyList<string> entries = candidates
                .Where(candidate => !string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase) &&
                                    string.Equals(WinPath.GetParent(candidate), key, StringComparison.OrdinalIgnoreCase))
                .OrderBy(entry => entry, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return FileSystemResult<IReadOnlyList<string>>.Success(entries);
        }

        /// <summary>A file may be written at <paramref name="key"/>: its folder exists, it is no folder, not read-only.</summary>
        private FileSystemResult CheckWritableTarget(string key)
        {
            string parent = WinPath.GetParent(key);
            if (parent == null || !directories.ContainsKey(parent))
                return FileSystemResult.Failure(FileSystemStatus.NotFound, "Could not find a part of the path " + key);
            if (directories.ContainsKey(key))
                return FileSystemResult.Failure(FileSystemStatus.AccessDenied, "Access to the path " + key + " is denied.");
            if (files.TryGetValue(key, out FileData existing) && existing.ReadOnly)
                return FileSystemResult.Failure(FileSystemStatus.AccessDenied, "Access to the path " + key + " is denied.");
            return FileSystemResult.Success;
        }

        /// <summary>Validates the path and applies the injected faults.</summary>
        private FileSystemResult Check(string path, FileSystemOperation operation, out string key)
        {
            if (!TryKey(path, out key))
                return FileSystemResult.Failure(FileSystemStatus.InvalidPath, "Illegal characters in path or not a full path: " + path);
            foreach (var fault in faults)
            {
                if (fault.Item2 == operation && WinPath.IsSameOrBelow(key, fault.Item1))
                    return FileSystemResult.Failure(fault.Item3, "Injected fault: " + operation + " " + path);
            }
            return FileSystemResult.Success;
        }

        private static bool TryKey(string path, out string key)
        {
            key = null;
            if (path.Length == 0 || path.Any(c => c < ' ') || path.IndexOfAny(InvalidCharacters) >= 0)
                return false;
            // A colon is allowed only as the drive separator (no alternate data streams).
            if ((path.Length > 2 && path.IndexOf(':', 2) >= 0) || !WinPath.IsFullyQualified(path))
                return false;
            key = WinPath.Normalize(path);
            return true;
        }

        private static string Key(string path)
        {
            if (!TryKey(path, out string key))
                throw new ArgumentException("A valid full Windows path is required: " + path, nameof(path));
            return key;
        }

        /// <summary>A read-only stream of a file that reports its reads (<see cref="OnRead"/>) and its disposal.</summary>
        private sealed class TrackedStream : MemoryStream
        {
            private readonly InMemoryFileSystem owner;
            private readonly string path;
            private bool disposed;

            public TrackedStream(InMemoryFileSystem owner, string path, byte[] content)
                : base(content, false)
            {
                this.owner = owner;
                this.path = path;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                owner.OnRead?.Invoke(path);
                return base.Read(buffer, offset, count);
            }

            protected override void Dispose(bool disposing)
            {
                if (!disposed)
                {
                    disposed = true;
                    System.Threading.Interlocked.Decrement(ref owner.openStreams);
                }
                base.Dispose(disposing);
            }
        }

        private static string RootOf(string key)
        {
            string current = key;
            for (string parent = WinPath.GetParent(current); parent != null; parent = WinPath.GetParent(parent))
                current = parent;
            return current;
        }
    }
}
