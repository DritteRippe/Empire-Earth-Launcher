using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Core.Integrity;
using Empire_Earth_Launcher.RealMachineTests.Harness;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.RealMachineTests.SelfTest
{
    /// <summary>
    /// <c>pick-targets</c>: one file of each class of the EE folder of a synthetic manifest, by the launcher's file classes,
    /// files directly in the folder first, never a program; only paths are printed, never a hash.
    /// </summary>
    [TestFixture]
    [Category(RealMachineCategories.SelfTest)]
    public class PickTargetsTests
    {
        private const string Manifest = @"C:\Games\EE\_setupdata_EE\files.sha256";

        private static readonly string[] Paths =
        {
            "Empire Earth - The Art of Conquest/EE-AOC.exe", "Empire Earth - The Art of Conquest/Data/a.cfg", "Empire Earth/Data/a.dll",
            "Empire Earth/Data/z.dat", "Empire Earth/EE-AOC.exe", "Empire Earth/Empire Earth.exe", "Empire Earth/WONLobby.cfg",
            "Empire Earth/b.dll", "Empire Earth/help.rtf", "Empire Earth/Language.dll"
        };

        private static string ManifestText(params string[] paths)
        {
            return string.Join("\n", paths.Select((path, index) => SampleHashes.Of(index + 1) + "  " + path)) + "\n";
        }

        [Test]
        public void Choose_TakesTheFirstFileOfEachClass_DirectlyInTheFolderFirst_WithoutPrograms()
        {
            ManifestParseResult manifest = ManifestReader.Parse(ManifestText(Paths));

            var chosen = PickTargets.Choose(manifest.Entries);

            Assert.That(chosen[FileClass.Code], Is.EqualTo("Empire Earth/Language.dll"));
            Assert.That(chosen[FileClass.Data], Is.EqualTo("Empire Earth/help.rtf"));
            Assert.That(chosen[FileClass.Mutable], Is.EqualTo("Empire Earth/WONLobby.cfg"));
        }

        [Test]
        public void Choose_GivesNullForAClassWithoutAFile()
        {
            var chosen = PickTargets.Choose(ManifestReader.Parse(ManifestText("Empire Earth/Empire Earth.exe", "Empire Earth/Data/x.dat")).Entries);

            Assert.That(chosen[FileClass.Code], Is.Null);
            Assert.That(chosen[FileClass.Data], Is.EqualTo("Empire Earth/Data/x.dat"));
        }

        [Test]
        public void Run_PrintsThePathsAsJson()
        {
            var fileSystem = new InMemoryFileSystem();
            fileSystem.AddFile(Manifest, ManifestText(Paths));
            var output = new StringWriter();
            var error = new StringWriter();

            int code = PickTargets.Run(new[] { "--root", @"C:\Games\EE", "--product", "EE" }, fileSystem, output, error);

            Assert.That(code, Is.EqualTo(0), error.ToString());
            Assert.That(output.ToString().TrimEnd(), Is.EqualTo(
                "{ \"code\": \"Empire Earth/Language.dll\", \"data\": \"Empire Earth/help.rtf\", \"mutable\": \"Empire Earth/WONLobby.cfg\" }"));
        }

        [TestCase("")]
        [TestCase(@"--root|C:\Games\EE")]
        [TestCase(@"--root|Games\EE|--product|EE")]
        [TestCase(@"--root|C:\Games\EE|--product|ee")]
        [TestCase(@"--root|C:\Games\EE|--game|EE")]
        public void Run_WithWrongArguments_ShowsTheUsage(string arguments)
        {
            var error = new StringWriter();
            string[] split = arguments.Length == 0 ? new string[0] : arguments.Split('|');

            Assert.That(PickTargets.Run(split, new InMemoryFileSystem(), new StringWriter(), error), Is.EqualTo(2));
            Assert.That(error.ToString(), Does.StartWith("Usage: pick-targets --root"));
        }

        [Test]
        public void Run_WithAMissingOrInvalidManifest_FailsWithoutPrintingAHash()
        {
            var fileSystem = new InMemoryFileSystem();
            var missing = new StringWriter();
            Assert.That(PickTargets.Run(new[] { "--root", @"C:\Games\EE", "--product", "EE" }, fileSystem, new StringWriter(), missing), Is.EqualTo(1));

            fileSystem.AddFile(Manifest, ManifestText("../outside.dll"));
            var invalid = new StringWriter();
            Assert.That(PickTargets.Run(new[] { "--root", @"C:\Games\EE", "--product", "EE" }, fileSystem, new StringWriter(), invalid), Is.EqualTo(1));

            Assert.That(missing.ToString(), Does.Contain("cannot be read: NotFound"));
            Assert.That(invalid.ToString(), Does.Contain("is invalid: UnsafePath in line 1"));
            Assert.That(Regex.IsMatch(invalid.ToString(), "[0-9a-f]{64}"), Is.False);
        }
    }
}
