using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Empire_Earth_Launcher.Properties;
using Empire_Earth_Launcher.Tests.TestSupport;
using Krypton.Toolkit;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// Why the lobby profiles are missing, or the friends of the profile, is shown in a line of its own above "Profile:" on
    /// the Play page, not as the description in the heading of the player group: there the 210 pixels of the group left only
    /// "Kei" of "Kein Lobby-Profil gefunden" after "Spieler online (nicht verfügbar)" (bug report of 2026-10-03).
    /// </summary>
    [TestFixture]
    [Category(TestCategories.WinForms)]
    [Apartment(ApartmentState.STA)]
    [SetUICulture("de-DE")]
    public class LobbyStatusTests
    {
        [SetUp]
        public void SetUp()
        {
            WinForms.RequireDisplay();
        }

        private static T Field<T>(GeneralUserControl page, string name) where T : class
        {
            var field = typeof(GeneralUserControl).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            return (T)field.GetValue(page);
        }

        /// <summary>The layout with stand-ins for the page (the page itself needs Windows, see the next test).</summary>
        [Test]
        public void TheStateLine_TakesTheHeightOfItsText_FromThePlayerList()
        {
            using (var list = new Panel { Height = 208 })
            using (var status = new LauncherWrapLabel { AutoSize = false, Left = 6, Width = 194, LabelStyle = LabelStyle.NormalControl })
            {
                GeneralUserControl.ShowLobbyStatus(status, list, 208, Resources.InstallationsWaitingForSetup);

                // As high as the text needs, at most MaxLobbyStatusHeight (Mono draws with a larger font than Windows).
                Assert.That(status.Height,
                    Is.EqualTo(Math.Min(status.TextHeight(status.Width), GeneralUserControl.MaxLobbyStatusHeight)));
                Assert.That(status.Height, Is.GreaterThan(30), "several lines");
                Assert.That(list.Height, Is.EqualTo(208 - status.Height));
                Assert.That(status.Top, Is.EqualTo(list.Bottom));
                Assert.That(status.Bottom, Is.EqualTo(208), "where the player list ended before");

                GeneralUserControl.ShowLobbyStatus(status, list, 208, new string('x', 2000));
                Assert.That(status.Height, Is.EqualTo(GeneralUserControl.MaxLobbyStatusHeight), "at most four lines or so");

                GeneralUserControl.ShowLobbyStatus(status, list, 208, null);
                Assert.That(status.Text, Is.Empty);
                Assert.That(list.Height, Is.EqualTo(208));
            }
        }

        [Test]
        public void TheReason_IsShownAboveTheProfile_AndNotInTheHeading()
        {
            using (GeneralUserControl page = WinForms.CreateOrIgnore(() => new GeneralUserControl()))
            {
                var group = Field<KryptonGroupBox>(page, "neoOnlineKryptonGroupBox");
                var status = Field<LauncherWrapLabel>(page, "lobbyStatusLauncherWrapLabel");
                var players = Field<Control>(page, "onlinePlayersKryptonDataGridView");
                var profile = Field<Control>(page, "lobbyUserKryptonLabel");
                int listHeight = players.Height;

                // The longest German reason (a setup is running).
                page.ShowLobbyStatus(Resources.InstallationsWaitingForSetup);

                Assert.That(group.Values.Description, Is.Empty, "nothing next to the heading");
                Assert.That(status.Text, Is.EqualTo(Resources.InstallationsWaitingForSetup));
                Assert.That(status.Height,
                    Is.EqualTo(Math.Min(status.TextHeight(status.Width), GeneralUserControl.MaxLobbyStatusHeight)),
                    "as high as the text needs (with the font of Windows at 100 % three lines), at most about four lines");
                Assert.That(players.Bottom, Is.LessThanOrEqualTo(status.Top), "the player list makes room");
                Assert.That(status.Bottom, Is.LessThanOrEqualTo(profile.Top), "the line is above \"Profil:\"");

                page.ShowLobbyStatus(Resources.NoLobbyProfileFound);
                Assert.That(players.Height, Is.EqualTo(listHeight - status.Height), "one line takes only its own height");

                page.ShowLobbyStatus(null);
                Assert.That(status.Text, Is.Empty);
                Assert.That(players.Height, Is.EqualTo(listHeight), "without a state the list has its whole height again");
            }
        }
    }

    /// <summary>The Play page writes no text into the description of the heading of the player group (see <see cref="LobbyStatusTests"/>).</summary>
    [TestFixture]
    [Category(TestCategories.SourceTree)]
    public class LobbyStatusSourceTests
    {
        [Test]
        public void ThePlayPage_SetsNoDescriptionOfAGroupHeading()
        {
            string code = File.ReadAllText(RepositoryRoot.GetFullPath("Empire Earth Launcher/GeneralUserControl.cs"));

            Assert.That(Regex.Matches(code, @"\.Values\.Description\s*=").Count, Is.EqualTo(0),
                "the description shares the heading line of the 210 pixel wide group; use ShowLobbyStatus");
        }
    }
}
