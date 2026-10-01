using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Empire_Earth_WON
{
    // Well it's not a lot be enough for minimal informations

    /// <summary>
    /// Client for the NeoEE lobby status service.
    /// </summary>
    /// <remarks>
    /// Framing: every packet starts with a 2-byte little endian size that counts the whole packet,
    /// including the size field itself. A request is the size (3) followed by the <see cref="RequestType"/>
    /// byte. A reply is the size followed by (size - 2) bytes of UTF-8 text whose fields are separated by a
    /// request specific character.
    /// </remarks>
    public class NeoAPI
    {
        public const string DefaultHost = "titan.empireearth.eu";
        public const int DefaultPort = 10005;

        /// <summary>
        /// Default limit for connecting (including the DNS lookup), for sending and for each receive.
        /// </summary>
        public const int DefaultTimeoutMilliseconds = 3000;

        /// <summary>Size of the length field that starts every packet.</summary>
        public const int HeaderSize = 2;

        private string ip;
        private int port;
        private int timeoutMilliseconds;

        public enum RequestType
        {
            INFO_MESSAGE = 7, CONNECTED_PLAYERS_MESSAGE = 8, GAMES_MESSAGE = 9, CHAT_MESSAGE = 10
        }

        private RequestType requestType;

        public NeoAPI(RequestType requestType, string ip = DefaultHost, int port = DefaultPort,
            int timeoutMilliseconds = DefaultTimeoutMilliseconds)
        {
            if (timeoutMilliseconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds), "The timeout must be positive.");
            this.requestType = requestType;
            this.ip = ip;
            this.port = port;
            this.timeoutMilliseconds = timeoutMilliseconds;
        }

        /// <summary>
        /// Result of <see cref="SendRequest"/>: either the reply fields or the error. Never null.
        /// </summary>
        public sealed class Reply
        {
            private static readonly string[] NoFields = new string[0];

            private Reply(string[] fields, Exception error)
            {
                Fields = fields ?? NoFields;
                Error = error;
            }

            public static Reply Succeeded(string[] fields)
            {
                if (fields == null)
                    throw new ArgumentNullException(nameof(fields));
                return new Reply(fields, null);
            }

            public static Reply Failed(Exception error)
            {
                if (error == null)
                    throw new ArgumentNullException(nameof(error));
                return new Reply(null, error);
            }

            public bool Success
            {
                get { return Error == null; }
            }

            /// <summary>The reply split into its fields; empty (never null) when the request failed.</summary>
            public string[] Fields { get; private set; }

            /// <summary>Why the request failed, or null on success.</summary>
            public Exception Error { get; private set; }
        }

        /// <summary>
        /// Sends the request and reads the reply. Network and protocol errors (unreachable server, timeout,
        /// closed connection, invalid size) do not throw: they are returned in <see cref="Reply.Error"/>, so
        /// the caller decides how to report them (e.g. once per outage instead of every poll).
        /// </summary>
        public Reply SendRequest(char split)
        {
            try
            {
                using (var tcpclient = new TcpClient())
                {
                    Connect(tcpclient, ip, port, timeoutMilliseconds);
                    tcpclient.SendTimeout = timeoutMilliseconds;
                    tcpclient.ReceiveTimeout = timeoutMilliseconds;

                    using (NetworkStream stream = tcpclient.GetStream())
                    {
                        stream.WriteTimeout = timeoutMilliseconds;
                        stream.ReadTimeout = timeoutMilliseconds;

                        WriteRequest(stream, requestType);
                        return Reply.Succeeded(ReadReply(stream, split));
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is SocketException || ex is TimeoutException ||
                                       ex is InvalidDataException || ex is ObjectDisposedException)
            {
                return Reply.Failed(ex);
            }
        }

        /// <summary>
        /// Connects with a timeout. TcpClient.Connect ignores SendTimeout/ReceiveTimeout and blocks for the
        /// OS connect timeout when the server does not answer; BeginConnect also covers the DNS lookup.
        /// </summary>
        private static void Connect(TcpClient tcpclient, string host, int port, int timeoutMilliseconds)
        {
            IAsyncResult pendingConnect = tcpclient.BeginConnect(host, port, null, null);
            if (!pendingConnect.AsyncWaitHandle.WaitOne(timeoutMilliseconds))
            {
                tcpclient.Close(); // aborts the pending connect
                throw new TimeoutException("Connecting to " + host + ":" + port + " timed out after " +
                                           timeoutMilliseconds + " ms.");
            }
            tcpclient.EndConnect(pendingConnect);
        }

        /// <summary>
        /// Writes a request packet for <paramref name="requestType"/> to <paramref name="stream"/>.
        /// </summary>
        public static void WriteRequest(Stream stream, RequestType requestType)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            byte[] packet = new byte[HeaderSize + 1] { HeaderSize + 1, 0, (byte)requestType };
            stream.Write(packet, 0, packet.Length);
            stream.Flush();
        }

        /// <summary>
        /// Reads one reply packet from <paramref name="stream"/> and splits its text at <paramref name="split"/>.
        /// </summary>
        /// <exception cref="EndOfStreamException">The stream ends before the announced size was read.</exception>
        /// <exception cref="InvalidDataException">The size field is smaller than the header itself.</exception>
        public static string[] ReadReply(Stream stream, char split)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            byte[] header = new byte[HeaderSize];
            ReadExactly(stream, header, 0, header.Length);
            int size = header[0] | (header[1] << 8); // little endian, whatever the CPU is

            // The size counts the header too, so anything below the header size cannot be a valid reply.
            // The upper bound is the 16-bit field itself (64 KiB), small enough to allocate.
            if (size < HeaderSize)
                throw new InvalidDataException("Invalid Neo reply: announced size " + size +
                                               " is smaller than the " + HeaderSize + "-byte header.");

            byte[] payload = new byte[size - HeaderSize];
            ReadExactly(stream, payload, 0, payload.Length);

            return Encoding.UTF8.GetString(payload, 0, payload.Length).Split(split);
        }

        /// <summary>
        /// Reads exactly <paramref name="count"/> bytes. Stream.Read may return fewer bytes than requested
        /// (e.g. when a TCP reply arrives in several segments), so it is called until the buffer is full.
        /// </summary>
        /// <exception cref="EndOfStreamException">The stream ended (the server closed the connection) first.</exception>
        public static void ReadExactly(Stream stream, byte[] buffer, int offset, int count)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset > buffer.Length - count)
                throw new ArgumentOutOfRangeException(nameof(count));

            while (count > 0)
            {
                int read = stream.Read(buffer, offset, count);
                if (read <= 0)
                    throw new EndOfStreamException("The connection was closed " + count + " bytes before the end of the Neo reply.");
                offset += read;
                count -= read;
            }
        }

        public class InfoMessage
        {
            /// <summary>Field separator of an INFO_MESSAGE reply.</summary>
            public const char Separator = ' ';

            public enum ServerState
            {
                OFFLINE = 0, ONLINE = 1, BUSY = 2, INSTALLING = 3
            }

            public Version Version { get; private set; }
            public string CodeName { get; private set; }

            /// <summary>Server state; may be a value newer than the known <see cref="ServerState"/> members.</summary>
            public ServerState State { get; private set; }
            public int OnlinePlayers { get; private set; }

            private InfoMessage()
            {
            }

            /// <summary>
            /// Parses the fields of an INFO_MESSAGE reply: version, code name, server state, online players.
            /// </summary>
            /// <exception cref="FormatException">The fields are missing or not valid.</exception>
            public static InfoMessage Parse(string[] fields)
            {
                if (fields == null)
                    throw new ArgumentNullException(nameof(fields));
                if (fields.Length < 4)
                    throw new FormatException("InfoMessage needs 4 fields, the reply has " + fields.Length + ".");

                Version version;
                if (!Version.TryParse(fields[0], out version))
                    throw new FormatException("Invalid server version \"" + fields[0] + "\".");

                return new InfoMessage
                {
                    Version = version,
                    CodeName = fields[1],
                    State = (ServerState)ParseNumber(fields[2], "server state"),
                    OnlinePlayers = ParseCount(fields[3], "online player count")
                };
            }

            /// <summary>
            /// Requests the server information. Never throws for network or protocol errors.
            /// </summary>
            /// <returns>true and the message, or false and the error.</returns>
            public static bool TryRequest(out InfoMessage message, out Exception error)
            {
                message = null;
                Reply reply = new NeoAPI(RequestType.INFO_MESSAGE).SendRequest(Separator);
                if (!reply.Success)
                {
                    error = reply.Error;
                    return false;
                }

                try
                {
                    message = Parse(reply.Fields);
                }
                catch (FormatException ex)
                {
                    error = ex;
                    return false;
                }
                error = null;
                return true;
            }
        }

        public class ConnectedPlayersMessage
        {
            /// <summary>Field separator of a CONNECTED_PLAYERS_MESSAGE reply.</summary>
            public const char Separator = '\x0B';

            /// <summary>Number of reply fields per player: name, WON ID, game state.</summary>
            public const int FieldsPerPlayer = 3;

            public class PlayerInfo
            {
                public enum PlayerGameState
                {
                    LOBBY = 0, ROOM = 1, PLAYING = 2
                }

                public string Name { get; private set; }
                public uint WON_ID { get; private set; }

                /// <summary>Game state; may be a value newer than the known <see cref="PlayerGameState"/> members.</summary>
                public PlayerGameState GameState { get; private set; }

                public PlayerInfo(string name, uint wonId, PlayerGameState gameState)
                {
                    Name = name;
                    WON_ID = wonId;
                    GameState = gameState;
                }

                /// <summary>
                /// Display text of <see cref="GameState"/>, with a fallback for states the launcher does not know.
                /// </summary>
                public string GameStateToString()
                {
                    switch (GameState)
                    {
                        case PlayerGameState.LOBBY:
                            return "Lobby";
                        case PlayerGameState.ROOM:
                            return "Room";
                        case PlayerGameState.PLAYING:
                            return "Playing";
                        default:
                            return "Unknown (" + (int)GameState + ")";
                    }
                }
            }

            public int OnlinePlayers { get; private set; }
            public List<PlayerInfo> PlayersInfo { get; private set; }

            private ConnectedPlayersMessage(int onlinePlayers, List<PlayerInfo> playersInfo)
            {
                OnlinePlayers = onlinePlayers;
                PlayersInfo = playersInfo;
            }

            /// <summary>
            /// Parses the fields of a CONNECTED_PLAYERS_MESSAGE reply: the player count followed by
            /// <see cref="FieldsPerPlayer"/> fields per player. The message is only created when the whole
            /// reply is valid, so a bad reply never yields a half filled list.
            /// </summary>
            /// <exception cref="FormatException">The count does not match the data or a field is invalid.</exception>
            public static ConnectedPlayersMessage Parse(string[] fields)
            {
                if (fields == null)
                    throw new ArgumentNullException(nameof(fields));
                if (fields.Length == 0 || string.IsNullOrWhiteSpace(fields[0]))
                    throw new FormatException("The connected players reply is empty.");

                int onlinePlayers = ParseCount(fields[0], "player count");

                // Trust the announced count only as far as the reply really contains player data.
                int playersInReply = (fields.Length - 1) / FieldsPerPlayer;
                if (onlinePlayers > playersInReply)
                    throw new FormatException("The server announced " + onlinePlayers + " players but sent data for " +
                                              playersInReply + ".");

                var playersInfo = new List<PlayerInfo>(onlinePlayers);
                for (int player = 0; player < onlinePlayers; ++player)
                {
                    int i = 1 + player * FieldsPerPlayer;
                    playersInfo.Add(new PlayerInfo(
                        fields[i],
                        ParseWonId(fields[i + 1]),
                        (PlayerInfo.PlayerGameState)ParseNumber(fields[i + 2], "game state")));
                }

                return new ConnectedPlayersMessage(onlinePlayers, playersInfo);
            }

            /// <summary>
            /// Requests the list of connected players. Never throws for network or protocol errors.
            /// </summary>
            /// <returns>true and the message, or false and the error.</returns>
            public static bool TryRequest(out ConnectedPlayersMessage message, out Exception error)
            {
                message = null;
                Reply reply = new NeoAPI(RequestType.CONNECTED_PLAYERS_MESSAGE).SendRequest(Separator);
                if (!reply.Success)
                {
                    error = reply.Error;
                    return false;
                }

                try
                {
                    message = Parse(reply.Fields);
                }
                catch (FormatException ex)
                {
                    error = ex;
                    return false;
                }
                error = null;
                return true;
            }

            private static uint ParseWonId(string text)
            {
                uint wonId;
                if (!uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out wonId))
                    throw new FormatException("Invalid WON ID \"" + text + "\".");
                return wonId;
            }
        }

        private static int ParseNumber(string text, string what)
        {
            int value;
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
                throw new FormatException("Invalid " + what + " \"" + text + "\".");
            return value;
        }

        private static int ParseCount(string text, string what)
        {
            int value = ParseNumber(text, what);
            if (value < 0)
                throw new FormatException("Invalid " + what + " " + value + ".");
            return value;
        }
    }
}
