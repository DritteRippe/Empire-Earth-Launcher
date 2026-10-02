using System;
using System.Linq;
using System.Text;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.GameSettings;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Play;
using Empire_Earth_Launcher.Tests.Fakes;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.GameSettings
{
    /// <summary>
    /// <see cref="CompatibilityOptions"/> (contract 3.7, ADR 0007 plan review, ADR 0015): switches only from Windows 8 on and
    /// outside Wine, HKCU only with every other entry kept, HKLM read-only, no second Windows version mode, on Windows 7
    /// only the removal of exactly <c>~ RUNASADMIN</c> and the old values shown.
    /// </summary>
    [TestFixture]
    public class CompatibilityOptionsTests
    {
        private const string Root = GameSettingsWorld.NeoRoot;
        private const string EeProgram = Root + @"\Empire Earth\Empire Earth.exe";
        private const string AocProgram = Root + @"\Empire Earth - The Art of Conquest\EE-AOC.exe";

        private static readonly RegistryLocation Hkcu = GameSettingsWorld.Layers;
        private static readonly RegistryLocation Hklm64 = RegistryLocation.LocalMachine64(ContractNames.CompatibilityLayersKey);

        private GameSettingsWorld w;
        private Installation installation;

        private CompatibilityOptions Create(FakeSystemInfo systemInfo = null)
        {
            w = new GameSettingsWorld(systemInfo);
            w.AddAdminInstallationOfAnotherAccount(Root, Product.NeoEE);
            installation = w.Discover().Selected;
            return new CompatibilityOptions(w.Registry, w.SystemInfo, w.Guard, w.Backups, w.Logger);
        }

        private string CurrentUserValue(string program)
        {
            return w.Get(Hkcu, program)?.StringValue;
        }

        [Test]
        public void Windows10_OffersTheFourEntriesOfTheContractRows()
        {
            CompatibilityState state = Create().Read(installation);

            Assert.That(state.SwitchesOffered, Is.True);
            Assert.That(state.Entries.Select(e => e.Name), Is.EqualTo(new[] { "DWM8And16BitMitigation", "HIGHDPIAWARE", "HeapClearAllocation", "WIN7RTM" }));
            Assert.That(state.Entries.All(e => e.IsOffered && e.State == EntryState.Off));
            Assert.That(state.Programs.Select(p => p.ProgramPath), Is.EqualTo(new[] { EeProgram, AocProgram }));
            Assert.That(state.LegacyValues, Is.Empty);
        }

        [Test]
        public void SetEntry_WritesHkcuForEveryProgram_KeepsOtherEntries()
        {
            CompatibilityOptions options = Create();
            w.RawRegistry.Seed(Hkcu, EeProgram, RegistryValue.FromString("~ RUNASADMIN DISABLEDXMAXIMIZEDWINDOWEDMODE"));

            CompatibilityResult result = options.SetEntry(installation, "HIGHDPIAWARE", true);
            options.SetEntry(installation, "WIN7RTM", true);

            Assert.That(result.Outcome, Is.EqualTo(GameSettingsOutcome.Done));
            Assert.That(CurrentUserValue(EeProgram), Is.EqualTo("~ RUNASADMIN DISABLEDXMAXIMIZEDWINDOWEDMODE HIGHDPIAWARE WIN7RTM"));
            Assert.That(CurrentUserValue(AocProgram), Is.EqualTo("~ HIGHDPIAWARE WIN7RTM"));
            Assert.That(options.Read(installation).Entries.Single(e => e.Name == "HIGHDPIAWARE").State, Is.EqualTo(EntryState.On));
            Assert.That(w.RawRegistry.GetValueNames(Hklm64).Status, Is.EqualTo(RegistryStatus.Missing), "HKLM is never written");
        }

        [Test]
        public void SetEntry_Off_RemovesTheEntry_AndAnEmptyValue()
        {
            CompatibilityOptions options = Create();
            w.RawRegistry.Seed(Hkcu, EeProgram, RegistryValue.FromString("~ RUNASADMIN HIGHDPIAWARE"));
            w.RawRegistry.Seed(Hkcu, AocProgram, RegistryValue.FromString("~ HIGHDPIAWARE"));

            Assert.That(options.Read(installation).Entries.Single(e => e.Name == "HIGHDPIAWARE").State, Is.EqualTo(EntryState.On));
            options.SetEntry(installation, "HIGHDPIAWARE", false);

            Assert.That(CurrentUserValue(EeProgram), Is.EqualTo("~ RUNASADMIN"));
            Assert.That(CurrentUserValue(AocProgram), Is.Null);
        }

        [Test]
        public void SetEntry_Mixed()
        {
            CompatibilityOptions options = Create();
            w.RawRegistry.Seed(Hkcu, AocProgram, RegistryValue.FromString("~ HeapClearAllocation"));

            Assert.That(options.Read(installation).Entries.Single(e => e.Name == "HeapClearAllocation").State, Is.EqualTo(EntryState.Mixed));
            CompatibilityResult result = options.SetEntry(installation, "HeapClearAllocation", true);

            Assert.That(result.Changes.Select(c => c.ValueName), Is.EqualTo(new[] { EeProgram }), "AoC has it already");
        }

        [Test]
        public void SetEntry_NeverRunAsAdminOrWinXpSp3()
        {
            CompatibilityOptions options = Create();

            Assert.That(() => options.SetEntry(installation, CompatibilityLayers.RunAsAdmin, true), Throws.ArgumentException);
            Assert.That(() => options.SetEntry(installation, "WINXPSP3", true), Throws.ArgumentException);
        }

        /// <summary>ADR 0015: the HKLM value of an admin setup has a Windows version mode, so no other one is offered.</summary>
        [TestCase("~ DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation WIN7RTM")]
        [TestCase("~ WINXPSP3")]
        [TestCase("~ RUNASADMIN VISTASP2")]
        public void HklmVersionMode_NoVersionModeOffered_HklmShownReadOnly(string hklm)
        {
            CompatibilityOptions options = Create();
            w.RawRegistry.Seed(Hklm64, EeProgram, RegistryValue.FromString(hklm));

            CompatibilityState state = options.Read(installation);

            Assert.That(state.Programs[0].LocalMachine, Is.EqualTo(hklm));
            Assert.That(state.Entries.Single(e => e.Name == "WIN7RTM").Unavailable, Is.EqualTo(EntryUnavailable.VersionModeSet));
            Assert.That(state.Entries.Where(e => e.Name != "WIN7RTM").All(e => e.IsOffered));
            Assert.That(options.SetEntry(installation, "WIN7RTM", true).Outcome, Is.EqualTo(GameSettingsOutcome.Failed));
            Assert.That(w.Changes, Is.Empty);
        }

        [Test]
        public void HkcuWithAnotherVersionMode_Win7RtmNotOffered_OtherEntriesKeepIt()
        {
            CompatibilityOptions options = Create();
            w.RawRegistry.Seed(Hkcu, EeProgram, RegistryValue.FromString("~ WINXPSP3"));

            CompatibilityState state = options.Read(installation);
            options.SetEntry(installation, "HIGHDPIAWARE", true);

            Assert.That(state.Entries.Single(e => e.Name == "WIN7RTM").IsOffered, Is.False);
            Assert.That(CurrentUserValue(EeProgram), Is.EqualTo("~ WINXPSP3 HIGHDPIAWARE"));
        }

        [Test]
        public void Hklm_EntryInLocalMachineIsShown()
        {
            CompatibilityOptions options = Create();
            w.RawRegistry.Seed(RegistryLocation.LocalMachine32(ContractNames.CompatibilityLayersKey), AocProgram,
                RegistryValue.FromString("~ HIGHDPIAWARE"));

            CompatibilityState state = options.Read(installation);

            Assert.That(state.Entries.Single(e => e.Name == "HIGHDPIAWARE").InLocalMachine, Is.True);
            Assert.That(state.Programs[1].LocalMachine, Is.EqualTo("~ HIGHDPIAWARE"));
        }

        /// <summary>Windows 7: no switch, only removing exactly <c>~ RUNASADMIN</c> and the old values shown (ADR 0007 plan review).</summary>
        [Test]
        public void Windows7_NoSwitches_OldValuesShownReadOnly()
        {
            CompatibilityOptions options = Create(FakeSystemInfo.Windows7());
            w.RawRegistry.Seed(Hkcu, EeProgram, RegistryValue.FromString("~ RUNASADMIN"));
            w.RawRegistry.Seed(Hklm64, AocProgram, RegistryValue.FromString("~ DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation WINXPSP3"));
            w.RawRegistry.Seed(Hkcu, AocProgram, RegistryValue.FromString("~ WINXPSP3"));

            CompatibilityState state = options.Read(installation);

            Assert.That(state.SwitchesOffered, Is.False);
            Assert.That(state.Entries, Is.Empty);
            Assert.That(state.RunAsAdminRemovable, Is.True);
            Assert.That(state.LegacyValues.Select(v => v.Key.ToString().Substring(0, 6) + " " + v.Value), Is.EquivalentTo(new[]
            {
                @"HKCU\S ~ WINXPSP3", @"HKLM64 ~ DWM8And16BitMitigation HIGHDPIAWARE HeapClearAllocation WINXPSP3"
            }));
            Assert.That(options.SetEntry(installation, "HIGHDPIAWARE", true).Outcome, Is.EqualTo(GameSettingsOutcome.Failed));
            Assert.That(w.Changes, Is.Empty);
        }

        [Test]
        public void Wine_NoSwitches()
        {
            CompatibilityState state = Create(new FakeSystemInfo { IsWine = true }).Read(installation);

            Assert.That(state.SwitchesOffered, Is.False);
            Assert.That(state.Entries, Is.Empty);
        }

        [TestCase(10)]
        [TestCase(6)]
        public void RemoveRunAsAdmin_OnlyExactly_WithABackup(int windowsMajor)
        {
            CompatibilityOptions options = Create(new FakeSystemInfo { WindowsVersion = new Version(windowsMajor, 1) });
            w.RawRegistry.Seed(Hkcu, EeProgram, RegistryValue.FromString("~ RUNASADMIN"));
            w.RawRegistry.Seed(Hkcu, AocProgram, RegistryValue.FromString("~ RUNASADMIN HIGHDPIAWARE"));

            CompatibilityResult result = options.RemoveRunAsAdmin(installation);

            Assert.That(result.Outcome, Is.EqualTo(GameSettingsOutcome.Done), result.ToString());
            Assert.That(CurrentUserValue(EeProgram), Is.Null);
            Assert.That(CurrentUserValue(AocProgram), Is.EqualTo("~ RUNASADMIN HIGHDPIAWARE"), "not exactly ~ RUNASADMIN");
            Assert.That(result.BackupFile, Does.Contain("_remove-runasadmin"));
            string backup = Encoding.Unicode.GetString(w.FileSystem.GetContent(result.BackupFile));
            Assert.That(backup, Does.Contain("\"" + EeProgram.Replace(@"\", @"\\") + "\"=\"~ RUNASADMIN\""));
            Assert.That(options.Read(installation).RunAsAdminRemovable, Is.False);
        }

        [Test]
        public void RemoveRunAsAdmin_BackupFails_NothingChanged()
        {
            CompatibilityOptions options = Create();
            w.RawRegistry.Seed(Hkcu, EeProgram, RegistryValue.FromString("~ RUNASADMIN"));
            w.FileSystem.FailOn(GameSettingsWorld.BackupsFolder, FileSystemOperation.Write, FileSystemStatus.IoError);

            Assert.That(options.RemoveRunAsAdmin(installation).Outcome, Is.EqualTo(GameSettingsOutcome.BackupFailed));
            Assert.That(CurrentUserValue(EeProgram), Is.EqualTo("~ RUNASADMIN"));
        }

        [TestCase(150, "HIGHDPIAWARE", false, true)]
        [TestCase(100, "HIGHDPIAWARE", false, false)]
        [TestCase(150, "HIGHDPIAWARE", true, false)]
        [TestCase(150, "WIN7RTM", false, false)]
        public void HighDpiHint_WhenSwitchedOffOnAScaledScreen(int percent, string entry, bool on, bool expected)
        {
            CompatibilityOptions options = Create(new FakeSystemInfo().WithScreen(1920, 1080, percent));

            Assert.That(options.NeedsHighDpiHint(entry, on), Is.EqualTo(expected));
        }

        [Test]
        public void EveryChange_IsBlockedBySetupAndGame(
            [Values("EE_Setup", "StainlessSteelStudiosPresentsEmpireEarth")] string mutex,
            [Values(true, false)] bool removeRunAsAdmin)
        {
            CompatibilityOptions options = Create();
            w.RawRegistry.Seed(Hkcu, EeProgram, RegistryValue.FromString("~ RUNASADMIN"));
            w.Mutexes.With(mutex);

            CompatibilityResult result = removeRunAsAdmin
                ? options.RemoveRunAsAdmin(installation)
                : options.SetEntry(installation, "HIGHDPIAWARE", true);

            Assert.That(result.Outcome, Is.EqualTo(GameSettingsOutcome.Blocked));
            Assert.That(result.Block.Block, Is.EqualTo(mutex.EndsWith("_Setup", StringComparison.Ordinal)
                ? MutationBlock.SetupRunning : MutationBlock.GameRunning));
            Assert.That(w.Changes, Is.Empty);
        }
    }
}
