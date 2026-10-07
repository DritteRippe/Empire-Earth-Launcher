using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Play;

namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>
    /// <see cref="IInstanceChannel"/> with <c>WM_COPYDATA</c>: finds the hidden message window of the running launcher by its
    /// name (<see cref="InstanceMessage.WindowName"/>), allows its process to take the foreground
    /// (<c>AllowSetForegroundWindow</c>, which only the process that has the foreground right may grant, i.e. this second
    /// launcher the user just started) and sends the message with a timeout. Checked on real Windows by the test plan.
    /// </summary>
    /// <remarks>
    /// The window is a message-only window (parent <c>HWND_MESSAGE</c>), so it is found below that parent and not by
    /// <c>FindWindow</c>. If the running launcher is elevated and this one is not, Windows drops the message (UIPI): the
    /// answer is <see cref="SendResult.Refused"/> and the second launcher shows its usual message (the running launcher lifts the
    /// filter for its own window, so that this only happens with an old Windows). A timeout is <see cref="SendResult.TimedOut"/>:
    /// the message is not taken back and is not sent again.
    /// </remarks>
    public sealed class WindowsInstanceChannel : IInstanceChannel
    {
        private const int WmCopyData = 0x004A;
        private const int ErrorInvalidWindowHandle = 1400;
        private const uint SmtoAbortIfHung = 0x0002;
        private const uint TimeoutMilliseconds = 3000;
        private static readonly IntPtr MessageOnlyParent = new IntPtr(-3);

        private readonly ILogger logger;

        public WindowsInstanceChannel(ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public SendResult TrySend(string windowName, byte[] message)
        {
            if (string.IsNullOrEmpty(windowName))
                throw new ArgumentException("A window name is required.", nameof(windowName));
            if (message == null || message.Length == 0)
                throw new ArgumentException("A message is required.", nameof(message));
            try
            {
                IntPtr window = FindWindowEx(MessageOnlyParent, IntPtr.Zero, null, windowName);
                if (window == IntPtr.Zero)
                    return SendResult.NotFound;

                GetWindowThreadProcessId(window, out int processId);
                if (processId != 0 && !AllowSetForegroundWindow(processId))
                    logger.Info("AllowSetForegroundWindow(" + processId + ") failed: " + new Win32Exception(Marshal.GetLastWin32Error()).Message);

                IntPtr data = Marshal.AllocHGlobal(message.Length);
                try
                {
                    Marshal.Copy(message, 0, data, message.Length);
                    var copyData = new CopyDataStruct
                    {
                        DataId = new IntPtr(InstanceMessage.DataId),
                        ByteCount = message.Length,
                        Data = data
                    };
                    IntPtr result;
                    IntPtr sent = SendMessageTimeout(window, WmCopyData, IntPtr.Zero, ref copyData, SmtoAbortIfHung,
                        TimeoutMilliseconds, out result);
                    if (sent == IntPtr.Zero)
                    {
                        int error = Marshal.GetLastWin32Error();
                        logger.Info("The launcher window " + windowName + " did not answer: " + new Win32Exception(error).Message);
                        // No window any more (it ended meanwhile) is no timeout; 0 with the error 0 or 1460 is one.
                        return error == ErrorInvalidWindowHandle ? SendResult.NotFound : SendResult.TimedOut;
                    }
                    return result != IntPtr.Zero ? SendResult.Delivered : SendResult.Refused;
                }
                finally
                {
                    Marshal.FreeHGlobal(data);
                }
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException || ex is Win32Exception)
            {
                // Not Windows (the tests under Mono) or a damaged user32: no forwarding, the usual message follows.
                logger.Warning("The message to the running launcher could not be sent.", ex);
                return SendResult.Refused;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CopyDataStruct
        {
            public IntPtr DataId;
            public int ByteCount;
            public IntPtr Data;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string windowName);

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr window, out int processId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllowSetForegroundWindow(int processId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(IntPtr window, int message, IntPtr wParam, ref CopyDataStruct lParam,
            uint flags, uint timeoutMilliseconds, out IntPtr result);
    }
}
