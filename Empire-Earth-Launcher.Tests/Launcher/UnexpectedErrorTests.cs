using System;
using System.Linq;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Settings;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// What happens to errors nobody expected: the message of the error dialog (<see cref="UnexpectedError"/>) and
    /// the logging of failed tasks nobody awaited (<c>Program.LogUnobservedTaskException</c>, ADR 0004).
    /// </summary>
    [TestFixture]
    [SetUICulture("en-US")]
    public class UnexpectedErrorTests
    {
        [Test]
        public void Message_NamesTheErrorAndTheLogFile()
        {
            string message = UnexpectedError.FormatMessage(new InvalidOperationException("broken invariant"), false);

            Assert.That(message, Does.StartWith("An unexpected error occurred. The launcher will try to continue."));
            Assert.That(message, Does.Contain("broken invariant"));
            Assert.That(message, Does.EndWith("Details have been written to " + LauncherPaths.LogFile + "."));
        }

        [Test]
        public void Message_WhenTheLauncherHasToClose_AndWithoutException()
        {
            string message = UnexpectedError.FormatMessage(null, true);

            Assert.That(message, Does.StartWith("An unexpected error occurred and the launcher has to close."));
            Assert.That(message, Does.Contain("Unknown error."));
        }

        [Test]
        public void UnobservedTaskException_IsLoggedAndMarkedObserved()
        {
            var error = new AggregateException(new InvalidOperationException("lost in a background task"));
            var arguments = new UnobservedTaskExceptionEventArgs(error);
            var logger = new RecordingLogger();

            global::Empire_Earth_Launcher.Program.LogUnobservedTaskException(logger, arguments);

            RecordingLogger.Entry entry = logger.Entries.Single();
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(entry.Exception, Is.SameAs(error));
            Assert.That(arguments.Observed, Is.True);
        }
    }
}
