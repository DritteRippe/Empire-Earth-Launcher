using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Integrity;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.RealMachineTests.Harness;
using Empire_Earth_Launcher.Tests.TestSupport;
using Microsoft.Win32;
using NUnit.Framework;

namespace Empire_Earth_Launcher.RealMachineTests.SelfTest
{
    /// <summary>
    /// The checks of a step end to end on an in-memory computer: the same code the RealMachine fixtures run on the runner, with
    /// the fakes of the unit tests instead of the real adapters. Every check passes for a matching expectation and reports each
    /// difference, never with a hash.
    /// </summary>
    [TestFixture]
    [Category(RealMachineCategories.SelfTest)]
    public class HarnessSessionTests
    {
        private static readonly Regex Sha256 = new Regex("[0-9a-fA-F]{64}");

        private HarnessWorld world;

        [SetUp]
        public void SetUp()
        {
            world = new HarnessWorld();
        }

        private static void AssertNoHash(IEnumerable<string> lines)
        {
            Assert.That(lines.Where(line => Sha256.IsMatch(line)), Is.Empty, "no line holds a hash");
        }

        // --- Discovery and integrity -----------------------------------------------------------------------------------

        [Test]
        public void MatchingExpectation_GivesNoProblem()
        {
            world.InstallCommunity();
            HarnessSession session = world.Session(HarnessWorld.Expected(
                ", 'sources': ['RegistryRecord', 'UninstallKey', 'InstalledFrom'], 'gameVersion': '2.0.0.5'" +
                ", 'integrity': { 'quick': { 'state': 'Ok', 'findings': [], 'offersRepair': false }, 'full': { 'state': 'Ok' } }" +
                ", 'defaultsStatus': { 'EE': 'Applied', 'AoC': 'Applied' }, 'consistency': { 'expected': [], 'allowed': [] }"));

            DiscoveryResult result = session.Discover();

            Assert.That(session.CheckDiscovery(result), Is.Empty);
            Assert.That(session.CheckIntegrity(result, IntegrityCheckKind.Quick, out int quick), Is.Empty);
            Assert.That(session.CheckIntegrity(result, IntegrityCheckKind.Full, out int full), Is.Empty);
            Assert.That(session.CheckGameSettingsState(result, out int state), Is.Empty);
            Assert.That(new[] { quick, full, state }, Is.All.EqualTo(1));
            Assert.That(session.CheckMachineState(), Is.Empty);
            Assert.That(session.Writes.Writes, Is.Empty);
        }

        [Test]
        public void EveryDifferenceOfTheDiscovery_IsAProblem()
        {
            world.InstallCommunity();
            HarnessSession session = world.Session(
                "{ 'schema': 1, 'selectedRoot': 'D:\\\\Other', 'installations': [" +
                " { 'product': 'NeoEE', 'root': '" + HarnessWorld.Json(HarnessWorld.Root) + "', 'kind': 'CommunityLegacy', 'mode': 'User'," +
                " 'appId': 'A24FCC7A-5491-4FEA-837B-4E4430C349DA', 'contractVersion': 0, 'hasArtOfConquest': false, 'state': 'Damaged'," +
                " 'missingPrograms': ['EE'], 'sources': ['UninstallKey'], 'sourcesInclude': ['UserChoice'], 'otherProductInRoot': 'NeoEE'," +
                " 'gameVersion': '1.0', 'setupVersion': '1.7.2' }," +
                " { 'product': 'EE', 'root': 'D:\\\\Missing' } ] }");

            IReadOnlyList<string> problems = session.CheckDiscovery(session.Discover());

            string[] expected =
            {
                "the product", "the kind", "the install mode", "the AppId", "the contract version", "HasArtOfConquest", "the state",
                "the missing programs", "the sources is {", "lack {UserChoice}", "the other product", "the game version", "the setup version",
                @"EE in D:\Missing: not found", "selected is " + HarnessWorld.Root
            };
            foreach (string text in expected)
                Assert.That(problems, Has.Some.Contains(text), text);
            Assert.That(problems, Has.Count.EqualTo(expected.Length));
        }

        [Test]
        public void AnotherInstallation_IsAProblem_OnlyWhenTheListIsExact()
        {
            world.InstallCommunity();
            world.World.AddLegacyInstallation(@"D:\Games\Neo Empire Earth", Product.NeoEE, admin: false);

            HarnessSession strict = world.Session(HarnessWorld.Expected());
            IReadOnlyList<string> exact = strict.CheckDiscovery(strict.Discover());
            HarnessSession open = world.Session(HarnessWorld.Expected(top: ", 'exactInstallations': false"));

            Assert.That(exact, Has.Count.EqualTo(1).And.Some.StartsWith("an installation that is not expected: NeoEE community-legacy"));
            Assert.That(open.CheckDiscovery(open.Discover()), Is.Empty);
        }

        [Test]
        public void DamagedInstallation_ReportsFindingsAsPathAndKind_WithoutHashes()
        {
            world.InstallCommunity();
            world.World.FileSystem.DeleteFile(HarnessWorld.Full(HarnessWorld.Language));
            world.World.FileSystem.AddFile(HarnessWorld.Full(HarnessWorld.Help), SampleHashes.Content(99));
            world.World.FileSystem.AddFile(HarnessWorld.Full(HarnessWorld.Lobby), SampleHashes.Content(98));
            HarnessSession matching = world.Session(HarnessWorld.Expected(
                ", 'integrity': { 'quick': { 'state': 'Damaged', 'findings': [ { 'path': 'empire earth/language.dll', 'kind': 'Missing' } ]," +
                " 'offersRepair': true }, 'full': { 'state': 'Damaged', 'findings': [ { 'path': '" + HarnessWorld.Language + "', 'kind': 'Missing' }," +
                " { 'path': '" + HarnessWorld.Help + "', 'kind': 'HashDiffers' } ] } }"));
            HarnessSession wrong = world.Session(HarnessWorld.Expected(
                ", 'integrity': { 'quick': { 'state': 'Ok', 'findings': [], 'offersRepair': false }," +
                " 'full': { 'state': 'Damaged', 'findings': [ { 'path': '" + HarnessWorld.AocData + "', 'kind': 'Missing' } ] } }"));

            var report = new List<string>();
            Assert.That(matching.CheckIntegrity(matching.Discover(), IntegrityCheckKind.Quick, out _, report), Is.Empty);
            Assert.That(matching.CheckIntegrity(matching.Discover(), IntegrityCheckKind.Full, out _, report), Is.Empty);
            IReadOnlyList<string> quick = wrong.CheckIntegrity(wrong.Discover(), IntegrityCheckKind.Quick, out _, report);
            IReadOnlyList<string> full = wrong.CheckIntegrity(wrong.Discover(), IntegrityCheckKind.Full, out _, report);

            Assert.That(quick, Has.Some.Contains("the state is Damaged"));
            Assert.That(quick, Has.Some.Contains("OffersRepair is True, expected False"));
            Assert.That(quick, Has.Some.EndsWith("a finding that is not expected: " + HarnessWorld.Language + "|code|Missing"));
            Assert.That(full, Has.Some.EndsWith("a finding that is not expected: " + HarnessWorld.Help + "|data|HashDiffers"));
            Assert.That(full, Has.Some.EndsWith("an expected finding is missing: " + HarnessWorld.AocData + "|Missing"));
            Assert.That(report, Has.Some.Contains("Damaged (6 listed"));
            AssertNoHash(quick.Concat(full).Concat(report));
            Assert.That(world.World.Logger.Messages.Any(message => Sha256.IsMatch(message)), Is.True,
                "the log of the core holds the hashes; that is why it stays in the work folder");
        }

        [Test]
        public void ARunningSetup_IsNamedAsTheReasonOfACancelledCheck()
        {
            world.InstallCommunity();
            world.Mutexes.With(Product.EE.SetupMutexName);
            HarnessSession expectingOk = world.Session(HarnessWorld.Expected(", 'integrity': { 'quick': { 'state': 'Ok' } }"));
            HarnessSession expectingCancel = world.Session(HarnessWorld.Expected(
                ", 'integrity': { 'quick': { 'state': 'Cancelled', 'cancelReason': 'SetupRunning' } }"));

            Assert.That(expectingOk.CheckIntegrity(expectingOk.Discover(), IntegrityCheckKind.Quick, out _),
                Has.Some.Contains("a setup mutex still exists"));
            Assert.That(expectingCancel.CheckIntegrity(expectingCancel.Discover(), IntegrityCheckKind.Quick, out _), Is.Empty);
        }

        [Test]
        public void DefaultsStatus_IsCompared()
        {
            world.InstallCommunity(setupWroteDefaults: false);
            HarnessSession session = world.Session(HarnessWorld.Expected(", 'defaultsStatus': { 'EE': 'Applied', 'AoC': 'Pending' }"));

            IReadOnlyList<string> problems = session.CheckGameSettingsState(session.Discover(), out int checkedInstallations);

            Assert.That(checkedInstallations, Is.EqualTo(1));
            Assert.That(problems, Is.EqualTo(new[] { "EE in " + HarnessWorld.Root + " EE: the defaults status is Pending, expected Applied" }));
        }

        // --- Defaults of the launcher start and reset ------------------------------------------------------------------

        [Test]
        public void FreshAccount_GetsTheRecommendedValues_ASecondStartChangesNothing_AndTheResetBacksUp()
        {
            world.InstallCommunity(setupWroteDefaults: false);
            HarnessSession session = world.Session(HarnessWorld.Expected(
                ", 'defaultsAtStart': { 'EE': 'FirstRun', 'AoC': 'FirstRun' }, 'installedFromAtStart': { 'EE': 'Created', 'AoC': 'Created' }",
                ", 'defaults': { 'expectRecommendedValues': true, 'reset': true }"));
            DiscoveryResult result = session.Discover();

            var report = new List<string>();
            Assert.That(session.ApplyDefaultsAtStart(result, report), Is.Empty);
            Assert.That(session.Writes.Writes, Is.Not.Empty);
            Assert.That(session.CheckSecondStart(result), Is.Empty);
            Assert.That(session.CheckReset(result), Is.Empty);
            Assert.That(session.CheckMachineState(), Is.Empty);

            Assert.That(report, Has.Some.StartsWith("defaults at the start: " + HarnessWorld.Root + " AoC: Installed From Created, defaults FirstRun"));
            Assert.That(world.World.FileSystem.AllFiles.Where(file => file.EndsWith(".reg")).ToList(),
                Has.Count.EqualTo(2).And.All.StartWith(HarnessWorld.Work + @"\Backups\"));
            Assert.That(session.Writes.Writes.Select(write => write.Key.Hive), Is.All.EqualTo(RegistryHive.CurrentUser));
        }

        [Test]
        public void InstallingAccount_GetsNothingWritten()
        {
            world.InstallCommunity();
            HarnessSession session = world.Session(HarnessWorld.Expected(
                ", 'defaultsAtStart': { 'EE': 'None', 'AoC': 'None' }, 'installedFromAtStart': { 'EE': 'Present', 'AoC': 'Present' }",
                ", 'defaults': { 'expectNoWrites': true }"));
            DiscoveryResult result = session.Discover();

            Assert.That(session.ApplyDefaultsAtStart(result), Is.Empty);
            Assert.That(session.CheckSecondStart(result), Is.Empty);
            Assert.That(session.CheckMachineState(), Is.Empty);
        }

        [Test]
        public void WritesWhereNoneAreExpected_AndOtherResultsPerGame_AreProblems()
        {
            world.InstallCommunity(setupWroteDefaults: false);
            HarnessSession session = world.Session(HarnessWorld.Expected(
                ", 'defaultsAtStart': { 'EE': 'None' }, 'installedFromAtStart': { 'AoC': 'Present' }", ", 'defaults': { 'expectNoWrites': true }"));

            IReadOnlyList<string> problems = session.ApplyDefaultsAtStart(session.Discover());

            Assert.That(problems, Has.Some.Contains("EE: the defaults at the start are FirstRun, expected None"));
            Assert.That(problems, Has.Some.Contains("AoC: \"Installed From\" at the start is Created, expected Present"));
            Assert.That(problems, Has.Some.StartsWith("the start wrote "));
        }

        [Test]
        public void SavedSetupValues_AreComparedAfterTheStart_ExceptTheAllowedDifferences()
        {
            // The values a setup wrote for its account: here those of a first launcher start, which are the same (contract 3.2).
            world.InstallCommunity(setupWroteDefaults: false);
            HarnessSession setup = world.Session(HarnessWorld.Expected(top: ", 'defaults': { }"));
            Assert.That(setup.ApplyDefaultsAtStart(setup.Discover()), Is.Empty);
            string file = HarnessWorld.Work + @"\setup-values.json";
            HarnessSession record = world.Session(HarnessWorld.Expected(top: ", 'defaults': { 'recordSetupValuesTo': '" + HarnessWorld.Json(file) + "' }"));
            Assert.That(record.RecordSetupValues(record.Discover()), Is.Empty);

            // The job removes the game settings of the account; the launcher starts as for a new account and changes one value.
            foreach (Game game in Game.All)
                world.World.Registry.DeleteSubKeyTree(RegistryLocation.CurrentUser(Product.EE.GetGameSettingsKey(game)));
            world.World.Registry.DeleteSubKeyTree(RegistryLocation.CurrentUser(Product.EE.DefaultsMarkerKey));
            world.World.Registry.DeleteSubKeyTree(RegistryLocation.CurrentUser(ContractNames.GpuPreferencesKey));
            world.World.Registry.Seed(RegistryLocation.CurrentUser(Product.EE.GetGameSettingsKey(Game.EmpireEarth)), "Music Volume",
                RegistryValue.FromDWord(10));
            HarnessSession compared = world.Session(HarnessWorld.Expected(top: ", 'defaults': { 'compareWithSetupValuesFrom': '" +
                                                                               HarnessWorld.Json(file) + "' }"));
            IReadOnlyList<string> problems = compared.ApplyDefaultsAtStart(compared.Discover());
            HarnessSession lenient = world.Session(HarnessWorld.Expected(top: ", 'defaults': { 'compareWithSetupValuesFrom': '" +
                                                                              HarnessWorld.Json(file) + "', 'allowedDifferences': ['music volume'] }"));

            Assert.That(problems, Is.EqualTo(new[]
                { HarnessWorld.Root + " EE \"Music Volume\" is DWord:10, the setup wrote DWord:44" }));
            Assert.That(lenient.ApplyDefaultsAtStart(lenient.Discover()), Is.Empty);
        }

        [Test]
        public void SetupValues_AreOnlySavedBelowTheWorkFolder()
        {
            world.InstallCommunity();
            HarnessSession session = world.Session(HarnessWorld.Expected(top: ", 'defaults': { 'recordSetupValuesTo': 'D:\\\\elsewhere\\\\values.json' }"));

            Assert.That(session.RecordSetupValues(session.Discover()), Has.Some.StartsWith("recordSetupValuesTo must be below the work folder"));
            Assert.That(world.World.FileSystem.FileExists(@"D:\elsewhere\values.json"), Is.False);
        }

        // --- State at the end ------------------------------------------------------------------------------------------

        [Test]
        public void MachineState_NamesRefusedChanges_ChangedFiles_AndChangedCdKeysWithoutNames()
        {
            world.InstallCommunity();
            RegistryLocation cdKeys = RegistryLocation.LocalMachine32(ContractNames.CdKeysKey);
            world.World.Registry.Seed(cdKeys, "CI-Dummy", RegistryValue.FromString("NOT-A-KEY-0000"));
            HarnessSession session = world.Session(HarnessWorld.Expected());

            world.World.Registry.Seed(cdKeys, "CI-Dummy", RegistryValue.FromString("NOT-A-KEY-0001"));
            world.World.FileSystem.AddFile(HarnessWorld.Full("Empire Earth/Data/new.txt"), "new");
            Assert.Throws<HarnessViolationException>(() =>
                session.ReadOnlyRegistry.SetValue(RegistryLocation.CurrentUser(@"Software\X"), "Y", RegistryValue.FromDWord(1)));
            IReadOnlyList<string> problems = session.CheckMachineState();

            Assert.That(problems, Has.Some.StartsWith("refused: SetValue HKCU\\Software\\X @\"Y\" by read-only code"));
            Assert.That(problems, Has.Some.EqualTo("changed during the checks: HKLM32\\" + ContractNames.CdKeysKey +
                                                  ": changed (1 entries; names and values are not shown)"));
            Assert.That(problems, Has.Some.EqualTo("changed during the checks: " + HarnessWorld.Root + @"\Empire Earth\Data\new.txt: added"));
            Assert.That(problems.Where(line => line.Contains("CI-Dummy") || line.Contains("NOT-A-KEY")), Is.Empty);
        }

        [Test]
        public void MachineState_NamesARunningGame_AndChangesOfAStepWithoutDefaults()
        {
            world.InstallCommunity(setupWroteDefaults: false);
            HarnessSession session = world.Session(HarnessWorld.Expected());
            world.Mutexes.With(Game.ArtOfConquest.MutexName);
            session.Writes.CreateSubKey(RegistryLocation.CurrentUser(@"Software\Empire Earth Community\GameDefaults\EE"));

            IReadOnlyList<string> problems = session.CheckMachineState();

            Assert.That(problems, Has.Some.StartsWith("EE-AOC.exe runs"));
            Assert.That(problems, Has.Some.StartsWith("1 registry change(s) in a step without defaults"));
        }
    }
}
