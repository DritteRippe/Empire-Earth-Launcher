using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Architecture
{
    /// <summary>
    /// The rules of launcher 1.0.0 for the suite installer (contract revision 4), kept in the sources: the command line reaches
    /// <c>LauncherArguments</c>, the suite record is only read, the hand-over to a running launcher starts no program, nothing of
    /// it touches the CD keys, and the names agree with <c>docs/CONTRACT.md</c>.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.SourceTree)]
    public class SuiteRulesTests
    {
        private const string Core = "Empire-Earth-Launcher-Core/";
        private const string Ui = "Empire Earth Launcher/";

        private static readonly string[] SuiteSources =
        {
            Core + "Installations/SuiteRecordReader.cs", Core + "Repair/SuiteRepairLocator.cs", Core + "Play/InstanceForwarding.cs",
            Core + "Play/LauncherArguments.cs", Core + "Platform/WindowsInstanceChannel.cs", Core + "Contract/SetupKind.cs",
            Core + "Lobby/PlayerListPolling.cs", Ui + "InstanceMessageWindow.cs", Ui + "LauncherInstanceTarget.cs", Ui + "ForegroundWindow.cs"
        };

        private static readonly string[] WritingCalls = { ".SetValue(", ".CreateSubKey(", ".DeleteValue(", ".DeleteSubKeyTree(" };

        private static string Read(string relativePath)
        {
            return File.ReadAllText(RepositoryRoot.GetFullPath(relativePath));
        }

        /// <summary>The code of a source without its comment lines.</summary>
        private static string Code(string relativePath)
        {
            return string.Join("\n", File.ReadAllLines(RepositoryRoot.GetFullPath(relativePath))
                .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
        }

        [Test]
        public void TheSuiteSources_Exist()
        {
            Assert.That(SuiteSources.Where(file => !File.Exists(RepositoryRoot.GetFullPath(file))), Is.Empty);
        }

        [Test]
        public void Main_TakesTheCommandLine_AndParsesIt()
        {
            string program = Code(Ui + "Program.cs");

            Assert.That(program, Does.Contain("static void Main(string[] args)"));
            Assert.That(program, Does.Contain("LauncherArguments.Parse(args"));
        }

        [Test]
        public void Contract_1_6_TheSuiteRecord_IsOnlyRead()
        {
            foreach (string file in new[] { Core + "Installations/SuiteRecordReader.cs", Core + "Repair/SuiteRepairLocator.cs" })
            {
                string code = Code(file);
                foreach (string call in WritingCalls)
                    Assert.That(code, Does.Not.Contain(call), file + " writes the registry");
            }
        }

        [Test]
        public void Contract_1_6_TheSuiteRecord_IsReadFromTheView64OfHklm()
        {
            string reader = Code(Core + "Installations/SuiteRecordReader.cs");

            Assert.That(reader, Does.Contain("RegistryHive.LocalMachine, RegistryView.Registry64, ContractNames.SuiteRecordKey"));
            Assert.That(reader, Does.Not.Contain("RegistryView.Default"), "an explicit view, contract 0 \"Registry views\"");
        }

        [Test]
        public void Contract_4_1_TheHandOver_StartsNoProgram_AndTheFolderOnlyOpensInTheExplorer()
        {
            foreach (string file in SuiteSources)
            {
                string code = Code(file);
                Assert.That(code, Does.Not.Contain("Process.Start"), file);
                Assert.That(code, Does.Not.Contain("ProcessStartInfo"), file);
                Assert.That(code, Does.Not.Contain("StartProgram("), file + " starts nothing");
            }
            Assert.That(Code(Core + "Repair/RepairAdvice.cs"), Does.Contain("starter.OpenFolder(SuiteFolder)"));
        }

        [Test]
        public void D6_NothingOfTheSuiteAdditions_TouchesTheCdKeys()
        {
            foreach (string file in SuiteSources)
            {
                string code = Code(file);
                Assert.That(code, Does.Not.Contain("CDKeys"), file);
                Assert.That(code, Does.Not.Contain("CdKeys"), file);
                Assert.That(code, Does.Not.Contain("authtools"), file);
                Assert.That(code, Does.Not.Contain("Sierra"), file);
            }
        }

        [Test]
        public void TheLauncher_NeverAsksForElevation_ForTheForwarding()
        {
            foreach (string file in SuiteSources)
                Assert.That(Code(file), Does.Not.Match(new Regex("runas", RegexOptions.IgnoreCase)), file);
        }

        [Test]
        public void TheNames_AreThoseOfTheContract()
        {
            string contract = Read("docs/CONTRACT.md");

            Assert.That(ContractNames.SuiteSetupMutexName, Is.EqualTo("EmpireEarthCommunity_Suite"));
            Assert.That(contract, Does.Contain("| Suite setup mutex (`SetupMutex`) | `" + ContractNames.SuiteSetupMutexName + "` |"));
            Assert.That(contract, Does.Contain("| Launcher mutex | `" + SingleInstance.MutexName + "` |"));
            Assert.That(contract, Does.Contain("| Suite `AppName` | `" + ContractNames.SuiteAppName + "` |"));
            Assert.That(contract, Does.Contain("| Suite record key | `" + ContractNames.SuiteRecordKey + "` |"));
            Assert.That(contract, Does.Contain("`" + ContractNames.ProductArgumentName + "=EE`"));
            Assert.That(contract, Does.Contain("`" + ContractNames.ProductArgumentName + "=NeoEE`"));
            foreach (string value in new[] { "ContractVersion", "SuiteVersion", "InstallPath", "Products", "SourceDir" })
                Assert.That(contract, Does.Contain("| `" + value + "` |"), value);
            Assert.That(ContractNames.SuiteProductsName, Is.EqualTo("Products"));
            Assert.That(ContractNames.SuiteSourceDirName, Is.EqualTo("SourceDir"));
            Assert.That(ContractNames.SuiteVersionName, Is.EqualTo("SuiteVersion"));
        }

        [Test]
        public void TheLauncherMutex_IsNoSetupMutex_AndTheSuiteMutexIsOne()
        {
            Assert.That(SetupKind.All.Select(kind => kind.MutexName), Does.Contain(ContractNames.SuiteSetupMutexName));
            Assert.That(SetupKind.All.Select(kind => kind.MutexName), Does.Not.Contain(SingleInstance.MutexName));
        }

        [Test]
        public void TheWindowName_StartsWithTheReservedLauncherName()
        {
            Assert.That(InstanceMessage.WindowName(3), Does.StartWith(SingleInstance.MutexName + "."));
        }
    }
}
