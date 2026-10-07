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
    /// </remarks>
    [DataContract(Name = "LauncherSettings", Namespace = "")]
    public sealed class LauncherSettings : IExtensibleDataObject
    {
        /// <summary>The schema this launcher reads and writes.</summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary>Theme used when none was chosen.</summary>
        public const string DefaultThemeName = "Light";

        /// <summary>
        /// <see cref="ThemeName"/> of the built-in colors chosen on the Launcher page: no theme file is applied, not even
        /// <see cref="DefaultThemeName"/>. The angle brackets cannot be part of a file name on Windows, so no theme file of
        /// the themes folder has this name.
        /// </summary>
        public const string BuiltInThemeName = "<built-in>";

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

        /// <summary>Theme of the launcher's themes folder, or <see cref="BuiltInThemeName"/>.</summary>
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

        /// <summary>
        /// The game chosen on the Play page last time: <c>EE</c> or <c>AoC</c> (<c>Contract.Game.Id</c>); empty or unknown
        /// means Empire Earth, and The Art of Conquest only counts while the installation has it. Added in L-WP6 as an
        /// optional member (schema 1).
        /// </summary>
        [DataMember(Order = 6)]
        public string LastGame { get; set; }

        /// <summary>
        /// The folder chosen for each product (contract 1.4 revision 6, source 1): a product without an entry or with an empty
        /// folder is found automatically. Launcher 1.1.0 keeps <see cref="GameDirectory"/> as a mirror of the folder of the
        /// current product (<see cref="LastProduct"/>), so that launcher 1.0.0 selects the same installation. Added in
        /// launcher 1.1.0 as an optional member (schema 1); <c>Installations.ProductChoices</c> reads and changes it.
        /// </summary>
        [DataMember(Order = 7)]
        public List<ProductFolder> ProductFolders { get; set; }

        /// <summary>
        /// The product of the game chosen on the Play page last time: <c>EE</c> or <c>NeoEE</c> (<c>Contract.Product.Id</c>);
        /// empty for the default selection of contract 1.4. Together with <see cref="LastGame"/> it is the remembered entry of
        /// the four games. Added in launcher 1.1.0 as an optional member (schema 1).
        /// </summary>
        [DataMember(Order = 8)]
        public string LastProduct { get; set; }

        /// <summary>Members of the file this launcher does not know (written by a newer launcher).</summary>
        public ExtensionDataObject ExtensionData { get; set; }

        private void SetDefaults()
        {
            GameDirectory = string.Empty;
            ThemeName = DefaultThemeName;
            CustomThemeFile = string.Empty;
            UiCulture = UiLanguage.Windows;
            HiddenHints = new List<HiddenHint>();
            LastGame = string.Empty;
            ProductFolders = new List<ProductFolder>();
            LastProduct = string.Empty;
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
            LastGame = LastGame ?? string.Empty;
            ProductFolders = ProductFolders ?? new List<ProductFolder>();
            ProductFolders.RemoveAll(entry => entry == null);
            LastProduct = LastProduct ?? string.Empty;
        }
    }

    /// <summary>
    /// The folder the player chose for one product (<see cref="LauncherSettings.ProductFolders"/>). An entry of a product this
    /// launcher does not know is kept and written back unchanged.
    /// </summary>
    [DataContract(Name = "ProductFolder", Namespace = "")]
    public sealed class ProductFolder : IExtensibleDataObject
    {
        /// <summary>The product id, <c>EE</c> or <c>NeoEE</c> (<c>Contract.Product.Id</c>).</summary>
        [DataMember(Order = 0)]
        public string Product { get; set; }

        /// <summary>The EE folder, the install root or the AoC folder chosen for the product; empty for automatic detection.</summary>
        [DataMember(Order = 1)]
        public string Folder { get; set; }

        /// <summary>Members of a newer launcher.</summary>
        public ExtensionDataObject ExtensionData { get; set; }
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
