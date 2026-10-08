using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using Empire_Earth_Mod_Lib.Serialization;

namespace Empire_Earth_Mod_Lib
{
    /// <summary>
    /// Reads mod archives (.eem, see <see cref="EemFormat"/>); the counterpart of <see cref="ModPackageBuilder"/>.
    /// </summary>
    public static class ModArchiveReader
    {
        /// <summary>
        /// Reads the mod data of a mod archive.
        /// </summary>
        /// <param name="eemPath">Path to the mod archive</param>
        /// <returns>Mod present in the archive (icon and banners are not loaded)</returns>
        /// <exception cref="IOException">The file cannot be read.</exception>
        /// <exception cref="UnauthorizedAccessException">The file cannot be opened (no permission).</exception>
        /// <exception cref="InvalidDataException">The file is not a valid mod archive.</exception>
        public static ModData ReadModData(string eemPath)
        {
            using (FileStream eemStream = File.OpenRead(eemPath))
            {
                return ReadModData(eemStream);
            }
        }

        /// <summary>
        /// Reads the mod data of a mod archive in a seekable stream. The stream is not closed.
        /// </summary>
        /// <param name="eemStream">Stream containing the mod archive (see <see cref="EemFormat"/>)</param>
        /// <returns>Mod present in the archive (icon and banners are not loaded); the paths of its files follow
        /// <see cref="EemFormat.IsValidFilePath"/>.</returns>
        /// <exception cref="IOException">The stream cannot be read.</exception>
        /// <exception cref="InvalidDataException">The stream is not a valid mod archive.</exception>
        public static ModData ReadModData(Stream eemStream)
        {
            if (eemStream == null)
                throw new ArgumentNullException(nameof(eemStream));

            byte[] data;
            using (ZipStorer zip = OpenArchive(eemStream))
            {
                ZipStorer.ZipFileEntry dataEntry = ReadEntries(zip)
                    .FirstOrDefault(entry => entry.FilenameInZip == EemFormat.DataEntryName);
                if (dataEntry == null)
                    throw new InvalidDataException("Invalid mod archive: the \"" + EemFormat.DataEntryName + "\" entry is missing.");
                if (dataEntry.FileSize > EemFormat.MaxDataEntryBytes)
                    throw new InvalidDataException("Invalid mod archive: the \"" + EemFormat.DataEntryName + "\" entry is too large.");
                data = ExtractEntry(zip, dataEntry);
            }

            ModData modData;
            try
            {
                // A new stream starts at position 0; the deserializer reads from the current position.
                using (var dataStream = new MemoryStream(data))
                {
                    modData = DataContractJsonHelper<ModData>.Deserialize(dataStream);
                }
            }
            catch (SerializationException ex)
            {
                throw new InvalidDataException("Invalid mod archive: the \"" + EemFormat.DataEntryName + "\" entry is not valid mod data. " + ex.Message, ex);
            }

            if (modData == null)
                throw new InvalidDataException("Invalid mod archive: the \"" + EemFormat.DataEntryName + "\" entry is empty.");

            // The file list comes from whoever made the archive. A path that leaves its product folder ("EEC\..\..",
            // "C:\Windows\...") would send code that installs the files anywhere on the disk.
            foreach (ModFile modFile in modData.ModFiles)
            {
                if (modFile == null || !EemFormat.IsValidFilePath(modFile.RelativeFilePath))
                    throw new InvalidDataException("Invalid mod archive: the path of the mod file \"" + modFile?.RelativeFilePath +
                                                   "\" is not a relative path inside a product folder.");
            }
            return modData;
        }

        /// <summary>
        /// Opens the ZIP archive in <paramref name="eemStream"/>. ZipStorer reports a file that is not a ZIP archive, or
        /// whose end records are damaged, with an <see cref="InvalidDataException"/> without a message of its own ("Found
        /// invalid data while decoding."); this one says what is wrong.
        /// </summary>
        private static ZipStorer OpenArchive(Stream eemStream)
        {
            try
            {
                return ZipStorer.Open(eemStream, FileAccess.Read, true);
            }
            catch (InvalidDataException ex)
            {
                throw new InvalidDataException("Invalid mod archive: the file is not a ZIP archive, or it is damaged.", ex);
            }
        }

        /// <summary>
        /// The entries of the central directory of the archive.
        /// </summary>
        /// <remarks>
        /// The vendored ZipStorer takes the sizes, dates and times of the records as they are: a damaged record makes it
        /// throw an <see cref="ArgumentException"/> (a name or field beyond the end of the directory, a time such as 31:00).
        /// It becomes the documented <see cref="InvalidDataException"/>, without changing the third-party code.
        /// </remarks>
        private static List<ZipStorer.ZipFileEntry> ReadEntries(ZipStorer zip)
        {
            try
            {
                return zip.ReadCentralDir();
            }
            catch (ArgumentException ex)
            {
                throw new InvalidDataException("Invalid mod archive: the directory of the ZIP archive is damaged.", ex);
            }
        }

        /// <summary>
        /// Extracts an entry whose size was already checked against <see cref="EemFormat.MaxDataEntryBytes"/>.
        /// </summary>
        /// <remarks>
        /// The vendored ZipStorer copies until it has written the size declared in the central directory and
        /// does not stop when the input ends early (Read() returns 0): a damaged or crafted archive that
        /// declares more bytes than it contains would make it loop forever. <see cref="BoundedWriteStream"/>
        /// turns that case into an <see cref="InvalidDataException"/> without changing the third-party code.
        /// </remarks>
        private static byte[] ExtractEntry(ZipStorer zip, ZipStorer.ZipFileEntry entry)
        {
            using (var output = new BoundedWriteStream(entry.FileSize))
            {
                if (!zip.ExtractFile(entry, output) || output.Length == 0)
                    throw new InvalidDataException("Invalid mod archive: the \"" + entry.FilenameInZip + "\" entry cannot be read.");
                return output.ToArray();
            }
        }

        /// <summary>
        /// Write-only memory stream that rejects an empty write (the input of the extraction ended before the
        /// declared size was reached) and any write beyond the declared size.
        /// </summary>
        private sealed class BoundedWriteStream : MemoryStream
        {
            private readonly long maxLength;

            public BoundedWriteStream(long maxLength)
            {
                this.maxLength = maxLength;
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (count == 0)
                    throw new InvalidDataException("Invalid mod archive: an entry is shorter than its declared size.");
                if (Length + count > maxLength)
                    throw new InvalidDataException("Invalid mod archive: an entry is longer than its declared size.");
                base.Write(buffer, offset, count);
            }

            public override void WriteByte(byte value)
            {
                if (Length + 1 > maxLength)
                    throw new InvalidDataException("Invalid mod archive: an entry is longer than its declared size.");
                base.WriteByte(value);
            }
        }
    }
}
