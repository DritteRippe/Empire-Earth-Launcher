using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Tests.Fakes
{
    /// <summary>
    /// <see cref="IHttpsClient"/> without a network: it records every URL (as sent, <see cref="Uri.AbsoluteUri"/>) and
    /// answers with the response configured for that URL, else with <see cref="DefaultResponse"/>. A request can be held
    /// until the test releases it (<see cref="Hold"/>) or cancels it.
    /// </summary>
    internal sealed class FakeHttpsClient : IHttpsClient
    {
        private readonly object sync = new object();
        private readonly List<string> requests = new List<string>();
        private readonly Dictionary<string, HttpsResponse> responses = new Dictionary<string, HttpsResponse>(StringComparer.Ordinal);
        private TaskCompletionSource<bool> held;

        /// <summary>The answer to a URL without a configured response: a network error ("no server" in the tests).</summary>
        public HttpsResponse DefaultResponse { get; set; } =
            HttpsResponse.Failed(HttpsOutcome.NetworkError, "HttpRequestException/WebException", "No answer configured.", TimeSpan.Zero);

        /// <summary>Every URL requested, in order.</summary>
        public IReadOnlyList<string> Requests
        {
            get
            {
                lock (sync)
                    return requests.ToList();
            }
        }

        /// <summary>The server answers <paramref name="url"/> with <paramref name="statusCode"/> and <paramref name="body"/>.</summary>
        public FakeHttpsClient Answer(string url, int statusCode, string body)
        {
            responses[new Uri(url).AbsoluteUri] = HttpsResponse.Answered(statusCode, body, TimeSpan.FromMilliseconds(50));
            return this;
        }

        /// <summary>The request of <paramref name="url"/> fails with <paramref name="outcome"/>.</summary>
        public FakeHttpsClient Fail(string url, HttpsOutcome outcome, string errorType = "HttpRequestException",
            string message = "Injected failure.")
        {
            responses[new Uri(url).AbsoluteUri] = HttpsResponse.Failed(outcome, errorType, message, TimeSpan.FromMilliseconds(50));
            return this;
        }

        /// <summary>From now on every request waits until <see cref="Release"/> or its cancellation.</summary>
        public void Hold()
        {
            held = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        /// <summary>Lets the held requests go on.</summary>
        public void Release()
        {
            held?.TrySetResult(true);
        }

        public async Task<HttpsResponse> GetAsync(Uri url, CancellationToken cancellationToken)
        {
            // The same check as the real client: only absolute https URLs.
            HttpsClient.RequireHttps(url);
            lock (sync)
                requests.Add(url.AbsoluteUri);
            TaskCompletionSource<bool> gate = held;
            if (gate != null)
            {
                var cancelled = new TaskCompletionSource<bool>();
                using (cancellationToken.Register(() => cancelled.TrySetResult(true)))
                    await Task.WhenAny(gate.Task, cancelled.Task).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return responses.TryGetValue(url.AbsoluteUri, out HttpsResponse response) ? response : DefaultResponse;
        }
    }
}
