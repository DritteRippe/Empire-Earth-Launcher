using System.Linq;
using System.Text;
using Empire_Earth_Launcher.Core.Integrity;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Integrity
{
    /// <summary>
    /// <see cref="ManifestReader"/> against contract 2.2: what readers MUST accept (BOM, LF, CRLF, uppercase hex digits, the
    /// binary marker, empty lines) and what makes the whole manifest invalid (a line of another form, an unsafe path).
    /// Hashes are synthetic (<see cref="SampleHashes"/>).
    /// </summary>
    [TestFixture]
    public class ManifestReaderTests
    {
        private static readonly string H1 = SampleHashes.Of(1);
        private static readonly string H2 = SampleHashes.Of(2);
        private static readonly string H3 = SampleHashes.Of(3);

        private static ManifestParseResult Parse(string text)
        {
            return ManifestReader.Parse(Encoding.UTF8.GetBytes(text));
        }

        [Test]
        public void Section2_2_TheFormatOfTheSetup_LfAndTwoSpaces()
        {
            ManifestParseResult result = Parse(
                H1 + "  Empire Earth/Empire Earth.exe\n" +
                H2 + "  Empire Earth - The Art of Conquest/Data/WONLobby Resources/_LobbyResource.cfg\n" +
                H3 + "  Tools/Diagnostic/file0001.dat\n");

            Assert.That(result.IsValid, Is.True, result.ToString());
            Assert.That(result.Entries.Select(entry => entry.Path), Is.EqualTo(new[]
            {
                "Empire Earth/Empire Earth.exe", "Empire Earth - The Art of Conquest/Data/WONLobby Resources/_LobbyResource.cfg",
                "Tools/Diagnostic/file0001.dat"
            }));
            Assert.That(result.Entries.Select(entry => entry.Hash), Is.EqualTo(new[] { H1, H2, H3 }));
            Assert.That(result.Entries.Select(entry => entry.Class), Is.EqualTo(new[] { FileClass.Code, FileClass.Mutable, FileClass.Data }));
            Assert.That(result.ToString(), Is.EqualTo("3 files"));
        }

        [Test]
        public void Section2_2_BomCrlfUppercaseAndBinaryMarker_AreAccepted()
        {
            byte[] bom = { 0xEF, 0xBB, 0xBF };
            byte[] text = Encoding.ASCII.GetBytes(H1.ToUpperInvariant() + " *Empire Earth/Empire Earth.exe\r\n" + H2 + "  b.dll\r\n");

            ManifestParseResult result = ManifestReader.Parse(bom.Concat(text).ToArray());

            Assert.That(result.IsValid, Is.True, result.ToString());
            Assert.That(result.Entries[0].Hash, Is.EqualTo(H1), "hashes are kept lowercase");
            Assert.That(result.Entries[0].Path, Is.EqualTo("Empire Earth/Empire Earth.exe"), "the binary marker is not part of the path");
            Assert.That(result.Entries[1].Path, Is.EqualTo("b.dll"));
        }

        [Test]
        public void Section2_2_EmptyLines_AreIgnored_AlsoAtTheEndAndWithCr()
        {
            ManifestParseResult result = Parse("\n" + H1 + "  a.dll\n\n\r\n" + H2 + "  b.dll");

            Assert.That(result.IsValid, Is.True, result.ToString());
            Assert.That(result.Entries.Count, Is.EqualTo(2), "the last line needs no line end");
            Assert.That(Parse(string.Empty).Entries, Is.Empty);
            Assert.That(Parse(string.Empty).IsValid, Is.True, "an empty manifest lists no file (the state is OK then)");
        }

        [TestCase("{0} a.dll", Description = "one space")]
        [TestCase("{0}  ", Description = "no path after two spaces")]
        [TestCase("{0}\ta.dll", Description = "a tab")]
        [TestCase("{0}x  a.dll", Description = "65 characters")]
        [TestCase("  a.dll", Description = "no hash")]
        [TestCase("not a manifest line", Description = "text")]
        [TestCase(" {0}  a.dll", Description = "leading space")]
        [TestCase("{0} -a.dll", Description = "another marker")]
        [TestCase("   ", Description = "white space only")]
        public void Section2_2_ALineOfAnotherForm_MakesTheWholeManifestInvalid(string line)
        {
            string shortHash = H1.Substring(1);
            string invalid = string.Format(line, H1);
            ManifestParseResult result = Parse(H2 + "  good.dll\n" + invalid + "\n" + H3 + "  other.dll\n");

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Problem, Is.EqualTo(ManifestProblem.InvalidLine));
            Assert.That(result.LineNumber, Is.EqualTo(2));
            Assert.That(result.Entries, Is.Empty, "no entry of an invalid manifest is used");
            Assert.That(Parse(shortHash + "  a.dll").Problem, Is.EqualTo(ManifestProblem.InvalidLine), "63 hex digits");
            Assert.That(Parse(H1.Substring(0, 63) + "g  a.dll").Problem, Is.EqualTo(ManifestProblem.InvalidLine), "not hex");
        }

        [TestCase("/Empire Earth/Empire Earth.exe", ManifestPathError.Absolute)]
        [TestCase("C:/Windows/system32/x.dll", ManifestPathError.Drive)]
        [TestCase("C:x.dll", ManifestPathError.Drive)]
        [TestCase("a/b:stream", ManifestPathError.Colon)]
        [TestCase(@"Empire Earth\Empire Earth.exe", ManifestPathError.Backslash)]
        [TestCase(@"\\server\share\x.dll", ManifestPathError.Absolute)]
        [TestCase("../outside.dll", ManifestPathError.ParentSegment)]
        [TestCase("Empire Earth/../../outside.dll", ManifestPathError.ParentSegment)]
        [TestCase("./a.dll", ManifestPathError.DotSegment)]
        [TestCase("a//b.dll", ManifestPathError.EmptySegment)]
        [TestCase("a/", ManifestPathError.EmptySegment)]
        [TestCase("a/b.dll.", ManifestPathError.TrailingDotOrSpace)]
        [TestCase("a/NUL.txt", ManifestPathError.ReservedName)]
        [TestCase("a/b?.dll", ManifestPathError.InvalidCharacter)]
        [TestCase("a/b\r.dll", ManifestPathError.InvalidCharacter)]
        [TestCase(" ", ManifestPathError.DotSegment)]
        public void Section2_2_AnUnsafePath_MakesTheWholeManifestInvalid(string path, ManifestPathError error)
        {
            ManifestParseResult result = Parse(H1 + "  good.dll\n" + H2 + "  " + path + "\n");

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Problem, Is.EqualTo(ManifestProblem.UnsafePath));
            Assert.That(result.PathError, Is.EqualTo(error));
            Assert.That(result.LineNumber, Is.EqualTo(2));
            Assert.That(result.Entries, Is.Empty);
            Assert.That(result.ToString(), Does.StartWith("UnsafePath (" + error + ") in line 2"));
        }

        [Test]
        public void Section2_2_APathListedTwice_IgnoringCase_IsInvalid()
        {
            ManifestParseResult result = Parse(H1 + "  Empire Earth/a.dll\n" + H2 + "  empire earth/A.DLL\n");

            Assert.That(result.Problem, Is.EqualTo(ManifestProblem.DuplicatePath));
            Assert.That(result.LineNumber, Is.EqualTo(2));
        }

        [Test]
        public void BytesThatAreNotUtf8_AreInvalid()
        {
            byte[] bytes = Encoding.ASCII.GetBytes(H1 + "  a").Concat(new byte[] { 0xFF, 0xFE }).ToArray();

            ManifestParseResult result = ManifestReader.Parse(bytes);

            Assert.That(result.Problem, Is.EqualTo(ManifestProblem.Encoding));
            Assert.That(result.ToString(), Is.EqualTo("not UTF-8"));
        }

        [Test]
        public void AnInvalidLine_IsShortenedForTheLog()
        {
            ManifestParseResult result = Parse(new string('x', 500));

            Assert.That(result.Line.Length, Is.EqualTo(203));
            Assert.That(result.Line, Does.EndWith("..."));
        }

        [Test]
        public void ThePathsOfTheReader_AreTheChecksOfWinPath()
        {
            // Defence in depth: every path the reader accepts also resolves below the root (contract 2.2).
            ManifestParseResult result = Parse(H1 + "  Empire Earth/Data/Saved Games/x.ees\n" + H2 + "  _readme.txt\n");

            foreach (ManifestEntry entry in result.Entries)
            {
                Assert.That(WinPath.TryResolveManifestPath(@"D:\Games\Neo Empire Earth", entry.Path, out string full),
                    Is.EqualTo(ManifestPathError.None));
                Assert.That(WinPath.IsBelow(full, @"D:\Games\Neo Empire Earth"), Is.True);
            }
        }
    }
}
