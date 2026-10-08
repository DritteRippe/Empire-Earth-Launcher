using System;
using System.IO;
using Empire_Earth_Mod_Lib;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Mod
{
    /// <summary>
    /// Product folders, display paths and default file types of mod files, and the path helpers of the .eem
    /// format they rely on.
    /// </summary>
    /// <remarks>
    /// Paths are written with '/', which is a directory separator on Windows and on Mono/Linux, so the tests
    /// run on both.
    /// </remarks>
    [TestFixture]
    public class ModFileTests
    {
        private static ModFile FileAt(string relativePath)
        {
            return new ModFile(relativePath, ModFile.ModFileType.Data, Guid.Empty, string.Empty);
        }

        private static string OsPath(string path)
        {
            return path.Replace('/', Path.DirectorySeparatorChar);
        }

        /* Product of a file */

        [TestCase("EEC/Data/file.xml", ModFile.ModFileProduct.EEC)]
        [TestCase("AOC/Data/file.xml", ModFile.ModFileProduct.AOC)]
        [TestCase("all/Data/file.xml", ModFile.ModFileProduct.Both)]
        [TestCase("eec/Data/file.xml", ModFile.ModFileProduct.EEC)]
        [TestCase("Aoc/file.xml", ModFile.ModFileProduct.AOC)]
        [TestCase("ALL/Users/default/Civilizations/x.civ", ModFile.ModFileProduct.Both)]
        [TestCase("/EEC/Data/file.xml", ModFile.ModFileProduct.EEC)]
        public void GetProduct_FileInAProductFolder_IsCaseInsensitive(string relativePath, ModFile.ModFileProduct expected)
        {
            ModFile file = FileAt(relativePath);

            Assert.That(file.GetProduct(), Is.EqualTo(expected));
            ModFile.ModFileProduct product;
            Assert.That(file.TryGetProduct(out product), Is.True);
            Assert.That(product, Is.EqualTo(expected));
        }

        [Test]
        public void GetProduct_PathWithTheSeparatorOfTheOs_IsRecognized()
        {
            ModFile file = FileAt(Path.Combine("AOC", "Data", "file.xml"));

            Assert.That(file.GetProduct(), Is.EqualTo(ModFile.ModFileProduct.AOC));
        }

        [TestCase("Banner0.png")]
        [TestCase("readme.txt")]
        [TestCase("EEC")]
        [TestCase("EECX/Data/file.xml")]
        [TestCase("Data/EEC/file.xml")]
        [TestCase("Both/Data/file.xml")]
        public void GetProduct_FileOutsideAProductFolder_ThrowsInvalidOperation(string relativePath)
        {
            // Such files made the file list of the mod creator crash (korr-S15); now they are reported as
            // "not in a product folder" instead.
            ModFile file = FileAt(relativePath);

            ModFile.ModFileProduct product;
            Assert.That(file.TryGetProduct(out product), Is.False);
            Assert.That(() => file.GetProduct(), Throws.TypeOf<InvalidOperationException>());
        }

        [TestCase("EEC/Data/file.xml", "Data/file.xml")]
        [TestCase("all/file.xml", "file.xml")]
        [TestCase("/AOC/Data/Models/unit.mdl/", "Data/Models/unit.mdl")]
        [TestCase("readme.txt", "readme.txt")]
        public void GetPathInProduct_StripsTheProductFolder(string relativePath, string expected)
        {
            Assert.That(FileAt(relativePath).GetPathInProduct(), Is.EqualTo(OsPath(expected)));
        }

        /* Product folders */

        [Test]
        public void ProductFolderNames_RoundTripForEveryProduct()
        {
            foreach (ModFile.ModFileProduct product in Enum.GetValues(typeof(ModFile.ModFileProduct)))
            {
                string folder = ModFile.GetFolderName(product);
                Assert.That(EemFormat.ProductFolders, Does.Contain(folder));

                ModFile.ModFileProduct parsed;
                Assert.That(ModFile.TryParseFolderName(folder.ToUpperInvariant(), out parsed), Is.True, folder);
                Assert.That(parsed, Is.EqualTo(product));
            }
        }

        [Test]
        public void ProductFolderNames_BothIsStoredAsAll()
        {
            Assert.That(ModFile.GetFolderName(ModFile.ModFileProduct.Both), Is.EqualTo("all"));
            Assert.That(ModFile.GetFolderName(ModFile.ModFileProduct.EEC), Is.EqualTo("EEC"));
            Assert.That(ModFile.GetFolderName(ModFile.ModFileProduct.AOC), Is.EqualTo("AOC"));
        }

        [Test]
        public void ProductFolderNames_UnknownValues_AreRejected()
        {
            ModFile.ModFileProduct product;
            Assert.That(ModFile.TryParseFolderName("Both", out product), Is.False);
            Assert.That(ModFile.TryParseFolderName(null, out product), Is.False);
            Assert.That(() => ModFile.GetFolderName((ModFile.ModFileProduct)42), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        /* Default file type */

        [TestCase(".exe")]
        [TestCase(".EXE")]
        [TestCase(".Exe")]
        [TestCase(".dll")]
        [TestCase(".DLL")]
        [TestCase(".bat")]
        [TestCase(".Cmd")]
        [TestCase(".com")]
        [TestCase(".SCR")]
        [TestCase(".msi")]
        [TestCase(".PS1")]
        [TestCase(".vbs")]
        [TestCase(".Js")]
        public void GetDefaultModFileType_ExecutableExtension_IsDetectedCaseInsensitively(string extension)
        {
            // Upper-case extensions such as ".EXE" were classified as data before (korr-S17).
            Assert.That(ModFile.GetDefaultModFileType(extension), Is.EqualTo(ModFile.ModFileType.Executable));
        }

        [TestCase(".json")]
        [TestCase(".CFG")]
        [TestCase(".xml")]
        [TestCase(".Config")]
        [TestCase(".INI")]
        public void GetDefaultModFileType_ConfigExtension_IsDetectedCaseInsensitively(string extension)
        {
            Assert.That(ModFile.GetDefaultModFileType(extension), Is.EqualTo(ModFile.ModFileType.ConfigFile));
        }

        [TestCase(".txt")]
        [TestCase(".png")]
        [TestCase(".exe.txt")]
        [TestCase("exe")]
        [TestCase("")]
        [TestCase(null)]
        public void GetDefaultModFileType_OtherExtension_IsData(string extension)
        {
            Assert.That(ModFile.GetDefaultModFileType(extension), Is.EqualTo(ModFile.ModFileType.Data));
        }

        [Test]
        public void GetDefaultModFileType_UsesTheLastExtensionOfAFileName()
        {
            Assert.That(ModFile.GetDefaultModFileType(Path.GetExtension("setup.TXT.EXE")),
                Is.EqualTo(ModFile.ModFileType.Executable));
        }

        /* Display names */

        [Test]
        public void Descriptions_RoundTripForEveryFileTypeProductAndWindowsVersion()
        {
            // The mod creator shows the descriptions in its grids and parses the selection back.
            AssertDescriptionsRoundTrip<ModFile.ModFileType>();
            AssertDescriptionsRoundTrip<ModFile.ModFileProduct>();
            AssertDescriptionsRoundTrip<WindowsVersion.WindowsVersionEnum>();
        }

        private static void AssertDescriptionsRoundTrip<TEnum>() where TEnum : struct, Enum
        {
            foreach (TEnum value in Enum.GetValues(typeof(TEnum)))
            {
                string description = value.GetDescription();
                Assert.That(EnumExtensions.ParseDescription<TEnum>(description), Is.EqualTo(value), description);
            }
            Assert.That(() => EnumExtensions.ParseDescription<TEnum>("no such value"), Throws.TypeOf<ArgumentException>());
        }

        /* Entry names of the archive */

        [Test]
        public void ToEntryName_UsesSlashesAndNoLeadingSeparator()
        {
            string relativePath = Path.Combine(Path.DirectorySeparatorChar + "variant", "EEC", "file.xml");

            Assert.That(EemFormat.ToEntryName(relativePath), Is.EqualTo("variant/EEC/file.xml"));
            Assert.That(EemFormat.ToEntryName("variant/all/file.xml/"), Is.EqualTo("variant/all/file.xml"));
        }

        [Test]
        public void GetProductFolder_ReturnsTheCanonicalName()
        {
            Assert.That(EemFormat.GetProductFolder("eec/file.xml"), Is.EqualTo(EemFormat.ProductFolderEec));
            Assert.That(EemFormat.GetProductFolder("ALL/file.xml"), Is.EqualTo(EemFormat.ProductFolderBoth));
            Assert.That(EemFormat.GetProductFolder("Banner0.png"), Is.Null);
        }

        /* Paths of mod files: never outside the product folder */

        [TestCase("EEC/Data/units.xml")]
        [TestCase(@"EEC\Data\units.xml")]
        [TestCase("all/Data/db/dbobjects.dat")]
        [TestCase(@"AOC\Tools\Patch.EXE")]
        [TestCase("eec/file.xml")]
        [TestCase("ALL/Data/Random Map Scripts/Two Islands.es")]
        [TestCase("EEC/Users/default/Civilizations/Zoë.civ")]
        [TestCase("EEC/Data/.hidden")]
        [TestCase("EEC/Data/..units.xml")]
        [TestCase("EEC/Data/CONFIG.xml")]
        [TestCase("EEC/Data/COM10.dat")]
        [TestCase("EEC/Data/nul_sound.wav")]
        public void IsValidFilePath_RelativePathInAProductFolder_IsValid(string relativePath)
        {
            Assert.That(EemFormat.IsValidFilePath(relativePath), Is.True);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("EEC", TestName = "{m}(the product folder alone)")]
        [TestCase("EEC/", TestName = "{m}(an empty file name)")]
        [TestCase("readme.txt")]
        [TestCase("Data/EEC/file.xml")]
        [TestCase("Both/Data/file.xml")]
        [TestCase("/EEC/Data/file.xml")]
        [TestCase(@"\EEC\Data\file.xml")]
        [TestCase(@"\\server\share\EEC\file.xml")]
        [TestCase("C:/EEC/file.xml")]
        [TestCase(@"C:\Windows\System32\evil.dll")]
        [TestCase(@"EEC\..\..\..\Windows\System32\evil.dll")]
        [TestCase("EEC/../../evil.dll")]
        [TestCase("EEC/./file.xml")]
        [TestCase("EEC/Data/..")]
        [TestCase("EEC/Data/...")]
        [TestCase("EEC/Data/.. /evil.dll")]
        [TestCase("EEC//file.xml")]
        [TestCase("EEC/Data/file.xml:stream")]
        [TestCase("EEC/Data/file.")]
        [TestCase("EEC/Data/file ")]
        [TestCase("EEC/Data/a*b.xml")]
        [TestCase("EEC/Data/a?b.xml")]
        [TestCase("EEC/Data/a\"b.xml")]
        [TestCase("EEC/Data/<b>.xml")]
        [TestCase("EEC/Data/a|b.xml")]
        [TestCase("EEC/Data/a\tb.xml", TestName = "{m}(a control character)")]
        [TestCase("EEC/Data/CON")]
        [TestCase("EEC/Data/nul.txt")]
        [TestCase("EEC/Data/Com1.dat")]
        [TestCase("EEC/Data/aux .log")]
        [TestCase("all/LPT9")]
        public void IsValidFilePath_PathThatCanLeaveTheProductFolderOrNoWindowsName_IsInvalid(string relativePath)
        {
            // Such a path in the data of a mod archive would send the code that installs its files elsewhere (zip slip).
            Assert.That(EemFormat.IsValidFilePath(relativePath), Is.False);
        }

        [Test]
        public void IsValidFilePath_PathOfTheOs_IsValid()
        {
            Assert.That(EemFormat.IsValidFilePath(Path.Combine("all", "Data", "file.xml")), Is.True);
        }

        [Test]
        public void GetBannerFileName_NumbersFromZero()
        {
            Assert.That(EemFormat.GetBannerFileName(0), Is.EqualTo("Banner0.png"));
            Assert.That(EemFormat.GetBannerFileName(12), Is.EqualTo("Banner12.png"));
            Assert.That(() => EemFormat.GetBannerFileName(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }
}
