using System;
using System.Globalization;
using Empire_Earth_Launcher.Core.Diagnostics;
using Empire_Earth_Launcher.Core.Mods;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// The texts of the Mods page (<see cref="Texts"/>, launcher 1.1.0): a preset with its badges, last edit, author and size, the
    /// choice of <c>dreXmod.config</c> and the list of a game folder. English is the neutral language; German and French have the
    /// same keys (<c>ResourceParityTests</c>) and are checked here for the parts that must not be lost in translation: the names
    /// of the folders, the numbers and the badges.
    /// </summary>
    [TestFixture]
    [SetUICulture("en-US")]
    public class ModsTextsTests
    {
        private static ModPreset Preset(string folder, string credits = null, long size = 18L * 1024 * 1024, bool complete = true)
        {
            return new ModPreset(folder, @"C:\Games\EE\Data\dxm\mods\" + folder, credits == null ? null : CreditsParser.Parse(credits), size, complete);
        }

        private static DreXmodConfig Config(string mod = "0", string modName = "dxm", string lobby = "1", string lobbyName = "dxm")
        {
            return DreXmodConfig.Parse(ModsModelWorld.Config(mod, modName, lobby, lobbyName));
        }

        // --- A preset ---------------------------------------------------------------------------------------------------

        [Test]
        public void APresetWithCredits_ShowsItsFolder_LastEdit_Author_AndSize()
        {
            ModPreset preset = Preset("yukon", ModsModelWorld.Credits("yukon", "21/10/2023", "author one, author two"));

            string text = Texts.ModsPreset(preset, Config());

            Assert.That(text, Is.EqualTo("yukon" + Environment.NewLine + "    last edit " +
                                         new DateTime(2023, 10, 21).ToString("d", CultureInfo.CurrentCulture) +
                                         "; created by author one, author two; size 18.0 MB"));
        }

        [Test]
        public void APresetWithoutCredits_ShowsOnlyTheFolderAndTheSize()
        {
            string text = Texts.ModsPreset(Preset("mine", size: 3 * 1024), Config());

            Assert.That(text, Is.EqualTo("mine" + Environment.NewLine + "    size 3 KB"));
        }

        [Test]
        public void TheNameOfTheCredits_FollowsTheFolder_UnlessItStartsWithIt()
        {
            string same = Texts.ModsPreset(Preset("dxm", ModsModelWorld.Credits("dxm (dreXmod)")), null);
            string other = Texts.ModsPreset(Preset("neo", ModsModelWorld.Credits("Classic Neo")), null);

            Assert.That(same, Does.StartWith("dxm (dreXmod)" + Environment.NewLine));
            Assert.That(other, Does.StartWith("neo (Classic Neo)" + Environment.NewLine));
        }

        [Test]
        public void ADateThatIsNoDate_IsShownAsWritten()
        {
            string text = Texts.ModsPreset(Preset("mine", ModsModelWorld.Credits("mine", "spring 2024")), null);

            Assert.That(text, Does.Contain("last edit spring 2024"));
        }

        [Test]
        public void TheBadges_FollowTheChoiceOfTheConfig_TheMatchIgnoresCase()
        {
            ModPreset yukon = Preset("Yukon");

            Assert.That(Texts.ModsPreset(yukon, Config("1", "yukon", "0", "dxm")), Does.StartWith("Yukon  [active mod]"));
            Assert.That(Texts.ModsPreset(yukon, Config("0", "yukon", "1", "YUKON")), Does.StartWith("Yukon  [active lobby theme]"));
            Assert.That(Texts.ModsPreset(yukon, Config("1", "yukon", "1", "yukon")), Does.StartWith("Yukon  [active mod, active lobby theme]"));
            Assert.That(Texts.ModsPreset(yukon, Config("0", "yukon", "0", "yukon")), Does.StartWith("Yukon" + Environment.NewLine),
                "a selector that is off names no active preset");
            Assert.That(Texts.ModsPreset(yukon, null), Does.StartWith("Yukon" + Environment.NewLine));
        }

        [Test]
        public void TheTemplate_IsMarked_AndKeepsItsFolderName()
        {
            string text = Texts.ModsPreset(Preset("template", ModsModelWorld.Credits("x", "XX/XX/XXXX", "x")), Config());

            Assert.That(text, Does.StartWith("template  [template for authors]" + Environment.NewLine));
        }

        [TestCase(0L, "1 KB")]
        [TestCase(1000L, "1 KB")]
        [TestCase(1536L, "2 KB")]
        [TestCase(1048575L, "1024 KB")]
        [TestCase(1048576L, "1.0 MB")]
        [TestCase(9648000L, "9.2 MB")]
        public void TheSize_IsInKilobytesBelowAMegabyte_AndInMegabytesAbove(long bytes, string expected)
        {
            Assert.That(Texts.ModsSize(Preset("x", size: bytes)), Is.EqualTo(expected));
        }

        [Test]
        public void ASizeThatIsOnlyALowerBound_SaysMoreThan()
        {
            Assert.That(Texts.ModsSize(Preset("x", size: 5L * 1024 * 1024, complete: false)), Is.EqualTo("more than 5.0 MB"));
        }

        // --- Languages --------------------------------------------------------------------------------------------------

        [TestCase("de", "aktiver Mod", "aktives Lobby-Theme", "zuletzt geändert", "erstellt von", "Größe")]
        [TestCase("fr", "mod actif", "thème de lobby actif", "dernière modification", "créé par", "taille")]
        public void ThePresetTexts_AreTranslated_TheNamesAndNumbersAreNot(string language, string mod, string lobby, string lastEdit, string by, string size)
        {
            using (TestUiLanguage.Use(language))
            {
                string text = Texts.ModsPreset(Preset("yukon", ModsModelWorld.Credits("yukon", "21/10/2023", "somebody"), 18L * 1024 * 1024),
                    Config("1", "yukon", "1", "yukon"));

                Assert.That(text, Does.StartWith("yukon  [" + mod + ", " + lobby + "]"));
                Assert.That(text, Does.Contain(lastEdit).And.Contain(by + " somebody").And.Contain(size + " 18"));
            }
        }

        [Test]
        public void TheSizeUnits_FollowTheLanguage()
        {
            using (TestUiLanguage.Use("fr"))
            {
                Assert.That(Texts.ModsSize(Preset("x", size: 2048)), Is.EqualTo("2 Ko"));
                Assert.That(Texts.ModsSize(Preset("x", size: 3L * 1024 * 1024)), Does.EndWith(" Mo"));
            }
        }
    }
}
