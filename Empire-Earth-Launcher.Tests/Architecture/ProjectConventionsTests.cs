using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Architecture
{
    /// <summary>
    /// Build settings that every project of the solution shares (ADR 0001, ADR 0002, ADR 0011, ADR 0012).
    /// </summary>
    /// <remarks>
    /// The projects are classic project files without a shared <c>Directory.Build.props</c> (xbuild does not
    /// import it), so the shared settings are repeated in every project and these tests keep them equal. They
    /// read the source tree (<see cref="RepositoryRoot"/>): the solution, every project file, its
    /// <c>packages.config</c>, <c>App.config</c> and application manifest, and the CI workflow.
    /// </remarks>
    [TestFixture]
    [Category(TestCategories.SourceTree)]
    public class ProjectConventionsTests
    {
        private const string TargetFramework = ".NETFramework,Version=v4.8";
        private const string LauncherProject = @"Empire Earth Launcher\Empire Earth Launcher.csproj";
        private const string TestProject = @"Empire-Earth-Launcher.Tests\Empire-Earth-Launcher.Tests.csproj";
        private const string WorkflowFile = ".github/workflows/build.yml";

        /// <summary>A step of the workflow that runs an action: <c>uses: ...</c> or <c>- uses: ...</c>.</summary>
        private static readonly Regex ActionStep = new Regex(@"^(?:-\s+)?uses:", RegexOptions.CultureInvariant);

        /// <summary>
        /// An action pinned to the full commit SHA of a release, with its version as a comment, the form Dependabot keeps up to
        /// date: <c>uses: owner/repo@&lt;40 hexadecimal digits&gt; # v1.2.3</c>.
        /// </summary>
        private static readonly Regex PinnedAction = new Regex(
            @"^(?:-\s+)?uses:\s+[\w.-]+/[\w.-]+(?:/[\w.-]+)*@[0-9a-f]{40} # v\d+(?:\.\d+){0,2}$", RegexOptions.CultureInvariant);

        private static readonly XNamespace AsmV3 = "urn:schemas-microsoft-com:asm.v3";
        private static readonly XNamespace Compatibility = "urn:schemas-microsoft-com:compatibility.v1";

        /// <summary>
        /// The lib folder each NuGet package is referenced from: the newest .NET Framework build of the package
        /// that a .NET Framework 4.8 project can use. A new package needs an entry here (and in
        /// THIRD-PARTY-NOTICES.md).
        /// </summary>
        private static readonly IDictionary<string, string> LibFolderByPackage =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Krypton.Toolkit", "net48" },
                { "NUnit", "net45" },
                { "NUnitLite", "net45" },
            };

        /// <summary>supportedOS ids of the launcher manifest: Windows 7, 8, 8.1 and 10 (also used by 11).</summary>
        private static readonly string[] LauncherSupportedOs =
        {
            "{35138b9a-5d96-4fbd-8e2d-a2440225f93a}",
            "{4a2f28e3-53b9-4441-ba9c-d69d4a4a6e38}",
            "{1f676c76-80e1-4239-95bb-83d0f6d0da78}",
            "{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}",
        };

        private static readonly Regex SolutionProjectLine = new Regex(
            @"^Project\(""\{[0-9A-Fa-f-]+\}""\)\s*=\s*""[^""]*"",\s*""(?<path>[^""]+\.csproj)""",
            RegexOptions.Multiline | RegexOptions.CultureInvariant);

        private static readonly Regex PackageHintPath = new Regex(
            @"^(?:\.\.\\)+packages\\(?<folder>[^\\]+)\\lib\\(?<lib>[^\\]+)\\[^\\]+\.dll$",
            RegexOptions.CultureInvariant);

        /// <summary>The C# projects of the solution, as written in it (relative, with <c>\</c>).</summary>
        public static IEnumerable<string> SolutionProjects()
        {
            string solution = File.ReadAllText(RepositoryRoot.SolutionFile);
            return SolutionProjectLine.Matches(solution).Cast<Match>().Select(m => m.Groups["path"].Value).ToList();
        }

        [Test]
        public void Solution_ContainsEveryProjectOfTheRepository()
        {
            var inSolution = SolutionProjects().Select(p => RepositoryRoot.ToRelativePath(RepositoryRoot.GetFullPath(p)));
            var onDisk = Directory.EnumerateFiles(RepositoryRoot.Path, "*.csproj", SearchOption.AllDirectories)
                                  .Select(RepositoryRoot.ToRelativePath)
                                  .Where(p => !p.Split('/').Any(IsBuildOrPackageFolder));

            Assert.That(inSolution, Is.EquivalentTo(onDisk), "every project must be in " + RepositoryRoot.SolutionFileName +
                                                             ", so that these tests, the verify script and CI see it");
            Assert.That(SolutionProjects(), Is.SupersetOf(new[] { LauncherProject, TestProject }));
        }

        [TestCaseSource(nameof(SolutionProjects))]
        public void Project_TargetsDotNetFramework48(string project)
        {
            Assert.That(Load(project).Values("TargetFrameworkVersion"), Is.Not.Empty.And.All.EqualTo("v4.8"));
        }

        [TestCaseSource(nameof(SolutionProjects))]
        public void Project_PinsCSharp8(string project)
        {
            Assert.That(Load(project).Values("LangVersion"), Is.Not.Empty.And.All.EqualTo("8.0"));
        }

        [TestCaseSource(nameof(SolutionProjects))]
        public void Project_IsDeterministic(string project)
        {
            Assert.That(Load(project).Values("Deterministic"), Is.Not.Empty.And.All.EqualTo("true"));
        }

        [TestCaseSource(nameof(SolutionProjects))]
        public void Project_IsAnyCpu_AndExecutablesDoNotPrefer32Bit(string project)
        {
            ProjectFile file = Load(project);

            Assert.That(file.Values("PlatformTarget"), Is.All.EqualTo("AnyCPU"));
            if (file.IsExecutable)
                Assert.That(file.Values("Prefer32Bit"), Is.Not.Empty.And.All.EqualTo("false"),
                    "executables run as 64-bit processes on 64-bit Windows; registry views are always named explicitly");
            else
                Assert.That(file.Values("Prefer32Bit"), Is.Empty, "Prefer32Bit has no effect on libraries");
        }

        [TestCaseSource(nameof(SolutionProjects))]
        public void Project_ReferencesEachPackageFromItsLibFolderForNet48(string project)
        {
            ProjectFile file = Load(project);
            IDictionary<string, string> versions = file.Packages();

            var referencedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string hintPath in file.Values("HintPath"))
            {
                Match match = PackageHintPath.Match(hintPath);
                Assert.That(match.Success, Is.True, "HintPath outside packages\\<id>.<version>\\lib\\<framework>\\: " + hintPath);

                string folder = match.Groups["folder"].Value;
                string package = versions.Keys.SingleOrDefault(id =>
                    string.Equals(folder, id + "." + versions[id], StringComparison.OrdinalIgnoreCase));
                Assert.That(package, Is.Not.Null, hintPath + " does not match a package and version of packages.config");
                Assert.That(LibFolderByPackage.ContainsKey(package), Is.True,
                    "no lib folder is defined for package " + package + " (LibFolderByPackage)");
                Assert.That(match.Groups["lib"].Value, Is.EqualTo(LibFolderByPackage[package]), hintPath);
                Assert.That(File.Exists(RepositoryRoot.CombineRelative(file.Directory, hintPath)), Is.True,
                    hintPath + " does not exist; are the NuGet packages restored?");
                referencedPackages.Add(package);
            }

            Assert.That(referencedPackages, Is.EquivalentTo(versions.Keys), "every package of packages.config is referenced");
        }

        [TestCaseSource(nameof(SolutionProjects))]
        public void PackagesConfig_TargetsNet48(string project)
        {
            string packagesConfig = Path.Combine(Load(project).Directory, "packages.config");
            if (!File.Exists(packagesConfig))
                Assert.Pass("The project uses no NuGet package.");

            var frameworks = XDocument.Load(packagesConfig).Root.Elements("package")
                                      .Select(p => (string)p.Attribute("targetFramework"));
            Assert.That(frameworks, Is.Not.Empty.And.All.EqualTo("net48"));
        }

        [TestCaseSource(nameof(SolutionProjects))]
        public void AppConfig_RequiresDotNetFramework48(string project)
        {
            ProjectFile file = Load(project);
            string appConfig = Path.Combine(file.Directory, "App.config");
            if (!File.Exists(appConfig))
            {
                Assert.That(file.IsExecutable, Is.False, "executables need an App.config with supportedRuntime");
                Assert.Pass("A library without App.config.");
            }

            var runtimes = XDocument.Load(appConfig).Root.Elements("startup").Elements("supportedRuntime").ToList();
            if (file.IsExecutable)
                Assert.That(runtimes, Has.Count.EqualTo(1), "executables declare exactly one supported runtime");
            foreach (XElement runtime in runtimes)
            {
                Assert.That((string)runtime.Attribute("version"), Is.EqualTo("v4.0"));
                Assert.That((string)runtime.Attribute("sku"), Is.EqualTo(TargetFramework));
            }
        }

        [TestCaseSource(nameof(SolutionProjects))]
        public void Manifest_RequestsNoElevation(string project)
        {
            ProjectFile file = Load(project);
            string manifestName = file.Values("ApplicationManifest").SingleOrDefault();
            if (manifestName == null)
            {
                Assert.That(project, Is.Not.EqualTo(LauncherProject), "the launcher has an application manifest");
                Assert.Pass("No application manifest (the compiler embeds its default manifest, asInvoker).");
            }

            XDocument manifest = XDocument.Load(RepositoryRoot.CombineRelative(file.Directory, manifestName));
            XElement level = manifest.Descendants(AsmV3 + "requestedExecutionLevel").Single();
            Assert.That((string)level.Attribute("level"), Is.EqualTo("asInvoker"));
            Assert.That((string)level.Attribute("uiAccess"), Is.EqualTo("false"));
        }

        [Test]
        public void LauncherManifest_SupportsWindows7To11_AndIsNotDpiAware()
        {
            ProjectFile launcher = Load(LauncherProject);
            Assert.That(launcher.Values("ApplicationManifest"), Is.EqualTo(new[] { "app.manifest" }));

            XDocument manifest = XDocument.Load(Path.Combine(launcher.Directory, "app.manifest"));
            var supportedOs = manifest.Descendants(Compatibility + "supportedOS")
                                      .Select(e => ((string)e.Attribute("Id")).ToLowerInvariant());
            Assert.That(supportedOs, Is.EquivalentTo(LauncherSupportedOs));
            Assert.That(manifest.Descendants().Select(e => e.Name.LocalName),
                Has.None.EqualTo("dpiAware").And.None.EqualTo("dpiAwareness"),
                "the launcher UI stays DPI-unaware (ADR 0011)");
        }

        /// <summary>
        /// The assemblies of this test run were really compiled for .NET Framework 4.8: the test assembly and
        /// every project it references (the mod creator is not referenced; its project file is checked above).
        /// </summary>
        [Test]
        public void BuiltAssemblies_TargetDotNetFramework48()
        {
            var assemblies = new List<Assembly> { typeof(ProjectConventionsTests).Assembly };
            ProjectFile tests = Load(TestProject);
            foreach (string reference in tests.Items("ProjectReference"))
            {
                var referenced = new ProjectFile(RepositoryRoot.CombineRelative(tests.Directory, reference));
                assemblies.Add(Assembly.Load(new AssemblyName { Name = referenced.Values("AssemblyName").Single() }));
            }

            Assert.That(assemblies, Has.Count.GreaterThan(1));
            foreach (Assembly assembly in assemblies)
            {
                var attribute = assembly.GetCustomAttribute<TargetFrameworkAttribute>();
                Assert.That(attribute, Is.Not.Null, assembly.FullName);
                Assert.That(attribute.FrameworkName, Is.EqualTo(TargetFramework), assembly.FullName);
            }
        }

        [Test]
        public void Workflow_BuildsAgainstTheNet48ReferenceAssemblies()
        {
            string workflow = File.ReadAllText(RepositoryRoot.GetFullPath(WorkflowFile));

            Assert.That(workflow, Does.Contain("REFASM_PACKAGE: Microsoft.NETFramework.ReferenceAssemblies.net48"));
            Assert.That(workflow, Does.Contain(@"/p:FrameworkPathOverride=$env:REFASM_ROOT\.NETFramework\v4.8"));
            Assert.That(workflow, Does.Not.Contain("ReferenceAssemblies.net40").And.Not.Contain(@".NETFramework\v4.0"));
        }

        /// <summary>
        /// Every action of the workflow is pinned to the commit of a release (review after the release of 1.1.0): a tag can be moved
        /// to other code, a commit cannot, and the job builds the test builds that are offered for download.
        /// </summary>
        [Test]
        public void Workflow_PinsEveryActionToTheCommitOfARelease()
        {
            List<string> steps = File.ReadAllLines(RepositoryRoot.GetFullPath(WorkflowFile))
                                     .Select(line => line.Trim())
                                     .Where(line => ActionStep.IsMatch(line))
                                     .ToList();

            Assert.That(steps, Is.Not.Empty, "the workflow runs actions");
            Assert.That(steps.Where(step => !PinnedAction.IsMatch(step)), Is.Empty,
                "pin with the full commit SHA and the version as a comment: uses: owner/repo@<SHA> # vX.Y.Z");
        }

        [TestCase("uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1", true)]
        [TestCase("- uses: NuGet/setup-nuget@fd55a6f3b34392fa83fde1454582407d8c714123 # v4.0", true)]
        [TestCase("uses: github/codeql-action/init@3d3c42e5aac5ba805825da76410c181273ba90b1 # v4", true)]
        [TestCase("uses: actions/checkout@v4", false)]
        [TestCase("uses: actions/checkout@main", false)]
        [TestCase("uses: actions/checkout@3d3c42e # v7.0.1", false)]
        [TestCase("uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1", false)]
        [TestCase("uses: actions/checkout@3D3C42E5AAC5BA805825DA76410C181273BA90B1 # v7.0.1", false)]
        public void Workflow_PinRule_TellsACommitFromAMovableReference(string step, bool pinned)
        {
            Assert.That(ActionStep.IsMatch(step), Is.True);
            Assert.That(PinnedAction.IsMatch(step), Is.EqualTo(pinned));
        }

        private static bool IsBuildOrPackageFolder(string folderName)
        {
            return folderName == "bin" || folderName == "obj" || folderName == "packages" || folderName.StartsWith(".", StringComparison.Ordinal);
        }

        private static ProjectFile Load(string solutionRelativePath)
        {
            return ProjectFile.Load(solutionRelativePath);
        }
    }
}
