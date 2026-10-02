using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;

namespace Empire_Earth_Launcher.Core.Settings
{
    /// <summary>
    /// The languages of the launcher's UI and the setting <see cref="LauncherSettings.UiCulture"/> that chooses one
    /// (ADR 0009): English (the neutral language), German and French, or the Windows display language. Any other
    /// Windows language shows English.
    /// </summary>
    public static class UiLanguage
    {
        /// <summary>The value of the setting for "the language of Windows"; also the default.</summary>
        public const string Windows = "";

        /// <summary>The values of the setting in the order of the language list: Windows, English, German, French.</summary>
        public static readonly IReadOnlyList<string> Choices =
            new ReadOnlyCollection<string>(new[] { Windows, "en", "de", "fr" });

        /// <summary>
        /// The setting in its stored form: <see cref="Windows"/> or one of the <see cref="Choices"/> in lower case
        /// (white space and case are ignored).
        /// </summary>
        /// <param name="setting">The value of the settings file; null and empty mean <see cref="Windows"/>.</param>
        /// <param name="language">The choice, or <see cref="Windows"/> if the value is unknown.</param>
        /// <returns>false if the value is not a language of the launcher (it is then used as <see cref="Windows"/>).</returns>
        public static bool TryNormalize(string setting, out string language)
        {
            string value = (setting ?? string.Empty).Trim().ToLowerInvariant();
            language = Choices.Contains(value, StringComparer.Ordinal) ? value : Windows;
            return language == value;
        }

        /// <summary>Position of a choice in <see cref="Choices"/>, or -1.</summary>
        public static int IndexOf(string language)
        {
            for (int i = 0; i < Choices.Count; i++)
            {
                if (string.Equals(Choices[i], language, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        /// <summary>The culture of a choice, or null for <see cref="Windows"/> (the UI culture stays as Windows sets it).</summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="language"/> is not one of the <see cref="Choices"/>.</exception>
        public static CultureInfo ToCulture(string language)
        {
            if (IndexOf(language) < 0)
                throw new ArgumentOutOfRangeException(nameof(language), language, "Not a UI language of the launcher.");
            return language == Windows ? null : CultureInfo.GetCultureInfo(language);
        }
    }
}
