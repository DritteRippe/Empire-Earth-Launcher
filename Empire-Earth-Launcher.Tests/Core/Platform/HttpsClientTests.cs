using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Platform;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Platform
{
    /// <summary>
    /// The settings of the real HTTPS client (ADR 0008 and its design review amendment), how it reads an answer and how it
    /// classifies errors. No request leaves the test: the handler and the client are created and inspected, and the requests
    /// of the client go to a handler that answers from memory (the request over the network is a test-plan case).
    /// </summary>
    [TestFixture]
    public class HttpsClientTests
    {
        private static readonly Uri UpdateApi = new Uri("https://api.empireearth.eu/setup/?product=x&type=game&version=2.0");

        /// <summary>A handler that answers every request from memory with HTTP 200, the bytes and the content type it is given.</summary>
        private sealed class AnsweringHandler : HttpMessageHandler
        {
            private readonly byte[] body;
            private readonly string contentType;

            public AnsweringHandler(byte[] body, string contentType)
            {
                this.body = body;
                this.contentType = contentType;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var content = new ByteArrayContent(body);
                if (contentType != null)
                    content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content, RequestMessage = request });
            }
        }

        private static async Task<HttpsResponse> Answer(byte[] body, string contentType)
        {
            using (HttpClient client = HttpsClient.CreateClient(new AnsweringHandler(body, contentType)))
                return await HttpsClient.GetAsync(client, UpdateApi, CancellationToken.None);
        }

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

        [TestCase("text/plain; charset=gibtsnicht")]
        [TestCase("text/plain; charset=\"utf-8\"")]
        [TestCase("text/plain; charset=utf-8")]
        [TestCase("text/plain")]
        [TestCase(null)]
        public async Task GetAsync_WithAnyCharacterSet_GivesTheAnswer(string contentType)
        {
            // ReadAsStringAsync threw an InvalidOperationException for a character set Windows does not know, which ended the
            // update check and the whole network diagnostics with "unexpected error" (review after 1.1.0).
            HttpsResponse response = await Answer(Encoding.ASCII.GetBytes("https://empireearth.eu/download/ee/"), contentType);

            Assert.That(response.Outcome, Is.EqualTo(HttpsOutcome.Answered), response.ToString());
            Assert.That(response.StatusCode, Is.EqualTo(200));
            Assert.That(response.Body, Is.EqualTo("https://empireearth.eu/download/ee/"));
        }

        [Test]
        public async Task GetAsync_AnAnswerOfFourKiB_IsRead_ALargerOneIsANetworkError()
        {
            HttpsResponse largest = await Answer(Enumerable.Repeat((byte)'1', 4096).ToArray(), "text/plain; charset=gibtsnicht");
            HttpsResponse larger = await Answer(Enumerable.Repeat((byte)'1', 4097).ToArray(), "text/plain; charset=gibtsnicht");

            Assert.That(largest.Outcome, Is.EqualTo(HttpsOutcome.Answered), largest.ToString());
            Assert.That(largest.Body, Has.Length.EqualTo(4096));
            Assert.That(larger.Outcome, Is.EqualTo(HttpsOutcome.NetworkError), larger.ToString());
            Assert.That(larger.ErrorType, Does.StartWith("HttpRequestException"));
            Assert.That(larger.Body, Is.Null);
        }

        [Test]
        public void DecodeBody_UsesTheCharacterSet_OrTheByteOrderMark_AndDropsTheMark()
        {
            Assert.That(HttpsClient.DecodeBody(new byte[] { 0x4D, 0xFC, 0x6E }, "iso-8859-1"), Is.EqualTo("M\u00fcn"));
            Assert.That(HttpsClient.DecodeBody(new byte[] { 0x4D, 0xFC, 0x6E }, "\"iso-8859-1\""), Is.EqualTo("M\u00fcn"),
                "a quoted name, as the .NET Framework passes it on");
            Assert.That(HttpsClient.DecodeBody(new byte[] { 0xEF, 0xBB, 0xBF, 0x31, 0x2E, 0x37 }, "utf-8"), Is.EqualTo("1.7"));
            Assert.That(HttpsClient.DecodeBody(new byte[] { 0xEF, 0xBB, 0xBF, 0x31, 0x2E, 0x37 }, null), Is.EqualTo("1.7"));
            Assert.That(HttpsClient.DecodeBody(new byte[] { 0xFF, 0xFE, 0x31, 0x00 }, null), Is.EqualTo("1"), "UTF-16 by its mark");
            Assert.That(HttpsClient.DecodeBody(new byte[0], "utf-8"), Is.Empty);
        }

        [TestCase("gibtsnicht")]
        [TestCase("")]
        [TestCase(null)]
        public void DecodeBody_WithoutAKnownCharacterSet_ReadsUtf8(string charSet)
        {
            // M, u with umlaut (C3 BC), n: UTF-8.
            Assert.That(HttpsClient.DecodeBody(new byte[] { 0x4D, 0xC3, 0xBC, 0x6E }, charSet), Is.EqualTo("M\u00fcn"));
        }

        [Test]
        public void DecodeBody_InvalidBytes_AreReplaced_NothingIsThrown()
        {
            Assert.That(HttpsClient.DecodeBody(new byte[] { 0x31, 0xC3 }, "utf-8"), Is.EqualTo("1\ufffd"));
            Assert.That(HttpsClient.DecodeBody(new byte[] { 0x31, 0xC3 }, "gibtsnicht"), Is.EqualTo("1\ufffd"));
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
