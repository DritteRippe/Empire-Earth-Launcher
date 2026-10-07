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
    /// Who touches the foreground (ADR 0010 amendment of 1.1.0): the functions of <c>user32.dll</c> that read or change which
    /// window is in front are imported only in <c>WindowsWindowSystem</c> (the hand-over to a started game, behind
    /// <c>IWindowSystem</c>), in <c>WindowsInstanceChannel</c> (the foreground right for the running launcher) and in
    /// <c>ForegroundWindow</c> (the launcher brings its own window to the front). Nothing else may move another window to the
    /// front or give a process the right to do so. Checked on the sources of the launcher and the core.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.SourceTree)]
    public class ForegroundRulesTests
    {
        private static readonly string[] Allowed =
        {
            "Empire-Earth-Launcher-Core/Platform/WindowsWindowSystem.cs",
            "Empire-Earth-Launcher-Core/Platform/WindowsInstanceChannel.cs",
            "Empire Earth Launcher/ForegroundWindow.cs",
        };

        private static readonly Regex ForegroundImport = new Regex(
            @"\bextern\b[^;]*\b(GetForegroundWindow|SetForegroundWindow\w*|AllowSetForegroundWindow\w*|LockSetForegroundWindow|SetActiveWindow|BringWindowToTop|SwitchToThisWindow|EnumWindows|AttachThreadInput)\s*\(",
            RegexOptions.CultureInvariant);

        private static IEnumerable<string> Sources()
        {
            foreach (string folder in new[] { "Empire Earth Launcher", "Empire-Earth-Launcher-Core" })
            {
                foreach (string file in Directory.EnumerateFiles(RepositoryRoot.GetFullPath(folder), "*.cs", SearchOption.AllDirectories))
                {
                    if (!RepositoryRoot.ToRelativePath(file).Split('/').Any(part => part == "bin" || part == "obj"))
                        yield return file;
                }
            }
        }

        [Test]
        public void TheForegroundFunctionsOfUser32_AreImportedInTheThreeAdaptersOnly()
        {
            var imports = new List<string>();
            foreach (string file in Sources())
            {
                string[] lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!lines[i].TrimStart().StartsWith("//", StringComparison.Ordinal) && ForegroundImport.IsMatch(lines[i]))
                        imports.Add(RepositoryRoot.ToRelativePath(file) + ":" + (i + 1) + ": " + lines[i].Trim());
                }
            }

            Assert.That(imports, Is.Not.Empty, "the check found no import at all");
            Assert.That(imports, Is.All.StartsWith(Allowed[0]).Or.StartsWith(Allowed[1]).Or.StartsWith(Allowed[2]));
            Assert.That(imports.Select(import => import.Substring(0, import.IndexOf(':'))).Distinct(), Is.EquivalentTo(Allowed),
                "every adapter of the list really imports one (else the list is too long)");
        }

        [TestCase("[DllImport(\"user32.dll\")] private static extern bool SetForegroundWindow(IntPtr window);", true)]
        [TestCase("private static extern IntPtr GetForegroundWindow();", true)]
        [TestCase("private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);", true)]
        [TestCase("private static extern bool AllowSetForegroundWindowNative(int processId);", true)]
        [TestCase("private static extern bool IsWindowEnabled(IntPtr window);", false)]
        [TestCase("windows.SetForegroundWindow(window);", false)]
        public void TheCheck_FindsAForegroundImport(string line, bool found)
        {
            Assert.That(ForegroundImport.IsMatch(line), Is.EqualTo(found));
        }
    }
}
