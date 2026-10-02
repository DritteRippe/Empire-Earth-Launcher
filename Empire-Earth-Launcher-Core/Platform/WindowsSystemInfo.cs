using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Empire_Earth_Launcher.Core.Logging;

namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>
    /// <see cref="ISystemInfo"/> on Windows (ADR 0011): the version from <c>RtlGetVersion</c> (independent of the
    /// manifest), Wine from <c>ntdll.dll</c>, the physical size of the primary screen from <c>EnumDisplayDevices</c> and
    /// <c>EnumDisplaySettings(ENUM_CURRENT_SETTINGS)</c> (the current display mode, in physical pixels whatever the DPI
    /// awareness of the process), the size a DPI-unaware program sees from <c>GetSystemMetrics</c> (the launcher is
    /// DPI-unaware like the game, ADR 0011), and the ANSI code page from <see cref="Encoding.Default"/>, which is the
    /// ANSI code page of the system on the .NET Framework.
    /// </summary>
    /// <remarks>
    /// A thin adapter, checked on real Windows by the test plan (100 % and 150 %, Windows 7 and 10/11); the unit tests use
    /// a fake. A failing Windows function is logged once and gives an unknown value, never an exception (ADR 0013).
    /// </remarks>
    public sealed class WindowsSystemInfo : ISystemInfo
    {
        private const int EnumCurrentSettings = -1;
        private const int DisplayDevicePrimaryDevice = 0x4;
        private const int SmCxScreen = 0;
        private const int SmCyScreen = 1;

        private readonly ILogger logger;
        private readonly Lazy<Version> windowsVersion;
        private readonly Lazy<bool> isWine;
        private bool screenProblemLogged;
        private Encoding ansiEncoding;
        private bool ansiProblemLogged;

        public WindowsSystemInfo(ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            windowsVersion = new Lazy<Version>(ReadWindowsVersion);
            isWine = new Lazy<bool>(DetectWine);
        }

        public Version WindowsVersion
        {
            get { return windowsVersion.Value; }
        }

        public bool IsWine
        {
            get { return isWine.Value; }
        }

        public ScreenSize PrimaryScreen
        {
            get
            {
                try
                {
                    string device = FindPrimaryDisplayDevice();
                    var mode = new DevMode { dmSize = (short)Marshal.SizeOf(typeof(DevMode)) };
                    if (EnumDisplaySettings(device, EnumCurrentSettings, ref mode) && mode.dmPelsWidth > 0 && mode.dmPelsHeight > 0)
                        return new ScreenSize(mode.dmPelsWidth, mode.dmPelsHeight);
                    LogScreenProblem("EnumDisplaySettings of " + (device ?? "the current display") + " failed (" +
                                     new Win32Exception(Marshal.GetLastWin32Error()).Message + ").", null);
                }
                catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
                {
                    LogScreenProblem("The display functions of Windows are not available.", ex);
                }
                return ScreenSize.Empty;
            }
        }

        public bool IsInAnsiCodePage(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            try
            {
                // An exception fallback also switches off the "best fit" mapping, which would turn e.g. a Greek letter
                // into a Latin one: the game would then look for another folder.
                if (ansiEncoding == null)
                    ansiEncoding = Encoding.GetEncoding(Encoding.Default.CodePage, EncoderFallback.ExceptionFallback,
                        DecoderFallback.ExceptionFallback);
                ansiEncoding.GetByteCount(text);
                return true;
            }
            catch (EncoderFallbackException)
            {
                return false;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException)
            {
                if (!ansiProblemLogged)
                {
                    ansiProblemLogged = true;
                    logger.Warning("The ANSI code page of Windows (" + Encoding.Default.CodePage + ") is not available: " + ex.Message);
                }
                return true;
            }
        }

        public ScreenSize PrimaryScreenUnaware
        {
            get
            {
                try
                {
                    int width = GetSystemMetrics(SmCxScreen);
                    int height = GetSystemMetrics(SmCyScreen);
                    return width > 0 && height > 0 ? new ScreenSize(width, height) : ScreenSize.Empty;
                }
                catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
                {
                    LogScreenProblem("GetSystemMetrics is not available.", ex);
                    return ScreenSize.Empty;
                }
            }
        }

        public string PrimaryDisplayAdapter
        {
            get
            {
                try
                {
                    string adapter = FindPrimaryDisplay()?.DeviceString?.Trim();
                    return string.IsNullOrEmpty(adapter) ? null : adapter;
                }
                catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
                {
                    LogScreenProblem("The display functions of Windows are not available.", ex);
                    return null;
                }
            }
        }

        /// <summary>The name of the primary display device (<c>\\.\DISPLAY1</c>), or null to ask for the current display.</summary>
        private static string FindPrimaryDisplayDevice()
        {
            return FindPrimaryDisplay()?.DeviceName;
        }

        /// <summary>The primary display device (<c>EnumDisplayDevices</c>), or null if Windows names none.</summary>
        private static DisplayDevice? FindPrimaryDisplay()
        {
            var device = new DisplayDevice { cb = Marshal.SizeOf(typeof(DisplayDevice)) };
            for (uint index = 0; EnumDisplayDevices(null, index, ref device, 0); index++)
            {
                if ((device.StateFlags & DisplayDevicePrimaryDevice) != 0)
                    return device;
                device = new DisplayDevice { cb = Marshal.SizeOf(typeof(DisplayDevice)) };
            }
            return null;
        }

        private Version ReadWindowsVersion()
        {
            try
            {
                var info = new OsVersionInfoEx { dwOSVersionInfoSize = Marshal.SizeOf(typeof(OsVersionInfoEx)) };
                if (RtlGetVersion(ref info) == 0)
                    return new Version(info.dwMajorVersion, info.dwMinorVersion, info.dwBuildNumber);
                logger.Warning("RtlGetVersion failed; the Windows version of .NET is used.");
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                logger.Warning("RtlGetVersion is not available; the Windows version of .NET is used.", ex);
            }
            return Environment.OSVersion.Version;
        }

        private bool DetectWine()
        {
            try
            {
                IntPtr ntdll = GetModuleHandle("ntdll.dll");
                return ntdll != IntPtr.Zero && GetProcAddress(ntdll, "wine_get_version") != IntPtr.Zero;
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                logger.Warning("Unable to check for Wine; Windows is assumed.", ex);
                return false;
            }
        }

        private void LogScreenProblem(string message, Exception exception)
        {
            if (screenProblemLogged)
                return;
            screenProblemLogged = true;
            logger.Warning("The size of the primary screen is unknown: " + message, exception);
        }

        // The fields of the native structures are written by Windows, not by this code.
#pragma warning disable CS0649
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DisplayDevice
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
        }

        /// <summary><c>DEVMODEW</c> with the display part of its unions.</summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DevMode
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OsVersionInfoEx
        {
            public int dwOSVersionInfoSize;
            public int dwMajorVersion;
            public int dwMinorVersion;
            public int dwBuildNumber;
            public int dwPlatformId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szCSDVersion;
            public ushort wServicePackMajor;
            public ushort wServicePackMinor;
            public ushort wSuiteMask;
            public byte wProductType;
            public byte wReserved;
        }
#pragma warning restore CS0649

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DisplayDevice lpDisplayDevice, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DevMode lpDevMode);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("ntdll.dll")]
        private static extern int RtlGetVersion(ref OsVersionInfoEx versionInfo);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, BestFitMapping = false)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
    }
}
