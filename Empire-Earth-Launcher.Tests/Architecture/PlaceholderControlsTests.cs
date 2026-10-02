using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Architecture
{
    /// <summary>
    /// The UI shows only controls that work (ADR 0014). The placeholder controls of the old designer, listed exactly
    /// in the amendment of ADR 0014, were removed with their fields, designer code, handlers and resources and must
    /// not come back: a feature that is implemented later gets a control with a new name in the same work package.
    /// </summary>
    [TestFixture]
    public class PlaceholderControlsTests
    {
        private const string LauncherFolder = "Empire Earth Launcher";
        private const string Adr0014 = "docs/adr/0014-only-working-features-in-the-ui.md";

        /// <summary>The removed controls, by the designer file (form or page) they were in (ADR 0014, amendment).</summary>
        private static readonly IDictionary<string, string[]> RemovedControlsByForm = new Dictionary<string, string[]>
        {
            {
                "LauncherSettingsUserControl", new[]
                {
                    // "Allow us to collect diagnostic data ...": contradicts "no telemetry" (ARCHITECTURE 10).
                    "diagnosticDataKryptonCheckBox", "associateModFilesKryptonCheckBox", "gameStartKryptonLabel",
                    "gameStartKryptonComboBox", "gameCloseKryptonLabel", "gameCloseKryptonComboBox",
                }
            },
            {
                "SettingsUserControl", new[]
                {
                    "magicButtonsKryptonGroupBox", "repairCdKeysKryptonButton", "resetGameKryptonButton",
                    "clearRegistryKryptonButton", "compatibilityKryptonGroupBox", "windowsCompatibilityKryptonGroupBox",
                    "compatibilityModeKryptonCheckBox", "compatibilityModeKryptonComboBox",
                    "clearHeapAllocationKryptonCheckBox", "bitDepthMitigationKryptonCheckBox", "gameWorkingKryptonButton",
                    "autoDetectCompatibilityKryptonButton", "directXKryptonGroupBox", "directXKryptonLabel",
                    "directXKryptonComboBox", "directXWrapperKryptonCheckBox", "dgVoodooSettingsKryptonButton",
                    "resolutionKryptonLabel", "resolutionKryptonComboBox", "monitorKryptonLabel", "monitorKryptonComboBox",
                    "gameFontKryptonLabel", "gameFontKryptonTextBox", "browseGameFontKryptonButton",
                    "advancedSettingsKryptonGroupBox", "dreXmodKryptonCheckBox", "dreXmodVersionKryptonComboBox",
                    "neoEEKryptonCheckBox", "discordPresenceKryptonCheckBox", "hdTexturesKryptonCheckBox",
                    "skipIntroKryptonCheckBox",
                }
            },
            {
                "GeneralUserControl", new[]
                {
                    "languageKryptonGroupBox", "languageKryptonLabel", "languageKryptonComboBox",
                    "fallbackLanguageKryptonLabel", "fallbackLanguageKryptonComboBox", "gameTextKryptonCheckBox",
                    "voicesKryptonCheckBox", "lobbyKryptonCheckBox", "onlineRankingKryptonGroupBox",
                    "rankingUserKryptonLabel", "rankingPointsKryptonLabel", "rankingRankKryptonLabel",
                    "modsInUseKryptonLabel",
                }
            },
            { "MainForm", new[] { "modsKryptonCheckButton" } },
        };

        /// <summary>Controls of the old designer that work and therefore stay (ADR 0014, amendment).</summary>
        private static readonly string[] KeptControls =
        {
            "compatibilityWarningKryptonPanel", "compatibilityWarningKryptonWrapLabel",
            "compatibilityWarningConfirmationKryptonButton", "compatibilityWarningPictureBox",
        };

        /// <summary>A control name in backticks, as ADR 0014 writes them.</summary>
        private static readonly Regex QuotedControlName = new Regex(@"`(?<name>[a-z][A-Za-z0-9]*Krypton[A-Za-z]+)`",
            RegexOptions.CultureInvariant);

        private static IEnumerable<string> RemovedControlNames
        {
            get { return RemovedControlsByForm.Values.SelectMany(names => names); }
        }

        private static Regex RemovedNamePattern
        {
            get
            {
                return new Regex(@"\b(" + string.Join("|", RemovedControlNames.Select(Regex.Escape)) + @")\b",
                    RegexOptions.CultureInvariant);
            }
        }

        /// <summary>Source files of the launcher of one kind, without build output.</summary>
        private static IEnumerable<string> LauncherSources(string searchPattern)
        {
            return Directory.EnumerateFiles(RepositoryRoot.GetFullPath(LauncherFolder), searchPattern,
                                            SearchOption.AllDirectories)
                            .Where(file => !RepositoryRoot.ToRelativePath(file).Split('/')
                                                          .Any(folder => folder == "bin" || folder == "obj"));
        }

        /// <summary>"file:line: text" for every line of the files that names a removed control.</summary>
        private static List<string> LinesNamingARemovedControl(IEnumerable<string> files)
        {
            Regex pattern = RemovedNamePattern;
            var offenders = new List<string>();
            foreach (string file in files)
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (pattern.IsMatch(lines[i]))
                        offenders.Add(RepositoryRoot.ToRelativePath(file) + ":" + (i + 1) + ": " + lines[i].Trim());
                }
            }

            return offenders;
        }

        [Test]
        [Category(TestCategories.SourceTree)]
        public void TheListIsTheOneOfAdr0014()
        {
            string adr = File.ReadAllText(RepositoryRoot.GetFullPath(Adr0014));
            int start = adr.IndexOf("## Amendment", StringComparison.Ordinal);
            int end = adr.IndexOf("Order:", start, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThan(0), "the amendment of ADR 0014 holds the list");
            Assert.That(end, Is.GreaterThan(start), "the list of ADR 0014 ends before \"Order:\"");

            var listed = QuotedControlName.Matches(adr.Substring(start, end - start)).Cast<Match>()
                                          .Select(m => m.Groups["name"].Value).Distinct().ToList();

            Assert.That(RemovedControlNames, Is.EquivalentTo(listed));
        }

        [Test]
        [Category(TestCategories.SourceTree)]
        public void DesignerFiles_NameNoRemovedControl()
        {
            List<string> designerFiles = LauncherSources("*.Designer.cs").ToList();
            Assert.That(designerFiles.Select(Path.GetFileName),
                Is.SupersetOf(RemovedControlsByForm.Keys.Select(form => form + ".Designer.cs")));

            Assert.That(LinesNamingARemovedControl(designerFiles), Is.Empty,
                "placeholder controls of ADR 0014 must not come back; a working replacement gets a new name");
        }

        [Test]
        [Category(TestCategories.SourceTree)]
        public void CodeAndResources_NameNoRemovedControl()
        {
            // Handlers and resources went with the controls.
            var files = LauncherSources("*.cs").Where(f => !f.EndsWith(".Designer.cs", StringComparison.Ordinal))
                                               .Concat(LauncherSources("*.resx"));

            Assert.That(LinesNamingARemovedControl(files), Is.Empty);
        }

        [Test]
        public void LauncherAssembly_HasNoFieldOfARemovedControl()
        {
            const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                                        BindingFlags.DeclaredOnly;
            var removed = new HashSet<string>(RemovedControlNames, StringComparer.Ordinal);

            var offenders = typeof(global::Empire_Earth_Launcher.MainForm).Assembly.GetTypes()
                .SelectMany(type => type.GetFields(Fields).Select(field => type.Name + "." + field.Name))
                .Where(field => removed.Contains(field.Substring(field.IndexOf('.') + 1)))
                .ToList();

            Assert.That(offenders, Is.Empty);
        }

        [Test]
        [Category(TestCategories.SourceTree)]
        public void CompatibilityWarningPanel_IsKept()
        {
            string designer = File.ReadAllText(RepositoryRoot.GetFullPath(LauncherFolder + "/SettingsUserControl.Designer.cs"));
            Type page = typeof(global::Empire_Earth_Launcher.SettingsUserControl);

            foreach (string name in KeptControls)
            {
                Assert.That(designer, Does.Contain("this." + name + " = new "), name);
                Assert.That(page.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic), Is.Not.Null, name);
            }
        }
    }
}
