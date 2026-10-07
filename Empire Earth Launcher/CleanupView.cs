using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Empire_Earth_Launcher.Core.Maintenance;
using Empire_Earth_Launcher.Properties;

namespace Empire_Earth_Launcher
{
    /// <summary>
    /// What the registry cleanup of the Tools page shows for a scan (R5, ADR 0007 plan review), without controls, so that the
    /// mapping is tested: the summary, the keys the player can select, the read-only list with its advice, and whether the
    /// delete button works. Without a key the launcher could delete the page says "nothing to clean up", shows the read-only
    /// list and no enabled delete button.
    /// </summary>
    internal sealed class CleanupView
    {
        private CleanupView(string summary, IList<CleanupItem> offered, string readOnlyText, bool deleteEnabled)
        {
            Summary = summary;
            Offered = new ReadOnlyCollection<CleanupItem>(offered);
            OfferedTexts = new ReadOnlyCollection<string>(offered.Select(Texts.CleanupOffered).ToList());
            ReadOnlyText = readOnlyText;
            DeleteEnabled = deleteEnabled;
        }

        /// <summary>The line below the explanation: checking, nothing to clean up, or how many keys can be deleted.</summary>
        public string Summary { get; }

        /// <summary>The keys the player can select, in the order of the list.</summary>
        public IReadOnlyList<CleanupItem> Offered { get; }

        /// <summary>The texts of <see cref="Offered"/>.</summary>
        public IReadOnlyList<string> OfferedTexts { get; }

        /// <summary>True if the list of selectable keys is shown (only when there is a key to select).</summary>
        public bool ShowsList
        {
            get { return Offered.Count > 0; }
        }

        /// <summary>The read-only keys with their advice, one per line; empty if there is none.</summary>
        public string ReadOnlyText { get; }

        /// <summary>True if "Delete selected..." works: keys are offered, at least one is selected, and changes are possible.</summary>
        public bool DeleteEnabled { get; }

        /// <param name="scan">The scan of the registry cleanup; null while it runs.</param>
        /// <param name="selectedCount">How many offered keys the player selected.</param>
        /// <param name="canChange">False while a setup, a search or another action runs (contract 4.2, ADR 0016).</param>
        public static CleanupView For(CleanupScan scan, int selectedCount, bool canChange)
        {
            if (scan == null)
                return new CleanupView(Resources.ToolsChecking, new CleanupItem[0], string.Empty, false);
            List<CleanupItem> offered = scan.Offered.ToList();
            string summary = offered.Count == 0
                ? Resources.CleanupNothingToCleanUp
                : string.Format(CultureInfo.CurrentCulture, Resources.CleanupCandidatesFormat, offered.Count);
            string readOnly = string.Join(Environment.NewLine, scan.ReadOnly.Select(Texts.CleanupAdvice));
            return new CleanupView(summary, offered, readOnly, offered.Count > 0 && selectedCount > 0 && canChange);
        }
    }
}
