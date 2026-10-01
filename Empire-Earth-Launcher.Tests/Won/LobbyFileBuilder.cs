using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Empire_Earth_Launcher.Tests.Won
{
    /// <summary>
    /// Writes WON lobby files (_wonlobbypersistent.dat, _wonuser*.dat) for the tests, field by field, in the
    /// layout documented on <see cref="Empire_Earth_WON.LobbyPersistentData"/>: little endian numbers and
    /// strings as a 16-bit character count followed by UTF-16LE code units.
    /// </summary>
    internal sealed class LobbyFileBuilder
    {
        private readonly MemoryStream stream = new MemoryStream();
        private readonly BinaryWriter writer;

        public LobbyFileBuilder()
        {
            // BinaryWriter writes little endian on every platform.
            writer = new BinaryWriter(stream);
        }

        /// <summary>The "WONPER" signature followed by its 32-bit value.</summary>
        public LobbyFileBuilder Signature(uint value = 1, string prefix = "WONPER")
        {
            writer.Write(Encoding.ASCII.GetBytes(prefix));
            writer.Write(value);
            return this;
        }

        public LobbyFileBuilder UInt16(ushort value)
        {
            writer.Write(value);
            return this;
        }

        public LobbyFileBuilder UInt32(uint value)
        {
            writer.Write(value);
            return this;
        }

        public LobbyFileBuilder Byte(byte value)
        {
            writer.Write(value);
            return this;
        }

        public LobbyFileBuilder Bytes(byte[] value)
        {
            writer.Write(value);
            return this;
        }

        /// <summary>A string: its length in UTF-16 code units, then the code units.</summary>
        public LobbyFileBuilder WideString(string value)
        {
            writer.Write((ushort)value.Length);
            writer.Write(Encoding.Unicode.GetBytes(value));
            return this;
        }

        /// <summary>A profile record of _wonlobbypersistent.dat.</summary>
        public LobbyFileBuilder PlayerInfo(string username, uint lastUse, ushort fileId, string password = "")
        {
            WideString(username);
            UInt32(lastUse);
            UInt16(fileId);
            UInt32(0x11111111); // sysTOUTime
            UInt32(0x22222222); // gameTOUTime
            byte[] passwordBytes = Encoding.ASCII.GetBytes(password);
            UInt16((ushort)passwordBytes.Length);
            Bytes(passwordBytes);
            return this;
        }

        /// <summary>A reconnect id record of _wonuser*.dat (6 bytes id, two 32-bit values).</summary>
        public LobbyFileBuilder ReconnectId(byte fill)
        {
            Bytes(new[] { fill, fill, fill, fill, fill, fill });
            UInt32(fill);
            UInt32(fill);
            return this;
        }

        public byte[] ToArray()
        {
            writer.Flush();
            return stream.ToArray();
        }

        /// <summary>A complete _wonlobbypersistent.dat with the given profiles and settings.</summary>
        public static byte[] GlobalFile(bool lobbySoundEffects, bool globalLobbyMusic, uint networkAdapter,
            params string[] usernames)
        {
            var builder = new LobbyFileBuilder().Signature().UInt16((ushort)usernames.Length);
            for (int i = 0; i < usernames.Length; i++)
                builder.PlayerInfo(usernames[i], (uint)(1000 + i), (ushort)i, "secret" + i);
            return builder
                .Byte(lobbySoundEffects ? (byte)1 : (byte)0)
                .Byte(globalLobbyMusic ? (byte)1 : (byte)0)
                .UInt32(networkAdapter)
                .ToArray();
        }

        /// <summary>A complete _wonuser*.dat with two reconnect ids, the ignored players and the friends.</summary>
        /// <param name="ignoredPlayers">Names of the ignored players.</param>
        /// <param name="friends">Name and WON ID of each friend, in file order.</param>
        public static byte[] UserFile(string[] ignoredPlayers, params KeyValuePair<string, uint>[] friends)
        {
            var builder = new LobbyFileBuilder().Signature()
                .UInt16(2).ReconnectId(0xAA).ReconnectId(0xBB)
                .UInt16((ushort)ignoredPlayers.Length);
            foreach (string ignored in ignoredPlayers)
                builder.WideString(ignored);
            builder.UInt16((ushort)friends.Length);
            foreach (KeyValuePair<string, uint> friend in friends)
                builder.WideString(friend.Key).UInt32(friend.Value);
            return builder.ToArray();
        }
    }
}
