using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.Core.Settings;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Settings
{
    /// <summary>
    /// <c>settings.json</c> (<see cref="SettingsStore"/>, ADR 0005), including the scenarios of the removed
    /// <c>UserSettingsRecoveryTests</c> (damaged file moved aside, older copy replaced, a file that cannot be
    /// handled left alone) and the behaviour a settings file of a newer launcher needs.
    /// </summary>
    [TestFixture]
    public class SettingsStoreTests
    {
        private const string Folder = @"C:\Users\Player\AppData\Local\Empire Earth Launcher";
        private const string File = Folder + @"\settings.json";
        private const string DamagedFile = File + ".damaged";

        private const string ValidJson =
            "{\"SchemaVersion\":1,\"GameDirectory\":\"D:\\\\Spiele\\\\Empire Earth\",\"ThemeName\":\"Dark\",\"CustomThemeFile\":\"\"}";

        private InMemoryFileSystem fileSystem;
        private RecordingLogger logger;
        private SettingsStore store;

        [SetUp]
        public void SetUp()
        {
            fileSystem = new InMemoryFileSystem();
            fileSystem.AddDirectory(Folder);
            logger = new RecordingLogger();
            store = new SettingsStore(fileSystem, File, logger);
        }

        private static void AssertDefaults(LauncherSettings settings)
        {
            Assert.That(settings.GameDirectory, Is.Empty);
            Assert.That(settings.ThemeName, Is.EqualTo("Light"));
            Assert.That(settings.CustomThemeFile, Is.Empty);
            Assert.That(settings.SchemaVersion, Is.EqualTo(1));
        }

        [Test]
        public void MissingFile_DefaultsAreUsed_AndNothingIsCreated()
        {
            Assert.That(store.Load(), Is.EqualTo(SettingsLoadStatus.NotFound));

            AssertDefaults(store.Current);
            Assert.That(store.CanSave, Is.True);
            Assert.That(fileSystem.AllFiles, Is.Empty);
            Assert.That(logger.MessagesOf(LogLevel.Error), Is.Empty);
        }

        [Test]
        public void ValidFile_IsLoaded_AndNothingIsChanged()
        {
            fileSystem.AddFile(File, ValidJson);

            Assert.That(store.Load(), Is.EqualTo(SettingsLoadStatus.Loaded));

            Assert.That(store.Current.GameDirectory, Is.EqualTo(@"D:\Spiele\Empire Earth"));
            Assert.That(store.Current.ThemeName, Is.EqualTo("Dark"));
            Assert.That(fileSystem.GetText(File), Is.EqualTo(ValidJson));
            Assert.That(fileSystem.AllFiles, Is.EqualTo(new[] { File }));
            Assert.That(logger.MessagesOf(LogLevel.Error), Is.Empty);
            Assert.That(logger.MessagesOf(LogLevel.Warning), Is.Empty);
        }

        [Test]
        public void DamagedFile_IsMovedAside_AndTheDefaultsAreUsed()
        {
            const string damaged = "{\"SchemaVersion\":1,\"ThemeName\":\"Da";
            fileSystem.AddFile(File, damaged);

            Assert.That(store.Load(), Is.EqualTo(SettingsLoadStatus.Damaged));

            AssertDefaults(store.Current);
            Assert.That(fileSystem.FileExists(File), Is.False);
            Assert.That(fileSystem.GetText(DamagedFile), Is.EqualTo(damaged));
            Assert.That(logger.MessagesOf(LogLevel.Error), Has.Count.EqualTo(1));
            Assert.That(logger.MessagesOf(LogLevel.Error)[0], Does.Contain(DamagedFile));

            // The settings work again: a change is saved into a new file.
            store.Current.ThemeName = "Blue";
            Assert.That(store.Save(), Is.EqualTo(SettingsSaveStatus.Saved));
            Assert.That(new SettingsStore(fileSystem, File, logger).LoadAndGet().ThemeName, Is.EqualTo("Blue"));
        }

        [Test]
        public void OlderDamagedCopy_IsReplaced()
        {
            fileSystem.AddFile(DamagedFile, "old copy");
            fileSystem.AddFile(File, "new damage");

            Assert.That(store.Load(), Is.EqualTo(SettingsLoadStatus.Damaged));

            Assert.That(fileSystem.GetText(DamagedFile), Is.EqualTo("new damage"));
            Assert.That(fileSystem.AllFiles, Is.EqualTo(new[] { DamagedFile }));
        }

        [Test]
        public void DamagedFileThatCannotBeMovedAside_DefaultsAreUsedAndItIsLogged()
        {
            fileSystem.AddFile(File, "{");
            fileSystem.FailOn(File, FileSystemOperation.Move, FileSystemStatus.AccessDenied);

            Assert.That(store.Load(), Is.EqualTo(SettingsLoadStatus.Damaged));

            AssertDefaults(store.Current);
            Assert.That(fileSystem.GetText(File), Is.EqualTo("{"));
            Assert.That(logger.MessagesOf(LogLevel.Error).Single(), Does.Contain("Unable to keep the damaged file"));
        }

        [Test]
        public void UnreadableFile_IsNeverOverwritten()
        {
            // Like a user.config error without a file name before: nothing is moved, nothing is lost.
            fileSystem.AddFile(File, ValidJson);
            fileSystem.FailOn(File, FileSystemOperation.Read, FileSystemStatus.AccessDenied);

            Assert.That(store.Load(), Is.EqualTo(SettingsLoadStatus.Unreadable));
            AssertDefaults(store.Current);
            Assert.That(store.CanSave, Is.False);

            store.Current.ThemeName = "Blue";
            Assert.That(store.Save(), Is.EqualTo(SettingsSaveStatus.NotSaved));

            Assert.That(fileSystem.GetText(File), Is.EqualTo(ValidJson));
            Assert.That(fileSystem.FileExists(DamagedFile), Is.False);
            Assert.That(logger.MessagesOf(LogLevel.Error), Has.Count.EqualTo(1));
            Assert.That(logger.MessagesOf(LogLevel.Warning), Has.Count.EqualTo(1));
        }

        [Test]
        public void FileOfANewerLauncher_IsNotOverwritten()
        {
            const string newer = "{\"SchemaVersion\":2,\"Theme\":{\"Name\":\"Dark\"}}";
            fileSystem.AddFile(File, newer);

            Assert.That(store.Load(), Is.EqualTo(SettingsLoadStatus.NewerSchema));
            AssertDefaults(store.Current);

            store.Current.GameDirectory = @"C:\Games";
            Assert.That(store.Save(), Is.EqualTo(SettingsSaveStatus.NotSaved));
            Assert.That(fileSystem.GetText(File), Is.EqualTo(newer));
            Assert.That(logger.MessagesOf(LogLevel.Warning)[0], Does.Contain("newer launcher"));
        }

        public static IEnumerable<TestCaseData> DamagedContents()
        {
            yield return new TestCaseData(new byte[0]).SetName("Damaged(empty)");
            yield return new TestCaseData(Bytes("{")).SetName("Damaged(open brace)");
            yield return new TestCaseData(Bytes("null")).SetName("Damaged(null)");
            yield return new TestCaseData(Bytes("[]")).SetName("Damaged(array)");
            yield return new TestCaseData(Bytes("42")).SetName("Damaged(number)");
            yield return new TestCaseData(Bytes("{}")).SetName("Damaged(no schema version)");
            yield return new TestCaseData(Bytes("{\"SchemaVersion\":0}")).SetName("Damaged(schema version 0)");
            yield return new TestCaseData(Bytes("{\"SchemaVersion\":\"one\"}")).SetName("Damaged(schema version text)");
            yield return new TestCaseData(Bytes("{\"SchemaVersion\":99999999999}")).SetName("Damaged(schema version overflow)");
            yield return new TestCaseData(Bytes("{\"SchemaVersion\":1} trailing")).SetName("Damaged(trailing text)");
            yield return new TestCaseData(Bytes("{\"SchemaVersion\":1,\"ThemeName\":\"a\",\"ThemeName\":\"b\"}")).SetName("Damaged(duplicate member)");
            yield return new TestCaseData(new byte[] { 0, 1, 2, 0xFF, 0xFE, 3 }).SetName("Damaged(binary)");
            yield return new TestCaseData(new byte[] { (byte)'{', (byte)'"', (byte)'x', (byte)'"', (byte)':', (byte)'"', 0xFC, (byte)'"', (byte)'}' })
                .SetName("Damaged(invalid UTF-8)");
            yield return new TestCaseData(Bytes("{\"SchemaVersion\":1,\"GameDirectory\":\"" + new string('x', (int)SettingsStore.MaxFileBytes) + "\"}"))
                .SetName("Damaged(larger than 1 MiB)");
        }

        [TestCaseSource(nameof(DamagedContents))]
        public void DamagedContent_IsMovedAside(byte[] content)
        {
            fileSystem.AddFile(File, content);

            Assert.That(store.Load(), Is.EqualTo(SettingsLoadStatus.Damaged));

            AssertDefaults(store.Current);
            Assert.That(fileSystem.GetContent(DamagedFile), Is.EqualTo(content));
            Assert.That(fileSystem.FileExists(File), Is.False);
        }

        [Test]
        public void AnyContent_NeverThrows()
        {
            // Truncated and randomly changed versions of a valid file: always a status, never an exception.
            byte[] valid = Bytes(ValidJson);
            var random = new Random(1);
            for (int i = 0; i < 400; i++)
            {
                byte[] content = valid.Take(random.Next(valid.Length + 1)).ToArray();
                if (i % 2 == 1 && content.Length > 0)
                    content[random.Next(content.Length)] = (byte)random.Next(256);
                fileSystem.AddFile(File, content);

                SettingsLoadStatus status = SettingsLoadStatus.Unreadable;
                Assert.That(() => status = store.Load(), Throws.Nothing, "content: " + BitConverter.ToString(content));
                Assert.That(status, Is.EqualTo(SettingsLoadStatus.Loaded).Or.EqualTo(SettingsLoadStatus.Damaged)
                    .Or.EqualTo(SettingsLoadStatus.NewerSchema));
                Assert.That(store.Current, Is.Not.Null);
            }
        }

        [Test]
        public void FileWithBom_IsLoaded()
        {
            fileSystem.AddFile(File, new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Bytes(ValidJson)).ToArray());

            Assert.That(store.Load(), Is.EqualTo(SettingsLoadStatus.Loaded));
            Assert.That(store.Current.ThemeName, Is.EqualTo("Dark"));
        }

        [Test]
        public void MissingAndNullMembers_GetTheirDefaults()
        {
            fileSystem.AddFile(File, "{\"SchemaVersion\":1,\"ThemeName\":null,\"CustomThemeFile\":null}");

            Assert.That(store.Load(), Is.EqualTo(SettingsLoadStatus.Loaded));
            AssertDefaults(store.Current);
        }

        [Test]
        public void UnknownMembers_SurviveASave()
        {
            // A newer launcher with the same schema version added members; this launcher keeps them.
            fileSystem.AddFile(File, "{\"SchemaVersion\":1,\"UiCulture\":\"de\",\"HiddenWarnings\":[{\"Name\":\"Game Bit Depth\",\"Value\":\"16\"}],\"ThemeName\":\"Dark\"}");
            store.Load();

            store.Current.ThemeName = "Blue";
            Assert.That(store.Save(), Is.EqualTo(SettingsSaveStatus.Saved));

            string saved = fileSystem.GetText(File);
            Assert.That(saved, Does.Contain("\"UiCulture\": \"de\""));
            Assert.That(saved, Does.Contain("\"Game Bit Depth\""));
            Assert.That(saved, Does.Contain("\"ThemeName\": \"Blue\""));
        }

        [Test]
        public void Save_CreatesTheFolder_AndWritesReadableIndentedJson()
        {
            var empty = new InMemoryFileSystem();
            var newStore = new SettingsStore(empty, File, logger);
            newStore.Current.GameDirectory = @"C:\Spiele\Müll\Empire Earth";
            newStore.Current.CustomThemeFile = @"C:\Themes\gold.xml";

            Assert.That(newStore.Save(), Is.EqualTo(SettingsSaveStatus.Saved));

            Assert.That(empty.DirectoryExists(Folder), Is.True);
            Assert.That(empty.AllFiles, Is.EqualTo(new[] { File }), "no temporary file is left");
            string json = empty.GetText(File);
            Assert.That(json, Does.StartWith("{"));
            Assert.That(json, Does.Contain("  \"SchemaVersion\": 1,"), "indented, schema version first");
            Assert.That(json, Does.Contain("Müll"), "UTF-8, not escaped");
            Assert.That(empty.GetContent(File).Take(3), Is.Not.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }), "no BOM");

            LauncherSettings loaded = new SettingsStore(empty, File, logger).LoadAndGet();
            Assert.That(loaded.GameDirectory, Is.EqualTo(@"C:\Spiele\Müll\Empire Earth"));
            Assert.That(loaded.CustomThemeFile, Is.EqualTo(@"C:\Themes\gold.xml"));
            Assert.That(loaded.ThemeName, Is.EqualTo("Light"));
        }

        [Test]
        public void Save_ReplacesTheFileThroughATemporaryFile()
        {
            fileSystem.AddFile(File, ValidJson);
            store.Load();
            store.Current.ThemeName = "Blue";

            Assert.That(store.Save(), Is.EqualTo(SettingsSaveStatus.Saved));

            Assert.That(fileSystem.AllFiles, Is.EqualTo(new[] { File }));
            Assert.That(new SettingsStore(fileSystem, File, logger).LoadAndGet().ThemeName, Is.EqualTo("Blue"));
        }

        [Test]
        public void Save_Failure_KeepsThePreviousFile()
        {
            fileSystem.AddFile(File, ValidJson);
            store.Load();
            fileSystem.FailOn(File + ".tmp", FileSystemOperation.Write, FileSystemStatus.IoError);
            store.Current.ThemeName = "Blue";

            Assert.That(store.Save(), Is.EqualTo(SettingsSaveStatus.Failed));

            Assert.That(fileSystem.GetText(File), Is.EqualTo(ValidJson));
            Assert.That(fileSystem.AllFiles, Is.EqualTo(new[] { File }));
            Assert.That(logger.MessagesOf(LogLevel.Error).Single(), Does.Contain("Unable to save the launcher settings"));
            Assert.That(store.Current.ThemeName, Is.EqualTo("Blue"), "the change applies to this session");
        }

        [Test]
        public void Save_WithoutTheDrive_Fails()
        {
            var newStore = new SettingsStore(fileSystem, @"Q:\Empire Earth Launcher\settings.json", logger);

            Assert.That(newStore.Save(), Is.EqualTo(SettingsSaveStatus.Failed));
        }

        private static byte[] Bytes(string text)
        {
            return new UTF8Encoding(false).GetBytes(text);
        }
    }

    internal static class SettingsStoreTestExtensions
    {
        /// <summary>Loads (expecting success) and returns the settings.</summary>
        public static LauncherSettings LoadAndGet(this SettingsStore store)
        {
            Assert.That(store.Load(), Is.EqualTo(SettingsLoadStatus.Loaded));
            return store.Current;
        }
    }
}
