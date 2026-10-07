using System;
using System.Text;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Repair
{
    /// <summary>Why the update API gave no usable answer (contract 4.5).</summary>
    public enum UpdateApiFailure
    {
        /// <summary>There is no failure: the answer is an HTTP 200.</summary>
        None,

        /// <summary>No answer within 10 seconds.</summary>
        Timeout,

        /// <summary>The TLS handshake failed (Windows 7 without a common cipher suite, a wrong certificate, R16).</summary>
        TlsError,

        /// <summary>No connection: DNS, offline, proxy, an answer larger than 4 KiB.</summary>
        NetworkError,

        /// <summary>An answer with a status other than 200 (also a redirect, which is never followed).</summary>
        StatusNotOk
    }

    /// <summary>
    /// The update API of the community website (contract 4.5, ADR 0008): the requests the launcher may send and how an answer
    /// is judged. Since revision 6 these are the only requests: the version questions with <c>&amp;type=</c>; the download
    /// page of the setup needs no request (<see cref="SetupDownloadPage"/>, contract 4.3).
    /// </summary>
    public static class UpdateApi
    {
        /// <summary>The endpoint of the update API (<c>UpdateApiURL</c> of the setup without the query).</summary>
        public const string Url = "https://api.empireearth.eu/setup/";

        /// <summary>
        /// The request of contract 4.5: <c>https://api.empireearth.eu/setup/?product=&lt;AppId&gt;</c> with
        /// <c>&amp;type=&lt;type&gt;</c> and <c>&amp;version=&lt;version&gt;</c> if given; every value escaped with
        /// <see cref="Uri.EscapeDataString"/> (a GUID and a version stay as they are). The AppId as read, without braces, case
        /// kept.
        /// </summary>
        public static string QueryUrl(string appId, string type = null, string version = null)
        {
            if (string.IsNullOrEmpty(appId))
                throw new ArgumentException("An AppId is required.", nameof(appId));
            if (version != null && type == null)
                throw new ArgumentException("A version is only sent with its type.", nameof(version));
            var url = new StringBuilder(Url).Append("?product=").Append(Uri.EscapeDataString(appId));
            if (type != null)
                url.Append("&type=").Append(Uri.EscapeDataString(type));
            if (version != null)
                url.Append("&version=").Append(Uri.EscapeDataString(version));
            return url.ToString();
        }

        /// <summary>
        /// Why <paramref name="response"/> is no usable answer: timeout, TLS or network error, or a status other than 200;
        /// null for an answer with HTTP 200.
        /// </summary>
        internal static UpdateApiFailure? FailureOf(HttpsResponse response)
        {
            if (response == null)
                throw new ArgumentNullException(nameof(response));
            switch (response.Outcome)
            {
                case HttpsOutcome.Timeout:
                    return UpdateApiFailure.Timeout;
                case HttpsOutcome.TlsError:
                    return UpdateApiFailure.TlsError;
                case HttpsOutcome.NetworkError:
                    return UpdateApiFailure.NetworkError;
                default:
                    return response.StatusCode == 200 ? (UpdateApiFailure?)null : UpdateApiFailure.StatusNotOk;
            }
        }
    }
}
