using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Integrity;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.RealMachineTests.Json;

namespace Empire_Earth_Launcher.RealMachineTests.Expectations
{
    /// <summary>
    /// Reads an expectation file (UTF-8, with or without BOM, JSON of <see cref="JsonReader"/>) into an
    /// <see cref="Expectation"/>. Strict: an unknown member, a wrong type or an unknown name of an enumeration is an error, and
    /// every error of the file is reported at once.
    /// </summary>
    /// <remarks>
    /// Names of enumerations are the names of the core (<c>Community</c>, <c>RegistryRecord</c>, <c>HashDiffers</c>, ...),
    /// ignoring case; numbers are not accepted. Products are <c>EE</c> and <c>NeoEE</c>, games <c>EE</c> and <c>AoC</c>.
    /// README (Tests, "Real machine") shows a complete file.
    /// </remarks>
    internal static class ExpectationFile
    {
        /// <summary>Larger files are refused: an expectation has a few kilobytes.</summary>
        public const long MaxFileBytes = 1024 * 1024;

        /// <summary>The names <see cref="DefaultsExpectation.AllowedDifferences"/> accepts besides the settings of the table.</summary>
        public const string MarkerName = "Marker";

        /// <inheritdoc cref="MarkerName"/>
        public const string GpuPreferenceName = "GpuPreference";

        /// <summary>Reads the file <paramref name="path"/>.</summary>
        /// <exception cref="FormatException">The file cannot be read or is not a valid expectation.</exception>
        public static Expectation Load(IFileSystem fileSystem, string path)
        {
            if (fileSystem == null)
                throw new ArgumentNullException(nameof(fileSystem));
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            FileSystemResult<byte[]> content = fileSystem.ReadAllBytes(path, MaxFileBytes);
            if (!content.IsOk)
                throw new FormatException("The expectation file " + path + " cannot be read: " + content + ".");
            try
            {
                return Parse(content.Value);
            }
            catch (FormatException ex)
            {
                throw new FormatException("The expectation file " + path + " is not valid: " + ex.Message, ex);
            }
        }

        /// <summary>The expectation in <paramref name="content"/> (UTF-8, a BOM is skipped).</summary>
        /// <exception cref="FormatException">Not valid UTF-8, not JSON or not a valid expectation.</exception>
        public static Expectation Parse(byte[] content)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));
            string text;
            try
            {
                text = new UTF8Encoding(false, true).GetString(content);
            }
            catch (DecoderFallbackException)
            {
                throw new FormatException("The file is not UTF-8.");
            }
            if (text.Length > 0 && text[0] == '\uFEFF')
                text = text.Substring(1);
            return Parse(text);
        }

        /// <summary>The expectation in <paramref name="json"/>.</summary>
        /// <exception cref="FormatException">Not JSON or not a valid expectation; the message lists every problem.</exception>
        public static Expectation Parse(string json)
        {
            if (json == null)
                throw new ArgumentNullException(nameof(json));
            var problems = new List<string>();
            Expectation expectation = Read(new Members(JsonReader.Parse(json), null, problems));
            if (problems.Count > 0)
                throw new FormatException(string.Join(" ", problems));
            return expectation;
        }

        private static Expectation Read(Members file)
        {
            var expectation = new Expectation();
            long? schema = file.Integer("schema", true);
            if (schema != null && schema != Expectation.SchemaVersion)
                file.Problem("schema", "is " + schema.Value.ToString(CultureInfo.InvariantCulture) + "; this harness reads schema " +
                                       Expectation.SchemaVersion.ToString(CultureInfo.InvariantCulture));
            expectation.Scenario = file.String("scenario");
            expectation.Step = file.String("step");
            expectation.UserChoice = file.String("userChoice");
            expectation.ExactInstallations = file.Boolean("exactInstallations") ?? true;
            expectation.SelectedRoot = file.Path("selectedRoot");
            expectation.WatchRoots = file.Array("watchRoots", (item, where) => file.PathItem(item, where)) ?? new string[0];
            expectation.Installations = file.Array("installations", (value, at) => ReadInstallation(file.Child(value, at)), true);
            Members defaults = file.Object("defaults");
            if (defaults != null)
                expectation.Defaults = ReadDefaults(defaults);
            file.CheckUnknown();

            foreach (IGrouping<string, InstallationExpectation> twice in expectation.Installations
                         .Where(installation => installation.Root != null).GroupBy(installation => installation.Root, WinPath.Comparer)
                         .Where(group => group.Count() > 1))
                file.Problem("installations", "names the root " + twice.Key + " more than once");
            for (int i = 0; i < expectation.Installations.Count && expectation.Defaults == null; i++)
            {
                InstallationExpectation installation = expectation.Installations[i];
                if (installation.DefaultsAtStart != null || installation.InstalledFromAtStart != null)
                    file.ProblemAt("installations[" + i.ToString(CultureInfo.InvariantCulture) + "]",
                        "has defaultsAtStart or installedFromAtStart, which need \"defaults\"");
            }
            return expectation;
        }

        private static InstallationExpectation ReadInstallation(Members item)
        {
            var installation = new InstallationExpectation
            {
                Product = item.Product("product", true),
                Root = item.Path("root", true),
                Kind = item.Enum<InstallationKind>("kind"),
                Mode = item.Enum<InstallMode>("mode"),
                AppId = item.String("appId"),
                ContractVersion = (int?)item.Integer("contractVersion"),
                HasArtOfConquest = item.Boolean("hasArtOfConquest"),
                State = item.Enum<InstallationState>("state"),
                MissingPrograms = item.Array("missingPrograms", item.GameItem),
                Sources = item.EnumArray("sources", item.EnumItem<InstallationSource>),
                SourcesInclude = item.EnumArray("sourcesInclude", item.EnumItem<InstallationSource>),
                ChecksOtherProductInRoot = item.Has("otherProductInRoot"),
                OtherProductInRoot = item.Product("otherProductInRoot"),
                GameVersion = item.String("gameVersion"),
                SetupVersion = item.String("setupVersion"),
                DefaultsStatus = item.PerGame<DefaultsStatus>("defaultsStatus"),
                DefaultsAtStart = item.PerGame<DefaultsAtStart>("defaultsAtStart"),
                InstalledFromAtStart = item.PerGame<InstalledFromAtStart>("installedFromAtStart")
            };
            if (installation.AppId != null && installation.AppId.IndexOfAny(new[] { '{', '}' }) >= 0)
                item.Problem("appId", "is written without braces");

            Members integrity = item.Object("integrity");
            if (integrity != null)
            {
                Members quick = integrity.Object("quick");
                Members full = integrity.Object("full");
                installation.Quick = quick == null ? null : ReadCheck(quick);
                installation.Full = full == null ? null : ReadCheck(full);
                integrity.CheckUnknown();
            }

            Members consistency = item.Object("consistency");
            if (consistency != null)
            {
                installation.Consistency = new ConsistencyExpectation
                {
                    Expected = consistency.EnumArray("expected", consistency.EnumItem<FindingCode>) ?? new FindingCode[0],
                    Allowed = consistency.EnumArray("allowed", consistency.EnumItem<FindingCode>) ?? new FindingCode[0]
                };
                consistency.CheckUnknown();
            }
            item.CheckUnknown();
            return installation;
        }

        private static CheckExpectation ReadCheck(Members check)
        {
            var expectation = new CheckExpectation
            {
                State = check.Enum<IntegrityState>("state", true) ?? IntegrityState.NotChecked,
                UnknownReason = check.Enum<UnknownReason>("unknownReason"),
                CancelReason = check.Enum<CancelReason>("cancelReason"),
                Findings = check.Array("findings", (value, where) =>
                {
                    Members finding = check.Child(value, where);
                    string path = finding.String("path", true);
                    FindingKind? kind = finding.Enum<FindingKind>("kind", true);
                    finding.CheckUnknown();
                    return path == null || kind == null ? null : new FindingExpectation(path, kind.Value);
                }),
                OffersRepair = check.Boolean("offersRepair")
            };
            check.CheckUnknown();
            return expectation;
        }

        private static DefaultsExpectation ReadDefaults(Members defaults)
        {
            var expectation = new DefaultsExpectation
            {
                RecordSetupValuesTo = defaults.Path("recordSetupValuesTo"),
                ExpectNoWrites = defaults.Boolean("expectNoWrites") ?? false,
                ExpectRecommendedValues = defaults.Boolean("expectRecommendedValues") ?? false,
                CompareWithSetupValuesFrom = defaults.Path("compareWithSetupValuesFrom"),
                AllowedDifferences = defaults.Array("allowedDifferences", (value, where) =>
                {
                    string name = defaults.StringItem(value, where);
                    if (name != null && !IsComparedName(name))
                        defaults.ProblemAt(where, "is \"" + name + "\", neither a setting of contract 3.2 nor " + MarkerName +
                                                  " or " + GpuPreferenceName);
                    return name;
                }) ?? new string[0],
                SecondStartChangesNothing = defaults.Boolean("secondStartChangesNothing") ?? true,
                Reset = defaults.Boolean("reset") ?? false
            };
            if (expectation.ExpectNoWrites && (expectation.ExpectRecommendedValues || expectation.Reset))
                defaults.Problem("expectNoWrites", "contradicts expectRecommendedValues and reset, which need writes");
            if (expectation.AllowedDifferences.Count > 0 && expectation.CompareWithSetupValuesFrom == null)
                defaults.Problem("allowedDifferences", "needs compareWithSetupValuesFrom");
            defaults.CheckUnknown();
            return expectation;
        }

        /// <summary>True if <paramref name="name"/> is a setting of the table, the marker or the GPU preference.</summary>
        public static bool IsComparedName(string name)
        {
            return GameSettingsTable.Find(name) != null || string.Equals(name, MarkerName, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, GpuPreferenceName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The members of one JSON object, read once each; what was not read is an unknown member.</summary>
        private sealed class Members
        {
            private readonly Dictionary<string, object> values;
            private readonly string where;
            private readonly List<string> problems;
            private readonly HashSet<string> read = new HashSet<string>(StringComparer.Ordinal);

            /// <param name="value">The JSON value; not an object is a problem.</param>
            /// <param name="where">How the problems name the object, e.g. <c>installations[0]</c>; null for the file itself.</param>
            /// <param name="problems">The problems of the whole file.</param>
            public Members(object value, string where, List<string> problems)
            {
                this.where = where;
                this.problems = problems;
                values = value as Dictionary<string, object>;
                if (values == null)
                {
                    ProblemAt(where ?? "the file", "must be an object");
                    values = new Dictionary<string, object>();
                }
            }

            /// <summary>The members of <paramref name="value"/>, an item or member of this object, with the same list of problems.</summary>
            public Members Child(object value, string at)
            {
                return new Members(value, at, problems);
            }

            public bool Has(string name)
            {
                read.Add(name);
                return values.ContainsKey(name);
            }

            /// <summary>A problem of the member <paramref name="member"/> of this object.</summary>
            public void Problem(string member, string message)
            {
                ProblemAt(Name(member), message);
            }

            /// <summary>A problem of whatever <paramref name="at"/> names.</summary>
            public void ProblemAt(string at, string message)
            {
                problems.Add(at + " " + message + ".");
            }

            public void CheckUnknown()
            {
                foreach (string name in values.Keys.Where(name => !read.Contains(name)).OrderBy(name => name, StringComparer.Ordinal))
                    ProblemAt(where ?? "the file", "has the unknown member \"" + name + "\"");
            }

            private object Get(string name, bool required)
            {
                if (Has(name))
                    return values[name];
                if (required)
                    Problem(name, "is required");
                return null;
            }

            public string String(string name, bool required = false)
            {
                return StringItem(Get(name, required), Name(name));
            }

            public string StringItem(object value, string at)
            {
                if (value == null || value is string)
                    return (string)value;
                ProblemAt(at, "must be a string");
                return null;
            }

            /// <summary>A full Windows path (<c>C:\...</c>), in the normal form of <see cref="WinPath"/>.</summary>
            public string Path(string name, bool required = false)
            {
                return PathItem(Get(name, required), Name(name));
            }

            public string PathItem(object value, string at)
            {
                string path = StringItem(value, at);
                if (path == null)
                    return null;
                if (!WinPath.IsFullyQualified(path))
                {
                    ProblemAt(at, "must be a full Windows path (C:\\...), not " + path);
                    return null;
                }
                return WinPath.Normalize(path);
            }

            public long? Integer(string name, bool required = false)
            {
                object value = Get(name, required);
                if (value == null || value is long)
                    return (long?)value;
                Problem(name, "must be an integer");
                return null;
            }

            public bool? Boolean(string name)
            {
                object value = Get(name, false);
                if (value == null || value is bool)
                    return (bool?)value;
                Problem(name, "must be true or false");
                return null;
            }

            public T? Enum<T>(string name, bool required = false)
                where T : struct
            {
                return EnumItem<T>(Get(name, required), Name(name));
            }

            public T? EnumItem<T>(object value, string at)
                where T : struct
            {
                string text = StringItem(value, at);
                if (text == null)
                    return null;
                string known = System.Enum.GetNames(typeof(T))
                                     .FirstOrDefault(n => string.Equals(n, text, StringComparison.OrdinalIgnoreCase));
                if (known == null)
                {
                    ProblemAt(at, "is \"" + text + "\", not one of " + string.Join(", ", System.Enum.GetNames(typeof(T))));
                    return null;
                }
                return (T)System.Enum.Parse(typeof(T), known);
            }

            public Product Product(string name, bool required = false)
            {
                string id = String(name, required);
                if (id == null)
                    return null;
                Product product = Core.Contract.Product.FromId(id);
                if (product == null || product.Id != id)
                    Problem(name, "is \"" + id + "\", not EE or NeoEE");
                return product;
            }

            public Game GameItem(object value, string at)
            {
                string id = StringItem(value, at);
                if (id == null)
                    return null;
                Game game = Game.FromId(id);
                if (game == null || game.Id != id)
                {
                    ProblemAt(at, "is \"" + id + "\", not EE or AoC");
                    return null;
                }
                return game;
            }

            public PerGame<T> PerGame<T>(string name)
                where T : struct
            {
                Members games = Object(name);
                if (games == null)
                    return null;
                var result = new PerGame<T>();
                foreach (Game game in Game.All)
                {
                    T? value = games.Enum<T>(game.Id);
                    if (value != null)
                        result.Set(game, value.Value);
                }
                games.CheckUnknown();
                return result;
            }

            public Members Object(string name)
            {
                object value = Get(name, false);
                return value == null ? null : Child(value, Name(name));
            }

            /// <summary>The items of an array member, each read by <paramref name="item"/>; items read as null are dropped.</summary>
            public IReadOnlyList<T> Array<T>(string name, Func<object, string, T> item, bool required = false)
                where T : class
            {
                object value = Get(name, required);
                if (value == null)
                    return required ? new T[0] : null;
                if (!(value is List<object> items))
                {
                    Problem(name, "must be an array");
                    return new T[0];
                }
                return items.Select((element, index) => item(element, Name(name) + "[" + index.ToString(CultureInfo.InvariantCulture) + "]"))
                            .Where(element => element != null)
                            .ToList();
            }

            /// <summary>The items of an array member of an enumeration.</summary>
            public IReadOnlyList<T> EnumArray<T>(string name, Func<object, string, T?> item)
                where T : struct
            {
                object value = Get(name, false);
                if (value == null)
                    return null;
                if (!(value is List<object> items))
                {
                    Problem(name, "must be an array");
                    return new T[0];
                }
                return items.Select((element, index) => item(element, Name(name) + "[" + index.ToString(CultureInfo.InvariantCulture) + "]"))
                            .Where(element => element != null)
                            .Select(element => element.Value)
                            .ToList();
            }

            /// <summary>How the problems name a member of this object, e.g. <c>installations[0].kind</c>.</summary>
            private string Name(string member)
            {
                return where == null ? member : where + "." + member;
            }
        }
    }
}
