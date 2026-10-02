using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Empire_Earth_Launcher.Core.Diagnostics
{
    /// <summary>
    /// The class of an IPv4 address (ADR 0013 plan review): a public address is shown only as its class, never as its value,
    /// because it identifies the connection of the player.
    /// </summary>
    public enum IPv4Class
    {
        /// <summary>A private address of a home network (10/8, 172.16/12, 192.168/16, RFC 1918): may be shown.</summary>
        Private,

        /// <summary>A link-local address (169.254/16): Windows found no DHCP server. May be shown.</summary>
        LinkLocal,

        /// <summary>The shared address space of carrier-grade NAT (100.64/10, RFC 6598): the provider shares one public address.</summary>
        Cgnat,

        /// <summary>A public address: only its class is shown.</summary>
        Public,

        /// <summary>0.0.0.0: no address (a router without an external IPv4, e.g. DS-Lite).</summary>
        Unspecified,

        /// <summary>Loopback, multicast, broadcast and the other special ranges.</summary>
        Special
    }

    /// <summary>The IPv6 state of an adapter or a computer, the only way IPv6 is shown (ADR 0013 plan review).</summary>
    public enum IPv6Class
    {
        /// <summary>No IPv6 address.</summary>
        None,

        /// <summary>Only link-local (fe80::/10) or unique local (fc00::/7) addresses: no IPv6 internet.</summary>
        LinkLocalOnly,

        /// <summary>A global IPv6 address: IPv6 internet.</summary>
        Global
    }

    /// <summary>Classifies addresses for the network diagnostics and decides which ones may be shown.</summary>
    public static class AddressClassifier
    {
        /// <summary>The class of an IPv4 address; <see cref="ArgumentException"/> for an IPv6 address.</summary>
        public static IPv4Class ClassOf(IPAddress address)
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));
            if (address.AddressFamily != AddressFamily.InterNetwork)
                throw new ArgumentException("Only IPv4 addresses have an IPv4 class.", nameof(address));
            byte[] b = address.GetAddressBytes();
            if (b[0] == 0 && b[1] == 0 && b[2] == 0 && b[3] == 0)
                return IPv4Class.Unspecified;
            if (b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168))
                return IPv4Class.Private;
            if (b[0] == 169 && b[1] == 254)
                return IPv4Class.LinkLocal;
            if (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
                return IPv4Class.Cgnat;
            if (b[0] == 0 || b[0] == 127 || b[0] >= 224)
                return IPv4Class.Special;
            return IPv4Class.Public;
        }

        /// <summary>
        /// True if an IPv4 address may be shown as it is: private and link-local addresses of the home network (ADR 0013 plan
        /// review); every other address is shown as its class only.
        /// </summary>
        public static bool MayShow(IPAddress address)
        {
            IPv4Class addressClass = ClassOf(address);
            return addressClass == IPv4Class.Private || addressClass == IPv4Class.LinkLocal;
        }

        /// <summary>The IPv6 class of a set of addresses (the IPv4 ones are ignored).</summary>
        public static IPv6Class IPv6ClassOf(IEnumerable<IPAddress> addresses)
        {
            if (addresses == null)
                throw new ArgumentNullException(nameof(addresses));
            List<IPAddress> ipv6 = addresses.Where(address => address.AddressFamily == AddressFamily.InterNetworkV6).ToList();
            if (ipv6.Count == 0)
                return IPv6Class.None;
            return ipv6.Any(IsGlobalIPv6) ? IPv6Class.Global : IPv6Class.LinkLocalOnly;
        }

        /// <summary>A global unicast IPv6 address (2000::/3), not link-local, unique local, loopback or multicast.</summary>
        public static bool IsGlobalIPv6(IPAddress address)
        {
            if (address == null)
                throw new ArgumentNullException(nameof(address));
            if (address.AddressFamily != AddressFamily.InterNetworkV6)
                return false;
            byte first = address.GetAddressBytes()[0];
            return (first & 0xE0) == 0x20;
        }
    }
}
