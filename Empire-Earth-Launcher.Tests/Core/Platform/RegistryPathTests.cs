using Empire_Earth_Launcher.Core.Platform;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Platform
{
    /// <summary>The canonical form of registry keys (<see cref="RegistryPath"/>, ADR 0007 amendment).</summary>
    [TestFixture]
    public class RegistryPathTests
    {
        private static string Canonical(string location)
        {
            return RegistryPath.Canonicalize(RegistryLocation.Parse(location)).ToString();
        }

        [TestCase(@"HKCU\Software\Sierra\CDKeys", @"HKCU\SOFTWARE\SIERRA\CDKEYS")]
        [TestCase(@"HKCU\software\sierra\cdkeys", @"HKCU\SOFTWARE\SIERRA\CDKEYS")]
        [TestCase(@"HKCU\Software/Sierra/CDKeys", @"HKCU\SOFTWARE\SIERRA\CDKEYS")]
        [TestCase(@"HKCU\\Software\\\Sierra\CDKeys\\", @"HKCU\SOFTWARE\SIERRA\CDKEYS")]
        [TestCase(@"HKCU\Software\WOW6432Node\Sierra\CDKeys", @"HKCU\SOFTWARE\SIERRA\CDKEYS")]
        [TestCase(@"HKLM64\Software\Sierra\CDKeys", @"HKLM64\SOFTWARE\SIERRA\CDKEYS")]
        [TestCase(@"HKLM64\Software\WOW6432Node\Sierra\CDKeys", @"HKLM32\SOFTWARE\SIERRA\CDKEYS")]
        [TestCase(@"HKLM64\Software\wow6432node\Wow6432Node\Sierra", @"HKLM32\SOFTWARE\SIERRA")]
        [TestCase(@"HKLM32\Software\WOW6432Node\Sierra\CDKeys", @"HKLM32\SOFTWARE\SIERRA\CDKEYS")]
        [TestCase(@"HKLM64\Software/WOW6432Node/Sierra", @"HKLM32\SOFTWARE\SIERRA")]
        [TestCase(@"HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE\Sierra\CDKeys", @"HKLM64\SOFTWARE\SIERRA\CDKEYS")]
        [TestCase(@"HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE\WOW6432Node\Sierra\CDKeys", @"HKLM32\SOFTWARE\SIERRA\CDKEYS")]
        [TestCase(@"HKCU\software/classes/virtualstore/machine/software/wow6432node/sierra", @"HKLM32\SOFTWARE\SIERRA")]
        [TestCase(@"HKCU\Software\Classes\VirtualStore\MACHINE", "HKLM64")]
        [TestCase(@"HKCU\Software\Classes\VirtualStore\MACHINE\SYSTEM\x", @"HKLM64\SYSTEM\X")]
        [TestCase(@"HKCU\Software\Classes\VirtualStore", @"HKCU\SOFTWARE\CLASSES\VIRTUALSTORE")]
        [TestCase(@"HKCU\Software\Classes\WOW6432Node\CLSID", @"HKCU\SOFTWARE\CLASSES\WOW6432NODE\CLSID")]
        [TestCase(@"HKLM64\Software\Classes\WOW6432Node", @"HKLM64\SOFTWARE\CLASSES\WOW6432NODE")]
        [TestCase(@"HKLM64\WOW6432Node\Software", @"HKLM64\WOW6432NODE\SOFTWARE")]
        [TestCase(@"HKLM64\Software\WOW6432Node", @"HKLM32\SOFTWARE")]
        [TestCase(@"HKU\S-1-5-21-1\Software\Sierra", @"HKU\S-1-5-21-1\SOFTWARE\SIERRA")]
        [TestCase("HKCU", "HKCU")]
        public void Canonicalize(string location, string expected)
        {
            Assert.That(Canonical(location), Is.EqualTo(expected));
        }

        [Test]
        [SetCulture("tr-TR")]
        public void Canonicalize_UpperCasesIndependentOfTheCulture()
        {
            // Turkish upper-cases "i" to the dotted capital I; the canonical form must not depend on that.
            Assert.That(Canonical(@"HKCU\software\sierra\cdkeys\initial"), Is.EqualTo(@"HKCU\SOFTWARE\SIERRA\CDKEYS\INITIAL"));
            Assert.That(Canonical(@"HKLM64\software\wow6432node\mad doc software"), Is.EqualTo(@"HKLM32\SOFTWARE\MAD DOC SOFTWARE"));
        }

        [Test]
        public void IsSameKey_ResolvesAliases()
        {
            Assert.That(RegistryPath.IsSameKey(RegistryLocation.Parse(@"HKLM64\Software\WOW6432Node\Sierra"),
                RegistryLocation.Parse(@"HKLM32\software\sierra")), Is.True);
            Assert.That(RegistryPath.IsSameKey(RegistryLocation.Parse(@"HKLM64\Software\Sierra"),
                RegistryLocation.Parse(@"HKLM32\Software\Sierra")), Is.False, "different views on 64-bit Windows");
        }

        [Test]
        public void IsSameOrBelow_ComparesCanonicalForms()
        {
            RegistryLocation cdKeys = RegistryLocation.Parse(@"HKLM32\Software\Sierra\CDKeys");

            Assert.That(RegistryPath.IsSameOrBelow(RegistryLocation.Parse(@"HKCU\Software\Classes\VirtualStore\MACHINE\SOFTWARE\WOW6432Node\Sierra\CDKeys\EE"), cdKeys), Is.True);
            Assert.That(RegistryPath.IsSameOrBelow(RegistryLocation.Parse(@"HKLM64\Software\WOW6432Node\Sierra\CDKeys"), cdKeys), Is.True);
            Assert.That(RegistryPath.IsSameOrBelow(RegistryLocation.Parse(@"HKLM32\Software\Sierra\CDKeys2"), cdKeys), Is.False);
            Assert.That(RegistryPath.IsSameOrBelow(RegistryLocation.Parse(@"HKLM32\Software\Sierra"), cdKeys), Is.False);
        }
    }
}
