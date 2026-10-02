using System.IO;
using Empire_Earth_Launcher.Core.Settings;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Settings
{
    /// <summary>
    /// Where the launcher keeps its per-user files (<see cref="LauncherPaths"/>). The real folders of the user are
    /// only computed, never written.
    /// </summary>
    [TestFixture]
    public class LauncherPathsTests
    {
        private static readonly string LocalAppData = Path.Combine(Path.GetTempPath(), "LocalAppData");
        private static readonly string Temp = Path.Combine(Path.GetTempPath(), "Temp");

        [Test]
        public void UserDataDirectory_IsBelowLocalApplicationData()
        {
            Assert.That(LauncherPaths.GetUserDataDirectory(LocalAppData, Temp),
                Is.EqualTo(Path.Combine(LocalAppData, "Empire Earth Launcher")));
        }

        [TestCase(null)]
        [TestCase("")]
        public void UserDataDirectory_WithoutLocalApplicationData_FallsBackToTheTemporaryFolder(string localAppData)
        {
            // Accounts without a profile have no %LOCALAPPDATA%; the temporary folder is still per user.
            Assert.That(LauncherPaths.GetUserDataDirectory(localAppData, Temp),
                Is.EqualTo(Path.Combine(Temp, "Empire Earth Launcher")));
        }

        [Test]
        public void LogAndSettings_AreInTheUserDataDirectory()
        {
            Assert.That(LauncherPaths.LogFile, Is.EqualTo(Path.Combine(LauncherPaths.UserDataDirectory, "log.txt")));
            Assert.That(LauncherPaths.SettingsFile, Is.EqualTo(Path.Combine(LauncherPaths.UserDataDirectory, "settings.json")));
        }
    }
}
