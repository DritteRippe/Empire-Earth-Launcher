using System;
using Empire_Earth_Launcher.Core.Platform;
using Microsoft.Win32;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Platform
{
    /// <summary>Registry keys with an explicit view (<see cref="RegistryLocation"/>, contract 0, ADR 0006).</summary>
    [TestFixture]
    public class RegistryLocationTests
    {
        [Test]
        public void LocalMachine_NeedsAnExplicitView()
        {
            Assert.That(() => new RegistryLocation(RegistryHive.LocalMachine, RegistryView.Default, "Software"),
                Throws.ArgumentException);
            Assert.That(RegistryLocation.LocalMachine32("Software").View, Is.EqualTo(RegistryView.Registry32));
            Assert.That(RegistryLocation.LocalMachine64("Software").View, Is.EqualTo(RegistryView.Registry64));
        }

        [Test]
        public void CurrentUser_IsTheSameInBothViews()
        {
            var view32 = new RegistryLocation(RegistryHive.CurrentUser, RegistryView.Registry32, @"Software\Neo");
            var view64 = new RegistryLocation(RegistryHive.CurrentUser, RegistryView.Registry64, @"Software\Neo");

            Assert.That(view32.View, Is.EqualTo(RegistryView.Default));
            Assert.That(view32, Is.EqualTo(view64));
            Assert.That(view32, Is.EqualTo(RegistryLocation.CurrentUser(@"software\neo")), "names ignore case");
        }

        [Test]
        public void Path_LosesEmptySegmentsButKeepsItsSpelling()
        {
            var location = RegistryLocation.CurrentUser(@"\Software\\Mad Doc Software/x\EE-AOC\");

            Assert.That(location.Path, Is.EqualTo(@"Software\Mad Doc Software/x\EE-AOC"), "/ is a valid character in key names");
            Assert.That(location.Segments, Is.EqualTo(new[] { "Software", "Mad Doc Software/x", "EE-AOC" }));
        }

        [Test]
        public void ParentAndChild()
        {
            RegistryLocation key = RegistryLocation.LocalMachine32(@"Software\SSSI\Empire Earth");

            Assert.That(key.Parent, Is.EqualTo(RegistryLocation.LocalMachine32(@"Software\SSSI")));
            Assert.That(key.Parent.Parent.Parent.IsRoot, Is.True);
            Assert.That(key.Parent.Parent.Parent.Parent, Is.Null);
            Assert.That(key.Child(@"Game Options"), Is.EqualTo(RegistryLocation.LocalMachine32(@"Software\SSSI\Empire Earth\Game Options")));
            Assert.That(key.Parent.Parent.Parent.Child("Software"), Is.EqualTo(RegistryLocation.LocalMachine32("Software")));
            Assert.That(key.Parent, Is.Not.EqualTo(RegistryLocation.LocalMachine64(@"Software\SSSI")), "other view");
        }

        [TestCase(@"HKCU\Software\Neo\Empire Earth", RegistryHive.CurrentUser, RegistryView.Default, @"Software\Neo\Empire Earth")]
        [TestCase(@"HKEY_CURRENT_USER\Software", RegistryHive.CurrentUser, RegistryView.Default, "Software")]
        [TestCase(@"hklm64\SOFTWARE\Sierra", RegistryHive.LocalMachine, RegistryView.Registry64, @"SOFTWARE\Sierra")]
        [TestCase(@"HKLM32\Software", RegistryHive.LocalMachine, RegistryView.Registry32, "Software")]
        [TestCase(@"HKU\S-1-5-21-1\Software", RegistryHive.Users, RegistryView.Default, @"S-1-5-21-1\Software")]
        [TestCase(@"HKCR\.eem", RegistryHive.ClassesRoot, RegistryView.Default, ".eem")]
        [TestCase("HKCU", RegistryHive.CurrentUser, RegistryView.Default, "")]
        public void Parse(string text, RegistryHive hive, RegistryView view, string path)
        {
            RegistryLocation location = RegistryLocation.Parse(text);

            Assert.That(location.Hive, Is.EqualTo(hive));
            Assert.That(location.View, Is.EqualTo(view));
            Assert.That(location.Path, Is.EqualTo(path));
        }

        [TestCase(@"HKLM\Software")]
        [TestCase(@"HKEY_LOCAL_MACHINE\Software")]
        [TestCase(@"Software\Neo")]
        [TestCase("")]
        [TestCase(null)]
        public void Parse_RefusesUnknownHivesAndHklmWithoutView(string text)
        {
            Assert.That(RegistryLocation.TryParse(text, out RegistryLocation location), Is.False);
            Assert.That(location, Is.Null);
            Assert.That(() => RegistryLocation.Parse(text), Throws.TypeOf<FormatException>());
        }

        [TestCase(@"HKCU\Software\Neo")]
        [TestCase(@"HKLM64\Software\Empire Earth Community\Installations\NeoEE")]
        [TestCase(@"HKLM32\Software\SSSI")]
        [TestCase(@"HKU\S-1-5-18")]
        [TestCase("HKLM32")]
        public void ToString_IsTheParsedText(string text)
        {
            Assert.That(RegistryLocation.Parse(text).ToString(), Is.EqualTo(text));
        }
    }
}
