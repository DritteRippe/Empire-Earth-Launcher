using System;
using System.Collections.Generic;
using System.Linq;
using Empire_Earth_Launcher.Core.Settings;

namespace Empire_Earth_Launcher.Core.GameSettings
{
    /// <summary>
    /// Hiding the hints of the consistency checks per value and content (ADR 0015): a hidden hint is stored in
    /// <c>settings.json</c> with the values it showed (<see cref="LauncherSettings.HiddenHints"/>), and it shows again as soon
    /// as one of them changes. The Game settings page always lists every finding; hiding affects the info bar of the Play
    /// page. Use on the UI thread, like the settings.
    /// </summary>
    public static class HintVisibility
    {
        /// <summary>True if the player hid this hint with exactly these values.</summary>
        public static bool IsHidden(LauncherSettings settings, ConsistencyFinding finding)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (finding == null)
                throw new ArgumentNullException(nameof(finding));
            return settings.HiddenHints.Any(hint => Matches(hint, finding));
        }

        /// <summary>The findings that are not hidden, in their order.</summary>
        public static IReadOnlyList<ConsistencyFinding> Visible(LauncherSettings settings, IEnumerable<ConsistencyFinding> findings)
        {
            if (findings == null)
                throw new ArgumentNullException(nameof(findings));
            return findings.Where(finding => !IsHidden(settings, finding)).ToList();
        }

        /// <summary>Hides the hint for its current values; an older entry of the same hint is replaced.</summary>
        public static void Hide(LauncherSettings settings, ConsistencyFinding finding)
        {
            Show(settings, finding);
            settings.HiddenHints.Add(new HiddenHint { Hint = finding.HintKey, Values = finding.HintValues });
        }

        /// <summary>Shows the hint again (removes every entry of it, whatever values it had).</summary>
        public static void Show(LauncherSettings settings, ConsistencyFinding finding)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (finding == null)
                throw new ArgumentNullException(nameof(finding));
            settings.HiddenHints.RemoveAll(hint => string.Equals(hint.Hint, finding.HintKey, StringComparison.OrdinalIgnoreCase));
        }

        private static bool Matches(HiddenHint hint, ConsistencyFinding finding)
        {
            return string.Equals(hint.Hint, finding.HintKey, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(hint.Values, finding.HintValues, StringComparison.Ordinal);
        }
    }
}
