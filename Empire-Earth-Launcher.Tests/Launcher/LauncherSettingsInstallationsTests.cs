using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Settings;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using Krypton.Toolkit;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// The list of installations on the Launcher page when the session product (<c>--product</c>) selected a row that is not the
    /// first one: showing the page must not count as a choice of the user (contract 1.4: not saved, the saved choice stays).
    /// </summary>
    [TestFixture]
    [Category(TestCategories.WinForms)]
    [Apartment(ApartmentState.STA)]
    public class LauncherSettingsInstallationsTests
    {
        private const string SettingsFolder = @"C:\Users\Player\AppData\Local\Empire Earth Launcher";

        [SetUp]
        public void SetUp()
        {
            WinForms.RequireDisplay();
        }

        [Test]
        public async Task ShowingThePage_WithTheSecondRowSelectedBySession_SavesNothingAndKeepsTheSessionProduct()
        {
            var world = new InstallationWorld();
            world.AddCommunityInstallation(@"C:\Program Files (x86)\Neo Empire Earth", Product.NeoEE);
            world.AddCommunityInstallation(@"C:\Games\EE Community", Product.EE);
            world.FileSystem.AddDirectory(SettingsFolder);
            var settings = new SettingsStore(world.FileSystem, SettingsFolder + @"\settings.json", world.Logger);
            settings.Load();
            var installations = new InstallationService(world.Logger, settings, world.CreateDiscovery(), world.FileSystem, null);
            await installations.RefreshAsync();
            Assert.That(installations.SelectProductForSession(Product.EE), Is.True);
            Assert.That(installations.Result.Installations.ToList().IndexOf(installations.Selected), Is.GreaterThan(0),
                "the selected installation is not the first row");

            using (var page = WinForms.CreateOrIgnore(() => new LauncherSettingsUserControl()))
            using (var form = new Form())
            {
                WinForms.RunOrIgnoreWithoutWindows(() =>
                {
                    page.Initialize(new FakeThemeService("Dark"), settings, installations, new UiOperation(world.Logger));
                    page.Dock = DockStyle.Fill;
                    form.Controls.Add(page);
                    form.Show();
                    Application.DoEvents();
                    Application.DoEvents();
                });
                await Task.Delay(50);
                Application.DoEvents();

                Assert.That(settings.Current.GameDirectory, Is.Empty, "no choice of the user was saved");
                Assert.That(installations.SessionProduct, Is.SameAs(Product.EE));
                Assert.That(installations.Selected.Product, Is.SameAs(Product.EE));
                Assert.That(world.FileSystem.FileExists(SettingsFolder + @"\settings.json"), Is.False);
            }
        }
    }
}
