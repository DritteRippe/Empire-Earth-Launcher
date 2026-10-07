using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Empire_Earth_WON
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
        /// <summary>
        /// File in the game folder with the lobby profiles of all users (<see cref="LobbyGlobalData"/>).
        /// </summary>
        public const string GlobalDataFileName = "_wonlobbypersistent.dat";

        private const string UserDataFilePrefix = "_wonuser";
        private const string UserDataFileExtension = ".dat";

        private const string FileSignaturePrefix = "WONPER";

        /// <summary>
        /// File in the game folder with the data of one lobby profile (<see cref="LobbyUserData"/>), e.g.
        /// "_wonuser3.dat".
        /// </summary>
        /// <param name="fileId"><see cref="LobbyGlobalData.PlayerInfoGlobalData.FileID"/> of the profile.</param>
        public static string GetUserDataFileName(ushort fileId)
        {
            return UserDataFilePrefix + fileId.ToString(CultureInfo.InvariantCulture) + UserDataFileExtension;
        }

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

            public List<PlayerInfoGlobalData> PlayerInfos { get; private set; }

            /// <summary>
            /// Whether the file contains the lobby settings that follow the profiles (<see cref="LobbySoundEffects"/>,
            /// <see cref="GlobalLobbyMusic"/>, <see cref="NetworkAdapter"/>). They are optional: a file that ends
            /// before or inside them is still accepted, its profiles are kept and these settings have their
            /// default values. The launcher only needs the profiles, and the original parser tolerated such files.
            /// </summary>
            public bool HasLobbySettings { get; private set; }

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
                    ushort passwordLength = reader.ReadUInt16();
                    SkipBytes(reader, passwordLength);
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
                PlayerInfos = new List<PlayerInfoGlobalData>();

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
                PlayerInfos = new List<PlayerInfoGlobalData>();
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

                ushort userNameCount = reader.ReadUInt16();
                EnsureAvailable(reader, (long)userNameCount * MinPlayerInfoRecordBytes);
                var playerInfos = new List<PlayerInfoGlobalData>(userNameCount);
                for (int i = 0; i < userNameCount; ++i)
                {
                    playerInfos.Add(new PlayerInfoGlobalData(reader));
                }

                // Optional trailer, see HasLobbySettings. A truncated profile above still throws.
                bool hasLobbySettings = false;
                bool lobbySoundEffects = false;
                bool globalLobbyMusic = false;
                uint networkAdapter = 0;
                try
                {
                    lobbySoundEffects = reader.ReadByte() != 0;
                    globalLobbyMusic = reader.ReadByte() != 0;
                    networkAdapter = reader.ReadUInt32();
                    hasLobbySettings = true;
                }
                catch (EndOfStreamException)
                {
                    // Missing or cut off: keep the defaults, not half of the values.
                    lobbySoundEffects = false;
                    globalLobbyMusic = false;
                    networkAdapter = 0;
                }

                // Only publish a completely parsed file.
                FileSignature = fileSignature;
                PlayerInfos.Clear();
                PlayerInfos.AddRange(playerInfos);
                HasLobbySettings = hasLobbySettings;
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

                ushort reconnectIdCount = reader.ReadUInt16();
                EnsureAvailable(reader, (long)reconnectIdCount * ReconnectIdRecordBytes);
                SkipBytes(reader, reconnectIdCount * ReconnectIdRecordBytes);

                ushort ignoredCount = reader.ReadUInt16();
                EnsureAvailable(reader, (long)ignoredCount * MinIgnoredRecordBytes);
                for (int i = 0; i < ignoredCount; ++i)
                    ReadWideString(reader);

                ushort friendCount = reader.ReadUInt16();
                EnsureAvailable(reader, (long)friendCount * MinFriendRecordBytes);
                var friends = new Dictionary<string, uint>();
                for (int i = 0; i < friendCount; ++i)
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
