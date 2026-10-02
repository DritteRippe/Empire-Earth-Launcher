using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Empire_Earth_Launcher.Tests.TestSupport;
using NUnit.Framework;

namespace Empire_Earth_Launcher.Tests.Architecture
{
    /// <summary>
    /// How the launcher starts programs (ADR 0010, contract 3.7 and 4.1, R3): only <c>ShellProcessStarter</c> calls
    /// <c>Process.Start</c>, always through the shell and never with the verb "runas"; no source of the launcher or its core
    /// ends a process. Checked on the sources of the launcher and the core (the mod creator is a program of its own).
    /// </summary>
    [TestFixture]
    [Category(TestCategories.SourceTree)]
    public class ProcessRulesTests
    {
        private const string ShellStarter = "Empire-Earth-Launcher-Core/Platform/ShellProcessStarter.cs";

        private static readonly Regex ProcessStart = new Regex(@"\bProcess\s*\.\s*Start\s*\(", RegexOptions.CultureInvariant);
        private static readonly Regex WithoutShell = new Regex(@"\bUseShellExecute\s*=\s*false\b", RegexOptions.CultureInvariant);
        private static readonly Regex RunAs = new Regex(@"""runas""", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex EndsAProcess = new Regex(@"\.\s*(Kill|CloseMainWindow)\s*\(|\bTerminateProcess\b|\btaskkill\b",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        /// <summary>The code lines (comments skipped) of every source file of the launcher and the core, as (file, line, text).</summary>
        private static IEnumerable<Tuple<string, int, string>> CodeLines()
        {
            foreach (string folder in new[] { "Empire Earth Launcher", "Empire-Earth-Launcher-Core" })
            {
                foreach (string file in Directory.EnumerateFiles(RepositoryRoot.GetFullPath(folder), "*.cs", SearchOption.AllDirectories))
                {
                    string relative = RepositoryRoot.ToRelativePath(file);
                    if (relative.Split('/').Any(part => part == "bin" || part == "obj"))
                        continue;
                    string[] lines = File.ReadAllLines(file);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i].Trim();
                        if (!line.StartsWith("//", StringComparison.Ordinal))
                            yield return Tuple.Create(relative, i + 1, line);
                    }
                }
            }
        }

        private static List<string> Matching(Regex pattern)
        {
            return CodeLines().Where(line => pattern.IsMatch(line.Item3))
                              .Select(line => line.Item1 + ":" + line.Item2 + ": " + line.Item3)
                              .ToList();
        }

        [Test]
        public void OnlyTheShellStarter_StartsProcesses()
        {
            List<string> starts = Matching(ProcessStart);

            Assert.That(starts, Is.Not.Empty, "the check found no start at all");
            Assert.That(starts, Is.All.StartsWith(ShellStarter + ":"));
        }

        [Test]
        public void Contract_3_7_NoStartWithoutTheShell()
        {
            Assert.That(Matching(WithoutShell), Is.Empty, "a plain CreateProcess ignores the compatibility layers (error 740)");
        }

        [Test]
        public void Contract_4_1_TheLauncherNeverAsksForElevation()
        {
            Assert.That(Matching(RunAs), Is.Empty);
        }

        [Test]
        public void Adr0010_TheLauncherNeverEndsAProcess()
        {
            Assert.That(Matching(EndsAProcess), Is.Empty, "the launcher explains and points to the Task Manager");
        }

        [TestCase("process.Kill();")]
        [TestCase("game.CloseMainWindow();")]
        [TestCase("Process.Start(\"taskkill\", \"/IM \\\"Empire Earth.exe\\\"\");")]
        public void TheCheck_FindsEndingAProcess(string line)
        {
            Assert.That(EndsAProcess.IsMatch(line), Is.True);
        }

        [TestCase("new ProcessStartInfo { UseShellExecute = false }", true)]
        [TestCase("info.Verb = \"RunAs\";", false)]
        public void TheCheck_FindsAStartWithoutShellOrElevated(string line, bool withoutShell)
        {
            Assert.That(withoutShell ? WithoutShell.IsMatch(line) : RunAs.IsMatch(line), Is.True);
        }
    }
}
