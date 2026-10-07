using System;

namespace Empire_Earth_WON
{
    /// <summary>
    /// Address of a NeoEE lobby status server and the time limit for each network operation.
    /// </summary>
    /// <remarks>Immutable; the arguments are validated when it is created.</remarks>
    public sealed class NeoServerEndpoint
    {
        /// <param name="host">Host name or IP address of the server.</param>
        /// <param name="port">TCP port of the status service (1-65535).</param>
        /// <param name="timeoutMilliseconds">Limit for connecting (including the DNS lookup), for sending and
        /// for each receive; must be positive.</param>
        /// <exception cref="ArgumentException"><paramref name="host"/> is empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="port"/> or
        /// <paramref name="timeoutMilliseconds"/> is out of range.</exception>
        public NeoServerEndpoint(string host, int port, int timeoutMilliseconds)
        {
            if (string.IsNullOrWhiteSpace(host))
                throw new ArgumentException("The host of the Neo server is missing.", nameof(host));
            if (port < 1 || port > 65535)
                throw new ArgumentOutOfRangeException(nameof(port), port, "The port must be between 1 and 65535.");
            if (timeoutMilliseconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds), timeoutMilliseconds,
                    "The timeout must be positive.");

            Host = host.Trim();
            Port = port;
            TimeoutMilliseconds = timeoutMilliseconds;
        }

        public string Host { get; }

        public int Port { get; }

        public int TimeoutMilliseconds { get; }

        public override string ToString()
        {
            return Host + ":" + Port;
        }
    }
}
