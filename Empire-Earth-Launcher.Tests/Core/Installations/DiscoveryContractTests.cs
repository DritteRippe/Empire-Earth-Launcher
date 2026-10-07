using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.TestSupport;
using Microsoft.Win32;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Installations
{
    /// <summary>
    /// Table test over every rule of contract 1.4 ("Discovery by the launcher": the five sources and the rules Validity,
    /// Merge, Kind, Two products in one root, AoC folder, Default selection, Errors, Read-only) and every row of the table
    /// of 1.5 (installations of setups up to 1.7.2). <see cref="EveryRuleOfTheContractHasACase"/> reads the rules from
    /// <c>docs/CONTRACT.md</c>, so a rule added to the contract without a case fails here.
    /// </summary>
    [TestFixture]
    public class DiscoveryContractTests
    {
        private const string NeoRoot = @"C:\Program Files (x86)\Neo Empire Earth";
        private const string EERoot = @"C:\Program Files (x86)\Empire Earth";
        private const string UserRoot = @"C:\Users\Player\AppData\Local\Programs\Empire Earth";

        /// <summary>One line of the table: the contract rule, a scenario and what the discovery must find.</summary>
        public sealed class ContractCase
        {
            internal ContractCase(string rule, string name, Action<InstallationWorld> arrange,
                Action<InstallationWorld, DiscoveryResult> verify, string userChoice = null, string launcherFolder = null,
                UserChoice[] choices = null)
            {
                Choices = choices;
                Rule = rule;
                Name = name;
                Arrange = arrange;
                Verify = verify;
                UserChoice = userChoice;
                LauncherFolder = launcherFolder;
            }

            /// <summary>The bold label of the rule in contract 1.4, or "1.5 row n".</summary>
            public string Rule { get; }

            public string Name { get; }

            internal Action<InstallationWorld> Arrange { get; }

            internal Action<InstallationWorld, DiscoveryResult> Verify { get; }

            public string UserChoice { get; }

            public string LauncherFolder { get; }

            /// <summary>The folders chosen per product (revision 6); instead of <see cref="UserChoice"/> if given.</summary>
            public UserChoice[] Choices { get; }

            public override string ToString()
            {
                return Rule + ": " + Name;
            }
        }

        private static Installation Only(DiscoveryResult result)
        {
            Assert.That(result.Installations, Has.Count.EqualTo(1), "installations: " + string.Join(" | ", result.Installations));
            return result.Installations[0];
        }

        private static string[] Roots(DiscoveryResult result)
        {
            return result.Installations.Select(installation => installation.Root).ToArray();
        }

        internal static IEnumerable<ContractCase> AllCases()
        {
            // --- 1. User choice ----------------------------------------------------------------------------------------
            const string userChoice = "User choice";
            yield return new ContractCase(userChoice, "the install root selects the installation",
                w => w.AddCommunityInstallation(NeoRoot, Product.NeoEE),
                (w, r) =>
                {
                    Assert.That(r.IsSelectedByUser, Is.True);
                    Assert.That(Only(r).Sources, Does.Contain(InstallationSource.UserChoice).And.Contain(InstallationSource.RegistryRecord));
                },
                userChoice: NeoRoot);
            yield return new ContractCase(userChoice, "the EE folder selects the installation",
                w => w.AddCommunityInstallation(NeoRoot, Product.NeoEE),
                (w, r) =>
                {
                    Assert.That(r.Selected.Root, Is.EqualTo(NeoRoot));
                    Assert.That(r.IsSelectedByUser, Is.True);
                },
                userChoice: NeoRoot + @"\Empire Earth");
            yield return new ContractCase(userChoice, "it wins against the other sources",
                w =>
                {
                    w.AddCommunityInstallation(NeoRoot, Product.NeoEE);
                    w.AddLegacyInstallation(EERoot, Product.EE);
                },
                (w, r) =>
                {
                    Assert.That(r.Selected.Root, Is.EqualTo(EERoot));
                    Assert.That(r.Installations, Has.Count.EqualTo(2));
                },
                userChoice: EERoot);
            yield return new ContractCase(userChoice, "it is kept even if it does not exist",
                w => w.AddCommunityInstallation(NeoRoot, Product.NeoEE),
                (w, r) =>
                {
                    Assert.That(r.Selected.EeFolder, Is.EqualTo(@"D:\Removed\Empire Earth"));
                    Assert.That(r.Selected.State, Is.EqualTo(InstallationState.FolderMissing));
                    Assert.That(r.Installations, Has.Count.EqualTo(2));
                },
                userChoice: @"D:\Removed\Empire Earth");
            yield return new ContractCase(userChoice, "a portable installation is found through it (O5)",
                w =>
                {
                    w.AddCommunityFiles(@"D:\EE Portable", Product.EE);
                    w.AddInstallInfo(@"D:\EE Portable", Product.EE, 1, "portable");
                },
                (w, r) =>
                {
                    Assert.That(Only(r).Kind, Is.EqualTo(InstallationKind.Community));
                    Assert.That(r.Selected.Mode, Is.EqualTo(InstallMode.Portable));
                },
                userChoice: @"D:\EE Portable");

            // --- 2. Registry records -----------------------------------------------------------------------------------
            const string records = "Registry records";
            yield return new ContractCase(records, "an admin record in HKLM64",
                w => w.AddCommunityInstallation(NeoRoot, Product.NeoEE),
                (w, r) =>
                {
                    Installation installation = Only(r);
                    Assert.That(installation.Origin, Is.EqualTo(InstallationSource.RegistryRecord));
                    Assert.That(installation.RecordKey.ToString(), Is.EqualTo(@"HKLM64\Software\Empire Earth Community\Installations\NeoEE"));
                    Assert.That(installation.Mode, Is.EqualTo(InstallMode.Admin));
                    Assert.That(installation.AppId, Is.EqualTo(InstallationWorld.NeoEEAppId));
                });
            yield return new ContractCase(records, "product NeoEE before EE",
                w =>
                {
                    w.AddCommunityInstallation(EERoot, Product.EE);
                    w.AddCommunityInstallation(NeoRoot, Product.NeoEE);
                },
                (w, r) => Assert.That(Roots(r), Is.EqualTo(new[] { NeoRoot, EERoot })));
            yield return new ContractCase(records, "per product HKCU, then HKLM64, then HKLM32",
                w =>
                {
                    foreach (string root in new[] { @"C:\A32", @"C:\B64", @"C:\CCU" })
                        w.AddCommunityFiles(root, Product.EE);
                    w.AddRecord(RegistryHive.LocalMachine, RegistryView.Registry32, Product.EE, @"C:\A32");
                    w.AddRecord(RegistryHive.LocalMachine, RegistryView.Registry64, Product.EE, @"C:\B64");
                    w.AddRecord(RegistryHive.CurrentUser, RegistryView.Default, Product.EE, @"C:\CCU", mode: "user");
                },
                (w, r) => Assert.That(Roots(r), Is.EqualTo(new[] { @"C:\CCU", @"C:\B64", @"C:\A32" })));

            // --- 3. Uninstall keys -------------------------------------------------------------------------------------
            const string uninstall = "Uninstall keys";
            yield return new ContractCase(uninstall, "the key of a 1.7.2 user installation in HKCU",
                w =>
                {
                    w.AddCommunityFiles(UserRoot, Product.EE);
                    w.AddUninstallKey(RegistryHive.CurrentUser, RegistryView.Default, Product.EE, UserRoot);
                },
                (w, r) =>
                {
                    Installation installation = Only(r);
                    Assert.That(installation.Origin, Is.EqualTo(InstallationSource.UninstallKey));
                    Assert.That(installation.Mode, Is.EqualTo(InstallMode.User));
                    Assert.That(installation.AppId, Is.EqualTo(InstallationWorld.EEAppId), "the GUID of the key name");
                });
            yield return new ContractCase(uninstall, "only the exact publishers of the contract",
                w =>
                {
                    w.AddCommunityFiles(EERoot, Product.EE);
                    w.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, Product.EE, EERoot, publisher: "Sierra");
                },
                (w, r) => Assert.That(r.Installations, Is.Empty));
            yield return new ContractCase(uninstall, "App Path before InstallLocation",
                w =>
                {
                    w.AddCommunityFiles(EERoot, Product.EE);
                    RegistryLocation key = w.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, Product.EE, EERoot);
                    w.FileSystem.AddDirectory(@"C:\Elsewhere");
                    w.Registry.Seed(key, ContractNames.UninstallInstallLocationName, RegistryValue.FromString(@"C:\Elsewhere\"));
                },
                (w, r) => Assert.That(Only(r).Root, Is.EqualTo(EERoot)));
            yield return new ContractCase(uninstall, "NeoEE before EE",
                w =>
                {
                    w.AddLegacyInstallation(EERoot, Product.EE);
                    w.AddLegacyInstallation(NeoRoot, Product.NeoEE);
                },
                (w, r) => Assert.That(Roots(r), Is.EqualTo(new[] { NeoRoot, EERoot })));

            yield return new ContractCase(uninstall, "the uninstall key of the suite (marker) is no candidate",
                w =>
                {
                    w.AddLegacyInstallation(EERoot, Product.EE);
                    w.FileSystem.AddDirectory(InstallationWorld.SuiteRoot);
                    w.AddSuiteUninstallKey();
                },
                (w, r) => Assert.That(Roots(r), Is.EqualTo(new[] { EERoot })));
            yield return new ContractCase(uninstall, "the suite key without marker is skipped by the suite root of the record",
                w =>
                {
                    w.AddLegacyInstallation(EERoot, Product.EE);
                    w.FileSystem.AddDirectory(InstallationWorld.SuiteRoot);
                    w.AddSuiteUninstallKey(marker: false);
                    w.AddSuiteRecord();
                },
                (w, r) => Assert.That(Roots(r), Is.EqualTo(new[] { EERoot })));
            yield return new ContractCase(uninstall, "without marker and record the suite key counts as before",
                w =>
                {
                    w.AddLegacyInstallation(EERoot, Product.EE);
                    w.FileSystem.AddDirectory(InstallationWorld.SuiteRoot);
                    w.AddSuiteUninstallKey(marker: false);
                },
                (w, r) => Assert.That(Roots(r), Is.EqualTo(new[] { InstallationWorld.SuiteRoot, EERoot }).IgnoreCase));
            yield return new ContractCase(uninstall, "a product key with an AppId of the record stays",
                w =>
                {
                    w.AddCommunityFiles(InstallationWorld.SuiteRoot, Product.EE);
                    w.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, Product.EE, InstallationWorld.SuiteRoot);
                    w.AddSuiteRecord();
                },
                (w, r) => Assert.That(Roots(r), Is.EqualTo(new[] { InstallationWorld.SuiteRoot }).IgnoreCase));

            // --- 4. "Installed From" values ----------------------------------------------------------------------------
            const string installedFrom = "\"Installed From\" values";
            yield return new ContractCase(installedFrom, "they name the EE folder, the root is its parent",
                w => w.AddForeignInstallation(@"C:\Games\EE"),
                (w, r) =>
                {
                    Installation installation = Only(r);
                    Assert.That(installation.EeFolder, Is.EqualTo(@"C:\Games\EE").IgnoreCase);
                    Assert.That(installation.Root, Is.EqualTo(@"C:\Games").IgnoreCase);
                    Assert.That(installation.Origin, Is.EqualTo(InstallationSource.InstalledFrom));
                });
            yield return new ContractCase(installedFrom, "key before hive: SSSI in HKCU + Neo in HKLM32 -> Neo first",
                w =>
                {
                    w.AddForeignInstallation(@"C:\Retail\Empire Earth");
                    w.AddForeignInstallation(@"C:\NeoCopy\Empire Earth", RegistryHive.LocalMachine, RegistryView.Registry32, neoKey: true);
                },
                (w, r) => Assert.That(r.Selected.Root, Is.EqualTo(@"C:\NeoCopy").IgnoreCase));
            yield return new ContractCase(installedFrom, "retail, GOG and older installations use the SSSI key",
                w => w.AddForeignInstallation(@"C:\GOG Games\Empire Earth Gold\Empire Earth"),
                (w, r) =>
                {
                    Assert.That(Only(r).Kind, Is.EqualTo(InstallationKind.Foreign));
                    Assert.That(r.Selected.Product, Is.SameAs(Product.EE));
                    Assert.That(r.Selected.InstalledFromKey.ToString(), Is.EqualTo(@"HKCU\Software\SSSI\Empire Earth"));
                });

            // --- 5. Launcher folder ------------------------------------------------------------------------------------
            const string launcher = "Launcher folder";
            yield return new ContractCase(launcher, "the launcher in the EE folder",
                w => w.AddEmpireEarth(@"D:\Copy\EE"),
                (w, r) =>
                {
                    Assert.That(Only(r).EeFolder, Is.EqualTo(@"D:\Copy\EE"));
                    Assert.That(r.Selected.Origin, Is.EqualTo(InstallationSource.LauncherFolder));
                },
                launcherFolder: @"D:\Copy\EE\");
            yield return new ContractCase(launcher, "the launcher in a folder of the install root",
                w => w.AddCommunityFiles(@"D:\Portable EE", Product.EE),
                (w, r) => Assert.That(Only(r).Root, Is.EqualTo(@"D:\Portable EE")),
                launcherFolder: @"D:\Portable EE\Launcher");
            yield return new ContractCase(launcher, "a launcher folder that is neither",
                w => w.FileSystem.AddDirectory(@"D:\Tools\Launcher"),
                (w, r) => Assert.That(r.Installations, Is.Empty),
                launcherFolder: @"D:\Tools\Launcher");

            // --- Validity -----------------------------------------------------------------------------------------------
            const string validity = "Validity";
            yield return new ContractCase(validity, "a root that does not exist is dropped",
                w => w.AddRecord(RegistryHive.LocalMachine, RegistryView.Registry64, Product.NeoEE, @"C:\Uninstalled"),
                (w, r) =>
                {
                    Assert.That(r.Installations, Is.Empty);
                    Assert.That(w.LogLinesAbout(@"C:\Uninstalled"), Has.Length.EqualTo(1));
                });
            yield return new ContractCase(validity, "a missing Empire Earth.exe means damaged, not dropped",
                w =>
                {
                    w.AddCommunityInstallation(NeoRoot, Product.NeoEE);
                    w.FileSystem.DeleteFile(NeoRoot + @"\Empire Earth\Empire Earth.exe");
                },
                (w, r) =>
                {
                    Assert.That(Only(r).State, Is.EqualTo(InstallationState.Damaged));
                    Assert.That(r.Selected.MissingPrograms, Is.EqualTo(new[] { Game.EmpireEarth }));
                });
            yield return new ContractCase(validity, "source 4 needs the EE folder",
                w =>
                {
                    w.FileSystem.AddDirectory(@"C:\Games");
                    w.SetInstalledFrom(InstallationWorld.Hkcu(@"Software\SSSI\Empire Earth"), @"C:\Games\EE");
                },
                (w, r) => Assert.That(r.Installations, Is.Empty));

            // --- Merge -------------------------------------------------------------------------------------------------
            const string merge = "Merge";
            yield return new ContractCase(merge, "record, uninstall key and Installed From of one root are one installation",
                w => w.AddCommunityInstallation(NeoRoot, Product.NeoEE),
                (w, r) => Assert.That(Only(r).Sources, Is.EqualTo(new[]
                {
                    InstallationSource.RegistryRecord, InstallationSource.UninstallKey, InstallationSource.InstalledFrom
                })));
            yield return new ContractCase(merge, "roots compared ignoring case, separators and trailing backslashes",
                w =>
                {
                    w.AddCommunityFiles(NeoRoot, Product.NeoEE);
                    w.AddInstallInfo(NeoRoot, Product.NeoEE);
                    w.AddRecord(RegistryHive.LocalMachine, RegistryView.Registry64, Product.NeoEE, NeoRoot + @"\");
                    w.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, Product.NeoEE,
                        "c:/program files (x86)//neo empire earth");
                },
                (w, r) => Assert.That(Only(r).Root, Is.EqualTo(NeoRoot), "the spelling of the most specific source"));
            yield return new ContractCase(merge, "the data of the most specific source wins",
                w =>
                {
                    w.AddCommunityFiles(NeoRoot, Product.NeoEE);
                    w.AddRecord(RegistryHive.LocalMachine, RegistryView.Registry64, Product.NeoEE, NeoRoot, appId: "AAAAAAAA-0000-0000-0000-000000000000");
                    w.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, Product.NeoEE, NeoRoot);
                },
                (w, r) => Assert.That(Only(r).AppId, Is.EqualTo("AAAAAAAA-0000-0000-0000-000000000000")));
            yield return new ContractCase(merge, "the user choice only selects",
                w => w.AddCommunityInstallation(NeoRoot, Product.NeoEE),
                (w, r) =>
                {
                    Assert.That(Only(r).Origin, Is.EqualTo(InstallationSource.RegistryRecord));
                    Assert.That(r.Selected.Kind, Is.EqualTo(InstallationKind.Community));
                },
                userChoice: NeoRoot.ToUpperInvariant() + @"\");

            // --- Kind --------------------------------------------------------------------------------------------------
            const string kind = "Kind";
            yield return new ContractCase(kind, "install.ini with ContractVersion 1 means community",
                w => w.AddCommunityInstallation(NeoRoot, Product.NeoEE),
                (w, r) =>
                {
                    Assert.That(Only(r).Kind, Is.EqualTo(InstallationKind.Community));
                    Assert.That(r.Selected.ContractVersion, Is.EqualTo(1));
                    Assert.That(r.Selected.InstallInfo, Is.Not.Null);
                });
            yield return new ContractCase(kind, "an uninstall key without contract version means community-legacy",
                w => w.AddLegacyInstallation(EERoot, Product.EE),
                (w, r) => Assert.That(Only(r).Kind, Is.EqualTo(InstallationKind.CommunityLegacy)));
            yield return new ContractCase(kind, "install.ini with ContractVersion 0 and an uninstall key means community-legacy",
                w =>
                {
                    w.AddLegacyInstallation(EERoot, Product.EE);
                    w.AddInstallInfo(EERoot, Product.EE, 0);
                },
                (w, r) => Assert.That(Only(r).Kind, Is.EqualTo(InstallationKind.CommunityLegacy)));
            yield return new ContractCase(kind, "nothing of a setup means foreign, product EE",
                w => w.AddForeignInstallation(@"C:\Sierra\Empire Earth"),
                (w, r) =>
                {
                    Assert.That(Only(r).Kind, Is.EqualTo(InstallationKind.Foreign));
                    Assert.That(r.Selected.Product, Is.SameAs(Product.EE));
                    Assert.That(r.Selected.Mode, Is.EqualTo(InstallMode.Unknown));
                    Assert.That(r.Selected.AppId, Is.Null);
                });
            yield return new ContractCase(kind, "foreign with neoee.dll in the EE folder means NeoEE",
                w =>
                {
                    w.AddEmpireEarth(@"C:\NeoCopy\Empire Earth", neoee: true);
                    w.SetInstalledFrom(InstallationWorld.Hkcu(@"Software\SSSI\Empire Earth"), @"C:\NeoCopy\Empire Earth");
                },
                (w, r) =>
                {
                    Assert.That(Only(r).Kind, Is.EqualTo(InstallationKind.Foreign));
                    Assert.That(r.Selected.Product, Is.SameAs(Product.NeoEE));
                });
            yield return new ContractCase(kind, "install.ini is read for roots of sources 1, 4 and 5 too",
                w =>
                {
                    w.AddCommunityFiles(@"D:\Neo Portable", Product.NeoEE);
                    w.AddInstallInfo(@"D:\Neo Portable", Product.NeoEE, 1, "portable");
                    w.SetInstalledFrom(InstallationWorld.Hkcu(@"Software\Neo\Empire Earth"), @"D:\Neo Portable\Empire Earth");
                },
                (w, r) =>
                {
                    Assert.That(Only(r).Kind, Is.EqualTo(InstallationKind.Community));
                    Assert.That(r.Selected.Mode, Is.EqualTo(InstallMode.Portable));
                    Assert.That(r.Selected.Origin, Is.EqualTo(InstallationSource.InstalledFrom));
                });

            // --- Two products in one root -----------------------------------------------------------------------------
            const string twoProducts = "Two products in one root";
            yield return new ContractCase(twoProducts, "the install.ini modified last is used and the root is flagged",
                w =>
                {
                    w.AddCommunityInstallation(@"C:\Games\Shared", Product.NeoEE);
                    w.Clock.Advance(TimeSpan.FromDays(1));
                    w.AddCommunityFiles(@"C:\Games\Shared", Product.EE);
                    w.AddInstallInfo(@"C:\Games\Shared", Product.EE);
                    w.AddRecord(RegistryHive.LocalMachine, RegistryView.Registry64, Product.EE, @"C:\Games\Shared");
                },
                (w, r) =>
                {
                    Installation installation = Only(r);
                    Assert.That(installation.Product, Is.SameAs(Product.EE), "installed last");
                    Assert.That(installation.OtherProductInRoot, Is.SameAs(Product.NeoEE));
                    Assert.That(installation.HasUnreliableIntegrity, Is.True, "the flag \"unreliable\" of O11");
                    Assert.That(w.LogLinesAbout("are both installed in"), Has.Length.EqualTo(1));
                });

            // --- AoC folder --------------------------------------------------------------------------------------------
            const string aoc = "AoC folder";
            yield return new ContractCase(aoc, "EE-AOC.exe in <root>\\Empire Earth - The Art of Conquest",
                w =>
                {
                    w.AddCommunityFiles(EERoot, Product.EE);
                    w.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, Product.EE, EERoot, components: null);
                },
                (w, r) => Assert.That(Only(r).AocFolder, Is.EqualTo(EERoot + @"\Empire Earth - The Art of Conquest")));
            yield return new ContractCase(aoc, "gameaoc without EE-AOC.exe means damaged",
                w =>
                {
                    w.AddCommunityInstallation(NeoRoot, Product.NeoEE);
                    w.FileSystem.DeleteFile(NeoRoot + @"\Empire Earth - The Art of Conquest\EE-AOC.exe");
                },
                (w, r) =>
                {
                    Assert.That(Only(r).AocFolder, Is.Not.Null);
                    Assert.That(r.Selected.State, Is.EqualTo(InstallationState.Damaged));
                    Assert.That(r.Selected.MissingPrograms, Is.EqualTo(new[] { Game.ArtOfConquest }));
                });
            yield return new ContractCase(aoc, "neither gameaoc nor EE-AOC.exe means no AoC",
                w => w.AddCommunityInstallation(NeoRoot, Product.NeoEE, artOfConquest: false),
                (w, r) =>
                {
                    Assert.That(Only(r).AocFolder, Is.Null);
                    Assert.That(r.Selected.State, Is.EqualTo(InstallationState.Ok));
                });
            yield return new ContractCase(aoc, "foreign: the folder of the AoC Installed From values",
                w => w.AddForeignInstallation(@"C:\Games\EE", aocFolder: @"C:\Games\AoC"),
                (w, r) => Assert.That(Only(r).AocFolder, Is.EqualTo(@"C:\Games\AoC").IgnoreCase));
            yield return new ContractCase(aoc, "community: the AoC Installed From values are not used",
                w =>
                {
                    w.AddCommunityInstallation(NeoRoot, Product.NeoEE, artOfConquest: false);
                    w.AddArtOfConquest(@"C:\Elsewhere\AoC");
                    w.SetInstalledFrom(InstallationWorld.Hkcu(@"Software\Neo\Art of Conquest"), @"C:\Elsewhere\AoC");
                },
                (w, r) => Assert.That(Only(r).AocFolder, Is.Null));

            // --- Default selection -------------------------------------------------------------------------------------
            const string selection = "Default selection";
            yield return new ContractCase(selection, "without a user choice the first in the order of the sources",
                w =>
                {
                    // A retail installation registered for all users (HKLM32) and a community user installation.
                    w.AddForeignInstallation(@"C:\Retail\Empire Earth", RegistryHive.LocalMachine, RegistryView.Registry32);
                    w.AddCommunityInstallation(@"D:\Games\Empire Earth", Product.EE, "user");
                },
                (w, r) =>
                {
                    Assert.That(r.Installations, Has.Count.EqualTo(2));
                    Assert.That(r.Selected.Root, Is.EqualTo(@"D:\Games\Empire Earth"));
                    Assert.That(r.IsSelectedByUser, Is.False);
                });
            yield return new ContractCase(selection, "revision 6: the installation of the product chosen last, by the folder chosen for it",
                w =>
                {
                    w.AddCommunityInstallation(NeoRoot, Product.NeoEE);
                    w.AddCommunityInstallation(EERoot, Product.EE);
                    w.AddForeignInstallation(@"D:\GOG Games\Empire Earth Gold\Empire Earth");
                },
                (w, r) =>
                {
                    Assert.That(r.Installations, Has.Count.EqualTo(3));
                    Assert.That(r.ChosenFor(Product.EE).EeFolder, Is.EqualTo(@"D:\GOG Games\Empire Earth Gold\Empire Earth").IgnoreCase);
                    Assert.That(r.ChosenFor(Product.NeoEE).Root, Is.EqualTo(NeoRoot));
                    Assert.That(r.ForProduct(Product.EE).Selected.EeFolder, Is.EqualTo(@"D:\GOG Games\Empire Earth Gold\Empire Earth").IgnoreCase,
                        "not the first installation of EE in the order of the sources");
                    Assert.That(r.ForProduct(Product.EE).IsSelectedByUser, Is.True);
                },
                choices: new[]
                {
                    new UserChoice(NeoRoot, Product.NeoEE), new UserChoice(@"D:\GOG Games\Empire Earth Gold\Empire Earth", Product.EE)
                });
            yield return new ContractCase(selection, "revision 6: without a folder chosen for the product, its first installation",
                w =>
                {
                    w.AddCommunityInstallation(NeoRoot, Product.NeoEE);
                    w.AddCommunityInstallation(EERoot, Product.EE);
                    w.AddForeignInstallation(@"D:\GOG Games\Empire Earth Gold\Empire Earth");
                },
                (w, r) =>
                {
                    Assert.That(r.ChosenFor(Product.EE), Is.Null);
                    Assert.That(r.ForProduct(Product.EE).Selected.Root, Is.EqualTo(EERoot), "source 2 before source 4");
                    Assert.That(r.ForProduct(Product.EE).IsSelectedByUser, Is.False);
                },
                choices: new[] { new UserChoice(NeoRoot, Product.NeoEE) });
            yield return new ContractCase(selection, "revision 6: a folder chosen for a product that does not exist (any more) stays its choice",
                w => w.AddCommunityInstallation(EERoot, Product.EE),
                (w, r) =>
                {
                    Installation missing = r.ChosenFor(Product.NeoEE);
                    Assert.That(missing.State, Is.EqualTo(InstallationState.FolderMissing));
                    Assert.That(missing.Product, Is.SameAs(Product.NeoEE));
                    Assert.That(r.ForProduct(Product.NeoEE).Selected, Is.SameAs(missing));
                    Assert.That(r.Installations, Has.Count.EqualTo(2), "it is listed");
                },
                choices: new[] { new UserChoice(@"D:\Removed\Neo Empire Earth", Product.NeoEE) });
            yield return new ContractCase(selection, "revision 6: a chosen folder whose installation is now of the other product is no choice for either",
                w =>
                {
                    w.AddCommunityInstallation(NeoRoot, Product.NeoEE);
                    w.AddCommunityInstallation(EERoot, Product.EE);
                },
                (w, r) =>
                {
                    Assert.That(r.ChosenFor(Product.EE), Is.Null);
                    Assert.That(r.ChosenFor(Product.NeoEE), Is.Null);
                    Assert.That(r.Installations, Has.Count.EqualTo(2), "it stays listed");
                    Assert.That(r.ForProduct(Product.EE).Selected.Root, Is.EqualTo(EERoot));
                },
                choices: new[] { new UserChoice(NeoRoot, Product.EE) });
            yield return new ContractCase(selection, "every installation found is shown",
                w =>
                {
                    w.AddForeignInstallation(@"C:\Retail\Empire Earth", RegistryHive.LocalMachine, RegistryView.Registry32);
                    w.AddCommunityInstallation(NeoRoot, Product.NeoEE);
                    w.AddLegacyInstallation(EERoot, Product.EE);
                },
                (w, r) => Assert.That(r.Installations, Has.Count.EqualTo(3)));

            // --- Errors ------------------------------------------------------------------------------------------------
            const string errors = "Errors";
            yield return new ContractCase(errors, "denied access drops only that candidate, with one log line",
                w =>
                {
                    w.AddCommunityInstallation(NeoRoot, Product.NeoEE);
                    w.AddLegacyInstallation(EERoot, Product.EE);
                    w.Registry.SetFault(InstallationWorld.Hklm64(Product.NeoEE.InstallRecordKey), RegistryStatus.AccessDenied);
                },
                (w, r) =>
                {
                    Assert.That(r.Installations, Has.Count.EqualTo(2));
                    Assert.That(w.LogLinesAbout(@"HKLM64\Software\Empire Earth Community\Installations\NeoEE"), Has.Length.EqualTo(1));
                });
            yield return new ContractCase(errors, "an invalid path drops only that candidate, with one log line",
                w =>
                {
                    w.AddLegacyInstallation(EERoot, Product.EE);
                    w.Registry.Seed(InstallationWorld.Hklm64(Product.NeoEE.InstallRecordKey), ContractNames.InstallPathName,
                        RegistryValue.FromString(@"Neo Empire Earth"));
                },
                (w, r) =>
                {
                    Assert.That(Roots(r), Is.EqualTo(new[] { EERoot }));
                    Assert.That(w.LogLinesAbout(@"Installations\NeoEE"), Has.Length.EqualTo(1));
                });
            yield return new ContractCase(errors, "an unreadable install.ini is logged, the installation stays",
                w =>
                {
                    w.AddCommunityInstallation(NeoRoot, Product.NeoEE);
                    w.FileSystem.FailOn(InstallationWorld.InstallInfoPath(NeoRoot, Product.NeoEE), Fakes.FileSystemOperation.Read,
                        FileSystemStatus.AccessDenied);
                },
                (w, r) =>
                {
                    Assert.That(Only(r).InstallInfo, Is.Null);
                    Assert.That(r.Selected.Kind, Is.EqualTo(InstallationKind.Community), "the record has the contract version");
                    Assert.That(w.LogLinesAbout(ContractNames.InstallInfoFileName), Has.Length.EqualTo(1));
                });

            // --- Read-only ---------------------------------------------------------------------------------------------
            yield return new ContractCase("Read-only", "the discovery writes nothing",
                w =>
                {
                    w.AddCommunityInstallation(NeoRoot, Product.NeoEE);
                    w.AddLegacyInstallation(EERoot, Product.EE);
                    w.AddForeignInstallation(@"C:\Retail\Empire Earth", RegistryHive.LocalMachine, RegistryView.Registry32,
                        aocFolder: @"C:\Retail\AoC");
                },
                (w, r) =>
                {
                    // Every case runs with registry and file system that fail at a write; this one also checks the record.
                    Assert.That(w.Registry.Changes, Is.Empty);
                    Assert.That(r.Installations, Has.Count.EqualTo(3));
                });

            // --- 1.5 Installations of setups up to 1.7.2 -------------------------------------------------------------
            yield return new ContractCase("1.5 row 1", "uninstall key with Publisher: source 3 with AppId, versions, components and tasks",
                w => w.AddLegacyInstallation(NeoRoot, Product.NeoEE),
                (w, r) =>
                {
                    Installation installation = Only(r);
                    Assert.That(installation.AppId, Is.EqualTo(InstallationWorld.NeoEEAppId));
                    Assert.That(installation.GameVersion, Is.EqualTo("1.7.2"));
                    Assert.That(installation.SetupVersion, Is.EqualTo("1.7.2"));
                    Assert.That(installation.Components.HasArtOfConquest, Is.True);
                    Assert.That(installation.Tasks.Contains("compatibility"), Is.True);
                    Assert.That(installation.UninstallKey, Is.Not.Null);
                });
            yield return new ContractCase("1.5 row 2", "HKCU Installed From of the account that ran the setup: source 4",
                w => w.AddLegacyInstallation(EERoot, Product.EE),
                (w, r) =>
                {
                    Assert.That(Only(r).Sources, Does.Contain(InstallationSource.InstalledFrom));
                    Assert.That(r.Selected.InstalledFromKey.ToString(), Is.EqualTo(@"HKCU\Software\SSSI\Empire Earth"));
                });
            yield return new ContractCase("1.5 row 2", "another account (no HKCU values) still finds an admin installation",
                w =>
                {
                    w.AddCommunityFiles(EERoot, Product.EE);
                    w.AddUninstallKey(RegistryHive.LocalMachine, RegistryView.Registry64, Product.EE, EERoot);
                },
                (w, r) => Assert.That(Only(r).Sources, Is.EqualTo(new[] { InstallationSource.UninstallKey })));
            yield return new ContractCase("1.5 row 3", "<root>\\<AppId>\\ with EEStatsSetup.dll is not used",
                w =>
                {
                    w.AddLegacyInstallation(EERoot, Product.EE);
                    w.FileSystem.AddFile(EERoot + @"\{" + InstallationWorld.EEAppId + @"}\EEStatsSetup.dll", "dll");
                },
                (w, r) =>
                {
                    Assert.That(Only(r).Kind, Is.EqualTo(InstallationKind.CommunityLegacy));
                    Assert.That(r.Selected.EeFolder, Is.EqualTo(EERoot + @"\Empire Earth"));
                });
            yield return new ContractCase("1.5 row 4", "no record, install.ini, manifest or defaults marker",
                w => w.AddLegacyInstallation(EERoot, Product.EE, admin: false),
                (w, r) =>
                {
                    Installation installation = Only(r);
                    Assert.That(installation.RecordKey, Is.Null);
                    Assert.That(installation.InstallInfo, Is.Null);
                    Assert.That(installation.ContractVersion, Is.EqualTo(0));
                    Assert.That(installation.HasNewerContract, Is.False);
                });
        }

        public static IEnumerable<TestCaseData> Cases()
        {
            return AllCases().Select(contractCase => new TestCaseData(contractCase)
                .SetName("Contract_1_4(" + contractCase.ToString().Replace("\"", string.Empty) + ")"));
        }

        [TestCaseSource(nameof(Cases))]
        public void Contract_1_4(ContractCase contractCase)
        {
            var world = new InstallationWorld();
            contractCase.Arrange(world);

            DiscoveryResult result = contractCase.Choices != null
                ? world.CreateDiscovery().DiscoverChoices(contractCase.Choices, contractCase.LauncherFolder)
                : world.Discover(contractCase.UserChoice, contractCase.LauncherFolder);

            contractCase.Verify(world, result);
            Assert.That(world.Registry.Changes, Is.Empty);
        }

        /// <summary>The bold labels of the sources and rules of contract 1.4, read from the contract.</summary>
        private static IReadOnlyList<string> ContractRules()
        {
            string contract = File.ReadAllText(RepositoryRoot.GetFullPath("docs/CONTRACT.md"));
            int start = contract.IndexOf("### 1.4 Discovery by the launcher", StringComparison.Ordinal);
            int end = contract.IndexOf("### 1.5 ", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThan(0));
            Assert.That(end, Is.GreaterThan(start));
            string section = contract.Substring(start, end - start).Replace("\r\n", "\n");
            return Regex.Matches(section, @"^(?:\d\.|-) \*\*(?<label>[^*]+)\*\*", RegexOptions.Multiline)
                        .Cast<Match>()
                        .Select(match => match.Groups["label"].Value)
                        .ToList();
        }

        /// <summary>
        /// The number of rows of the table of contract 1.5. The section ends at the next heading (contract revision 4 added
        /// 1.6 and 1.7 with tables of their own before section 2).
        /// </summary>
        private static int ContractTable15Rows()
        {
            string contract = File.ReadAllText(RepositoryRoot.GetFullPath("docs/CONTRACT.md")).Replace("\r\n", "\n");
            int start = contract.IndexOf("### 1.5 ", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThan(0), "CONTRACT.md has no section 1.5");
            int end = contract.IndexOf("\n#", start + 1, StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start), "CONTRACT.md has no heading after section 1.5");
            return contract.Substring(start, end - start).Split('\n')
                           .Count(line => line.StartsWith("| ", StringComparison.Ordinal) &&
                                          !line.StartsWith("| What exists", StringComparison.Ordinal));
        }

        [Test]
        [Category(TestCategories.SourceTree)]
        public void EveryRuleOfTheContractHasACase()
        {
            IReadOnlyList<string> rules = ContractRules();
            Assert.That(rules, Is.EqualTo(new[]
            {
                "User choice", "Registry records", "Uninstall keys", "\"Installed From\" values", "Launcher folder",
                "Validity", "Merge", "Kind", "Two products in one root", "AoC folder", "Default selection", "Errors",
                "Read-only"
            }), "contract 1.4 changed: add cases for it");

            var covered = new HashSet<string>(AllCases().Select(contractCase => contractCase.Rule));
            Assert.That(rules.Where(rule => !covered.Contains(rule)), Is.Empty);

            int rows = ContractTable15Rows();
            Assert.That(rows, Is.EqualTo(4), "contract 1.5 changed: add cases for it");
            for (int row = 1; row <= rows; row++)
                Assert.That(covered, Does.Contain("1.5 row " + row));
        }
    }
}
