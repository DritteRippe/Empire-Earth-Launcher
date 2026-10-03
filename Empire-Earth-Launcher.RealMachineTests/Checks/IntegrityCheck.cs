using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Empire_Earth_Launcher.Core.Integrity;
using Empire_Earth_Launcher.RealMachineTests.Expectations;
using Empire_Earth_Launcher.RealMachineTests.Harness;

namespace Empire_Earth_Launcher.RealMachineTests.Checks
{
    /// <summary>
    /// A report of the integrity checker (contract 2.5) against the expectation. Findings are compared and printed as
    /// <c>path|kind</c> only (<see cref="SafeText"/>): the report carries the hashes of game files.
    /// </summary>
    internal static class IntegrityCheck
    {
        public static IReadOnlyList<string> Compare(string what, CheckExpectation expected, IntegrityReport report)
        {
            if (expected == null)
                throw new ArgumentNullException(nameof(expected));
            if (report == null)
                throw new ArgumentNullException(nameof(report));
            var problems = new List<string>();
            if (report.State != expected.State)
            {
                string hint = report.State == IntegrityState.Cancelled && report.CancelReason == CancelReason.SetupRunning
                    ? " (a setup mutex still exists: the job did not wait for the end of the setup or uninstaller)"
                    : string.Empty;
                problems.Add(what + ": the state is " + Describe(report) + ", expected " + expected.State + hint);
            }
            if (expected.UnknownReason != null && report.UnknownReason != expected.UnknownReason.Value)
                problems.Add(what + ": the reason is " + report.UnknownReason + ", expected " + expected.UnknownReason.Value);
            if (expected.CancelReason != null && report.CancelReason != expected.CancelReason.Value)
                problems.Add(what + ": the cancel reason is " + report.CancelReason + ", expected " + expected.CancelReason.Value);
            if (expected.OffersRepair != null && report.OffersRepair != expected.OffersRepair.Value)
                problems.Add(what + ": OffersRepair is " + report.OffersRepair + ", expected " + expected.OffersRepair.Value);

            if (expected.Findings != null)
            {
                var actual = new HashSet<string>(report.Findings.Select(Key), StringComparer.OrdinalIgnoreCase);
                var wanted = new HashSet<string>(expected.Findings.Select(finding => finding.ToString()), StringComparer.OrdinalIgnoreCase);
                foreach (IntegrityFinding extra in report.Findings.Where(finding => !wanted.Contains(Key(finding))))
                    problems.Add(what + ": a finding that is not expected: " + SafeText.Finding(extra));
                foreach (string missing in wanted.Where(finding => !actual.Contains(finding)).OrderBy(finding => finding, StringComparer.Ordinal))
                    problems.Add(what + ": an expected finding is missing: " + SafeText.Redact(missing));
            }

            // A finished check of a real manifest lists files; 0 would mean an empty manifest passed for a whole installation.
            bool finished = report.State == IntegrityState.Ok || report.State == IntegrityState.Modified ||
                            report.State == IntegrityState.Incomplete || report.State == IntegrityState.Damaged;
            if (finished && report.ListedFiles == 0)
                problems.Add(what + ": the manifest lists no file");
            return SafeText.Redact(problems);
        }

        /// <summary>One line for the log of the harness: state, reason, counts and the findings, without hashes.</summary>
        public static string Describe(IntegrityReport report)
        {
            if (report == null)
                throw new ArgumentNullException(nameof(report));
            string detail = report.State == IntegrityState.Unknown ? " (" + report.UnknownReason + ")"
                : report.State == IntegrityState.Cancelled ? " (" + report.CancelReason + ")" : string.Empty;
            string findings = report.Findings.Count == 0
                ? string.Empty
                : ", findings " + string.Join(", ", report.Findings.Take(10).Select(SafeText.Finding)) +
                  (report.Findings.Count > 10 ? " and " + (report.Findings.Count - 10).ToString(CultureInfo.InvariantCulture) + " more" : string.Empty);
            return report.State + detail + " (" + report.ListedFiles.ToString(CultureInfo.InvariantCulture) + " listed, " +
                   report.HashedFiles.ToString(CultureInfo.InvariantCulture) + " hashed" + findings + ")";
        }

        private static string Key(IntegrityFinding finding)
        {
            return finding.Path + "|" + finding.Kind;
        }
    }
}
