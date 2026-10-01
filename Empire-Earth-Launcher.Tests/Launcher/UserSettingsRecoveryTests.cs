using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Launcher
{
    /// <summary>
    /// Start with a damaged user.config (<see cref="UserSettingsRecovery"/>). The real settings provider is
    /// replaced by <see cref="DamageableSettingsProvider"/>, which fails like LocalFileSettingsProvider while
    /// its "user.config" exists, so neither the real user settings nor the test's own config are touched.
    /// </summary>
    [TestFixture]
    public class UserSettingsRecoveryTests
    {
        private TemporaryDirectory directory;
        private string userConfig;
        private RecordingLogger logger;

        [SetUp]
        public void SetUp()
        {
            directory = new TemporaryDirectory();
            userConfig = directory.Combine("user.config");
            DamageableSettingsProvider.DamagedFile = userConfig;
            DamageableSettingsProvider.ReportFileName = true;
            logger = new RecordingLogger();
        }

        [TearDown]
        public void TearDown()
        {
            DamageableSettingsProvider.DamagedFile = null;
            directory.Dispose();
        }

        private string BackupFile
        {
            get { return userConfig + UserSettingsRecovery.DamagedFileSuffix; }
        }

        [Test]
        public void ReadableSettings_NothingIsChanged()
        {
            var settings = new TestSettings();

            Assert.That(UserSettingsRecovery.EnsureReadable(settings, nameof(TestSettings.ThemeName), logger), Is.True);

            Assert.That(settings.ThemeName, Is.EqualTo("Light"));
            Assert.That(logger.Errors, Is.Empty);
            Assert.That(Directory.GetFileSystemEntries(directory.Path), Is.Empty);
        }

        [Test]
        public void DamagedUserConfig_IsMovedAsideAndTheDefaultsAreUsed()
        {
            File.WriteAllText(userConfig, "<configuration><userSettings>");
            var settings = new TestSettings();
            Assert.That(() => settings.ThemeName, Throws.InstanceOf<ConfigurationException>(), "precondition");

            Assert.That(UserSettingsRecovery.EnsureReadable(settings, nameof(TestSettings.ThemeName), logger), Is.True);

            // Every setting can be read and written again (the launcher reads several after the check).
            Assert.That(settings.ThemeName, Is.EqualTo("Light"));
            Assert.That(settings.GameDirectory, Is.EqualTo(string.Empty));
            settings.GameDirectory = "C:/Games/Empire Earth";
            Assert.That(userConfig, Does.Not.Exist);
            Assert.That(File.ReadAllText(BackupFile), Is.EqualTo("<configuration><userSettings>"));
            Assert.That(logger.Errors, Has.Count.EqualTo(1));
            Assert.That(logger.Errors[0], Does.Contain(BackupFile));
        }

        [Test]
        public void OlderDamagedCopy_IsReplaced()
        {
            File.WriteAllText(BackupFile, "old copy");
            File.WriteAllText(userConfig, "new damage");

            Assert.That(UserSettingsRecovery.EnsureReadable(new TestSettings(), nameof(TestSettings.ThemeName), logger),
                Is.True);

            Assert.That(File.ReadAllText(BackupFile), Is.EqualTo("new damage"));
            Assert.That(Directory.GetFiles(directory.Path), Is.EqualTo(new[] { BackupFile }));
        }

        [Test]
        public void ErrorWithoutFileName_NothingIsMovedAndFalseIsReturned()
        {
            File.WriteAllText(userConfig, "damaged");
            DamageableSettingsProvider.ReportFileName = false;

            Assert.That(UserSettingsRecovery.EnsureReadable(new TestSettings(), nameof(TestSettings.ThemeName), logger),
                Is.False);

            Assert.That(File.ReadAllText(userConfig), Is.EqualTo("damaged"));
            Assert.That(BackupFile, Does.Not.Exist);
            Assert.That(logger.Errors, Has.Count.EqualTo(1));
        }

        [Test]
        public void FindDamagedFile_UsesTheInnermostFileName()
        {
            var error = new ConfigurationErrorsException("Configuration system failed to initialize",
                new ConfigurationErrorsException("Root element is missing.", userConfig, 3));

            Assert.That(UserSettingsRecovery.FindDamagedFile(error), Is.EqualTo(userConfig));
            Assert.That(UserSettingsRecovery.FindDamagedFile(new ConfigurationErrorsException("no file")), Is.Null);
            Assert.That(UserSettingsRecovery.FindDamagedFile(null), Is.Null);
        }

        [Test]
        public void IsApplicationConfigFile_ComparesFullPathsCaseInsensitively()
        {
            string applicationConfig = directory.Combine("Empire Earth Launcher.exe.config");

            Assert.That(UserSettingsRecovery.IsApplicationConfigFile(applicationConfig.ToUpperInvariant(),
                applicationConfig.ToUpperInvariant()), Is.True);
            Assert.That(UserSettingsRecovery.IsApplicationConfigFile(
                Path.Combine(directory.Path, ".", "Empire Earth Launcher.exe.config"), applicationConfig), Is.True);
            Assert.That(UserSettingsRecovery.IsApplicationConfigFile(userConfig, applicationConfig), Is.False);
            Assert.That(UserSettingsRecovery.IsApplicationConfigFile(userConfig, null), Is.False);
        }

        /// <summary>Settings shaped like the launcher's user settings, backed by the fake provider.</summary>
        [SettingsProvider(typeof(DamageableSettingsProvider))]
        public sealed class TestSettings : ApplicationSettingsBase
        {
            [UserScopedSetting]
            [DefaultSettingValue("Light")]
            public string ThemeName
            {
                get { return (string)this[nameof(ThemeName)]; }
                set { this[nameof(ThemeName)] = value; }
            }

            [UserScopedSetting]
            [DefaultSettingValue("")]
            public string GameDirectory
            {
                get { return (string)this[nameof(GameDirectory)]; }
                set { this[nameof(GameDirectory)] = value; }
            }
        }

        /// <summary>
        /// Fails like LocalFileSettingsProvider with a damaged user.config while <see cref="DamagedFile"/>
        /// exists; otherwise every setting has its default value. Nothing is saved.
        /// </summary>
        public sealed class DamageableSettingsProvider : SettingsProvider
        {
            internal static string DamagedFile;
            internal static bool ReportFileName = true;

            public override string ApplicationName { get; set; }

            public override void Initialize(string name, NameValueCollection config)
            {
                base.Initialize(string.IsNullOrEmpty(name) ? nameof(DamageableSettingsProvider) : name, config);
            }

            public override SettingsPropertyValueCollection GetPropertyValues(SettingsContext context,
                SettingsPropertyCollection collection)
            {
                if (DamagedFile != null && File.Exists(DamagedFile))
                {
                    throw new ConfigurationErrorsException("Configuration system failed to initialize",
                        ReportFileName
                            ? new ConfigurationErrorsException("Root element is missing.", DamagedFile, 1)
                            : new ConfigurationErrorsException("Root element is missing."));
                }

                var values = new SettingsPropertyValueCollection();
                foreach (SettingsProperty property in collection)
                    values.Add(new SettingsPropertyValue(property));
                return values;
            }

            public override void SetPropertyValues(SettingsContext context, SettingsPropertyValueCollection collection)
            {
            }
        }

        private sealed class RecordingLogger : ILogger
        {
            public readonly List<string> Errors = new List<string>();

            public void Log(LogLevel level, string message, Exception exception = null)
            {
                if (level == LogLevel.Error)
                    Errors.Add(message);
            }
        }
    }
}
