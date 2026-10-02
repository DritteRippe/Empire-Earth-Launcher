using System;
using System.Linq;
using Empire_Earth_Launcher.Core.Backup;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Lobby;
using Empire_Earth_Launcher.Core.Maintenance;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.TestSupport;
using Empire_Earth_Launcher.Tests.Won;
using Empire_Earth_WON;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// The texts of the maintenance tools (<see cref="Texts"/>, L-WP8) in English, the neutral language, for results of the
    /// real core classes: the WON login reset names the backup folder and that it holds login data, the import names every
    /// file it left out and the multiplayer note, the name check lists the names. German and French have the same keys
    /// (<c>ResourceParityTests</c>).
    /// </summary>
    [TestFixture]
    [SetUICulture("en-US")]
    public class MaintenanceTextsTests
    {
        private const string Root = @"C:\Program Files (x86)\Neo Empire Earth";
        private const string VirtualStore = @"C:\Users\Player\AppData\Local\VirtualStore";
        private const string Downloads = @"C:\Users\Player\Downloads";

        private static readonly string[] Virtualized = { @"C:\Program Files (x86)" };

        private GameSettingsWorld w;
        private EffectivePathResolver paths;

        [SetUp]
        public void SetUp()
        {
            w = new GameSettingsWorld();
            w.World.AddCommunityInstallation(Root, Product.NeoEE);
            paths = new EffectivePathResolver(w.FileSystem, VirtualStore, Virtualized);
        }

        private Installation Installation()
        {
            return InstallationWorld.ByRoot(w.Discover(), Root);
        }

        private SavedGames Saves()
        {
            return new SavedGames(w.FileSystem, paths, w.SystemInfo, w.Guard, new FileBackup(w.FileSystem, w.Backups, w.Logger),
                w.World.Clock, w.Logger);
        }

        [Test]
        public void WonReset_NamesTheBackupFolder_WhichHoldsLoginData()
        {
            w.FileSystem.AddFile(Root + @"\Empire Earth\_wonlogin.ks", "x");
            var reset = new WonLoginReset(w.FileSystem, paths, w.Guard, new FileBackup(w.FileSystem, w.Backups, w.Logger), w.Logger);
            Assert.That(Texts.WonFiles(reset.Find(Installation())), Is.EqualTo("WON login files: " + Root + @"\Empire Earth\_wonlogin.ks"));

            WonResetResult result = reset.Reset(Installation());

            Assert.That(Texts.WonResult(result), Is.EqualTo("1 files moved to " + result.BackupFolder +
                ". This folder contains login data: never pass it on. If the login still fails, repair the installation with the setup."));
            Assert.That(Texts.WonResult(reset.Reset(Installation())), Is.EqualTo("No WON login file was found; there is nothing to reset."));
            w.Mutexes.With("StainlessSteelStudiosPresentsEmpireEarth");
            Assert.That(Texts.WonResult(reset.Reset(Installation())), Is.EqualTo("Not possible while Empire Earth.exe is running."));
        }

        [Test]
        public void Import_NamesEveryFileItLeftOut_AndTheMultiplayerNote()
        {
            w.FileSystem.AddFile(Downloads + @"\Kampf um Köln.ees", "s");
            w.FileSystem.AddFile(Downloads + @"\virus.exe", "x");
            w.FileSystem.AddFile(Root + @"\Empire Earth\Data\Saved Games\Duel.ees", "old");
            w.FileSystem.AddFile(Downloads + @"\Duel.ees", "new");
            SavedGames saves = Saves();
            ImportPlan plan = saves.PlanImport(Installation(), Game.EmpireEarth,
                new[] { Downloads + @"\Kampf um Köln.ees", Downloads + @"\virus.exe", Downloads + @"\Duel.ees" });
            Assert.That(Texts.ImportConfirm(plan), Does.Contain(Root + @"\Empire Earth\Data\Saved Games\Duel.ees"));

            ImportResult result = saves.Import(plan, false);

            Assert.That(Texts.ImportResult(result).Split('\n').Select(line => line.TrimEnd('\r')), Is.EqualTo(new[]
            {
                "1 of 3 files imported.",
                "virus.exe: only .ees and .scn files can be imported",
                "Duel.ees: not replaced",
                "Kampf um Köln.ees: the name has characters outside plain ASCII; in multiplayer every player needs exactly this name."
            }));
        }

        [Test]
        public void Export_NamesTheFolder()
        {
            w.FileSystem.AddFile(Root + @"\Empire Earth\Data\Scenarios\Island.scn", "s");
            w.FileSystem.AddDirectory(@"C:\Users\Player\Documents");

            ExportResult result = Saves().Export(Installation(), @"C:\Users\Player\Documents");

            Assert.That(Texts.ExportResult(result), Is.EqualTo("1 files exported to: " + result.Folder));
            Assert.That(Texts.SavedGamesState(Saves().List(Installation())), Is.EqualTo("0 saved games and 1 scenarios found."));
            Assert.That(Texts.ExportResult(Saves().Export(Installation(), Root)), Is.EqualTo("Choose a folder outside the game folders."));
        }

        [Test]
        public void NameCheck_ListsTheNames()
        {
            w.FileSystem.AddFile(Root + @"\Empire Earth\" + LobbyPersistentData.GlobalDataFileName,
                LobbyFileBuilder.GlobalFile(true, true, 1, "Jürgen"));
            w.FileSystem.AddDirectory(Root + @"\Empire Earth - The Art of Conquest\Users\Zoë");
            w.FileSystem.AddDirectory(Root + @"\Empire Earth\Users\Player");
            var checks = new NameChecks(w.FileSystem, paths, new LobbyProfileRepository(w.Logger, w.FileSystem, paths), w.Logger);

            string text = Texts.NameCheck(checks.Check(Installation()));

            Assert.That(text.Split('\n').Select(line => line.TrimEnd('\r')), Is.EqualTo(new[]
            {
                "2 names have characters outside plain ASCII:",
                "Empire Earth, lobby profile: Jürgen",
                "The Art of Conquest, player: Zoë"
            }));
        }

        [Test]
        public void VirtualStore_SaysWhatIsSerious()
        {
            w.FileSystem.AddFile(VirtualStore + @"\Program Files (x86)\Neo Empire Earth\Empire Earth\neoee.dll", "patched");
            w.FileSystem.AddFile(VirtualStore + @"\Program Files (x86)\Neo Empire Earth\Empire Earth\neoee.log", "log");

            VirtualStoreReport report = new VirtualStoreScanner(w.FileSystem, paths, w.Logger).Scan(Installation());

            Assert.That(Texts.VirtualStoreState(report), Does.StartWith("1 files of the installation or program files are used from the VirtualStore"));
            Assert.That(Texts.VirtualStoreState(report), Does.Contain("1 other files (lobby profiles, logs, saved games)"));
            Assert.That(Texts.VirtualStoreFiles(report).Split('\n')[0].TrimEnd('\r'), Is.EqualTo(
                VirtualStore + @"\Program Files (x86)\Neo Empire Earth\Empire Earth\neoee.dll (used instead of " + Root + @"\Empire Earth\neoee.dll)"));
        }
    }
}
