using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Properties;
using Empire_Earth_Launcher.Tests.TestSupport;
using Krypton.Toolkit;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// The list of the four games on the Play page (launcher 1.1.0), driven through the states of <see cref="PlayPageWorld"/>: the
    /// radio buttons are the four games in the order of <see cref="PlayEntry.All"/>, stacked one below the other, with the texts of
    /// the language; a game that is not installed is disabled, the hint is shown only then, and the Play button comes after the list
    /// in the tab order. The geometry (nothing overlaps or is cut off, also in the two-line form of a long name) is checked by
    /// <c>PageLayoutTests</c> for the same states. These tests need Windows libraries for the Krypton controls and run on Windows.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.WinForms)]
    [Apartment(ApartmentState.STA)]
    public class PlayPageEntriesTests
    {
        private IDisposable language;

        [SetUp]
        public void SetUp()
        {
            WinForms.RequireDisplay();
        }

        [TearDown]
        public void TearDown()
        {
            language?.Dispose();
            language = null;
        }

        private static T Get<T>(Control page, string field) where T : class
        {
            FieldInfo info = typeof(GeneralUserControl).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, "GeneralUserControl has no field " + field);
            return (T)info.GetValue(page);
        }

        private static KryptonRadioButton[] Radios(Control page)
        {
            return new[]
            {
                Get<KryptonRadioButton>(page, "empireEarthKryptonRadioButton"),
                Get<KryptonRadioButton>(page, "empireEarthAocKryptonRadioButton"),
                Get<KryptonRadioButton>(page, "neoEmpireEarthKryptonRadioButton"),
                Get<KryptonRadioButton>(page, "neoEmpireEarthAocKryptonRadioButton"),
            };
        }

        [TestCase("en")]
        [TestCase("de")]
        [TestCase("fr")]
        public void TheFourGames_AreStackedInTheOrderOfThePage_WithTheirNames(string lang)
        {
            language = TestUiLanguage.Use(lang);
            using (PlayPageWorld world = WinForms.CreateOrIgnore(() => PlayPageWorld.In(PlayPageState.AllEntries)))
            {
                KryptonRadioButton[] radios = Radios(world.Page);

                Assert.That(radios.Select(radio => radio.Values.Text.Replace(Environment.NewLine, " ")), Is.EqualTo(new[]
                {
                    Resources.PlayEntryEmpireEarth, Resources.PlayEntryEmpireEarthAoc, Resources.PlayEntryNeoEE, Resources.PlayEntryNeoEEAoc
                }), "the name is on one line or, if it does not fit, split into two after the en dash");
                Assert.That(radios.Select(radio => radio.Top), Is.Ordered.Ascending.And.Unique, "one below the other");
                Assert.That(radios.Select(radio => radio.Left).Distinct().Count(), Is.EqualTo(1), "one column");
                Assert.That(radios.All(radio => radio.Parent == radios[0].Parent), Is.True, "one radio group");
            }
        }

        [Test]
        public void AllEntries_AreEnabled_TheLongestIsChecked_AndThereIsNoHint()
        {
            language = TestUiLanguage.Use("en");
            using (PlayPageWorld world = WinForms.CreateOrIgnore(() => PlayPageWorld.In(PlayPageState.AllEntries)))
            {
                KryptonRadioButton[] radios = Radios(world.Page);

                Assert.That(radios.Select(radio => radio.Enabled), Is.EqualTo(new[] { true, true, true, true }));
                Assert.That(radios.Select(radio => radio.Checked), Is.EqualTo(new[] { false, false, false, true }));
                Assert.That(LayoutChecker.IsSelfVisible(Get<Control>(world.Page, "playEntriesHintKryptonWrapLabel")), Is.False);
            }
        }

        [TestCase("en")]
        [TestCase("de")]
        [TestCase("fr")]
        public void AGameThatIsNotInstalled_IsDisabled_AndTheHintIsShownBelowTheList(string lang)
        {
            language = TestUiLanguage.Use(lang);
            using (PlayPageWorld world = WinForms.CreateOrIgnore(() => PlayPageWorld.In(PlayPageState.EntriesWithHint)))
            {
                KryptonRadioButton[] radios = Radios(world.Page);
                LauncherWrapLabel hint = Get<LauncherWrapLabel>(world.Page, "playEntriesHintKryptonWrapLabel");

                Assert.That(radios.Select(radio => radio.Enabled), Is.EqualTo(new[] { false, false, true, true }));
                Assert.That(radios.Select(radio => radio.Checked), Is.EqualTo(new[] { false, false, true, false }));
                Assert.That(LayoutChecker.IsSelfVisible(hint), Is.True);
                Assert.That(hint.Text, Is.EqualTo(Resources.PlayEntriesNotInstalledHint));
                Assert.That(hint.Top, Is.GreaterThanOrEqualTo(radios.Max(radio => radio.Bottom)), "below the list");
            }
        }

        [Test]
        public void TheTabOrder_GoesThroughTheList_AndThenToPlay()
        {
            language = TestUiLanguage.Use("en");
            using (PlayPageWorld world = WinForms.CreateOrIgnore(() => PlayPageWorld.In(PlayPageState.AllEntries)))
            {
                KryptonRadioButton[] radios = Radios(world.Page);
                var play = Get<KryptonButton>(world.Page, "playKryptonButton");
                var column = Get<Control>(world.Page, "gameColumnPanel");
                var online = Get<Control>(world.Page, "neoOnlineKryptonGroupBox");

                Assert.That(radios.Select(radio => radio.TabIndex), Is.EqualTo(new[] { 1, 2, 3, 4 }), "list order");
                Assert.That(play.TabIndex, Is.GreaterThan(radios.Max(radio => radio.TabIndex)));
                Assert.That(column.TabIndex, Is.LessThan(play.TabIndex), "the column with the list comes before the Play button");
                Assert.That(play.TabIndex, Is.LessThan(online.TabIndex));
            }
        }
    }
}
