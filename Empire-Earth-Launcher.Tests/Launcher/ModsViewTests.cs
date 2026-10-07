using System;
using System.Linq;
using System.Threading.Tasks;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// <see cref="ModsView"/>, everything the Mods page decides: which blocks and controls are shown and enabled and what they
    /// say, in English; German and French are in <c>ModsTextsTests</c>. The page itself only assigns the view and is measured by
    /// <c>PageLayoutTests</c>.
    /// </summary>
    [TestFixture]
    [SetUICulture("en-US")]
    public class ModsViewTests
    {
        private ModsModelWorld m;

        [SetUp]
        public void SetUp()
        {
            m = new ModsModelWorld();
        }

        private ModsView View()
        {
            return ModsView.Of(m.Model, m.Watcher);
        }

        [Test]
        public void BeforeTheSearchHasFinished_ThePageSaysSearching_AndShowsNothingElse()
        {
            ModsView view = View();

            Assert.That(view.Installation, Is.EqualTo("Searching for Empire Earth installations..."));
            Assert.That(view.ShowGames, Is.False);
            Assert.That(view.Games, Is.Empty);
            Assert.That(view.ShowTemplateSwitch, Is.False);
        }

        [Test]
        public async Task WithoutAnInstallation_ThePageSaysSo()
        {
            await m.Installations.RefreshAsync();

            ModsView view = View();

            Assert.That(view.Installation, Is.EqualTo("Empire Earth installation not found"));
            Assert.That(view.ShowGames, Is.False);
            Assert.That(view.Status, Is.Empty);
        }

        [Test]
        public async Task WithDreXmod2_NoBlockIsShown()
        {
            m.AddInstallation(ModsModelWorld.DreXmod2);
            m.AddShippedPresets();
            await m.Search();

            ModsView view = View();

            Assert.That(view.ShowGames, Is.False);
            Assert.That(view.Games, Is.Empty);
        }

        [Test]
        public async Task WithDreXmod3_ABlockPerGameShowsTheChoice_AndThePresetsWithTheirBadges()
        {
            m.AddInstallation();
            m.AddShippedPresets(ModsModelWorld.Config(mod: "1", modName: "yukon", lobby: "1", lobbyName: "energycube"));
            await m.Search();

            ModsView view = View();

            Assert.That(view.ShowGames, Is.True);
            Assert.That(view.Status, Is.Empty);
            Assert.That(view.Games.Select(game => game.Heading), Is.EqualTo(new[] { "Empire Earth", "The Art of Conquest" }));
            ModsGameView ee = view.Games[0];
            Assert.That(ee.Selection, Is.EqualTo("Active mod: yukon" + Environment.NewLine + "Active lobby theme: energycube"));
            string[] lines = ee.Presets.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            Assert.That(lines[0], Is.EqualTo("dxm (dreXmod)"));
            Assert.That(lines[2], Is.EqualTo("energycube  [active lobby theme]"));
            Assert.That(lines[4], Is.EqualTo("yukon  [active mod]"));
            Assert.That(lines[5], Does.Contain("created by author one, author two").And.Contain("size 18 KB"));
            Assert.That(ee.OpenFolderEnabled, Is.True);
            Assert.That(ee.OpenConfigEnabled, Is.True);
        }

        [Test]
        public async Task TheTemplate_IsHiddenUnlessAsked_AndThePageOffersTheSwitchBecauseThereIsOne()
        {
            m.AddInstallation();
            m.AddShippedPresets();
            await m.Search();

            ModsView hidden = View();
            m.Model.ShowTemplates = true;
            ModsView shown = View();

            Assert.That(hidden.ShowTemplateSwitch, Is.True);
            Assert.That(hidden.TemplatesShown, Is.False);
            Assert.That(hidden.Games[0].Presets, Does.Not.Contain("template"));
            Assert.That(shown.TemplatesShown, Is.True);
            Assert.That(shown.Games[0].Presets, Does.Contain("template  [template for authors]"));
        }

        [Test]
        public async Task WithoutATemplateFolder_ThereIsNoSwitch()
        {
            m.AddInstallation();
            m.AddPreset(ModsModelWorld.EeFolder, "yukon", ModsModelWorld.Credits("yukon"));
            await m.Search();

            Assert.That(View().ShowTemplateSwitch, Is.False);
        }

        [Test]
        public async Task AMissingFolderAndConfig_AreSaid_AndTheirButtonsAreDisabled()
        {
            m.AddInstallation();
            await m.Search();

            ModsGameView ee = View().Games[0];

            Assert.That(ee.Presets, Does.Contain("does not exist"));
            Assert.That(ee.Selection, Does.Contain("dreXmod.config was not found"));
            Assert.That(ee.OpenFolderEnabled, Is.False);
            Assert.That(ee.OpenConfigEnabled, Is.False);
        }

        [Test]
        public async Task WhileASetupRuns_ThePageSaysSo_AndTheButtonsAreDisabled()
        {
            m.AddInstallation();
            m.AddShippedPresets();
            await m.Search();

            m.StartSetup();
            ModsView view = View();

            Assert.That(view.Status, Does.StartWith("The NeoEE setup is running."));
            Assert.That(view.Games.All(game => !game.OpenFolderEnabled && !game.OpenConfigEnabled), Is.True);
            Assert.That(view.ShowGames, Is.True, "what was read stays on the page");
        }

        [Test]
        public async Task WhileTheFirstReadIsUnderWay_ThePageSaysChecking_AndShowsNoBlock()
        {
            m.AddInstallation();
            m.AddShippedPresets();
            ModsView duringRead = null;
            m.Model.Changed += (sender, e) =>
            {
                if (m.Model.IsReading && m.Model.Snapshot == null && duringRead == null)
                    duringRead = View();
            };

            await m.Search();

            Assert.That(duringRead, Is.Not.Null);
            Assert.That(duringRead.Status, Is.EqualTo("Checking..."));
            Assert.That(duringRead.ShowGames, Is.False);
            Assert.That(View().Status, Is.Empty);
        }
    }
}
