using System;
using System.Globalization;
using System.Reflection;
using System.Windows.Forms;
using Empire_Earth_Launcher.Properties;
using Krypton.Toolkit;

namespace Empire_Earth_Launcher.Tests.TestSupport
{
    /// <summary>The states of the Play page the geometry tests drive it through (<see cref="PlayPageWorld"/>).</summary>
    public enum PlayPageState
    {
        /// <summary>The page as the designer made it, before any state arrives.</summary>
        Designer,

        /// <summary>
        /// The texts of the game group at their longest: two program versions, the result of the version check, an integrity
        /// state with "Repair...", and the line of a running setup.
        /// </summary>
        LongTexts,

        /// <summary>The info bar with the display question of the first run, which lists several differences.</summary>
        InfoBar,

        /// <summary>The long texts of the game group and the info bar together, as in the smallest window they fill the column.</summary>
        LongTextsAndInfoBar,

        /// <summary>The player list says why it is empty (a setup runs) and offers the link to the network check.</summary>
        LobbyStatus,
    }

    /// <summary>
    /// A Play page filled the way the launcher fills it: created hidden in a window that is not shown, then given the texts of a
    /// state, all before anything is shown. The texts are the real ones from the resources in the UI language of the test, so
    /// that a German translation is as long as the player sees it. The page itself is driven through its own methods
    /// (<c>SetText</c>, <c>SetShown</c>), which <see cref="GeneralUserControl"/> calls when its models change; the models are not
    /// needed for the geometry.
    /// </summary>
    internal sealed class PlayPageWorld : IDisposable
    {
        private readonly System.Drawing.Size constructedSize;
        private readonly Form window;

        private PlayPageWorld(PlayPageState state, bool hidden, float fontScale)
        {
            Page = new GeneralUserControl();
            constructedSize = Page.Size;
            LauncherPages.ScaleFonts(Page, fontScale);
            // The window of the main window is not shown while the pages are filled; without it the page is visible by itself.
            window = hidden ? LauncherPages.Host(Page) : null;
            Apply(state);
        }

        public GeneralUserControl Page { get; }

        /// <summary>The size of the page after construction (the designer size, scaled by the font of the computer).</summary>
        public System.Drawing.Size ConstructedSize
        {
            get { return constructedSize; }
        }

        /// <summary>The window that holds the page, never shown unless the caller shows it; null for a page that was created visible.</summary>
        public Form Window
        {
            get { return window; }
        }

        public static PlayPageWorld In(PlayPageState state, bool hidden = true, float fontScale = 1f)
        {
            return new PlayPageWorld(state, hidden, fontScale);
        }

        private void Apply(PlayPageState state)
        {
            if (state == PlayPageState.LongTexts || state == PlayPageState.LongTextsAndInfoBar)
                ShowLongTexts();
            if (state == PlayPageState.InfoBar || state == PlayPageState.LongTextsAndInfoBar)
                ShowDisplayQuestion();
            if (state == PlayPageState.LobbyStatus)
            {
                Page.ShowLobbyStatus(Resources.InstallationsWaitingForSetup);
                Control link = Get<Control>("networkCheckKryptonLinkLabel");
                link.Visible = true;
            }
            Call("LayoutPage");
        }

        private void ShowLongTexts()
        {
            string versions = string.Join(Environment.NewLine,
                string.Format(CultureInfo.CurrentCulture, Resources.PlayVersionFormat, "Empire Earth.exe", "2.0.0.2949"),
                string.Format(CultureInfo.CurrentCulture, Resources.PlayVersionUnknownFormat, "EE-AOC.exe"));
            SetText("programVersionsKryptonWrapLabel", versions);
            SetText("versionResultKryptonWrapLabel",
                string.Format(CultureInfo.CurrentCulture, Resources.VersionGameUpdateFormat, "2.0.0.2949", "2.0.1.3012"));
            SetText("integrityKryptonWrapLabel",
                string.Format(CultureInfo.CurrentCulture, Resources.IntegrityBadgeUnreliableFormat, Resources.IntegrityBadgeIncomplete));
            SetText("playStatusKryptonWrapLabel", Resources.InstallationsWaitingForSetup);
            var integrityButton = Get<KryptonButton>("integrityKryptonButton");
            integrityButton.Values.Text = Resources.IntegrityRepairButton;
            Call("SetShown", integrityButton, true);
        }

        private void ShowDisplayQuestion()
        {
            string difference = string.Format(CultureInfo.CurrentCulture, Resources.DisplayDifferenceFormat, "Empire Earth",
                "Game Bit Depth", "16", "32");
            string text = string.Format(CultureInfo.CurrentCulture, Resources.DisplayQuestionFormat,
                string.Join("; ", difference, difference, difference));
            Get<LauncherWrapLabel>("gameSettingsHintKryptonWrapLabel").Text = text;
            Get<KryptonButton>("gameSettingsHintFirstKryptonButton").Values.Text = Resources.DisplayQuestionApply;
            Get<KryptonButton>("gameSettingsHintSecondKryptonButton").Values.Text = Resources.DisplayQuestionKeep;
            Call("SetShown", Get<Control>("gameSettingsHintKryptonPanel"), true);
        }

        private void SetText(string label, string text)
        {
            Call("SetText", Get<LauncherWrapLabel>(label), text);
        }

        private T Get<T>(string field) where T : class
        {
            FieldInfo info = typeof(GeneralUserControl).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            if (info == null)
                throw new InvalidOperationException("GeneralUserControl has no field " + field + "; adjust PlayPageWorld.");
            return (T)info.GetValue(Page);
        }

        private void Call(string method, params object[] arguments)
        {
            MethodInfo info = typeof(GeneralUserControl).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic);
            if (info == null)
                throw new InvalidOperationException("GeneralUserControl has no method " + method + "; adjust PlayPageWorld.");
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
