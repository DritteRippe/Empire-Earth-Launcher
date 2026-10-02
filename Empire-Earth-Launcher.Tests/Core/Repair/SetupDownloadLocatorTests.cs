using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Repair;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Repair
{
    /// <summary>
    /// <see cref="SetupDownloadLocator"/> (contract 4.3, ADR 0008): the URL of the update API when it answers with an allowed
    /// URL, the fixed page with the reason logged in every other case, and the query of the setup. No network: the answers
    /// come from <see cref="FakeHttpsClient"/>.
    /// </summary>
    [TestFixture]
    public class SetupDownloadLocatorTests
    {
        private const string AppId = "00000000-0000-0000-0000-000000000AEE";
        private const string Query = "https://api.empireearth.eu/setup/?product=" + AppId;

        private FakeHttpsClient client;
        private RecordingLogger logger;
        private SetupDownloadLocator locator;

        [SetUp]
        public void SetUp()
        {
            client = new FakeHttpsClient();
            logger = new RecordingLogger();
            locator = new SetupDownloadLocator(client, logger);
        }

        [Test]
        public async Task Section4_3_TheUrlOfTheApi_WhenItIsAllowed()
        {
            client.Answer(Query, 200, "  https://files.empireearth.eu/NeoEE-Setup-2.0.1.exe\r\n");

            SetupDownloadLocation location = await locator.LocateAsync(AppId);

            Assert.That(location.IsFromUpdateApi, Is.True);
            Assert.That(location.Url, Is.EqualTo("https://files.empireearth.eu/NeoEE-Setup-2.0.1.exe"), "the answer is trimmed");
            Assert.That(location.Reason, Is.EqualTo(FallbackReason.None));
            Assert.That(client.Requests, Is.EqualTo(new[] { Query }), "one request, nothing but the query");
            Assert.That(logger.Messages, Has.Some.Contains("GET " + Query + ": HTTP 200 in"));
        }

        [Test]
        public async Task Section4_3_NoAppId_FixedPage_WithoutARequest()
        {
            foreach (string appId in new[] { null, "", "   " })
            {
                SetupDownloadLocation location = await locator.LocateAsync(appId);

                Assert.That(location.Url, Is.EqualTo("https://empireearth.eu/download"));
                Assert.That(location.Reason, Is.EqualTo(FallbackReason.NoAppId));
            }
            Assert.That(client.Requests, Is.Empty);
            Assert.That(logger.MessagesOf(LogLevel.Info), Has.Some.Contains("NoAppId"));
        }

        [TestCase(404)]
        [TestCase(500)]
        [TestCase(301, Description = "a redirect is never followed")]
        [TestCase(204)]
        public async Task Section4_3_AStatusOtherThan200_FixedPage(int status)
        {
            client.Answer(Query, status, "https://empireearth.eu/download/new");

            SetupDownloadLocation location = await locator.LocateAsync(AppId);

            Assert.That(location.Url, Is.EqualTo(SetupDownloadLocator.FixedPageUrl));
            Assert.That(location.Reason, Is.EqualTo(FallbackReason.StatusNotOk));
            Assert.That(logger.MessagesOf(LogLevel.Warning).Single(), Does.Contain("StatusNotOk").And.Contain("HTTP " + status));
        }

        [TestCase(HttpsOutcome.Timeout, "TaskCanceledException", FallbackReason.Timeout)]
        [TestCase(HttpsOutcome.TlsError, "HttpRequestException/WebException/AuthenticationException", FallbackReason.TlsError)]
        [TestCase(HttpsOutcome.NetworkError, "HttpRequestException/WebException", FallbackReason.NetworkError)]
        public async Task Section4_3_NoAnswer_FixedPage_AndTheReasonIsLogged(HttpsOutcome outcome, string errorType, FallbackReason reason)
        {
            client.Fail(Query, outcome, errorType, "injected");

            SetupDownloadLocation location = await locator.LocateAsync(AppId);

            Assert.That(location.Url, Is.EqualTo(SetupDownloadLocator.FixedPageUrl));
            Assert.That(location.Reason, Is.EqualTo(reason));
            Assert.That(logger.MessagesOf(LogLevel.Warning).Single(), Does.Contain(reason.ToString()).And.Contain(errorType),
                "the log names the inner exception type (ADR 0008 design review)");
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("http://empireearth.eu/download")]
        [TestCase("https://evil.example/setup.exe")]
        [TestCase("https://github.com/someone/Empire-Earth-Setup/releases")]
        [TestCase("<html>maintenance</html>")]
        public async Task Section4_3_ARefusedUrl_FixedPage(string answer)
        {
            client.Answer(Query, 200, answer);

            SetupDownloadLocation location = await locator.LocateAsync(AppId);

            Assert.That(location.Url, Is.EqualTo(SetupDownloadLocator.FixedPageUrl));
            Assert.That(location.Reason, Is.EqualTo(FallbackReason.UrlRejected));
            Assert.That(logger.MessagesOf(LogLevel.Warning).Single(), Does.Contain("UrlRejected"));
        }

        [Test]
        public void ACancelledRequest_IsCancelled()
        {
            client.Hold();
            var cancel = new CancellationTokenSource();
            Task<SetupDownloadLocation> located = locator.LocateAsync(AppId, cancel.Token);

            cancel.Cancel();

            Assert.That(() => located.GetAwaiter().GetResult(), Throws.InstanceOf<OperationCanceledException>());
        }

        [Test]
        public void ALongRefusedAnswer_IsShortenedInTheLog()
        {
            Assert.That(SetupDownloadLocator.Shorten(new string('x', 4000)).Length, Is.EqualTo(203));
        }

        // --- The query (ADR 0008 plan review, REV-12) -------------------------------------------------------------------

        [Test]
        public void Query_IsTheFormOfTheSetup()
        {
            // utils.iss: UpdateApiURL = 'https://api.empireearth.eu/setup/?product={#AppID}', the AppID without braces;
            // setup_is6.iss: QueryUpdateApi('&type=' + TypeName + '&version=' + Version).
            Assert.That(SetupDownloadLocator.QueryUrl(AppId), Is.EqualTo("https://api.empireearth.eu/setup/?product=" + AppId));
            Assert.That(SetupDownloadLocator.QueryUrl(AppId, "game", "2.0.0.5"),
                Is.EqualTo("https://api.empireearth.eu/setup/?product=" + AppId + "&type=game&version=2.0.0.5"));
            Assert.That(SetupDownloadLocator.QueryUrl(AppId, "setup", "2.0.0"),
                Is.EqualTo("https://api.empireearth.eu/setup/?product=" + AppId + "&type=setup&version=2.0.0"));
            Assert.That(SetupDownloadLocator.QueryUrl(AppId, "game"),
                Is.EqualTo("https://api.empireearth.eu/setup/?product=" + AppId + "&type=game"));
        }

        [Test]
        public void Query_KeepsTheAppIdAsRead_CaseAndAll()
        {
            Assert.That(SetupDownloadLocator.QueryUrl("abcdef01-2345-6789-ABCD-ef0123456789"),
                Does.EndWith("?product=abcdef01-2345-6789-ABCD-ef0123456789"), "no braces added, case kept");
            Assert.That(new Uri(SetupDownloadLocator.QueryUrl(AppId, "game", "2.0.0.5")).AbsoluteUri,
                Is.EqualTo(SetupDownloadLocator.QueryUrl(AppId, "game", "2.0.0.5")), "the URL is sent as built");
        }

        [Test]
        public void Query_EscapesWhatIsNotAGuid()
        {
            Assert.That(SetupDownloadLocator.QueryUrl("{x y}&a=b", "game", "1 2"),
                Is.EqualTo("https://api.empireearth.eu/setup/?product=%7Bx%20y%7D%26a%3Db&type=game&version=1%202"));
            Assert.Throws<ArgumentException>(() => SetupDownloadLocator.QueryUrl(""));
            Assert.Throws<ArgumentException>(() => SetupDownloadLocator.QueryUrl(AppId, null, "1"));
        }

        [Test]
        public async Task TheLocator_UsesTheQuery()
        {
            client.Answer("https://api.empireearth.eu/setup/?product=abcdef01-2345-6789-ABCD-ef0123456789", 200,
                "https://empireearth.eu/download/ee");

            SetupDownloadLocation location = await locator.LocateAsync("abcdef01-2345-6789-ABCD-ef0123456789");

            Assert.That(client.Requests.Single(), Is.EqualTo("https://api.empireearth.eu/setup/?product=abcdef01-2345-6789-ABCD-ef0123456789"));
            Assert.That(location.Url, Is.EqualTo("https://empireearth.eu/download/ee"));
        }
    }
}
