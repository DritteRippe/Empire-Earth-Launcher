using System;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;
using Empire_Earth_Launcher.Properties;
using Krypton.Toolkit;

namespace Empire_Earth_Launcher.Tests.TestSupport
{
    /// <summary>The states of the Launcher page the geometry tests drive it through (<see cref="LauncherPageWorld"/>).</summary>
    public enum LauncherPageState
    {
        /// <summary>The page as the designer made it, before any state arrives.</summary>
        Designer,

        /// <summary>
        /// A discovery result with a long game folder, the origin of the choice, the hints below the list at their longest
        /// (installations that share their settings, two products in one folder, a damaged installation) and the note that the
        /// new language is used from the next start on.
        /// </summary>
        LongTexts,
    }

    /// <summary>
    /// A Launcher page filled the way the launcher fills it: created hidden in a window that is not shown, then given the texts
    /// of a state, all before anything is shown. The texts are the real ones from the resources in the UI language of the test.
    /// The page is driven through its own methods (<c>SetShown</c>, <c>LayoutPage</c>), which it calls itself when the
    /// discovery changes; the services behind it are not needed for the geometry.
    /// </summary>
    internal sealed class LauncherPageWorld : IDisposable
    {
        private readonly Form window;

        private LauncherPageWorld(LauncherPageState state, bool hidden, float fontScale)
        {
            Page = new LauncherSettingsUserControl();
            ConstructedSize = Page.Size;
            LauncherPages.ScaleFonts(Page, fontScale);
            // The window of the main window is not shown while the pages are filled; without it the page is visible by itself.
            window = hidden ? LauncherPages.Host(Page) : null;
            if (state == LauncherPageState.LongTexts)
                ShowLongTexts();
            Call("LayoutPage");
        }

        public LauncherSettingsUserControl Page { get; }

        /// <summary>The size of the page after construction (the designer size, scaled by the font of the computer).</summary>
        public System.Drawing.Size ConstructedSize { get; }

        /// <summary>The window that holds the page, never shown unless the caller shows it; null for a page that was created visible.</summary>
        public Form Window
        {
            get { return window; }
        }

        public static LauncherPageWorld In(LauncherPageState state, bool hidden = true, float fontScale = 1f)
        {
            return new LauncherPageWorld(state, hidden, fontScale);
        }

        private void ShowLongTexts()
        {
            Get<KryptonTextBox>("gameDirectoryKryptonTextBox").Text =
                @"C:\Program Files (x86)\Neo Empire Earth\Empire Earth - The Art of Conquest\Empire Earth";
            Get<LauncherWrapLabel>("gameDirectorySourceKryptonWrapLabel").Text = Resources.GameDirectorySourceUserMissing;
            const string root = @"C:\Program Files (x86)\Neo Empire Earth";
            Get<LauncherWrapLabel>("installationsHintKryptonWrapLabel").Text = string.Join(Environment.NewLine,
                string.Format(CultureInfo.CurrentCulture, Resources.InstallationsSharedSettingsFormat, 2, "Neo Empire Earth",
                    @"HKCU\Software\Neo Empire Earth\Empire Earth"),
                string.Format(CultureInfo.CurrentCulture, Resources.InstallationSharedRootFormat, root, "Empire Earth"),
                string.Format(CultureInfo.CurrentCulture, Resources.InstallationNewerSetupFormat, root),
                string.Format(CultureInfo.CurrentCulture, Resources.InstallationDamagedFormat, "EE-AOC.exe", root));
            Call("SetShown", Get<Control>("uiLanguageHintKryptonWrapLabel"), true);
        }

        private T Get<T>(string field) where T : class
        {
            FieldInfo info = typeof(LauncherSettingsUserControl).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            if (info == null)
                throw new InvalidOperationException("LauncherSettingsUserControl has no field " + field + "; adjust LauncherPageWorld.");
            return (T)info.GetValue(Page);
        }

        private void Call(string method, params object[] arguments)
        {
            MethodInfo info = typeof(LauncherSettingsUserControl).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            if (info == null)
                throw new InvalidOperationException("LauncherSettingsUserControl has no method " + method + "; adjust LauncherPageWorld.");
            try
            {
                info.Invoke(Page, arguments);
            }
            catch (TargetInvocationException ex)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }

        public void Dispose()
        {
            if (window != null)
                window.Dispose();
            else
                Page.Dispose();
        }
    }
}
