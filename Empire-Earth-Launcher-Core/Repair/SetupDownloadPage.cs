using System;
using System.ComponentModel;
using System.IO;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Repair
{
    /// <summary>
    /// The download page of the community setup (contract 4.3, ADR 0008 amendment of 1.1.0): one fixed page per product, chosen
    /// by the installation alone. No request is made to choose it. The website answers the two product pages with a redirect
    /// to the current setup, so the browser downloads it right away; the launcher follows no redirect and never downloads or
    /// starts the setup itself (contract 4.1). Since launcher 1.1.1 an installation of the suite gets the release page of its
    /// package instead (<see cref="PackageRelease"/>, ADR 0008 amendment of 1.1.1).
    /// </summary>
    /// <remarks>
    /// Up to launcher 1.0.0 the page came from the update API (<c>GET /setup/?product=&lt;AppId&gt;</c>), whose answer named a
    /// host that no longer resolves; the update API now only answers the version questions of contract 4.5
    /// (<see cref="UpdateApi"/>).
    /// </remarks>
    public static class SetupDownloadPage
    {
        /// <summary>The page of both setups: foreign installations and an unknown product (<c>SetupURL</c> of the setup).</summary>
        public const string General = "https://empireearth.eu/download/";

        /// <summary>The page of the EE setup (<c>SetupURLEE</c> of the setup).</summary>
        public const string EmpireEarth = "https://empireearth.eu/download/ee/";

        /// <summary>The page of the NeoEE setup (<c>SetupURLNeoEE</c> of the setup).</summary>
        public const string NeoEE = "https://empireearth.eu/download/neo/";

        /// <summary>
        /// The page of the newest release of the package "Empire Earth Community" on GitHub, with its ZIP file (the suite
        /// <c>Empire Earth Community Setup.exe</c>, the product setups it embeds, the launcher). It is the download for an
        /// installation of the suite (<see cref="SuitePackage"/>, <see cref="RepairAdvice.DownloadUrl"/>) and the place where
        /// newer versions of the package appear, which the version check of contract 4.5 does not ask about. It is not chosen
        /// by <see cref="For"/>: the suite record decides, not the installation alone.
        /// </summary>
        public const string PackageRelease = "https://github.com/DritteRippe/Empire-Earth-Community/releases/latest";

        /// <summary>
        /// The page for <paramref name="installation"/> (the table of contract 4.3): EE and NeoEE installations of a community
        /// setup get their product's page; a foreign installation, an unknown product and null get <see cref="General"/>.
        /// </summary>
        public static string For(Installation installation)
        {
            if (installation == null || installation.Kind == InstallationKind.Foreign)
                return General;
            if (installation.Product == Product.EE)
                return EmpireEarth;
            if (installation.Product == Product.NeoEE)
                return NeoEE;
            return General;
        }

        /// <summary>A name of the page for the log: <c>EE</c>, <c>NeoEE</c>, <c>package release</c> or <c>general</c>.</summary>
        public static string NameOf(string url)
        {
            return url == EmpireEarth ? Product.EE.Id
                : url == NeoEE ? Product.NeoEE.Id
                : url == PackageRelease ? "package release"
                : "general";
        }

        /// <summary>
        /// Opens <paramref name="url"/> in the default browser through <paramref name="starter"/> (shell, no verb: not elevated,
        /// contract 4.3 point 2). No request is made; the browser follows a redirect of the website itself. A failure is logged
        /// and returned, never thrown; the caller logs why the page is opened.
        /// </summary>
        public static DownloadPageResult Open(string url, IProcessStarter starter, ILogger logger)
        {
            if (url == null)
                throw new ArgumentNullException(nameof(url));
            if (starter == null)
                throw new ArgumentNullException(nameof(starter));
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            try
            {
                starter.OpenUrl(url);
                return DownloadPageResult.Opened;
            }
            catch (Exception ex) when (ex is Win32Exception || ex is FileNotFoundException || ex is InvalidOperationException)
            {
                logger.Warning("The download page " + url + " could not be opened.", ex);
                return DownloadPageResult.Failed;
            }
        }
    }
}
