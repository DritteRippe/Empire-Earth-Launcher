using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Lobby;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_WON;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Lobby
{
    /// <summary>
    /// <see cref="PlayerListPolling"/>: the player list is polled only while the selected installation is NeoEE (v1.0.0). A source
    /// that counts its requests stands in for the status server; no network.
    /// </summary>
    [TestFixture]
    public class PlayerListPollingTests
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private sealed class CountingSource : IPlayerListSource
        {
            private int requests;

            public int Requests
            {
                get { return Volatile.Read(ref requests); }
            }

            public ManualResetEventSlim Requested { get; } = new ManualResetEventSlim();

            public string Endpoint
            {
                get { return "titan.example:10005"; }
            }

            public bool TryGetConnectedPlayers(out NeoApiClient.ConnectedPlayersMessage message, out Exception error)
            {
                Interlocked.Increment(ref requests);
                Requested.Set();
                message = null;
                error = new InvalidOperationException("no server in the tests");
                return false;
            }
        }

        private RecordingLogger logger;
        private List<CountingSource> sources;
        private PlayerListPolling polling;

        [SetUp]
        public void SetUp()
        {
            logger = new RecordingLogger();
            sources = new List<CountingSource>();
            polling = new PlayerListPolling(() =>
            {
                var source = new CountingSource();
                sources.Add(source);
                return new PlayerListPoller(source, TimeSpan.FromMinutes(5), logger);
            });
        }

        [TearDown]
        public void TearDown()
        {
            polling.Dispose();
        }

        private static Installation Installation(Product product, InstallationKind kind = InstallationKind.Community)
        {
            string root = @"C:\Games\" + product.Id;
            return new Installation(product, root, root + @"\Empire Earth", null, kind, InstallMode.Admin,
                new[] { InstallationSource.RegistryRecord });
        }

        [Test]
        public void ShouldPoll_OnlyForNeoEE()
        {
            Assert.That(PlayerListPolling.ShouldPoll(Installation(Product.NeoEE)), Is.True);
            Assert.That(PlayerListPolling.ShouldPoll(Installation(Product.EE)), Is.False);
            Assert.That(PlayerListPolling.ShouldPoll(Installation(Product.EE, InstallationKind.Foreign)), Is.False, "retail, GOG");
            Assert.That(PlayerListPolling.ShouldPoll(null), Is.False, "no installation: no request");
        }

        [Test]
        public void EE_SendsNoRequest_AndCreatesNoPoller()
        {
            Assert.That(polling.Apply(Installation(Product.EE)), Is.False);
            Assert.That(polling.Apply(null), Is.False);

            Assert.That(polling.IsPolling, Is.False);
            Assert.That(sources, Is.Empty, "the poller is not even created");
        }

        [Test]
        public void NeoEE_StartsThePolling_AndReportsItsResults()
        {
            var updates = new BlockingCollection<PlayerListUpdate>();
            polling.Updated += (sender, e) => updates.Add(e);

            bool running = polling.Apply(Installation(Product.NeoEE));

            Assert.That(running, Is.True);
            Assert.That(polling.IsPolling, Is.True);
            Assert.That(updates.TryTake(out PlayerListUpdate update, Timeout), Is.True, "the first result comes at once");
            Assert.That(update.Status, Is.EqualTo(PlayerListStatus.Unavailable));
            Assert.That(sources, Has.Count.EqualTo(1));
        }

        [Test]
        public void TheSameSelectionAgain_StartsNoSecondPoller()
        {
            polling.Apply(Installation(Product.NeoEE));
            polling.Apply(Installation(Product.NeoEE));
            polling.Apply(Installation(Product.NeoEE));

            Assert.That(sources, Has.Count.EqualTo(1));
            Assert.That(polling.IsPolling, Is.True);
        }

        [Test]
        public void SwitchingToEE_EndsThePolling_AndSwitchingBackStartsANewOne()
        {
            polling.Apply(Installation(Product.NeoEE));
            Assert.That(sources[0].Requested.Wait(Timeout), Is.True);
            int before = sources[0].Requests;

            Assert.That(polling.Apply(Installation(Product.EE)), Is.False);

            Assert.That(polling.IsPolling, Is.False);
            Assert.That(sources[0].Requests, Is.EqualTo(before), "no further request of the ended poller");

            Assert.That(polling.Apply(Installation(Product.NeoEE)), Is.True);

            Assert.That(sources, Has.Count.EqualTo(2), "a poller lives once");
            Assert.That(sources[1].Requested.Wait(Timeout), Is.True);
        }

        [Test]
        public void ARemovedSelection_EndsThePolling()
        {
            polling.Apply(Installation(Product.NeoEE));

            Assert.That(polling.Apply(null), Is.False);
            Assert.That(polling.IsPolling, Is.False);
        }

        [Test]
        public void AfterDispose_NothingStartsAgain()
        {
            polling.Apply(Installation(Product.NeoEE));
            polling.Dispose();

            Assert.That(polling.IsPolling, Is.False);
            Assert.That(polling.Apply(Installation(Product.NeoEE)), Is.False);
            Assert.That(sources, Has.Count.EqualTo(1));
        }

        [Test]
        public void ThePoller_IsChecked()
        {
            Assert.That(() => new PlayerListPolling(null), Throws.ArgumentNullException);
        }
    }
}
