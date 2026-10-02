using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Integrity;
using Empire_Earth_Launcher.Core.Maintenance;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;

namespace Empire_Earth_Launcher.Core.Diagnostics
{
    /// <summary>Everything the diagnostics report shows: the latest results of the launcher's checks (each may be missing).</summary>
    public sealed class DiagnosticsInput
    {
        /// <summary>The version of the launcher (<c>0.1.0-alpha</c>).</summary>
        public string LauncherVersion { get; set; }

        /// <summary>When the report is made (local time).</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>The Windows version, Wine and the screen.</summary>
        public ISystemInfo SystemInfo { get; set; }

        /// <summary>True on 64-bit Windows.</summary>
        public bool Is64BitWindows { get; set; }

        /// <summary>The UI language of the launcher (<c>de-DE</c>).</summary>
        public string UiCulture { get; set; }

        /// <summary>The installations; null while none was searched.</summary>
        public DiscoveryResult Discovery { get; set; }

        /// <summary>The file versions of the programs of the selected installation.</summary>
        public IReadOnlyList<ProgramVersion> ProgramVersions { get; set; }

        /// <summary>Whether a DirectX wrapper is installed for each game of the selected installation (contract 3.3).</summary>
        public IReadOnlyList<KeyValuePair<Game, RasterizerRecommendation>> DirectXWrappers { get; set; }

        /// <summary>The latest integrity check of the selected installation.</summary>
        public IntegrityReport Integrity { get; set; }

        /// <summary>The defaults state of each game of the selected installation (contract 3.6).</summary>
        public IReadOnlyList<KeyValuePair<Game, DefaultsStatus>> Defaults { get; set; }

        /// <summary>The consistency findings of the selected installation (contract 3.6).</summary>
        public IReadOnlyList<ConsistencyFinding> ConsistencyFindings { get; set; }

        /// <summary>The VirtualStore copies of the selected installation (R8).</summary>
        public VirtualStoreReport VirtualStore { get; set; }

        /// <summary>The name check of the selected installation (R10): only the numbers of the names are shown.</summary>
        public NameCheckReport Names { get; set; }

        /// <summary>The registry cleanup scan: only whether each <c>CDKeys</c> key exists is shown, and the counts.</summary>
        public CleanupScan Cleanup { get; set; }

        /// <summary>The latest network diagnostics; null if they did not run.</summary>
        public NetworkReport Network { get; set; }
    }

    /// <summary>
    /// The diagnostics report (ARCHITECTURE 4.6): one English text, the support language of the forum like <c>log.txt</c>,
    /// with the launcher and Windows version, the screen and the display adapter, the installations, the file versions,
    /// whether a DirectX wrapper is installed, the integrity state and its findings, the game defaults and the consistency
    /// findings, the VirtualStore, the CD keys as "exists"/"missing" and the network. The player copies or saves it; the
    /// launcher never sends it anywhere.
    /// </summary>
    /// <remarks>
    /// The privacy rules of ADR 0013 (plan review) are built in: every path passes <see cref="ReportAnonymizer"/>; no CD-key
    /// value (contract 3.8, O8), no content of the backup folder, no player or profile name (only "EE lobby profile 1:
    /// characters outside printable ASCII"), no MAC, adapter GUID, adapter name or DNS suffix, IPv4 only private or
    /// link-local, IPv6 and the external address only as their class, no computer or domain name, no exception message
    /// (only result codes). Lines end with CRLF, as the Windows clipboard and Notepad expect.
    /// </remarks>
    public static class DiagnosticsReport
    {
        /// <summary>At most this many findings of a list are shown; the rest is counted.</summary>
        public const int MaxListed = 20;

        private const string Indent = "  ";

        /// <summary>Builds the report text.</summary>
        public static string Build(DiagnosticsInput input, ReportAnonymizer anonymizer)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            if (anonymizer == null)
                throw new ArgumentNullException(nameof(anonymizer));
            var lines = new List<string>
            {
                "Empire Earth Launcher - diagnostics report",
                "==========================================",
                "Made by the launcher on request and never sent anywhere. It contains no CD keys, no login data, no player or",
                "profile names, no MAC or public IP address and no user or computer name (they are replaced by <user>,",
                "<computer> and <server>). Please read it before you post it.",
                string.Empty,
                "Created:      " + input.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                "Launcher:     " + (input.LauncherVersion ?? "?"),
            };
            AddWindows(lines, input);
            lines.Add(string.Empty);
            AddInstallations(lines, input.Discovery, anonymizer);
            lines.Add(string.Empty);
            AddSelected(lines, input, anonymizer);
            lines.Add(string.Empty);
            AddNetwork(lines, input.Network, anonymizer);
            return string.Join("\r\n", lines) + "\r\n";
        }

        private static void AddWindows(List<string> lines, DiagnosticsInput input)
        {
            ISystemInfo system = input.SystemInfo;
            if (system == null)
            {
                lines.Add("Windows:      ?");
                return;
            }
            lines.Add("Windows:      NT " + system.WindowsVersion + (input.Is64BitWindows ? ", 64-bit" : ", 32-bit") +
                      (system.IsWine ? ", Wine" : string.Empty));
            lines.Add("Screen:       " + system.PrimaryScreen + " physical, " + system.PrimaryScreenUnaware +
                      " for DPI-unaware programs (" + system.ScalingPercent().ToString(CultureInfo.InvariantCulture) + " %)");
            lines.Add("Display:      " + (string.IsNullOrWhiteSpace(system.PrimaryDisplayAdapter) ? "?" : system.PrimaryDisplayAdapter.Trim()));
            lines.Add("UI language:  " + (string.IsNullOrEmpty(input.UiCulture) ? "?" : input.UiCulture));
        }

        private static void AddInstallations(List<string> lines, DiscoveryResult discovery, ReportAnonymizer anonymizer)
        {
            if (discovery == null)
            {
                lines.Add("Installations: not searched yet");
                return;
            }
            lines.Add("Installations (" + discovery.Installations.Count.ToString(CultureInfo.InvariantCulture) + ")" +
                      (discovery.UserChoice == null ? string.Empty : ", folder chosen in the launcher: " + anonymizer.Path(discovery.UserChoice)));
            int number = 0;
            foreach (Installation installation in discovery.Installations)
            {
                number++;
                lines.Add(Indent + number.ToString(CultureInfo.InvariantCulture) + ". " + installation.Product.Id + ", " +
                          KindName(installation.Kind) + ", mode " + installation.Mode.ToString().ToLowerInvariant() + ", contract " +
                          installation.ContractVersion.ToString(CultureInfo.InvariantCulture) +
                          (installation == discovery.Selected ? ", selected" + (discovery.IsSelectedByUser ? " by the player" : string.Empty) : string.Empty));
                lines.Add(Indent + Indent + "Root:       " + anonymizer.Path(installation.Root));
                lines.Add(Indent + Indent + "EE folder:  " + anonymizer.Path(installation.EeFolder));
                lines.Add(Indent + Indent + "AoC folder: " + (installation.AocFolder == null ? "none" : anonymizer.Path(installation.AocFolder)));
                lines.Add(Indent + Indent + "State:      " + StateName(installation) +
                          (installation.OtherProductInRoot == null ? string.Empty : ", shares its root with " + installation.OtherProductInRoot.Id));
                lines.Add(Indent + Indent + "Setup:      " + (installation.SetupVersion ?? "?") +
                          (installation.SetupBuild == null ? string.Empty : " (build " + installation.SetupBuild + ")") +
                          ", game " + (installation.GameVersion ?? "?") + ", AppId " + (installation.AppId ?? "none") +
                          ", sources " + string.Join(",", installation.Sources.Select(source => ((int)source).ToString(CultureInfo.InvariantCulture))));
            }
        }

        private static void AddSelected(List<string> lines, DiagnosticsInput input, ReportAnonymizer anonymizer)
        {
            Installation selected = input.Discovery?.Selected;
            lines.Add("Selected installation" + (selected == null ? ": none" : string.Empty));
            if (selected == null)
            {
                AddCdKeys(lines, input.Cleanup);
                return;
            }
            lines.Add(Indent + "Programs:     " + (input.ProgramVersions == null || input.ProgramVersions.Count == 0
                ? "?"
                : string.Join("; ", input.ProgramVersions.Select(version => version.ToString()))));
            lines.Add(Indent + "DirectX wrapper: " + (input.DirectXWrappers == null || input.DirectXWrappers.Count == 0
                ? "?"
                : string.Join("; ", input.DirectXWrappers.Select(wrapper => wrapper.Key.Id + " " + WrapperName(wrapper.Value)))));
            AddIntegrity(lines, input.Integrity);
            lines.Add(Indent + "Game defaults: " + (input.Defaults == null || input.Defaults.Count == 0
                ? "?"
                : string.Join("; ", input.Defaults.Select(line => line.Key.Id + " " + DefaultsName(line.Value)))));
            AddConsistency(lines, input.ConsistencyFindings, anonymizer);
            AddVirtualStore(lines, input.VirtualStore, anonymizer);
            AddNames(lines, input.Names);
            AddCdKeys(lines, input.Cleanup);
        }

        private static string WrapperName(RasterizerRecommendation wrapper)
        {
            switch (wrapper.Reason)
            {
                case RasterizerReason.WrapperInInstallInfo:
                    return "installed (install.ini)";
                case RasterizerReason.WrapperInUninstallKey:
                    return "installed (uninstall key)";
                case RasterizerReason.WrapperFile:
                    return "installed (" + wrapper.WrapperFile + " in the game folder)";
                case RasterizerReason.NoWrapperInInstallInfo:
                    return "none (install.ini)";
                case RasterizerReason.NoWrapperInUninstallKey:
                    return "none (uninstall key)";
                case RasterizerReason.NoWrapperFile:
                    return "none (no wrapper file in the game folder)";
                default:
                    return "?";
            }
        }

        private static void AddIntegrity(List<string> lines, IntegrityReport report)
        {
            if (report == null)
            {
                lines.Add(Indent + "Integrity:    not checked yet");
                return;
            }
            string state = report.State.ToString() +
                           (report.UnknownReason != UnknownReason.None ? " (" + report.UnknownReason + ")" : string.Empty) +
                           (report.CancelReason != CancelReason.None ? " (" + report.CancelReason + ")" : string.Empty) +
                           (report.IsUnreliable ? ", unreliable (two products in one folder)" : string.Empty);
            lines.Add(Indent + "Integrity:    " + state + ", " + report.Kind.ToString().ToLowerInvariant() + " check, " +
                      ReportAnonymizer.Count(report.ListedFiles, "file", "files") + " listed, " +
                      report.HashedFiles.ToString(CultureInfo.InvariantCulture) + " hashed, " +
                      ReportAnonymizer.Count(report.Findings.Count, "finding", "findings"));
            foreach (IntegrityFinding finding in report.Findings.Take(MaxListed))
                lines.Add(Indent + Indent + "- " + finding.Class.ToString().ToLowerInvariant() + " " + finding.Path + ": " + KindName(finding.Kind));
            AddMore(lines, report.Findings.Count);
        }

        private static void AddConsistency(List<string> lines, IReadOnlyList<ConsistencyFinding> findings, ReportAnonymizer anonymizer)
        {
            if (findings == null)
            {
                lines.Add(Indent + "Consistency:  not checked yet");
                return;
            }
            lines.Add(Indent + "Consistency:  " + (findings.Count == 0 ? "no findings" : ReportAnonymizer.Count(findings.Count, "finding", "findings")));
            foreach (ConsistencyFinding finding in findings.Take(MaxListed))
            {
                string values = finding.Code == FindingCode.InstalledFromNotOnADrive || finding.Code == FindingCode.FolderOutsideAnsiCodePage
                    ? anonymizer.Path(finding.Installation.GetGameFolder(finding.Game))
                    : finding.HintValues;
                lines.Add(Indent + Indent + "- " + finding.Code + (finding.Game == null ? string.Empty : " (" + finding.Game.Id + ")") + ": " +
                          values + (finding.Recommended == null ? string.Empty : ", recommended " + finding.Recommended));
            }
            AddMore(lines, findings.Count);
        }

        private static void AddVirtualStore(List<string> lines, VirtualStoreReport report, ReportAnonymizer anonymizer)
        {
            if (report == null)
            {
                lines.Add(Indent + "VirtualStore: not checked yet");
                return;
            }
            if (!report.IsVirtualizable)
            {
                lines.Add(Indent + "VirtualStore: not used (the game folders are not below Program Files, ProgramData or Windows)");
                return;
            }
            int serious = report.Findings.Count(finding => finding.IsSerious);
            lines.Add(Indent + "VirtualStore: " + ReportAnonymizer.Count(report.Findings.Count, "file", "files") + ", " +
                      serious.ToString(CultureInfo.InvariantCulture) + " serious" + (report.Truncated ? ", more not listed" : string.Empty) +
                      (report.ManifestUnusable ? ", manifest unreadable" : string.Empty));
            foreach (VirtualStoreFinding finding in report.Findings.Take(MaxListed))
                lines.Add(Indent + Indent + "- " + (finding.IsSerious ? "serious " : string.Empty) + finding.Reason + ": " +
                          anonymizer.Path(finding.VirtualStorePath) + (finding.OriginalExists ? " (shadows the file of the game folder)" : string.Empty));
            AddMore(lines, report.Findings.Count);
        }

        /// <summary>ADR 0013 plan review: names only as their number and source, never the name.</summary>
        private static void AddNames(List<string> lines, NameCheckReport report)
        {
            if (report == null)
            {
                lines.Add(Indent + "Player names: not checked yet");
                return;
            }
            lines.Add(Indent + "Player names: " + ReportAnonymizer.Count(report.NamesChecked, "name", "names") + " checked, " +
                      report.Warnings.Count.ToString(CultureInfo.InvariantCulture) + " with characters outside printable ASCII");
            foreach (NameWarning warning in report.Warnings.Take(MaxListed))
                lines.Add(Indent + Indent + "- " + warning.Game.Id + " " +
                          (warning.Source == NameSource.LobbyProfile ? "lobby profile " : "player ") +
                          warning.Number.ToString(CultureInfo.InvariantCulture) + ": characters outside printable ASCII");
            AddMore(lines, report.Warnings.Count);
        }

        /// <summary>Contract 3.8, O8: whether each <c>CDKeys</c> key exists, never a value.</summary>
        private static void AddCdKeys(List<string> lines, CleanupScan cleanup)
        {
            if (cleanup == null)
            {
                lines.Add(Indent + "CD keys:      not checked yet");
                return;
            }
            var keys = cleanup.Items.Where(item => item.Entry.Scope == CleanupScope.Protected)
                .Select(item => CleanupCandidates.CdKeysBelow(item.Entry.Key) + " " +
                                (item.State == CleanupState.Missing ? "missing"
                                    : item.Advice.CdKeysExist == true ? "exists"
                                    : item.Advice.CdKeysExist == false ? "missing" : "unreadable"))
                .ToList();
            lines.Add(Indent + "CD keys:      " + string.Join("; ", keys));
            lines.Add(Indent + "Old registry entries: " + ReportAnonymizer.Count(cleanup.Offered.Count, "key", "keys") + " offered for cleanup, " +
                      cleanup.ReadOnly.Count.ToString(CultureInfo.InvariantCulture) + " shown read-only");
        }

        private static void AddNetwork(List<string> lines, NetworkReport report, ReportAnonymizer anonymizer)
        {
            if (report == null)
            {
                lines.Add("Network: not checked (Tools page, \"Check network\")");
                return;
            }
            lines.Add("Network (checked " + report.CheckedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + ")");
            lines.Add(Indent + "Verdict:      " + OutageHint.Describe(report.Verdict));
            if (report.Adapters.Problem != null)
                lines.Add(Indent + "Adapters:     could not be listed");
            else
            {
                lines.Add(Indent + "Adapters:     " + report.Adapters.Adapters.Count.ToString(CultureInfo.InvariantCulture));
                foreach (NetworkAdapter adapter in report.Adapters.Adapters)
                    lines.Add(Indent + Indent + "- " + NetworkDiagnostics.Describe(adapter));
            }
            foreach (DnsLookup lookup in report.Lookups)
                lines.Add(Indent + "DNS:          " + lookup.Host + ": " + DescribeLookup(lookup));
            lines.Add(Indent + "Update API:   " + (report.UpdateApiResponse == null
                ? "not asked (no installation with an AppId)"
                : report.UpdateApi == UpdateApiAnswer.Answered
                    ? "answered (HTTP " + report.UpdateApiResponse.StatusCode.ToString(CultureInfo.InvariantCulture) + ")"
                    : "no answer (" + report.UpdateApiResponse.Outcome + ")"));
            lines.Add(Indent + "Status server: " + (report.StatusEndpoint == null
                ? "not configured"
                : report.StatusEndpoint + ": " + (report.StatusServer == StatusServerAnswer.Answered
                    ? "answered, " + (report.OnlinePlayers ?? 0).ToString(CultureInfo.InvariantCulture) + " players online"
                    : "no answer (" + (report.StatusError ?? "?") + ")")));
            foreach (NeoEeConfig config in report.NeoEeConfigs)
                lines.Add(Indent + config.Game.Id + " " + NeoEeConfigReader.FileName + ": " + NetworkDiagnostics.DescribeValues(config));
            foreach (WonLobbyConfig config in report.WonLobbyConfigs)
                lines.Add(Indent + config.Game.Id + " " + WonLobbyConfigReader.FileName + ": " + NetworkDiagnostics.DescribeValues(config));
            foreach (UpnpInfo info in report.UpnpInfos)
                lines.Add(Indent + info.Game.Id + " " + UpnpInfoParser.FileName + ": " + NetworkDiagnostics.DescribeValues(info));
            foreach (PortForwarding table in report.PortForwarding)
                lines.Add(Indent + (table.Game.Id + " ports:").PadRight(14) + table + " to " +
                          (report.ForwardingTarget == null ? "the IPv4 address of this computer" : ReportAnonymizer.Address(report.ForwardingTarget)));
            lines.Add(Indent + "Hints:        " + (report.Hints.Count == 0 ? "none" : string.Join(", ", report.Hints)));
        }

        /// <summary>A lookup without the addresses of the server: they do not help the support and change.</summary>
        private static string DescribeLookup(DnsLookup lookup)
        {
            if (!lookup.IsResolved)
                return lookup.Outcome == DnsOutcome.NotFound ? "not found" : lookup.Outcome.ToString().ToLowerInvariant();
            int ipv4 = lookup.Addresses.Count(address => address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            return "resolved (" + ipv4.ToString(CultureInfo.InvariantCulture) + " IPv4, " +
                   (lookup.Addresses.Count - ipv4).ToString(CultureInfo.InvariantCulture) + " IPv6)";
        }

        private static void AddMore(List<string> lines, int count)
        {
            if (count > MaxListed)
                lines.Add(Indent + Indent + "... and " + (count - MaxListed).ToString(CultureInfo.InvariantCulture) + " more");
        }

        private static string KindName(InstallationKind kind)
        {
            switch (kind)
            {
                case InstallationKind.Community:
                    return "community setup";
                case InstallationKind.CommunityLegacy:
                    return "community setup 1.7.2 or older";
                default:
                    return "other (CD, GOG, copy)";
            }
        }

        private static string KindName(FindingKind kind)
        {
            switch (kind)
            {
                case FindingKind.Missing:
                    return "missing";
                case FindingKind.HashDiffers:
                    return "changed";
                case FindingKind.MissingAfterInstall:
                    return "missing after the installation";
                default:
                    return "unreadable";
            }
        }

        private static string StateName(Installation installation)
        {
            switch (installation.State)
            {
                case InstallationState.Damaged:
                    return "damaged, missing " + string.Join(", ", installation.MissingPrograms.Select(game => game.ProgramName));
                case InstallationState.FolderMissing:
                    return "folder missing";
                default:
                    return "ok";
            }
        }

        private static string DefaultsName(DefaultsStatus status)
        {
            switch (status)
            {
                case DefaultsStatus.Applied:
                    return "applied";
                case DefaultsStatus.AppliedByNewerVersion:
                    return "applied by a newer version";
                case DefaultsStatus.Pending:
                    return "pending (applied at the next start)";
                case DefaultsStatus.WaitingForPlay:
                    return "waiting for the first Play (shared settings key)";
                default:
                    return "not applied (newer contract)";
            }
        }
    }
}
