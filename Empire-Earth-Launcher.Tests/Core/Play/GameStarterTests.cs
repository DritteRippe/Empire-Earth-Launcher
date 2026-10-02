using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Core.Repair;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Play
{
    /// <summary>
    /// <see cref="GameStarter"/> with fakes (ADR 0010, ARCHITECTURE 4.2, contract 3.6, 3.7, 4.2): the fixed order, the
    /// refusals, the start through the shell in the real game folder, the log line and the start errors as results.
    /// </summary>
    [TestFixture]
    public class GameStarterTests
    {
        private const string Root = @"C:\Program Files (x86)\Neo Empire Earth";
        private const string EeFolder = Root + @"\Empire Earth";
        private const string AocFolder = Root + @"\Empire Earth - The Art of Conquest";
        private const string EeProgram = EeFolder + @"\Empire Earth.exe";
        private const string AocProgram = AocFolder + @"\EE-AOC.exe";

        private List<string> journal;
        private FakeMutexProbe mutexes;
        private FakeProcessList processes;
        private InMemoryFileSystem fileSystem;
        private JournalPreparation preparation;
        private FakeProcessStarter starter;
        private RecordingLogger logger;
        private GameStarter gameStarter;
        private Installation installation;

        /// <summary>Class S and the first run, recorded in the journal; the results are set by the test.</summary>
        private sealed class JournalPreparation : IGameStartPreparation
        {
            private readonly List<string> journal;

            public JournalPreparation(List<string> journal)
            {
                this.journal = journal;
            }

            public GameSettingsResult SyncResult { get; set; } =
                new GameSettingsResult(GameSettingsOutcome.Done, null, null, null, null);

            public DefaultsAtStart DefaultsResult { get; set; } = DefaultsAtStart.None;

            public DisplayQuestion Question { get; set; }

            public GameSettingsResult SynchronizeInstalledFrom(Installation installation, Game game)
            {
                journal.Add("sync " + game.Id);
                return SyncResult;
            }

            public DefaultsAtStart ApplyDefaultsIfNeeded(Installation installation, Game game, out DisplayQuestion question)
            {
                journal.Add("defaults " + game.Id);
                question = Question;
                return DefaultsResult;
            }
        }

        /// <summary>Writes "log &lt;level&gt;: &lt;message&gt;" into the journal and keeps the entries.</summary>
        private sealed class JournalLogger : ILogger
        {
            private readonly List<string> journal;

            public JournalLogger(List<string> journal, RecordingLogger inner)
            {
                this.journal = journal;
                Inner = inner;
            }

            public RecordingLogger Inner { get; }

            public void Log(LogLevel level, string message, Exception exception = null)
            {
                journal.Add("log " + level + ": " + message);
                Inner.Log(level, message, exception);
            }
        }

        private static Installation NeoEE(InstallationKind kind = InstallationKind.Community, string aocFolder = AocFolder)
        {
            return new Installation(Product.NeoEE, Root, EeFolder, aocFolder, kind, InstallMode.Admin,
                new[] { InstallationSource.RegistryRecord });
        }

        [SetUp]
        public void SetUp()
        {
            journal = new List<string>();
            mutexes = new FakeMutexProbe { OnProbe = journal.Add };
            processes = new FakeProcessList();
            fileSystem = new InMemoryFileSystem { OnFileExists = journal.Add };
            fileSystem.AddFile(EeProgram, "program");
            fileSystem.AddFile(AocProgram, "program");
            preparation = new JournalPreparation(journal);
            starter = new FakeProcessStarter { OnCall = journal.Add };
            logger = new RecordingLogger();
            gameStarter = new GameStarter(new RunningGameDetector(mutexes, processes), fileSystem, preparation, starter,
                new JournalLogger(journal, logger));
            installation = NeoEE();
        }

        [Test]
        public void TheOrder_IsSetupGameProgramSyncFirstRunStartLog()
        {
            StartResult result = gameStarter.Start(installation, Game.EmpireEarth, false);

            Assert.That(result.Outcome, Is.EqualTo(StartOutcome.Started));
            Assert.That(journal, Is.EqualTo(new[]
            {
                "mutex NeoEE_Setup", "mutex EE_Setup",
                "mutex StainlessSteelStudiosPresentsEmpireEarth", "mutex MadDocSoftwarePresentsEmpireEarthExpansion",
                "exists " + EeProgram,
                "sync EE", "defaults EE",
                "start " + EeProgram,
                "log Info: Game started: NeoEE " + Root + ", game EE, program " + EeProgram + ", pid 4242."
            }));
        }

        [Test]
        public void Contract_3_7_TheGameStartsInItsRealFolder()
        {
            gameStarter.Start(installation, Game.ArtOfConquest, false);

            Assert.That(starter.Started, Is.EqualTo(new[] { Tuple.Create(AocProgram, AocFolder) }));
        }

        [Test]
        public void Contract_3_7_AForeignInstallationStartsInTheFolderOfItsInstalledFromValues()
        {
            fileSystem.AddFile(@"D:\Games\EE\Empire Earth.exe", "program");
            var foreign = new Installation(Product.EE, @"D:\Games", @"D:\Games\EE", null, InstallationKind.Foreign,
                InstallMode.Unknown, new[] { InstallationSource.InstalledFrom });

            StartResult result = gameStarter.Start(foreign, Game.EmpireEarth, false);

            Assert.That(result.IsStarted, Is.True);
            Assert.That(starter.Started, Is.EqualTo(new[] { Tuple.Create(@"D:\Games\EE\Empire Earth.exe", @"D:\Games\EE") }));
        }

        [Test]
        public void ThePid_IsLoggedAndReturned()
        {
            starter.ProcessId = 1234;

            StartResult result = gameStarter.Start(installation, Game.ArtOfConquest, false);

            Assert.That(result.ProcessId, Is.EqualTo(1234));
            Assert.That(logger.MessagesOf(LogLevel.Info).Last(), Does.EndWith("game AoC, program " + AocProgram + ", pid 1234."));
        }

        [Test]
        public void NoProcessFromTheShell_IsLoggedAsPidUnknown()
        {
            starter.ProcessId = null;

            StartResult result = gameStarter.Start(installation, Game.EmpireEarth, false);

            Assert.That(result.IsStarted, Is.True, "a start without a process object still counts (ADR 0010 amendment)");
            Assert.That(result.ProcessId, Is.Null);
            Assert.That(logger.MessagesOf(LogLevel.Info).Last(), Does.EndWith(", pid unknown."));
        }

        [TestCase("NeoEE_Setup", "NeoEE")]
        [TestCase("EE_Setup", "EE")]
        public void Contract_4_2_ARunningSetup_RefusesTheStartBeforeAnythingElse(string mutex, string product)
        {
            mutexes.With(mutex);

            StartResult result = gameStarter.Start(installation, Game.EmpireEarth, true);

            Assert.That(result.Outcome, Is.EqualTo(StartOutcome.SetupRunning));
            Assert.That(result.RunningSetup.Id, Is.EqualTo(product));
            Assert.That(journal.Where(line => !line.StartsWith("log ", StringComparison.Ordinal)),
                Is.All.StartsWith("mutex ").And.All.EndsWith("_Setup"), "no game probe, no file, no registry, no start");
            Assert.That(starter.Started, Is.Empty);
            Assert.That(logger.Messages.Last(), Does.Contain("the " + product + " setup is running (mutex " + mutex + ")"));
        }

        [Test]
        public void TheSameGameRunning_IsRefused_WithoutAProcessHint()
        {
            mutexes.With(Game.EmpireEarth.MutexName);

            StartResult result = gameStarter.Start(installation, Game.EmpireEarth, true);

            Assert.That(result.Outcome, Is.EqualTo(StartOutcome.AlreadyRunning));
            Assert.That(result.ProcessFound, Is.False);
            Assert.That(starter.Started, Is.Empty, "never a second start, also not when the player would start anyway");
            Assert.That(journal, Has.None.StartsWith("sync"));
        }

        [Test]
        public void Forum18_AHangingGame_IsRefusedWithTheProcessHint()
        {
            mutexes.With(Game.EmpireEarth.MutexName);
            processes.With("Empire Earth.exe");

            StartResult result = gameStarter.Start(installation, Game.EmpireEarth, false);

            Assert.That(result.Outcome, Is.EqualTo(StartOutcome.AlreadyRunning));
            Assert.That(result.ProcessFound, Is.True);
            Assert.That(result.ProgramPath, Is.EqualTo(EeProgram));
            Assert.That(logger.Messages.Last(), Does.Contain("a process Empire Earth.exe exists, it may hang"));
        }

        [Test]
        public void TheOtherGameRunning_IsAWarningFirst()
        {
            mutexes.With(Game.EmpireEarth.MutexName);

            StartResult result = gameStarter.Start(installation, Game.ArtOfConquest, false);

            Assert.That(result.Outcome, Is.EqualTo(StartOutcome.OtherGameRunning));
            Assert.That(result.OtherGame, Is.SameAs(Game.EmpireEarth));
            Assert.That(starter.Started, Is.Empty);
            Assert.That(journal, Has.None.StartsWith("exists").And.None.StartsWith("sync"));
        }

        [Test]
        public void TheOtherGameRunning_StartsWhenThePlayerConfirms()
        {
            mutexes.With(Game.ArtOfConquest.MutexName);

            StartResult result = gameStarter.Start(installation, Game.EmpireEarth, true);

            Assert.That(result.Outcome, Is.EqualTo(StartOutcome.Started));
            Assert.That(result.OtherGame, Is.SameAs(Game.ArtOfConquest));
            Assert.That(starter.Started.Single().Item1, Is.EqualTo(EeProgram));
            Assert.That(logger.Messages.Last(), Does.Contain("EE-AOC.exe is running, the player started anyway"));
        }

        [Test]
        public void Rev11_AMissingProgram_IsDamagedWithTheRepairAdviceAndTheFixedPage()
        {
            fileSystem.DeleteFile(AocProgram);

            StartResult result = gameStarter.Start(installation, Game.ArtOfConquest, false);

            Assert.That(result.Outcome, Is.EqualTo(StartOutcome.Damaged));
            Assert.That(result.RepairAdvice, Is.Not.Null);
            Assert.That(result.RepairAdvice.DownloadUrl, Is.EqualTo("https://empireearth.eu/download"));
            Assert.That(result.RepairAdvice.MissingPrograms, Is.EqualTo(new[] { Game.ArtOfConquest }));
            Assert.That(result.RepairAdvice.Steps.First(), Is.EqualTo(RepairStep.AddAntivirusException));
            Assert.That(result.RepairAdvice.Steps, Does.Contain(RepairStep.KeepCdKeysTask));
            Assert.That(starter.Started, Is.Empty);
            Assert.That(starter.OpenedUrls, Is.Empty, "the page opens only when the player asks for it");
            Assert.That(journal, Has.None.StartsWith("sync"), "nothing is written for a damaged installation");
            Assert.That(logger.MessagesOf(LogLevel.Warning).Single(), Does.Contain(AocProgram + " is missing"));
        }

        [Test]
        public void AChosenFolderThatIsGone_IsRefused()
        {
            installation.State = InstallationState.FolderMissing;

            StartResult result = gameStarter.Start(installation, Game.EmpireEarth, false);

            Assert.That(result.Outcome, Is.EqualTo(StartOutcome.FolderMissing));
            Assert.That(result.RepairAdvice, Is.Null);
            Assert.That(starter.Started, Is.Empty);
        }

        private static IEnumerable<TestCaseData> StartErrors()
        {
            yield return new TestCaseData(new Win32Exception(1223), StartOutcome.ElevationCancelled, 1223)
                .SetName("StartError_1223_UacCancelled_IsElevationCancelled");
            yield return new TestCaseData(new Win32Exception(2), StartOutcome.Damaged, 2)
                .SetName("StartError_2_FileNotFound_IsDamaged");
            yield return new TestCaseData(new Win32Exception(3), StartOutcome.Damaged, 3)
                .SetName("StartError_3_PathNotFound_IsDamaged");
            yield return new TestCaseData(new FileNotFoundException("gone", EeProgram), StartOutcome.Damaged, 2)
                .SetName("StartError_FileNotFoundException_IsDamaged");
            yield return new TestCaseData(new Win32Exception(5), StartOutcome.AccessDenied, 5)
                .SetName("StartError_5_AccessDenied_IsAccessDenied");
            yield return new TestCaseData(new Win32Exception(1260), StartOutcome.AccessDenied, 1260)
                .SetName("StartError_1260_BlockedByPolicy_IsAccessDenied");
            yield return new TestCaseData(new Win32Exception(225), StartOutcome.BlockedByAntivirus, 225)
                .SetName("StartError_225_Virus_IsBlockedByAntivirus");
            yield return new TestCaseData(new Win32Exception(740), StartOutcome.Failed, 740)
                .SetName("StartError_740_ElevationRequired_IsFailed");
            yield return new TestCaseData(new InvalidOperationException("no file name"), StartOutcome.Failed, 0)
                .SetName("StartError_InvalidOperation_IsFailed");
        }

        [TestCaseSource(nameof(StartErrors))]
        public void StartErrors_AreResultsNotExceptions(Exception error, StartOutcome outcome, int code)
        {
            starter.StartException = error;

            StartResult result = null;
            Assert.That(() => result = gameStarter.Start(installation, Game.EmpireEarth, false), Throws.Nothing);

            Assert.That(result.Outcome, Is.EqualTo(outcome));
            Assert.That(result.ErrorCode, Is.EqualTo(code));
            Assert.That(result.ErrorMessage, Is.EqualTo(error.Message));
            Assert.That(result.IsStarted, Is.False);
            Assert.That(result.ProcessId, Is.Null);
            Assert.That(result.RepairAdvice != null,
                Is.EqualTo(outcome == StartOutcome.Damaged || outcome == StartOutcome.BlockedByAntivirus));
            Assert.That(logger.Messages, Has.None.StartsWith("Info: Game started"));
        }

        [Test]
        public void ACancelledElevation_IsInformationNotAnError()
        {
            starter.StartException = new Win32Exception(1223);

            gameStarter.Start(installation, Game.EmpireEarth, false);

            Assert.That(logger.MessagesOf(LogLevel.Error), Is.Empty);
            Assert.That(logger.MessagesOf(LogLevel.Info).Last(), Does.Contain("elevation prompt was not confirmed (error 1223)"));
        }

        [Test]
        public void ABlockedSync_DoesNotStopTheStart()
        {
            preparation.SyncResult = new GameSettingsResult(GameSettingsOutcome.Blocked, null, null, null, null);
            preparation.DefaultsResult = DefaultsAtStart.Blocked;

            StartResult result = gameStarter.Start(installation, Game.EmpireEarth, false);

            Assert.That(result.IsStarted, Is.True);
            Assert.That(result.InstalledFrom, Is.SameAs(preparation.SyncResult));
            Assert.That(result.Defaults, Is.EqualTo(DefaultsAtStart.Blocked));
            Assert.That(logger.MessagesOf(LogLevel.Warning).Single(), Does.Contain("without synchronized \"Installed From\" values"));
        }

        [Test]
        public void TheDisplayQuestionOfTheFirstRun_IsReturnedWithoutBlockingTheStart()
        {
            preparation.DefaultsResult = DefaultsAtStart.FirstRunWithQuestion;
            preparation.Question = new DisplayQuestion(new DisplayQuestionItem[0]);

            StartResult result = gameStarter.Start(installation, Game.EmpireEarth, false);

            Assert.That(result.IsStarted, Is.True);
            Assert.That(result.Defaults, Is.EqualTo(DefaultsAtStart.FirstRunWithQuestion));
            Assert.That(result.Question, Is.SameAs(preparation.Question));
        }

        [Test]
        public void ArtOfConquest_OfAnInstallationWithoutIt_IsAProgrammingError()
        {
            Installation withoutAoc = NeoEE(aocFolder: null);

            Assert.That(() => gameStarter.Start(withoutAoc, Game.ArtOfConquest, false), Throws.ArgumentException);
            Assert.That(() => gameStarter.StartAsync(withoutAoc, Game.ArtOfConquest, false), Throws.ArgumentException);
        }

        [Test]
        public async Task StartAsync_StartsOnTheThreadPool()
        {
            StartResult result = await gameStarter.StartAsync(installation, Game.EmpireEarth, false);

            Assert.That(result.IsStarted, Is.True);
            Assert.That(starter.Started.Single().Item1, Is.EqualTo(EeProgram));
        }

        // --- With the real game settings (GameDefaultsService) --------------------------------------------------------

        [Test]
        public void Contract_3_6_ClassSAndTheFirstRunAreWrittenBeforeTheStart()
        {
            var w = new GameSettingsWorld();
            w.AddAdminInstallationOfAnotherAccount(GameSettingsWorld.NeoRoot, Product.NeoEE);
            Installation selected = w.Discover().Selected;
            var shell = new FakeProcessStarter();
            RegistryLocation key = GameSettingsWorld.Settings(Product.NeoEE, Game.ArtOfConquest);
            RegistryValue directoryAtStart = null;
            shell.OnCall = call => directoryAtStart = w.Get(key, "Installed From Directory");
            var real = new GameStarter(new RunningGameDetector(w.Mutexes, new FakeProcessList()), w.FileSystem,
                w.CreateDefaultsService(), shell, w.Logger);

            StartResult result = real.Start(selected, Game.ArtOfConquest, false);

            Assert.That(result.IsStarted, Is.True);
            Assert.That(directoryAtStart, Is.Not.Null, "class S is written before the start");
            Assert.That(directoryAtStart.StringValue, Is.EqualTo(@"\PROGRAM FILES (X86)\NEO EMPIRE EARTH\Empire Earth - The Art of Conquest\"));
            Assert.That(result.InstalledFrom.IsDone, Is.True);
            Assert.That(result.Defaults, Is.EqualTo(DefaultsAtStart.FirstRun));
            Assert.That(w.Get(GameSettingsWorld.Marker(Product.NeoEE), "AoC").DWordValue, Is.EqualTo(1));
        }

        [Test]
        public void StartedAnywayWhileTheOtherGameRuns_TheGuardKeepsTheSettingsAndTheGameStarts()
        {
            var w = new GameSettingsWorld();
            w.AddAdminInstallationOfAnotherAccount(GameSettingsWorld.NeoRoot, Product.NeoEE);
            Installation selected = w.Discover().Selected;
            w.Mutexes.With(Game.EmpireEarth.MutexName);
            var shell = new FakeProcessStarter();
            var real = new GameStarter(new RunningGameDetector(w.Mutexes, new FakeProcessList()), w.FileSystem,
                w.CreateDefaultsService(), shell, w.Logger);

            StartResult result = real.Start(selected, Game.ArtOfConquest, true);

            Assert.That(result.IsStarted, Is.True);
            Assert.That(result.InstalledFrom.Outcome, Is.EqualTo(GameSettingsOutcome.Blocked));
            Assert.That(result.Defaults, Is.EqualTo(DefaultsAtStart.Blocked));
            Assert.That(w.Get(GameSettingsWorld.Settings(Product.NeoEE, Game.ArtOfConquest), "Installed From Directory"), Is.Null,
                "Empire Earth may write the shared settings on exit (ADR 0016)");
        }
    }
}
