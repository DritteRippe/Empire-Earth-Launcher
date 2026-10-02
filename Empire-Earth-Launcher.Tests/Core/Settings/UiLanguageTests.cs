using System;
using System.Globalization;
using Empire_Earth_Launcher.Core.Settings;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Settings
{
    /// <summary>
    /// The UI language setting of settings.json (<see cref="UiLanguage"/>, ADR 0009): Windows language, English,
    /// German or French; anything else means the Windows language.
    /// </summary>
    [TestFixture]
    public class UiLanguageTests
    {
        [Test]
        public void Choices_AreWindowsEnglishGermanFrench_InTheOrderOfTheList()
        {
            Assert.That(UiLanguage.Choices, Is.EqualTo(new[] { "", "en", "de", "fr" }));
            Assert.That(UiLanguage.Windows, Is.Empty);
        }

        [TestCase(null, "")]
        [TestCase("", "")]
        [TestCase("  ", "")]
        [TestCase("en", "en")]
        [TestCase("de", "de")]
        [TestCase("fr", "fr")]
        [TestCase("DE", "de")]
        [TestCase(" Fr ", "fr")]
        public void KnownValues_AreNormalized(string setting, string expected)
        {
            Assert.That(UiLanguage.TryNormalize(setting, out string language), Is.True);
            Assert.That(language, Is.EqualTo(expected));
        }

        [TestCase("de-DE")]
        [TestCase("es")]
        [TestCase("pt-BR")]
        [TestCase("zh")]
        [TestCase("german")]
        public void UnknownValues_MeanTheWindowsLanguage(string setting)
        {
            Assert.That(UiLanguage.TryNormalize(setting, out string language), Is.False);
            Assert.That(language, Is.EqualTo(UiLanguage.Windows));
        }

        [Test]
        [SetCulture("tr-TR")]
        [SetUICulture("tr-TR")]
        public void Normalizing_DoesNotDependOnTheCulture()
        {
            // Turkish lower-cases "I" to a dotless i; the setting only has ASCII letters and uses the invariant culture.
            Assert.That(UiLanguage.TryNormalize("EN", out string language), Is.True);
            Assert.That(language, Is.EqualTo("en"));
        }

        [TestCase("", 0)]
        [TestCase("en", 1)]
        [TestCase("de", 2)]
        [TestCase("fr", 3)]
        [TestCase("es", -1)]
        public void IndexOf_FindsTheChoice(string language, int expected)
        {
            Assert.That(UiLanguage.IndexOf(language), Is.EqualTo(expected));
        }

        [Test]
        public void ToCulture_IsNullForWindows_AndTheNeutralCultureOtherwise()
        {
            Assert.That(UiLanguage.ToCulture(UiLanguage.Windows), Is.Null);
            Assert.That(UiLanguage.ToCulture("en"), Is.EqualTo(CultureInfo.GetCultureInfo("en")));
            Assert.That(UiLanguage.ToCulture("de").Name, Is.EqualTo("de"));
            Assert.That(UiLanguage.ToCulture("fr").Name, Is.EqualTo("fr"));
        }

        [Test]
        public void ToCulture_RefusesAValueThatWasNotNormalized()
        {
            Assert.That(() => UiLanguage.ToCulture("DE"), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => UiLanguage.ToCulture(null), Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }
}
