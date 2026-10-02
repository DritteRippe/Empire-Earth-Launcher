using System.Reflection;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Platform
{
    /// <summary>
    /// <see cref="WindowsFileVersionReader"/> on real files: the test program itself (it has a version resource), a file
    /// without one and a missing file. Runs under Mono and on Windows.
    /// </summary>
    [TestFixture]
    public class WindowsFileVersionReaderTests
    {
        private readonly WindowsFileVersionReader reader = new WindowsFileVersionReader();

        [Test]
        public void AProgram_HasTheVersionOfItsResource()
        {
            string expected = typeof(WindowsFileVersionReaderTests).Assembly
                                  .GetCustomAttribute<AssemblyFileVersionAttribute>().Version;

            Assert.That(reader.GetFileVersion(typeof(WindowsFileVersionReaderTests).Assembly.Location), Is.EqualTo(expected));
        }

        [Test]
        public void AFileWithoutVersion_HasNone()
        {
            using (var directory = new TemporaryDirectory())
            {
                string file = directory.CreateFile("Empire Earth.exe", "not a program");

                Assert.That(reader.GetFileVersion(file), Is.Null);
            }
        }

        [Test]
        public void AMissingFile_HasNoVersion()
        {
            using (var directory = new TemporaryDirectory())
                Assert.That(reader.GetFileVersion(directory.Combine("EE-AOC.exe")), Is.Null);
        }

        [TestCase(2, 0, 0, 2949, ExpectedResult = "2.0.0.2949")]
        [TestCase(1, 0, 0, 2473, ExpectedResult = "1.0.0.2473")]
        [TestCase(0, 0, 0, 1, ExpectedResult = "0.0.0.1")]
        [TestCase(0, 0, 0, 0, ExpectedResult = null)]
        public string Format_GivesFourNumbersOrNone(int major, int minor, int build, int revision)
        {
            return WindowsFileVersionReader.Format(major, minor, build, revision);
        }
    }
}
