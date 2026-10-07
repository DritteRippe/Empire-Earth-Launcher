using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Lobby;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_WON;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Lobby
{
    /// <summary>
    /// <see cref="PlayerListPoller"/> (ADR 0004): the behaviour of the old worker thread loop of the Play page with
    /// its review fixes (korr-S3/S5, wart-S5/S6), with a scripted source and a delay of one millisecond. No network.
    /// </summary>
    [TestFixture]
    public class PlayerListPollerTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

        private RecordingLogger logger;
        private ConcurrentQueue<PlayerListUpdate> updates;
        private ConcurrentQueue<PlayerListAvailabilityEventArgs> availability;
        private ConcurrentQueue<TimeSpan> delays;

        /// <summary>A step of the script that throws instead of answering (a bug in the request).</summary>
        private sealed class Throw
        {
        }

        /// <summary>Answers with the steps of the script in order, then repeats the last one.</summary>
        private sealed class ScriptedSource : IPlayerListSource
        {
            private readonly object[] script;
            private int requests;

            public ScriptedSource(params object[] script)
            {
                this.script = script;
            }

            public int Requests
            {
                get { return Volatile.Read(ref requests); }
            }

            /// <summary>Set while a request runs; the request waits for <see cref="Release"/> if <see cref="Block"/> is set.</summary>
            public ManualResetEventSlim Entered { get; } = new ManualResetEventSlim();

            public ManualResetEventSlim Release { get; } = new ManualResetEventSlim(true);

            public string Endpoint
            {
                get { return "titan.example:10005"; }
            }

            public bool TryGetConnectedPlayers(out NeoApiClient.ConnectedPlayersMessage message, out Exception error)
            {
                int index = Interlocked.Increment(ref requests) - 1;
                Entered.Set();
                Release.Wait();
                object step = script[Math.Min(index, script.Length - 1)];
                if (step is Throw)
                    throw new InvalidOperationException("bug in request " + index);
                message = step as NeoApiClient.ConnectedPlayersMessage;
                error = step as Exception;
                return message != null;
            }
        }

        private static NeoApiClient.ConnectedPlayersMessage Players(int count)
        {
            var fields = new List<string> { count.ToString() };
            for (int i = 0; i < count; i++)
                fields.AddRange(new[] { "Player" + i, (i + 1).ToString(), "0" });
            return NeoApiClient.ConnectedPlayersMessage.Parse(fields.ToArray());
        }

        [SetUp]
        public void SetUp()
        {
            logger = new RecordingLogger();
            updates = new ConcurrentQueue<PlayerListUpdate>();
            availability = new ConcurrentQueue<PlayerListAvailabilityEventArgs>();
            delays = new ConcurrentQueue<TimeSpan>();
        }

        private PlayerListPoller Create(IPlayerListSource source, Func<TimeSpan, CancellationToken, Task> delay = null)
        {
            var poller = new PlayerListPoller(source, Interval, logger, delay ?? ((time, token) =>
            {
                delays.Enqueue(time);
                return Task.Delay(1, token);
            }));
            poller.Updated += (sender, e) => updates.Enqueue(e);
            poller.AvailabilityChanged += (sender, e) => availability.Enqueue(e);
            return poller;
        }

        private void WaitForUpdates(int count)
        {
            Assert.That(SpinWait.SpinUntil(() => updates.Count >= count, Timeout), Is.True, "timed out waiting for " + count + " updates");
        }

        private static void WaitForEnd(PlayerListPoller poller)
        {
            Assert.That(poller.Completion.Wait(Timeout), Is.True, "the polling did not end");
            Assert.That(poller.Completion.Status, Is.EqualTo(TaskStatus.RanToCompletion));
        }

        [Test]
        public void WartS5_NoRequestBeforeStart()
        {
            var source = new ScriptedSource(Players(1));
            using (PlayerListPoller poller = Create(source))
            {
                Thread.Sleep(50);

                Assert.That(source.Requests, Is.Zero);
                Assert.That(poller.Completion, Is.Null);
                Assert.That(updates, Is.Empty);
            }
        }

        [Test]
        public void TheFirstRequest_IsSentAtOnce_AndTheListIsReported()
        {
            var source = new ScriptedSource(Players(2));
            using (PlayerListPoller poller = Create(source))
            {
                poller.Start();
                WaitForUpdates(1);
            }

            PlayerListUpdate first = updates.First();
            Assert.That(first.Status, Is.EqualTo(PlayerListStatus.Available));
            Assert.That(first.Message.OnlinePlayers, Is.EqualTo(2));
            Assert.That(availability.Select(a => a.IsAvailable).First(), Is.True);
            Assert.That(logger.Entries, Is.Empty, "a list that is there from the start is not logged");
        }

        [Test]
        public void KorrS3_OneLogLinePerOutage_AndOneWhenTheListIsBack()
        {
            var down = new SocketException(10060);
            var source = new ScriptedSource(Players(1), down, new TimeoutException(), new Throw(), Players(1), Players(3),
                new SocketException(11001), Players(1));
            using (PlayerListPoller poller = Create(source))
            {
                poller.Start();
                WaitForUpdates(8);
                poller.Dispose();
                WaitForEnd(poller);
            }

            Assert.That(updates.Take(8).Select(u => u.Status), Is.EqualTo(new[]
            {
                PlayerListStatus.Available, PlayerListStatus.Unavailable, PlayerListStatus.Unavailable,
                PlayerListStatus.Unavailable, PlayerListStatus.Available, PlayerListStatus.Available,
                PlayerListStatus.Unavailable, PlayerListStatus.Available
            }));
            Assert.That(logger.Messages, Is.EqualTo(new[]
            {
                "Error: The online player list of titan.example:10005 is unavailable, retrying every 30000 ms.",
                "Info: The online player list is available again.",
                "Error: The online player list of titan.example:10005 is unavailable, retrying every 30000 ms.",
                "Info: The online player list is available again."
            }));
            Assert.That(logger.Entries[0].Exception, Is.SameAs(down), "the error that started the outage");
            Assert.That(availability.Select(a => a.IsAvailable), Is.EqualTo(new[] { true, false, true, false, true }));
        }

        [Test]
        public void TheOutage_IsAnEventForTheOutageHint()
        {
            var down = new SocketException(10061);
            using (PlayerListPoller poller = Create(new ScriptedSource(down)))
            {
                poller.Start();
                WaitForUpdates(3);
            }

            PlayerListAvailabilityEventArgs outage = availability.Single();
            Assert.That(outage.IsAvailable, Is.False);
            Assert.That(outage.Error, Is.SameAs(down));
            Assert.That(logger.Entries, Has.Count.EqualTo(1));
        }

        [Test]
        public void WartS6_AnExceptionOfARequest_DoesNotEndThePolling()
        {
            var source = new ScriptedSource(new Throw(), Players(1));
            using (PlayerListPoller poller = Create(source))
            {
                poller.Start();
                WaitForUpdates(2);

                Assert.That(poller.Completion.IsCompleted, Is.False);
            }

            Assert.That(updates.First().Status, Is.EqualTo(PlayerListStatus.Unavailable));
            Assert.That(updates.First().Error, Is.TypeOf<InvalidOperationException>());
            Assert.That(updates.ElementAt(1).Status, Is.EqualTo(PlayerListStatus.Available));
        }

        [Test]
        public void EveryWait_IsTheInterval()
        {
            using (PlayerListPoller poller = Create(new ScriptedSource(Players(0))))
            {
                poller.Start();
                WaitForUpdates(3);
            }

            Assert.That(delays.Take(2), Is.All.EqualTo(Interval));
        }

        [Test]
        public void Dispose_EndsThePolling()
        {
            var source = new ScriptedSource(Players(1));
            PlayerListPoller poller = Create(source);
            poller.Start();
            WaitForUpdates(1);

            poller.Dispose();

            WaitForEnd(poller);
            int requests = source.Requests;
            Thread.Sleep(50);
            Assert.That(source.Requests, Is.EqualTo(requests), "no request after the end");
        }

        [Test]
        public void Cancel_EndsThePolling()
        {
            var source = new ScriptedSource(Players(1));
            using (var cancel = new CancellationTokenSource())
            using (PlayerListPoller poller = Create(source, (time, token) => Task.Delay(Timeout, token)))
            {
                poller.Start(cancel.Token);
                WaitForUpdates(1);

                cancel.Cancel();

                WaitForEnd(poller);
            }
        }

        [Test]
        public void KorrS5_ARequestStillRunningAtDispose_IsNotReported()
        {
            var source = new ScriptedSource(Players(1));
            source.Release.Reset();
            PlayerListPoller poller = Create(source);
            poller.Start();
            Assert.That(source.Entered.Wait(Timeout), Is.True);

            poller.Dispose();
            source.Release.Set();

            WaitForEnd(poller);
            Assert.That(updates, Is.Empty, "the page is gone; nothing is shown or logged");
            Assert.That(logger.Entries, Is.Empty);
        }

        [Test]
        public void ABugInThePolling_EndsItWithALogLineAndTheStoppedState()
        {
            var bug = new InvalidOperationException("delay failed");
            PlayerListPoller poller = Create(new ScriptedSource(Players(1)), (time, token) => throw bug);
            poller.Start();

            WaitForEnd(poller);

            Assert.That(updates.Select(u => u.Status), Is.EqualTo(new[] { PlayerListStatus.Available, PlayerListStatus.Stopped }));
            Assert.That(updates.Last().Error, Is.SameAs(bug));
            Assert.That(logger.MessagesOf(LogLevel.Error), Is.EqualTo(new[] { "The online player list polling stopped unexpectedly." }));
        }

        [Test]
        public void AFailingHandler_IsLogged_AndThePollingGoesOn()
        {
            using (PlayerListPoller poller = Create(new ScriptedSource(Players(1))))
            {
                poller.Updated += (sender, e) => throw new InvalidOperationException("handler bug");
                poller.Start();
                WaitForUpdates(2);

                Assert.That(poller.Completion.IsCompleted, Is.False);
            }

            Assert.That(logger.MessagesOf(LogLevel.Error), Has.Some.EqualTo("A handler of the online player list failed."));
        }

        /// <summary>Counts the callbacks posted to it and runs them at once.</summary>
        private sealed class CountingContext : SynchronizationContext
        {
            public int Posted;

            public override void Post(SendOrPostCallback d, object state)
            {
                Interlocked.Increment(ref Posted);
                d(state);
            }
        }

        [Test]
        public void TheEvents_AreRaisedThroughTheContextOfStart()
        {
            var context = new CountingContext();
            SynchronizationContext previous = SynchronizationContext.Current;
            PlayerListPoller poller = Create(new ScriptedSource(Players(1)));
            SynchronizationContext.SetSynchronizationContext(context);
            try
            {
                poller.Start();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }

            WaitForUpdates(2);
            poller.Dispose();
            WaitForEnd(poller);

            Assert.That(context.Posted, Is.GreaterThanOrEqualTo(3), "the availability and every update");
        }

        [Test]
        public void Arguments_AndASecondStart_AreChecked()
        {
            var source = new ScriptedSource(Players(1));
            Assert.That(() => new PlayerListPoller(null, Interval, logger), Throws.ArgumentNullException);
            Assert.That(() => new PlayerListPoller(source, TimeSpan.Zero, logger), Throws.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(() => new PlayerListPoller(source, Interval, null), Throws.ArgumentNullException);

            using (PlayerListPoller poller = Create(source))
            {
                poller.Start();
                Assert.That(() => poller.Start(), Throws.InvalidOperationException);
            }

            PlayerListPoller disposed = Create(source);
            disposed.Dispose();
            Assert.That(() => disposed.Start(), Throws.InstanceOf<ObjectDisposedException>());
        }
    }
}
