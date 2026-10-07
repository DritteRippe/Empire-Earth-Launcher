using System;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Repair;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Repair
{
    /// <summary>
    /// <see cref="UpdateApi"/> (contract 4.5, ADR 0008): the form of the requests, which is the setup's (<c>UpdateApiURL</c> of
    /// <c>utils.iss</c>, <c>QueryUpdateApi</c> of <c>setup_is6.iss</c>), and which answers are no answer. No network.
    /// </summary>
    [TestFixture]
    public class UpdateApiTests
    {
        private const string AppId = "00000000-0000-0000-0000-000000000AEE";

        [Test]
        public void Url_IsTheEndpointOfTheUpdateApi()
        {
            Assert.That(UpdateApi.Url, Is.EqualTo("https://api.empireearth.eu/setup/"));
        }

        [Test]
        public void Query_IsTheFormOfTheSetup()
        {
            // utils.iss: UpdateApiURL = 'https://api.empireearth.eu/setup/?product={#AppID}', the AppID without braces;
            // setup_is6.iss: QueryUpdateApi('&type=' + TypeName + '&version=' + Version).
            Assert.That(UpdateApi.QueryUrl(AppId), Is.EqualTo("https://api.empireearth.eu/setup/?product=" + AppId));
            Assert.That(UpdateApi.QueryUrl(AppId, "game", "2.0.0.5"),
                Is.EqualTo("https://api.empireearth.eu/setup/?product=" + AppId + "&type=game&version=2.0.0.5"));
            Assert.That(UpdateApi.QueryUrl(AppId, "setup", "2.0.0"),
                Is.EqualTo("https://api.empireearth.eu/setup/?product=" + AppId + "&type=setup&version=2.0.0"));
            Assert.That(UpdateApi.QueryUrl(AppId, "game"),
                Is.EqualTo("https://api.empireearth.eu/setup/?product=" + AppId + "&type=game"));
        }

        [Test]
        public void Query_KeepsTheAppIdAsRead_CaseAndAll()
        {
            Assert.That(UpdateApi.QueryUrl("abcdef01-2345-6789-ABCD-ef0123456789"),
                Does.EndWith("?product=abcdef01-2345-6789-ABCD-ef0123456789"), "no braces added, case kept");
            Assert.That(new Uri(UpdateApi.QueryUrl(AppId, "game", "2.0.0.5")).AbsoluteUri,
                Is.EqualTo(UpdateApi.QueryUrl(AppId, "game", "2.0.0.5")), "the URL is sent as built");
        }

        [Test]
        public void Query_EscapesWhatIsNotAGuid()
        {
            Assert.That(UpdateApi.QueryUrl("{x y}&a=b", "game", "1 2"),
                Is.EqualTo("https://api.empireearth.eu/setup/?product=%7Bx%20y%7D%26a%3Db&type=game&version=1%202"));
            Assert.Throws<ArgumentException>(() => UpdateApi.QueryUrl(""));
            Assert.Throws<ArgumentException>(() => UpdateApi.QueryUrl(null));
            Assert.Throws<ArgumentException>(() => UpdateApi.QueryUrl(AppId, null, "1"));
        }

        [Test]
        public void FailureOf_AnAnswerWithHttp200_IsNoFailure()
        {
            Assert.That(UpdateApi.FailureOf(HttpsResponse.Answered(200, "true", TimeSpan.Zero)), Is.Null);
        }

        [TestCase(404)]
        [TestCase(500)]
        [TestCase(301, Description = "a redirect is never followed")]
        [TestCase(204)]
        public void FailureOf_AStatusOtherThan200_IsStatusNotOk(int status)
        {
            Assert.That(UpdateApi.FailureOf(HttpsResponse.Answered(status, "", TimeSpan.Zero)), Is.EqualTo(UpdateApiFailure.StatusNotOk));
        }

        [TestCase(HttpsOutcome.Timeout, UpdateApiFailure.Timeout)]
        [TestCase(HttpsOutcome.TlsError, UpdateApiFailure.TlsError)]
        [TestCase(HttpsOutcome.NetworkError, UpdateApiFailure.NetworkError)]
        public void FailureOf_NoAnswer_NamesTheReason(HttpsOutcome outcome, UpdateApiFailure expected)
        {
            Assert.That(UpdateApi.FailureOf(HttpsResponse.Failed(outcome, "SomeException", "injected", TimeSpan.Zero)), Is.EqualTo(expected));
        }

        [Test]
        public void FailureOf_NullIsRefused()
        {
            Assert.Throws<ArgumentNullException>(() => UpdateApi.FailureOf(null));
        }
    }
}
