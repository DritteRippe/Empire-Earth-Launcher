using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Empire_Earth_Launcher.Core.Diagnostics
{
    /// <summary>
    /// What identifies the player and the computer and therefore never appears in the diagnostics report or in the log lines
    /// of the network diagnostics (ADR 0013 plan review): the Windows user name, the profile folders, the computer name and
    /// the domain. <c>Program</c> takes them from Windows; the tests pass synthetic ones.
    /// </summary>
    public sealed class PrivateNames
    {
        public PrivateNames(string userName, IEnumerable<string> computerNames, string domainName, string userProfile,
            string localApplicationData)
        {
            UserName = Clean(userName);
            ComputerNames = new ReadOnlyCollection<string>((computerNames ?? Enumerable.Empty<string>())
                .Select(Clean).Where(name => name != null).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
            DomainName = Clean(domainName);
            UserProfile = TrimSeparators(Clean(userProfile));
            LocalApplicationData = TrimSeparators(Clean(localApplicationData));
        }

        /// <summary>The Windows user name (<c>Environment.UserName</c>); null if unknown.</summary>
        public string UserName { get; }

        /// <summary>The names of the computer (NetBIOS and DNS host name).</summary>
        public IReadOnlyList<string> ComputerNames { get; }

        /// <summary>The domain of the computer; null if none.</summary>
        public string DomainName { get; }

        /// <summary><c>%USERPROFILE%</c> (<c>C:\Users\Name</c>); null if unknown.</summary>
        public string UserProfile { get; }

        /// <summary><c>%LOCALAPPDATA%</c>; null if unknown.</summary>
        public string LocalApplicationData { get; }

        /// <summary>
        /// The folder names that stand for the player: the user name and the name of the profile folder, which differs from it
        /// for a renamed account (<c>C:\Users\Maik.PC</c>).
        /// </summary>
        internal IEnumerable<string> UserSegments
        {
            get
            {
                if (UserName != null)
                    yield return UserName;
                string profileFolder = UserProfile == null ? null : UserProfile.Split('\\', '/').LastOrDefault();
                if (!string.IsNullOrEmpty(profileFolder) && profileFolder.IndexOf(':') < 0 &&
                    !string.Equals(profileFolder, UserName, StringComparison.OrdinalIgnoreCase))
                    yield return profileFolder;
            }
        }

        private static string Clean(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        private static string TrimSeparators(string path)
        {
            if (path == null)
                return null;
            string trimmed = path.TrimEnd('\\', '/');
            return trimmed.Length < 3 ? null : trimmed;
        }
    }

    /// <summary>
    /// The privacy rules of the diagnostics report and of the log lines of the network diagnostics (ADR 0013 plan review,
    /// ARCHITECTURE 4.6): paths without the user name and the computer name, IPv4 addresses only where they may be shown,
    /// IPv6 only as a class.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>A path below <c>%LOCALAPPDATA%</c> or <c>%USERPROFILE%</c> starts with that variable instead.</item>
    /// <item>Every path segment that is the user name or the name of the profile folder becomes <c>&lt;user&gt;</c> (also in
    /// <c>D:\Users\&lt;name&gt;</c> and VirtualStore paths), and a segment that starts with it and a dot as well
    /// (<c>Name.PC</c>); a segment that is a computer name becomes <c>&lt;computer&gt;</c>, the domain <c>&lt;domain&gt;</c>.</item>
    /// <item>The server of a UNC path becomes <c>&lt;computer&gt;</c> when it is this computer, else <c>&lt;server&gt;</c> (a
    /// NAS, an address or the domain: no server name is ever shown).</item>
    /// <item>An IPv4 address is shown only when it is private or link-local; any other as <c>&lt;public address&gt;</c>,
    /// <c>&lt;CGNAT address&gt;</c> and so on.</item>
    /// </list>
    /// MAC addresses, adapter GUIDs and names, DNS suffixes and player names never reach the anonymizer: the report and the
    /// log lines are built without them.
    /// </remarks>
    public sealed class ReportAnonymizer
    {
        public const string UserPlaceholder = "<user>";
        public const string ComputerPlaceholder = "<computer>";
        public const string ServerPlaceholder = "<server>";
        public const string DomainPlaceholder = "<domain>";

        private readonly PrivateNames names;

        public ReportAnonymizer(PrivateNames names)
        {
            this.names = names ?? throw new ArgumentNullException(nameof(names));
        }

        /// <summary>A path without the user name and the computer name; an empty text for null.</summary>
        public string Path(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;
            string text = ReplacePrefix(path, names.LocalApplicationData, "%LOCALAPPDATA%")
                          ?? ReplacePrefix(path, names.UserProfile, "%USERPROFILE%")
                          ?? path;
            var result = new StringBuilder(text.Length);
            int start = 0;
            bool unc = text.StartsWith(@"\\", StringComparison.Ordinal) && !text.StartsWith(@"\\?\", StringComparison.Ordinal) &&
                       !text.StartsWith(@"\\.\", StringComparison.Ordinal);
            if (unc)
            {
                int end = text.IndexOfAny(new[] { '\\', '/' }, 2);
                string server = end < 0 ? text.Substring(2) : text.Substring(2, end - 2);
                result.Append(@"\\").Append(IsComputer(server) ? ComputerPlaceholder : ServerPlaceholder);
                if (end < 0)
                    return result.ToString();
                start = end;
            }
            int segmentStart = start;
            for (int i = start; i <= text.Length; i++)
            {
                if (i < text.Length && text[i] != '\\' && text[i] != '/')
                    continue;
                result.Append(Segment(text.Substring(segmentStart, i - segmentStart)));
                if (i < text.Length)
                    result.Append(text[i]);
                segmentStart = i + 1;
            }
            return result.ToString();
        }

        /// <summary>
        /// An IPv4 address as it may be shown: the value for private and link-local addresses, else its class
        /// (<c>&lt;public address&gt;</c>); an IPv6 address only as <c>&lt;IPv6 address&gt;</c>.
        /// </summary>
        public static string Address(IPAddress address)
        {
            if (address == null)
                return string.Empty;
            if (address.AddressFamily != AddressFamily.InterNetwork)
                return "<IPv6 address>";
            return AddressClassifier.MayShow(address) ? address.ToString() : "<" + ClassName(AddressClassifier.ClassOf(address)) + " address>";
        }

        /// <summary>The English name of an IPv4 class for the report and the log.</summary>
        public static string ClassName(IPv4Class addressClass)
        {
            switch (addressClass)
            {
                case IPv4Class.Private:
                    return "private";
                case IPv4Class.LinkLocal:
                    return "link-local";
                case IPv4Class.Cgnat:
                    return "CGNAT";
                case IPv4Class.Public:
                    return "public";
                case IPv4Class.Unspecified:
                    return "unspecified (0.0.0.0)";
                default:
                    return "special";
            }
        }

        /// <summary>The English name of an IPv6 class for the report and the log.</summary>
        public static string ClassName(IPv6Class addressClass)
        {
            switch (addressClass)
            {
                case IPv6Class.None:
                    return "none";
                case IPv6Class.LinkLocalOnly:
                    return "link-local only";
                default:
                    return "global";
            }
        }

        private string Segment(string segment)
        {
            if (segment.Length == 0)
                return segment;
            foreach (string user in names.UserSegments)
            {
                if (string.Equals(segment, user, StringComparison.OrdinalIgnoreCase) ||
                    segment.StartsWith(user + ".", StringComparison.OrdinalIgnoreCase))
                    return UserPlaceholder;
            }
            if (IsComputer(segment))
                return ComputerPlaceholder;
            if (IsDomain(segment))
                return DomainPlaceholder;
            return segment;
        }

        /// <summary>True for a name of this computer, also in its domain (<c>PC.corp.example</c>).</summary>
        private bool IsComputer(string name)
        {
            foreach (string computer in names.ComputerNames)
            {
                if (string.Equals(name, computer, StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith(computer + ".", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>True for the domain of the computer or a name in it.</summary>
        private bool IsDomain(string name)
        {
            string domain = names.DomainName;
            return domain != null && (string.Equals(name, domain, StringComparison.OrdinalIgnoreCase) ||
                                      name.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary><paramref name="variable"/> plus the rest if <paramref name="path"/> is <paramref name="prefix"/> or below it; else null.</summary>
        private static string ReplacePrefix(string path, string prefix, string variable)
        {
            if (prefix == null || !path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return null;
            if (path.Length == prefix.Length)
                return variable;
            char next = path[prefix.Length];
            return next == '\\' || next == '/' ? variable + path.Substring(prefix.Length) : null;
        }

        /// <summary>Counts for the report and the log: <c>1 file</c>, <c>2 files</c>.</summary>
        internal static string Count(int count, string singular, string plural)
        {
            return count.ToString(CultureInfo.InvariantCulture) + " " + (count == 1 ? singular : plural);
        }
    }
}
