using System;
using System.Collections.Generic;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Properties;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;
using LauncherProgram = Empire_Earth_Launcher.Program;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// The start of a second launcher (<c>Program.ClaimSingleInstance</c>, ADR 0010): it shows the localized message and
    /// gets no handle, so <c>Main</c> ends before any window or service exists. With <see cref="FakeMutexProbe"/>.
    /// </summary>
    [TestFixture]
    public class SingleInstanceStartupTests
    {
        [Test]
        public void TheFirstLauncher_StartsWithoutMessage()
        {
            var messages = new List<string>();

            using (IDisposable handle = LauncherProgram.ClaimSingleInstance(new FakeMutexProbe(), new RecordingLogger(), messages.Add))
                Assert.That(handle, Is.Not.Null);

            Assert.That(messages, Is.Empty);
        }

        [Test]
        public void ASecondLauncher_ShowsTheMessage_AndEnds()
        {
            var mutexes = new FakeMutexProbe().With(SingleInstance.MutexName);
            var messages = new List<string>();

            IDisposable handle = LauncherProgram.ClaimSingleInstance(mutexes, new RecordingLogger(), messages.Add);

            Assert.That(handle, Is.Null, "Main returns without a window");
            Assert.That(messages, Is.EqualTo(new[] { Resources.LauncherAlreadyRunning }));
            Assert.That(mutexes.Exists(SingleInstance.MutexName), Is.True, "the first launcher keeps its mutex");
        }
    }
}
