using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Tests.Fakes;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Core.Installations
{
    /// <summary>
    /// The effective game paths of ADR 0016: in a virtualizable game folder the VirtualStore copy of a file comes first,
    /// in every other folder the VirtualStore is never looked at.
    /// </summary>
    [TestFixture]
    public class EffectivePathResolverTests
    {
        private const string VirtualStore = @"C:\Users\Player\AppData\Local\VirtualStore";

        private static readonly string[] Virtualized =
        {
            @"C:\Program Files", @"C:\Program Files (x86)", @"C:\ProgramData", @"C:\Windows"
        };

        private InMemoryFileSystem fileSystem;
        private EffectivePathResolver resolver;

        [SetUp]
        public void SetUp()
        {
            fileSystem = new InMemoryFileSystem();
            fileSystem.AddDrive("D:");
            resolver = new EffectivePathResolver(fileSystem, VirtualStore, Virtualized);
        }

        [TestCase(@"C:\Program Files (x86)\Empire Earth\Empire Earth\_wonlobbypersistent.dat", true)]
        [TestCase(@"c:\program files\Sierra\Empire Earth\x.dat", true)]
        [TestCase(@"C:\ProgramData\EE\x.dat", true)]
        [TestCase(@"C:\Windows\EE\x.dat", true)]
        [TestCase(@"C:\Program Files (x86)", true)]
        [TestCase(@"C:\Games\EE\x.dat", false)]
        [TestCase(@"C:\Program Files (x86)2\EE\x.dat", false)]
        [TestCase(@"C:\Users\Player\AppData\Local\Programs\Empire Earth\Empire Earth\x.dat", false)]
        [TestCase(@"D:\Program Files (x86)\EE\x.dat", false)]
        [TestCase(@"\\server\share\Program Files\x.dat", false)]
        [TestCase(@"Program Files\x.dat", false)]
        public void IsVirtualizable_OnlyBelowTheVirtualizedFolders(string path, bool expected)
        {
            Assert.That(resolver.IsVirtualizable(path), Is.EqualTo(expected));
        }

        [Test]
        public void VirtualStorePath_IsThePathWithoutItsDriveBelowTheVirtualStore()
        {
            Assert.That(resolver.GetVirtualStorePath(@"C:\Program Files (x86)\Empire Earth\Empire Earth\_wonlobbypersistent.dat"),
                Is.EqualTo(VirtualStore + @"\Program Files (x86)\Empire Earth\Empire Earth\_wonlobbypersistent.dat"));
            Assert.That(resolver.GetVirtualStorePath(@"C:\Games\EE\x.dat"), Is.Null);
        }

        [Test]
        public void Resolve_VirtualStoreCopyFirst_WhenTheFolderIsVirtualizable()
        {
            const string gameFile = @"C:\Program Files (x86)\Empire Earth\Empire Earth\_wonlobbypersistent.dat";
            const string copy = VirtualStore + @"\Program Files (x86)\Empire Earth\Empire Earth\_wonlobbypersistent.dat";
            fileSystem.AddFile(gameFile, "original");
            fileSystem.AddFile(copy, "virtual");

            EffectivePath effective = resolver.Resolve(gameFile);

            Assert.That(effective.IsVirtualStoreCopy, Is.True);
            Assert.That(effective.Path, Is.EqualTo(copy));
            Assert.That(effective.GamePath, Is.EqualTo(gameFile));
            Assert.That(effective.VirtualStorePath, Is.EqualTo(copy));
        }

        [Test]
        public void Resolve_GameFile_WithoutACopy()
        {
            const string gameFile = @"C:\Program Files (x86)\Empire Earth\Empire Earth\_wonlobbypersistent.dat";
            fileSystem.AddFile(gameFile, "original");

            EffectivePath effective = resolver.Resolve(gameFile);

            Assert.That(effective.IsVirtualStoreCopy, Is.False);
            Assert.That(effective.Path, Is.EqualTo(gameFile));
            Assert.That(effective.VirtualStorePath, Is.Not.Null);
        }

        [Test]
        public void Resolve_NeverUsesTheVirtualStore_ForAFolderThatIsNotVirtualizable()
        {
            const string gameFile = @"C:\Games\EE\_wonlobbypersistent.dat";
            fileSystem.AddFile(VirtualStore + @"\Games\EE\_wonlobbypersistent.dat", "stale copy");

            EffectivePath effective = resolver.Resolve(gameFile);

            Assert.That(effective.IsVirtualStoreCopy, Is.False);
            Assert.That(effective.Path, Is.EqualTo(gameFile));
            Assert.That(effective.VirtualStorePath, Is.Null);
            Assert.That(fileSystem.TotalOpenCount, Is.EqualTo(0));
        }

        [Test]
        public void Resolve_ACopyIsUsedEvenIfTheGameFileDoesNotExist()
        {
            // The game created the file while it was virtualized: only the copy exists.
            const string copy = VirtualStore + @"\Program Files\Sierra\Empire Earth\_wonlobbypersistent.dat";
            fileSystem.AddFile(copy, "virtual");

            Assert.That(resolver.Resolve(@"C:\Program Files\Sierra\Empire Earth\_wonlobbypersistent.dat").Path, Is.EqualTo(copy));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(@"VirtualStore")]
        public void WithoutAVirtualStoreFolder_NothingIsVirtualizable(string virtualStore)
        {
            var withoutStore = new EffectivePathResolver(fileSystem, virtualStore, Virtualized);

            Assert.That(withoutStore.IsVirtualizable(@"C:\Program Files\x.dat"), Is.False);
            Assert.That(withoutStore.Resolve(@"C:\Program Files\x.dat").Path, Is.EqualTo(@"C:\Program Files\x.dat"));
        }

        [Test]
        public void VirtualizedFolders_AreNormalizedWithoutDuplicatesOrInvalidEntries()
        {
            // On 32-bit Windows "Program Files (x86)" is "Program Files"; an empty or relative entry is ignored.
            var duplicated = new EffectivePathResolver(fileSystem, VirtualStore,
                new[] { @"C:\Program Files\", @"c:\program files", "", null, "Windows", @"\\server\share" });

            Assert.That(duplicated.VirtualizedDirectories, Is.EqualTo(new[] { @"C:\Program Files" }));
        }

        [Test]
        public void Arguments_AreChecked()
        {
            Assert.That(() => new EffectivePathResolver(null, VirtualStore, Virtualized), Throws.ArgumentNullException);
            Assert.That(() => new EffectivePathResolver(fileSystem, VirtualStore, null), Throws.ArgumentNullException);
            Assert.That(() => resolver.Resolve(null), Throws.ArgumentNullException);
            Assert.That(() => resolver.IsVirtualizable(null), Throws.ArgumentNullException);
        }
    }
}
