using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.RealMachineTests.Expectations;
using Empire_Earth_Launcher.RealMachineTests.Json;

namespace Empire_Earth_Launcher.RealMachineTests.Checks
{
    /// <summary>
    /// One value of the game settings of a game of an installation as read from HKCU: a setting of the table of contract 3.2,
    /// the defaults marker (3.5) or the GPU preference (3.4). <see cref="Data"/> is <c>Type:data</c>, <c>Missing</c> or
    /// <c>Unreadable ...</c>; settings and paths only, no secret and nothing of the game data.
    /// </summary>
    internal sealed class SettingValue
    {
        public SettingValue(string root, Game game, string name, string data)
        {
            Root = WinPath.Normalize(root ?? throw new ArgumentNullException(nameof(root)));
            Game = game ?? throw new ArgumentNullException(nameof(game));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public string Root { get; }

        public Game Game { get; }

        /// <summary>The name of the setting (<see cref="GameSetting.Name"/>), <c>Marker</c> or <c>GpuPreference</c>.</summary>
        public string Name { get; }

        public string Data { get; }

        /// <summary>Root, game and name: what two lists of values are matched by.</summary>
        public string Id
        {
            get { return Root.ToUpperInvariant() + "|" + Game.Id + "|" + Name.ToUpperInvariant(); }
        }

        public override string ToString()
        {
            return Root + " " + Game.Id + " \"" + Name + "\" = " + Data;
        }
    }

    /// <summary>Reads, saves and compares the game settings of installations (<see cref="SettingValue"/>).</summary>
    internal static class SettingValues
    {
        /// <summary>The <c>Data</c> of a value that does not exist.</summary>
        public const string Missing = "Missing";

        private const int Schema = 1;

        /// <summary>Every value of every game of <paramref name="installation"/> in the HKCU of the current account.</summary>
        public static IReadOnlyList<SettingValue> Read(IRegistry registry, Installation installation)
        {
            if (registry == null)
                throw new ArgumentNullException(nameof(registry));
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            var values = new List<SettingValue>();
            foreach (Game game in GameDefaultsService.GamesOf(installation))
            {
                foreach (Tuple<string, RegistryLocation, string> value in Locations(installation, game))
                    values.Add(new SettingValue(installation.Root, game, value.Item1, Describe(registry.GetValue(value.Item2, value.Item3))));
            }
            return values;
        }

        /// <summary>Name, key and value name of every value of one game: the table, the marker, the GPU preference.</summary>
        public static IEnumerable<Tuple<string, RegistryLocation, string>> Locations(Installation installation, Game game)
        {
            RegistryLocation key = GameDefaultsService.SettingsKey(installation, game);
            foreach (GameSetting setting in GameSettingsTable.All)
                yield return Tuple.Create(setting.Name, setting.KeyIn(key), setting.ValueName);
            yield return Tuple.Create(ExpectationFile.MarkerName, GameDefaultsService.MarkerKey(installation.Product), game.Id);
            yield return Tuple.Create(ExpectationFile.GpuPreferenceName, RegistryLocation.CurrentUser(ContractNames.GpuPreferencesKey),
                GameDefaultsService.ProgramPath(installation, game));
        }

        /// <summary>The <c>Data</c> of a read value.</summary>
        public static string Describe(RegistryResult<RegistryValue> read)
        {
            if (read.Status == RegistryStatus.Missing)
                return Missing;
            return read.IsOk ? Describe(read.Value) : "Unreadable " + read.Status;
        }

        /// <summary>The <c>Data</c> of a value: <c>DWord:44</c>, <c>String:Direct3D</c>, other types as the core writes them in its log.</summary>
        public static string Describe(RegistryValue value)
        {
            if (value == null)
                return Missing;
            switch (value.Type)
            {
                case RegistryValueType.DWord:
                    return value.Type + ":" + value.DWordValue.ToString(CultureInfo.InvariantCulture);
                case RegistryValueType.String:
                case RegistryValueType.ExpandString:
                    return value.Type + ":" + value.StringValue;
                default:
                    return value.Type + ":" + value;
            }
        }

        /// <summary>The values as the JSON file of <see cref="DefaultsExpectation.RecordSetupValuesTo"/> (UTF-8, LF).</summary>
        public static byte[] ToJson(IEnumerable<SettingValue> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            var text = new StringBuilder();
            text.Append("{\n  ").Append(JsonWriter.Member("schema", Schema.ToString(CultureInfo.InvariantCulture))).Append(",\n  \"values\": [");
            string separator = "\n";
            foreach (SettingValue value in values)
            {
                text.Append(separator).Append("    { ")
                    .Append(JsonWriter.Member("root", JsonWriter.Quote(value.Root))).Append(", ")
                    .Append(JsonWriter.Member("game", JsonWriter.Quote(value.Game.Id))).Append(", ")
                    .Append(JsonWriter.Member("name", JsonWriter.Quote(value.Name))).Append(", ")
                    .Append(JsonWriter.Member("data", JsonWriter.Quote(value.Data))).Append(" }");
                separator = ",\n";
            }
            text.Append("\n  ]\n}\n");
            return new UTF8Encoding(false).GetBytes(text.ToString());
        }

        /// <summary>The values of a file written by <see cref="ToJson"/>.</summary>
        /// <exception cref="FormatException">The file is not such a file.</exception>
        public static IReadOnlyList<SettingValue> FromJson(byte[] content)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));
            string text = new UTF8Encoding(false, true).GetString(content).TrimStart('\uFEFF');
            var file = JsonReader.Parse(text) as Dictionary<string, object>;
            if (file == null || file.Count != 2 || !Equals(Member(file, "schema"), (long)Schema) || !(Member(file, "values") is List<object> items))
                throw new FormatException("Not a file of saved game settings (schema " + Schema.ToString(CultureInfo.InvariantCulture) + ").");
            var values = new List<SettingValue>();
            foreach (object item in items)
            {
                var value = item as Dictionary<string, object>;
                Game game = Game.FromId(Member(value, "game") as string);
                if (value == null || value.Count != 4 || !(Member(value, "root") is string root) || game == null ||
                    !(Member(value, "name") is string name) || !(Member(value, "data") is string data) || !WinPath.IsFullyQualified(root))
                    throw new FormatException("A value of the saved game settings is invalid.");
                values.Add(new SettingValue(root, game, name, data));
            }
            return values;
        }

        private static object Member(Dictionary<string, object> members, string name)
        {
            return members != null && members.TryGetValue(name, out object value) ? value : null;
        }

        /// <summary>
        /// The differences of <paramref name="actual"/> from <paramref name="expected"/> (matched by root, game and name), except
        /// for the names in <paramref name="allowedDifferences"/>; <paramref name="what"/> names the expected values.
        /// </summary>
        public static IReadOnlyList<string> Compare(IEnumerable<SettingValue> expected, IEnumerable<SettingValue> actual,
            IEnumerable<string> allowedDifferences, string what)
        {
            if (expected == null)
                throw new ArgumentNullException(nameof(expected));
            if (actual == null)
                throw new ArgumentNullException(nameof(actual));
            var allowed = new HashSet<string>(allowedDifferences ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            Dictionary<string, SettingValue> wanted = expected.ToDictionary(value => value.Id, StringComparer.Ordinal);
            var problems = new List<string>();
            foreach (SettingValue value in actual)
            {
                if (allowed.Contains(value.Name))
                    continue;
                if (!wanted.TryGetValue(value.Id, out SettingValue other))
                    problems.Add(value.Root + " " + value.Game.Id + " \"" + value.Name + "\": not in " + what);
                else if (!string.Equals(value.Data, other.Data, StringComparison.Ordinal))
                    problems.Add(value.Root + " " + value.Game.Id + " \"" + value.Name + "\" is " + value.Data + ", " + what + " " + other.Data);
            }
            return problems;
        }
    }
}
