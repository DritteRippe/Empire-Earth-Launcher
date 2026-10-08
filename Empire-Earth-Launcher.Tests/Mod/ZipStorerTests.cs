using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Mod
{
    /// <summary>
    /// The local modifications of the vendored ZipStorer (<c>Empire-Earth-Mod/Empire-Earth-Mod-Lib/ZipStorer.cs</c>,
    /// third-party code, listed in its header and in THIRD-PARTY-NOTICES.md). Each test fails without its modification,
    /// except the case <c>Open_NegativeCentralDirectorySize_IsRejected</c>: the unmodified ZipStorer rejected a negative
    /// size of the central directory as well (see <see cref="Open_CentralDirectoryOutsideTheArchive_IsRejected"/>).
    /// </summary>
    [TestFixture]
    public class ZipStorerTests
    {
        /// <summary>Size of the extra fields ZipStorer writes for every entry: ZIP64 sizes and NTFS times, 36 bytes each.</summary>
        private const int ExtraFieldBytes = 72;

        /// <summary>Size of a central directory record without its name, extra fields and comment.</summary>
        private const int CentralRecordHeaderBytes = 46;

        /// <summary>
        /// An archive written like <c>ModPackageBuilder</c> writes one: UTF-8 names, every entry with its own time and,
        /// optionally, a comment.
        /// </summary>
        private static MemoryStream CreateArchive(IEnumerable<Tuple<string, DateTime, string>> entries)
        {
            var stream = new MemoryStream();
            using (ZipStorer zip = ZipStorer.Create(stream, string.Empty, true))
            {
                zip.EncodeUTF8 = true;
                foreach (Tuple<string, DateTime, string> entry in entries)
                {
                    using (var content = new MemoryStream(Encoding.UTF8.GetBytes("content of " + entry.Item1)))
                    {
                        zip.AddStream(ZipStorer.Compression.Deflate, entry.Item1, content, entry.Item2, entry.Item3);
                    }
                }
            }
            stream.Position = 0;
            return stream;
        }

        /// <summary>An ASCII entry name of exactly <paramref name="length"/> characters below the folder of a variant.</summary>
        private static string NameOfLength(int length, char filler)
        {
            string prefix = Guid.Empty + "/EEC/Data/";
            const string extension = ".xml";
            string name = prefix + new string(filler, length - prefix.Length - extension.Length) + extension;
            Assert.That(name.Length, Is.EqualTo(length));
            return name;
        }

        /* ReadExtraInfo reads only the extra field of its entry (backport of upstream issue #71) */

        /// <summary>
        /// The old ReadExtraInfo went on after the extra fields of an entry: it took the signature of the next central
        /// directory record ("PK", 1, 2) for an extra field of 513 bytes and jumped 517 bytes ahead. With these name
        /// lengths it landed exactly on the NTFS times of the fourth record (118 + 66 and 118 + 66 bytes for the second and
        /// third record, then 46 + 67 + 36 bytes up to the times of the fourth), so the first entry got the times of the
        /// fourth one. Other lengths made it read garbage instead.
        /// </summary>
        [Test]
        public void ReadCentralDir_EveryEntryKeepsItsOwnTimes()
        {
            Assert.That(CentralRecordHeaderBytes + 66 + ExtraFieldBytes + CentralRecordHeaderBytes + 66 + ExtraFieldBytes +
                        CentralRecordHeaderBytes + 67 + 36, Is.EqualTo(517), "the layout this test is built on");
            var entries = new[]
            {
                Tuple.Create("data", new DateTime(2024, 6, 10, 12, 0, 2), (string)null),
                Tuple.Create(NameOfLength(66, 'b'), new DateTime(2023, 6, 11, 12, 30, 4), (string)null),
                Tuple.Create(NameOfLength(66, 'c'), new DateTime(2022, 6, 12, 13, 0, 6), (string)null),
                Tuple.Create(NameOfLength(67, 'd'), new DateTime(2021, 6, 13, 13, 30, 8), (string)null),
            };

            using (MemoryStream archive = CreateArchive(entries))
            using (ZipStorer zip = ZipStorer.Open(archive, FileAccess.Read, true))
            {
                List<ZipStorer.ZipFileEntry> read = zip.ReadCentralDir();

                Assert.That(read.Select(entry => entry.FilenameInZip), Is.EqualTo(entries.Select(entry => entry.Item1)));
                for (int i = 0; i < entries.Length; i++)
                {
                    Assert.That(read[i].ModifyTime, Is.EqualTo(entries[i].Item2), read[i].FilenameInZip);
                    Assert.That(read[i].CreationTime, Is.EqualTo(entries[i].Item2), read[i].FilenameInZip);
                }
            }
        }

        /// <summary>
        /// Whatever follows the extra fields of an entry is not read as an extra field. Here the comment of the entry starts
        /// like a ZIP64 extra field at the very end of the central directory: the old ReadExtraInfo read beyond the end of
        /// the directory and threw <see cref="ArgumentException"/>. Without a comment the same happened by chance, to about
        /// 3 in 1000 archives in a simulation of mod archives, which the mod library then could not read back.
        /// </summary>
        [Test]
        public void ReadCentralDir_DataAfterTheExtraFields_IsNotReadAsAnExtraField()
        {
            const string comment = "\u0001\0\0\0\0"; // extra field id 0x0001 (ZIP64) and length 0, then the directory ends
            DateTime time = new DateTime(2024, 6, 10, 12, 0, 2);

            using (MemoryStream archive = CreateArchive(new[] { Tuple.Create("data", time, comment) }))
            using (ZipStorer zip = ZipStorer.Open(archive, FileAccess.Read, true))
            {
                ZipStorer.ZipFileEntry entry = zip.ReadCentralDir().Single();

                Assert.That(entry.FilenameInZip, Is.EqualTo("data"));
                Assert.That(entry.Comment, Is.EqualTo(comment));
                Assert.That(entry.ModifyTime, Is.EqualTo(time));
                byte[] content;
                Assert.That(zip.ExtractFile(entry, out content), Is.True);
                Assert.That(Encoding.UTF8.GetString(content), Is.EqualTo("content of data"));
            }
        }

        /* ReadFileInfo checks where the central directory is */

        /// <summary>
        /// The end of central directory record of an archive declares where the central directory starts and how large it
        /// is. ZipStorer allocated the declared size as it was (up to 2 GB from a classic record, more from a ZIP64 one, for a
        /// file of a few hundred bytes), read what was there and opened the archive. Now a directory that does not lie
        /// between the start of the archive and that record makes the archive invalid. ZipStorer always writes a ZIP64
        /// record, whose values it reads; this test changes them. The first three cases opened without the modification.
        /// The negative size did not: allocating it threw an <see cref="OverflowException"/>, which the catch-all of
        /// ReadFileInfo turned into an invalid archive. The case stays as a regression test of that behavior; now the check
        /// rejects a negative size before anything is allocated.
        /// </summary>
        [TestCase(64L * 1024 * 1024, 0L, TestName = "Open_CentralDirectoryLargerThanTheArchive_IsRejected")]
        [TestCase(0L, 64L * 1024 * 1024, TestName = "Open_CentralDirectoryBehindTheEndOfTheArchive_IsRejected")]
        [TestCase(1L, 0L, TestName = "Open_CentralDirectoryOverlappingTheEndRecords_IsRejected")]
        [TestCase(-1L - 64L * 1024 * 1024, 0L, TestName = "Open_NegativeCentralDirectorySize_IsRejected")]
        public void Open_CentralDirectoryOutsideTheArchive_IsRejected(long addToSize, long addToOffset)
        {
            byte[] bytes;
            using (MemoryStream archive = CreateArchive(new[] { Tuple.Create("data", new DateTime(2024, 6, 10, 12, 0, 2), (string)null) }))
            {
                bytes = archive.ToArray();
            }
            // ZIP64 end of central directory record: signature 0x06064b50, directory size at offset 40, its start at 48.
            int record = -1;
            for (int i = 0; i + 56 <= bytes.Length; i++)
            {
                if (BitConverter.ToUInt32(bytes, i) == 0x06064b50)
                    record = i;
            }
            Assert.That(record, Is.GreaterThan(0), "ZIP64 end of central directory record");
            BitConverter.GetBytes(BitConverter.ToInt64(bytes, record + 40) + addToSize).CopyTo(bytes, record + 40);
            BitConverter.GetBytes(BitConverter.ToInt64(bytes, record + 48) + addToOffset).CopyTo(bytes, record + 48);

            using (var damaged = new MemoryStream(bytes))
            {
                Assert.That(() => ZipStorer.Open(damaged, FileAccess.Read, true), Throws.TypeOf<InvalidDataException>());
            }
        }

        [Test]
        public void Open_ArchiveWithSeveralEntries_ReadsTheWholeCentralDirectory()
        {
            var entries = Enumerable.Range(0, 20)
                .Select(i => Tuple.Create(Guid.Empty + "/all/Data/file" + i + ".xml", new DateTime(2024, 6, 10, 12, 0, 2), (string)null))
                .ToList();

            using (MemoryStream archive = CreateArchive(entries))
            using (ZipStorer zip = ZipStorer.Open(archive, FileAccess.Read, true))
            {
                Assert.That(zip.ReadCentralDir().Select(entry => entry.FilenameInZip), Is.EqualTo(entries.Select(entry => entry.Item1)));
            }
        }
    }
}
