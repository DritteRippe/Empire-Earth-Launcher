using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Lobby;
using Empire_Earth_Launcher.Core.Maintenance;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using Empire_Earth_Launcher.Tests.Won;
using Empire_Earth_WON;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Maintenance
{
    /// <summary>
    /// <see cref="NameChecks"/> (R10, forum t=3563 p=23879, t=2126 p=14281, forum report section 8 row 16 and test case 17):
    /// lobby profiles and player folders <c>Users\&lt;Name&gt;</c> with characters outside printable ASCII are warned about,
    /// from the game folder and its VirtualStore copy; the names never reach the log.
    /// </summary>
    [TestFixture]
    public class NameChecksTests
    {
        private const string Root = @"C:\Program Files (x86)\Neo Empire Earth";
        private const string EeFolder = Root + @"\Empire Earth";
        private const string AocFolder = Root + @"\Empire Earth - The Art of Conquest";
        private const string VirtualStore = @"C:\Users\Player\AppData\Local\VirtualStore";
        private const string EeCopy = VirtualStore + @"\Program Files (x86)\Neo Empire Earth\Empire Earth";

        private static readonly string[] Virtualized =
            { @"C:\Program Files", @"C:\Program Files (x86)", @"C:\ProgramData", @"C:\Windows" };

        private InstallationWorld world;

        [SetUp]
        public void SetUp()
        {
            world = new InstallationWorld();
            world.AddCommunityInstallation(Root, Product.NeoEE);
        }

        private NameCheckReport Check()
        {
            var fileSystem = new WriteForbiddingFileSystem(world.FileSystem);
            var paths = new EffectivePathResolver(fileSystem, VirtualStore, Virtualized);
            var checks = new NameChecks(fileSystem, paths, new LobbyProfileRepository(world.Logger, fileSystem, paths), world.Logger);
            return checks.Check(InstallationWorld.ByRoot(world.Discover(), Root));
        }

        [Test]
        public void ProfilesAndPlayerFolders_OutsidePrintableAscii_AreWarnedAbout()
        {
            world.FileSystem.AddFile(EeCopy + @"\" + LobbyPersistentData.GlobalDataFileName,
                LobbyFileBuilder.GlobalFile(true, true, 1, "Player", "Jürgen", "Ωmega"));
            world.FileSystem.AddDirectory(EeFolder + @"\Users\Player1");
            world.FileSystem.AddDirectory(EeFolder + @"\Users\Hans Müller");
            world.FileSystem.AddDirectory(EeCopy + @"\Users\Zoë");
            world.FileSystem.AddDirectory(AocFolder + @"\Users\[CLAN] Bob~");

            NameCheckReport report = Check();

            Assert.That(report.NamesChecked, Is.EqualTo(7));
            Assert.That(report.Warnings.Select(warning => warning.Game.Id + " " + warning.Source + " " + warning.Name), Is.EquivalentTo(new[]
            {
                "EE LobbyProfile Jürgen", "EE LobbyProfile Ωmega", "EE PlayerFolder Hans Müller", "EE PlayerFolder Zoë"
            }));
            Assert.That(world.Logger.Messages.Any(message => message.Contains("Jürgen") || message.Contains("Müller") || message.Contains("Zoë")),
                Is.False, "names are never logged");
        }

        [TestCase("Player 1", true)]
        [TestCase("[CLAN] Bob~", true)]
        [TestCase("Jürgen", false)]
        [TestCase("Tab\tName", false)]
        [TestCase("Ω", false)]
        [TestCase("", true)]
        public void PrintableAscii_IsSpaceToTilde(string name, bool expected)
        {
            Assert.That(SavedGames.IsPrintableAscii(name), Is.EqualTo(expected));
        }

        [Test]
        public void WithoutProfilesAndFolders_NothingIsWarned()
        {
            NameCheckReport report = Check();

            Assert.That(report.NamesChecked, Is.EqualTo(0));
            Assert.That(report.Warnings, Is.Empty);
        }
    }
}
