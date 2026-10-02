using System;

namespace Empire_Earth_Launcher.Core.Repair
{
    /// <summary>
    /// Which download URL of the update API the launcher may open (contract 4.3 step 2, ADR 0008): a port of
    /// <c>IsAllowedUpdateUrl</c>, <c>SplitHttpsUrl</c> and <c>IsDomainOrSubdomain</c> of the setup's <c>utils.iss</c>, so that
    /// setup and launcher accept exactly the same URLs. Its tests contain every case of the setup's
    /// <c>TestIsAllowedUpdateUrl</c> (<c>ci/tests/unit_tests.iss</c>) with the same names, plus the launcher's own.
    /// </summary>
    /// <remarks>
    /// Allowed: <c>https://</c> (the scheme compared ignoring case, as <c>CompareText</c> does); no user information, port,
    /// backslash, space, control or non-ASCII character anywhere; host <c>empireearth.eu</c>, <c>neoee.net</c> or a subdomain
    /// of either, or <c>github.com</c> with a path that starts with <c>/EE-modders/</c> (ignoring case) and contains neither
    /// <c>..</c> nor <c>%</c> (anyone can publish on GitHub).
    /// </remarks>
    public static class UpdateUrlPolicy
    {
        /// <summary>The website of the community (<c>DomainMain</c> of the setup).</summary>
        public const string DomainMain = "empireearth.eu";

        /// <summary>The website of NeoEE (<c>DomainNeoEE</c>).</summary>
        public const string DomainNeoEE = "neoee.net";

        /// <summary>GitHub (<c>GitHubHost</c>); only the organization below is allowed there.</summary>
        public const string GitHubHost = "github.com";

        /// <summary>The organization of the community on GitHub (<c>GitHubProjectPath</c>).</summary>
        public const string GitHubProjectPath = "/EE-modders/";

        private const string HttpsPrefix = "https://";

        /// <summary>True if the update API may send the player to <paramref name="url"/> (<c>IsAllowedUpdateUrl</c>).</summary>
        public static bool IsAllowed(string url)
        {
            if (!TrySplitHttpsUrl(url, out string host, out string path))
                return false;
            if (IsDomainOrSubdomain(host, DomainMain) || IsDomainOrSubdomain(host, DomainNeoEE))
                return true;
            return host == GitHubHost &&
                   path.StartsWith(GitHubProjectPath, StringComparison.OrdinalIgnoreCase) &&
                   path.IndexOf("..", StringComparison.Ordinal) < 0 &&
                   path.IndexOf('%') < 0;
        }

        /// <summary>
        /// <c>SplitHttpsUrl</c>: the host (lowercase) and the rest (<c>/</c> if empty) of an absolute https URL; false for
        /// anything else and for URLs a check of the host could be fooled with: user information (<c>@</c>), ports, backslashes,
        /// spaces, control and non-ASCII characters.
        /// </summary>
        internal static bool TrySplitHttpsUrl(string url, out string host, out string path)
        {
            host = string.Empty;
            path = string.Empty;
            if (url == null || !url.StartsWith(HttpsPrefix, StringComparison.OrdinalIgnoreCase))
                return false;
            foreach (char c in url)
            {
                if (c <= ' ' || c >= (char)127 || c == '\\')
                    return false;
            }

            string rest = url.Substring(HttpsPrefix.Length);
            int end = rest.IndexOfAny(new[] { '/', '?', '#' });
            if (end >= 0)
            {
                path = rest.Substring(end);
                host = rest.Substring(0, end);
            }
            else
            {
                path = "/";
                host = rest;
            }
            // Only ASCII is left, so the invariant lower-casing is the setup's LowerCase.
            host = host.ToLowerInvariant();
            return host.Length > 0 && host.IndexOf('@') < 0 && host.IndexOf(':') < 0;
        }

        /// <summary><c>IsDomainOrSubdomain</c>: <paramref name="host"/> is <paramref name="domain"/> or one of its subdomains (both lowercase).</summary>
        internal static bool IsDomainOrSubdomain(string host, string domain)
        {
            return host == domain ||
                   (host.Length > domain.Length + 1 && host.EndsWith("." + domain, StringComparison.Ordinal));
        }
    }
}
