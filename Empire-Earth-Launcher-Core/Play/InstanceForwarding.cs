using System;
using System.Text;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Logging;

namespace Empire_Earth_Launcher.Core.Play
{
    /// <summary>
    /// The message a second launcher hands to the running one (contract 1.4, revision 4): the product of
    /// <c>--product=</c>. On Windows it travels as <c>WM_COPYDATA</c> to a hidden window of the running launcher; the text
    /// and the name of the window are fixed here, so that both sides and the tests agree.
    /// </summary>
    public static class InstanceMessage
    {
        /// <summary>Start of the window name; the Windows session id follows (one launcher per session, ADR 0010).</summary>
        public const string WindowNamePrefix = SingleInstance.MutexName + ".";

        /// <summary>The <c>dwData</c> of the <c>COPYDATASTRUCT</c>: "EEL1". A message with another value is not ours.</summary>
        public const int DataId = 0x45454C31;

        /// <summary>The most bytes of a message that are read; anything longer is refused.</summary>
        public const int MaxBytes = 64;

        private const string ProductPrefix = "product=";

        /// <summary>The name of the hidden window of the launcher of Windows session <paramref name="sessionId"/>.</summary>
        public static string WindowName(int sessionId)
        {
            return WindowNamePrefix + sessionId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>The text for <paramref name="product"/>: <c>product=EE</c> or <c>product=NeoEE</c>.</summary>
        public static string Encode(Product product)
        {
            if (product == null)
                throw new ArgumentNullException(nameof(product));
            return ProductPrefix + product.Id;
        }

        /// <summary>The bytes of a message (UTF-8, no terminator).</summary>
        public static byte[] ToBytes(string text)
        {
            return new UTF8Encoding(false).GetBytes(text ?? throw new ArgumentNullException(nameof(text)));
        }

        /// <summary>
        /// The product of a message; null for anything that is not exactly <c>product=EE</c> or <c>product=NeoEE</c>
        /// (also for bytes that are too many or no UTF-8). Never throws: the bytes come from another process.
        /// </summary>
        public static Product Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaxBytes)
                return null;
            string text;
            try
            {
                text = new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (ArgumentException)
            {
                return null;
            }
            return text.StartsWith(ProductPrefix, StringComparison.Ordinal)
                ? LauncherArguments.ParseProduct(text.Substring(ProductPrefix.Length))
                : null;
        }
    }

    /// <summary>The outcome of <see cref="IInstanceChannel.TrySend"/>.</summary>
    public enum SendResult
    {
        /// <summary>There is no window of that name (yet): the running launcher may not have created it.</summary>
        NotFound,

        /// <summary>The window took the message.</summary>
        Delivered,

        /// <summary>The window refused the message (for example UIPI between an elevated and a normal process).</summary>
        Refused,

        /// <summary>The window did not answer in time. The message is sent and is not taken back: it may still be handled.</summary>
        TimedOut
    }

    /// <summary>
    /// Delivers a message to the hidden window of a running launcher (the client side of the forwarding). The Windows
    /// implementation finds the window by its name, allows it to take the foreground and sends <c>WM_COPYDATA</c>; the tests
    /// use a fake that connects the sender with a receiver in the same process.
    /// </summary>
    public interface IInstanceChannel
    {
        /// <summary>
        /// Sends <paramref name="message"/> to the window <paramref name="windowName"/> and waits for its answer.
        /// </summary>
        /// <returns>See <see cref="SendResult"/>.</returns>
        SendResult TrySend(string windowName, byte[] message);
    }

    /// <summary>
    /// What the running launcher does with a forwarded product (the server side): bring its window to the front, and switch
    /// the selection if no game start is in progress.
    /// </summary>
    public interface IInstanceTarget
    {
        /// <summary>True if the selection may change now: no game start is in progress.</summary>
        bool IsIdle { get; }

        /// <summary>Selects <paramref name="product"/> for this session (not saved).</summary>
        void SelectProduct(Product product);

        /// <summary>Brings the main window to the front (restores it if it is minimized).</summary>
        void BringToFront();
    }

    /// <summary>
    /// The second launcher: hands the <c>--product</c> argument to the running launcher of the same Windows session and
    /// reports whether that worked. If not, the caller shows the usual "already running" message.
    /// </summary>
    public sealed class InstanceForwarder
    {
        /// <summary>
        /// How often the window of the running launcher is looked for (only while there is none): it is created a moment after
        /// the mutex, so a launcher that was started just before this one is waited for (about 10 s, a cold start).
        /// </summary>
        public const int Attempts = 50;

        /// <summary>The wait between two attempts.</summary>
        public static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(200);

        private readonly IInstanceChannel channel;
        private readonly Action<TimeSpan> sleep;
        private readonly ILogger logger;

        /// <param name="channel">The way to the window of the running launcher.</param>
        /// <param name="sleep">Waits between two attempts (<c>Thread.Sleep</c>; the tests do not wait).</param>
        /// <param name="logger">Log of the launcher.</param>
        public InstanceForwarder(IInstanceChannel channel, Action<TimeSpan> sleep, ILogger logger)
        {
            this.channel = channel ?? throw new ArgumentNullException(nameof(channel));
            this.sleep = sleep ?? throw new ArgumentNullException(nameof(sleep));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Hands <paramref name="arguments"/> to the running launcher of Windows session <paramref name="sessionId"/>.
        /// Without <c>--product</c> there is nothing to hand over and nothing is sent.
        /// </summary>
        /// <returns>True if the running launcher took the product (or has it queued after a timeout); false if nothing was sent,
        /// there is no window or the window refused.</returns>
        public bool TryForward(LauncherArguments arguments, int sessionId)
        {
            if (arguments == null)
                throw new ArgumentNullException(nameof(arguments));
            if (arguments.SessionProduct == null)
                return false;

            string window = InstanceMessage.WindowName(sessionId);
            byte[] message = InstanceMessage.ToBytes(InstanceMessage.Encode(arguments.SessionProduct));
            for (int attempt = 1; attempt <= Attempts; attempt++)
            {
                SendResult result = channel.TrySend(window, message);
                if (result == SendResult.Delivered)
                {
                    logger.Info("The running launcher took " + ContractNames.ProductArgumentName + "=" + arguments.SessionProduct.Id +
                                "; this one ends.");
                    return true;
                }
                if (result == SendResult.TimedOut)
                {
                    // The message is sent and stays in the queue of the busy launcher: no second one, no message to the user.
                    logger.Warning("The running launcher (window " + window + ") did not answer in time; the " +
                                   ContractNames.ProductArgumentName + "=" + arguments.SessionProduct.Id +
                                   " it was sent stays queued, and this one ends.");
                    return true;
                }
                if (result == SendResult.Refused)
                    break;
                if (attempt < Attempts)
                    sleep(RetryDelay);
            }
            logger.Warning("The running launcher (window " + window + ") did not take " + ContractNames.ProductArgumentName +
                           "=" + arguments.SessionProduct.Id + "; this one ends with the usual message.");
            return false;
        }
    }

    /// <summary>
    /// The running launcher: takes a message of <see cref="InstanceMessage"/> from its hidden window and acts on it
    /// through <see cref="IInstanceTarget"/>. The bytes come from another process: a message that is not ours changes nothing.
    /// </summary>
    public sealed class InstanceReceiver
    {
        private readonly IInstanceTarget target;
        private readonly ILogger logger;
        private Product pending;

        public InstanceReceiver(IInstanceTarget target, ILogger logger)
        {
            this.target = target ?? throw new ArgumentNullException(nameof(target));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Handles a message: the window comes to the front, and the product is selected if the launcher is idle. During a
        /// game start (or while a dialog is open) the selection stays (the start would otherwise run for another installation
        /// than the one asked for); the product is kept and applied by <see cref="ApplyPending"/> when the launcher is idle.
        /// </summary>
        /// <returns>True if the message was ours (the answer of <c>WM_COPYDATA</c>), also if the selection stayed.</returns>
        public bool Handle(byte[] message)
        {
            Product product = InstanceMessage.Decode(message);
            if (product == null)
            {
                logger.Warning("A message to the launcher window was ignored: it is not a product selection.");
                return false;
            }

            logger.Info("A second launcher handed over " + ContractNames.ProductArgumentName + "=" + product.Id + ".");
            target.BringToFront();
            if (!target.IsIdle)
            {
                pending = product;
                logger.Info("A game start is in progress; the selection stays.");
                return true;
            }
            pending = null;
            target.SelectProduct(product);
            return true;
        }

        /// <summary>
        /// Applies the product that came while the launcher was busy, as soon as it is idle; the newest product wins. Called
        /// on the UI thread when its message queue is empty. Cheap without a pending product.
        /// </summary>
        public void ApplyPending()
        {
            Product product = pending;
            if (product == null || !target.IsIdle)
                return;
            pending = null;
            logger.Info("The launcher is idle again; selecting the " + product.Id + " handed over earlier.");
            target.SelectProduct(product);
        }
    }
}
