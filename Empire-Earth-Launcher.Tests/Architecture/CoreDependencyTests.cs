using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Architecture
{
    /// <summary>
    /// The core library is UI-free and depends only on the BCL and the WON library (ADR 0003, ARCHITECTURE 3).
    /// Checked twice: on the compiled assembly (what it really references) and on the project file and the
    /// sources (so that an unused reference or a <c>using</c> of a UI namespace is caught as well).
    /// </summary>
    [TestFixture]
    public class CoreDependencyTests
    {
        private const string CoreProject = @"Empire-Earth-Launcher-Core\Empire-Earth-Launcher-Core.csproj";
        private const string WonProject = @"..\Empire-Earth-WON\Empire-Earth-WON.csproj";

        /// <summary>Assemblies of the UI and of other parts of the solution the core must never reference.</summary>
        private static readonly string[] ForbiddenAssemblies =
        {
            "System.Windows.Forms", "System.Windows.Forms.DataVisualization", "System.Drawing", "System.Design",
            "Accessibility", "PresentationCore", "PresentationFramework", "WindowsBase", "Krypton.Toolkit",
            "Empire Earth Launcher", "Empire_Earth_Mod_Lib", "Empire_Earth_Mod",
        };

        /// <summary>
        /// Everything the core may reference (ARCHITECTURE 3): the BCL parts it needs and the WON library. A new
        /// entry needs a reason in ARCHITECTURE.md.
        /// </summary>
        private static readonly string[] AllowedAssemblies =
        {
            "mscorlib", "System", "System.Core", "System.Runtime.Serialization", "System.Xml", "System.Net.Http",
            "System.IO.Compression", "Empire_Earth_WON",
        };

        private static readonly Regex UiNamespaceUsage = new Regex(
            @"\b(System\.Windows\.Forms|System\.Drawing|Krypton\.Toolkit|System\.Windows\.(Controls|Media))\b",
            RegexOptions.CultureInvariant);

        private static Assembly CoreAssembly
        {
            get { return typeof(ContractNames).Assembly; }
        }

        [Test]
        public void CoreAssembly_HasTheExpectedName()
        {
            Assert.That(CoreAssembly.GetName().Name, Is.EqualTo("Empire_Earth_Launcher_Core"));
        }

        [Test]
        public void CoreAssembly_ReferencesNoUiAssembly()
        {
            var referenced = CoreAssembly.GetReferencedAssemblies().Select(a => a.Name);

            Assert.That(referenced.Intersect(ForbiddenAssemblies, StringComparer.OrdinalIgnoreCase), Is.Empty,
                "the core must stay UI-free (ADR 0003)");
        }

        [Test]
        public void CoreAssembly_ReferencesOnlyTheBclAndTheWonLibrary()
        {
            var referenced = CoreAssembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

            Assert.That(referenced, Is.SubsetOf(AllowedAssemblies));
        }

        [Test]
        public void CoreProject_IsALibraryWithTheDocumentedNames()
        {
            ProjectFile core = ProjectFile.Load(CoreProject);

            Assert.That(core.Values("OutputType"), Is.EqualTo(new[] { "Library" }));
            Assert.That(core.Values("AssemblyName"), Is.EqualTo(new[] { "Empire_Earth_Launcher_Core" }));
            Assert.That(core.Values("RootNamespace"), Is.EqualTo(new[] { "Empire_Earth_Launcher.Core" }));
        }

        [Test]
        public void CoreProject_ReferencesOnlyTheBclAndTheWonProject()
        {
            ProjectFile core = ProjectFile.Load(CoreProject);

            // "System.Xml, Version=..." -> "System.Xml"
            var references = core.Items("Reference").Select(r => r.Split(',')[0].Trim()).ToList();
            Assert.That(references, Is.SubsetOf(AllowedAssemblies));
            Assert.That(core.Items("ProjectReference"), Is.EqualTo(new[] { WonProject }).Or.Empty,
                "the core may reference the WON library and nothing else of the solution");
            Assert.That(core.Values("HintPath"), Is.Empty, "the core uses no NuGet package");
            Assert.That(core.Packages(), Is.Empty, "the core uses no NuGet package");
        }

        [Test]
        public void CoreSources_UseNoUiNamespace()
        {
            ProjectFile core = ProjectFile.Load(CoreProject);
            var offenders = new List<string>();
            foreach (string source in Directory.EnumerateFiles(core.Directory, "*.cs", SearchOption.AllDirectories))
            {
                string relative = RepositoryRoot.ToRelativePath(source);
                if (relative.Split('/').Any(folder => folder == "bin" || folder == "obj"))
                    continue;
                string[] lines = File.ReadAllLines(source);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (UiNamespaceUsage.IsMatch(lines[i]) && !lines[i].TrimStart().StartsWith("//", StringComparison.Ordinal))
                        offenders.Add(relative + ":" + (i + 1) + ": " + lines[i].Trim());
                }
            }

            Assert.That(offenders, Is.Empty, "the core must not use UI namespaces (ADR 0003)");
        }
    }
}
