using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Empire_Earth_Launcher.Tests.TestSupport
{
    /// <summary>A classic MSBuild project file of the source tree, read for the architecture tests.</summary>
    internal sealed class ProjectFile
    {
        /// <summary>XML namespace of classic MSBuild project files.</summary>
        public static readonly XNamespace MSBuild = "http://schemas.microsoft.com/developer/msbuild/2003";

        private readonly XDocument xml;

        public ProjectFile(string fullPath)
        {
            xml = XDocument.Load(fullPath);
            FullPath = fullPath;
            Directory = Path.GetDirectoryName(fullPath);
        }

        /// <summary>Loads a project by its path relative to the repository root (as written in the solution).</summary>
        public static ProjectFile Load(string solutionRelativePath)
        {
            return new ProjectFile(RepositoryRoot.GetFullPath(solutionRelativePath));
        }

        /// <summary>Full path of the project file.</summary>
        public string FullPath { get; }

        /// <summary>Folder of the project file.</summary>
        public string Directory { get; }

        /// <summary>true for console and Windows programs.</summary>
        public bool IsExecutable
        {
            get
            {
                string outputType = Values("OutputType").Single();
                return outputType == "Exe" || outputType == "WinExe";
            }
        }

        /// <summary>Every value of a property or item metadata, in every property group or item.</summary>
        public IEnumerable<string> Values(string name)
        {
            return xml.Descendants(MSBuild + name).Select(e => e.Value.Trim()).ToList();
        }

        /// <summary>The Include attribute of every item of a type.</summary>
        public IEnumerable<string> Items(string itemType)
        {
            return xml.Descendants(MSBuild + itemType).Select(e => (string)e.Attribute("Include")).ToList();
        }

        /// <summary>Package id -> version from the packages.config next to the project (empty if none).</summary>
        public IDictionary<string, string> Packages()
        {
            string packagesConfig = Path.Combine(Directory, "packages.config");
            if (!File.Exists(packagesConfig))
                return new Dictionary<string, string>();
            return XDocument.Load(packagesConfig).Root.Elements("package")
                            .ToDictionary(p => (string)p.Attribute("id"), p => (string)p.Attribute("version"),
                                          StringComparer.OrdinalIgnoreCase);
        }
    }
}
