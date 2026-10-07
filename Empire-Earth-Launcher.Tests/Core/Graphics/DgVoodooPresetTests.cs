using System.Linq;
using Empire_Earth_Launcher.Core.Graphics;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Graphics
{
    /// <summary>
    /// <see cref="DgVoodooPreset"/>: which keys of a <c>dgVoodoo.conf</c> still have the window settings of a setup before 1.1.0
    /// (setup ADR 0005, amendment 2026-10-07). The texts are synthetic, laid out like the files of the setups, never a copy.
    /// </summary>
    [TestFixture]
    public class DgVoodooPresetTests
    {
        /// <summary>The window keys of the preset of setup 1.0.0 (dgVoodoo 2.82.1): real fullscreen, Alt+Enter on, deferred switch.</summary>
        internal const string OldConf =
            "Version                              = 0x282\r\n" +
            "\r\n" +
            "[General]\r\n" +
            "OutputAPI                            = d3d11_fl10_1\r\n" +
            "FullScreenMode                       = true\r\n" +
            "\r\n" +
            "[DirectX]\r\n" +
            "AppControlledScreenMode             = true\r\n" +
            "DisableAltEnterToToggleScreenMode   = false\r\n" +
            "\r\n" +
            "[DirectXExt]\r\n" +
            "DeferredScreenModeSwitch            = true\r\n" +
            "\r\n" +
            "[GeneralExt]\r\n" +
            "WindowedAttributes\t\t\t= Borderless, AlwaysOnTop, FullscreenSize\r\n";

        /// <summary>The window keys of setup 1.1.0 (dgVoodoo 2.87.5): fake fullscreen, Alt+Enter off, no deferred switch.</summary>
        internal const string CurrentConf =
            "Version                              = 0x287\r\n" +
            "\r\n" +
            "[General]\r\n" +
            "OutputAPI                            = d3d11_fl10_1\r\n" +
            "FullScreenMode                       = true\r\n" +
            "\r\n" +
            "[DirectX]\r\n" +
            "AppControlledScreenMode             = true\r\n" +
            "DisableAltEnterToToggleScreenMode   = true\r\n" +
            "\r\n" +
            "[DirectXExt]\r\n" +
            "DeferredScreenModeSwitch            = false\r\n" +
            "\r\n" +
            "[GeneralExt]\r\n" +
            "FullscreenAttributes                = fake\r\n" +
            "WindowedAttributes\t\t\t= Borderless, AlwaysOnTop, FullscreenSize\r\n";

        private static string[] Outdated(string text)
        {
            return DgVoodooPreset.OutdatedKeys(DgVoodooConf.Parse(text)).Select(finding => finding.ToString()).ToArray();
        }

        [Test]
        public void TheConfOfSetup100_HasFourOutdatedKeys_InAFixedOrder()
        {
            Assert.That(Outdated(OldConf), Is.EqualTo(new[]
            {
                "Version = 0x282",
                "DeferredScreenModeSwitch = true",
                "DisableAltEnterToToggleScreenMode = false",
                "FullscreenAttributes = (not set)",
            }));
        }

        [Test]
        public void TheConfOfSetup110_HasNone()
        {
            Assert.That(Outdated(CurrentConf), Is.Empty);
        }

        [Test]
        public void ARealFullscreenConfWithAltEnterOn_HasTwoOutdatedKeys()
        {
            // The laptop test K2: real fullscreen at the panel resolution, Alt+Enter on, no fake fullscreen.
            string text = CurrentConf.Replace("DisableAltEnterToToggleScreenMode   = true", "DisableAltEnterToToggleScreenMode   = false")
                .Replace("FullscreenAttributes                = fake\r\n", string.Empty);

            Assert.That(Outdated(text), Is.EqualTo(new[] { "DisableAltEnterToToggleScreenMode = false", "FullscreenAttributes = (not set)" }));
        }

        [TestCase("0x287", false)]
        [TestCase("287", false)]
        [TestCase("0X287", false)]
        [TestCase("0x2A0", false)]
        [TestCase("0x300", false)]
        [TestCase("0x286", true)]
        [TestCase("0x282", true)]
        [TestCase("0x99", true)]
        [TestCase("garbage", true)]
        [TestCase("", true)]
        [TestCase("0x", true)]
        public void TheVersion_IsHexadecimalWithOrWithout0x_AndAtLeast0x287(string version, bool outdated)
        {
            string text = CurrentConf.Replace("0x287", version);

            Assert.That(DgVoodooPreset.OutdatedKeys(DgVoodooConf.Parse(text)).Any(finding => finding.Key == "Version"), Is.EqualTo(outdated), version);
        }

        [Test]
        public void AMissingVersion_IsOutdated_WithoutAValue()
        {
            PresetFinding finding = DgVoodooPreset.OutdatedKeys(DgVoodooConf.Parse(CurrentConf.Replace("Version                              = 0x287\r\n", string.Empty))).Single();

            Assert.That(finding.Key, Is.EqualTo("Version"));
            Assert.That(finding.Value, Is.Null);
        }

        [Test]
        public void AVersionInASection_IsNotTheVersionOfTheFile()
        {
            string text = CurrentConf.Replace("Version                              = 0x287\r\n", string.Empty).Replace("[General]\r\n", "[General]\r\nVersion = 0x287\r\n");

            Assert.That(Outdated(text), Is.EqualTo(new[] { "Version = (not set)" }));
        }

        [Test]
        public void KeysAndValuesInAnotherCase_AreTheSame()
        {
            string text = CurrentConf.ToUpperInvariant().Replace("0X287", "0x287");
            string old = OldConf.Replace("= true", "= TRUE").Replace("= false", "= False").ToLowerInvariant();

            Assert.That(Outdated(text), Is.Empty);
            Assert.That(Outdated(old).Select(finding => finding.Split(' ')[0]),
                Is.EqualTo(new[] { "Version", "DeferredScreenModeSwitch", "DisableAltEnterToToggleScreenMode", "FullscreenAttributes" }),
                "the findings name the keys the same way whatever the case in the file");
        }

        [Test]
        public void AMissingDeferredSwitchOrAltEnterKey_IsNotOutdated_BecauseTheDefaultsAreTheCurrentValues()
        {
            string text = CurrentConf.Replace("DeferredScreenModeSwitch            = false\r\n", string.Empty)
                .Replace("DisableAltEnterToToggleScreenMode   = true\r\n", string.Empty);

            Assert.That(Outdated(text), Is.Empty);
        }

        [TestCase("fake", false)]
        [TestCase("Fake, other", false)]
        [TestCase("Borderless, FAKE", false)]
        [TestCase(" fake ,Borderless", false)]
        [TestCase("borderless", true)]
        [TestCase("fakeish", true)]
        [TestCase("", true)]
        public void FullscreenAttributes_NeedsTheItemFake(string value, bool outdated)
        {
            string text = CurrentConf.Replace("= fake", "= " + value);

            Assert.That(DgVoodooPreset.OutdatedKeys(DgVoodooConf.Parse(text)).Any(finding => finding.Key == "FullscreenAttributes"), Is.EqualTo(outdated), value);
        }

        [Test]
        public void AFindingKeepsTheValueAsWritten()
        {
            PresetFinding finding = DgVoodooPreset.OutdatedKeys(DgVoodooConf.Parse(CurrentConf.Replace("= fake", "= Borderless"))).Single();

            Assert.That(finding.Key, Is.EqualTo("FullscreenAttributes"));
            Assert.That(finding.Value, Is.EqualTo("Borderless"));
        }

        [Test]
        public void AnEmptyConf_IsOutdated_InTwoKeys()
        {
            Assert.That(Outdated(string.Empty), Is.EqualTo(new[] { "Version = (not set)", "FullscreenAttributes = (not set)" }));
        }

        [Test]
        public void ANullConf_Throws()
        {
            Assert.That(() => DgVoodooPreset.OutdatedKeys(null), Throws.ArgumentNullException);
        }
    }
}
