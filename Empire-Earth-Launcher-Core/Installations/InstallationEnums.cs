using System;
using Empire_Earth_Launcher.Core.Contract;

namespace Empire_Earth_Launcher.Core.Installations
{
    /// <summary>Who installed an installation (contract 1.4, "kind").</summary>
    public enum InstallationKind
    {
        /// <summary>A community setup since v2: <c>install.ini</c> or the registry record has a contract version.</summary>
        Community,

        /// <summary>A community setup up to 1.7.2: found by its uninstall key, without a contract version (contract 1.5).</summary>
        CommunityLegacy,

        /// <summary>Anything else: retail CD, GOG, an old patch chain, a copy.</summary>
        Foreign
    }

    /// <summary>Install mode of the setup (contract 0, "Install modes").</summary>
    public enum InstallMode
    {
        /// <summary>Not known (foreign installations, or no source names it).</summary>
        Unknown,

        /// <summary><c>admin</c>: for all users, records in HKLM.</summary>
        Admin,

        /// <summary><c>user</c>: for the account that ran the setup, records in HKCU.</summary>
        User,

        /// <summary><c>portable</c>: no registry record and no uninstall key.</summary>
        Portable
    }

    /// <summary>The sources of the discovery, in the order of default preference (contract 1.4).</summary>
    public enum InstallationSource
    {
        /// <summary>1: the folder chosen in the launcher settings (it only selects).</summary>
        UserChoice = 1,

        /// <summary>2: the registry record of a setup since v2 (contract 1.1).</summary>
        RegistryRecord = 2,

        /// <summary>3: the uninstall key of a community setup (contract 1.3).</summary>
        UninstallKey = 3,

        /// <summary>4: the "Installed From" values of the Empire Earth settings key (contract 3.3).</summary>
        InstalledFrom = 4,

        /// <summary>5: the folder of the launcher or its parent.</summary>
        LauncherFolder = 5
    }

    /// <summary>Whether the programs of an installation are there (contract 1.4, "Validity" and "AoC folder").</summary>
    public enum InstallationState
    {
        /// <summary>The program of every game of the installation exists.</summary>
        Ok,

        /// <summary>
        /// A program is missing (<see cref="Installation.MissingPrograms"/>), typically deleted or quarantined by an
        /// antivirus: listed, never dropped, and the reason for the repair advice.
        /// </summary>
        Damaged,

        /// <summary>The folder the user chose does not exist (any more); kept so that the user sees it.</summary>
        FolderMissing
    }

    /// <summary>Conversions of the contract's install mode names.</summary>
    public static class InstallModes
    {
        /// <summary>
        /// The mode written as <c>admin</c>, <c>user</c> or <c>portable</c> (ignoring case and surrounding white space);
        /// <see cref="InstallMode.Unknown"/> for anything else.
        /// </summary>
        public static InstallMode Parse(string text)
        {
            string trimmed = text?.Trim();
            if (string.Equals(trimmed, ContractNames.AdminInstallMode, StringComparison.OrdinalIgnoreCase))
                return InstallMode.Admin;
            if (string.Equals(trimmed, ContractNames.UserInstallMode, StringComparison.OrdinalIgnoreCase))
                return InstallMode.User;
            if (string.Equals(trimmed, ContractNames.PortableInstallMode, StringComparison.OrdinalIgnoreCase))
                return InstallMode.Portable;
            return InstallMode.Unknown;
        }
    }
}
