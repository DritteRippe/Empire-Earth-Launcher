using System;
using System.Linq;
using Empire_Earth_Launcher.Core.Diagnostics;
using Empire_Earth_Launcher.Core.Lobby;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Diagnostics
{
    /// <summary>
    /// The outage hint (forum report section 8 row 9, not droppable, REV-05): every combination of the name lookup of the
    /// status server, the update API and the status server, and the link of the Play page for every state of the player
    /// list.
    /// </summary>
    [TestFixture]
    public class OutageHintTests
    {
        // DNS of the status server | update API | status server -> verdict
        [TestCase(true, UpdateApiAnswer.Answered, StatusServerAnswer.Answered, OutageVerdict.ServerAnswers)]
        [TestCase(true, UpdateApiAnswer.NoAnswer, StatusServerAnswer.Answered, OutageVerdict.ServerAnswers)]
        [TestCase(true, UpdateApiAnswer.NotAsked, StatusServerAnswer.Answered, OutageVerdict.ServerAnswers)]
        [TestCase(false, UpdateApiAnswer.Answered, StatusServerAnswer.Answered, OutageVerdict.ServerAnswers)]
        [TestCase(false, UpdateApiAnswer.NoAnswer, StatusServerAnswer.Answered, OutageVerdict.ServerAnswers)]
        [TestCase(false, UpdateApiAnswer.NotAsked, StatusServerAnswer.Answered, OutageVerdict.ServerAnswers)]
        [TestCase(true, UpdateApiAnswer.Answered, StatusServerAnswer.NoAnswer, OutageVerdict.ProbablyServerOutage)]
        [TestCase(true, UpdateApiAnswer.NoAnswer, StatusServerAnswer.NoAnswer, OutageVerdict.NoServerReached)]
        [TestCase(true, UpdateApiAnswer.NotAsked, StatusServerAnswer.NoAnswer, OutageVerdict.Undetermined)]
        [TestCase(false, UpdateApiAnswer.Answered, StatusServerAnswer.NoAnswer, OutageVerdict.ServerNameNotResolved)]
        [TestCase(false, UpdateApiAnswer.NoAnswer, StatusServerAnswer.NoAnswer, OutageVerdict.NoConnection)]
        [TestCase(false, UpdateApiAnswer.NotAsked, StatusServerAnswer.NoAnswer, OutageVerdict.NoConnection)]
        [TestCase(true, UpdateApiAnswer.Answered, StatusServerAnswer.NotConfigured, OutageVerdict.StatusServerNotConfigured)]
        [TestCase(true, UpdateApiAnswer.NoAnswer, StatusServerAnswer.NotConfigured, OutageVerdict.StatusServerNotConfigured)]
        [TestCase(true, UpdateApiAnswer.NotAsked, StatusServerAnswer.NotConfigured, OutageVerdict.StatusServerNotConfigured)]
        [TestCase(false, UpdateApiAnswer.Answered, StatusServerAnswer.NotConfigured, OutageVerdict.StatusServerNotConfigured)]
        [TestCase(false, UpdateApiAnswer.NoAnswer, StatusServerAnswer.NotConfigured, OutageVerdict.StatusServerNotConfigured)]
        [TestCase(false, UpdateApiAnswer.NotAsked, StatusServerAnswer.NotConfigured, OutageVerdict.StatusServerNotConfigured)]
        public void EveryCombination_HasItsVerdict(bool dns, UpdateApiAnswer updateApi, StatusServerAnswer status, OutageVerdict expected)
        {
            Assert.That(OutageHint.Evaluate(dns, updateApi, status), Is.EqualTo(expected));
        }

        /// <summary>The cases above are all 2 x 3 x 3 combinations; a new enum value would need a new row.</summary>
        [Test]
        public void TheTable_CoversEveryCombination()
        {
            Assert.That(Enum.GetValues(typeof(UpdateApiAnswer)).Length, Is.EqualTo(3));
            Assert.That(Enum.GetValues(typeof(StatusServerAnswer)).Length, Is.EqualTo(3));
            foreach (bool dns in new[] { true, false })
            foreach (UpdateApiAnswer api in Enum.GetValues(typeof(UpdateApiAnswer)))
            foreach (StatusServerAnswer status in Enum.GetValues(typeof(StatusServerAnswer)))
                Assert.That(OutageHint.Describe(OutageHint.Evaluate(dns, api, status)), Is.Not.Empty);
        }

        /// <summary>"probably a server outage, not your computer" exactly when DNS works, the update API answers and the server does not.</summary>
        [Test]
        public void TheOutageVerdict_NeedsAWorkingNameAndAnAnsweringUpdateApi()
        {
            var outages =
                from dns in new[] { true, false }
                from UpdateApiAnswer api in Enum.GetValues(typeof(UpdateApiAnswer))
                from StatusServerAnswer status in Enum.GetValues(typeof(StatusServerAnswer))
                where OutageHint.Evaluate(dns, api, status) == OutageVerdict.ProbablyServerOutage
                select dns + " " + api + " " + status;

            Assert.That(outages, Is.EqualTo(new[] { "True Answered NoAnswer" }));
            Assert.That(OutageHint.Describe(OutageVerdict.ProbablyServerOutage), Does.Contain("not this computer"));
            Assert.That(OutageHint.IsServerSide(OutageVerdict.ProbablyServerOutage), Is.True);
            Assert.That(OutageHint.IsServerSide(OutageVerdict.NoConnection), Is.False);
        }

        [TestCase(PlayerListStatus.Available, false)]
        [TestCase(PlayerListStatus.Unavailable, true)]
        [TestCase(PlayerListStatus.Stopped, false)]
        public void ThePlayerList_LinksToTheCheck_WhenItIsNotAvailable(PlayerListStatus status, bool expected)
        {
            Assert.That(OutageHint.LinksToNetworkCheck(status), Is.EqualTo(expected));
        }

        [Test]
        public void EveryPlayerListState_IsDecided()
        {
            Assert.That(Enum.GetValues(typeof(PlayerListStatus)).Cast<PlayerListStatus>().Count(OutageHint.LinksToNetworkCheck),
                Is.EqualTo(1));
        }
    }
}
