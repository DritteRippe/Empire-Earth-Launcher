using System;
using System.IO;

namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>Operations built from the primitives of <see cref="IFileSystem"/>.</summary>
    public static class FileSystemExtensions
    {
        /// <summary>Suffix of the temporary file of <see cref="WriteAllBytesAtomically"/>.</summary>
        public const string TemporaryFileSuffix = ".tmp";

        /// <summary>
        /// Reads a whole file, but at most <paramref name="maxBytes"/> bytes: a larger file is
        /// <see cref="FileSystemStatus.TooLarge"/> (a damaged or foreign file must not fill the memory).
        /// </summary>
        public static FileSystemResult<byte[]> ReadAllBytes(this IFileSystem fileSystem, string path, long maxBytes)
        {
            if (fileSystem == null)
                throw new ArgumentNullException(nameof(fileSystem));
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (maxBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(maxBytes));

            FileSystemResult<Stream> opened = fileSystem.OpenRead(path);
            if (!opened.IsOk)
                return FileSystemResult<byte[]>.Failure(opened.Status, opened.Detail);

            try
            {
                using (Stream stream = opened.Value)
                using (var content = new MemoryStream())
                {
                    var buffer = new byte[81920];
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (content.Length + read > maxBytes)
                            return FileSystemResult<byte[]>.Failure(FileSystemStatus.TooLarge,
                                "larger than " + maxBytes + " bytes");
                        content.Write(buffer, 0, read);
                    }
                    return FileSystemResult<byte[]>.Success(content.ToArray());
                }
            }
            catch (IOException ex)
            {
                return FileSystemResult<byte[]>.Failure(FileSystemStatus.IoError, ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return FileSystemResult<byte[]>.Failure(FileSystemStatus.AccessDenied, ex.Message);
            }
        }

        /// <summary>
        /// Writes a file so that it is never left half written (ADR 0005, ADR 0013): the data goes to
        /// <c>&lt;path&gt;.tmp</c> first, which then replaces the file (or is renamed to it if there is none yet).
        /// On failure the previous file is unchanged and the temporary file is removed.
        /// </summary>
        public static FileSystemResult WriteAllBytesAtomically(this IFileSystem fileSystem, string path, byte[] data)
        {
            if (fileSystem == null)
                throw new ArgumentNullException(nameof(fileSystem));
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            string temporaryFile = path + TemporaryFileSuffix;
            FileSystemResult result = fileSystem.WriteAllBytes(temporaryFile, data);
            if (result.IsOk)
            {
                result = fileSystem.FileExists(path)
                    ? fileSystem.Replace(temporaryFile, path)
                    : fileSystem.Move(temporaryFile, path);
            }

            if (!result.IsOk && fileSystem.FileExists(temporaryFile))
                fileSystem.DeleteFile(temporaryFile);
            return result;
        }
    }
}
