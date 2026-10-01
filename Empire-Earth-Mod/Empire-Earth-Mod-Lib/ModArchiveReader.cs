using System;
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
        /// <returns>Mod present in the archive (icon and banners are not loaded)</returns>
        /// <exception cref="IOException">The stream cannot be read.</exception>
        /// <exception cref="InvalidDataException">The stream is not a valid mod archive.</exception>
        public static ModData ReadModData(Stream eemStream)
        {
            if (eemStream == null)
                throw new ArgumentNullException(nameof(eemStream));

            byte[] data;
            using (var zip = ZipStorer.Open(eemStream, FileAccess.Read, true))
            {
                ZipStorer.ZipFileEntry dataEntry = zip.ReadCentralDir()
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
            return modData;
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
