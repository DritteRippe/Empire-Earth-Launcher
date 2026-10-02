using System;
using Empire_Earth_Launcher.Core.Lobby;

namespace Empire_Earth_Launcher.Core.Diagnostics
{
    /// <summary>What the NeoEE status server did when the network diagnostics asked it for the online players.</summary>
    public enum StatusServerAnswer
    {
        /// <summary>It sent a valid list: the server runs.</summary>
        Answered,

        /// <summary>No valid answer (no connection, timeout, closed connection, nonsense).</summary>
        NoAnswer,

        /// <summary>The server settings of the launcher are invalid, so it was not asked (the player list is off too).</summary>
        NotConfigured
    }

    /// <summary>What the update API did when the network diagnostics asked it (the request of contract 4.3, ADR 0008).</summary>
    public enum UpdateApiAnswer
    {
        /// <summary>It answered with any HTTP status: the computer reaches the internet over HTTPS.</summary>
        Answered,

        /// <summary>No answer: timeout, TLS or network error.</summary>
        NoAnswer,

        /// <summary>Not asked: no installation has an AppId, the only thing the launcher may send (ADR 0008).</summary>
        NotAsked
    }

    /// <summary>
    /// The outage hint (forum report section 8 row 9, ARCHITECTURE 4.6; not droppable, REV-05): whether a missing player list
    /// is probably an outage of the NeoEE server or a problem of this computer.
    /// </summary>
    public enum OutageVerdict
    {
        /// <summary>The status server answers: no outage.</summary>
        ServerAnswers,

        /// <summary>
        /// DNS resolves the server and the update API answers, but the status server does not: "probably a server outage, not
        /// your computer".
        /// </summary>
        ProbablyServerOutage,

        /// <summary>The update API answers, but the name of the status server cannot be resolved: a DNS problem or a changed name.</summary>
        ServerNameNotResolved,

        /// <summary>Neither the name resolves nor the update API answers (or it could not be asked): the computer seems offline.</summary>
        NoConnection,

        /// <summary>The name resolves, but neither server answers: firewall, antivirus, proxy - or both servers are down.</summary>
        NoServerReached,

        /// <summary>The name resolves and the status server does not answer, but the update API could not be asked (no AppId).</summary>
        Undetermined,

        /// <summary>The status server is not configured (invalid settings in <c>Empire Earth Launcher.exe.config</c>).</summary>
        StatusServerNotConfigured
    }

    /// <summary>
    /// Decides the outage hint from the three answers of the network diagnostics, and when the Play page links to it.
    /// </summary>
    public static class OutageHint
    {
        /// <summary>The verdict for every combination of the name lookup, the update API and the status server.</summary>
        /// <param name="statusHostResolves">True if DNS resolved the name of the status server.</param>
        /// <param name="updateApi">What the update API did.</param>
        /// <param name="statusServer">What the status server did.</param>
        public static OutageVerdict Evaluate(bool statusHostResolves, UpdateApiAnswer updateApi, StatusServerAnswer statusServer)
        {
            switch (statusServer)
            {
                case StatusServerAnswer.Answered:
                    return OutageVerdict.ServerAnswers;
                case StatusServerAnswer.NotConfigured:
                    return OutageVerdict.StatusServerNotConfigured;
            }
            switch (updateApi)
            {
                case UpdateApiAnswer.Answered:
                    return statusHostResolves ? OutageVerdict.ProbablyServerOutage : OutageVerdict.ServerNameNotResolved;
                case UpdateApiAnswer.NoAnswer:
                    return statusHostResolves ? OutageVerdict.NoServerReached : OutageVerdict.NoConnection;
                default:
                    return statusHostResolves ? OutageVerdict.Undetermined : OutageVerdict.NoConnection;
            }
        }

        /// <summary>
        /// True if the player list of the Play page links to the network check: when the list is "not available" (the server
        /// did not answer). A list that arrives, or a polling that ended by an error of the launcher (logged), has no link.
        /// </summary>
        public static bool LinksToNetworkCheck(PlayerListStatus status)
        {
            return status == PlayerListStatus.Unavailable;
        }

        /// <summary>True for a verdict that says the problem is the server, not the computer.</summary>
        public static bool IsServerSide(OutageVerdict verdict)
        {
            return verdict == OutageVerdict.ProbablyServerOutage || verdict == OutageVerdict.ServerNameNotResolved;
        }

        /// <summary>The verdict for the log (English).</summary>
        public static string Describe(OutageVerdict verdict)
        {
            switch (verdict)
            {
                case OutageVerdict.ServerAnswers:
                    return "the NeoEE status server answers";
                case OutageVerdict.ProbablyServerOutage:
                    return "probably a server outage, not this computer (DNS works and the update API answers, the status server does not)";
                case OutageVerdict.ServerNameNotResolved:
                    return "the name of the status server is not resolved although the update API answers";
                case OutageVerdict.NoConnection:
                    return "no connection: the name does not resolve and the update API does not answer";
                case OutageVerdict.NoServerReached:
                    return "no server answers although the name resolves (firewall, antivirus, proxy, or both servers down)";
                case OutageVerdict.Undetermined:
                    return "undetermined: the status server does not answer and the update API could not be asked (no AppId)";
                case OutageVerdict.StatusServerNotConfigured:
                    return "the status server is not configured";
                default:
                    throw new ArgumentOutOfRangeException(nameof(verdict), verdict, null);
            }
        }
    }
}
