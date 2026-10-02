using System;
using System.Collections.Generic;
using Empire_Earth_Launcher.Core.Platform;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Platform
{
    /// <summary>
    /// Windows path rules as string logic (<see cref="WinPath"/>, ADR 0006). These tests run unchanged under Mono
    /// on Linux, where <see cref="System.IO.Path"/> knows neither drives nor <c>\</c>.
    /// </summary>
    [TestFixture]
    public class WinPathTests
    {
        [TestCase(@"C:\Games\Empire Earth", @"C:\Games\Empire Earth")]
        [TestCase(@"C:/Games/Empire Earth", @"C:\Games\Empire Earth")]
        [TestCase(@"C:\Games\\Empire Earth\\", @"C:\Games\Empire Earth")]
        [TestCase(@"C:\Games\Empire Earth\", @"C:\Games\Empire Earth")]
        [TestCase(@"  C:\Games\Empire Earth  ", @"C:\Games\Empire Earth")]
        [TestCase(@"C:\Games\.\Empire Earth", @"C:\Games\Empire Earth")]
        [TestCase(@"C:\Games\Old\..\Empire Earth", @"C:\Games\Empire Earth")]
        [TestCase(@"C:\..\..\Games", @"C:\Games")]
        [TestCase(@"C:\Games\Empire Earth.", @"C:\Games\Empire Earth")]
        [TestCase(@"C:\Games\Empire Earth. .\x", @"C:\Games\Empire Earth\x")]
        [TestCase(@"C:\Games\...\x", @"C:\x")]
        [TestCase(@"C:\", @"C:\")]
        [TestCase(@"C:", @"C:\")]
        [TestCase(@"c:\games", @"c:\games")]
        [TestCase(@"D:\Empire Earth", @"D:\Empire Earth")]
        [TestCase(@"\\server\share\Games\\EE\", @"\\server\share\Games\EE")]
        [TestCase(@"//server/share/Games", @"\\server\share\Games")]
        [TestCase(@"\\server\share\..\x", @"\\server\share\x")]
        [TestCase(@"\\server\share", @"\\server\share")]
        [TestCase(@"Empire Earth\Data", @"Empire Earth\Data")]
        [TestCase(@"..\Data", @"..\Data")]
        [TestCase(@"\Games\EE\", @"\Games\EE")]
        [TestCase(@"C:Games", @"C:Games")]
        [TestCase("", "")]
        public void Normalize(string path, string expected)
        {
            Assert.That(WinPath.Normalize(path), Is.EqualTo(expected));
        }

        [TestCase(@"C:\Games\Empire Earth", @"c:/games/empire earth/", true)]
        [TestCase(@"C:\Program Files (x86)\Neo Empire Earth", @"C:\PROGRAM FILES (X86)\NEO EMPIRE EARTH\", true)]
        [TestCase(@"C:\Games\EE", @"C:\Games\EE2", false)]
        [TestCase(@"C:\Games\EE", @"D:\Games\EE", false)]
        [TestCase(@"C:", @"C:\", true)]
        [TestCase(@"C:\Spiele\Müll", @"C:\SPIELE\MÜLL", true)]
        public void IsSamePath(string first, string second, bool expected)
        {
            Assert.That(WinPath.IsSamePath(first, second), Is.EqualTo(expected));
            Assert.That(WinPath.IsSamePath(second, first), Is.EqualTo(expected));
        }

        [Test]
        [SetCulture("tr-TR")]
        public void IsSamePath_IgnoresCaseIndependentOfTheCulture()
        {
            // Under tr-TR, culture-aware upper-casing maps "i" to the dotted capital I; ordinal comparison does not.
            Assert.That(WinPath.IsSamePath(@"C:\Spiele\Empire Earth\Data\initial.ini", @"C:\SPIELE\EMPIRE EARTH\DATA\INITIAL.INI"), Is.True);
            Assert.That(WinPath.Comparer.GetHashCode(@"c:\initial"), Is.EqualTo(WinPath.Comparer.GetHashCode(@"C:\INITIAL\")));
        }

        [Test]
        public void Comparer_MergesSpellingsOfTheSameRoot()
        {
            var roots = new HashSet<string>(WinPath.Comparer)
            {
                @"C:\Program Files (x86)\Neo Empire Earth",
                @"c:\program files (x86)\neo empire earth\",
                @"C:/Program Files (x86)//Neo Empire Earth",
                @"D:\Empire Earth",
            };

            Assert.That(roots, Has.Count.EqualTo(2));
            Assert.That(WinPath.Comparer.Equals(null, null), Is.True);
            Assert.That(WinPath.Comparer.Equals(@"C:\x", null), Is.False);
        }

        [TestCase(@"C:\Games", true)]
        [TestCase(@"c:", true)]
        [TestCase(@"\\server\share\x", true)]
        [TestCase(@"Games\EE", false)]
        [TestCase(@"\Games\EE", false)]
        [TestCase(@"C:Games", false)]
        [TestCase("", false)]
        public void IsFullyQualified(string path, bool expected)
        {
            Assert.That(WinPath.IsFullyQualified(path), Is.EqualTo(expected));
        }

        [TestCase(@"C:\Games", "C:")]
        [TestCase(@"d:\x", "d:")]
        [TestCase(@"C:", "C:")]
        [TestCase(@"\\server\share", null)]
        [TestCase(@"Games", null)]
        [TestCase(@"1:\Games", null)]
        public void GetDrive(string path, string expected)
        {
            Assert.That(WinPath.GetDrive(path), Is.EqualTo(expected));
        }

        [TestCase(@"C:\Games\Empire Earth", @"C:\Games")]
        [TestCase(@"C:\Games\Empire Earth\", @"C:\Games")]
        [TestCase(@"D:\Empire Earth", @"D:\")]
        [TestCase(@"C:\", null)]
        [TestCase(@"\\server\share\EE", @"\\server\share")]
        [TestCase(@"\\server\share", null)]
        [TestCase(@"Empire Earth\Data", @"Empire Earth")]
        [TestCase(@"Empire Earth", null)]
        public void GetParent(string path, string expected)
        {
            Assert.That(WinPath.GetParent(path), Is.EqualTo(expected));
        }

        [TestCase(@"C:\Games\Empire Earth\Empire Earth.exe", "Empire Earth.exe", ".exe")]
        [TestCase(@"C:\Games\Empire Earth\", "Empire Earth", "")]
        [TestCase(@"C:\Data\archive.tar.GZ", "archive.tar.GZ", ".GZ")]
        [TestCase(@"C:\", "", "")]
        public void GetFileNameAndExtension(string path, string name, string extension)
        {
            Assert.That(WinPath.GetFileName(path), Is.EqualTo(name));
            Assert.That(WinPath.GetExtension(path), Is.EqualTo(extension));
        }

        [TestCase(@"C:\Games\Neo Empire Earth", "Empire Earth", @"C:\Games\Neo Empire Earth\Empire Earth")]
        [TestCase(@"C:\Games\Neo Empire Earth\", @"Empire Earth\Data\", @"C:\Games\Neo Empire Earth\Empire Earth\Data")]
        [TestCase(@"C:\Games", "Empire Earth/Data/WONLobby Resources", @"C:\Games\Empire Earth\Data\WONLobby Resources")]
        [TestCase(@"D:", "Empire Earth", @"D:\Empire Earth")]
        [TestCase(@"D:\", "Empire Earth", @"D:\Empire Earth")]
        [TestCase(@"C:\Games", "", @"C:\Games")]
        [TestCase(@"\\server\share", "EE", @"\\server\share\EE")]
        public void Combine(string basePath, string relative, string expected)
        {
            Assert.That(WinPath.Combine(basePath, relative), Is.EqualTo(expected));
        }

        [TestCase(@"\Games")]
        [TestCase("/Games")]
        [TestCase(@"D:\Games")]
        [TestCase("D:Games")]
        public void Combine_RootedRelativePart_Throws(string relative)
        {
            // System.IO.Path.Combine would silently return the second path; for the core that is always a bug.
            Assert.That(() => WinPath.Combine(@"C:\Games", relative), Throws.ArgumentException);
        }

        [TestCase(@"C:\Games\EE\Data", @"C:\Games\EE", true)]
        [TestCase(@"c:\games\ee\data\x.ini", @"C:\GAMES\EE\", true)]
        [TestCase(@"C:\Games\EE", @"C:\Games\EE", false)]
        [TestCase(@"C:\Games\EE2", @"C:\Games\EE", false)]
        [TestCase(@"C:\Games\EE\..\x", @"C:\Games\EE", false)]
        [TestCase(@"C:\Games\EE\.\..\EE\x", @"C:\Games\EE", true)]
        [TestCase(@"C:\Games", @"C:\", true)]
        [TestCase(@"C:\", @"C:\", false)]
        [TestCase(@"D:\Games\EE\x", @"C:\Games\EE", false)]
        [TestCase(@"\\server\share\EE\x", @"\\server\share\EE", true)]
        [TestCase(@"EE\x", @"EE", false)]
        [TestCase(@"C:\Games\EE\x", @"EE", false)]
        public void IsBelow(string path, string directory, bool expected)
        {
            Assert.That(WinPath.IsBelow(path, directory), Is.EqualTo(expected));
        }

        [TestCase(@"C:\Games\EE", @"c:\games\ee\", true)]
        [TestCase(@"C:\Games\EE\x", @"C:\Games\EE", true)]
        [TestCase(@"C:\Games\EE2", @"C:\Games\EE", false)]
        [TestCase(@"EE", @"EE", false)]
        public void IsSameOrBelow(string path, string directory, bool expected)
        {
            Assert.That(WinPath.IsSameOrBelow(path, directory), Is.EqualTo(expected));
        }

        [TestCase("Empire Earth/Empire Earth.exe", @"C:\Games\Neo Empire Earth\Empire Earth\Empire Earth.exe")]
        [TestCase("Empire Earth - The Art of Conquest/Data/WONLobby Resources/_LobbyResource.cfg",
            @"C:\Games\Neo Empire Earth\Empire Earth - The Art of Conquest\Data\WONLobby Resources\_LobbyResource.cfg")]
        [TestCase("Tools/Diagnostic/EE-Diagnostic.exe", @"C:\Games\Neo Empire Earth\Tools\Diagnostic\EE-Diagnostic.exe")]
        [TestCase("..foo/bar.txt", @"C:\Games\Neo Empire Earth\..foo\bar.txt")]
        [TestCase("CONSOLE.txt", @"C:\Games\Neo Empire Earth\CONSOLE.txt")]
        public void ManifestPath_Valid(string manifestPath, string expected)
        {
            Assert.That(WinPath.TryResolveManifestPath(@"C:\Games\Neo Empire Earth", manifestPath, out string fullPath),
                Is.EqualTo(ManifestPathError.None));
            Assert.That(fullPath, Is.EqualTo(expected));
        }

        // Contract 2.2: "a path that is absolute, contains a drive, a :, a \ or a .. segment makes the whole manifest
        // invalid"; the launcher never opens a file outside the install root because of the manifest.
        [TestCase("", ManifestPathError.Empty)]
        [TestCase(null, ManifestPathError.Empty)]
        [TestCase("/Windows/System32/x.dll", ManifestPathError.Absolute)]
        [TestCase(@"\Windows\x.dll", ManifestPathError.Absolute)]
        [TestCase(@"\\server\share\x.dll", ManifestPathError.Absolute)]
        [TestCase("C:/Windows/x.dll", ManifestPathError.Drive)]
        [TestCase("c:x.dll", ManifestPathError.Drive)]
        [TestCase("Empire Earth/x.dll:stream", ManifestPathError.Colon)]
        [TestCase("1:/x.dll", ManifestPathError.Colon)]
        [TestCase(@"Empire Earth\x.dll", ManifestPathError.Backslash)]
        [TestCase(@"Empire Earth/..\..\x.dll", ManifestPathError.Backslash)]
        [TestCase("../x.dll", ManifestPathError.ParentSegment)]
        [TestCase("Empire Earth/../../x.dll", ManifestPathError.ParentSegment)]
        [TestCase("Empire Earth/..", ManifestPathError.ParentSegment)]
        [TestCase("./Empire Earth/x.dll", ManifestPathError.DotSegment)]
        [TestCase("Empire Earth/.../x.dll", ManifestPathError.DotSegment)]
        [TestCase("Empire Earth/.. /x.dll", ManifestPathError.DotSegment)]
        [TestCase("Empire Earth//x.dll", ManifestPathError.EmptySegment)]
        [TestCase("Empire Earth/", ManifestPathError.EmptySegment)]
        [TestCase("Empire Earth./x.dll", ManifestPathError.TrailingDotOrSpace)]
        [TestCase("Empire Earth /x.dll", ManifestPathError.TrailingDotOrSpace)]
        [TestCase("Empire Earth/x?.dll", ManifestPathError.InvalidCharacter)]
        [TestCase("Empire Earth/x*.dll", ManifestPathError.InvalidCharacter)]
        [TestCase("Empire Earth/x\t.dll", ManifestPathError.InvalidCharacter)]
        [TestCase("Empire Earth/x\0.dll", ManifestPathError.InvalidCharacter)]
        [TestCase("Empire Earth/CON", ManifestPathError.ReservedName)]
        [TestCase("Empire Earth/nul.txt", ManifestPathError.ReservedName)]
        [TestCase("COM1/x.dll", ManifestPathError.ReservedName)]
        [TestCase("Empire Earth/lpt9.log", ManifestPathError.ReservedName)]
        public void ManifestPath_Invalid(string manifestPath, ManifestPathError expected)
        {
            Assert.That(WinPath.TryResolveManifestPath(@"C:\Games\Neo Empire Earth", manifestPath, out string fullPath),
                Is.EqualTo(expected));
            Assert.That(fullPath, Is.Null);
        }

        [Test]
        public void ManifestPath_RootMustBeFullyQualified()
        {
            Assert.That(() => WinPath.TryResolveManifestPath(@"Games\EE", "x.dll", out _), Throws.ArgumentException);
        }

        [Test]
        public void ManifestPath_BelowADriveRoot()
        {
            Assert.That(WinPath.TryResolveManifestPath(@"D:", "Empire Earth/Empire Earth.exe", out string fullPath),
                Is.EqualTo(ManifestPathError.None));
            Assert.That(fullPath, Is.EqualTo(@"D:\Empire Earth\Empire Earth.exe"));
        }

        [Test]
        public void NullArguments_Throw()
        {
            Assert.That(() => WinPath.Normalize(null), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => WinPath.IsSamePath(null, "x"), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => WinPath.Combine(@"C:\", null), Throws.TypeOf<ArgumentNullException>());
            Assert.That(() => WinPath.IsBelow(null, @"C:\"), Throws.TypeOf<ArgumentNullException>());
        }
    }
}
