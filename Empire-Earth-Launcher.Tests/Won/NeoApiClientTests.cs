using System;
using System.IO;
using System.Linq;
using System.Text;
using Empire_Earth_Launcher.Tests.TestSupport;
using Empire_Earth_WON;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Won
{
    /// <summary>
    /// Framing and parsing of the NeoEE status protocol, on in-memory streams: no connection is opened.
    /// </summary>
    [TestFixture]
    public class NeoApiClientTests
    {
        private const char PlayerSeparator = NeoApiClient.ConnectedPlayersMessage.Separator;

        /// <summary>A reply packet: 2-byte little endian size (including itself), then the UTF-8 text.</summary>
        private static byte[] ReplyPacket(string text)
        {
            byte[] payload = Encoding.UTF8.GetBytes(text);
            int size = payload.Length + NeoApiClient.HeaderSize;
            return new[] { (byte)(size & 0xFF), (byte)(size >> 8) }.Concat(payload).ToArray();
        }

        private static string Join(params string[] fields)
        {
            return string.Join(PlayerSeparator.ToString(), fields);
        }

        /* Framing */

        [TestCase(NeoApiClient.RequestType.Info, (byte)7)]
        [TestCase(NeoApiClient.RequestType.ConnectedPlayers, (byte)8)]
        public void WriteRequest_WritesSizeThenRequestType(NeoApiClient.RequestType requestType, byte typeByte)
        {
            using (var stream = new MemoryStream())
            {
                NeoApiClient.WriteRequest(stream, requestType);

                Assert.That(stream.ToArray(), Is.EqualTo(new byte[] { 3, 0, typeByte }));
            }
        }

        [Test]
        public void ReadReply_SplitsTheTextAtTheSeparator()
        {
            using (var stream = new MemoryStream(ReplyPacket("1.2.3 Neo 1 42")))
            {
                Assert.That(NeoApiClient.ReadReply(stream, ' '), Is.EqualTo(new[] { "1.2.3", "Neo", "1", "42" }));
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(5)]
        public void ReadReply_ReplyArrivingInSmallChunks_IsReadCompletely(int bytesPerRead)
        {
            // The header and the multi-byte UTF-8 characters are split across reads; a single Read used to
            // return a truncated, zero padded reply (korr-S4).
            string text = Join("2", "Jürgen", "42", "0", "玩家", "4294967295", "2");
            var stream = new ChunkedReadStream(ReplyPacket(text), bytesPerRead);

            string[] fields = NeoApiClient.ReadReply(stream, PlayerSeparator);

            Assert.That(fields, Is.EqualTo(text.Split(PlayerSeparator)));
            Assert.That(stream.RemainingBytes, Is.EqualTo(0));
            Assert.That(stream.ReadCalls, Is.GreaterThan(1));
        }

        [Test]
        public void ReadReply_LongReply_IsReadCompletely()
        {
            string name = new string('x', 60000); // close to the 64 KiB limit of the size field
            var stream = new ChunkedReadStream(ReplyPacket("1" + PlayerSeparator + name), 1460); // TCP segment size

            string[] fields = NeoApiClient.ReadReply(stream, PlayerSeparator);

            Assert.That(fields.Length, Is.EqualTo(2));
            Assert.That(fields[1], Is.EqualTo(name));
        }

        [Test]
        public void ReadReply_ReadsExactlyOnePacket()
        {
            byte[] first = ReplyPacket("first");
            byte[] data = first.Concat(ReplyPacket("second")).ToArray();
            using (var stream = new MemoryStream(data))
            {
                Assert.That(NeoApiClient.ReadReply(stream, ' '), Is.EqualTo(new[] { "first" }));
                Assert.That(stream.Position, Is.EqualTo(first.Length));
                Assert.That(NeoApiClient.ReadReply(stream, ' '), Is.EqualTo(new[] { "second" }));
            }
        }

        [Test]
        public void ReadReply_SizeOfOnlyTheHeader_GivesOneEmptyField()
        {
            using (var stream = new MemoryStream(new byte[] { 2, 0 }))
            {
                Assert.That(NeoApiClient.ReadReply(stream, ' '), Is.EqualTo(new[] { string.Empty }));
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        public void ReadReply_SizeSmallerThanTheHeader_ThrowsInvalidData(int size)
        {
            using (var stream = new MemoryStream(new byte[] { (byte)size, 0, 65, 66, 67 }))
            {
                Assert.That(() => NeoApiClient.ReadReply(stream, ' '), Throws.TypeOf<InvalidDataException>());
            }
        }

        [Test]
        public void ReadReply_ConnectionClosedBeforeTheAnnouncedSize_ThrowsEndOfStream()
        {
            byte[] packet = ReplyPacket("1.2.3 Neo 1 42");
            byte[] truncated = packet.Take(packet.Length - 3).ToArray();

            Assert.That(() => NeoApiClient.ReadReply(new ChunkedReadStream(truncated, 2), ' '),
                Throws.TypeOf<EndOfStreamException>());
        }

        [TestCase(0)]
        [TestCase(1)]
        public void ReadReply_ConnectionClosedInTheHeader_ThrowsEndOfStream(int headerBytes)
        {
            Assert.That(() => NeoApiClient.ReadReply(new MemoryStream(new byte[headerBytes]), ' '),
                Throws.TypeOf<EndOfStreamException>());
        }

        [Test]
        public void ReadReply_SizeIsLittleEndian()
        {
            // 0x0105 = 261 bytes: 2 header + 259 payload. Big endian would announce 0x0501 = 1281 bytes.
            string text = new string('a', 259);
            byte[] packet = ReplyPacket(text);
            Assert.That(packet.Take(2), Is.EqualTo(new byte[] { 0x05, 0x01 }));

            using (var stream = new MemoryStream(packet))
            {
                Assert.That(NeoApiClient.ReadReply(stream, ' '), Is.EqualTo(new[] { text }));
            }
        }

        [Test]
        public void ReadExactly_FillsTheBufferAtTheOffset()
        {
            var stream = new ChunkedReadStream(new byte[] { 1, 2, 3, 4 }, 1);
            byte[] buffer = new byte[6];

            NeoApiClient.ReadExactly(stream, buffer, 1, 4);

            Assert.That(buffer, Is.EqualTo(new byte[] { 0, 1, 2, 3, 4, 0 }));
            Assert.That(stream.ReadCalls, Is.EqualTo(4));
        }

        [Test]
        public void ReadExactly_InvalidArguments_Throw()
        {
            var stream = new MemoryStream(new byte[10]);

            Assert.That(() => NeoApiClient.ReadExactly(null, new byte[1], 0, 1), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => NeoApiClient.ReadExactly(stream, null, 0, 1), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => NeoApiClient.ReadExactly(stream, new byte[4], 2, 3), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => NeoApiClient.ReadExactly(stream, new byte[4], -1, 1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => NeoApiClient.ReadExactly(stream, new byte[4], 0, -1), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        /* Info reply */

        [Test]
        public void InfoMessage_ValidReply_IsParsed()
        {
            var message = NeoApiClient.InfoMessage.Parse(new[] { "1.2.3", "Neo", "1", "42" });

            Assert.That(message.Version, Is.EqualTo(new Version(1, 2, 3)));
            Assert.That(message.CodeName, Is.EqualTo("Neo"));
            Assert.That(message.State, Is.EqualTo(NeoApiClient.InfoMessage.ServerState.Online));
            Assert.That(message.OnlinePlayers, Is.EqualTo(42));
        }

        [Test]
        public void InfoMessage_UnknownServerState_IsKeptAsNumber()
        {
            var message = NeoApiClient.InfoMessage.Parse(new[] { "1.0", "Neo", "9", "0" });

            Assert.That((int)message.State, Is.EqualTo(9));
        }

        [TestCase("1.2.3 Neo 1", TestName = "InfoMessage_TooFewFields_ThrowsFormat")]
        [TestCase("one Neo 1 42", TestName = "InfoMessage_InvalidVersion_ThrowsFormat")]
        [TestCase("1.2.3 Neo x 42", TestName = "InfoMessage_InvalidState_ThrowsFormat")]
        [TestCase("1.2.3 Neo 1 -5", TestName = "InfoMessage_NegativePlayerCount_ThrowsFormat")]
        [TestCase("1.2.3 Neo 1 99999999999", TestName = "InfoMessage_PlayerCountOverflow_ThrowsFormat")]
        public void InfoMessage_InvalidReply_ThrowsFormat(string reply)
        {
            string[] fields = reply.Split(NeoApiClient.InfoMessage.Separator);

            Assert.That(() => NeoApiClient.InfoMessage.Parse(fields), Throws.TypeOf<FormatException>());
        }

        /* ConnectedPlayers reply */

        [Test]
        public void ConnectedPlayers_ValidReply_IsParsed()
        {
            string[] fields = Join("2", "Jürgen", "42", "0", "Bob", "4294967295", "2").Split(PlayerSeparator);

            var message = NeoApiClient.ConnectedPlayersMessage.Parse(fields);

            Assert.That(message.OnlinePlayers, Is.EqualTo(2));
            Assert.That(message.PlayersInfo.Select(p => p.Name), Is.EqualTo(new[] { "Jürgen", "Bob" }));
            Assert.That(message.PlayersInfo.Select(p => p.WonId), Is.EqualTo(new[] { 42u, uint.MaxValue }));
            Assert.That(message.PlayersInfo.Select(p => p.GameState), Is.EqualTo(new[]
            {
                NeoApiClient.ConnectedPlayersMessage.PlayerInfo.PlayerGameState.Lobby,
                NeoApiClient.ConnectedPlayersMessage.PlayerInfo.PlayerGameState.Playing
            }));
        }

        [Test]
        public void ConnectedPlayers_NoPlayers_GivesAnEmptyList()
        {
            var message = NeoApiClient.ConnectedPlayersMessage.Parse(new[] { "0" });

            Assert.That(message.OnlinePlayers, Is.EqualTo(0));
            Assert.That(message.PlayersInfo, Is.Empty);
        }

        [Test]
        public void ConnectedPlayers_MoreDataThanAnnounced_UsesTheAnnouncedPlayers()
        {
            string[] fields = Join("1", "Alice", "1", "0", "Bob", "2", "0").Split(PlayerSeparator);

            var message = NeoApiClient.ConnectedPlayersMessage.Parse(fields);

            Assert.That(message.PlayersInfo.Select(p => p.Name), Is.EqualTo(new[] { "Alice" }));
        }

        [TestCase("3|Alice|1|0|Bob|2|0", TestName = "ConnectedPlayers_MorePlayersAnnouncedThanSent_ThrowsFormat")]
        [TestCase("1|Alice|1", TestName = "ConnectedPlayers_IncompletePlayer_ThrowsFormat")]
        [TestCase("-1", TestName = "ConnectedPlayers_NegativeCount_ThrowsFormat")]
        [TestCase("two|Alice|1|0", TestName = "ConnectedPlayers_InvalidCount_ThrowsFormat")]
        [TestCase("", TestName = "ConnectedPlayers_EmptyReply_ThrowsFormat")]
        [TestCase("1|Alice|-1|0", TestName = "ConnectedPlayers_NegativeWonId_ThrowsFormat")]
        [TestCase("1|Alice|4294967296|0", TestName = "ConnectedPlayers_WonIdOverflow_ThrowsFormat")]
        [TestCase("1|Alice|1|lobby", TestName = "ConnectedPlayers_InvalidGameState_ThrowsFormat")]
        public void ConnectedPlayers_InvalidReply_ThrowsFormat(string reply)
        {
            // "|" stands for the separator character (vertical tab) to keep the test names readable.
            string[] fields = reply.Replace('|', PlayerSeparator).Split(PlayerSeparator);

            Assert.That(() => NeoApiClient.ConnectedPlayersMessage.Parse(fields), Throws.TypeOf<FormatException>());
        }

        /* Reply and endpoint */

        [Test]
        public void Reply_FailedRequest_HasNoFieldsAndTheError()
        {
            var error = new EndOfStreamException();

            NeoApiClient.Reply reply = NeoApiClient.Reply.Failed(error);

            Assert.That(reply.Success, Is.False);
            Assert.That(reply.Error, Is.SameAs(error));
            Assert.That(reply.Fields, Is.Not.Null.And.Empty);
        }

        [Test]
        public void Reply_RequiresFieldsOrAnError()
        {
            Assert.That(() => NeoApiClient.Reply.Succeeded(null), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => NeoApiClient.Reply.Failed(null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Endpoint_ValidArguments_AreKept()
        {
            var endpoint = new NeoServerEndpoint(" neo.example.invalid ", 25000, 1500);

            Assert.That(endpoint.Host, Is.EqualTo("neo.example.invalid"));
            Assert.That(endpoint.Port, Is.EqualTo(25000));
            Assert.That(endpoint.TimeoutMilliseconds, Is.EqualTo(1500));
            Assert.That(new NeoApiClient(endpoint).Endpoint, Is.SameAs(endpoint));
        }

        [Test]
        public void Endpoint_InvalidArguments_Throw()
        {
            Assert.That(() => new NeoServerEndpoint(" ", 25000, 1500), Throws.TypeOf<ArgumentException>());
            Assert.That(() => new NeoServerEndpoint("host", 0, 1500), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new NeoServerEndpoint("host", 65536, 1500), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new NeoServerEndpoint("host", 25000, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new NeoApiClient(null), Throws.TypeOf<ArgumentNullException>());
        }
    }
}
