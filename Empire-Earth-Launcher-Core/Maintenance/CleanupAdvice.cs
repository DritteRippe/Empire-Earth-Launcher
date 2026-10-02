using System;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Maintenance
{
    /// <summary>The state of a key of the cleanup list on this computer (<see cref="RegistryCleanup.Scan"/>).</summary>
    public enum CleanupState
    {
        /// <summary>The key does not exist; it is not shown.</summary>
        Missing,

        /// <summary>
        /// Stale (ADR 0007 amendment): no installation of its product was found, and the folder its "Installed From" values
        /// name is missing on a present, fixed, local drive.
        /// </summary>
        Stale,

        /// <summary>Kept: an installation of its product was found (the key may hold its settings).</summary>
        InstallationFound,

        /// <summary>Kept: the folder its "Installed From" values name exists.</summary>
        FolderExists,

        /// <summary>
        /// Kept: the folder is on a drive that is missing, removable, a network drive or of an unknown kind (a USB stick that
        /// is not plugged in is not a removed installation).
        /// </summary>
        DriveNotFixed,

        /// <summary>Kept: the key names no folder (no or invalid "Installed From" values), so nothing shows that it is stale.</summary>
        NoFolderNamed,

        /// <summary>
        /// Kept: whether the folder exists cannot be told (access denied to it or to its parent); Windows reports such a folder
        /// as missing, which is no proof that it is (security review).
        /// </summary>
        FolderUnknown,

        /// <summary>Kept: the key or its values cannot be read.</summary>
        Unreadable,

        /// <summary><c>Software\Sierra</c>: never deleted, it contains the NeoEE CD keys.</summary>
        Protected
    }

    /// <summary>What the Tools page tells the player about a key of the cleanup list.</summary>
    public enum CleanupAdviceCode
    {
        /// <summary>The launcher can delete the key (HKCU, stale) after a <c>.reg</c> backup; the player selects it.</summary>
        LauncherCanDelete,

        /// <summary>
        /// HKLM, stale: export the key with the Registry Editor (right-click, Export), then delete it there as an administrator
        /// (forum t=12082 p=49553, t=10577 p=46301). Only the SSSI and Mad Doc keys of the list get it.
        /// </summary>
        ExportThenDeleteAsAdministrator,

        /// <summary>Do not delete: the key contains the NeoEE CD keys (<c>Software\Sierra</c>, forum 4.19).</summary>
        DoNotDeleteContainsCdKeys,

        /// <summary>Keep: an installation of the product was found.</summary>
        KeepInstallationFound,

        /// <summary>Keep: the folder the key names exists.</summary>
        KeepFolderExists,

        /// <summary>Keep: the folder is on a drive that is not a present, fixed, local drive.</summary>
        KeepDriveNotFixed,

        /// <summary>Keep: the key names no folder.</summary>
        KeepNoFolderNamed,

        /// <summary>Keep: whether the folder the key names exists cannot be told (access denied).</summary>
        KeepFolderUnknown,

        /// <summary>Keep: the key cannot be read.</summary>
        KeepUnreadable
    }

    /// <summary>
    /// The advice for one key of the cleanup list: a code and its parameters (the key, the folder it names, whether the CD keys
    /// exist). The <c>Texts</c> class of the launcher turns it into a sentence. Only two codes name a key as something to delete
    /// (<see cref="DeletionTarget"/>), and never a protected key or an ancestor of one (ADR 0007 plan review; checked here and
    /// by a test of every entry in every state).
    /// </summary>
    public sealed class CleanupAdvice
    {
        private CleanupAdvice(CleanupAdviceCode code, RegistryLocation key, string folder, bool? cdKeysExist)
        {
            Code = code;
            Key = key;
            Folder = folder;
            CdKeysExist = cdKeysExist;
        }

        public CleanupAdviceCode Code { get; }

        /// <summary>The key the advice is about.</summary>
        public RegistryLocation Key { get; }

        /// <summary>The folder the "Installed From" values name; null if none.</summary>
        public string Folder { get; }

        /// <summary>For <see cref="CleanupAdviceCode.DoNotDeleteContainsCdKeys"/>: whether <c>CDKeys</c> exists below it (O8: only "exists").</summary>
        public bool? CdKeysExist { get; }

        /// <summary>
        /// The key the advice names as something to delete (by the launcher or by the player with the Registry Editor); null
        /// for every other advice.
        /// </summary>
        public RegistryLocation DeletionTarget
        {
            get
            {
                return Code == CleanupAdviceCode.LauncherCanDelete || Code == CleanupAdviceCode.ExportThenDeleteAsAdministrator
                    ? Key
                    : null;
            }
        }

        /// <summary>The advice for <paramref name="entry"/> in <paramref name="state"/>.</summary>
        /// <exception cref="InvalidOperationException">The advice would name a protected key for deletion (a bug of the list).</exception>
        public static CleanupAdvice For(CleanupEntry entry, CleanupState state, string folder, bool? cdKeysExist)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));
            CleanupAdviceCode code;
            if (entry.Scope == CleanupScope.Protected)
                code = CleanupAdviceCode.DoNotDeleteContainsCdKeys;
            else
            {
                switch (state)
                {
                    case CleanupState.Stale:
                        code = entry.Scope == CleanupScope.LauncherDeletes
                            ? CleanupAdviceCode.LauncherCanDelete
                            : CleanupAdviceCode.ExportThenDeleteAsAdministrator;
                        break;
                    case CleanupState.InstallationFound:
                        code = CleanupAdviceCode.KeepInstallationFound;
                        break;
                    case CleanupState.FolderExists:
                        code = CleanupAdviceCode.KeepFolderExists;
                        break;
                    case CleanupState.DriveNotFixed:
                        code = CleanupAdviceCode.KeepDriveNotFixed;
                        break;
                    case CleanupState.NoFolderNamed:
                        code = CleanupAdviceCode.KeepNoFolderNamed;
                        break;
                    case CleanupState.FolderUnknown:
                        code = CleanupAdviceCode.KeepFolderUnknown;
                        break;
                    default:
                        // Missing, Unreadable and Protected (the latter only for protected entries): nothing to delete.
                        code = CleanupAdviceCode.KeepUnreadable;
                        break;
                }
            }

            var advice = new CleanupAdvice(code, entry.Key, folder,
                code == CleanupAdviceCode.DoNotDeleteContainsCdKeys ? cdKeysExist : null);
            if (advice.DeletionTarget != null && RegistryWritePolicy.ProtectionOf(advice.DeletionTarget) != RegistryWriteDenial.None)
                throw new InvalidOperationException("The cleanup advice would name a protected key for deletion: " + entry);
            return advice;
        }

        public override string ToString()
        {
            return Code + " " + Key + (Folder == null ? string.Empty : " (" + Folder + ")") +
                   (CdKeysExist == null ? string.Empty : CdKeysExist.Value ? ", CD keys exist" : ", no CD keys");
        }
    }
}
