using System;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.RealMachineTests.Expectations;
using Empire_Earth_Launcher.RealMachineTests.Harness;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using Microsoft.Win32;

namespace Empire_Earth_Launcher.RealMachineTests.SelfTest
{
    /// <summary>
    /// An in-memory computer for the self-tests of the harness (the fakes of the unit tests): an EE installation of a setup since
    /// v2 for all users in <see cref="Root"/>, with synthetic files (<c>sample-&lt;n&gt;</c>), their manifest, <c>install.ini</c>,
    /// the record and the uninstall key, and a <see cref="HarnessSession"/> over it with the work folder <see cref="Work"/>.
    /// </summary>
    internal sealed class HarnessWorld
    {
        public const string Root = @"C:\Program Files (x86)\Empire Earth";
        public const string Work = @"D:\e2e\A\installed";
        public const string AppId = InstallationWorld.EEAppId;
        public const string Tasks = "compatibility,compatibility_windows,firewallexception,desktopicon";

        public const string EeProgram = "Empire Earth/Empire Earth.exe";
        public const string Language = "Empire Earth/Language.dll";
        public const string Help = "Empire Earth/help.rtf";
        public const string Lobby = "Empire Earth/WONLobby.cfg";
        public const string AocProgram = "Empire Earth - The Art of Conquest/EE-AOC.exe";
        public const string AocData = "Empire Earth - The Art of Conquest/Data/file0001.dat";

        /// <summary>The installed files and the sample of their content.</summary>
        public static readonly Tuple<string, int>[] Files =
        {
            Tuple.Create(EeProgram, 1), Tuple.Create(Language, 2), Tuple.Create(Help, 3), Tuple.Create(Lobby, 4),
            Tuple.Create(AocProgram, 5), Tuple.Create(AocData, 6)
        };

        public HarnessWorld()
        {
            World = new InstallationWorld();
            SystemInfo = new FakeSystemInfo();
            Mutexes = new FakeMutexProbe();
        }

        public InstallationWorld World { get; }

        public FakeSystemInfo SystemInfo { get; }

        public FakeMutexProbe Mutexes { get; }

        /// <summary>The full path of a manifest path below <see cref="Root"/>.</summary>
        public static string Full(string manifestPath)
        {
            return WinPath.Combine(Root, manifestPath);
        }

        /// <summary><paramref name="path"/> written inside a JSON string.</summary>
        public static string Json(string path)
        {
            return path.Replace(@"\", @"\\");
        }

        /// <summary>
        /// The installation. <paramref name="setupWroteDefaults"/>: the setup wrote "Installed From" and the defaults marker of
        /// both games for this account (contract 3.5), as after a setup since v2 run by the same account.
        /// </summary>
        public void InstallCommunity(bool setupWroteDefaults = true)
        {
            foreach (Tuple<string, int> file in Files)
                World.FileSystem.AddFile(Full(file.Item1), SampleHashes.Content(file.Item2));
            World.FileSystem.AddFile(Full(Product.EE.SetupDataFolderName + "/" + ContractNames.ManifestFileName),
                string.Join("\n", Files.Select(file => SampleHashes.Of(file.Item2) + "  " + file.Item1)) + "\n");
            World.AddInstallInfo(Root, Product.EE, 1, "admin", "game,gameaoc", Tasks, AppId);
            World.AddRecord(RegistryHive.LocalMachine, RegistryView.Registry64, Product.EE, Root, 1, "admin", AppId);
            World.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, Product.EE, Root, AppId, "game,gameaoc", Tasks,
                contractVersion: 1);
            if (!setupWroteDefaults)
                return;
            World.SetInstalledFrom(InstallationWorld.EmpireEarthKey(Product.EE, RegistryHive.CurrentUser, RegistryView.Default),
                Full("Empire Earth"));
            World.SetInstalledFrom(InstallationWorld.ArtOfConquestKey(Product.EE, RegistryHive.CurrentUser, RegistryView.Default),
                Full("Empire Earth - The Art of Conquest"));
            foreach (Game game in Game.All)
                World.Registry.Seed(RegistryLocation.CurrentUser(Product.EE.DefaultsMarkerKey), game.Id, RegistryValue.FromDWord(1));
        }

        /// <summary>A session for the expectation <paramref name="json"/> (single quotes stand for double quotes).</summary>
        public HarnessSession Session(string json)
        {
            return new HarnessSession(ExpectationFile.Parse(json.Replace('\'', '"')), Work, World.Registry, World.FileSystem, SystemInfo,
                Mutexes, World.Clock, World.Logger);
        }

        /// <summary>
        /// The expectation of the installation as installed, with <paramref name="extra"/> appended to the installation and
        /// <paramref name="top"/> to the file (both JSON members with a leading comma, single quotes for double quotes).
        /// </summary>
        public static string Expected(string extra = "", string top = "")
        {
            return "{ 'schema': 1, 'scenario': 'A', 'step': 'installed', 'selectedRoot': '" + Json(Root) + "', 'installations': [ {" +
                   " 'product': 'EE', 'root': '" + Json(Root) + "', 'kind': 'Community', 'mode': 'Admin', 'appId': '" + AppId + "'," +
                   " 'contractVersion': 1, 'hasArtOfConquest': true, 'state': 'Ok', 'missingPrograms': []" + extra + " } ]" + top + " }";
        }
    }
}
