using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Maintenance;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Properties;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// The UI mapping of the registry cleanup (<see cref="CleanupView"/>, ADR 0007 plan review, ADR 0014): without a key the
    /// launcher could delete the page says "nothing to clean up", shows the read-only list and no enabled delete button; with
    /// keys the button works only when one is selected and no setup runs.
    /// </summary>
    [TestFixture]
    [SetUICulture("en-US")]
    public class CleanupViewTests
    {
        private GameSettingsWorld w;

        [SetUp]
        public void SetUp()
        {
            w = new GameSettingsWorld();
        }

        private CleanupScan Scan()
        {
            return new RegistryCleanup(w.Registry, w.FileSystem, w.Guard, w.Backups, w.Logger).Scan(w.Discover());
        }

        private void SeedStale(string id, string folder = @"D:\Old\Empire Earth")
        {
            RegistryLocation key = CleanupCandidates.All.Single(entry => entry.Id == id).Key;
            w.RawRegistry.SeedKey(key);
            w.World.SetInstalledFrom(key, folder);
        }

        [TestCase(0)]
        [TestCase(3)]
        public void WithoutCandidates_NothingToCleanUp_TheReadOnlyList_AndNoEnabledDeleteButton(int selected)
        {
            SeedStale("hklm32-sssi-ee");
            w.RawRegistry.Seed(RegistryLocation.LocalMachine32(@"Software\Sierra\CDKeys"), "Empire Earth", RegistryValue.FromString("NOT-A-KEY-0000"));

            CleanupView view = CleanupView.For(Scan(), selected, true);

            Assert.That(view.Summary, Is.EqualTo(Resources.CleanupNothingToCleanUp));
            Assert.That(view.Summary, Is.EqualTo("Nothing to clean up: no entry of your account is a leftover."));
            Assert.That(view.ShowsList, Is.False);
            Assert.That(view.DeleteEnabled, Is.False);
            Assert.That(view.ReadOnlyText.Split('\n').Select(line => line.TrimEnd('\r')), Is.EqualTo(new[]
            {
                @"HKEY_LOCAL_MACHINE\Software\WOW6432Node\SSSI\Empire Earth: leftover of a removed installation (folder D:\OLD\Empire Earth no longer exists). It applies to all users, so the launcher does not change it. To remove it: start the Registry Editor as administrator, right-click the key, choose Export and save the file, then delete the key.",
                @"HKEY_LOCAL_MACHINE\Software\WOW6432Node\Sierra: do not delete, it contains the NeoEE CD keys (CD keys present)."
            }));
            Assert.That(view.ReadOnlyText, Does.Not.Contain("NOT-A-KEY"), "never a value of the CD keys");
        }

        [Test]
        public void WithCandidates_TheDeleteButtonNeedsASelection_AndNoSetup()
        {
            SeedStale("hkcu-ee-ee");
            SeedStale("vs-sssi-ee");
            CleanupScan scan = Scan();

            CleanupView none = CleanupView.For(scan, 0, true);
            CleanupView one = CleanupView.For(scan, 1, true);
            CleanupView setup = CleanupView.For(scan, 1, false);

            Assert.That(none.Summary, Is.EqualTo("2 entries of your account are leftovers of a removed installation. Select the ones to delete."));
            Assert.That(none.ShowsList, Is.True);
            Assert.That(none.OfferedTexts, Is.EqualTo(new[]
            {
                @"HKEY_CURRENT_USER\Software\SSSI\Empire Earth (folder D:\OLD\Empire Earth no longer exists)",
                @"HKEY_CURRENT_USER\Software\Classes\VirtualStore\MACHINE\SOFTWARE\SSSI\Empire Earth (folder D:\OLD\Empire Earth no longer exists)"
            }));
            Assert.That(none.DeleteEnabled, Is.False);
            Assert.That(one.DeleteEnabled, Is.True);
            Assert.That(setup.DeleteEnabled, Is.False, "contract 4.2: no change while a setup runs");
            Assert.That(none.ReadOnlyText, Is.Empty);
        }

        [Test]
        public void WhileTheScanRuns_ItSaysChecking()
        {
            CleanupView view = CleanupView.For(null, 1, true);

            Assert.That(view.Summary, Is.EqualTo(Resources.ToolsChecking));
            Assert.That(view.DeleteEnabled, Is.False);
            Assert.That(view.ShowsList, Is.False);
        }

        [Test]
        public void TheAdviceOfAKeptKey_SaysWhy()
        {
            w.World.AddCommunityInstallation(GameSettingsWorld.NeoRoot, Product.NeoEE);
            SeedStale("vs-sssi-ee", GameSettingsWorld.NeoRoot + @"\Empire Earth");
            w.FileSystem.AddDrive("E:", DriveKind.Removable);
            SeedStale("vs-maddoc-aoc", @"E:\Games\AoC");

            CleanupView view = CleanupView.For(Scan(), 0, true);

            Assert.That(view.ReadOnlyText, Does.Contain(@"SOFTWARE\SSSI\Empire Earth: kept, its folder C:\PROGRAM FILES (X86)\NEO EMPIRE EARTH\Empire Earth exists."));
            Assert.That(view.ReadOnlyText, Does.Contain(@"SOFTWARE\Mad Doc Software\EE-AOC: kept, its folder E:\GAMES\AoC is on a drive that is not connected or not a local hard disk."));
        }
    }
}
