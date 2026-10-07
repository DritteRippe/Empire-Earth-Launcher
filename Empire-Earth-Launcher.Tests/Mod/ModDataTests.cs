using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.Serialization;
using Empire_Earth_Mod_Lib;
using Empire_Earth_Mod_Lib.Serialization;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Mod
{
    /// <summary>
    /// Version rule, variants and JSON form of <see cref="ModData"/>.
    /// </summary>
    [TestFixture]
    public class ModDataTests
    {
        /* Version validation */

        [TestCase("1.0", 1, 0, -1, -1)]
        [TestCase("1.2.3", 1, 2, 3, -1)]
        [TestCase("1.2.3.4", 1, 2, 3, 4)]
        [TestCase("  2.5  ", 2, 5, -1, -1)]
        [TestCase("0.0.0.0", 0, 0, 0, 0)]
        [TestCase("2147483647.1", int.MaxValue, 1, -1, -1)]
        public void TryParseVersion_ValidVersion_IsAccepted(string text, int major, int minor, int build, int revision)
        {
            Version version;

            Assert.That(ModData.TryParseVersion(text, out version), Is.True);
            Assert.That(version.Major, Is.EqualTo(major));
            Assert.That(version.Minor, Is.EqualTo(minor));
            Assert.That(version.Build, Is.EqualTo(build));
            Assert.That(version.Revision, Is.EqualTo(revision));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("1")]
        [TestCase("v1.0")]
        [TestCase("1.0-beta")]
        [TestCase("1,0")]
        [TestCase("1..0")]
        [TestCase("1.-1")]
        [TestCase("1.2.3.4.5")]
        [TestCase("2147483648.0")]
        [TestCase("one.two")]
        public void TryParseVersion_InvalidVersion_IsRejectedWithoutException(string text)
        {
            // The mod creator used "new Version(text)", which threw on such input (korr-S20).
            Version version;

            Assert.That(ModData.TryParseVersion(text, out version), Is.False);
            Assert.That(version, Is.Null);
        }

        [Test]
        public void Json_VersionIsStoredAsText()
        {
            var mod = new ModData { Version = new Version(1, 2, 3) };

            string json = mod.ToString();

            Assert.That(json, Does.Contain("\"version\":\"1.2.3\""));
            Assert.That(json, Does.Not.Contain("k__BackingField"));
            Assert.That(DataContractJsonHelper<ModData>.Deserialize(json).Version, Is.EqualTo(new Version(1, 2, 3)));
        }

        [Test]
        public void Json_InvalidVersion_IsRejected()
        {
            string json = new ModData { Version = new Version(1, 0) }.ToString().Replace("\"1.0\"", "\"1.x\"");

            Assert.That(() => DataContractJsonHelper<ModData>.Deserialize(json),
                Throws.InstanceOf<SerializationException>());
        }

        [Test]
        public void Json_MissingOrEmptyVersion_GivesNoVersion()
        {
            Assert.That(DataContractJsonHelper<ModData>.Deserialize("{}").Version, Is.Null);
            Assert.That(DataContractJsonHelper<ModData>.Deserialize("{\"version\":\"\"}").Version, Is.Null);
            Assert.That(DataContractJsonHelper<ModData>.Deserialize("{\"version\":null}").Version, Is.Null);
        }

        [Test]
        public void Json_MissingCollections_AreCreatedOnDeserialization()
        {
            // Deserialization does not run the constructor (korr-S9).
            ModData mod = DataContractJsonHelper<ModData>.Deserialize("{\"name\":\"Mod\"}");

            Assert.That(mod.Name, Is.EqualTo("Mod"));
            Assert.That(mod.Authors, Is.Not.Null.And.Empty);
            Assert.That(mod.ModFiles, Is.Not.Null.And.Empty);
            Assert.That(mod.RequiredMods, Is.Not.Null.And.Empty);
            Assert.That(mod.IncompatibleMods, Is.Not.Null.And.Empty);
            Assert.That(mod.Variants, Is.EquivalentTo(new Dictionary<Guid, string> { { Guid.Empty, ModData.DefaultVariantName } }));
        }

        /* Variants */

        [Test]
        public void NewMod_HasOnlyTheDefaultVariant()
        {
            var mod = new ModData();

            Assert.That(mod.Variants.Keys, Is.EqualTo(new[] { Guid.Empty }));
            Assert.That(mod.Variants[Guid.Empty], Is.EqualTo(ModData.DefaultVariantName));
            Assert.That(mod.Uuid, Is.Not.EqualTo(Guid.Empty));
        }

        [Test]
        public void RemoveVariant_RemovesTheVariantAndOnlyItsFiles()
        {
            var mod = new ModData();
            Guid variant = Guid.NewGuid();
            Guid otherVariant = Guid.NewGuid();
            mod.AddOrUpdateVariant(variant, "HD");
            mod.AddOrUpdateVariant(otherVariant, "Classic");
            mod.ModFiles.Add(new ModFile("EEC/a.xml", ModFile.ModFileType.ConfigFile, variant, string.Empty));
            mod.ModFiles.Add(new ModFile("EEC/b.xml", ModFile.ModFileType.ConfigFile, otherVariant, string.Empty));
            mod.ModFiles.Add(new ModFile("all/c.xml", ModFile.ModFileType.ConfigFile, Guid.Empty, string.Empty));

            Assert.That(mod.RemoveVariant(variant), Is.True);

            Assert.That(mod.DoesVariantExist(variant), Is.False);
            Assert.That(mod.ModFiles.Select(file => file.RelativeFilePath), Is.EqualTo(new[] { "EEC/b.xml", "all/c.xml" }));
            Assert.That(mod.RemoveVariant(otherVariant), Is.True);
            Assert.That(mod.Variants.Keys, Is.EqualTo(new[] { Guid.Empty }));
        }

        [Test]
        public void RemoveVariant_WithoutFiles_ReturnsFalse()
        {
            var mod = new ModData();
            Guid variant = Guid.NewGuid();
            mod.AddOrUpdateVariant(variant, "HD");

            Assert.That(mod.RemoveVariant(variant), Is.False);
            Assert.That(mod.DoesVariantExist(variant), Is.False);
        }

        [Test]
        public void RemoveVariant_DefaultOrUnknownVariant_ThrowsDataException()
        {
            var mod = new ModData();

            Assert.That(() => mod.RemoveVariant(Guid.Empty), Throws.TypeOf<DataException>());
            Assert.That(() => mod.RemoveVariant(Guid.NewGuid()), Throws.TypeOf<DataException>());
            Assert.That(mod.DoesVariantExist(Guid.Empty), Is.True);
        }

        [Test]
        public void ClearVariants_KeepsTheDefaultVariant()
        {
            var mod = new ModData();
            mod.AddOrUpdateVariant(Guid.NewGuid(), "HD");
            mod.AddOrUpdateVariant(Guid.NewGuid(), "Classic");

            mod.ClearVariants();

            Assert.That(mod.Variants.Keys, Is.EqualTo(new[] { Guid.Empty }));
        }
    }
}
