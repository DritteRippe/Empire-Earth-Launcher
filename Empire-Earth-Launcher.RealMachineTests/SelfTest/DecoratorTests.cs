using System;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.RealMachineTests.Harness;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.RealMachineTests.SelfTest
{
    /// <summary>
    /// The wrappers of the real adapters: the read-only ones refuse every change, the recording registry records every change
    /// and refuses a deleted tree, the work folder file system changes nothing outside its folder; every refusal is recorded.
    /// </summary>
    [TestFixture]
    [Category(RealMachineCategories.SelfTest)]
    public class DecoratorTests
    {
        private static readonly RegistryLocation Key = RegistryLocation.CurrentUser(@"Software\SSSI\Empire Earth");

        private InMemoryRegistry registry;
        private InMemoryFileSystem fileSystem;
        private HarnessViolations violations;

        [SetUp]
        public void SetUp()
        {
            registry = new InMemoryRegistry();
            registry.Seed(Key, "Music Volume", RegistryValue.FromDWord(44));
            fileSystem = new InMemoryFileSystem();
            fileSystem.AddDrive("D:");
            fileSystem.AddFile(@"C:\Games\EE\Empire Earth.exe", "exe");
            violations = new HarnessViolations();
        }

        [Test]
        public void ReadOnlyRegistry_ReadsAndRefusesEveryChange()
        {
            var readOnly = new ReadOnlyRegistry(registry, violations);

            Assert.That(readOnly.GetValue(Key, "Music Volume").Value, Is.EqualTo(RegistryValue.FromDWord(44)));
            Assert.That(readOnly.GetValueNames(Key).Value, Is.EqualTo(new[] { "Music Volume" }));
            Assert.Throws<HarnessViolationException>(() => readOnly.SetValue(Key, "Music Volume", RegistryValue.FromDWord(1)));
            Assert.Throws<HarnessViolationException>(() => readOnly.DeleteValue(Key, "Music Volume"));
            Assert.Throws<HarnessViolationException>(() => readOnly.CreateSubKey(Key.Child("X")));
            Assert.Throws<HarnessViolationException>(() => readOnly.DeleteSubKeyTree(Key));

            Assert.That(violations.Items, Has.Count.EqualTo(4).And.All.EndsWith("by read-only code"));
            Assert.That(registry.Changes, Is.Empty);
        }

        [Test]
        public void ReadOnlyFileSystem_ReadsAndRefusesEveryChange()
        {
            var readOnly = new ReadOnlyFileSystem(fileSystem, violations);
            const string program = @"C:\Games\EE\Empire Earth.exe";

            Assert.That(readOnly.FileExists(program), Is.True);
            Assert.That(readOnly.ReadAllBytes(program, 100).IsOk, Is.True);
            Assert.Throws<HarnessViolationException>(() => readOnly.WriteAllBytes(program, new byte[0]));
            Assert.Throws<HarnessViolationException>(() => readOnly.DeleteFile(program));
            Assert.Throws<HarnessViolationException>(() => readOnly.Move(program, program + ".bak"));
            Assert.Throws<HarnessViolationException>(() => readOnly.Replace(program + ".new", program));
            Assert.Throws<HarnessViolationException>(() => readOnly.CreateDirectory(@"C:\Games\EE\New"));

            Assert.That(violations.Items, Has.Count.EqualTo(5));
            Assert.That(fileSystem.GetText(program), Is.EqualTo("exe"));
        }

        [Test]
        public void RecordingRegistry_RecordsEveryChange_AndRefusesADeletedTree()
        {
            var recording = new RecordingRegistry(registry, violations);

            recording.CreateSubKey(Key.Child("Game Options"));
            recording.SetValue(Key, "Sound Volume", RegistryValue.FromDWord(60));
            recording.DeleteValue(Key, "Music Volume");
            Assert.Throws<HarnessViolationException>(() => recording.DeleteSubKeyTree(Key));

            Assert.That(recording.Writes, Has.Count.EqualTo(3));
            Assert.That(recording.Writes[1].ToString(), Is.EqualTo(@"SetValue HKCU\Software\SSSI\Empire Earth @""Sound Volume"""));
            Assert.That(recording.Writes[0].ValueName, Is.Null);
            Assert.That(registry.GetValue(Key, "Sound Volume").IsOk, Is.True, "the changes reach the registry");
            Assert.That(registry.ProbeKey(Key).IsOk, Is.True, "the tree was not deleted");
            Assert.That(violations.Items, Has.Count.EqualTo(1));
        }

        [Test]
        public void WorkFolderFileSystem_ChangesOnlyBelowTheWorkFolder()
        {
            var work = new WorkFolderFileSystem(fileSystem, @"D:\e2e\A\installed\", violations);

            Assert.That(work.WorkFolder, Is.EqualTo(@"D:\e2e\A\installed"));
            Assert.That(work.CreateDirectory(@"D:\e2e\A\installed\Backups").IsOk, Is.True);
            Assert.That(work.WriteAllBytes(@"D:\e2e\A\installed\Backups\a.reg", new byte[] { 1 }).IsOk, Is.True);
            Assert.That(work.WriteAllBytesAtomically(@"D:\e2e\A\installed\values.json", new byte[] { 2 }).IsOk, Is.True);
            Assert.That(work.IsInWorkFolder(@"d:\E2E\a\INSTALLED"), Is.True);
            Assert.That(work.IsInWorkFolder(@"D:\e2e\A\installed2\x"), Is.False);

            Assert.Throws<HarnessViolationException>(() => work.WriteAllBytes(@"D:\e2e\A\installed2\x", new byte[0]));
            Assert.Throws<HarnessViolationException>(() => work.Move(@"D:\e2e\A\installed\Backups\a.reg", @"C:\Games\EE\a.reg"));
            Assert.Throws<HarnessViolationException>(() => work.Replace(@"C:\Games\EE\Empire Earth.exe", @"D:\e2e\A\installed\x"));
            Assert.Throws<HarnessViolationException>(() => work.DeleteFile(@"C:\Games\EE\Empire Earth.exe"));
            Assert.Throws<HarnessViolationException>(() => work.CreateDirectory(@"D:\e2e\A\installed\..\other"));

            Assert.That(violations.Items, Has.Count.EqualTo(5).And.All.Contain(@"outside the work folder D:\e2e\A\installed"));
            Assert.That(fileSystem.FileExists(@"C:\Games\EE\Empire Earth.exe"), Is.True);
            Assert.Throws<ArgumentException>(() => new WorkFolderFileSystem(fileSystem, @"e2e\work", violations));
        }
    }
}
