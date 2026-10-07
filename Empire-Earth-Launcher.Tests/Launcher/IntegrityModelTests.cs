using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Integrity;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Core.Repair;
using Empire_Earth_Launcher.Core.Settings;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// <see cref="IntegrityModel"/>, the integrity check of the Play and Tools pages (L-WP7, contract 2.5 and 4.2, ADR 0016
    /// plan review): the quick check in the background after every search, again after a setup, never while a setup runs,
    /// cancelled when one starts; the full check on request and its cancel; the repair advice of the report. With the fake
    /// registry, file system and mutexes; every file content is synthetic.
    /// </summary>
    [TestFixture]
    public class IntegrityModelTests
    {
        private const string SettingsFile = @"C:\Users\Player\AppData\Local\Empire Earth Launcher\settings.json";
        private const string Root = @"C:\Program Files (x86)\Neo Empire Earth";
        private const string EeProgram = Root + @"\Empire Earth\Empire Earth.exe";
        private const string EeData = Root + @"\Empire Earth\Data\file0001.dat";
        private const string LegacyRoot = @"C:\Games\Empire Earth Community";
        private const string ForeignFolder = @"D:\Retail\Empire Earth";

        private InstallationWorld world;
        private FakeMutexProbe mutexes;
        private SettingsStore settings;
        private SetupWatcher watcher;
        private InstallationService installations;
        private IntegrityModel model;
        private int changed;

        [SetUp]
        public void SetUp()
        {
            world = new InstallationWorld();
            mutexes = new FakeMutexProbe();
            settings = new SettingsStore(world.FileSystem, SettingsFile, world.Logger);
            settings.Load();
            watcher = new SetupWatcher(mutexes, world.Clock, world.Logger);
            installations = new InstallationService(world.Logger, settings, world.CreateDiscovery(), world.FileSystem, null,
                watcher);
            // The check must only read (contract 2.5): both fakes fail the test at the first change.
            var checker = new IntegrityChecker(new WriteForbiddingFileSystem(world.FileSystem),
                new WriteForbiddingRegistry(world.Registry), mutexes, world.Logger);
            model = new IntegrityModel(checker, installations, watcher, world.Logger);
            model.Changed += (sender, e) => changed++;
        }

        /// <summary>
        /// A NeoEE installation of a setup since v2 (admin) with a data file and the manifest of its files, the hashes taken
        /// from the synthetic contents.
        /// </summary>
        private void InstallNeoEE()
        {
            world.AddCommunityInstallation(Root, Product.NeoEE);
            world.FileSystem.AddFile(EeData, SampleHashes.Content(1));
            var lines = new List<string>();
            foreach (string path in new[] { "Empire Earth/Empire Earth.exe", "Empire Earth/neoee.dll",
                         "Empire Earth/Data/file0001.dat", "Empire Earth - The Art of Conquest/EE-AOC.exe" })
            {
                string hash = SampleHashes.Sha256(world.FileSystem.GetContent(WinPath.Combine(Root, path)));
                lines.Add(hash + "  " + path);
            }
            world.FileSystem.AddFile(Root + @"\_setupdata_NeoEE\files.sha256", string.Join("\n", lines) + "\n");
        }

        private async Task<IntegrityReport> SearchAndCheck()
        {
            await installations.RefreshAsync();
            Assert.That(model.LastCheck, Is.Not.Null, "every search result starts the quick check");
            return await model.LastCheck;
        }

        private void SetupStarts()
        {
            mutexes.With("NeoEE_Setup");
            world.Clock.Advance(SetupWatcher.Interval);
            watcher.Tick();
        }

        private async Task SetupEnds()
        {
            mutexes.Remove("NeoEE_Setup");
            world.Clock.Advance(SetupWatcher.Interval);
            watcher.Tick();
            await installations.RefreshAfterSetup;
        }

        [Test]
        public void BeforeTheFirstSearch_NothingIsChecked()
        {
            Assert.That(model.Report, Is.Null);
            Assert.That(model.LastCheck, Is.Null);
            Assert.That(model.CanCheck, Is.False);
            Assert.That(model.CreateRepairAdvice(), Is.Null);
        }

        [Test]
        public async Task Contract_2_5_AfterTheSearch_TheQuickCheckRunsInTheBackground()
        {
            InstallNeoEE();

            IntegrityReport report = await SearchAndCheck();

            Assert.That(report.State, Is.EqualTo(IntegrityState.Ok), report.ToString());
            Assert.That(report.Kind, Is.EqualTo(IntegrityCheckKind.Quick));
            Assert.That(report.HashedFiles, Is.EqualTo(3), "the quick check hashes the code files only");
            Assert.That(model.Report, Is.SameAs(report));
            Assert.That(model.IsChecking, Is.False);
            Assert.That(model.CanStartFullCheck, Is.True);
            Assert.That(changed, Is.GreaterThanOrEqualTo(2), "check started, check ended");
            Assert.That(world.Logger.Messages, Has.Some.Contains("Integrity: quick check of " + Root + ": Ok"));
        }

        [Test]
        public async Task TheQuickCheck_NeverDelaysTheSearch()
        {
            InstallNeoEE();
            var release = new ManualResetEventSlim();
            world.FileSystem.OnRead = path =>
            {
                if (path == EeProgram)
                    release.Wait(5000);
            };

            await installations.RefreshAsync();

            Assert.That(installations.Selected, Is.Not.Null, "the search has finished");
            Assert.That(model.IsChecking, Is.True);
            Assert.That(model.LastCheck.IsCompleted, Is.False, "the check runs on the thread pool and nobody waits for it");
            release.Set();
            Assert.That((await model.LastCheck).State, Is.EqualTo(IntegrityState.Ok));
        }

        [Test]
        public async Task Contract_2_5_ADeletedDataFile_IsIncomplete_AndTheAdviceNamesItWithTheAntivirusExceptionFirst()
        {
            InstallNeoEE();
            await installations.RefreshAsync();
            await model.LastCheck;
            world.FileSystem.DeleteFile(EeData);
            await installations.RefreshAsync();

            IntegrityReport report = await model.LastCheck;

            Assert.That(report.State, Is.EqualTo(IntegrityState.Incomplete), report.ToString());
            RepairAdvice advice = model.CreateRepairAdvice();
            Assert.That(advice.Reason, Is.EqualTo(RepairReason.IntegrityFindings));
            Assert.That(advice.Files.Select(file => file.Path), Is.EqualTo(new[] { "Empire Earth/Data/file0001.dat" }));
            Assert.That(advice.Steps.First(), Is.EqualTo(RepairStep.AddAntivirusException));
            Assert.That(advice.DownloadUrl, Is.EqualTo(SetupDownloadPage.For(advice.Installation)), "the page of the product, no request");
        }

        [Test]
        public async Task Contract_2_5_AnOlderSetup_IsUnknown_OnlyAsABadge_WithoutTheRepairOfTheReport()
        {
            world.AddLegacyInstallation(LegacyRoot, Product.EE);

            IntegrityReport report = await SearchAndCheck();

            Assert.That(report.State, Is.EqualTo(IntegrityState.Unknown));
            Assert.That(report.UnknownReason, Is.EqualTo(UnknownReason.LegacySetup));
            Assert.That(report.OffersRepair, Is.False, "legacy: only the badge says to run the current setup");
            Assert.That(model.CreateRepairAdvice().Reason, Is.EqualTo(RepairReason.Requested),
                "the Tools page still gives the advice on request");
        }

        [Test]
        public async Task Contract_2_5_AForeignInstallation_IsNotChecked()
        {
            world.AddForeignInstallation(ForeignFolder);

            IntegrityReport report = await SearchAndCheck();

            Assert.That(report.State, Is.EqualTo(IntegrityState.NotChecked));
            Assert.That(model.CanCheck, Is.False, "no full check of a foreign installation");
            Assert.That(model.CanStartFullCheck, Is.False);
        }

        [Test]
        public async Task Contract_2_5_TheFullCheck_OnRequest_HashesTheDataFiles()
        {
            InstallNeoEE();
            await SearchAndCheck();
            world.FileSystem.AddFile(EeData, SampleHashes.Content(2));

            IntegrityReport report = await model.StartFullCheckAsync();

            Assert.That(report.Kind, Is.EqualTo(IntegrityCheckKind.Full));
            Assert.That(report.State, Is.EqualTo(IntegrityState.Modified), "a changed data file is only information");
            Assert.That(report.HashedFiles, Is.EqualTo(4));
            Assert.That(model.Report, Is.SameAs(report));
            Assert.That(world.Logger.Messages, Has.Some.Contains("the full check of " + Root + " was started by the user"));
        }

        /// <summary>Runs every <see cref="SynchronizationContext.Post"/> at once, as if the UI thread were always free.</summary>
        private sealed class InlineContext : SynchronizationContext
        {
            public override void Post(SendOrPostCallback d, object state)
            {
                d(state);
            }
        }

        [Test]
        public async Task TheFullCheck_ReportsItsProgress_OncePerPercent()
        {
            InstallNeoEE();
            var lines = new List<string>(world.FileSystem.GetText(Root + @"\_setupdata_NeoEE\files.sha256").TrimEnd('\n').Split('\n'));
            for (int i = 0; i < 400; i++)
            {
                string path = "Empire Earth/Data/Sounds/file" + (1000 + i).ToString(System.Globalization.CultureInfo.InvariantCulture) + ".wav";
                world.FileSystem.AddFile(WinPath.Combine(Root, path), SampleHashes.Content(1000 + i));
                lines.Add(SampleHashes.Of(1000 + i) + "  " + path);
            }
            world.FileSystem.AddFile(Root + @"\_setupdata_NeoEE\files.sha256", string.Join("\n", lines) + "\n");
            await SearchAndCheck();
            var percents = new List<int>();
            model.Changed += (sender, e) =>
            {
                if (model.Progress != null)
                    percents.Add(model.Progress.Percent);
            };

            SynchronizationContext previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new InlineContext());
            Task<IntegrityReport> check;
            try
            {
                // The progress object captures the context of the thread that starts the check, as on the UI thread.
                check = model.StartFullCheckAsync();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
            IntegrityReport report = await check;

            Assert.That(report.State, Is.EqualTo(IntegrityState.Ok));
            Assert.That(report.ListedFiles, Is.EqualTo(404));
            Assert.That(percents, Is.Ordered.And.Unique, "one event per percent, not one per file");
            Assert.That(percents.Count, Is.InRange(90, 101));
            Assert.That(percents.Last(), Is.EqualTo(100));
        }

        [Test]
        public async Task CancelCheck_EndsTheFullCheck_WithoutFindings_AndTheFileIsClosed()
        {
            InstallNeoEE();
            await SearchAndCheck();
            world.FileSystem.DeleteFile(EeData);
            world.FileSystem.OnRead = path =>
            {
                if (path == EeProgram)
                    model.CancelCheck();
            };

            IntegrityReport report = await model.StartFullCheckAsync();

            Assert.That(report.State, Is.EqualTo(IntegrityState.Cancelled));
            Assert.That(report.CancelReason, Is.EqualTo(CancelReason.Requested));
            Assert.That(report.Findings, Is.Empty, "the findings of a cancelled check are dropped");
            Assert.That(world.FileSystem.OpenStreamCount, Is.EqualTo(0));
            Assert.That(model.IsChecking, Is.False);
        }

        [Test]
        public async Task Contract_4_2_ASetupThatStartsDuringTheCheck_CancelsIt_AndTheCheckRunsAgainAfterIt()
        {
            InstallNeoEE();
            await SearchAndCheck();
            world.FileSystem.OnRead = path =>
            {
                if (path == EeProgram)
                    SetupStarts();
            };

            IntegrityReport cancelled = await model.StartFullCheckAsync();

            Assert.That(cancelled.State, Is.EqualTo(IntegrityState.Cancelled));
            Assert.That(cancelled.CancelReason, Is.EqualTo(CancelReason.SetupRunning));
            Assert.That(cancelled.Findings, Is.Empty);
            Assert.That(world.FileSystem.OpenStreamCount, Is.EqualTo(0), "the file is closed for the setup");
            Assert.That(model.Report, Is.SameAs(cancelled), "the pages say that the check was cancelled");
            Assert.That(model.CanCheck, Is.False, "no check while the setup runs");
            Assert.That(world.Logger.Messages, Has.Some.Contains("the running full check is cancelled (contract 4.2)"));

            world.FileSystem.OnRead = null;
            world.FileSystem.DeleteFile(EeProgram);
            await SetupEnds();
            IntegrityReport again = await model.LastCheck;

            Assert.That(again, Is.Not.SameAs(cancelled), "the search after the setup starts the quick check");
            Assert.That(again.Kind, Is.EqualTo(IntegrityCheckKind.Quick));
            Assert.That(again.State, Is.EqualTo(IntegrityState.Damaged));
        }

        [Test]
        public async Task Contract_4_2_WhileASetupRuns_NoCheckStarts()
        {
            InstallNeoEE();
            SetupStarts();

            await installations.RefreshAsync();

            Assert.That(installations.IsWaitingForSetup, Is.True);
            Assert.That(model.LastCheck, Is.Null, "no search, no check: install.ini and the manifest are not read");
            Assert.That(model.CanCheck, Is.False);
            Assert.That(world.FileSystem.OpenCount(Root + @"\_setupdata_NeoEE\files.sha256"), Is.EqualTo(0));

            await SetupEnds();
            Assert.That((await model.LastCheck).State, Is.EqualTo(IntegrityState.Ok));
        }

        [Test]
        public async Task AnotherInstallation_CancelsTheRunningCheck_AndOnlyTheLatestReportCounts()
        {
            InstallNeoEE();
            world.AddLegacyInstallation(LegacyRoot, Product.EE);
            var release = new ManualResetEventSlim();
            world.FileSystem.OnRead = path =>
            {
                if (path == EeProgram)
                    release.Wait(5000);
            };
            await installations.RefreshAsync();
            Task<IntegrityReport> first = model.LastCheck;
            Assert.That(first.IsCompleted, Is.False);

            await installations.ChooseFolderAsync(LegacyRoot);
            Task<IntegrityReport> second = model.LastCheck;
            release.Set();
            IntegrityReport firstReport = await first;
            IntegrityReport secondReport = await second;

            Assert.That(firstReport.State, Is.EqualTo(IntegrityState.Cancelled));
            Assert.That(secondReport.UnknownReason, Is.EqualTo(UnknownReason.LegacySetup));
            Assert.That(model.Report, Is.SameAs(secondReport), "the report of the selected installation");
        }

        [Test]
        public async Task ACheckThatFails_IsLogged_AndThePagesNoLongerSayChecking()
        {
            InstallNeoEE();
            await SearchAndCheck();
            world.FileSystem.OnRead = path =>
            {
                if (path == EeProgram)
                    throw new InvalidOperationException("Injected bug.");
            };
            int before = changed;

            Task<IntegrityReport> check = model.StartFullCheckAsync();

            Assert.That(async () => await check, Throws.InvalidOperationException);
            Assert.That(model.IsChecking, Is.False);
            Assert.That(changed, Is.GreaterThanOrEqualTo(before + 2), "started and ended");
            // The fault is logged by a continuation on the thread pool.
            string line = "The integrity check of " + Root + " failed.";
            for (int i = 0; i < 200 && !world.Logger.Messages.Any(message => message.Contains(line)); i++)
                await Task.Delay(25);
            Assert.That(world.Logger.Messages, Has.Some.Contains(line));
        }

        [Test]
        public void StartFullCheck_WithoutAnInstallation_IsAProgrammingError()
        {
            Assert.That(() => model.StartFullCheckAsync(), Throws.InvalidOperationException);
        }
    }
}
