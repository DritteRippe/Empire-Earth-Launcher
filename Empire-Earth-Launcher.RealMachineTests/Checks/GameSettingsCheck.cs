using System;
using System.Collections.Generic;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.RealMachineTests.Expectations;

namespace Empire_Earth_Launcher.RealMachineTests.Checks
{
    /// <summary>
    /// The state of the game settings of an installation before the launcher changes anything: the status of the defaults per
    /// game (contract 3.5: <c>Applied</c> after a setup since v2 for the account that ran it, <c>Pending</c> after 1.7.2) and the
    /// consistency findings (contract 3.3, 3.7), which depend on the screen of the runner.
    /// </summary>
    internal static class GameSettingsCheck
    {
        public static IReadOnlyList<string> CompareStatus(InstallationExpectation expected, Installation installation,
            DiscoveryResult discovery, GameDefaultsService defaults)
        {
            if (expected == null)
                throw new ArgumentNullException(nameof(expected));
            var problems = new List<string>();
            if (expected.DefaultsStatus == null)
                return problems;
            bool unambiguous = discovery.IsUnambiguous(installation);
            foreach (Game game in expected.DefaultsStatus.Games)
            {
                if (!GameDefaultsService.GamesOf(installation).Contains(game))
                {
                    problems.Add(expected + ": defaultsStatus names " + game.Id + ", which is not installed");
                    continue;
                }
                DefaultsStatus status = defaults.GetStatus(installation, game, unambiguous);
                if (status != expected.DefaultsStatus[game])
                    problems.Add(expected + " " + game.Id + ": the defaults status is " + status + ", expected " + expected.DefaultsStatus[game]);
            }
            return problems;
        }

        public static IReadOnlyList<string> CompareConsistency(InstallationExpectation expected, IReadOnlyList<ConsistencyFinding> findings)
        {
            if (expected == null)
                throw new ArgumentNullException(nameof(expected));
            if (findings == null)
                throw new ArgumentNullException(nameof(findings));
            var problems = new List<string>();
            if (expected.Consistency == null)
                return problems;
            var codes = new HashSet<FindingCode>(findings.Select(finding => finding.Code));
            foreach (FindingCode missing in expected.Consistency.Expected.Where(code => !codes.Contains(code)))
                problems.Add(expected + ": the consistency finding " + missing + " is missing");
            foreach (ConsistencyFinding extra in findings.Where(finding => !expected.Consistency.Expected.Contains(finding.Code) &&
                                                                           !expected.Consistency.Allowed.Contains(finding.Code)))
                problems.Add(expected + ": a consistency finding that is not expected: " + extra);
            return problems;
        }
    }
}
