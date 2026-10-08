using System;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Repair
{
    /// <summary>
    /// The package "Empire Earth Community" whose suite installed an installation, as its record says (contract 1.6): the
    /// player downloaded it as a ZIP file from its release page (<see cref="SetupDownloadPage.PackageRelease"/>) and ran
    /// <c>Empire Earth Community Setup.exe</c> from the unpacked folder. Such an installation is repaired and updated with the
    /// package, never with the product setup of the community website, which is another build with the same AppId and would
    /// replace the setups and fixes of the package (contract 4.4, revision 4; since launcher 1.1.1 also without the folder).
    /// </summary>
    public sealed class SuitePackage
    {
        /// <param name="folder">The folder to run the suite from again; null or blank if it is gone.</param>
        public SuitePackage(string folder)
        {
            Folder = string.IsNullOrWhiteSpace(folder) ? null : folder;
        }

        /// <summary>
        /// The folder <c>SourceDir</c> the suite was started from in its last run, if it still exists: the unpacked package
        /// with <c>Empire Earth Community Setup.exe</c>; null if it is gone (or a network path, which is not probed on the UI
        /// thread). Then the player downloads the package again.
        /// </summary>
        public string Folder { get; }

        public override string ToString()
        {
            return Folder == null ? "suite package, folder gone" : "suite package in " + Folder;
        }
    }

    /// <summary>
    /// Whether the suite "Empire Earth Community" installed an installation, and where it can be run again to repair it
    /// (contract 4.4, revision 4): its record lists the product of the installation, and its folder <c>SourceDir</c> exists
    /// or is gone. The record is read every time (read-only), so an advice shows what the last run of the suite wrote.
    /// </summary>
    public sealed class SuiteRepairLocator
    {
        private readonly SuiteRecordReader reader;
        private readonly IFileSystem fileSystem;
        private readonly ILogger logger;

        /// <summary>The last line logged, so that an advice that asks again and again logs a change only.</summary>
        private string lastLogged;

        public SuiteRepairLocator(SuiteRecordReader reader, IFileSystem fileSystem, ILogger logger)
        {
            this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// The package whose suite installed <paramref name="installation"/>, with the folder to run
        /// <c>Empire Earth Community Setup.exe</c> from again if it still exists; null if there is no suite record, it does
        /// not list the product, the installation is foreign (the community setups do not repair those, contract 4.4), or
        /// the suite did not install this installation: it always runs as administrator for all users (contract 1.6, 1.7),
        /// so only an installation in administrator mode, and one with the AppId the record embeds, if both are known.
        /// </summary>
        public SuitePackage PackageFor(Installation installation)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            if (installation.Kind == InstallationKind.Foreign)
                return null;
            if (installation.Mode != InstallMode.Admin)
                return null;
            SuiteRecord record = reader.Read();
            if (record == null)
                return null;
            string embedded = record.AppIdFor(installation.Product);
            if (embedded != null && installation.AppId != null &&
                !string.Equals(installation.AppId.Trim('{', '}'), embedded, StringComparison.OrdinalIgnoreCase))
                return null;
            string product = installation.Product.Id;
            if (!record.Lists(installation.Product))
            {
                Log("Suite record: " + record + "; it gives no folder to repair the " + product + " installation from.");
                return null;
            }
            string folder = record.RepairFolderFor(installation.Product, fileSystem);
            Log("Suite record: " + record + "; " + (folder == null
                ? "the suite installed the " + product + " installation, but there is no folder to run it from again: the " +
                  "advice is to download the package again."
                : "the " + product + " installation can be repaired by the suite from " + folder + "."));
            return new SuitePackage(folder);
        }

        /// <summary>
        /// The folder to run <c>Empire Earth Community Setup.exe</c> from again for <paramref name="installation"/>
        /// (<see cref="SuitePackage.Folder"/> of <see cref="PackageFor"/>); null if the suite did not install it or the folder
        /// is gone.
        /// </summary>
        public string FolderFor(Installation installation)
        {
            return PackageFor(installation)?.Folder;
        }

        /// <summary>Logs <paramref name="line"/> unless it is the line logged last.</summary>
        private void Log(string line)
        {
            if (line == lastLogged)
                return;
            lastLogged = line;
            logger.Info(line);
        }
    }
}
