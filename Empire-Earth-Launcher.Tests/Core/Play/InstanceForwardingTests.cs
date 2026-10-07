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
    /// The hand-over of <c>--product</c> from a second launcher to the running one (contract 1.4, revision 4), and since revision 6
    /// of the request to come to the front when the second launcher has no argument: the message, the client
    /// (<see cref="InstanceForwarder"/>), the server (<see cref="InstanceReceiver"/>) and a round trip in one process through a
    /// fake channel instead of <c>WM_COPYDATA</c>. No window, no network.
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

            /// <summary>The answer instead of the window's, once the window exists (for example a timeout).</summary>
            public SendResult? Forced { get; set; }

            public SendResult TrySend(string windowName, byte[] message)
            {
                Sends++;
                if (Sends <= MissingFor || !windows.TryGetValue(windowName, out Func<byte[], bool> window))
                    return SendResult.NotFound;
                if (Forced.HasValue)
                    return Forced.Value;
                return window(message) ? SendResult.Delivered : SendResult.Refused;
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

        [Test]
        public void TheShowMessage_RoundTrips_AndHasNoProduct()
        {
            byte[] bytes = InstanceMessage.ToBytes(InstanceMessage.Encode(null));

            Assert.That(InstanceMessage.ShowText, Is.EqualTo("show"));
            Assert.That(InstanceMessage.Encode(null), Is.EqualTo("show"));
            Assert.That(InstanceMessage.TryDecode(bytes, out Product product), Is.True);
            Assert.That(product, Is.Null, "no product: the window only comes to the front");
            Assert.That(InstanceMessage.Decode(bytes), Is.Null, "the old Decode knows products only");
        }

        [TestCase("product=EE", "EE")]
        [TestCase("product=NeoEE", "NeoEE")]
        public void TryDecode_ReadsAProduct(string text, string id)
        {
            Assert.That(InstanceMessage.TryDecode(InstanceMessage.ToBytes(text), out Product product), Is.True);
            Assert.That(product, Is.SameAs(Product.FromId(id)));
        }

        [TestCase("show ")]
        [TestCase("SHOW")]
        [TestCase("Show")]
        [TestCase("show\0")]
        [TestCase("shown")]
        [TestCase("product=")]
        [TestCase("product=ee")]
        [TestCase("product=AoC")]
        [TestCase("product=EE\n")]
        [TestCase("")]
        public void TryDecode_RefusesWhatIsNotExactlyOurs(string text)
        {
            Assert.That(InstanceMessage.TryDecode(InstanceMessage.ToBytes(text), out Product product), Is.False);
            Assert.That(product, Is.Null);
        }

        [Test]
        public void TryDecode_BytesFromAnotherProcess_NeverThrow()
        {
            Assert.That(InstanceMessage.TryDecode(null, out _), Is.False);
            Assert.That(InstanceMessage.TryDecode(new byte[0], out _), Is.False);
            Assert.That(InstanceMessage.TryDecode(new byte[] { 0xFF, 0xFE, 0x00 }, out _), Is.False, "no UTF-8");
            Assert.That(InstanceMessage.TryDecode(new byte[InstanceMessage.MaxBytes + 1], out _), Is.False, "too long");
            Assert.That(InstanceMessage.TryDecode(InstanceMessage.ToBytes("show" + new string(' ', InstanceMessage.MaxBytes)), out _), Is.False);
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

        /// <summary>The one shortcut of the suite starts the launcher without an argument (contract 1.7 point 8, revision 6).</summary>
        [Test]
        public void WithoutTheArgument_TheForwarderSendsShow_AndTheRunningLauncherOnlyComesToTheFront()
        {
            RunningLauncher(1);

            bool forwarded = forwarder.TryForward(Args(), 1);

            Assert.That(forwarded, Is.True);
            Assert.That(channel.Sends, Is.EqualTo(1));
            Assert.That(target.Calls, Is.EqualTo(new[] { "front" }), "no product is selected: the selection of the running launcher stays");
            Assert.That(logger.Messages.Any(m => m.Contains("The running launcher took the request to come to the front")), Is.True);
            Assert.That(logger.Messages.Any(m => m.Contains("A second launcher asked the launcher to come to the front; the selection stays.")), Is.True);
        }

        [Test]
        public void AnInvalidProduct_IsNoProduct_TheForwarderSendsShow()
        {
            RunningLauncher(1);

            Assert.That(forwarder.TryForward(Args("--product=Neo"), 1), Is.True);

            Assert.That(target.Calls, Is.EqualTo(new[] { "front" }));
        }

        [Test]
        public void Show_DuringAGameStart_ChangesNothing_AndKeepsAPendingProduct()
        {
            var receiver = new InstanceReceiver(target, logger);
            target.IsIdle = false;
            receiver.Handle(InstanceMessage.ToBytes("product=EE"));

            bool handled = receiver.Handle(InstanceMessage.ToBytes("show"));

            Assert.That(handled, Is.True);
            target.IsIdle = true;
            receiver.ApplyPending();
            Assert.That(target.Calls, Is.EqualTo(new[] { "front", "front", "select EE" }), "show neither selects nor clears the product that came before");
        }

        [Test]
        public void Show_NeverCallsSelectProduct()
        {
            var receiver = new InstanceReceiver(target, logger);

            receiver.Handle(InstanceMessage.ToBytes("show"));
            receiver.ApplyPending();

            Assert.That(target.Calls, Is.EqualTo(new[] { "front" }));
        }

        [Test]
        public void WithoutTheArgument_AndWithoutARunningLauncher_ShowsTheUsualMessage()
        {
            var mutexes = new FakeMutexProbe().With(SingleInstance.MutexName);
            var messages = new List<string>();

            IDisposable handle = Empire_Earth_Launcher.Program.ClaimSingleInstance(mutexes, logger, messages.Add,
                () => forwarder.TryForward(Args(), 7));

            Assert.That(handle, Is.Null);
            Assert.That(messages, Is.EqualTo(new[] { Empire_Earth_Launcher.Properties.Resources.LauncherAlreadyRunning }),
                "a launcher 1.0.0 that does not know show is no window of ours: the old message");
        }

        [Test]
        public void WithoutTheArgument_TheSecondLauncherShowsNoMessage_WhenTheRunningOneTookShow()
        {
            var mutexes = new FakeMutexProbe().With(SingleInstance.MutexName);
            RunningLauncher(7);
            var messages = new List<string>();

            IDisposable handle = Empire_Earth_Launcher.Program.ClaimSingleInstance(mutexes, logger, messages.Add,
                () => forwarder.TryForward(Args(), 7));

            Assert.That(handle, Is.Null, "Main returns");
            Assert.That(messages, Is.Empty, "no message \"already running\" on every second click");
            Assert.That(target.Calls, Is.EqualTo(new[] { "front" }));
        }

        [Test]
        public void ARunningLauncherWithoutAWindow_IsRetriedBriefly_ThenTheUsualMessageFollows()
        {
            bool forwarded = forwarder.TryForward(Args("--product=EE"), 1);

            Assert.That(forwarded, Is.False);
            Assert.That(channel.Sends, Is.EqualTo(InstanceForwarder.Attempts));
            Assert.That(sleeps, Has.Count.EqualTo(InstanceForwarder.Attempts - 1));
            Assert.That(sleeps, Is.All.EqualTo(InstanceForwarder.RetryDelay));
            Assert.That(InstanceForwarder.Attempts * InstanceForwarder.RetryDelay.TotalMilliseconds, Is.InRange(5000, 12000),
                "long enough for a cold start of the running launcher");
            Assert.That(logger.MessagesOf(LogLevel.Warning).Single(), Does.Contain("did not take --product=EE"));
        }

        [Test]
        public void ATimeout_IsNotRetried_TheMessageIsSent_AndNoMessageFollows()
        {
            RunningLauncher(1);
            channel.Forced = SendResult.TimedOut;

            bool forwarded = forwarder.TryForward(Args("--product=EE"), 1);

            Assert.That(forwarded, Is.True, "the message is queued in the busy launcher; the user gets no error");
            Assert.That(channel.Sends, Is.EqualTo(1));
            Assert.That(sleeps, Is.Empty);
        }

        [Test]
        public void ARefusal_IsNotRetried_TheUsualMessageFollows()
        {
            RunningLauncher(1);
            channel.Forced = SendResult.Refused;

            bool forwarded = forwarder.TryForward(Args("--product=EE"), 1);

            Assert.That(forwarded, Is.False);
            Assert.That(channel.Sends, Is.EqualTo(1));
            Assert.That(sleeps, Is.Empty);
        }

        [Test]
        public void AProductThatCameWhileBusy_IsSelectedWhenTheLauncherIsIdleAgain_TheNewestWins()
        {
            var receiver = new InstanceReceiver(target, logger);
            target.IsIdle = false;
            receiver.Handle(InstanceMessage.ToBytes("product=EE"));
            receiver.Handle(InstanceMessage.ToBytes("product=NeoEE"));

            receiver.ApplyPending();
            Assert.That(target.Calls, Is.EqualTo(new[] { "front", "front" }), "still busy: nothing selected");

            target.IsIdle = true;
            receiver.ApplyPending();
            receiver.ApplyPending();

            Assert.That(target.Calls, Is.EqualTo(new[] { "front", "front", "select NeoEE" }), "once, the newest");
        }

        [Test]
        public void AProductHandledWhileIdle_ClearsAnOlderPendingOne()
        {
            var receiver = new InstanceReceiver(target, logger);
            target.IsIdle = false;
            receiver.Handle(InstanceMessage.ToBytes("product=EE"));
            target.IsIdle = true;
            receiver.Handle(InstanceMessage.ToBytes("product=NeoEE"));
            receiver.ApplyPending();

            Assert.That(target.Calls.Where(c => c.StartsWith("select", StringComparison.Ordinal)), Is.EqualTo(new[] { "select NeoEE" }));
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
