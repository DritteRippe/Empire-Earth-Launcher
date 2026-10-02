using System;
using System.Collections.Generic;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Diagnostics
{
    /// <summary>
    /// The values of <c>WONLobby.cfg</c> the network diagnostics show: <c>CDKeyCheck</c> (NeoEE expects <c>true</c>, t=10950
    /// p=47202) and the two ports of the lobby. Read only: the launcher never offers to change the file (ARCHITECTURE 4.6);
    /// the setup installs it.
    /// </summary>
    public sealed class WonLobbyConfig
    {
        internal WonLobbyConfig(Game game, string path, bool isVirtualStoreCopy, ConfigFileStatus status, string problem,
            bool? cdKeyCheck, bool cdKeyCheckInvalid, int? fileTransferPort, int? lobbyPort)
        {
            Game = game;
            Path = path;
            IsVirtualStoreCopy = isVirtualStoreCopy;
            Status = status;
            Problem = problem;
            CdKeyCheck = cdKeyCheck;
            CdKeyCheckInvalid = cdKeyCheckInvalid;
            FileTransferPort = fileTransferPort;
            LobbyPort = lobbyPort;
        }

        public Game Game { get; }

        public string Path { get; }

        public bool IsVirtualStoreCopy { get; }

        public ConfigFileStatus Status { get; }

        /// <summary>Why the file could not be read, for the log.</summary>
        public string Problem { get; }

        /// <summary><c>CDKeyCheck</c>: null if the line is missing or its value is neither true nor false.</summary>
        public bool? CdKeyCheck { get; }

        /// <summary>True if <c>CDKeyCheck</c> exists with a value other than true or false.</summary>
        public bool CdKeyCheckInvalid { get; }

        /// <summary><c>EEFileTransferPort</c>: the TCP port of file transfers between players (33335 by default).</summary>
        public int? FileTransferPort { get; }

        /// <summary><c>LobbyPort</c>: the lobby port of a hosted game (33336 by default).</summary>
        public int? LobbyPort { get; }

        /// <summary>
        /// True if <c>CDKeyCheck</c> is not <c>true</c> in a NeoEE installation: NeoEE needs it (t=10950 p=47202), and the repair
        /// with the setup installs the file again. In EE without NeoEE <c>false</c> is the setting of the old direct connect
        /// (forum 4.3), so nothing is said there.
        /// </summary>
        public bool NeedsCdKeyCheckHint(Product product)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));
            return product == Product.NeoEE && Status == ConfigFileStatus.Read && CdKeyCheck != true;
        }
    }

    /// <summary>Reads <c>WONLobby.cfg</c> of a game folder, read-only (R7, forum 4.19, t=10950).</summary>
    /// <remarks>
    /// The format is <c>Key: value</c> with comments after <c>//</c>. Only <c>CDKeyCheck</c>, <c>EEFileTransferPort</c> and
    /// <c>LobbyPort</c> are taken; the directory servers of the file are not looked up (only the NeoEE servers are,
    /// ARCHITECTURE 4.6).
    /// </remarks>
    public static class WonLobbyConfigReader
    {
        public const string FileName = "WONLobby.cfg";

        /// <summary>Reads <c>WONLobby.cfg</c> of the folder of <paramref name="game"/> where the game reads it (ADR 0016).</summary>
        public static WonLobbyConfig Read(IFileSystem fileSystem, EffectivePathResolver effectivePaths, string gameFolder, Game game)
        {
            return Parse(KeyValueFile.Read(fileSystem, effectivePaths, gameFolder, FileName), game);
        }

        internal static WonLobbyConfig Parse(KeyValueFile file, Game game)
        {
            if (file.Status != ConfigFileStatus.Read)
                return new WonLobbyConfig(game, file.Path, file.IsVirtualStoreCopy, file.Status, file.Problem, null, false, null, null);
            IReadOnlyDictionary<string, string> values = file.Values("//");
            bool? cdKeyCheck = KeyValueFile.Boolean(values, "CDKeyCheck");
            return new WonLobbyConfig(game, file.Path, file.IsVirtualStoreCopy, ConfigFileStatus.Read, null, cdKeyCheck,
                KeyValueFile.IsInvalid(values, "CDKeyCheck", cdKeyCheck), KeyValueFile.Port(values, "EEFileTransferPort"),
                KeyValueFile.Port(values, "LobbyPort"));
        }
    }
}
