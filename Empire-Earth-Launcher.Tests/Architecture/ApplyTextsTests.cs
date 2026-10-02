using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Architecture
{
    /// <summary>
    /// Every text of the launcher's UI comes from <c>Properties/Resources*.resx</c> and is set in one
    /// <c>ApplyTexts</c> method per form or page (ADR 0009). The designer texts are placeholders: each control with a
    /// designer text is assigned again in <c>ApplyTexts</c>, so no designer text reaches the user untranslated.
    /// </summary>
    /// <remarks>
    /// The rules are checked on the sources. A designer text "needs translation" when it contains a letter or comes
    /// from a form resx (<c>resources.GetString</c>); texts without letters ("...", "?", "") stay in the designer.
    /// </remarks>
    [TestFixture]
    [Category(TestCategories.SourceTree)]
    public class ApplyTextsTests
    {
        private const string LauncherFolder = "Empire Earth Launcher";

        /// <summary>The forms and pages of the launcher; a new one needs its ApplyTexts as well.</summary>
        private static readonly string[] ExpectedForms =
        {
            "GeneralUserControl", "LauncherDialog", "LauncherSettingsUserControl", "MainForm", "SettingsUserControl",
        };

        /// <summary>
        /// A text property set in InitializeComponent: <c>this.control.Values.Text = "..."</c>, <c>.Values.Heading</c>,
        /// <c>.Values.Description</c>, <c>.Text</c>, <c>.HeaderText</c>, or <c>this.Text</c> of the form itself; the
        /// value is a literal or a text of the form's own resx.
        /// </summary>
        private static readonly Regex DesignerText = new Regex(
            @"^\s*this\.(?:(?<control>\w+)\.)?(?<property>(?:Values\.)?(?:Text|Heading|Description)|HeaderText)\s*=\s*" +
            @"(?<value>""(?:[^""\\]|\\.)*""|resources\.GetString\(""[^""]*""\))\s*;",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);

        /// <summary>Items of a list set in the designer (<c>this.control.Items.AddRange(new object[] { ... });</c>).</summary>
        private static readonly Regex DesignerItems = new Regex(
            @"this\.(?<control>\w+)\.Items\.AddRange\(new object\[\] \{(?<items>.*?)\}\);",
            RegexOptions.Singleline | RegexOptions.CultureInvariant);

        private static readonly Regex StringLiteral = new Regex(@"""(?:[^""\\]|\\.)*""", RegexOptions.CultureInvariant);
        private static readonly Regex Letter = new Regex(@"\p{L}", RegexOptions.CultureInvariant);

        /// <summary>A text property assigned a literal with a letter in hand-written code.</summary>
        private static readonly Regex LiteralTextInCode = new Regex(
            @"\.(?:Text|Heading|Description|HeaderText|Filter|Title)\s*=\s*""[^""]*\p{L}",
            RegexOptions.CultureInvariant);

        /// <summary>The assignment of the form's own Text (<c>Text = ...</c> or <c>this.Text = ...</c>).</summary>
        private static readonly Regex OwnTextAssignment = new Regex(@"(?<![\w.])(?:this\.)?Text\s*=",
            RegexOptions.CultureInvariant);

        private static string LauncherFile(string fileName)
        {
            return RepositoryRoot.GetFullPath(LauncherFolder + "/" + fileName);
        }

        public static IEnumerable<string> Forms()
        {
            return Directory.EnumerateFiles(RepositoryRoot.GetFullPath(LauncherFolder), "*.Designer.cs")
                            .Select(Path.GetFileName)
                            .Where(name => name != "Resources.Designer.cs" && name != "Settings.Designer.cs")
                            .Select(name => name.Substring(0, name.Length - ".Designer.cs".Length))
                            .OrderBy(name => name, StringComparer.Ordinal)
                            .ToList();
        }

        /// <summary>
        /// The body of the method <c>ApplyTexts</c> of a code-behind file (from the opening to the matching closing
        /// brace), or null if there is none.
        /// </summary>
        private static string ApplyTextsBody(string code)
        {
            Match declaration = Regex.Match(code, @"\bvoid\s+ApplyTexts\s*\([^)]*\)\s*\{");
            if (!declaration.Success)
                return null;
            int depth = 1;
            int start = declaration.Index + declaration.Length;
            for (int i = start; i < code.Length; i++)
            {
                if (code[i] == '{')
                    depth++;
                else if (code[i] == '}' && --depth == 0)
                    return code.Substring(start, i - start);
            }

            return null;
        }

        /// <summary>The controls of a designer file whose texts need translation ("this" for the form itself).</summary>
        private static IEnumerable<string> ControlsWithTexts(string designer)
        {
            foreach (Match match in DesignerText.Matches(designer))
            {
                string value = match.Groups["value"].Value;
                if (value.StartsWith("resources.", StringComparison.Ordinal) || Letter.IsMatch(value))
                    yield return match.Groups["control"].Success ? match.Groups["control"].Value : "this";
            }

            foreach (Match match in DesignerItems.Matches(designer))
            {
                if (StringLiteral.Matches(match.Groups["items"].Value).Cast<Match>().Any(item => Letter.IsMatch(item.Value)))
                    yield return match.Groups["control"].Value;
            }
        }

        [Test]
        public void EveryFormOfTheLauncherIsChecked()
        {
            Assert.That(Forms(), Is.EqualTo(ExpectedForms));
        }

        [TestCaseSource(nameof(Forms))]
        public void ApplyTexts_IsCalledAfterInitializeComponent(string form)
        {
            string code = File.ReadAllText(LauncherFile(form + ".cs"));

            Assert.That(ApplyTextsBody(code), Is.Not.Null, form + ".cs needs an ApplyTexts method (ADR 0009)");
            int initialize = code.IndexOf("InitializeComponent();", StringComparison.Ordinal);
            int call = Regex.Match(code, @"\bApplyTexts\s*\([^)]*\)\s*;").Index;
            Assert.That(initialize, Is.GreaterThan(0));
            Assert.That(call, Is.GreaterThan(initialize), "ApplyTexts must run after InitializeComponent in " + form);
        }

        [TestCaseSource(nameof(Forms))]
        public void EveryDesignerText_IsSetAgainInApplyTexts(string form)
        {
            string designer = File.ReadAllText(LauncherFile(form + ".Designer.cs"));
            string body = ApplyTextsBody(File.ReadAllText(LauncherFile(form + ".cs"))) ?? string.Empty;

            var missing = ControlsWithTexts(designer).Distinct()
                .Where(control => control == "this"
                    ? !OwnTextAssignment.IsMatch(body)
                    : !Regex.IsMatch(body, @"\b" + Regex.Escape(control) + @"\."))
                .ToList();

            Assert.That(missing, Is.Empty,
                "these controls of " + form + ".Designer.cs have a designer text that ApplyTexts does not replace");
        }

        [Test]
        public void TheCheckFindsTheDesignerTexts()
        {
            // Guards the regular expressions above: the designer files hold texts of these kinds.
            string general = File.ReadAllText(LauncherFile("GeneralUserControl.Designer.cs"));
            string launcher = File.ReadAllText(LauncherFile("LauncherSettingsUserControl.Designer.cs"));
            string mainForm = File.ReadAllText(LauncherFile("MainForm.Designer.cs"));
            string dialog = File.ReadAllText(LauncherFile("LauncherDialog.Designer.cs"));

            Assert.That(ControlsWithTexts(general), Does.Contain("playKryptonButton").And.Contain("usernameColumn")
                                                        .And.Contain("gameSettingsKryptonGroupBox"));
            Assert.That(ControlsWithTexts(launcher), Does.Contain("themeKryptonComboBox")
                                                         .And.Not.Contain("browseGameDirectoryKryptonButton"));
            Assert.That(ControlsWithTexts(mainForm), Does.Contain("playKryptonCheckButton"));
            Assert.That(ControlsWithTexts(dialog), Does.Contain("this").And.Contain("okKryptonButton"));
        }

        [Test]
        public void HandWrittenCode_AssignsNoLiteralText()
        {
            var offenders = new List<string>();
            foreach (string file in Directory.EnumerateFiles(RepositoryRoot.GetFullPath(LauncherFolder), "*.cs",
                         SearchOption.AllDirectories))
            {
                string relative = RepositoryRoot.ToRelativePath(file);
                if (relative.EndsWith(".Designer.cs", StringComparison.Ordinal) ||
                    relative.Split('/').Any(folder => folder == "bin" || folder == "obj"))
                    continue;
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (LiteralTextInCode.IsMatch(lines[i]) && !lines[i].TrimStart().StartsWith("//", StringComparison.Ordinal))
                        offenders.Add(relative + ":" + (i + 1) + ": " + lines[i].Trim());
                }
            }

            Assert.That(offenders, Is.Empty, "UI texts come from Properties/Resources.resx (ADR 0009)");
        }

        [Test]
        public void OnlyTheResourcesHaveTranslations()
        {
            // Texts of forms and pages are in Properties/Resources*.resx; a culture-specific form resx
            // (MainForm.fr.resx until v2) would be a second place for a text.
            var culturalResx = Directory.EnumerateFiles(RepositoryRoot.GetFullPath(LauncherFolder), "*.resx",
                                                        SearchOption.AllDirectories)
                                        .Select(RepositoryRoot.ToRelativePath)
                                        .Where(path => !path.Split('/').Any(folder => folder == "bin" || folder == "obj"))
                                        .Where(path => Regex.IsMatch(Path.GetFileName(path), @"\.[a-z]{2}(-[A-Za-z]+)?\.resx$"))
                                        .Where(path => !path.StartsWith(LauncherFolder + "/Properties/Resources.",
                                                                        StringComparison.Ordinal))
                                        .ToList();

            Assert.That(culturalResx, Is.Empty);
        }
    }
}
