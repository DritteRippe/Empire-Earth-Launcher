using System.Collections.Generic;
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
    /// Members still to come with their work packages (ARCHITECTURE 8): last game.
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

        /// <summary>
        /// Language of the launcher's UI: <c>en</c>, <c>de</c> or <c>fr</c>, or empty for the Windows display language
        /// (<see cref="UiLanguage"/>, ADR 0009). Applied when the launcher starts; an unknown value means the Windows
        /// language and is kept in the file.
        /// </summary>
        [DataMember(Order = 4)]
        public string UiCulture { get; set; }

        /// <summary>
        /// The hints of the consistency checks the player hid (ADR 0015): each with the values it was about, so that the hint
        /// shows again when a value changes. Added in L-WP5 as an optional member (schema 1).
        /// </summary>
        [DataMember(Order = 5)]
        public List<HiddenHint> HiddenHints { get; set; }

        /// <summary>Members of the file this launcher does not know (written by a newer launcher).</summary>
        public ExtensionDataObject ExtensionData { get; set; }

        private void SetDefaults()
        {
            GameDirectory = string.Empty;
            ThemeName = DefaultThemeName;
            CustomThemeFile = string.Empty;
            UiCulture = UiLanguage.Windows;
            HiddenHints = new List<HiddenHint>();
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
            UiCulture = UiCulture ?? UiLanguage.Windows;
            HiddenHints = HiddenHints ?? new List<HiddenHint>();
            HiddenHints.RemoveAll(hint => hint == null);
        }
    }

    /// <summary>
    /// A hint the player hid (ADR 0015, ARCHITECTURE 8 "hidden warnings"): what it is about and the values it showed. Written
    /// and compared by <c>GameSettings.HintVisibility</c>.
    /// </summary>
    [DataContract(Name = "HiddenHint", Namespace = "")]
    public sealed class HiddenHint : IExtensibleDataObject
    {
        /// <summary>The finding and its game settings key, e.g. <c>BitDepthMismatch HKCU\Software\Neo\Empire Earth</c>.</summary>
        [DataMember(Order = 0)]
        public string Hint { get; set; }

        /// <summary>The values when it was hidden, e.g. <c>Game Bit Depth=16; Texture Bit Depth=32</c>.</summary>
        [DataMember(Order = 1)]
        public string Values { get; set; }

        /// <summary>Members of a newer launcher.</summary>
        public ExtensionDataObject ExtensionData { get; set; }
    }
}
