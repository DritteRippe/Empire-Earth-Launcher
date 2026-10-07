using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Play
{
    /// <summary><see cref="ProgramVersions"/>: the file versions of the programs for the Play page (forum report section 8 row 1).</summary>
    [TestFixture]
    public class ProgramVersionsTests
    {
        private const string EeProgram = @"C:\Games\Neo Empire Earth\Empire Earth\Empire Earth.exe";
        private const string AocProgram = @"C:\Games\Neo Empire Earth\Empire Earth - The Art of Conquest\EE-AOC.exe";

        private static Installation NeoEE(bool artOfConquest)
        {
            return new Installation(Product.NeoEE, @"C:\Games\Neo Empire Earth", @"C:\Games\Neo Empire Earth\Empire Earth",
                artOfConquest ? @"C:\Games\Neo Empire Earth\Empire Earth - The Art of Conquest" : null,
                InstallationKind.Community, InstallMode.Admin, new[] { InstallationSource.RegistryRecord });
        }

        [Test]
        public void BothPrograms_WithTheirVersions()
        {
            var fileSystem = new InMemoryFileSystem();
            fileSystem.AddFile(EeProgram, "program");
            fileSystem.AddFile(AocProgram, "program");
            var reader = new FakeFileVersionReader().With(EeProgram, "2.0.0.2949").With(AocProgram, "1.0.0.2473");

            var versions = new ProgramVersions(fileSystem, reader).Read(NeoEE(true));

            Assert.That(versions.Select(v => v.ToString()), Is.EqualTo(new[] { "Empire Earth.exe 2.0.0.2949", "EE-AOC.exe 1.0.0.2473" }));
            Assert.That(versions.Select(v => v.ProgramPath), Is.EqualTo(new[] { EeProgram, AocProgram }));
        }

        [Test]
        public void WithoutArtOfConquest_OnlyEmpireEarth()
        {
            var fileSystem = new InMemoryFileSystem();
            fileSystem.AddFile(EeProgram, "program");

            var versions = new ProgramVersions(fileSystem, new FakeFileVersionReader()).Read(NeoEE(false));

            Assert.That(versions.Select(v => v.ToString()), Is.EqualTo(new[] { "Empire Earth.exe without version" }));
        }

        [Test]
        public void AMissingProgram_HasNoVersion()
        {
            var reader = new FakeFileVersionReader().With(EeProgram, "2.0.0.2949");

            ProgramVersion version = new ProgramVersions(new InMemoryFileSystem(), reader).Read(NeoEE(false)).Single();

            Assert.That(version.Exists, Is.False);
            Assert.That(version.Version, Is.Null);
            Assert.That(version.ToString(), Is.EqualTo("Empire Earth.exe missing"));
        }
    }
}
