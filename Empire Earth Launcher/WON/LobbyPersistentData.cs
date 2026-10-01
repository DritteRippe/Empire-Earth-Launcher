using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Empire_Earth_Launcher.WON
{
    /// <summary>
    /// LobbyGlobalData: Parsing fully done
    /// LobbyUserData: Only friends parsing done
    /// </summary>
    /// <remarks>
    /// File layout: "WONPER" signature followed by a 32-bit value, then the records. Numbers are little
    /// endian, strings are a 16-bit character count followed by that many UTF-16LE code units.
    /// Every parser either reads a complete, consistent file or throws: <see cref="InvalidDataException"/>
    /// for a wrong signature or for data (string, record list) that cannot fit into the rest of the file,
    /// <see cref="EndOfStreamException"/> for a file that ends in the middle of a value.
    /// </remarks>
    public static class LobbyPersistentData
    {
        private const string FileSignaturePrefix = "WONPER";

        /// <summary>
        /// Creates the reader used by all parsers. The reader is intentionally never disposed: on .NET 4.0
        /// disposing a BinaryReader also closes the stream, which belongs to the caller.
        /// </summary>
        private static BinaryReader CreateReader(Stream input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            // Encoding.Unicode is UTF-16LE, the encoding of the WON lobby strings.
            return new BinaryReader(input, Encoding.Unicode);
        }

        private static uint ReadFileSignature(BinaryReader reader)
        {
            string prefix = Encoding.ASCII.GetString(ReadBytesExactly(reader, FileSignaturePrefix.Length));
            if (!prefix.Equals(FileSignaturePrefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Not a WON lobby file: the \"" + FileSignaturePrefix + "\" signature is missing.");
            return reader.ReadUInt32();
        }

        private static string ReadWideString(BinaryReader reader)
        {
            int length = reader.ReadUInt16();
            // Decode the exact number of bytes instead of using ReadChars, so that the stream position
            // stays correct even for invalid UTF-16 (e.g. a lone surrogate) in a corrupted file.
            return Encoding.Unicode.GetString(ReadBytesExactly(reader, length * 2));
        }

        private static void SkipBytes(BinaryReader reader, int count)
        {
            ReadBytesExactly(reader, count);
        }

        private static byte[] ReadBytesExactly(BinaryReader reader, int count)
        {
            EnsureAvailable(reader, count);
            byte[] bytes = reader.ReadBytes(count);
            if (bytes.Length != count)
                throw new EndOfStreamException("Unexpected end of the WON lobby file: " + count + " bytes expected, " + bytes.Length + " found.");
            return bytes;
        }

        /// <summary>
        /// Rejects data that cannot fit into the rest of the stream (typically a length or record count
        /// read from a corrupted file) before anything is allocated or read for it.
        /// </summary>
        /// <param name="reader">Reader positioned at the start of the data.</param>
        /// <param name="minimumBytes">Smallest number of bytes the data can occupy.</param>
        private static void EnsureAvailable(BinaryReader reader, long minimumBytes)
        {
            Stream stream = reader.BaseStream;
            if (!stream.CanSeek)
                return; // ReadBytes/ReadUInt16/... still throw at the end of the stream
            long remaining = stream.Length - stream.Position;
            if (minimumBytes > remaining)
                throw new InvalidDataException("Corrupted WON lobby file: " + minimumBytes + " bytes needed at offset " +
                                               stream.Position + " but only " + remaining + " bytes left.");
        }

        public class LobbyGlobalData
        {
            /// <summary>
            /// Smallest possible size of a <see cref="PlayerInfoGlobalData"/> record (all strings empty).
            /// </summary>
            private const int MinPlayerInfoRecordBytes = 2 + 4 + 2 + 4 + 4 + 2;

            private readonly string path;
            public uint FileSignature { get; private set; }

            public List<PlayerInfoGlobalData> PlayerInfoGlobalDatas { get; private set; }

            public bool LobbySoundEffects { get; private set; }
            public bool GlobalLobbyMusic { get; private set; }
            public uint NetworkAdapter { get; private set; }

            public class PlayerInfoGlobalData
            {
                public string Username { get; private set; }
                public uint LastUse { get; private set; }
                public ushort FileID { get; private set; }

                internal PlayerInfoGlobalData(BinaryReader reader)
                {
                    Username = ReadWideString(reader);
                    LastUse = reader.ReadUInt32();
                    FileID = reader.ReadUInt16();

                    reader.ReadUInt32(); // sysTOUTime, not used by the launcher
                    reader.ReadUInt32(); // gameTOUTime, not used by the launcher

                    // Skip the password part, it is not needed by the launcher
                    ushort passlen = reader.ReadUInt16();
                    SkipBytes(reader, passlen);
                }
            }

            /// <summary>
            /// Will automatically parse the given lobby user file
            /// </summary>
            /// <param name="path">Path to the lobby user file (_wonlobbypersistent.dat)</param>
            /// <exception cref="IOException">The file cannot be read or is truncated (<see cref="EndOfStreamException"/>).</exception>
            /// <exception cref="InvalidDataException">The file is not a valid lobby file.</exception>
            public LobbyGlobalData(string path)
            {
                this.path = path;
                PlayerInfoGlobalDatas = new List<PlayerInfoGlobalData>();

                Reload();
            }

            /// <summary>
            /// Parses lobby data (content of _wonlobbypersistent.dat) from the current position of
            /// <paramref name="input"/>. The stream is not closed.
            /// </summary>
            /// <exception cref="EndOfStreamException">The data is truncated.</exception>
            /// <exception cref="InvalidDataException">The data is not a valid lobby file.</exception>
            public LobbyGlobalData(Stream input)
            {
                PlayerInfoGlobalDatas = new List<PlayerInfoGlobalData>();
                Load(input);
            }

            /// <summary>
            /// Parses the file again. On error the previously loaded data is kept.
            /// </summary>
            /// <exception cref="InvalidOperationException">The data was parsed from a stream, not from a file.</exception>
            public void Reload()
            {
                if (path == null)
                    throw new InvalidOperationException("This lobby data was read from a stream and cannot be reloaded.");
                using (Stream input = File.OpenRead(path))
                {
                    Load(input);
                }
            }

            private void Load(Stream input)
            {
                BinaryReader reader = CreateReader(input);
                uint fileSignature = ReadFileSignature(reader);

                ushort aNumUserNames = reader.ReadUInt16();
                EnsureAvailable(reader, (long)aNumUserNames * MinPlayerInfoRecordBytes);
                var playerInfos = new List<PlayerInfoGlobalData>(aNumUserNames);
                for (int i = 0; i < aNumUserNames; ++i)
                {
                    playerInfos.Add(new PlayerInfoGlobalData(reader));
                }

                bool lobbySoundEffects = reader.ReadByte() != 0;
                bool globalLobbyMusic = reader.ReadByte() != 0;
                uint networkAdapter = reader.ReadUInt32();

                // Only publish a completely parsed file.
                FileSignature = fileSignature;
                PlayerInfoGlobalDatas.Clear();
                PlayerInfoGlobalDatas.AddRange(playerInfos);
                LobbySoundEffects = lobbySoundEffects;
                GlobalLobbyMusic = globalLobbyMusic;
                NetworkAdapter = networkAdapter;
            }
        }

        public class LobbyUserData
        {
            /// <summary>Size of a reconnect id record: 6 bytes id plus two 32-bit values.</summary>
            private const int ReconnectIdRecordBytes = 6 + 4 + 4;
            /// <summary>Smallest possible ignored player record (empty name).</summary>
            private const int MinIgnoredRecordBytes = 2;
            /// <summary>Smallest possible friend record (empty name plus WON ID).</summary>
            private const int MinFriendRecordBytes = 2 + 4;

            private readonly string path;

            public uint FileSignature { get; private set; }

            /// <summary>
            /// Dictionary of Friends, key is the player name and value is it's WON ID
            /// </summary>
            /// <remarks>If a name appears more than once, the last entry of the file wins.</remarks>
            public IDictionary<string, uint> Friends { get; private set; }

            /// <summary>
            /// Will automatically parse the given lobby user file
            /// </summary>
            /// <param name="path">Path to the lobby user file (_wonuser.dat)</param>
            /// <exception cref="IOException">The file cannot be read or is truncated (<see cref="EndOfStreamException"/>).</exception>
            /// <exception cref="InvalidDataException">The file is not a valid lobby user file.</exception>
            public LobbyUserData(string path)
            {
                this.path = path;
                this.Friends = new Dictionary<string, uint>();

                Reload();
            }

            /// <summary>
            /// Parses lobby user data (content of _wonuser&lt;id&gt;.dat) from the current position of
            /// <paramref name="input"/>. The stream is not closed.
            /// </summary>
            /// <exception cref="EndOfStreamException">The data is truncated.</exception>
            /// <exception cref="InvalidDataException">The data is not a valid lobby user file.</exception>
            public LobbyUserData(Stream input)
            {
                this.Friends = new Dictionary<string, uint>();
                Load(input);
            }

            /// <summary>
            /// Parses the file again. On error the previously loaded data is kept.
            /// </summary>
            /// <exception cref="InvalidOperationException">The data was parsed from a stream, not from a file.</exception>
            public void Reload()
            {
                if (path == null)
                    throw new InvalidOperationException("This lobby user data was read from a stream and cannot be reloaded.");
                using (Stream input = File.OpenRead(path))
                {
                    Load(input);
                }
            }

            private void Load(Stream input)
            {
                BinaryReader reader = CreateReader(input);
                uint fileSignature = ReadFileSignature(reader);

                ushort numReconnectIds = reader.ReadUInt16();
                EnsureAvailable(reader, (long)numReconnectIds * ReconnectIdRecordBytes);
                SkipBytes(reader, numReconnectIds * ReconnectIdRecordBytes);

                ushort numIgnored = reader.ReadUInt16();
                EnsureAvailable(reader, (long)numIgnored * MinIgnoredRecordBytes);
                for (int i = 0; i < numIgnored; ++i)
                    ReadWideString(reader);

                ushort numFriend = reader.ReadUInt16();
                EnsureAvailable(reader, (long)numFriend * MinFriendRecordBytes);
                var friends = new Dictionary<string, uint>();
                for (int i = 0; i < numFriend; ++i)
                {
                    string name = ReadWideString(reader);
                    uint wonId = reader.ReadUInt32();
                    friends[name] = wonId;
                }

                // Only publish a completely parsed file.
                FileSignature = fileSignature;
                Friends.Clear();
                foreach (var friend in friends)
                    Friends.Add(friend.Key, friend.Value);
            }
        }
    }
}
