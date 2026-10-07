using System;
using System.Linq;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Properties;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// The texts of the list of the four games on the Play page (launcher 1.1.0) in English, German and French: the names of the
    /// games are not translated and agree with <see cref="PlayEntry.EnglishName"/>, with the en dash where the two-line form
    /// splits them; the hint names the Launcher page by its translated name and the two ways to get a game that is greyed out.
    /// </summary>
    [TestFixture]
    public class PlayEntryTextsTests
    {
        private const string EnDash = "–";

        private static string[] Names()
        {
            return new[]
            {
                Resources.PlayEntryEmpireEarth, Resources.PlayEntryEmpireEarthAoc, Resources.PlayEntryNeoEE, Resources.PlayEntryNeoEEAoc
            };
        }

        [TestCase("en")]
        [TestCase("de")]
        [TestCase("fr")]
        public void TheNamesOfTheGames_AreInTheOrderOfThePage_AndNotTranslated(string lang)
        {
            using (TestUiLanguage.Use(lang))
            {
                Assert.That(Names(), Is.EqualTo(new[]
                {
                    "Empire Earth", "Empire Earth " + EnDash + " The Art of Conquest", "Neo Empire Earth",
                    "Neo Empire Earth " + EnDash + " The Art of Conquest"
                }));
                Assert.That(Names(), Is.EqualTo(PlayEntry.All.Select(entry => entry.EnglishName.Replace(" - ", " " + EnDash + " "))),
                    "the same names as the log line of a choice");
            }
        }

        [TestCase("en")]
        [TestCase("de")]
        [TestCase("fr")]
        public void TheNameOfAGame_WithTheArtOfConquest_HasOneEnDash_ToSplitAt(string lang)
        {
            using (TestUiLanguage.Use(lang))
            {
                foreach (string name in new[] { Resources.PlayEntryEmpireEarthAoc, Resources.PlayEntryNeoEEAoc })
                {
                    Assert.That(name.Split(new[] { " " + EnDash + " " }, StringSplitOptions.None), Has.Length.EqualTo(2), name);
                    Assert.That(name, Does.Not.Contain("-"), "no hyphen where the en dash splits the name");
                }
                foreach (string name in new[] { Resources.PlayEntryEmpireEarth, Resources.PlayEntryNeoEE })
                    Assert.That(name, Does.Not.Contain(EnDash), "a game without the expansion has nothing to split");
            }
        }

        [TestCase("en", "Greyed out games are not installed.")]
        [TestCase("de", "Ausgegraute Spiele sind nicht installiert.")]
        [TestCase("fr", "Les jeux grisés ne sont pas installés.")]
        public void TheHint_SaysWhyAGameIsGreyedOut_AndNamesTheTwoWaysToGetIt(string lang, string start)
        {
            using (TestUiLanguage.Use(lang))
            {
                string hint = Resources.PlayEntriesNotInstalledHint;

                Assert.That(hint, Does.StartWith(start));
                Assert.That(hint, Does.Contain("Empire Earth Community Setup"));
                Assert.That(hint, Does.Contain("The Art of Conquest").And.Contain("Neo Empire Earth"));
                Assert.That(hint, Does.Contain("CD").And.Contain("GOG"), "the other installations that the Launcher page chooses");
                Assert.That(hint, Does.Contain(Resources.NavigationLauncher), "the page is named as the navigation names it");
            }
        }

        [Test]
        public void TheEnglishHint_IsTheOneOfTheDesign()
        {
            using (TestUiLanguage.Use("en"))
                Assert.That(Resources.PlayEntriesNotInstalledHint, Is.EqualTo(
                    "Greyed out games are not installed. Empire Earth Community Setup installs Empire Earth with The Art of Conquest and " +
                    "Neo Empire Earth; another installation (CD, GOG) is chosen on the Launcher page."));
        }
    }
}
