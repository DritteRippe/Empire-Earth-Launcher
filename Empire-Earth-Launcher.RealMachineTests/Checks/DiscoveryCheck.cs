using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.RealMachineTests.Expectations;

namespace Empire_Earth_Launcher.RealMachineTests.Checks
{
    /// <summary>
    /// The installations the discovery found (contract 1.4) against the expectation: one problem line per difference, empty if
    /// everything matches. Installations are matched by their root.
    /// </summary>
    internal static class DiscoveryCheck
    {
        public static IReadOnlyList<string> Compare(Expectation expectation, DiscoveryResult result)
        {
            if (expectation == null)
                throw new ArgumentNullException(nameof(expectation));
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            var problems = new List<string>();
            foreach (InstallationExpectation expected in expectation.Installations)
            {
                Installation found = Find(result, expected.Root);
                if (found == null)
                    problems.Add(expected + ": not found; the discovery found " + Describe(result));
                else
                    CompareInstallation(expected, found, problems);
            }

            if (expectation.ExactInstallations)
            {
                foreach (Installation other in result.Installations.Where(installation => expectation.ForRoot(installation.Root) == null))
                    problems.Add("an installation that is not expected: " + other);
            }

            if (expectation.SelectedRoot != null && (result.Selected == null || !WinPath.IsSamePath(result.Selected.Root, expectation.SelectedRoot)))
                problems.Add("selected is " + (result.Selected?.Root ?? "none") + ", expected " + expectation.SelectedRoot);
            return problems;
        }

        /// <summary>The installation of <paramref name="root"/>, or null.</summary>
        public static Installation Find(DiscoveryResult result, string root)
        {
            return result.Installations.FirstOrDefault(installation => WinPath.IsSamePath(installation.Root, root));
        }

        /// <summary>The installations for a message: one log line each, or "none".</summary>
        public static string Describe(DiscoveryResult result)
        {
            return result.Installations.Count == 0 ? "none" : string.Join("; ", result.Installations.Select(installation => installation.ToString()));
        }

        private static void CompareInstallation(InstallationExpectation expected, Installation found, List<string> problems)
        {
            void Check<T>(string what, T actual, T wanted)
            {
                if (!EqualityComparer<T>.Default.Equals(actual, wanted))
                    problems.Add(expected + ": " + what + " is " + Text(actual) + ", expected " + Text(wanted));
            }

            Check("the product", found.Product, expected.Product);
            if (expected.Kind != null)
                Check("the kind", found.Kind, expected.Kind.Value);
            if (expected.Mode != null)
                Check("the install mode", found.Mode, expected.Mode.Value);
            if (expected.AppId != null && !string.Equals(found.AppId, expected.AppId, StringComparison.OrdinalIgnoreCase))
                problems.Add(expected + ": the AppId is " + Text(found.AppId) + ", expected " + expected.AppId);
            if (expected.ContractVersion != null)
                Check("the contract version", found.ContractVersion, expected.ContractVersion.Value);
            if (expected.HasArtOfConquest != null)
                Check("HasArtOfConquest", found.HasArtOfConquest, expected.HasArtOfConquest.Value);
            if (expected.State != null)
                Check("the state", found.State, expected.State.Value);
            if (expected.MissingPrograms != null)
                Check("the missing programs", Names(found.MissingPrograms.Select(game => game.Id)), Names(expected.MissingPrograms.Select(game => game.Id)));
            if (expected.Sources != null)
                Check("the sources", Names(found.Sources.Select(source => source.ToString())), Names(expected.Sources.Select(source => source.ToString())));
            if (expected.SourcesInclude != null)
            {
                List<InstallationSource> missing = expected.SourcesInclude.Except(found.Sources).ToList();
                if (missing.Count > 0)
                    problems.Add(expected + ": the sources " + Names(found.Sources.Select(source => source.ToString())) + " lack " +
                                 Names(missing.Select(source => source.ToString())));
            }
            if (expected.ChecksOtherProductInRoot)
                Check("the other product in the root", found.OtherProductInRoot, expected.OtherProductInRoot);
            if (expected.GameVersion != null)
                Check("the game version", found.GameVersion, expected.GameVersion);
            if (expected.SetupVersion != null)
                Check("the setup version", found.SetupVersion, expected.SetupVersion);

            // Contract 1.4: the game folders of a community installation are fixed below its root.
            if (found.Kind == InstallationKind.Community || found.Kind == InstallationKind.CommunityLegacy)
            {
                foreach (Game game in found.HasArtOfConquest ? Game.All : new[] { Game.EmpireEarth })
                {
                    string folder = WinPath.Combine(found.Root, game.FolderName);
                    if (!WinPath.IsSamePath(found.GetGameFolder(game), folder))
                        problems.Add(expected + ": the folder of " + game.Id + " is " + found.GetGameFolder(game) + ", expected " + folder);
                }
            }
        }

        /// <summary>A set of names as one sorted text, for comparing and printing.</summary>
        private static string Names(IEnumerable<string> names)
        {
            return "{" + string.Join(",", names.Distinct().OrderBy(name => name, StringComparer.Ordinal)) + "}";
        }

        private static string Text(object value)
        {
            return value == null ? "none" : Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }
}
