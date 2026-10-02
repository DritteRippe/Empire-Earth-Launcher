using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.GameSettings
{
    /// <summary>
    /// <see cref="DisplayQuestion.Combine"/>: the open question of the launcher start and the one of a first Play (L-WP6) are
    /// asked as one, each game of each installation once.
    /// </summary>
    [TestFixture]
    public class DisplayQuestionTests
    {
        private static Installation Installation(string root)
        {
            return new Installation(Product.NeoEE, root, root + @"\Empire Earth", root + @"\Empire Earth - The Art of Conquest",
                InstallationKind.Community, InstallMode.Admin, new[] { InstallationSource.RegistryRecord });
        }

        private static DisplayQuestion Question(string root, params Game[] games)
        {
            return new DisplayQuestion(games.Select(game => new DisplayQuestionItem(Installation(root), game, new ValueDifference[0])));
        }

        [Test]
        public void WithNull_TheOtherOne()
        {
            DisplayQuestion question = Question(@"C:\Neo", Game.EmpireEarth);

            Assert.That(DisplayQuestion.Combine(null, null), Is.Null);
            Assert.That(DisplayQuestion.Combine(question, null), Is.SameAs(question));
            Assert.That(DisplayQuestion.Combine(null, question), Is.SameAs(question));
        }

        [Test]
        public void EachGameOfEachInstallation_IsAskedOnce()
        {
            DisplayQuestion atStart = Question(@"C:\Neo", Game.EmpireEarth);
            DisplayQuestion atPlay = Question(@"c:\neo\", Game.EmpireEarth, Game.ArtOfConquest);
            DisplayQuestion other = Question(@"D:\Neo", Game.EmpireEarth);

            DisplayQuestion combined = DisplayQuestion.Combine(DisplayQuestion.Combine(atStart, atPlay), other);

            Assert.That(combined.Items.Select(item => item.Installation.Root + " " + item.Game.Id),
                Is.EqualTo(new[] { @"C:\Neo EE", @"c:\neo\ AoC", @"D:\Neo EE" }));
            Assert.That(combined.Items[0], Is.SameAs(atStart.Items[0]), "the first question of a game stays");
        }
    }
}
