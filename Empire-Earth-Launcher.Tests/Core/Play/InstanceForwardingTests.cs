using System;
using System.Collections.Generic;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Play
{
    /// <summary>
    /// The hand-over of <c>--product</c> from a second launcher to the running one (contract 1.4, revision 4): the message, the
    /// client (<see cref="InstanceForwarder"/>), the server (<see cref="InstanceReceiver"/>) and a round trip in one process
    /// through a fake channel instead of <c>WM_COPYDATA</c>. No window, no network.
    /// </summary>
    [TestFixture]
    public class InstanceForwardingTests
    {
        /// <summary>A window registry in one process: the "windows" are receivers, found by name.</summary>
        private sealed class InProcessChannel : IInstanceChannel
        {
            private readonly Dictionary<string, Func<byte[], bool>> windows = new Dictionary<string, Func<byte[], bool>>(StringComparer.Ordinal);

            public int Sends { get; private set; }

            /// <summary>The window does not exist for the first <see cref="MissingFor"/> sends (it is created a moment after the mutex).</summary>
            public int MissingFor { get; set; }

            public void Register(string name, Func<byte[], bool> window)
            {
                windows[name] = window;
            }

            public bool TrySend(string windowName, byte[] message)
            {
                Sends++;
                if (Sends <= MissingFor || !windows.TryGetValue(windowName, out Func<byte[], bool> window))
                    return false;
                return window(message);
            }
        }

        /// <summary>The running launcher as the receiver sees it.</summary>
        private sealed class FakeTarget : IInstanceTarget
        {
            public bool IsIdle { get; set; } = true;

            public List<string> Calls { get; } = new List<string>();

            public void SelectProduct(Product product)
            {
                Calls.Add("select " + product.Id);
            }

            public void BringToFront()
            {
                Calls.Add("front");
            }
        }

        private RecordingLogger logger;
        private InProcessChannel channel;
        private FakeTarget target;
        private List<TimeSpan> sleeps;
        private InstanceForwarder forwarder;

        [SetUp]
        public void SetUp()
        {
            logger = new RecordingLogger();
            channel = new InProcessChannel();
            target = new FakeTarget();
            sleeps = new List<TimeSpan>();
            forwarder = new InstanceForwarder(channel, sleeps.Add, logger);
        }

        private void RunningLauncher(int sessionId)
        {
            var receiver = new InstanceReceiver(target, logger);
            channel.Register(InstanceMessage.WindowName(sessionId), receiver.Handle);
        }

        private static LauncherArguments Args(params string[] args)
        {
            return LauncherArguments.Parse(args, new RecordingLogger());
        }

        // --- The message ----------------------------------------------------------------------------------------------

        [Test]
        public void TheWindowName_IsPerSession()
        {
            Assert.That(InstanceMessage.WindowName(1), Is.EqualTo("EmpireEarthCommunityLauncher.1"));
            Assert.That(InstanceMessage.WindowName(12), Is.EqualTo("EmpireEarthCommunityLauncher.12"));
            Assert.That(InstanceMessage.WindowName(1), Is.Not.EqualTo(InstanceMessage.WindowName(2)));
            Assert.That(InstanceMessage.WindowNamePrefix, Is.EqualTo(SingleInstance.MutexName + "."));
        }

        [TestCase("EE")]
        [TestCase("NeoEE")]
        public void TheMessage_RoundTrips(string id)
        {
            Product product = Product.FromId(id);

            byte[] bytes = InstanceMessage.ToBytes(InstanceMessage.Encode(product));

            Assert.That(InstanceMessage.Encode(product), Is.EqualTo("product=" + id));
            Assert.That(InstanceMessage.Decode(bytes), Is.SameAs(product));
        }

        [TestCase("product=")]
        [TestCase("product=ee")]
        [TestCase("product=AoC")]
        [TestCase("Product=EE")]
        [TestCase("product=EE ")]
        [TestCase("product=EE\0")]
        [TestCase("hello")]
        public void AMessageThatIsNotOurs_DecodesToNothing(string text)
        {
            Assert.That(InstanceMessage.Decode(InstanceMessage.ToBytes(text)), Is.Null);
        }

        [Test]
        public void Bytes_FromAnotherProcess_NeverThrow()
        {
            Assert.That(InstanceMessage.Decode(null), Is.Null);
            Assert.That(InstanceMessage.Decode(new byte[0]), Is.Null);
            Assert.That(InstanceMessage.Decode(new byte[] { 0xFF, 0xFE, 0x00 }), Is.Null, "no UTF-8");
            Assert.That(InstanceMessage.Decode(new byte[InstanceMessage.MaxBytes + 1]), Is.Null, "too long");
            byte[] padded = InstanceMessage.ToBytes("product=EE" + new string(' ', InstanceMessage.MaxBytes));
            Assert.That(InstanceMessage.Decode(padded), Is.Null, "too long");
        }

        // --- Client and server ----------------------------------------------------------------------------------------

        [Test]
        public void RoundTrip_TheRunningLauncher_BringsItselfToTheFront_AndSelectsTheProduct()
        {
            RunningLauncher(1);

            bool forwarded = forwarder.TryForward(Args("--product=NeoEE"), 1);

            Assert.That(forwarded, Is.True);
            Assert.That(target.Calls, Is.EqualTo(new[] { "front", "select NeoEE" }));
            Assert.That(channel.Sends, Is.EqualTo(1));
            Assert.That(sleeps, Is.Empty);
            Assert.That(logger.Messages.Any(m => m.Contains("The running launcher took --product=NeoEE")), Is.True);
            Assert.That(logger.Messages.Any(m => m.Contains("A second launcher handed over --product=NeoEE")), Is.True);
        }

        [Test]
        public void RoundTrip_AGameStartInProgress_OnlyBringsTheWindowToTheFront()
        {
            RunningLauncher(1);
            target.IsIdle = false;

            bool forwarded = forwarder.TryForward(Args("--product=EE"), 1);

            Assert.That(forwarded, Is.True, "the launcher took it, the second one ends silently");
            Assert.That(target.Calls, Is.EqualTo(new[] { "front" }), "the selection stays during the start");
            Assert.That(logger.Messages.Any(m => m.Contains("A game start is in progress; the selection stays.")), Is.True);
        }

        [Test]
        public void ASecondLauncherOfAnotherSession_FindsNoWindow()
        {
            RunningLauncher(2);

            bool forwarded = forwarder.TryForward(Args("--product=EE"), 1);

            Assert.That(forwarded, Is.False, "one launcher per session: sessions do not share a window");
            Assert.That(target.Calls, Is.Empty);
        }

        [Test]
        public void WithoutTheArgument_NothingIsSent()
        {
            RunningLauncher(1);

            Assert.That(forwarder.TryForward(Args(), 1), Is.False);
            Assert.That(forwarder.TryForward(Args("--product=Neo"), 1), Is.False, "an invalid value is no product");
            Assert.That(channel.Sends, Is.Zero);
            Assert.That(target.Calls, Is.Empty);
        }

        [Test]
        public void ARunningLauncherWithoutAWindow_IsRetriedBriefly_ThenTheUsualMessageFollows()
        {
            bool forwarded = forwarder.TryForward(Args("--product=EE"), 1);

            Assert.That(forwarded, Is.False);
            Assert.That(channel.Sends, Is.EqualTo(InstanceForwarder.Attempts));
            Assert.That(sleeps, Has.Count.EqualTo(InstanceForwarder.Attempts - 1));
            Assert.That(sleeps, Is.All.EqualTo(InstanceForwarder.RetryDelay));
            Assert.That(InstanceForwarder.Attempts * InstanceForwarder.RetryDelay.TotalMilliseconds, Is.LessThanOrEqualTo(1500), "a short wait");
            Assert.That(logger.MessagesOf(LogLevel.Warning).Single(), Does.Contain("did not take --product=EE"));
        }

        [Test]
        public void TheWindowThatAppearsAMomentLater_IsFoundByARetry()
        {
            RunningLauncher(1);
            channel.MissingFor = 2;

            bool forwarded = forwarder.TryForward(Args("--product=EE"), 1);

            Assert.That(forwarded, Is.True);
            Assert.That(channel.Sends, Is.EqualTo(3));
            Assert.That(sleeps, Has.Count.EqualTo(2));
            Assert.That(target.Calls, Is.EqualTo(new[] { "front", "select EE" }));
        }

        [Test]
        public void AMessageThatIsNotOurs_ChangesNothing()
        {
            var receiver = new InstanceReceiver(target, logger);

            bool handled = receiver.Handle(InstanceMessage.ToBytes("product=AoC"));

            Assert.That(handled, Is.False);
            Assert.That(target.Calls, Is.Empty, "not even the front");
            Assert.That(logger.MessagesOf(LogLevel.Warning).Single(), Does.Contain("not a product selection"));
        }

        [Test]
        public void Arguments_AreChecked()
        {
            Assert.That(() => new InstanceForwarder(null, sleeps.Add, logger), Throws.ArgumentNullException);
            Assert.That(() => new InstanceForwarder(channel, null, logger), Throws.ArgumentNullException);
            Assert.That(() => new InstanceForwarder(channel, sleeps.Add, null), Throws.ArgumentNullException);
            Assert.That(() => new InstanceReceiver(null, logger), Throws.ArgumentNullException);
            Assert.That(() => new InstanceReceiver(target, null), Throws.ArgumentNullException);
            Assert.That(() => forwarder.TryForward(null, 1), Throws.ArgumentNullException);
            Assert.That(() => InstanceMessage.Encode(null), Throws.ArgumentNullException);
        }

        // --- Together with the single-instance mutex -------------------------------------------------------------------

        [Test]
        public void TheSecondLauncher_ThatHandedOverItsProduct_ShowsNoMessage()
        {
            var mutexes = new FakeMutexProbe().With(SingleInstance.MutexName);
            RunningLauncher(7);
            var messages = new List<string>();

            IDisposable handle = Empire_Earth_Launcher.Program.ClaimSingleInstance(mutexes, logger, messages.Add,
                () => forwarder.TryForward(Args("--product=NeoEE"), 7));

            Assert.That(handle, Is.Null, "Main returns");
            Assert.That(messages, Is.Empty);
            Assert.That(target.Calls, Is.EqualTo(new[] { "front", "select NeoEE" }));
        }

        [Test]
        public void TheSecondLauncher_ThatCouldNotHandOver_ShowsTheUsualMessage()
        {
            var mutexes = new FakeMutexProbe().With(SingleInstance.MutexName);
            var messages = new List<string>();

            IDisposable handle = Empire_Earth_Launcher.Program.ClaimSingleInstance(mutexes, logger, messages.Add,
                () => forwarder.TryForward(Args("--product=NeoEE"), 7));

            Assert.That(handle, Is.Null);
            Assert.That(messages, Is.EqualTo(new[] { Empire_Earth_Launcher.Properties.Resources.LauncherAlreadyRunning }));
        }

        [Test]
        public void TheFirstLauncher_NeverHandsOver()
        {
            var mutexes = new FakeMutexProbe();
            var messages = new List<string>();
            int handOvers = 0;

            using (IDisposable handle = Empire_Earth_Launcher.Program.ClaimSingleInstance(mutexes, logger, messages.Add,
                       () => { handOvers++; return true; }))
                Assert.That(handle, Is.Not.Null);

            Assert.That(handOvers, Is.Zero);
            Assert.That(messages, Is.Empty);
        }
    }
}
