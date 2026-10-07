using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Core.Integrity;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Integrity
{
    /// <summary>
    /// The file classes of contract 2.4: the lists of the code are exactly the rows of the table in <c>docs/CONTRACT.md</c>
    /// (the code row is <c>CodeFileExtensions</c> of the setup's <c>utils.iss</c>), and a path is classified by the extension
    /// of its last name, ignoring case.
    /// </summary>
    [TestFixture]
    public class FileClassifierTests
    {
        /// <summary>The extensions of a row of the table 2.4: <c>| `class` | `ext ext ...` | why |</c>.</summary>
        private static string[] ContractRow(string fileClass)
        {
            string contract = File.ReadAllText(RepositoryRoot.GetFullPath("docs/CONTRACT.md"));
            int section = contract.IndexOf("### 2.4 File classes", System.StringComparison.Ordinal);
            Assert.That(section, Is.GreaterThan(0), "section 2.4 of the contract");
            Match row = new Regex(@"^\| `" + fileClass + @"` \| `(?<extensions>[^`]*)` \|", RegexOptions.Multiline)
                .Match(contract, section);
            Assert.That(row.Success, Is.True, "the row " + fileClass + " of the table 2.4");
            return row.Groups["extensions"].Value.Split(' ');
        }

        [Test]
        [Category(TestCategories.SourceTree)]
        public void Section2_4_TheCodeRowOfTheContract_IsTheListOfTheCode()
        {
            Assert.That(FileClassifier.CodeExtensions, Is.EqualTo(ContractRow("code")), "same extensions in the same order");
        }

        [Test]
        [Category(TestCategories.SourceTree)]
        public void Section2_4_TheMutableRowOfTheContract_IsTheListOfTheCode()
        {
            Assert.That(FileClassifier.MutableExtensions, Is.EqualTo(ContractRow("mutable")));
            Assert.That(FileClassifier.MutableExtensions, Is.EqualTo(new[] { "cfg", "ini", "conf", "config", "log" }));
        }

        [Test]
        public void TheLists_AreLowercase_WithoutDuplicates_AndDisjoint()
        {
            string[] all = FileClassifier.CodeExtensions.Concat(FileClassifier.MutableExtensions).ToArray();

            Assert.That(all, Is.All.Matches<string>(extension => extension == extension.ToLowerInvariant() && extension.Length > 0));
            Assert.That(all, Is.Unique);
            Assert.That(FileClassifier.CodeExtensions.Count, Is.EqualTo(44));
        }

        [TestCase("Empire Earth/Empire Earth.exe", FileClass.Code)]
        [TestCase("Empire Earth/neoee.dll", FileClass.Code)]
        [TestCase("Empire Earth/Data/mods/file0001.ASI", FileClass.Code)]
        [TestCase("Empire Earth/Mss32/file0002.flt", FileClass.Code)]
        [TestCase("Empire Earth/Mss32/file0003.m3d", FileClass.Code)]
        [TestCase("Tools/readme.chm", FileClass.Code)]
        [TestCase("Empire Earth/Empire Earth.lnk", FileClass.Code)]
        [TestCase("Empire Earth/Empire Earth.cfg", FileClass.Mutable)]
        [TestCase("Empire Earth/NeoEE.cfg", FileClass.Mutable)]
        [TestCase("Empire Earth/dxwrapper.ini", FileClass.Mutable)]
        [TestCase("Empire Earth/x.CONF", FileClass.Mutable)]
        [TestCase("Empire Earth - The Art of Conquest/x.config", FileClass.Mutable)]
        [TestCase("Empire Earth/x.log", FileClass.Mutable)]
        [TestCase("Empire Earth/Data/Textures/file0004.tga", FileClass.Data)]
        [TestCase("Empire Earth/Data/file0005.ssa", FileClass.Data)]
        [TestCase("Empire Earth/readme", FileClass.Data)]
        [TestCase("Empire Earth/archive.exe.bak", FileClass.Data)]
        [TestCase("Empire Earth/exe", FileClass.Data)]
        [TestCase("Empire Earth.dir/file", FileClass.Data)]
        public void Section2_4_ByTheExtensionOfTheLastName(string path, FileClass expected)
        {
            Assert.That(FileClassifier.Classify(path), Is.EqualTo(expected));
        }

        [TestCase(@"C:\Games\EE\Empire Earth.exe", "exe")]
        [TestCase("a/b.DLL", "dll")]
        [TestCase("a/b.dll. ", "dll")]
        [TestCase("a/.exe", "exe")]
        [TestCase("a.b/c", "")]
        [TestCase("noextension", "")]
        public void ExtensionOf_LikeTheSetup(string path, string extension)
        {
            Assert.That(FileClassifier.ExtensionOf(path), Is.EqualTo(extension));
        }

        [Test]
        public void Names_AreThoseOfTheContract()
        {
            Assert.That(FileClassifier.Name(FileClass.Code), Is.EqualTo("code"));
            Assert.That(FileClassifier.Name(FileClass.Mutable), Is.EqualTo("mutable"));
            Assert.That(FileClassifier.Name(FileClass.Data), Is.EqualTo("data"));
        }
    }
}
