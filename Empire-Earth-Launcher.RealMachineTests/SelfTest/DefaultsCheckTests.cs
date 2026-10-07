using System.Collections.Generic;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.RealMachineTests.Checks;
using Empire_Earth_Launcher.RealMachineTests.Harness;
using NUnit.Framework;

namespace Empire_Earth_Launcher.RealMachineTests.SelfTest
{
    /// <summary>
    /// What the defaults of the launcher start may write on the runner: only the values of the table in the game settings keys
    /// of the installation's games, its defaults marker and the GPU preference of its programs, all in HKCU (contract 3.1 to
    /// 3.5) - narrower than the launcher's write policy.
    /// </summary>
    [TestFixture]
    [Category(RealMachineCategories.SelfTest)]
    public class DefaultsCheckTests
    {
        private static readonly RegistryLocation Settings = RegistryLocation.CurrentUser(@"Software\SSSI\Empire Earth");
        private static readonly RegistryLocation AocSettings = RegistryLocation.CurrentUser(@"Software\Mad Doc Software\EE-AOC");
        private static readonly RegistryLocation Marker = RegistryLocation.CurrentUser(@"Software\Empire Earth Community\GameDefaults\EE");
        private static readonly RegistryLocation Gpu = RegistryLocation.CurrentUser(ContractNames.GpuPreferencesKey);

        private Installation installation;

        [SetUp]
        public void SetUp()
        {
            var world = new HarnessWorld();
            world.InstallCommunity();
            installation = DiscoveryCheck.Find(world.World.Discover(), HarnessWorld.Root);
        }

        private IReadOnlyList<string> Unexpected(params RegistryWrite[] writes)
        {
            return DefaultsCheck.UnexpectedWrites(writes, new[] { installation });
        }

        [Test]
        public void TheWritesOfTheDefaults_AreExpected()
        {
            Assert.That(Unexpected(
                new RegistryWrite(RegistryOperation.CreateSubKey, Settings, null),
                new RegistryWrite(RegistryOperation.SetValue, Settings, "music volume"),
                new RegistryWrite(RegistryOperation.DeleteValue, Settings, "Game Bit Depth"),
                new RegistryWrite(RegistryOperation.CreateSubKey, AocSettings.Child("Game Options"), null),
                new RegistryWrite(RegistryOperation.SetValue, AocSettings.Child("Game Options"), "Ending Epoch"),
                new RegistryWrite(RegistryOperation.SetValue, Marker, "AoC"),
                new RegistryWrite(RegistryOperation.SetValue, Gpu, HarnessWorld.Full("Empire Earth/Empire Earth.exe")),
                new RegistryWrite(RegistryOperation.SetValue, Gpu, HarnessWorld.Full("Empire Earth - The Art of Conquest/EE-AOC.exe"))), Is.Empty);
        }

        [Test]
        public void EveryOtherWrite_IsAProblem()
        {
            IReadOnlyList<string> problems = Unexpected(
                new RegistryWrite(RegistryOperation.SetValue, RegistryLocation.LocalMachine64(@"Software\SSSI\Empire Earth"), "Music Volume"),
                new RegistryWrite(RegistryOperation.SetValue, RegistryLocation.CurrentUser(ContractNames.CdKeysKey), "x"),
                new RegistryWrite(RegistryOperation.SetValue, Settings, "Player Name"),
                new RegistryWrite(RegistryOperation.SetValue, Settings, "Map Size"),
                new RegistryWrite(RegistryOperation.SetValue, RegistryLocation.CurrentUser(@"Software\Neo\Empire Earth"), "Music Volume"),
                new RegistryWrite(RegistryOperation.SetValue, Marker, "NeoEE"),
                new RegistryWrite(RegistryOperation.SetValue, Gpu, @"C:\Other\Empire Earth.exe"),
                new RegistryWrite(RegistryOperation.SetValue, RegistryLocation.CurrentUser(ContractNames.CompatibilityLayersKey),
                    HarnessWorld.Full("Empire Earth/Empire Earth.exe")),
                new RegistryWrite(RegistryOperation.DeleteSubKeyTree, Settings, null));

            Assert.That(problems, Has.Count.EqualTo(9).And.All.StartsWith("a change the defaults may not make: "));
        }
    }
}
