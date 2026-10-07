using System;
using Empire_Earth_Launcher.Core.Mods;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Mods
{
    /// <summary>
    /// <see cref="CreditsParser"/>: the head of the <c>CREDITS</c> file of a dreXmod preset. The file is free text whose own last
    /// lines say that its format may be changed completely, so the parser is tolerant. The texts are synthetic (written here,
    /// laid out like the files of the presets), never a copy of a real file.
    /// </summary>
    [TestFixture]
    public class CreditsParserTests
    {
        private const string Separator = "====================================================";

        private static string Credits(string name = "Name: sample (dreXmod)", string lastEdit = "Last Edit: 21/10/2023",
            string createdBy = "Created by: author one, author two")
        {
            return Separator + "\r\n\r\n" + name + "\r\n" + lastEdit + "\r\n" + createdBy + "\r\n\r\n" + Separator + "\r\n\r\n" +
                   "This mod and lobby theme has been possible by using the following resources:\r\n\r\n" +
                   "Mod:\r\n\t- Sounds\r\n\t\t- intro.mp3: remastered intro [somebody]\r\n";
        }

        [Test]
        public void TheThreeLines_AreRead()
        {
            ModCredits credits = CreditsParser.Parse(Credits());

            Assert.That(credits.Name, Is.EqualTo("sample (dreXmod)"));
            Assert.That(credits.LastEdit, Is.EqualTo("21/10/2023"));
            Assert.That(credits.LastEditDate, Is.EqualTo(new DateTime(2023, 10, 21)));
            Assert.That(credits.CreatedBy, Is.EqualTo("author one, author two"));
            Assert.That(credits.IsEmpty, Is.False);
        }

        [TestCase("21/10/2023", 2023, 10, 21)]
        [TestCase("1/2/2024", 2024, 2, 1)]
        [TestCase("05/03/2022", 2022, 3, 5)]
        [TestCase("21.10.2023", 2023, 10, 21)]
        [TestCase("2023-10-21", 2023, 10, 21)]
        public void TheDate_IsDayMonthYear_AndOtherCommonWritings(string text, int year, int month, int day)
        {
            ModCredits credits = CreditsParser.Parse(Credits(lastEdit: "Last Edit: " + text));

            Assert.That(credits.LastEditDate, Is.EqualTo(new DateTime(year, month, day)));
        }

        [TestCase("XX/XX/XXXX")]
        [TestCase("31/02/2023")]
        [TestCase("last autumn")]
        [TestCase("13/13/2023")]
        public void ADateThatIsNoDate_StaysText_WithoutADate(string text)
        {
            ModCredits credits = CreditsParser.Parse(Credits(lastEdit: "Last Edit: " + text));

            Assert.That(credits.LastEdit, Is.EqualTo(text));
            Assert.That(credits.LastEditDate, Is.Null);
        }

        [Test]
        public void AMissingLine_IsNull()
        {
            ModCredits credits = CreditsParser.Parse("Name: only a name\r\nand some free text\r\n");

            Assert.That(credits.Name, Is.EqualTo("only a name"));
            Assert.That(credits.LastEdit, Is.Null);
            Assert.That(credits.LastEditDate, Is.Null);
            Assert.That(credits.CreatedBy, Is.Null);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   \r\n\r\n")]
        [TestCase("Only free text. Nothing in the format of the header.\r\nA second line.")]
        public void WithoutTheLines_TheCreditsAreEmpty(string text)
        {
            ModCredits credits = CreditsParser.Parse(text);

            Assert.That(credits.IsEmpty, Is.True);
            Assert.That(credits.Name, Is.Null);
        }

        [Test]
        public void AnEmptyValue_IsMissing()
        {
            ModCredits credits = CreditsParser.Parse("Name:\r\nLast Edit:   \r\nCreated by:\r\n");

            Assert.That(credits.IsEmpty, Is.True);
        }

        [TestCase("\r\n")]
        [TestCase("\n")]
        [TestCase("\r")]
        public void EveryLineEnd_IsRead(string lineEnd)
        {
            ModCredits credits = CreditsParser.Parse(Credits().Replace("\r\n", lineEnd));

            Assert.That(credits.Name, Is.EqualTo("sample (dreXmod)"));
            Assert.That(credits.CreatedBy, Is.EqualTo("author one, author two"));
        }

        [Test]
        public void Keys_AreReadIgnoringCaseSpacesAndTheSeparator()
        {
            ModCredits credits = CreditsParser.Parse("  NAME : Spaced\r\n\tlast edit=2/1/2020\r\ncreated  BY:\ttabbed\r\n");

            Assert.That(credits.Name, Is.EqualTo("Spaced"));
            Assert.That(credits.LastEditDate, Is.EqualTo(new DateTime(2020, 1, 2)));
            Assert.That(credits.CreatedBy, Is.EqualTo("tabbed"));
        }

        [Test]
        public void AByteOrderMark_IsIgnored()
        {
            ModCredits credits = CreditsParser.Parse("\uFEFFName: with mark\r\n");

            Assert.That(credits.Name, Is.EqualTo("with mark"));
        }

        [Test]
        public void TheFirstLineOfAKind_Counts()
        {
            ModCredits credits = CreditsParser.Parse("Name: first\r\nName: second\r\nCreated by: a\r\nCreated by: b\r\n");

            Assert.That(credits.Name, Is.EqualTo("first"));
            Assert.That(credits.CreatedBy, Is.EqualTo("a"));
        }

        [Test]
        public void ALineOfTheFreeTextFarBelowTheHeader_IsNotTheName()
        {
            string text = new string('\n', CreditsParser.HeaderLines) + "Name: somebody in the resource list\r\n";

            Assert.That(CreditsParser.Parse(text).Name, Is.Null);
        }

        [Test]
        public void TheKeysOfFreeText_AreNotTakenFromTheMiddleOfALine()
        {
            ModCredits credits = CreditsParser.Parse("The Name: of the thing\r\nsee Created by: nobody\r\n");

            Assert.That(credits.IsEmpty, Is.True);
        }
    }
}
