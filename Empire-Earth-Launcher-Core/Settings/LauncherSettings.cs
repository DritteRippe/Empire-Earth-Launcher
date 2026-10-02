using System.Runtime.Serialization;

namespace Empire_Earth_Launcher.Core.Settings
{
    /// <summary>
    /// The user settings of the launcher, stored as <c>settings.json</c> by <see cref="SettingsStore"/> (ADR 0005).
    /// Use on the UI thread.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Members written by a newer launcher are kept through <see cref="ExtensionData"/> and written back unchanged,
    /// so an older launcher never removes them. Adding an optional member is a compatible change that keeps
    /// <see cref="CurrentSchemaVersion"/>; renaming, removing or changing the meaning of a member raises it.
    /// </para>
    /// <para>
    /// Members still to come with their work packages (ARCHITECTURE 8): UI language, last game, hidden warnings.
    /// </para>
    /// </remarks>
    [DataContract(Name = "LauncherSettings", Namespace = "")]
    public sealed class LauncherSettings : IExtensibleDataObject
    {
        /// <summary>The schema this launcher reads and writes.</summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary>Theme used when none was chosen.</summary>
        public const string DefaultThemeName = "Light";

        public LauncherSettings()
        {
            SetDefaults();
            SchemaVersion = CurrentSchemaVersion;
        }

        /// <summary>Version of the schema of the file; 0 when the file had none (not a settings file).</summary>
        [DataMember(Order = 0)]
        public int SchemaVersion { get; set; }

        /// <summary>
        /// Folder chosen by the user (install root or EE folder, contract 1.4 source 1); empty for automatic
        /// detection. Kept even if the folder no longer exists, so that the user sees it.
        /// </summary>
        [DataMember(Order = 1)]
        public string GameDirectory { get; set; }

        /// <summary>Theme of the launcher's themes folder.</summary>
        [DataMember(Order = 2)]
        public string ThemeName { get; set; }

        /// <summary>Theme file chosen with "Custom"; empty if none. Wins over <see cref="ThemeName"/>.</summary>
        [DataMember(Order = 3)]
        public string CustomThemeFile { get; set; }

        /// <summary>Members of the file this launcher does not know (written by a newer launcher).</summary>
        public ExtensionDataObject ExtensionData { get; set; }

        private void SetDefaults()
        {
            GameDirectory = string.Empty;
            ThemeName = DefaultThemeName;
            CustomThemeFile = string.Empty;
        }

        /// <summary>The serializer creates the object without a constructor: start from the defaults.</summary>
        [OnDeserializing]
        private void OnDeserializing(StreamingContext context)
        {
            SetDefaults();
            SchemaVersion = 0;
        }

        /// <summary>A member written as <c>null</c> gets its default.</summary>
        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            GameDirectory = GameDirectory ?? string.Empty;
            ThemeName = ThemeName ?? DefaultThemeName;
            CustomThemeFile = CustomThemeFile ?? string.Empty;
        }
    }
}
