using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;

namespace Empire_Earth_Launcher.WON
{
    // Well it's not a lot be enough for minimal informations

    public class NeoAPI
    {
        private string ip;
        private int port;

        public enum RequestType
        {
            INFO_MESSAGE = 7, CONNECTED_PLAYERS_MESSAGE = 8, GAMES_MESSAGE = 9, CHAT_MESSAGE = 10
        }

        private RequestType requestType;

        public NeoAPI(RequestType requestType, string ip = "titan.empireearth.eu", int port = 10005)
        {
            this.requestType = requestType;
            this.ip = ip;
            this.port = port;
        }

        public string[] SendRequest(char split)
        {
            string[] result = null;

            try
            {
                using (var tcpclient = new TcpClient())
                {
                    tcpclient.SendTimeout = 1000;
                    tcpclient.ReceiveTimeout = 1000;

                    tcpclient.Connect(ip, port);

                    using (NetworkStream stream = tcpclient.GetStream())
                    {
                        // Format request
                        byte[] buffer = new byte[3] { 3, 0, (byte)requestType };
                        stream.Write(buffer, 0, buffer.Length);

                        // Get reply
                        byte[] rec = new byte[2];
                        int len = stream.Read(rec, 0, rec.Length);

                        if (len == 0)
                            throw new Exception("Server socket is closed !");
                        ushort size = BitConverter.ToUInt16(rec, 0);
                        if (size == 0)
                            throw new Exception("Server packet size is 0 !");

                        // Get reply by defining packet size and removing already rec
                        rec = new byte[size - 2];
                        stream.Read(rec, 0, rec.Length);


                        result = Encoding.UTF8.GetString(rec, 0, rec.Length).Split(split);

                        if (result.Length != 0)
                        {
                            Console.WriteLine("Reply from NeoAPI for " + requestType);
                            foreach(string reply in result)
                                Console.WriteLine(reply);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Program.Logging.Log("Error while sending request to Neo !", Logging.LogLevel.Warning);
                Program.Logging.Log(ex.ToString(), Logging.LogLevel.Warning);
            }
            return result;
        }

        public class InfoMessage
        {
            public enum ServerState
            {
                OFFLINE = 0, ONLINE = 1, BUSY = 2, INSTALLING = 3
            }

            public Version Version { get; private set; }
            public string CodeName { get; private set; }
            public ServerState State { get; private set; }
            public int OnlinePlayers { get; private set; }

            public InfoMessage()
            {
                Reload();
            }

            public bool Reload()
            {
                string[] neo_api_reply = new NeoAPI(RequestType.INFO_MESSAGE).SendRequest(' ');

                if (neo_api_reply.Length == 0 || string.IsNullOrWhiteSpace(neo_api_reply[0]))
                    return false;

                try
                {

                    Version = Version.Parse(neo_api_reply[0]);
                    CodeName = neo_api_reply[1];
                    State = (ServerState) int.Parse(neo_api_reply[2]);
                    OnlinePlayers = int.Parse(neo_api_reply[3]);
                }
                catch (Exception ex)
                {
                    Program.Logging.Log("Unnable to parse InfoMessage from Neo !");
                    Program.Logging.Log(ex.ToString(), Logging.LogLevel.Warning);
                    return false;
                }
                return true;
            }

        }

        public class ConnectedPlayersMessage
        {
            public class PlayerInfo
            {
                public enum PlayerGameState
                {
                    LOBBY = 0, ROOM = 1, PLAYING = 2
                }

                public string Name { get; private set; }
                public uint WON_ID { get; private set; }
                public PlayerGameState GameState { get; private set; }

                public PlayerInfo(string[] player_info_part)
                {
                    if (player_info_part.Length != 3)
                        return;
                    Name = player_info_part[0];
                    WON_ID = uint.Parse(player_info_part[1]);
                    GameState = (PlayerGameState) int.Parse(player_info_part[2]);
                }

                public string GameStateToString(PlayerGameState gameState)
                {
                    switch (gameState)
                    {
                        case PlayerGameState.LOBBY:
                            return "Lobby";
                        case PlayerGameState.ROOM:
                            return "Room";
                        case PlayerGameState.PLAYING:
                            return "Playing";
                        default:
                            return null;
                    }
                }

            }

            public int OnlinePlayers { get; private set; }
            public List<PlayerInfo> PlayersInfo { get; private set; }


            public ConnectedPlayersMessage()
            {
                PlayersInfo = new List<PlayerInfo>();
                Reload();
            }

            public bool Reload()
            {
                string[] neo_api_reply = new NeoAPI(RequestType.CONNECTED_PLAYERS_MESSAGE).SendRequest('\x0B');

                if (neo_api_reply.Length == 0 || string.IsNullOrWhiteSpace(neo_api_reply[0]))
                    return false;

                try
                {

                    OnlinePlayers = int.Parse(neo_api_reply[0]);

                    if (OnlinePlayers != 0)
                    {
                        for (int i = 1; i != (OnlinePlayers * 3) + 1; i += 3)
                        {
                            PlayerInfo playerInfo = new PlayerInfo(new string[3] { neo_api_reply[i], neo_api_reply[(i + 1)], neo_api_reply[(i + 2)] });
                            PlayersInfo.Add(playerInfo);
                        }
                    }
                    
                }
                catch (Exception ex)
                {
                    Program.Logging.Log("Unnable to parse ConnectedPlayersMessage from Neo !");
                    Program.Logging.Log(ex.ToString(), Logging.LogLevel.Warning);
                    return false;
                }
                return true;
            }

        }

    }
}
