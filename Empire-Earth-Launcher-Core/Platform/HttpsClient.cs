using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>
    /// <see cref="IHttpsClient"/> over <see cref="HttpClient"/> (ADR 0008): one client for the lifetime of the launcher,
    /// created by <c>Program</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The certificate of the server is validated by Windows as for every other program: this class (and the whole
    /// launcher, an architecture test checks it) never sets a validation callback. The TLS versions are those of
    /// <see cref="ServicePointManager.SecurityProtocol"/>, which <c>Program</c> sets once (ADR 0008 plan review).
    /// </para>
    /// <para>
    /// The handler follows no redirect (<see cref="HttpClientHandler.AllowAutoRedirect"/> false: the update API answers
    /// with the URL in the body, a redirect is an answer with its status) and keeps no cookies; the client waits at most
    /// <see cref="RequestTimeout"/> and buffers at most <see cref="MaxResponseBytes"/> bytes
    /// (<see cref="HttpClient.MaxResponseContentBufferSize"/>), so a larger answer is an error. No header is added: the
    /// request carries nothing but the URL (no telemetry, contract 4.3).
    /// </para>
    /// </remarks>
    public sealed class HttpsClient : IHttpsClient, IDisposable
    {
        /// <summary>The longest a request may take, connection and answer included.</summary>
        public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

        /// <summary>The largest answer accepted (4 KiB): the update API answers with one URL or one version.</summary>
        public const int MaxResponseBytes = 4096;

        private readonly HttpClient client;

        public HttpsClient()
        {
            client = CreateClient(CreateHandler());
        }

        /// <summary>The handler of the client: no redirects, no cookies; certificate validation by Windows.</summary>
        internal static HttpClientHandler CreateHandler()
        {
            return new HttpClientHandler
            {
                AllowAutoRedirect = false,
                UseCookies = false
            };
        }

        /// <summary>The client on <paramref name="handler"/>: timeout and size limit of the answer.</summary>
        internal static HttpClient CreateClient(HttpMessageHandler handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            return new HttpClient(handler, true)
            {
                Timeout = RequestTimeout,
                MaxResponseContentBufferSize = MaxResponseBytes
            };
        }

        /// <summary>Throws unless <paramref name="url"/> is an absolute <c>https://</c> URL (a programming error otherwise).</summary>
        internal static void RequireHttps(Uri url)
        {
            if (url == null)
                throw new ArgumentNullException(nameof(url));
            if (!url.IsAbsoluteUri || url.Scheme != Uri.UriSchemeHttps)
                throw new ArgumentException("Only absolute https URLs are requested: " + url, nameof(url));
        }

        public async Task<HttpsResponse> GetAsync(Uri url, CancellationToken cancellationToken)
        {
            RequireHttps(url);
            Stopwatch watch = Stopwatch.StartNew();
            try
            {
                // ResponseContentRead: the body is read through the buffer of the client, which enforces the 4 KiB limit.
                using (HttpResponseMessage response = await client
                           .GetAsync(url, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false))
                {
                    string body = response.Content == null
                        ? string.Empty
                        : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return HttpsResponse.Answered((int)response.StatusCode, body, watch.Elapsed);
                }
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                // HttpClient ends a request that exceeds its Timeout with a TaskCanceledException.
                return HttpsResponse.Failed(HttpsOutcome.Timeout, DescribeTypes(ex), InnermostMessage(ex), watch.Elapsed);
            }
            catch (HttpRequestException ex)
            {
                return HttpsResponse.Failed(Classify(ex), DescribeTypes(ex), InnermostMessage(ex), watch.Elapsed);
            }
        }

        /// <summary>
        /// <see cref="HttpsOutcome.TlsError"/> if the error contains an <see cref="AuthenticationException"/> or a
        /// <see cref="WebException"/> with the status of a failed handshake or certificate, else
        /// <see cref="HttpsOutcome.NetworkError"/>.
        /// </summary>
        internal static HttpsOutcome Classify(Exception exception)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));
            foreach (Exception current in Chain(exception))
            {
                if (current is AuthenticationException)
                    return HttpsOutcome.TlsError;
                if (current is WebException web &&
                    (web.Status == WebExceptionStatus.TrustFailure || web.Status == WebExceptionStatus.SecureChannelFailure))
                    return HttpsOutcome.TlsError;
            }
            return HttpsOutcome.NetworkError;
        }

        /// <summary>The type names of the error and its inner errors, outermost first, separated by <c>/</c>.</summary>
        internal static string DescribeTypes(Exception exception)
        {
            return string.Join("/", Chain(exception).Select(current => current.GetType().Name));
        }

        private static string InnermostMessage(Exception exception)
        {
            return Chain(exception).Last().Message;
        }

        private static IEnumerable<Exception> Chain(Exception exception)
        {
            for (Exception current = exception; current != null; current = current.InnerException)
                yield return current;
        }

        public void Dispose()
        {
            client.Dispose();
        }
    }
}
