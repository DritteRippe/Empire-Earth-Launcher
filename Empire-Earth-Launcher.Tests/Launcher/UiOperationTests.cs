using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// The async event handler helper of the UI (<see cref="UiOperation"/>, ADR 0004), through its WinForms-free
    /// core <c>RunAsync</c>: the trigger is disabled while the work runs, errors are logged and reported once,
    /// cancellation is normal, a second start is ignored.
    /// </summary>
    [TestFixture]
    public class UiOperationTests
    {
        private RecordingLogger logger;
        private UiOperation operation;
        private FakeTrigger trigger;
        private List<Exception> reported;

        [SetUp]
        public void SetUp()
        {
            logger = new RecordingLogger();
            operation = new UiOperation(logger);
            trigger = new FakeTrigger();
            reported = new List<Exception>();
        }

        private Task<UiOperationOutcome> Run(Func<Task> work, FakeTrigger other = null)
        {
            FakeTrigger target = other ?? trigger;
            return operation.RunAsync(target, "playButton", enabled => target.SetEnabled(enabled), work, reported.Add);
        }

        [Test]
        public async Task Work_DisablesTheTriggerUntilItIsDone()
        {
            var release = new TaskCompletionSource<bool>();
            bool enabledDuringWork = true;

            Task<UiOperationOutcome> running = Run(async () =>
            {
                enabledDuringWork = trigger.Enabled;
                await release.Task;
            });
            Assert.That(running.IsCompleted, Is.False);
            Assert.That(trigger.Enabled, Is.False);

            release.SetResult(true);

            Assert.That(await running, Is.EqualTo(UiOperationOutcome.Completed));
            Assert.That(enabledDuringWork, Is.False);
            Assert.That(trigger.Enabled, Is.True);
            Assert.That(trigger.Changes, Is.EqualTo(new[] { false, true }));
            Assert.That(reported, Is.Empty);
            Assert.That(logger.Messages, Is.Empty);
        }

        [Test]
        public async Task Exception_IsLoggedAndReportedOnce_AndTheTriggerIsEnabledAgain()
        {
            var error = new InvalidOperationException("broken invariant");

            UiOperationOutcome outcome = await Run(async () =>
            {
                await Task.Yield();
                throw error;
            });

            Assert.That(outcome, Is.EqualTo(UiOperationOutcome.Failed));
            Assert.That(reported, Is.EqualTo(new[] { error }));
            RecordingLogger.Entry entry = logger.Entries.Single();
            Assert.That(entry.Level, Is.EqualTo(LogLevel.Error));
            Assert.That(entry.Message, Is.EqualTo("The operation of playButton failed unexpectedly."));
            Assert.That(entry.Exception, Is.SameAs(error));
            Assert.That(trigger.Enabled, Is.True);
        }

        [Test]
        public async Task ExceptionBeforeTheFirstAwait_IsHandledTheSameWay()
        {
            UiOperationOutcome outcome = await Run(() => throw new ArgumentException("synchronous"));

            Assert.That(outcome, Is.EqualTo(UiOperationOutcome.Failed));
            Assert.That(reported, Has.Count.EqualTo(1));
            Assert.That(trigger.Enabled, Is.True);
        }

        [Test]
        public async Task Cancellation_IsNormal_NothingIsReported()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();

                UiOperationOutcome outcome = await Run(() => Task.Delay(1000, cancellation.Token));

                Assert.That(outcome, Is.EqualTo(UiOperationOutcome.Canceled));
            }
            Assert.That(reported, Is.Empty);
            Assert.That(logger.MessagesOf(LogLevel.Error), Is.Empty);
            Assert.That(logger.MessagesOf(LogLevel.Info).Single(), Does.StartWith("The operation of playButton was canceled"));
            Assert.That(trigger.Enabled, Is.True);
        }

        [Test]
        public async Task SecondStartWhileRunning_IsIgnored()
        {
            var release = new TaskCompletionSource<bool>();
            int started = 0;
            Func<Task> work = () =>
            {
                started++;
                return release.Task;
            };

            Task<UiOperationOutcome> first = Run(work);
            UiOperationOutcome second = await Run(work);

            Assert.That(second, Is.EqualTo(UiOperationOutcome.Ignored));
            Assert.That(started, Is.EqualTo(1));
            Assert.That(trigger.Enabled, Is.False, "the ignored start does not enable the trigger");

            release.SetResult(true);
            Assert.That(await first, Is.EqualTo(UiOperationOutcome.Completed));
            Assert.That(await Run(work), Is.EqualTo(UiOperationOutcome.Completed), "after it finished it runs again");
            Assert.That(started, Is.EqualTo(2));
        }

        [Test]
        public async Task OtherTriggers_RunIndependently()
        {
            var release = new TaskCompletionSource<bool>();
            var other = new FakeTrigger();

            Task<UiOperationOutcome> first = Run(() => release.Task);
            UiOperationOutcome second = await Run(() => Task.CompletedTask, other);

            Assert.That(second, Is.EqualTo(UiOperationOutcome.Completed));
            Assert.That(other.Enabled, Is.True);
            Assert.That(trigger.Enabled, Is.False);
            release.SetResult(true);
            await first;
        }

        [Test]
        public async Task FailingReport_IsLogged_AndNothingEscapes()
        {
            UiOperationOutcome outcome = await operation.RunAsync(trigger, "playButton", trigger.SetEnabled,
                () => throw new InvalidOperationException("first"),
                exception => throw new InvalidOperationException("the dialog failed"));

            Assert.That(outcome, Is.EqualTo(UiOperationOutcome.Failed));
            Assert.That(logger.MessagesOf(LogLevel.Error), Has.Count.EqualTo(2));
            Assert.That(trigger.Enabled, Is.True);
        }

        [Test]
        public void NullArguments_AreProgrammingErrors()
        {
            Assert.That(() => new UiOperation(null), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => operation.RunAsync(trigger, "x", trigger.SetEnabled, null, reported.Add),
                Throws.TypeOf<ArgumentNullException>());
        }

        /// <summary>Stands in for the control that started the operation.</summary>
        private sealed class FakeTrigger
        {
            public bool Enabled { get; private set; } = true;

            /// <summary>Every value set, in order.</summary>
            public List<bool> Changes { get; } = new List<bool>();

            public void SetEnabled(bool enabled)
            {
                Enabled = enabled;
                Changes.Add(enabled);
            }
        }
    }
}
