using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Architecture
{
    /// <summary>
    /// The launcher's texts in English (neutral), German and French have the same keys, no empty value and the same
    /// <c>{n}</c> placeholders (ADR 0009, amendment), in the source files and in the built satellite assemblies.
    /// </summary>
    /// <remarks>
    /// Only string entries count: entries with a <c>type</c> or <c>mimetype</c> (images, icons, files) are not
    /// texts, and the comments for translators are not compared. Those entries belong to the neutral file only, the
    /// satellites inherit them. How to add a text or a language is described in <c>docs/TRANSLATING.md</c>.
    /// </remarks>
    [TestFixture]
    [Category(TestCategories.SourceTree)]
    public class ResourceParityTests
    {
        private const string PropertiesFolder = "Empire Earth Launcher/Properties";
        private const string NeutralFile = PropertiesFolder + "/Resources.resx";
        private const string LauncherProject = @"Empire Earth Launcher\Empire Earth Launcher.csproj";
        private const string BaseName = "Empire_Earth_Launcher.Properties.Resources";

        /// <summary>The translations of the launcher. A new language needs an entry here (docs/TRANSLATING.md).</summary>
        private static readonly string[] Cultures = { "de", "fr" };

        /// <summary>A composite format item: <c>{0}</c>, <c>{1,5}</c>, <c>{0:N0}</c>.</summary>
        private static readonly Regex FormatItem = new Regex(@"\{(?<index>\d+)(?:,[^}:]*)?(?::[^}]*)?\}",
            RegexOptions.CultureInvariant);

        /// <summary>The entries of one resx file.</summary>
        private sealed class ResxFile
        {
            /// <summary>String entries in file order: name -> value.</summary>
            public readonly List<KeyValuePair<string, string>> Strings = new List<KeyValuePair<string, string>>();

            /// <summary>Names of the entries with a type or mimetype (images, icons, files).</summary>
            public readonly List<string> NonStrings = new List<string>();

            /// <summary>Values of the file references (<c>ResXFileRef</c>): "relative path;type".</summary>
            public readonly List<string> FileReferences = new List<string>();

            public IDictionary<string, string> StringMap
            {
                get { return Strings.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal); }
            }

            public static ResxFile Load(string relativePath)
            {
                var file = new ResxFile();
                XDocument xml = XDocument.Load(RepositoryRoot.GetFullPath(relativePath), LoadOptions.PreserveWhitespace);
                foreach (XElement data in xml.Root.Elements("data"))
                {
                    string name = (string)data.Attribute("name");
                    string type = (string)data.Attribute("type");
                    string value = (string)data.Element("value") ?? string.Empty;
                    if (type == null && data.Attribute("mimetype") == null)
                    {
                        file.Strings.Add(new KeyValuePair<string, string>(name, value));
                        continue;
                    }

                    file.NonStrings.Add(name);
                    if (type != null && type.StartsWith("System.Resources.ResXFileRef", StringComparison.Ordinal))
                        file.FileReferences.Add(value);
                }

                return file;
            }
        }

        public static IEnumerable<string> TranslatedCultures()
        {
            return Cultures;
        }

        public static IEnumerable<string> AllFiles()
        {
            return new[] { NeutralFile }.Concat(Cultures.Select(TranslationFile));
        }

        private static string TranslationFile(string culture)
        {
            return PropertiesFolder + "/Resources." + culture + ".resx";
        }

        private static IEnumerable<int> PlaceholderIndices(string value)
        {
            return FormatItem.Matches(value).Cast<Match>().Select(m => int.Parse(m.Groups["index"].Value,
                CultureInfo.InvariantCulture)).Distinct().OrderBy(i => i);
        }

        [Test]
        public void TheResourceFilesAreTheNeutralOneAndTheListedTranslations()
        {
            var cultures = Directory.EnumerateFiles(RepositoryRoot.GetFullPath(PropertiesFolder), "Resources.*.resx")
                                    .Select(Path.GetFileName)
                                    .Select(name => name.Substring("Resources.".Length,
                                                                   name.Length - "Resources.".Length - ".resx".Length));

            Assert.That(cultures, Is.EquivalentTo(Cultures),
                "every translation must be listed in Cultures, so that this test compares it (docs/TRANSLATING.md)");
        }

        [Test]
        public void TheNeutralFileHasTheTextsOfTheLauncher()
        {
            ResxFile neutral = ResxFile.Load(NeutralFile);

            // Guards the parser: these keys exist since L-WP3, and the images are no strings.
            Assert.That(neutral.Strings.Select(e => e.Key), Does.Contain("LauncherTitle").And.Contain("NavigationPlay"));
            Assert.That(neutral.Strings.Select(e => e.Key), Does.Not.Contain("ee_book"));
            Assert.That(neutral.NonStrings, Does.Contain("ee_book"));
            Assert.That(neutral.StringMap["GameExecutableMissingFormat"], Does.Contain("{1}"));
        }

        [TestCaseSource(nameof(TranslatedCultures))]
        public void Translation_HasExactlyTheStringKeysOfTheNeutralFile(string culture)
        {
            var neutral = ResxFile.Load(NeutralFile).Strings.Select(e => e.Key).ToList();
            var translated = ResxFile.Load(TranslationFile(culture)).Strings.Select(e => e.Key).ToList();

            Assert.That(neutral.Except(translated), Is.Empty, "keys missing in Resources." + culture + ".resx");
            Assert.That(translated.Except(neutral), Is.Empty, "keys of Resources." + culture + ".resx that the neutral file does not have");
        }

        [TestCaseSource(nameof(AllFiles))]
        public void NoKeyTwice_AndNoEmptyText(string file)
        {
            ResxFile resx = ResxFile.Load(file);

            var duplicates = resx.Strings.Select(e => e.Key).Concat(resx.NonStrings)
                                 .GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key);
            var empty = resx.Strings.Where(e => string.IsNullOrWhiteSpace(e.Value)).Select(e => e.Key);

            Assert.That(duplicates, Is.Empty, "keys that appear twice in " + file);
            Assert.That(empty, Is.Empty, "empty texts in " + file);
        }

        [TestCaseSource(nameof(TranslatedCultures))]
        public void Translation_KeepsThePlaceholders(string culture)
        {
            IDictionary<string, string> neutral = ResxFile.Load(NeutralFile).StringMap;
            IDictionary<string, string> translated = ResxFile.Load(TranslationFile(culture)).StringMap;
            var problems = new List<string>();

            foreach (KeyValuePair<string, string> entry in translated.Where(e => neutral.ContainsKey(e.Key)))
            {
                var expected = PlaceholderIndices(neutral[entry.Key]).ToList();
                var actual = PlaceholderIndices(entry.Value).ToList();
                if (!expected.SequenceEqual(actual))
                {
                    problems.Add(entry.Key + ": {" + string.Join("},{", expected) + "} expected, {" +
                                 string.Join("},{", actual) + "} found");
                    continue;
                }

                if (expected.Count == 0 && entry.Value.IndexOfAny(new[] { '{', '}' }) < 0)
                    continue;
                try
                {
                    // The launcher formats these texts with string.Format; a stray brace would throw there.
                    string.Format(CultureInfo.InvariantCulture, entry.Value,
                        Enumerable.Range(0, expected.Count == 0 ? 0 : expected.Max() + 1).Cast<object>().ToArray());
                }
                catch (FormatException)
                {
                    problems.Add(entry.Key + ": not a valid format string");
                }
            }

            Assert.That(problems, Is.Empty, "placeholders of Resources." + culture + ".resx");
        }

        [TestCaseSource(nameof(TranslatedCultures))]
        public void ImagesAndFiles_AreOnlyInTheNeutralFile(string culture)
        {
            Assert.That(ResxFile.Load(TranslationFile(culture)).NonStrings, Is.Empty,
                "images, icons and file references belong to Resources.resx; the satellites inherit them");
        }

        [Test]
        public void FileReferencesOfTheNeutralFile_Exist()
        {
            var missing = ResxFile.Load(NeutralFile).FileReferences
                                  .Select(reference => reference.Split(';')[0])
                                  .Where(path => !File.Exists(RepositoryRoot.CombineRelative(
                                      RepositoryRoot.GetFullPath(PropertiesFolder), path)))
                                  .ToList();

            Assert.That(missing, Is.Empty);
        }

        [Test]
        public void GeneratedResourcesClass_HasAPropertyForEveryText()
        {
            // Resources.Designer.cs is generated from Resources.resx; it must be regenerated after a text is added.
            var properties = typeof(global::Empire_Earth_Launcher.Properties.Resources)
                             .GetProperties(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
                             .Where(p => p.PropertyType == typeof(string))
                             .Select(p => p.Name);

            Assert.That(properties, Is.EquivalentTo(ResxFile.Load(NeutralFile).Strings.Select(e => e.Key)));
        }

        [Test]
        public void LauncherProject_EmbedsEveryTranslation()
        {
            var embedded = ProjectFile.Load(LauncherProject).Items("EmbeddedResource");

            Assert.That(embedded, Is.SupersetOf(Cultures.Select(c => @"Properties\Resources." + c + ".resx")));
        }

        [TestCaseSource(nameof(TranslatedCultures))]
        public void BuiltSatelliteAssembly_HoldsEveryTextOfTheSourceFile(string culture)
        {
            // The launcher of the same configuration as this test program (bin\Debug or bin\Release).
            string configuration = Path.GetFileName(Path.GetDirectoryName(typeof(ResourceParityTests).Assembly.Location));
            string satellite = RepositoryRoot.GetFullPath("Empire Earth Launcher/bin/" + configuration + "/" + culture +
                                                          "/Empire Earth Launcher.resources.dll");
            Assert.That(File.Exists(satellite), Is.True, satellite + " was not built");

            Assembly assembly = Assembly.Load(File.ReadAllBytes(satellite));
            var built = new Dictionary<string, string>(StringComparer.Ordinal);
            using (Stream stream = assembly.GetManifestResourceStream(BaseName + "." + culture + ".resources"))
            {
                Assert.That(stream, Is.Not.Null, "no " + BaseName + "." + culture + ".resources in " + satellite);
                using (var reader = new ResourceReader(stream))
                {
                    // The resource generators keep the CRLF line breaks of the resx file in a text; the XML parser of
                    // this test turns them into LF, so the line breaks are compared as LF.
                    foreach (DictionaryEntry entry in reader)
                        built.Add((string)entry.Key, (entry.Value as string)?.Replace("\r\n", "\n"));
                }
            }

            Assert.That(assembly.GetName().CultureInfo.Name, Is.EqualTo(culture));
            Assert.That(built, Is.EquivalentTo(ResxFile.Load(TranslationFile(culture)).StringMap),
                "the satellite assembly differs from Resources." + culture + ".resx: build the solution again");
        }
    }
}
