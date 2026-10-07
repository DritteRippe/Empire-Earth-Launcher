using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;
using Empire_Earth_Launcher.Core.Platform;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Platform
{
    /// <summary>
    /// The settings of the real HTTPS client (ADR 0008 and its design review amendment) and how it classifies errors. No
    /// request is sent: the handler and the client are only created and inspected (the request itself is a test-plan case).
    /// </summary>
    [TestFixture]
    public class HttpsClientTests
    {
        [Test]
        public void Handler_FollowsNoRedirect_AndKeepsNoCookies()
        {
            using (HttpClientHandler handler = HttpsClient.CreateHandler())
            {
                Assert.That(handler.AllowAutoRedirect, Is.False, "a redirect is an answer, never followed");
                Assert.That(handler.UseCookies, Is.False);
                Assert.That(handler.UseDefaultCredentials, Is.False, "no credentials are sent");
            }
        }

        [Test]
        public void Client_WaitsTenSeconds_AndBuffersAtMostFourKiB()
        {
            using (HttpClient client = HttpsClient.CreateClient(HttpsClient.CreateHandler()))
            {
                Assert.That(client.Timeout, Is.EqualTo(TimeSpan.FromSeconds(10)));
                Assert.That(client.MaxResponseContentBufferSize, Is.EqualTo(4096));
                Assert.That(client.DefaultRequestHeaders, Is.Empty, "nothing but the URL is sent (contract 4.3)");
            }
        }

        [TestCase("http://api.empireearth.eu/setup/")]
        [TestCase("ftp://empireearth.eu/")]
        public void OnlyHttpsUrls_AreRequested(string url)
        {
            Assert.Throws<ArgumentException>(() => HttpsClient.RequireHttps(new Uri(url)));
        }

        [Test]
        public void RelativeUrls_AreRefused()
        {
            Assert.Throws<ArgumentException>(() => HttpsClient.RequireHttps(new Uri("/setup/", UriKind.Relative)));
            Assert.Throws<ArgumentNullException>(() => HttpsClient.RequireHttps(null));
            Assert.DoesNotThrow(() => HttpsClient.RequireHttps(new Uri("https://api.empireearth.eu/setup/?product=x")));
        }

        [Test]
        public void Classify_AnInnerAuthenticationException_IsATlsError()
        {
            var tls = new HttpRequestException("An error occurred while sending the request.",
                new WebException("The request was aborted: Could not create SSL/TLS secure channel.",
                    new AuthenticationException("A call to SSPI failed, see inner exception."),
                    WebExceptionStatus.SecureChannelFailure, null));

            Assert.That(HttpsClient.Classify(tls), Is.EqualTo(HttpsOutcome.TlsError));
            Assert.That(HttpsClient.DescribeTypes(tls),
                Is.EqualTo("HttpRequestException/WebException/AuthenticationException"), "the log names the inner type (ADR 0008)");
        }

        [TestCase(WebExceptionStatus.TrustFailure)]
        [TestCase(WebExceptionStatus.SecureChannelFailure)]
        public void Classify_AFailedHandshakeOrCertificate_IsATlsError(WebExceptionStatus status)
        {
            var error = new HttpRequestException("x", new WebException("y", null, status, null));

            Assert.That(HttpsClient.Classify(error), Is.EqualTo(HttpsOutcome.TlsError));
        }

        [Test]
        public void Classify_OtherErrors_AreNetworkErrors()
        {
            Assert.That(HttpsClient.Classify(new HttpRequestException("x",
                new WebException("The remote name could not be resolved", WebExceptionStatus.NameResolutionFailure))),
                Is.EqualTo(HttpsOutcome.NetworkError));
            Assert.That(HttpsClient.Classify(new HttpRequestException("x", new IOException("reset", new SocketException(10054)))),
                Is.EqualTo(HttpsOutcome.NetworkError));
            Assert.That(HttpsClient.Classify(new HttpRequestException(
                "Cannot write more bytes to the buffer than the configured maximum buffer size: 4096.")),
                Is.EqualTo(HttpsOutcome.NetworkError), "an answer larger than 4 KiB");
        }

        [Test]
        public void Response_LogLine_HasStatusOrError_AndNeverTheBody()
        {
            HttpsResponse answer = HttpsResponse.Answered(200, "https://empireearth.eu/download", TimeSpan.FromMilliseconds(120));
            HttpsResponse failed = HttpsResponse.Failed(HttpsOutcome.Timeout, "TaskCanceledException", "A task was canceled.",
                TimeSpan.FromSeconds(10));

            Assert.That(answer.IsOk, Is.True);
            Assert.That(answer.ToString(), Is.EqualTo("HTTP 200 in 120 ms"));
            Assert.That(failed.IsOk, Is.False);
            Assert.That(failed.Body, Is.Null);
            Assert.That(failed.ToString(), Is.EqualTo("Timeout (TaskCanceledException: A task was canceled.) after 10000 ms"));
            Assert.That(HttpsResponse.Answered(404, "x", TimeSpan.Zero).IsOk, Is.False);
            Assert.Throws<ArgumentException>(() => HttpsResponse.Failed(HttpsOutcome.Answered, "x", "y", TimeSpan.Zero));
        }
    }
}
