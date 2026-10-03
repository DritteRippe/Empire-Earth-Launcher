using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Architecture
{
    /// <summary>
    /// The real-machine harness (<c>Empire-Earth-Launcher.RealMachineTests</c>, ADR 0012 amendment of the CI end-to-end test) is
    /// the one test program that touches the computer it runs on, and only on a GitHub-hosted runner. These rules keep that
    /// exception as narrow as it was designed: the rules of <see cref="TestIsolationTests"/> hold for every source of the
    /// harness, except that exactly one file, <c>Harness/RealAdapters.cs</c>, creates the real registry adapter; only
    /// <c>RealMachine/</c> and <c>Program.cs</c> use those adapters; every fixture there is explicit, in the category
    /// <c>RealMachine</c> and opens the gate first; the harness starts no program, asks no server, prints nothing to the
    /// console outside <c>Program.cs</c> and reads no hash out; neither this test program nor the launcher references it, so
    /// the <c>Tests\</c> folder of the laptop package never contains it; and the example of the README is the one its
    /// self-test reads.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.SourceTree)]
    public class RealMachineTestRulesTests
    {
        private const string Folder = "Empire-Earth-Launcher.RealMachineTests";
        private const string ProjectPath = Folder + @"\Empire-Earth-Launcher.RealMachineTests.csproj";
        private const string Adapters = Folder + "/Harness/RealAdapters.cs";
        private const string ProgramFile = Folder + "/Program.cs";
        private const string RealMachineFolder = Folder + "/RealMachine/";
        private const string SelfTestFolder = Folder + "/SelfTest/";

        /// <summary>A forbidden use, with the reason shown when a source line matches it.</summary>
        private sealed class Rule
        {
            public Rule(string reason, string pattern, params string[] allowedFiles)
            {
                Reason = reason;
                Pattern = new Regex(pattern, RegexOptions.CultureInvariant);
                AllowedFiles = allowedFiles;
            }

            public string Reason { get; }

            public Regex Pattern { get; }

            public string[] AllowedFiles { get; }
        }

        private static readonly Rule[] Rules =
        {
            new Rule("a real adapter outside RealAdapters",
                @"\bnew\s+([\w.]+\.)?(WindowsRegistry|LocalFileSystem|WindowsSystemInfo|WindowsMutexProbe|WindowsMutexOwner|WindowsProcessList|" +
                @"WindowsFileVersionReader|WindowsNetworkInfo|ShellProcessStarter)\s*\(", Adapters),
            new Rule("the real adapters outside RealMachine/ and Program.cs", @"\bRealAdapters\s*\.", Adapters),
            new Rule("a program start", @"\bProcess\s*\.\s*Start\b|\bProcessStartInfo\b|\bGameStarter\b|\bPlayModel\b|\bShellProcessStarter\b"),
            new Rule("a request to a server",
                @"\b(HttpsClient|NeoApiClient|UpdateChecker|SetupDownloadLocator|PlayerListPoller|NetworkDiagnostics|WindowsNetworkInfo)\b"),
            new Rule("the CD-key registration", @"authtools"),
            new Rule("a maintenance action of the launcher", @"\b(RegistryCleanup|WonLoginReset|SavedGames|CompatibilityOptions|FileBackup)\b"),
            new Rule("a hash read out of a finding or the manifest", @"\b(ExpectedHash|ActualHash)\b|\.\s*Hash\b"),
            new Rule("the console logger of the launcher (its lines would reach the CI report)", @"\bTraceFileLogger\b"),
            new Rule("console output outside Program.cs", @"\bConsole\s*\.", ProgramFile),
            new Rule("a token of 64 hex digits", @"(?<![0-9A-Fa-f])[0-9A-Fa-f]{64}(?![0-9A-Fa-f])"),
        };

        private static IEnumerable<string> Sources()
        {
            return Directory.EnumerateFiles(RepositoryRoot.GetFullPath(Folder), "*.cs", SearchOption.AllDirectories)
                            .Select(RepositoryRoot.ToRelativePath)
                            .Where(file => !file.Split('/').Any(part => part == "bin" || part == "obj"))
                            .OrderBy(file => file, StringComparer.Ordinal);
        }

        private static IReadOnlyList<string> Lines(string file)
        {
            return File.ReadAllLines(RepositoryRoot.GetFullPath(file));
        }

        /// <summary>The lines of <paramref name="file"/> that are not comments.</summary>
        private static IEnumerable<string> CodeLines(string file)
        {
            return Lines(file).Select(line => line.Trim()).Where(line => !line.StartsWith("//", StringComparison.Ordinal));
        }

        /// <summary>Every code line of <paramref name="file"/> that breaks a rule of this harness, as "file:line: reason: text".</summary>
        private static IEnumerable<string> Offenders(string file, IReadOnlyList<string> lines)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i].Trim();
                if (line.StartsWith("//", StringComparison.Ordinal))
                    continue;
                foreach (Rule rule in Rules.Where(rule => rule.Pattern.IsMatch(line)))
                {
                    bool allowed = rule.AllowedFiles.Contains(file) ||
                                   (rule.Reason.StartsWith("the real adapters", StringComparison.Ordinal) &&
                                    (file.StartsWith(RealMachineFolder, StringComparison.Ordinal) || file == ProgramFile));
                    if (!allowed)
                        yield return file + ":" + (i + 1) + ": " + rule.Reason + ": " + line;
                }
            }
        }

        [Test]
        public void TheHarness_KeepsTheRulesOfTheTests_ExceptTheOneRegistryAdapter()
        {
            List<string> sources = Sources().ToList();
            Assert.That(sources, Has.Count.GreaterThan(20).And.Member(Adapters), "the harness was not found");

            List<string> offenders = sources.SelectMany(file => TestIsolationTests.Offenders(file, Lines(file))).ToList();
            List<string> exempt = offenders.Where(line => line.StartsWith(Adapters + ":", StringComparison.Ordinal) &&
                                                          line.Contains(": " + TestIsolationTests.RealRegistryAdapter + ": "))
                                           .ToList();

            Assert.That(offenders.Except(exempt), Is.Empty, "the harness keeps every rule of TestIsolationTests");
            Assert.That(exempt, Has.Count.EqualTo(1), "exactly one line creates the real registry adapter");
        }

        [Test]
        public void TheHarness_StartsNothing_AsksNoServer_AndPrintsNoHash()
        {
            Assert.That(Sources().SelectMany(file => Offenders(file, Lines(file))), Is.Empty);
        }

        [Test]
        public void EveryRealMachineFixture_IsExplicit_InItsCategory_AndOpensTheGateFirst()
        {
            List<string> sources = Sources().ToList();
            List<string> fixtures = sources.Where(file => File.ReadAllText(RepositoryRoot.GetFullPath(file)).Contains("[TestFixture]")).ToList();
            List<string> realMachine = fixtures.Where(file => file.StartsWith(RealMachineFolder, StringComparison.Ordinal)).ToList();

            Assert.That(realMachine, Has.Count.GreaterThanOrEqualTo(5));
            foreach (string file in realMachine)
            {
                string text = File.ReadAllText(RepositoryRoot.GetFullPath(file));
                Assert.That(text, Does.Contain("[Explicit(RealMachineRun.ExplicitReason)]"), file);
                Assert.That(text, Does.Contain("[Category(RealMachineCategories.RealMachine)]"), file);
                Assert.That(Regex.IsMatch(text, @"\[OneTimeSetUp\]\s+public void \w+\(\)\s+\{\s+session = RealMachineRun\.Open\(\);"), Is.True,
                    file + ": the one-time set-up opens the gate first");
            }
            foreach (string file in fixtures.Except(realMachine))
            {
                string text = File.ReadAllText(RepositoryRoot.GetFullPath(file));
                Assert.That(file, Does.StartWith(SelfTestFolder));
                Assert.That(text, Does.Contain("[Category(RealMachineCategories.SelfTest)]"), file);
            }
            Assert.That(sources.Where(file => !file.StartsWith(RealMachineFolder, StringComparison.Ordinal))
                               .Where(file => CodeLines(file).Any(line =>
                                   Regex.IsMatch(line, @"\[Explicit|Category\(RealMachineCategories\.RealMachine\)|RealMachineRun\.Open"))),
                Is.Empty, "the category and the gate belong to RealMachine/ only");
        }

        [Test]
        public void OnlyTheCoreIsReferenced_AndNothingReferencesTheHarness()
        {
            ProjectFile harness = ProjectFile.Load(ProjectPath);

            Assert.That(harness.Items("ProjectReference"), Is.EqualTo(new[] { @"..\Empire-Earth-Launcher-Core\Empire-Earth-Launcher-Core.csproj" }));
            Assert.That(harness.Items("Compile").Where(item => item.StartsWith(@"..\", StringComparison.Ordinal)),
                Is.All.Matches<string>(item => item == @"..\SharedAssemblyInfo.cs" ||
                                               Regex.IsMatch(item, @"^\.\.\\Empire-Earth-Launcher\.Tests\\(Fakes|TestSupport)\\\w+\.cs$")),
                "only the fakes of the unit tests are compiled in");
            foreach (string project in ProjectConventionsTests.SolutionProjects().Where(path => path != ProjectPath))
                Assert.That(ProjectFile.Load(project).Items("ProjectReference"), Has.None.Contains("RealMachineTests"), project);
        }

        [Test]
        public void TheReadmeExample_IsTheOneTheSelfTestReads()
        {
            string readme = File.ReadAllText(RepositoryRoot.GetFullPath("README.md")).Replace("\r\n", "\n");
            Match block = Regex.Match(readme, @"\*\*Real machine\*\*.*?```json\n(?<json>.*?)\n```", RegexOptions.Singleline);
            string selfTest = File.ReadAllText(RepositoryRoot.GetFullPath(SelfTestFolder + "ExpectationFileTests.cs")).Replace("\r\n", "\n");
            Match constant = Regex.Match(selfTest, "ReadmeExample = @\"(?<json>(?:[^\"]|\"\")*)\";");

            Assert.That(block.Success, Is.True, "README, Tests, \"Real machine\" has a json block");
            Assert.That(constant.Success, Is.True, "ExpectationFileTests has ReadmeExample");
            Assert.That(block.Groups["json"].Value, Is.EqualTo(constant.Groups["json"].Value.Replace("\"\"", "\"")));
        }

        [TestCase("Empire-Earth-Launcher.RealMachineTests/Harness/HarnessSession.cs", "var registry = new WindowsRegistry();", "a real adapter outside")]
        [TestCase("Empire-Earth-Launcher.RealMachineTests/SelfTest/X.cs", "var files = new LocalFileSystem();", "a real adapter outside")]
        [TestCase("Empire-Earth-Launcher.RealMachineTests/Harness/X.cs", "inner = new Core.Platform.WindowsRegistry();", "a real adapter outside")]
        [TestCase("Empire-Earth-Launcher.RealMachineTests/Checks/X.cs", "IRegistry registry = RealAdapters.Create(work).Registry;", "the real adapters outside")]
        [TestCase("Empire-Earth-Launcher.RealMachineTests/RealMachine/X.cs", "Process.Start(\"Empire Earth.exe\");", "a program start")]
        [TestCase("Empire-Earth-Launcher.RealMachineTests/RealMachine/X.cs", "new GameStarter(detector, files, defaults, shell, logger);", "a program start")]
        [TestCase("Empire-Earth-Launcher.RealMachineTests/RealMachine/X.cs", "var check = new UpdateChecker(client, logger);", "a request to a server")]
        [TestCase("Empire-Earth-Launcher.RealMachineTests/RealMachine/X.cs", "LoadLibrary(\"authtools.dll\");", "the CD-key registration")]
        [TestCase("Empire-Earth-Launcher.RealMachineTests/RealMachine/X.cs", "new RegistryCleanup(registry, files, guard, backups, logger);", "a maintenance action")]
        [TestCase("Empire-Earth-Launcher.RealMachineTests/Checks/X.cs", "line += finding.ExpectedHash;", "a hash read out")]
        [TestCase("Empire-Earth-Launcher.RealMachineTests/Harness/X.cs", "Write(entry.Hash);", "a hash read out")]
        [TestCase("Empire-Earth-Launcher.RealMachineTests/Harness/X.cs", "var logger = new TraceFileLogger(path);", "the console logger")]
        [TestCase("Empire-Earth-Launcher.RealMachineTests/Checks/X.cs", "Console.WriteLine(report);", "console output outside Program.cs")]
        public void TheRules_FindEachForbiddenUse(string file, string line, string reason)
        {
            Assert.That(Offenders(file, new[] { line }).Single(), Does.Contain(": " + reason));
        }

        [Test]
        public void TheRules_FindATokenOf64HexDigits()
        {
            string token = new string('a', 32) + new string('F', 32);

            Assert.That(Offenders(Folder + "/Checks/X.cs", new[] { "const string Hash = \"" + token + "\";" }).Single(),
                Does.Contain(": a token of 64 hex digits: "));
            Assert.That(Offenders(Folder + "/Checks/X.cs", new[] { "\"" + token.Substring(1) + "\"", "\"" + token + "0\"" }), Is.Empty);
        }

        [TestCase(Adapters, "return new RealAdapters(new WindowsRegistry(), fileSystem, new WindowsSystemInfo(logger), probe, logger);")]
        [TestCase("Empire-Earth-Launcher.RealMachineTests/RealMachine/RealMachineRun.cs", "RealAdapters adapters = RealAdapters.Create(workFolder);")]
        [TestCase(ProgramFile, "return PickTargets.Run(args, RealAdapters.CreateReadOnlyFileSystem(violations), Console.Out, Console.Error);")]
        [TestCase("Empire-Earth-Launcher.RealMachineTests/SelfTest/X.cs", "var world = new InstallationWorld();")]
        [TestCase("Empire-Earth-Launcher.RealMachineTests/Checks/X.cs", "string text = SampleHashes.Of(1) + finding.Path;")]
        [TestCase("Empire-Earth-Launcher.RealMachineTests/Checks/X.cs", "// Console.WriteLine is never used here")]
        public void TheRules_AllowWhatTheHarnessNeeds(string file, string line)
        {
            Assert.That(Offenders(file, new[] { line }), Is.Empty);
        }
    }
}
