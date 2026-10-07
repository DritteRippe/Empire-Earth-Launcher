using System;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Platform
{
    /// <summary><see cref="WindowsProcessList"/>: the process name of a program and a lookup of a name nobody uses.</summary>
    [TestFixture]
    public class WindowsProcessListTests
    {
        [TestCase("Empire Earth.exe", ExpectedResult = "Empire Earth")]
        [TestCase("EE-AOC.EXE", ExpectedResult = "EE-AOC")]
        [TestCase("Loader", ExpectedResult = "Loader")]
        public string ProcessName_IsTheFileNameWithoutExe(string program)
        {
            return WindowsProcessList.ProcessName(program);
        }

        [TestCase(@"C:\Games\Empire Earth.exe")]
        [TestCase("")]
        [TestCase(null)]
        public void ProcessName_NeedsAFileName(string program)
        {
            Assert.That(() => WindowsProcessList.ProcessName(program), Throws.ArgumentException);
        }

        [Test]
        public void AProgramNobodyRuns_IsNotRunning()
        {
            var logger = new RecordingLogger();
            var list = new WindowsProcessList(logger);

            Assert.That(list.IsRunning("NoSuchProgram-" + Guid.NewGuid().ToString("N") + ".exe"), Is.False);
            Assert.That(logger.Entries, Is.Empty);
        }
    }
}
