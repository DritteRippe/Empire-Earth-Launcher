using System;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Repair
{
    /// <summary>
    /// Where the suite "Empire Earth Community" can be run again to repair an installation (contract 4.4, revision 4): the
    /// folder <c>SourceDir</c> of its record, if the record lists the product of the installation and the folder exists.
    /// The record is read every time (read-only), so an advice shows what the last run of the suite wrote.
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
        /// The folder to run <c>Empire Earth Community Setup.exe</c> from again for <paramref name="installation"/>; null if
        /// there is no suite record, it does not list the product, the folder is gone (or is a network path, which is not
        /// probed on the UI thread), the installation is foreign (the community setups do not repair those, contract 4.4), or
        /// the suite would not repair this installation: it always runs as administrator for all users (contract 1.6, 1.7),
        /// so only an installation in administrator mode, and one with the AppId the record embeds, if both are known.
        /// </summary>
        public string FolderFor(Installation installation)
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
            string folder = record.RepairFolderFor(installation.Product, fileSystem);
            string line = "Suite record: " + record + "; " + (folder == null
                ? "it gives no folder to repair the " + installation.Product.Id + " installation from."
                : "the " + installation.Product.Id + " installation can be repaired by the suite from " + folder + ".");
            if (line != lastLogged)
            {
                lastLogged = line;
                logger.Info(line);
            }
            return folder;
        }
    }
}
