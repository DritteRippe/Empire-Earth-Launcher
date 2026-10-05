using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Play;

namespace Empire_Earth_Launcher
{
    /// <summary>
    /// The hidden window of the running launcher that a second launcher sends <c>WM_COPYDATA</c> to (contract 1.4, revision 4):
    /// a message-only window named <see cref="InstanceMessage.WindowName"/>, created on the UI thread before the main window
    /// runs, so the message loop of <c>Application.Run</c> delivers its messages. The content goes to an
    /// <see cref="InstanceReceiver"/>. Checked on real Windows by the test plan; the logic is in the core and tested there.
    /// </summary>
    internal sealed class InstanceMessageWindow : NativeWindow, IDisposable
    {
        private const int WmCopyData = 0x004A;
        private static readonly IntPtr MessageOnlyParent = new IntPtr(-3);

        private readonly InstanceReceiver receiver;
        private readonly ILogger logger;

        private InstanceMessageWindow(string name, InstanceReceiver receiver, ILogger logger)
        {
            this.receiver = receiver;
            this.logger = logger;
            CreateHandle(new CreateParams { Caption = name, Parent = MessageOnlyParent });
        }

        /// <summary>
        /// Creates the window; null if Windows refuses (logged): the launcher then runs without taking a product from a second
        /// launcher, which then shows its usual message.
        /// </summary>
        public static InstanceMessageWindow TryCreate(string name, InstanceReceiver receiver, ILogger logger)
        {
            if (receiver == null)
                throw new ArgumentNullException(nameof(receiver));
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            try
            {
                return new InstanceMessageWindow(name, receiver, logger);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception)
            {
                logger.Warning("The window for the product of a second launcher could not be created.", ex);
                return null;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg != WmCopyData)
            {
                base.WndProc(ref m);
                return;
            }
            bool handled = false;
            try
            {
                byte[] bytes = ReadMessage(m.LParam);
                handled = bytes != null && receiver.Handle(bytes);
            }
            catch (Exception ex)
            {
                // A message of another process must not end the launcher.
                logger.Error("A message to the launcher window could not be handled.", ex);
            }
            m.Result = handled ? new IntPtr(1) : IntPtr.Zero;
        }

        /// <summary>The bytes of a <c>WM_COPYDATA</c> of ours; null if it carries another id or has no or too many bytes.</summary>
        private static byte[] ReadMessage(IntPtr lParam)
        {
            if (lParam == IntPtr.Zero)
                return null;
            var data = (CopyDataStruct)Marshal.PtrToStructure(lParam, typeof(CopyDataStruct));
            if (data.DataId.ToInt64() != InstanceMessage.DataId || data.ByteCount <= 0 || data.ByteCount > InstanceMessage.MaxBytes ||
                data.Data == IntPtr.Zero)
                return null;
            var bytes = new byte[data.ByteCount];
            Marshal.Copy(data.Data, bytes, 0, bytes.Length);
            return bytes;
        }

        public void Dispose()
        {
            if (Handle != IntPtr.Zero)
                DestroyHandle();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CopyDataStruct
        {
            public IntPtr DataId;
            public int ByteCount;
            public IntPtr Data;
        }
    }
}
