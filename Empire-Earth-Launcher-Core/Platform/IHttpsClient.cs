using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>How an HTTPS request ended (ADR 0008; ADR 0013: environment problems are results).</summary>
    public enum HttpsOutcome
    {
        /// <summary>The server answered: <see cref="HttpsResponse.StatusCode"/> and <see cref="HttpsResponse.Body"/> are set (any status).</summary>
        Answered,

        /// <summary>No answer within the timeout of the client (10 s).</summary>
        Timeout,

        /// <summary>
        /// The TLS handshake failed: an <see cref="System.Security.Authentication.AuthenticationException"/> inside the
        /// error, e.g. no common cipher suite on Windows 7 or a certificate for another name (R16).
        /// </summary>
        TlsError,

        /// <summary>Any other error: DNS, connection refused or reset, proxy, an answer larger than 4 KiB.</summary>
        NetworkError
    }

    /// <summary>The result of <see cref="IHttpsClient.GetAsync"/>.</summary>
    public sealed class HttpsResponse
    {
        private HttpsResponse(HttpsOutcome outcome, int statusCode, string body, string errorType, string errorMessage,
            TimeSpan duration)
        {
            Outcome = outcome;
            StatusCode = statusCode;
            Body = body;
            ErrorType = errorType;
            ErrorMessage = errorMessage;
            Duration = duration;
        }

        public HttpsOutcome Outcome { get; }

        /// <summary>The HTTP status of an answer; 0 otherwise.</summary>
        public int StatusCode { get; }

        /// <summary>The body of an answer (at most 4 KiB); null otherwise.</summary>
        public string Body { get; }

        /// <summary>
        /// The types of the error and of its inner errors, outermost first (<c>HttpRequestException/WebException/
        /// AuthenticationException</c>), for the log; null for an answer.
        /// </summary>
        public string ErrorType { get; }

        /// <summary>The message of the innermost error, for the log; null for an answer.</summary>
        public string ErrorMessage { get; }

        /// <summary>How long the request took.</summary>
        public TimeSpan Duration { get; }

        /// <summary>True for an answer with HTTP 200.</summary>
        public bool IsOk
        {
            get { return Outcome == HttpsOutcome.Answered && StatusCode == 200; }
        }

        /// <summary>An answer of the server.</summary>
        public static HttpsResponse Answered(int statusCode, string body, TimeSpan duration)
        {
            return new HttpsResponse(HttpsOutcome.Answered, statusCode, body ?? string.Empty, null, null, duration);
        }

        /// <summary>A request without an answer.</summary>
        public static HttpsResponse Failed(HttpsOutcome outcome, string errorType, string errorMessage, TimeSpan duration)
        {
            if (outcome == HttpsOutcome.Answered)
                throw new ArgumentException("A failed request needs an outcome other than Answered.", nameof(outcome));
            return new HttpsResponse(outcome, 0, null, errorType, errorMessage, duration);
        }

        /// <summary>One line for the log: the status or the error, and the duration (never the body).</summary>
        public override string ToString()
        {
            string milliseconds = ((long)Duration.TotalMilliseconds).ToString(CultureInfo.InvariantCulture) + " ms";
            return Outcome == HttpsOutcome.Answered
                ? "HTTP " + StatusCode.ToString(CultureInfo.InvariantCulture) + " in " + milliseconds
                : Outcome + " (" + ErrorType + ": " + ErrorMessage + ") after " + milliseconds;
        }
    }

    /// <summary>
    /// GET requests over HTTPS (ADR 0008): certificate validation always on, no redirects, no cookies, a timeout of
    /// 10 seconds and answers of at most 4 KiB. The launcher sends only the requests of contract 4.3 and 4.5 to the update
    /// API. The tests use a fake; no test opens a connection.
    /// </summary>
    public interface IHttpsClient
    {
        /// <summary>
        /// Sends <c>GET <paramref name="url"/></c>. Network, TLS and timeout problems are returned as
        /// <see cref="HttpsResponse"/>, never thrown; a redirect is an answer with its 3xx status, not followed.
        /// </summary>
        /// <param name="url">An absolute <c>https://</c> URL; anything else is a programming error and throws.</param>
        /// <param name="cancellationToken">Ends the request with <see cref="OperationCanceledException"/>.</param>
        Task<HttpsResponse> GetAsync(Uri url, CancellationToken cancellationToken);
    }
}
