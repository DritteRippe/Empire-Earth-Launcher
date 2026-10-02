using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Repair
{
    /// <summary>Why the fixed download page is used instead of the URL of the update API (contract 4.3 step 3).</summary>
    public enum FallbackReason
    {
        /// <summary>The URL comes from the update API.</summary>
        None,

        /// <summary>The update API was not asked (yet): the advice shows the fixed page until it has an answer.</summary>
        NotAsked,

        /// <summary>The installation has no AppId (a foreign installation).</summary>
        NoAppId,

        /// <summary>No answer within 10 seconds.</summary>
        Timeout,

        /// <summary>The TLS handshake failed (Windows 7 without a common cipher suite, a wrong certificate, R16).</summary>
        TlsError,

        /// <summary>No connection: DNS, offline, proxy, an answer larger than 4 KiB.</summary>
        NetworkError,

        /// <summary>An answer with a status other than 200 (also a redirect, which is never followed).</summary>
        StatusNotOk,

        /// <summary>The answer is not a URL that <see cref="UpdateUrlPolicy"/> allows (also an empty answer).</summary>
        UrlRejected
    }

    /// <summary>Where the setup is downloaded (contract 4.3): the URL and, for the fixed page, why it is used.</summary>
    public sealed class SetupDownloadLocation
    {
        private SetupDownloadLocation(string url, FallbackReason reason, string detail)
        {
            Url = url;
            Reason = reason;
            Detail = detail;
        }

        /// <summary>The fixed page before the update API was asked.</summary>
        public static SetupDownloadLocation NotAsked { get; } =
            new SetupDownloadLocation(SetupDownloadLocator.FixedPageUrl, FallbackReason.NotAsked, null);

        /// <summary>The URL to open in the browser: from the update API, or the fixed page.</summary>
        public string Url { get; }

        /// <summary><see cref="FallbackReason.None"/> for a URL of the update API, else why the fixed page is used.</summary>
        public FallbackReason Reason { get; }

        /// <summary>The status or error behind <see cref="Reason"/>, for the log; null if there is none.</summary>
        public string Detail { get; }

        /// <summary>True if the URL comes from the update API.</summary>
        public bool IsFromUpdateApi
        {
            get { return Reason == FallbackReason.None; }
        }

        internal static SetupDownloadLocation FromUpdateApi(string url)
        {
            return new SetupDownloadLocation(url, FallbackReason.None, null);
        }

        internal static SetupDownloadLocation FixedPage(FallbackReason reason, string detail)
        {
            return new SetupDownloadLocation(SetupDownloadLocator.FixedPageUrl, reason, detail);
        }

        public override string ToString()
        {
            return IsFromUpdateApi ? Url + " (update API)" : Url + " (fixed page: " + Reason + (Detail == null ? ")" : ", " + Detail + ")");
        }
    }

    /// <summary>
    /// Finds the download of the current setup (contract 4.3, ADR 0008): with an AppId it asks the update API
    /// <c>GET https://api.empireearth.eu/setup/?product=&lt;AppId&gt;</c> and accepts the trimmed answer of an HTTP 200 only if
    /// <see cref="UpdateUrlPolicy"/> allows it; for every other outcome (no AppId, a status other than 200, a timeout, a TLS
    /// or network error, a refused URL) it gives the fixed page <c>https://empireearth.eu/download</c>, and logs why.
    /// </summary>
    /// <remarks>
    /// The request is the setup's (<c>UpdateApiURL</c> of <c>utils.iss</c>, <c>QueryUpdateApi</c> of <c>setup_is6.iss</c>): the
    /// AppId as read, without braces, case kept, escaped with <see cref="Uri.EscapeDataString"/>. Nothing else is sent
    /// (contract 4.3: no telemetry). The launcher never downloads or starts the setup (contract 4.1).
    /// </remarks>
    public sealed class SetupDownloadLocator
    {
        /// <summary>The endpoint of the update API (<c>UpdateApiURL</c> of the setup without the query).</summary>
        public const string UpdateApiUrl = "https://api.empireearth.eu/setup/";

        /// <summary>The fixed download page of the community setup (contract 4.3 step 3, <c>SetupURL</c> of the setup).</summary>
        public const string FixedPageUrl = "https://empireearth.eu/download";

        private readonly IHttpsClient client;
        private readonly ILogger logger;

        public SetupDownloadLocator(IHttpsClient client, ILogger logger)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// The request of contract 4.3 and 4.5: <c>https://api.empireearth.eu/setup/?product=&lt;AppId&gt;</c>, with
        /// <c>&amp;type=&lt;type&gt;</c> and <c>&amp;version=&lt;version&gt;</c> if given; every value escaped with
        /// <see cref="Uri.EscapeDataString"/> (a GUID and a version stay as they are).
        /// </summary>
        public static string QueryUrl(string appId, string type = null, string version = null)
        {
            if (string.IsNullOrEmpty(appId))
                throw new ArgumentException("An AppId is required.", nameof(appId));
            if (version != null && type == null)
                throw new ArgumentException("A version is only sent with its type.", nameof(version));
            var url = new StringBuilder(UpdateApiUrl).Append("?product=").Append(Uri.EscapeDataString(appId));
            if (type != null)
                url.Append("&type=").Append(Uri.EscapeDataString(type));
            if (version != null)
                url.Append("&version=").Append(Uri.EscapeDataString(version));
            return url.ToString();
        }

        /// <summary>Asks the update API for the download of the setup of <paramref name="appId"/>; never throws for network problems.</summary>
        /// <param name="appId">The AppId of the installation as read (record, <c>install.ini</c> or uninstall key name); null if none.</param>
        /// <param name="cancellationToken">Ends the request with <see cref="OperationCanceledException"/>.</param>
        public async Task<SetupDownloadLocation> LocateAsync(string appId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(appId))
                return Fallback(FallbackReason.NoAppId, "the installation has no AppId");

            string query = QueryUrl(appId);
            HttpsResponse response = await client.GetAsync(new Uri(query), cancellationToken).ConfigureAwait(false);
            logger.Info("Update API: GET " + query + ": " + response + ".");
            FallbackReason? failure = FailureOf(response);
            if (failure != null)
                return Fallback(failure.Value, response.ToString());

            string url = response.Body.Trim();
            if (!UpdateUrlPolicy.IsAllowed(url))
                return Fallback(FallbackReason.UrlRejected, "not an https URL of the project: \"" + Shorten(url) + "\"");
            logger.Info("Repair: the update API names the setup download " + url + ".");
            return SetupDownloadLocation.FromUpdateApi(url);
        }

        /// <summary>
        /// Why <paramref name="response"/> is no usable answer: timeout, TLS or network error, or a status other than 200;
        /// null for an answer with HTTP 200.
        /// </summary>
        internal static FallbackReason? FailureOf(HttpsResponse response)
        {
            if (response == null)
                throw new ArgumentNullException(nameof(response));
            switch (response.Outcome)
            {
                case HttpsOutcome.Timeout:
                    return FallbackReason.Timeout;
                case HttpsOutcome.TlsError:
                    return FallbackReason.TlsError;
                case HttpsOutcome.NetworkError:
                    return FallbackReason.NetworkError;
                default:
                    return response.StatusCode == 200 ? (FallbackReason?)null : FallbackReason.StatusNotOk;
            }
        }

        /// <summary>At most 200 characters of an answer, for the log.</summary>
        internal static string Shorten(string text)
        {
            return text.Length > 200 ? text.Substring(0, 200) + "..." : text;
        }

        private SetupDownloadLocation Fallback(FallbackReason reason, string detail)
        {
            string line = "Repair: the fixed download page " + FixedPageUrl + " is used (" + reason + ": " + detail + ").";
            if (reason == FallbackReason.NoAppId)
                logger.Info(line);
            else
                logger.Warning(line);
            return SetupDownloadLocation.FixedPage(reason, detail);
        }
    }
}
