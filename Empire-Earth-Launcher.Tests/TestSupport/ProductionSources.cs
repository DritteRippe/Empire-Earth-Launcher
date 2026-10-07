using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Empire_Earth_Launcher.Tests.TestSupport
{
    /// <summary>One line of code of a source file, for the architecture tests that read the sources.</summary>
    internal sealed class SourceLine
    {
        public SourceLine(string file, int number, string text)
        {
            File = file;
            Number = number;
            Text = text;
        }

        /// <summary>The file, relative to the repository root, with <c>/</c> as separator.</summary>
        public string File { get; }

        /// <summary>The line number, from 1.</summary>
        public int Number { get; }

        /// <summary>The line without surrounding white space.</summary>
        public string Text { get; }

        public override string ToString()
        {
            return File + ":" + Number + ": " + Text;
        }
    }

    /// <summary>
    /// The C# sources of the programs and libraries of the repository: every <c>.cs</c> file outside the test project, the
    /// NuGet packages and the build folders (launcher, core, WON library, mod library and mod creator). Only for tests of
    /// the category <see cref="TestCategories.SourceTree"/>.
    /// </summary>
    internal static class ProductionSources
    {
        private const string TestProjectFolder = "Empire-Earth-Launcher.Tests";

        /// <summary>The files, relative to the repository root, sorted.</summary>
        public static IReadOnlyList<string> Files()
        {
            return Directory.EnumerateFiles(RepositoryRoot.Path, "*.cs", SearchOption.AllDirectories)
                            .Select(RepositoryRoot.ToRelativePath)
                            .Where(file =>
                            {
                                string[] parts = file.Split('/');
                                return parts[0] != TestProjectFolder && parts[0] != "packages" &&
                                       !parts.Any(part => part == "bin" || part == "obj");
                            })
                            .OrderBy(file => file, StringComparer.Ordinal)
                            .ToList();
        }

        /// <summary>Every line of code of <see cref="Files"/>; lines that start with <c>//</c> (comments, documentation) are skipped.</summary>
        public static IEnumerable<SourceLine> CodeLines()
        {
            foreach (string file in Files())
            {
                string[] lines = System.IO.File.ReadAllLines(RepositoryRoot.GetFullPath(file));
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (!line.StartsWith("//", StringComparison.Ordinal))
                        yield return new SourceLine(file, i + 1, line);
                }
            }
        }
    }
}
