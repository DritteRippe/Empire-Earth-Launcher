using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// The language setting is applied when the launcher starts (<c>Program.ApplyUiLanguage</c>, ADR 0009): for the
    /// UI thread and every thread started later, and only for the texts, not for number and date formats.
    /// </summary>
    [TestFixture]
    public class UiLanguageStartupTests
    {
        private CultureInfo uiCulture;
        private CultureInfo culture;
        private CultureInfo defaultThreadUiCulture;
        private RecordingLogger logger;

        [SetUp]
        public void SetUp()
        {
            uiCulture = Thread.CurrentThread.CurrentUICulture;
            culture = Thread.CurrentThread.CurrentCulture;
            defaultThreadUiCulture = CultureInfo.DefaultThreadCurrentUICulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-GB");
            CultureInfo.DefaultThreadCurrentUICulture = null;
            logger = new RecordingLogger();
        }

        [TearDown]
        public void TearDown()
        {
            Thread.CurrentThread.CurrentUICulture = uiCulture;
            Thread.CurrentThread.CurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = defaultThreadUiCulture;
        }

        [Test]
        public void ChosenLanguage_IsUsedForTheTexts_OnThisAndLaterThreads()
        {
            CultureInfo applied = global::Empire_Earth_Launcher.Program.ApplyUiLanguage(logger, "de");

            Assert.That(applied.Name, Is.EqualTo("de"));
            Assert.That(Thread.CurrentThread.CurrentUICulture.Name, Is.EqualTo("de"));
            Assert.That(CultureInfo.DefaultThreadCurrentUICulture.Name, Is.EqualTo("de"));
            Assert.That(Thread.CurrentThread.CurrentCulture.Name, Is.EqualTo("en-GB"), "formats stay those of Windows");
            string onAnotherThread = Task.Run(() => CultureInfo.CurrentUICulture.Name).Result;
            Assert.That(onAnotherThread, Is.EqualTo("de"));
            Assert.That(logger.MessagesOf(LogLevel.Info), Does.Contain("UI language: de (launcher setting)"));
        }

        [Test]
        public void NoChoice_KeepsTheWindowsLanguage()
        {
            Assert.That(global::Empire_Earth_Launcher.Program.ApplyUiLanguage(logger, ""), Is.Null);

            Assert.That(Thread.CurrentThread.CurrentUICulture.Name, Is.EqualTo("en-US"));
            Assert.That(CultureInfo.DefaultThreadCurrentUICulture, Is.Null);
            Assert.That(logger.MessagesOf(LogLevel.Info), Does.Contain("UI language: en-US (Windows)"));
            Assert.That(logger.MessagesOf(LogLevel.Warning), Is.Empty);
        }

        [Test]
        public void UnknownChoice_IsLogged_AndTheWindowsLanguageIsUsed()
        {
            Assert.That(global::Empire_Earth_Launcher.Program.ApplyUiLanguage(logger, "es"), Is.Null);

            Assert.That(Thread.CurrentThread.CurrentUICulture.Name, Is.EqualTo("en-US"));
            Assert.That(logger.MessagesOf(LogLevel.Warning).Single(), Does.Contain("\"es\""));
        }
    }
}
