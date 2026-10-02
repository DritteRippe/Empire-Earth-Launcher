using Empire_Earth_Launcher.Core.Contract;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Contract
{
    /// <summary><see cref="Game.FromId"/>: the id saved as the last game in settings.json.</summary>
    [TestFixture]
    public class GameTests
    {
        [TestCase("EE", "EE")]
        [TestCase("AoC", "AoC")]
        [TestCase("aoc", "AoC")]
        [TestCase("", null)]
        [TestCase(null, null)]
        [TestCase("Empire Earth", null)]
        public void FromId_FindsTheGameIgnoringCase(string id, string expected)
        {
            Assert.That(Game.FromId(id)?.Id, Is.EqualTo(expected));
        }
    }
}
