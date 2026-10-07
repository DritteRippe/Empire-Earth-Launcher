using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Empire_Earth_Launcher.Core.Contract;
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
    /// in the tab order (the real sequence, walked with <see cref="Control.GetNextControl"/>). A page bound to a real
    /// <see cref="PlayModel"/> (<see cref="PlayModelWorld"/>) shows which games can be chosen and, when a radio button is checked,
    /// selects the product for every page and saves the product and the game. The geometry (nothing overlaps or is cut off, also in
    /// the two-line form of a long name) is checked by <c>PageLayoutTests</c> for the same states. These tests need Windows libraries
    /// for the Krypton controls and run on Windows (the keyboard use, arrow keys and Tab, is case WP13-08 of the test plan).
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

        /// <summary>
        /// Every control of the page in the order of the Tab key, as <see cref="Control.GetNextControl"/> walks it (nested
        /// containers included), from <paramref name="first"/> to <paramref name="last"/>, and only those a Tab stops at: a tab
        /// stop that is enabled and shown, with every container around it shown.
        /// </summary>
        private static IReadOnlyList<Control> TabStops(Control page, Control first, Control last)
        {
            var stops = new List<Control>();
            Control current = first;
            for (int guard = 0; current != null && guard < 500; guard++)
            {
                if (current.TabStop && current.Enabled && IsShown(page, current))
                    stops.Add(current);
                if (current == last)
                    return stops;
                current = page.GetNextControl(current, true);
            }
            Assert.Fail("The tab order from " + first.Name + " does not reach " + last.Name + ".");
            return stops;
        }

        /// <summary>True if the control and all its containers up to the page are set visible (the page itself is not shown here).</summary>
        private static bool IsShown(Control page, Control control)
        {
            for (Control c = control; c != null && c != page; c = c.Parent)
            {
                if (!LayoutChecker.IsSelfVisible(c))
                    return false;
            }
            return true;
        }

        [Test]
        public void TheTabOrder_GoesThroughTheList_AndThenToPlay()
        {
            language = TestUiLanguage.Use("en");
            using (PlayPageWorld world = WinForms.CreateOrIgnore(() => PlayPageWorld.In(PlayPageState.AllEntries)))
            {
                KryptonRadioButton[] radios = Radios(world.Page);
                var play = Get<KryptonButton>(world.Page, "playKryptonButton");

                IReadOnlyList<Control> stops = TabStops(world.Page, radios[0], play);

                // The radio buttons of Krypton are tab stops of their own, one after the other in the order of the list. Then come
                // the buttons below the list (the version check; the integrity button and the info bar only when they are shown),
                // and the Play button ends the walk: nothing but buttons is between the list and Play.
                Assert.That(stops.Take(4), Is.EqualTo(radios), "the four games, in the order of the list");
                Assert.That(stops.Last(), Is.SameAs(play));
                Assert.That(stops.Skip(4).Take(stops.Count - 5), Is.All.InstanceOf<KryptonButton>());
                Assert.That(radios.Select(radio => radio.TabIndex), Is.EqualTo(new[] { 1, 2, 3, 4 }), "list order");
            }
        }

        [Test]
        public void AGameThatIsNotInstalled_IsNoTabStop_AndTheOthersStayInOrder()
        {
            language = TestUiLanguage.Use("en");
            using (PlayPageWorld world = WinForms.CreateOrIgnore(() => PlayPageWorld.In(PlayPageState.EntriesWithHint)))
            {
                KryptonRadioButton[] radios = Radios(world.Page);
                var play = Get<KryptonButton>(world.Page, "playKryptonButton");

                IReadOnlyList<Control> stops = TabStops(world.Page, radios[0], play);

                Assert.That(stops.Take(2), Is.EqualTo(new[] { radios[2], radios[3] }), "the two greyed games are skipped by Tab");
                Assert.That(stops.Last(), Is.SameAs(play));
            }
        }

        // --- A page bound to a real PlayModel: the wiring of the click and of the games that can be chosen --------------------

        /// <summary>
        /// Binds the page to the model as <c>Initialize</c> does for the Play list (the other models of the page are not needed
        /// for it) and shows the state of the model.
        /// </summary>
        private static void Bind(GeneralUserControl page, PlayModelWorld world)
        {
            SetField(page, "installations", world.Installations);
            SetField(page, "play", world.Model);
            MethodInfo show = typeof(GeneralUserControl).GetMethod("ShowPlayState", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(show, Is.Not.Null, "GeneralUserControl has no method ShowPlayState");
            show.Invoke(page, null);
        }

        private static void SetField(Control page, string field, object value)
        {
            FieldInfo info = typeof(GeneralUserControl).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, "GeneralUserControl has no field " + field);
            info.SetValue(page, value);
        }

        [Test]
        public void TheGamesThatAreNotInstalled_AreDisabled_AndTheChosenOneIsChecked_AsTheModelSaysSo()
        {
            language = TestUiLanguage.Use("en");
            var model = new PlayModelWorld(withEE: false);
            model.Search();
            using (PlayPageWorld world = WinForms.CreateOrIgnore(() => PlayPageWorld.In(PlayPageState.Designer)))
            {
                Bind(world.Page, model);
                KryptonRadioButton[] radios = Radios(world.Page);

                Assert.That(radios.Select(radio => radio.Enabled), Is.EqualTo(model.Model.Entries.Select(model.Model.IsAvailable)),
                    "the page asks the model for every game");
                Assert.That(radios.Select(radio => radio.Enabled), Is.EqualTo(new[] { false, false, true, true }), "only NeoEE is installed");
                Assert.That(radios.Select(radio => radio.Checked), Is.EqualTo(new[] { false, false, true, false }));
                Assert.That(LayoutChecker.IsSelfVisible(Get<Control>(world.Page, "playEntriesHintKryptonWrapLabel")), Is.True);
            }
        }

        [Test]
        public void CheckingAGame_SelectsItsProductForEveryPage_AndSavesTheProductAndTheGame()
        {
            language = TestUiLanguage.Use("en");
            var model = new PlayModelWorld(withEE: true);
            model.Search();
            using (PlayPageWorld world = WinForms.CreateOrIgnore(() => PlayPageWorld.In(PlayPageState.Designer)))
            {
                Bind(world.Page, model);
                KryptonRadioButton[] radios = Radios(world.Page);
                Assert.That(radios.Select(radio => radio.Enabled), Is.EqualTo(new[] { true, true, true, true }));
                Assert.That(model.Installations.Selected.Product, Is.SameAs(Product.NeoEE), "contract 1.4: NeoEE comes first");

                radios[1].Checked = true; // Empire Earth - The Art of Conquest

                Assert.That(model.Model.SelectedEntry, Is.SameAs(PlayEntry.EmpireEarthArtOfConquest));
                Assert.That(model.Installations.Selected.Product, Is.SameAs(Product.EE), "every page works with the EE installation now");
                Assert.That(model.Saved().LastProduct, Is.EqualTo("EE"));
                Assert.That(model.Saved().LastGame, Is.EqualTo("AoC"));
                Assert.That(radios.Select(radio => radio.Checked), Is.EqualTo(new[] { false, true, false, false }), "one game is checked");

                radios[2].Checked = true; // Neo Empire Earth

                Assert.That(model.Model.SelectedEntry, Is.SameAs(PlayEntry.NeoEmpireEarth));
                Assert.That(model.Installations.Selected.Product, Is.SameAs(Product.NeoEE));
                Assert.That(model.Saved().LastProduct, Is.EqualTo("NeoEE"));
                Assert.That(model.Saved().LastGame, Is.EqualTo("EE"));
            }
        }

        /// <summary>A game that is greyed out is not taken, also if code (or an accessibility tool) checks it: the page shows the chosen one again.</summary>
        [Test]
        public void CheckingAGameThatIsNotInstalled_ChangesNothing_AndTheChosenGameIsCheckedAgain()
        {
            language = TestUiLanguage.Use("en");
            var model = new PlayModelWorld(withEE: true, eeComponents: "game");
            model.Search();
            using (PlayPageWorld world = WinForms.CreateOrIgnore(() => PlayPageWorld.In(PlayPageState.Designer)))
            {
                Bind(world.Page, model);
                KryptonRadioButton[] radios = Radios(world.Page);
                Assert.That(radios.Select(radio => radio.Enabled), Is.EqualTo(new[] { true, false, true, true }), "EE has no Art of Conquest");
                PlayEntry before = model.Model.SelectedEntry;

                radios[1].Checked = true;

                Assert.That(model.Model.SelectedEntry, Is.SameAs(before), "nothing was chosen");
                Assert.That(model.World.FileSystem.FileExists(PlayModelWorld.SettingsFile), Is.False, "nothing was saved");
                Assert.That(radios.Select(radio => radio.Checked), Is.EqualTo(new[] { false, false, true, false }),
                    "the page shows the game that is chosen again");
            }
        }
    }
}
