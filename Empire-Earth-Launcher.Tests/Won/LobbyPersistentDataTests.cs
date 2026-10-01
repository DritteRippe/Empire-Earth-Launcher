using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Empire_Earth_Launcher.Tests.TestSupport;
using Empire_Earth_WON;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Won
{
    [TestFixture]
    public class LobbyPersistentDataTests
    {
        // Names outside ASCII and outside the Basic Multilingual Plane: they were decoded byte by byte as
        // UTF-8 before (korr-S11), which turned every one of them into other characters.
        private const string GermanName = "Jürgen Größe";
        private const string CyrillicName = "Игрок";
        private const string ChineseName = "玩家";
        private const string EmojiName = "Ace 🎮"; // surrogate pair in UTF-16

        private static LobbyPersistentData.LobbyGlobalData ParseGlobal(byte[] data)
        {
            using (var stream = new MemoryStream(data))
            {
                return new LobbyPersistentData.LobbyGlobalData(stream);
            }
        }

        private static LobbyPersistentData.LobbyUserData ParseUser(byte[] data)
        {
            using (var stream = new MemoryStream(data))
            {
                return new LobbyPersistentData.LobbyUserData(stream);
            }
        }

        private static KeyValuePair<string, uint> Friend(string name, uint wonId)
        {
            return new KeyValuePair<string, uint>(name, wonId);
        }

        /* _wonlobbypersistent.dat */

        [Test]
        public void GlobalData_ValidFile_ReadsProfilesAndSettings()
        {
            byte[] file = LobbyFileBuilder.GlobalFile(true, false, 0xDEADBEEF, "Alice", "Bob");

            var data = ParseGlobal(file);

            Assert.That(data.FileSignature, Is.EqualTo(1u));
            Assert.That(data.PlayerInfos.Select(p => p.Username), Is.EqualTo(new[] { "Alice", "Bob" }));
            Assert.That(data.PlayerInfos.Select(p => p.LastUse), Is.EqualTo(new[] { 1000u, 1001u }));
            Assert.That(data.PlayerInfos.Select(p => p.FileID), Is.EqualTo(new[] { (ushort)0, (ushort)1 }));
            Assert.That(data.LobbySoundEffects, Is.True);
            Assert.That(data.GlobalLobbyMusic, Is.False);
            Assert.That(data.NetworkAdapter, Is.EqualTo(0xDEADBEEF));
        }

        [Test]
        public void GlobalData_NonAsciiNames_AreDecodedAsUtf16()
        {
            byte[] file = LobbyFileBuilder.GlobalFile(false, true, 0, GermanName, CyrillicName, ChineseName, EmojiName);

            var data = ParseGlobal(file);

            Assert.That(data.PlayerInfos.Select(p => p.Username),
                Is.EqualTo(new[] { GermanName, CyrillicName, ChineseName, EmojiName }));
            // The password of every profile is skipped exactly: the settings after the records are correct.
            Assert.That(data.GlobalLobbyMusic, Is.True);
        }

        [Test]
        public void GlobalData_NoProfiles_GivesAnEmptyList()
        {
            var data = ParseGlobal(LobbyFileBuilder.GlobalFile(false, false, 7));

            Assert.That(data.PlayerInfos, Is.Empty);
            Assert.That(data.NetworkAdapter, Is.EqualTo(7u));
        }

        [Test]
        public void GlobalData_WrongSignature_ThrowsInvalidData()
        {
            byte[] file = new LobbyFileBuilder().Signature(1, "WONXXX").UInt16(0).Byte(0).Byte(0).UInt32(0).ToArray();

            Assert.That(() => ParseGlobal(file), Throws.TypeOf<InvalidDataException>());
        }

        [Test]
        public void GlobalData_EveryTruncation_ThrowsADocumentedException()
        {
            byte[] file = LobbyFileBuilder.GlobalFile(true, true, 3, GermanName, "Bob");

            for (int length = 0; length < file.Length; length++)
            {
                byte[] truncated = file.Take(length).ToArray();
                Assert.That(() => ParseGlobal(truncated),
                    Throws.TypeOf<EndOfStreamException>().Or.TypeOf<InvalidDataException>(),
                    "file cut after " + length + " of " + file.Length + " bytes");
            }
        }

        [Test]
        public void GlobalData_TruncatedInTheTrailingSettings_ThrowsEndOfStream()
        {
            byte[] file = LobbyFileBuilder.GlobalFile(true, true, 3, "Alice");
            byte[] truncated = file.Take(file.Length - 2).ToArray(); // half of the network adapter

            Assert.That(() => ParseGlobal(truncated), Throws.TypeOf<EndOfStreamException>());
        }

        [Test]
        public void GlobalData_TruncatedNonSeekableStream_ThrowsEndOfStream()
        {
            // Without Length the up-front size checks are skipped; BinaryReader must still stop at the end.
            byte[] file = LobbyFileBuilder.GlobalFile(true, true, 3, GermanName, "Bob");
            byte[] truncated = file.Take(file.Length / 2).ToArray();

            Assert.That(() => new LobbyPersistentData.LobbyGlobalData(new ChunkedReadStream(truncated, 3)),
                Throws.TypeOf<EndOfStreamException>());
        }

        [Test]
        public void GlobalData_NonSeekableStreamInChunks_IsParsedCompletely()
        {
            byte[] file = LobbyFileBuilder.GlobalFile(true, false, 9, GermanName, EmojiName);

            var data = new LobbyPersistentData.LobbyGlobalData(new ChunkedReadStream(file, 1));

            Assert.That(data.PlayerInfos.Select(p => p.Username), Is.EqualTo(new[] { GermanName, EmojiName }));
            Assert.That(data.NetworkAdapter, Is.EqualTo(9u));
        }

        [Test]
        public void GlobalData_BogusNameLength_ThrowsInvalidDataBeforeReadingIt()
        {
            // The name announces 65535 characters (131070 bytes), the file has a few bytes left.
            byte[] file = new LobbyFileBuilder().Signature().UInt16(1)
                .UInt16(0xFFFF).Bytes(new byte[40])
                .ToArray();

            Assert.That(() => ParseGlobal(file), Throws.TypeOf<InvalidDataException>());
        }

        [Test]
        public void GlobalData_BogusProfileCount_ThrowsInvalidData()
        {
            // 65535 profiles announced, one present.
            byte[] file = new LobbyFileBuilder().Signature().UInt16(0xFFFF)
                .PlayerInfo("Alice", 1, 1)
                .Byte(0).Byte(0).UInt32(0)
                .ToArray();

            Assert.That(() => ParseGlobal(file), Throws.TypeOf<InvalidDataException>());
        }

        [Test]
        public void GlobalData_BogusPasswordLength_ThrowsInvalidData()
        {
            byte[] file = new LobbyFileBuilder().Signature().UInt16(1)
                .WideString("Alice").UInt32(1).UInt16(1).UInt32(0).UInt32(0)
                .UInt16(0xFFFF).Bytes(new byte[6])
                .ToArray();

            Assert.That(() => ParseGlobal(file), Throws.TypeOf<InvalidDataException>());
        }

        [Test]
        public void GlobalData_ParsingDoesNotCloseTheCallersStream()
        {
            using (var stream = new MemoryStream(LobbyFileBuilder.GlobalFile(false, false, 0, "Alice")))
            {
                new LobbyPersistentData.LobbyGlobalData(stream);

                Assert.That(stream.CanRead, Is.True);
                Assert.That(stream.Position, Is.EqualTo(stream.Length));
            }
        }

        [Test]
        public void GlobalData_ReadFromStream_CannotBeReloaded()
        {
            var data = ParseGlobal(LobbyFileBuilder.GlobalFile(false, false, 0, "Alice"));

            Assert.That(() => data.Reload(), Throws.TypeOf<InvalidOperationException>());
        }

        [Test]
        public void GlobalData_FromFile_ReloadKeepsThePreviousDataWhenTheNewFileIsBroken()
        {
            using (var directory = new TemporaryDirectory())
            {
                string path = directory.Combine(LobbyPersistentData.GlobalDataFileName);
                File.WriteAllBytes(path, LobbyFileBuilder.GlobalFile(true, true, 5, GermanName));
                var data = new LobbyPersistentData.LobbyGlobalData(path);

                byte[] newFile = LobbyFileBuilder.GlobalFile(false, false, 6, "Bob", "Carol");
                File.WriteAllBytes(path, newFile.Take(newFile.Length - 1).ToArray());

                Assert.That(() => data.Reload(), Throws.InstanceOf<IOException>().Or.TypeOf<InvalidDataException>());
                Assert.That(data.PlayerInfos.Select(p => p.Username), Is.EqualTo(new[] { GermanName }));
                Assert.That(data.NetworkAdapter, Is.EqualTo(5u));

                File.WriteAllBytes(path, newFile);
                data.Reload();

                Assert.That(data.PlayerInfos.Select(p => p.Username), Is.EqualTo(new[] { "Bob", "Carol" }));
                Assert.That(data.NetworkAdapter, Is.EqualTo(6u));
            }
        }

        [Test]
        public void GlobalData_MissingFile_ThrowsFileNotFound()
        {
            using (var directory = new TemporaryDirectory())
            {
                string path = directory.Combine(LobbyPersistentData.GlobalDataFileName);

                Assert.That(() => new LobbyPersistentData.LobbyGlobalData(path), Throws.InstanceOf<FileNotFoundException>());
            }
        }

        [Test]
        public void GetUserDataFileName_UsesTheFileIdOfTheProfile()
        {
            Assert.That(LobbyPersistentData.GetUserDataFileName(3), Is.EqualTo("_wonuser3.dat"));
            Assert.That(LobbyPersistentData.GetUserDataFileName(65535), Is.EqualTo("_wonuser65535.dat"));
        }

        /* _wonuser*.dat */

        [Test]
        public void UserData_ValidFile_SkipsReconnectIdsAndIgnoredPlayersAndReadsFriends()
        {
            byte[] file = LobbyFileBuilder.UserFile(new[] { "Troll", CyrillicName },
                Friend(GermanName, 42), Friend(ChineseName, 7), Friend("Max", uint.MaxValue));

            var data = ParseUser(file);

            Assert.That(data.FileSignature, Is.EqualTo(1u));
            Assert.That(data.Friends, Is.EquivalentTo(new Dictionary<string, uint>
            {
                { GermanName, 42 }, { ChineseName, 7 }, { "Max", uint.MaxValue }
            }));
        }

        [Test]
        public void UserData_DuplicateFriend_LastEntryWins()
        {
            byte[] file = LobbyFileBuilder.UserFile(new string[0],
                Friend("Alice", 1), Friend("Bob", 2), Friend("Alice", 3));

            var data = ParseUser(file);

            Assert.That(data.Friends.Count, Is.EqualTo(2));
            Assert.That(data.Friends["Alice"], Is.EqualTo(3u));
        }

        [Test]
        public void UserData_EveryTruncation_ThrowsADocumentedException()
        {
            byte[] file = LobbyFileBuilder.UserFile(new[] { "Troll" }, Friend(GermanName, 42), Friend("Bob", 2));

            for (int length = 0; length < file.Length; length++)
            {
                byte[] truncated = file.Take(length).ToArray();
                Assert.That(() => ParseUser(truncated),
                    Throws.TypeOf<EndOfStreamException>().Or.TypeOf<InvalidDataException>(),
                    "file cut after " + length + " of " + file.Length + " bytes");
            }
        }

        [Test]
        public void UserData_BogusIgnoredPlayerCount_ThrowsInvalidData()
        {
            byte[] file = new LobbyFileBuilder().Signature()
                .UInt16(0)        // reconnect ids
                .UInt16(0xFFFF)   // ignored players announced
                .WideString("Troll")
                .UInt16(0)        // friends
                .ToArray();

            Assert.That(() => ParseUser(file), Throws.TypeOf<InvalidDataException>());
        }

        [Test]
        public void UserData_BogusReconnectIdCount_ThrowsInvalidData()
        {
            byte[] file = new LobbyFileBuilder().Signature()
                .UInt16(0xFFFF).ReconnectId(1)
                .UInt16(0).UInt16(0)
                .ToArray();

            Assert.That(() => ParseUser(file), Throws.TypeOf<InvalidDataException>());
        }

        [Test]
        public void UserData_FromFile_ReloadKeepsThePreviousFriendsWhenTheNewFileIsBroken()
        {
            using (var directory = new TemporaryDirectory())
            {
                string path = directory.Combine(LobbyPersistentData.GetUserDataFileName(1));
                File.WriteAllBytes(path, LobbyFileBuilder.UserFile(new string[0], Friend("Alice", 1)));
                var data = new LobbyPersistentData.LobbyUserData(path);

                File.WriteAllBytes(path, new LobbyFileBuilder().Signature(1, "BROKEN").ToArray());

                Assert.That(() => data.Reload(), Throws.TypeOf<InvalidDataException>());
                Assert.That(data.Friends.Keys, Is.EqualTo(new[] { "Alice" }));
            }
        }
    }
}
