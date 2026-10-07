using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Architecture
{
    /// <summary>
    /// The main window can be resized freely and its pages grow with it (1.1.0, ADR 0017): the navigation buttons sit in a
    /// panel at the left edge, the six pages fill the rest, and the window never gets smaller than it opens. The main window
    /// cannot be created under Mono, so the rules are checked on the sources; the pages themselves are measured by
    /// <c>PageLayoutTests</c> at several window sizes.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.SourceTree)]
    public class MainWindowLayoutTests
    {
        private const string LauncherFolder = "Empire Earth Launcher";

        private static readonly string[] Pages =
        {
            "generalUserControl", "settingsUserControl", "graphicsUserControl", "modsUserControl", "toolsUserControl",
            "launcherSettingsUserControl"
        };

        private static readonly string[] NavigationButtons =
        {
            "playKryptonCheckButton", "settingsKryptonCheckButton", "graphicsKryptonCheckButton", "modPresetsKryptonCheckButton",
            "toolsKryptonCheckButton", "launcherKryptonCheckButton"
        };

        private static string Designer()
        {
            return File.ReadAllText(RepositoryRoot.GetFullPath(LauncherFolder + "/MainForm.Designer.cs"));
        }

        [Test]
        public void EveryPage_FillsTheSpaceNextToTheNavigationPanel()
        {
            string designer = Designer();

            foreach (string page in Pages)
                Assert.That(designer, Does.Contain("this." + page + ".Dock = System.Windows.Forms.DockStyle.Fill;"), page);
        }

        [Test]
        public void TheNavigationButtons_SitInAPanelDockedAtTheLeftEdge()
        {
            string designer = Designer();

            Assert.That(designer, Does.Contain("this.navigationPanel.Dock = System.Windows.Forms.DockStyle.Left;"));
            foreach (string button in NavigationButtons)
            {
                Assert.That(designer, Does.Contain("this.navigationPanel.Controls.Add(this." + button + ");"), button);
                Assert.That(designer, Does.Not.Contain("this.Controls.Add(this." + button + ");"),
                    button + " is a child of the panel, not of the window");
            }
        }

        [Test]
        public void ThePanelIsAddedAfterThePages_SoThatItTakesItsEdgeBeforeThePagesFillTheRest()
        {
            string designer = Designer();

            int panel = designer.IndexOf("this.Controls.Add(this.navigationPanel);", StringComparison.Ordinal);
            Assert.That(panel, Is.GreaterThan(0));
            foreach (string page in Pages)
                Assert.That(designer.IndexOf("this.Controls.Add(this." + page + ");", StringComparison.Ordinal),
                    Is.InRange(0, panel - 1), page + " must come before the panel (WinForms docks the last control first)");
        }

        [Test]
        public void TheWindow_NeverGetsSmallerThanItOpens()
        {
            string code = File.ReadAllText(RepositoryRoot.GetFullPath(LauncherFolder + "/MainForm.cs"));

            Assert.That(Regex.IsMatch(code, @"override void OnLoad\(EventArgs e\)\s*\{[^}]*MinimumSize = Size;"), Is.True,
                "the minimum size is the size of the window when it opens");
        }

        [Test]
        public void TheDesigner_DoesNotFixTheWindowSize()
        {
            string designer = Designer();

            Assert.That(designer, Does.Not.Contain("MaximumSize"));
            Assert.That(designer, Does.Not.Contain("MaximizeBox = false"));
            Assert.That(designer, Does.Not.Contain("FormBorderStyle"));
        }
    }
}
