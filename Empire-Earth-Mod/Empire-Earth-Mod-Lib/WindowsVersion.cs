using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;

namespace Empire_Earth_Mod_Lib
{
    public static class WindowsVersion
    {
        
        /// <summary>
        /// The [Description] is the name shown to the user (see <see cref="EnumExtensions.GetDescription"/>).
        /// Version are using big numbers in case we want to add another version
        /// and still being able to compare with &lt; and &gt; operators.
        /// Because ModData will store the version as number, changing the order will result
        /// in an invalid version or invalid comparison.
        /// </summary>
        public enum WindowsVersionEnum
        {
            [Description("Error")] Error = 0,
            [Description("Wine")] Wine = 1,
            [Description("Windows 95")] W95 = 100,
            [Description("Windows 98")] W98 = 200,
            [Description("Windows Me")] Me = 300,
            [Description("Windows NT")] Nt = 400,
            [Description("Windows 2000")] W2000 = 500,
            [Description("Windows XP")] Xp = 600,
            [Description("Windows Vista")] Vista = 700,
            [Description("Windows 7")] Seven = 800,
            [Description("Windows 8")] Eight = 900,
            [Description("Windows 10")] Ten = 1000,
            [Description("Windows 11")] Eleven = 1100
        }

        [DllImport("kernel32.dll", CharSet=CharSet.Auto, SetLastError=true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet=CharSet.Auto, ExactSpelling=true, SetLastError=true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        /**
         * Don't forget to add Windows 10 in supportedOS in app.manifest or
         * it will not be able to detect it
         *
         * Microsoft est pas foutu de faire un truc pour identifier la version de Windows...
         * A little dirty I know but It support all versions :V
         */
        public static WindowsVersionEnum GetCurrentWindowsVersion()
        {
            // Based from isWine (https://github.com/zocker-160/isWINE/blob/master/isWINE_dll/isWINE.cpp)
            // Don't seems to work sadly...
            /*
            IntPtr ntPtr = GetModuleHandle("ntdll.dll");
            if (ntPtr != IntPtr.Zero && GetProcAddress(ntPtr, "wine_get_version") != IntPtr.Zero)
                return WindowsVersionEnum.Wine; // Not working for some reasons for the moment
            */

            OperatingSystem operatingSystem = Environment.OSVersion;

            switch (operatingSystem.Platform)
            {
                case PlatformID.Win32Windows:
                    //This is a pre-NT version of Windows
                    switch (operatingSystem.Version.Minor)
                    {
                        case 0:
                            return WindowsVersionEnum.W95;
                        case 10:
                            return WindowsVersionEnum.W98;
                        case 90:
                            return WindowsVersionEnum.Me;
                        default:
                            return WindowsVersionEnum.Error;
                    }
                case PlatformID.Win32NT:
                    switch (operatingSystem.Version.Major)
                    {
                        case 3:
                        case 4:
                            return WindowsVersionEnum.Nt;
                        case 5:
                            return operatingSystem.Version.Minor == 0
                                ? WindowsVersionEnum.W2000
                                : WindowsVersionEnum.Xp;
                        case 6:
                            switch (operatingSystem.Version.Minor)
                            {
                                case 0:
                                    return WindowsVersionEnum.Vista;
                                case 1:
                                    return WindowsVersionEnum.Seven;
                                default:
                                {
                                    return operatingSystem.Version.Minor == 2 || operatingSystem.Version.Minor == 3 ?
                                        WindowsVersionEnum.Eight : WindowsVersionEnum.Error;
                                }
                            }
                        case 10:
                            return operatingSystem.Version.Build < 22000
                                ? WindowsVersionEnum.Ten
                                : WindowsVersionEnum.Eleven;
                        default:
                            return WindowsVersionEnum.Error;
                    }
                default:
                    return WindowsVersionEnum.Error;
            }
        }
    }
}