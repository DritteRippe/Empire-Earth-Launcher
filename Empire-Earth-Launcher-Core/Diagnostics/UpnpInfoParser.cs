using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Diagnostics
{
    /// <summary>What <see cref="UpnpInfoParser"/> made of <c>upnp_info.txt</c>.</summary>
    public enum UpnpInfoStatus
    {
        /// <summary>There is no <c>upnp_info.txt</c> (NeoEE writes it while it hosts, if at all).</summary>
        Missing,

        /// <summary>The file exists but could not be read.</summary>
        Unreadable,

        /// <summary>Nothing in the file was recognized: shown as "unknown format" (the format is not documented).</summary>
        UnknownFormat,

        /// <summary>At least one address or port line was recognized.</summary>
        Recognized
    }

    /// <summary>A port line of <c>upnp_info.txt</c>: the port, its protocol and whether the router accepted it.</summary>
    public sealed class UpnpPortLine
    {
        internal UpnpPortLine(int port, string protocol, bool? succeeded)
        {
            Port = port;
            Protocol = protocol;
            Succeeded = succeeded;
        }

        public int Port { get; }

        /// <summary><c>TCP</c> or <c>UDP</c>.</summary>
        public string Protocol { get; }

        /// <summary>True for a line that says the forwarding worked, false for one with an error, null if it says neither.</summary>
        public bool? Succeeded { get; }
    }

    /// <summary>
    /// The result of <see cref="UpnpInfoParser"/>. The external address is kept only as its class (ADR 0013 plan review):
    /// the value never leaves the parser, so it can neither be shown nor logged.
    /// </summary>
    public sealed class UpnpInfo
    {
        internal UpnpInfo(Game game, string path, UpnpInfoStatus status, string problem, IPv4Class? externalAddressClass,
            IPAddress localAddress, IEnumerable<UpnpPortLine> ports)
        {
            Game = game;
            Path = path;
            Status = status;
            Problem = problem;
            ExternalAddressClass = externalAddressClass;
            LocalAddress = localAddress;
            Ports = new ReadOnlyCollection<UpnpPortLine>((ports ?? Enumerable.Empty<UpnpPortLine>()).ToList());
        }

        public Game Game { get; }

        public string Path { get; }

        public UpnpInfoStatus Status { get; }

        /// <summary>Why the file could not be read, for the log.</summary>
        public string Problem { get; }

        /// <summary>The class of the external IPv4 address the router reported; null if none was recognized.</summary>
        public IPv4Class? ExternalAddressClass { get; }

        /// <summary>The local IPv4 address of the computer the router forwards to, if private (it may be shown); else null.</summary>
        public IPAddress LocalAddress { get; }

        /// <summary>The recognized port lines (at most <see cref="UpnpInfoParser.MaxPortLines"/>).</summary>
        public IReadOnlyList<UpnpPortLine> Ports { get; }
    }

    /// <summary>
    /// A tolerant reader of <c>upnp_info.txt</c>, which NeoEE may write into the game folder (forum report section 8 row 8;
    /// the setup removes it with the other runtime files). Its format is not documented and no sample was available, so the
    /// parser only takes what it recognizes in any layout and calls everything else "unknown format" (ARCHITECTURE 14; the
    /// test plan collects samples, WP9-05):
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>a line with a label that names the external side (<c>external</c>, <c>WAN</c>, <c>public</c>) followed by an IPv4
    /// address: the external address, kept as its class only;</item>
    /// <item>a line with a label for the local side (<c>local</c>, <c>LAN</c>, <c>internal</c>, <c>client</c>) followed by a
    /// private IPv4 address: the local address;</item>
    /// <item>a line with <c>TCP</c> or <c>UDP</c> and a port from 1024 to 65535: a port line, succeeded if it says
    /// <c>success</c>, <c>ok</c>, <c>added</c>, <c>mapped</c>, <c>redirected</c> or <c>forwarded</c>, failed if it says
    /// <c>fail</c>, <c>error</c>, <c>denied</c> or <c>conflict</c>.</item>
    /// </list>
    /// Nothing of the file is logged (it may hold the external address).
    /// </remarks>
    public static class UpnpInfoParser
    {
        public const string FileName = "upnp_info.txt";

        /// <summary>At most this many port lines are kept.</summary>
        public const int MaxPortLines = 10;

        private static readonly Regex IPv4 = new Regex(@"(?<![\d.])(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})(?![\d.])",
            RegexOptions.CultureInvariant);

        private static readonly Regex ExternalLabel = new Regex(@"\b(?:extern\w*|wan\w*|public\w*)", RegexOptions.CultureInvariant |
            RegexOptions.IgnoreCase);

        private static readonly Regex LocalLabel = new Regex(@"\b(?:local\w*|lan\w*|intern\w*|client\w*)",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly Regex Protocol = new Regex(@"\b(TCP|UDP)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly Regex PortNumber = new Regex(@"(?<![\d./])(\d{4,5})(?![\d.])", RegexOptions.CultureInvariant);

        private static readonly Regex Success = new Regex(@"\b(?:success\w*|ok|added|mapped|redirected|forwarded)\b",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly Regex Failure = new Regex(@"\b(?:fail\w*|error\w*|denied|conflict\w*)",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        /// <summary>Reads <c>upnp_info.txt</c> of the folder of <paramref name="game"/> where the game writes it (ADR 0016).</summary>
        public static UpnpInfo Read(IFileSystem fileSystem, EffectivePathResolver effectivePaths, string gameFolder, Game game)
        {
            KeyValueFile file = KeyValueFile.Read(fileSystem, effectivePaths, gameFolder, FileName);
            switch (file.Status)
            {
                case ConfigFileStatus.Missing:
                    return new UpnpInfo(game, file.Path, UpnpInfoStatus.Missing, null, null, null, null);
                case ConfigFileStatus.Unreadable:
                    return new UpnpInfo(game, file.Path, UpnpInfoStatus.Unreadable, file.Problem, null, null, null);
                default:
                    return Parse(file.Text, game, file.Path);
            }
        }

        /// <summary>Parses the text of <c>upnp_info.txt</c>; never throws for any text.</summary>
        public static UpnpInfo Parse(string text, Game game, string path = null)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            IPv4Class? external = null;
            IPAddress local = null;
            var ports = new List<UpnpPortLine>();
            foreach (string rawLine in text.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0)
                    continue;
                Match address = IPv4.Match(line);
                IPAddress parsed = address.Success ? ParseIPv4(address) : null;
                if (parsed != null)
                {
                    string label = line.Substring(0, address.Index);
                    if (external == null && ExternalLabel.IsMatch(label))
                        external = AddressClassifier.ClassOf(parsed);
                    else if (local == null && LocalLabel.IsMatch(label) && AddressClassifier.MayShow(parsed))
                        local = parsed;
                }
                if (ports.Count < MaxPortLines)
                    AddPortLine(line, ports);
            }
            bool recognized = external != null || local != null || ports.Count > 0;
            return new UpnpInfo(game, path, recognized ? UpnpInfoStatus.Recognized : UpnpInfoStatus.UnknownFormat, null, external,
                local, ports);
        }

        private static void AddPortLine(string line, List<UpnpPortLine> ports)
        {
            Match protocol = Protocol.Match(line);
            if (!protocol.Success)
                return;
            // The address parts are no ports: remove them first.
            string withoutAddresses = IPv4.Replace(line, " ");
            foreach (Match number in PortNumber.Matches(withoutAddresses))
            {
                int port = int.Parse(number.Value, CultureInfo.InvariantCulture);
                if (port < 1024 || port > 65535)
                    continue;
                bool? succeeded = Failure.IsMatch(line) ? false : Success.IsMatch(line) ? true : (bool?)null;
                ports.Add(new UpnpPortLine(port, protocol.Value.ToUpperInvariant(), succeeded));
                return;
            }
        }

        private static IPAddress ParseIPv4(Match match)
        {
            var bytes = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                if (!int.TryParse(match.Groups[i + 1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int part) ||
                    part > 255)
                    return null;
                bytes[i] = (byte)part;
            }
            return new IPAddress(bytes);
        }
    }
}
