using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Diagnostics
{
    /// <summary>
    /// The values of <c>NeoEE.cfg</c> the network diagnostics show (ARCHITECTURE 4.6): the RIP hosting configuration that the
    /// NeoEE setup installs in both game folders (class <c>mutable</c>). Read only; the launcher never changes it.
    /// </summary>
    public sealed class NeoEeConfig
    {
        internal NeoEeConfig(Game game, string path, bool isVirtualStoreCopy, ConfigFileStatus status, string problem,
            bool? active, string server, int? defaultPort, int? memberPorts, bool? portCheck, bool? tryUpnp,
            IEnumerable<string> invalidKeys)
        {
            Game = game;
            Path = path;
            IsVirtualStoreCopy = isVirtualStoreCopy;
            Status = status;
            Problem = problem;
            Active = active;
            Server = server;
            DefaultPort = defaultPort;
            MemberPorts = memberPorts;
            PortCheck = portCheck;
            TryUpnp = tryUpnp;
            InvalidKeys = new ReadOnlyCollection<string>((invalidKeys ?? Enumerable.Empty<string>()).ToList());
        }

        public Game Game { get; }

        /// <summary>The file that was read (the VirtualStore copy if the game uses it).</summary>
        public string Path { get; }

        public bool IsVirtualStoreCopy { get; }

        public ConfigFileStatus Status { get; }

        /// <summary>Why the file could not be read, for the log.</summary>
        public string Problem { get; }

        /// <summary><c>Active</c>: RIP hosting on (NeoEE hosts most games without port forwarding, t=5843 p=39204); null if unknown.</summary>
        public bool? Active { get; }

        /// <summary><c>Server</c>: the NeoEE server of RIP hosting (a host name); null if missing or not a host name.</summary>
        public string Server { get; }

        /// <summary><c>DefaultPort</c>: the game port the host forwards (33334 by default, t=11057 p=48100); null if unknown.</summary>
        public int? DefaultPort { get; }

        /// <summary><c>MemberPorts</c>: the relay member ports of RIP hosting (33340 by default); null if unknown.</summary>
        public int? MemberPorts { get; }

        /// <summary><c>PortCheck</c>: NeoEE checks whether <see cref="DefaultPort"/> is forwarded; null if unknown.</summary>
        public bool? PortCheck { get; }

        /// <summary><c>TryUPnP</c>: NeoEE asks the router to forward the ports (UPnP); null if unknown.</summary>
        public bool? TryUpnp { get; }

        /// <summary>The keys whose value is not what NeoEE expects (e.g. <c>Active: maybe</c>).</summary>
        public IReadOnlyList<string> InvalidKeys { get; }
    }

    /// <summary>Reads <c>NeoEE.cfg</c> of a game folder, read-only (R7, forum t=11057 p=48100, t=5843 p=39204).</summary>
    /// <remarks>
    /// The format is the one of the file the NeoEE setup installs: <c>Key:</c>, tabs, the value, and a comment after <c>#</c>.
    /// Unknown keys are ignored, an invalid value is reported in <see cref="NeoEeConfig.InvalidKeys"/>. The values are not
    /// secret (server name, ports, switches), so the report may show them.
    /// </remarks>
    public static class NeoEeConfigReader
    {
        public const string FileName = "NeoEE.cfg";

        private static readonly Regex HostName =
            new Regex(@"^(?=.{1,253}$)[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?)*$",
                RegexOptions.CultureInvariant);

        /// <summary>Reads <c>NeoEE.cfg</c> of the folder of <paramref name="game"/> where the game reads it (ADR 0016).</summary>
        public static NeoEeConfig Read(IFileSystem fileSystem, EffectivePathResolver effectivePaths, string gameFolder, Game game)
        {
            return Parse(KeyValueFile.Read(fileSystem, effectivePaths, gameFolder, FileName), game);
        }

        internal static NeoEeConfig Parse(KeyValueFile file, Game game)
        {
            if (file.Status != ConfigFileStatus.Read)
                return new NeoEeConfig(game, file.Path, file.IsVirtualStoreCopy, file.Status, file.Problem, null, null, null, null,
                    null, null, null);
            IReadOnlyDictionary<string, string> values = file.Values("#");
            var invalid = new List<string>();
            bool? active = Boolean(values, "Active", invalid);
            string server = null;
            if (values.TryGetValue("Server", out string serverText))
            {
                if (IsHostName(serverText))
                    server = serverText;
                else
                    invalid.Add("Server");
            }
            int? defaultPort = Port(values, "DefaultPort", invalid);
            int? memberPorts = Port(values, "MemberPorts", invalid);
            bool? portCheck = Boolean(values, "PortCheck", invalid);
            bool? tryUpnp = Boolean(values, "TryUPnP", invalid);
            return new NeoEeConfig(game, file.Path, file.IsVirtualStoreCopy, ConfigFileStatus.Read, null, active, server,
                defaultPort, memberPorts, portCheck, tryUpnp, invalid);
        }

        /// <summary>A host name of letters, digits, hyphens and dots (an address is a host name too); nothing else is looked up.</summary>
        public static bool IsHostName(string text)
        {
            return !string.IsNullOrEmpty(text) && HostName.IsMatch(text);
        }

        private static bool? Boolean(IReadOnlyDictionary<string, string> values, string key, List<string> invalid)
        {
            bool? value = KeyValueFile.Boolean(values, key);
            if (KeyValueFile.IsInvalid(values, key, value))
                invalid.Add(key);
            return value;
        }

        private static int? Port(IReadOnlyDictionary<string, string> values, string key, List<string> invalid)
        {
            int? value = KeyValueFile.Port(values, key);
            if (KeyValueFile.IsInvalid(values, key, value))
                invalid.Add(key);
            return value;
        }
    }
}
