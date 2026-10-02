using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Settings
{
    /// <summary>Outcome of <see cref="SettingsStore.Load"/>.</summary>
    public enum SettingsLoadStatus
    {
        /// <summary>The file was read.</summary>
        Loaded,
        /// <summary>There is no file yet (first start); the defaults are used.</summary>
        NotFound,
        /// <summary>The file was damaged; it was renamed to <c>settings.json.damaged</c> if possible, the defaults are used.</summary>
        Damaged,
        /// <summary>The file could not be read (access, I/O); the defaults are used and nothing is saved.</summary>
        Unreadable,
        /// <summary>A newer launcher wrote the file with another schema; the defaults are used and nothing is saved.</summary>
        NewerSchema
    }

    /// <summary>Outcome of <see cref="SettingsStore.Save"/>.</summary>
    public enum SettingsSaveStatus
    {
        /// <summary>The file was written.</summary>
        Saved,
        /// <summary>Not written on purpose: the file could not be read or belongs to a newer launcher (see <see cref="SettingsStore.CanSave"/>).</summary>
        NotSaved,
        /// <summary>Writing failed (logged); the previous file is unchanged.</summary>
        Failed
    }

    /// <summary>
    /// Reads and writes <see cref="LauncherSettings"/> as <c>settings.json</c> (ADR 0005): UTF-8 JSON with a
    /// schema version, unknown members kept, written through a temporary file so that a crash never leaves a
    /// half-written file. A damaged file is renamed to <c>settings.json.damaged</c> (replacing an older copy) and
    /// the defaults are used, as the removed <c>UserSettingsRecovery</c> did for <c>user.config</c>.
    /// </summary>
    /// <remarks>
    /// Nothing here throws for a missing, damaged, unreadable or unwritable file (ADR 0013); every such case is
    /// logged once. A file that cannot be read, or that a newer launcher wrote, is never overwritten in this
    /// session (<see cref="CanSave"/>): the settings work for the session, but the file stays as it is. Use on the
    /// UI thread.
    /// </remarks>
    public sealed class SettingsStore
    {
        /// <summary>Suffix of the copy of a damaged settings file, kept for diagnosis.</summary>
        public const string DamagedFileSuffix = ".damaged";

        /// <summary>A larger file is damaged: the launcher writes a few hundred bytes.</summary>
        public const long MaxFileBytes = 1024 * 1024;

        private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };

        private readonly IFileSystem fileSystem;
        private readonly ILogger logger;

        /// <param name="fileSystem">Where the file is kept.</param>
        /// <param name="settingsFile">Full path of <c>settings.json</c> (<see cref="LauncherPaths.SettingsFile"/>).</param>
        /// <param name="logger">Log of the launcher.</param>
        public SettingsStore(IFileSystem fileSystem, string settingsFile, ILogger logger)
        {
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            if (string.IsNullOrEmpty(settingsFile))
                throw new ArgumentException("The settings file is required.", nameof(settingsFile));
            SettingsFile = settingsFile;
        }

        /// <summary>Full path of <c>settings.json</c>.</summary>
        public string SettingsFile { get; }

        /// <summary>Full path of the copy of a damaged file.</summary>
        public string DamagedFile
        {
            get { return SettingsFile + DamagedFileSuffix; }
        }

        /// <summary>The settings of this session: the defaults until <see cref="Load"/>; never null.</summary>
        public LauncherSettings Current { get; private set; } = new LauncherSettings();

        /// <summary>
        /// False after <see cref="SettingsLoadStatus.Unreadable"/> or <see cref="SettingsLoadStatus.NewerSchema"/>:
        /// <see cref="Save"/> then leaves the file alone.
        /// </summary>
        public bool CanSave { get; private set; } = true;

        /// <summary>Reads the file into <see cref="Current"/> (see <see cref="SettingsLoadStatus"/>).</summary>
        public SettingsLoadStatus Load()
        {
            Current = new LauncherSettings();
            CanSave = true;

            FileSystemResult<byte[]> read = fileSystem.ReadAllBytes(SettingsFile, MaxFileBytes);
            switch (read.Status)
            {
                case FileSystemStatus.Ok:
                    break;
                case FileSystemStatus.NotFound:
                    logger.Info("No launcher settings yet (" + SettingsFile + "), the defaults are used.");
                    return SettingsLoadStatus.NotFound;
                case FileSystemStatus.TooLarge:
                    return MoveDamagedFileAside("larger than " + MaxFileBytes + " bytes", null);
                default:
                    CanSave = false;
                    logger.Error("Unable to read the launcher settings " + SettingsFile + " (" + read +
                                 "); the defaults are used and the file is not changed in this session.");
                    return SettingsLoadStatus.Unreadable;
            }

            if (!TryParse(read.Value, out LauncherSettings loaded, out string problem, out Exception error))
                return MoveDamagedFileAside(problem, error);

            if (loaded.SchemaVersion > LauncherSettings.CurrentSchemaVersion)
            {
                CanSave = false;
                logger.Warning("The launcher settings " + SettingsFile + " were written by a newer launcher (schema " +
                               loaded.SchemaVersion + ", this launcher knows " + LauncherSettings.CurrentSchemaVersion +
                               "); the defaults are used and the file is not changed.");
                return SettingsLoadStatus.NewerSchema;
            }

            Current = loaded;
            logger.Info("Launcher settings loaded from " + SettingsFile + ".");
            return SettingsLoadStatus.Loaded;
        }

        /// <summary>Writes <see cref="Current"/> (see <see cref="SettingsSaveStatus"/>); failures are logged.</summary>
        public SettingsSaveStatus Save()
        {
            if (!CanSave)
            {
                logger.Warning("The launcher settings are not saved: " + SettingsFile +
                               " could not be read or belongs to a newer launcher. The change applies to this session only.");
                return SettingsSaveStatus.NotSaved;
            }

            Current.SchemaVersion = LauncherSettings.CurrentSchemaVersion;
            byte[] data = Serialize(Current);

            string folder = WinPath.GetParent(SettingsFile);
            FileSystemResult result = folder == null ? FileSystemResult.Success : fileSystem.CreateDirectory(folder);
            if (result.IsOk)
                result = fileSystem.WriteAllBytesAtomically(SettingsFile, data);
            if (!result.IsOk)
            {
                logger.Error("Unable to save the launcher settings to " + SettingsFile + " (" + result +
                             "); the change applies to this session only.");
                return SettingsSaveStatus.Failed;
            }
            return SettingsSaveStatus.Saved;
        }

        /// <summary>The bytes of <paramref name="settings"/> as indented UTF-8 JSON without BOM.</summary>
        internal static byte[] Serialize(LauncherSettings settings)
        {
            var serializer = new DataContractJsonSerializer(typeof(LauncherSettings));
            using (var stream = new MemoryStream())
            {
                using (XmlDictionaryWriter writer = JsonReaderWriterFactory.CreateJsonWriter(stream, new UTF8Encoding(false), false, true, "  "))
                {
                    serializer.WriteObject(writer, settings);
                    writer.Flush();
                }
                return stream.ToArray();
            }
        }

        /// <summary>
        /// Reads settings from <paramref name="data"/>. A UTF-8 BOM (added by some editors) is accepted. False with a
        /// reason for everything that is not a settings object with a schema version.
        /// </summary>
        internal static bool TryParse(byte[] data, out LauncherSettings settings, out string problem, out Exception error)
        {
            settings = null;
            problem = null;
            error = null;
            int offset = data.Length >= Utf8Bom.Length && data[0] == Utf8Bom[0] && data[1] == Utf8Bom[1] && data[2] == Utf8Bom[2]
                ? Utf8Bom.Length
                : 0;
            try
            {
                var serializer = new DataContractJsonSerializer(typeof(LauncherSettings));
                using (var stream = new MemoryStream(data, offset, data.Length - offset, false))
                    settings = serializer.ReadObject(stream) as LauncherSettings;
            }
            catch (Exception ex) when (IsDeserializationError(ex))
            {
                error = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex;
                problem = "invalid JSON or values";
                return false;
            }

            if (settings == null)
            {
                problem = "not a settings object";
                return false;
            }
            if (settings.SchemaVersion < 1)
            {
                problem = "no SchemaVersion";
                settings = null;
                return false;
            }
            return true;
        }

        /// <summary>
        /// The errors <see cref="DataContractJsonSerializer"/> reports for invalid data. Mono reports some of them
        /// wrapped in a <see cref="TargetInvocationException"/> (the property setters are called through reflection
        /// there); <see cref="ArgumentException"/> covers invalid UTF-8 (<see cref="DecoderFallbackException"/>).
        /// </summary>
        private static bool IsDeserializationError(Exception ex)
        {
            return ex is SerializationException || ex is XmlException || ex is FormatException ||
                   ex is OverflowException || ex is InvalidCastException || ex is ArgumentException ||
                   (ex is TargetInvocationException && ex.InnerException != null && IsDeserializationError(ex.InnerException));
        }

        private SettingsLoadStatus MoveDamagedFileAside(string problem, Exception error)
        {
            Current = new LauncherSettings();

            FileSystemResult result = fileSystem.FileExists(DamagedFile) ? fileSystem.DeleteFile(DamagedFile) : FileSystemResult.Success;
            if (result.IsOk)
                result = fileSystem.Move(SettingsFile, DamagedFile);

            if (result.IsOk)
            {
                logger.Error("The launcher settings were damaged (" + problem + ") and have been reset to their defaults. " +
                             "The damaged file was kept as " + DamagedFile + ".", error);
            }
            else
            {
                logger.Error("The launcher settings " + SettingsFile + " are damaged (" + problem + "); the defaults are " +
                             "used. Unable to keep the damaged file as " + DamagedFile + " (" + result + ").", error);
            }
            return SettingsLoadStatus.Damaged;
        }
    }
}
