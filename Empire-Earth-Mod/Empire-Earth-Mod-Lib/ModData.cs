using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.Serialization;
using Empire_Earth_Mod_Lib.Serialization;

namespace Empire_Earth_Mod_Lib
{
    /// <summary>
    /// The description of a mod, stored as JSON in its archive: name, version, variants, files... Icon and
    /// banners are kept in <see cref="ModAssets"/>, archives are written by <see cref="ModPackageBuilder"/> and
    /// read by <see cref="ModArchiveReader"/>.
    /// </summary>
    /// <remarks>
    /// The [DataMember] names define the JSON of the <see cref="EemFormat.DataEntryName"/> entry of a mod
    /// archive; without [DataContract] the JSON keys were compiler generated backing field names such as
    /// "&lt;Uuid&gt;k__BackingField".
    /// </remarks>
    [DataContract(Name = "ModData", Namespace = "")]
    public class ModData
    {
        /// <summary>Name of the variant every mod has (<see cref="Guid.Empty"/>).</summary>
        public const string DefaultVariantName = "default";

        // Mod UUID
        [DataMember(Name = "uuid")]
        public Guid Uuid { get; set; }

        // Mod Basic Info
        [DataMember(Name = "name")]
        public string Name { get; set; }
        [DataMember(Name = "description")]
        public string Description { get; set; }
        [DataMember(Name = "authors")]
        public List<string> Authors { get; set; }

        [DataMember(Name = "contact")]
        public string Contact { get; set; }

        [DataMember(Name = "variants")]
        public Dictionary<Guid, string> Variants { get; set; }
        [DataMember(Name = "files")]
        public List<ModFile> ModFiles { get; set; }
        [DataMember(Name = "requiredMods")]
        public List<string> RequiredMods { get; set; }
        [DataMember(Name = "incompatibleMods")]
        public List<string> IncompatibleMods { get; set; }

        [DataMember(Name = "licenseName")]
        public string LicenseName { get; set; }
        [DataMember(Name = "licenseText")]
        public string LicenseText { get; set; }

        // Mod Version
        public Version Version { get; set; }
        [DataMember(Name = "buildDate")]
        public DateTime BuildDate { get; set; }

        /// <summary>
        /// <see cref="Version"/> as text (e.g. "1.0.0.0") in the data file, instead of the private fields of
        /// System.Version.
        /// </summary>
        [DataMember(Name = "version")]
        private string VersionText
        {
            get { return Version == null ? null : Version.ToString(); }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    Version = null;
                    return;
                }

                System.Version version;
                if (!TryParseVersion(value, out version))
                    throw new SerializationException("Invalid mod version \"" + value + "\".");
                Version = version;
            }
        }

        /// <summary>
        /// The rule for mod versions, shared by the mod creator (input of the author) and the mod archive
        /// reader: two to four non-negative numbers separated by dots, e.g. "1.0" or "1.2.3.4". White space
        /// around the text is ignored.
        /// </summary>
        /// <returns>false, and null in <paramref name="version"/>, if <paramref name="text"/> is not a valid version.</returns>
        public static bool TryParseVersion(string text, out Version version)
        {
            version = null;
            return !string.IsNullOrWhiteSpace(text) && Version.TryParse(text.Trim(), out version);
        }

        [DataMember(Name = "minWindows")]
        public WindowsVersion.WindowsVersionEnum MinWindows { get; set; }

        public ModData()
        {
            Uuid = Guid.NewGuid();
            Authors = new List<string>();
            Variants = new Dictionary<Guid, string> { { Guid.Empty, DefaultVariantName } };
            ModFiles = new List<ModFile>();
            RequiredMods = new List<string>();
            IncompatibleMods = new List<string>();
            BuildDate = DateTime.Now;
        }

        /// <summary>
        /// Deserialization does not run the constructor: tolerates collections missing from a data file.
        /// </summary>
        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            if (Authors == null)
                Authors = new List<string>();
            if (Variants == null)
                Variants = new Dictionary<Guid, string> { { Guid.Empty, DefaultVariantName } };
            if (ModFiles == null)
                ModFiles = new List<ModFile>();
            if (RequiredMods == null)
                RequiredMods = new List<string>();
            if (IncompatibleMods == null)
                IncompatibleMods = new List<string>();
        }

        public void ClearVariants()
        {
            Variants.ToList().ForEach(variant =>
            {
                if (variant.Key != Guid.Empty)
                    Variants.Remove(variant.Key);
            });
        }

        public void AddOrUpdateVariant(Guid uuid, string name)
        {
            Variants[uuid] = name;
        }

        /// <summary>
        /// Removes a variant from the mod, together with its entries in <see cref="ModFiles"/>. Its banners are
        /// kept in <see cref="ModAssets"/>; remove them there (<see cref="ModAssets.RemoveVariant"/>).
        /// </summary>
        /// <param name="uuid">The variant UUID</param>
        /// <returns>true if files of the variant were removed</returns>
        /// <exception cref="DataException">variant don't exist or trying to delete default variant</exception>
        public bool RemoveVariant(Guid uuid)
        {
            if (!Variants.ContainsKey(uuid))
                throw new DataException("Variant does not exist");
            if (uuid == Guid.Empty)
                throw new DataException("Unable to delete the default variant");
            bool hadFiles = ModFiles.RemoveAll(modFile => modFile.Variant == uuid) > 0;
            Variants.Remove(uuid);
            return hadFiles;
        }

        public bool DoesVariantExist(Guid variant)
        {
            return Variants.ContainsKey(variant);
        }

        /// <summary>
        /// The mod data as JSON, as stored in the <see cref="EemFormat.DataEntryName"/> entry of the archive.
        /// </summary>
        public override string ToString()
        {
            return DataContractJsonHelper<ModData>.Serialize(this);
        }
    }
}
