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
                if (!zip.ExtractFile(dataEntry, out data) || data.Length == 0)
                    throw new InvalidDataException("Invalid mod archive: the \"" + EemFormat.DataEntryName + "\" entry cannot be read.");
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
    }
}
