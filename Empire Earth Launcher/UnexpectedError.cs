using System;
using System.Globalization;
using System.Windows.Forms;
using Empire_Earth_Launcher.Core.Settings;
using Empire_Earth_Launcher.Properties;

namespace Empire_Earth_Launcher
{
    /// <summary>
    /// The localized message for an exception nobody expected (ADR 0013: programming errors throw and end in the
    /// global handlers or in <see cref="UiOperation"/>). Environment problems never get here: the core returns them
    /// as results with their own texts.
    /// </summary>
    internal static class UnexpectedError
    {
        /// <summary>
        /// "An unexpected error occurred ...", the exception message and where the details are logged, in the UI
        /// language.
        /// </summary>
        /// <param name="exception">The error; null if it is unknown.</param>
        /// <param name="isTerminating">true if the launcher has to close.</param>
        internal static string FormatMessage(Exception exception, bool isTerminating)
        {
            return (isTerminating ? Resources.UnexpectedErrorClosing : Resources.UnexpectedErrorContinuing)
                   + Environment.NewLine + Environment.NewLine
                   + (exception != null ? exception.Message : Resources.UnknownError)
                   + Environment.NewLine + Environment.NewLine
                   + string.Format(CultureInfo.CurrentCulture, Resources.DetailsWrittenToLogFormat, LauncherPaths.LogFile);
        }

        /// <summary>Shows <see cref="FormatMessage"/> in an error message box.</summary>
        /// <param name="owner">The window the message belongs to, or null.</param>
        internal static void Show(IWin32Window owner, Exception exception, bool isTerminating)
        {
            MessageBox.Show(owner, FormatMessage(exception, isTerminating), Resources.LauncherTitle, MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
