using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.GameSettings
{
    /// <summary>
    /// <see cref="GameSettingsTable"/> against the table of contract 3.2 in <c>docs/CONTRACT.md</c>: the same values in the
    /// same order, with the same type, data (fixed, per game, or "see 3.3") and class S, D or P. The setup's
    /// <c>ci/check_contract.py</c> compares the same table with the <c>GameSettings</c> block of <c>setup_is6.iss</c>, so a
    /// change in one of the three places fails a check.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.SourceTree)]
    public class GameSettingsTableContractTests
    {
        private sealed class ContractRow
        {
            public string Name;
            public string Type;
            public string Data;
            public string Class;
        }

        private static readonly Regex Code = new Regex("`([^`]*)`", RegexOptions.CultureInvariant);

        private static List<ContractRow> ReadTable()
        {
            string[] lines = File.ReadAllLines(RepositoryRoot.GetFullPath("docs/CONTRACT.md"));
            int start = Array.FindIndex(lines, line => line.StartsWith("### 3.2 Values", StringComparison.Ordinal));
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "CONTRACT.md has no section 3.2");
            int header = Array.FindIndex(lines, start, line => line.StartsWith("| Value | Type | Data | Class |", StringComparison.Ordinal));
            Assert.That(header, Is.GreaterThan(start), "section 3.2 has no table Value | Type | Data | Class");

            var rows = new List<ContractRow>();
            for (int i = header + 2; i < lines.Length && lines[i].StartsWith("|", StringComparison.Ordinal); i++)
            {
                string[] cells = lines[i].Trim().Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();
                Assert.That(cells, Has.Length.EqualTo(4), "row " + lines[i]);
                rows.Add(new ContractRow { Name = Code.Match(cells[0]).Groups[1].Value, Type = cells[1], Data = cells[2], Class = cells[3] });
            }
            return rows;
        }

        private static string TypeName(RegistryValueType type)
        {
            return type == RegistryValueType.String ? "REG_SZ" : type == RegistryValueType.DWord ? "REG_DWORD" : type.ToString();
        }

        /// <summary>The data as the contract writes it, for one game: a number, a text, or "computed".</summary>
        private static string ContractData(ContractRow row, Game game)
        {
            if (row.Data.StartsWith("see [3.3]", StringComparison.Ordinal))
                return "computed";
            Match perGame = Regex.Match(row.Data, "^Empire Earth `([^`]*)` \\(0x([0-9A-F]+)\\), The Art of Conquest `([^`]*)` \\(0x([0-9A-F]+)\\)");
            if (perGame.Success)
            {
                Assert.That(int.Parse(perGame.Groups[2].Value, NumberStyles.HexNumber), Is.EqualTo(int.Parse(perGame.Groups[1].Value)), row.Name);
                Assert.That(int.Parse(perGame.Groups[4].Value, NumberStyles.HexNumber), Is.EqualTo(int.Parse(perGame.Groups[3].Value)), row.Name);
                return perGame.Groups[game == Game.EmpireEarth ? 1 : 3].Value;
            }
            Match fixedData = Regex.Match(row.Data, "^`([^`]*)`(?: \\(0x([0-9A-F]+)[,)])?");
            Assert.That(fixedData.Success, Is.True, "unknown data of " + row.Name + ": " + row.Data);
            if (fixedData.Groups[2].Success)
                Assert.That(int.Parse(fixedData.Groups[2].Value, NumberStyles.HexNumber), Is.EqualTo(int.Parse(fixedData.Groups[1].Value)),
                    "hex and decimal of " + row.Name);
            return fixedData.Groups[1].Value;
        }

        private static string CodeData(GameSetting setting, Game game)
        {
            if (setting.IsComputed)
                return "computed";
            RegistryValue value = setting.FixedValue(game);
            return value.Type == RegistryValueType.DWord
                ? value.DWordValue.ToString(CultureInfo.InvariantCulture)
                : value.StringValue;
        }

        [Test]
        public void Table_3_2_HasTheValuesOfTheCodeInTheSameOrder()
        {
            Assert.That(ReadTable().Select(row => row.Name), Is.EqualTo(GameSettingsTable.All.Select(setting => setting.Name)));
        }

        [Test]
        public void Table_3_2_TypeDataAndClassOfEveryValue()
        {
            List<ContractRow> rows = ReadTable();
            foreach (GameSetting setting in GameSettingsTable.All)
            {
                ContractRow row = rows.Single(r => r.Name == setting.Name);
                Assert.That(row.Type, Is.EqualTo(TypeName(setting.Type)), "type of " + setting.Name);
                Assert.That(row.Class, Is.EqualTo(setting.Class.ToString()), "class of " + setting.Name);
                foreach (Game game in Game.All)
                    Assert.That(ContractData(row, game), Is.EqualTo(CodeData(setting, game)), "data of " + setting.Name + " for " + game.Id);
            }
        }

        [Test]
        public void Table_3_2_EndingEpochIsTheLastEpochOfEachGame()
        {
            GameSetting endingEpoch = GameSettingsTable.Find(@"Game Options\Ending Epoch");

            Assert.That(endingEpoch.Data, Is.EqualTo(SettingData.PerGame));
            Assert.That(endingEpoch.FixedValue(Game.EmpireEarth), Is.EqualTo(RegistryValue.FromDWord(13)));
            Assert.That(endingEpoch.FixedValue(Game.ArtOfConquest), Is.EqualTo(RegistryValue.FromDWord(14)));
        }

        [Test]
        public void Section_3_3_NamesTheRasterizersAndTheWindowLimits()
        {
            // Line breaks and indentation of the Markdown do not matter.
            string contract = Regex.Replace(File.ReadAllText(RepositoryRoot.GetFullPath("docs/CONTRACT.md")), @"\s+", " ");

            Assert.That(contract, Does.Contain("**`Rasterizer Name`**: `" + GameSettingsTable.Direct3DRasterizer +
                                              "` under Wine or when a DirectX wrapper is installed, otherwise `" +
                                              GameSettingsTable.HardwareTnLRasterizer + "`."));
            Assert.That(contract, Does.Contain("limited to " + ComputedValues.MinGameWindowWidth + " to " + ComputedValues.MaxGameWindowWidth));
            Assert.That(contract, Does.Contain("limited to " + ComputedValues.MinGameWindowHeight + " to " + ComputedValues.MaxGameWindowHeight));
            Assert.That(contract, Does.Contain("if one of `" + string.Join("`, `", GameSettingsTable.DirectXWrapperFiles) +
                                              "` is in the game folder"));
            Assert.That(GameSettingsTable.DirectXWrapperFiles, Is.EqualTo(new[] { "DDraw.dll", "D3DImm.dll", "D3D8.dll", "D3D9.dll" }));
        }
    }
}
