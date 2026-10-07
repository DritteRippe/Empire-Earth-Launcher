using System.Linq;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Repair;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using Microsoft.Win32;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Repair
{
    /// <summary>
    /// <see cref="UpdateChecker"/> (contract 4.5, ADR 0008 plan review): the game version check for every installation with
    /// an AppId (community and community-legacy), the setup version check, the answers of the API as the setup reads them,
    /// and the latest version shown only if it looks like a version.
    /// </summary>
    [TestFixture]
    public class UpdateCheckerTests
    {
        private const string Root = @"C:\Program Files (x86)\Neo Empire Earth";
        private static readonly string Api = "https://api.empireearth.eu/setup/?product=" + InstallationWorld.NeoEEAppId;

        private FakeHttpsClient client;
        private RecordingLogger logger;
        private UpdateChecker checker;

        [SetUp]
        public void SetUp()
        {
            client = new FakeHttpsClient();
            logger = new RecordingLogger();
            checker = new UpdateChecker(client, logger);
        }

        private static Installation Community()
        {
            var world = new InstallationWorld();
            world.AddCommunityInstallation(Root, Product.NeoEE);
            return world.Discover().Selected;
        }

        [Test]
        public async Task Section4_5_GameVersion_Outdated_WithTheLatestVersion()
        {
            client.Answer(Api + "&type=game&version=2.0.0.5", 200, "false\n").Answer(Api + "&type=game", 200, " 2.0.1.0 ");

            VersionCheckResult result = await checker.CheckAsync(Community(), VersionKind.Game);

            Assert.That(result.Outcome, Is.EqualTo(VersionCheckOutcome.UpdateAvailable));
            Assert.That(result.InstalledVersion, Is.EqualTo("2.0.0.5"));
            Assert.That(result.LatestVersion, Is.EqualTo("2.0.1.0"));
            Assert.That(client.Requests, Is.EqualTo(new[] { Api + "&type=game&version=2.0.0.5", Api + "&type=game" }));
            Assert.That(logger.Messages.Last(), Does.Contain("Version check: Game version 2.0.0.5 of " + Root + ": UpdateAvailable, latest 2.0.1.0"));
        }

        [TestCase("true")]
        [TestCase("False")]
        [TestCase("")]
        [TestCase("anything")]
        public async Task Section4_5_EveryOtherAnswerOf200_IsUpToDate_AsInTheSetup(string answer)
        {
            client.Answer(Api + "&type=game&version=2.0.0.5", 200, answer);

            VersionCheckResult result = await checker.CheckAsync(Community(), VersionKind.Game);

            Assert.That(result.Outcome, Is.EqualTo(VersionCheckOutcome.UpToDate));
            Assert.That(result.LatestVersion, Is.Null);
            Assert.That(client.Requests.Count, Is.EqualTo(1), "the latest version is only asked for an outdated one");
        }

        [Test]
        public async Task Section4_5_SetupVersion()
        {
            client.Answer(Api + "&type=setup&version=2.0.0", 200, "false").Answer(Api + "&type=setup", 200, "2.1.0");

            VersionCheckResult result = await checker.CheckAsync(Community(), VersionKind.Setup);

            Assert.That(result.Kind, Is.EqualTo(VersionKind.Setup));
            Assert.That(result.Outcome, Is.EqualTo(VersionCheckOutcome.UpdateAvailable));
            Assert.That(result.LatestVersion, Is.EqualTo("2.1.0"));
        }

        [Test]
        public async Task Section4_5_Legacy_IsCheckedWithTheAppIdAndVersionOfTheUninstallKey()
        {
            var world = new InstallationWorld();
            world.AddLegacyInstallation(Root, Product.NeoEE);
            Installation legacy = world.Discover().Selected;
            client.Answer(Api + "&type=game&version=1.7.2", 200, "false").Answer(Api + "&type=game", 200, "2.0.0.5");

            VersionCheckResult result = await checker.CheckAsync(legacy, VersionKind.Game);

            Assert.That(legacy.Kind, Is.EqualTo(InstallationKind.CommunityLegacy));
            Assert.That(result.Outcome, Is.EqualTo(VersionCheckOutcome.UpdateAvailable));
            Assert.That(result.InstalledVersion, Is.EqualTo("1.7.2"));
            Assert.That(client.Requests.First(), Is.EqualTo(Api + "&type=game&version=1.7.2"));
        }

        [Test]
        public async Task Section4_5_Foreign_IsNotChecked()
        {
            var world = new InstallationWorld();
            world.AddForeignInstallation(@"C:\Games\EE");
            Installation foreign = world.Discover().Selected;

            VersionCheckResult result = await checker.CheckAsync(foreign, VersionKind.Game);

            Assert.That(result.Outcome, Is.EqualTo(VersionCheckOutcome.NotPossible));
            Assert.That(client.Requests, Is.Empty);
        }

        [TestCase(HttpsOutcome.Timeout, UpdateApiFailure.Timeout)]
        [TestCase(HttpsOutcome.TlsError, UpdateApiFailure.TlsError)]
        [TestCase(HttpsOutcome.NetworkError, UpdateApiFailure.NetworkError)]
        public async Task NoAnswer_IsFailed_NeverUpToDate(HttpsOutcome outcome, UpdateApiFailure reason)
        {
            client.Fail(Api + "&type=game&version=2.0.0.5", outcome);

            VersionCheckResult result = await checker.CheckAsync(Community(), VersionKind.Game);

            Assert.That(result.Outcome, Is.EqualTo(VersionCheckOutcome.Failed));
            Assert.That(result.Failure, Is.EqualTo(reason));
        }

        [Test]
        public async Task AStatusOtherThan200_IsFailed()
        {
            client.Answer(Api + "&type=game&version=2.0.0.5", 503, "false");

            VersionCheckResult result = await checker.CheckAsync(Community(), VersionKind.Game);

            Assert.That(result.Outcome, Is.EqualTo(VersionCheckOutcome.Failed));
            Assert.That(result.Failure, Is.EqualTo(UpdateApiFailure.StatusNotOk));
        }

        [Test]
        public async Task Section4_5_TheLatestVersion_IsAQuestionMark_WithoutAnAnswer()
        {
            client.Answer(Api + "&type=game&version=2.0.0.5", 200, "false");

            VersionCheckResult result = await checker.CheckAsync(Community(), VersionKind.Game);

            Assert.That(result.Outcome, Is.EqualTo(VersionCheckOutcome.UpdateAvailable));
            Assert.That(result.LatestVersion, Is.EqualTo("?"));
        }

        [TestCase("2.0.1.0", "2.0.1.0")]
        [TestCase("  2.0.1 beta_3-rc  ", "2.0.1 beta_3-rc")]
        [TestCase("12345678901234567890123456789012", "12345678901234567890123456789012")]
        [TestCase("123456789012345678901234567890123", "?")]
        [TestCase("2.0<script>", "?")]
        [TestCase("2.0\n2.1", "?")]
        [TestCase("v2.0.1", "v2.0.1")]
        [TestCase("2,0", "?")]
        [TestCase("2.0.1\u00e9", "?")]
        [TestCase("", "?")]
        [TestCase(null, "?")]
        public void Section4_5_DisplayableVersion_AtMost32AllowedCharacters(string answer, string shown)
        {
            Assert.That(UpdateChecker.DisplayableVersion(answer), Is.EqualTo(shown));
        }

        [Test]
        public void TypeNames_AreThoseOfTheSetup()
        {
            Assert.That(UpdateChecker.TypeName(VersionKind.Game), Is.EqualTo("game"));
            Assert.That(UpdateChecker.TypeName(VersionKind.Setup), Is.EqualTo("setup"));
        }
    }
}
