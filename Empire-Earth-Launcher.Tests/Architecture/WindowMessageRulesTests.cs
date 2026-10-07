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
    /// What the launcher may do to a window of another program (ADR 0010 amendment of 1.1.0, A1b): the functions of
    /// <c>user32.dll</c> that post or send a message, show, hide, move, resize, close or destroy a window are imported only where
    /// a rule allows them: <c>PostMessage</c> in <c>WindowsWindowSystem</c> (the one <c>WM_ACTIVATE</c> to the main window of a
    /// started game), <c>SendMessageTimeout</c> in <c>WindowsInstanceChannel</c> (the "show" message to a second launcher) and
    /// <c>ShowWindow</c> in <c>ForegroundWindow</c> (the launcher's own window). The one post uses the message and the
    /// parameter of A1b and nothing else, and only the activator calls it. Checked on the sources of the launcher and the core,
    /// comment lines skipped.
    /// </summary>
    [TestFixture]
    [Category(TestCategories.SourceTree)]
    public class WindowMessageRulesTests
    {
        private const string WindowSystem = "Empire-Earth-Launcher-Core/Platform/WindowsWindowSystem.cs";
        private const string InstanceChannel = "Empire-Earth-Launcher-Core/Platform/WindowsInstanceChannel.cs";
        private const string OwnWindow = "Empire Earth Launcher/ForegroundWindow.cs";
        private const string Activator = "Empire-Earth-Launcher-Core/Play/GameWindowActivator.cs";

        private static readonly Regex MessageOrWindowImport = new Regex(
            @"\bextern\b[^;]*\b(PostMessage\w*|PostThreadMessage\w*|SendMessage\w*|SendNotifyMessage\w*|ShowWindow\w*|MoveWindow|SetWindowPos|" +
            @"SetWindowPlacement|CloseWindow|DestroyWindow|SetWindowLong\w*|EndTask)\s*\(",
            RegexOptions.CultureInvariant);

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

        private static IEnumerable<Tuple<string, int, string>> LinesOf(string relativeFile)
        {
            return CodeLines().Where(line => line.Item1 == relativeFile);
        }

        [Test]
        public void TheFunctionsThatPostSendShowOrMoveAWindow_AreImportedInThreeAdaptersOnly()
        {
            var imports = new List<string>();
            foreach (Tuple<string, int, string> line in CodeLines())
            {
                Match match = MessageOrWindowImport.Match(line.Item3);
                if (match.Success)
                    imports.Add(line.Item1 + ":" + match.Groups[1].Value);
            }

            Assert.That(imports, Is.Not.Empty, "the check found no import at all");
            Assert.That(imports, Is.EquivalentTo(new[]
            {
                WindowSystem + ":PostMessageNative",
                InstanceChannel + ":SendMessageTimeout",
                OwnWindow + ":ShowWindow",
            }), "every adapter of the list really has its import, and no other source has one of these");
        }

        [Test]
        public void TheOnePost_CarriesWmActivateWithWaActive_AndNoOtherMessage()
        {
            List<string> lines = LinesOf(WindowSystem).Select(line => line.Item3).ToList();

            List<string> calls = lines.Where(line => line.Contains("PostMessageNative(") && !line.Contains("extern")).ToList();
            Assert.That(calls, Has.Count.EqualTo(1), "one call");
            Assert.That(calls[0], Does.Contain("WmActivate").And.Contain("new IntPtr(WaActive)").And.Contain("IntPtr.Zero"));
            Assert.That(lines, Has.Some.Contains("const int WmActivate = 0x0006;"));
            Assert.That(lines, Has.Some.Contains("const int WaActive = 1;"));
            Assert.That(lines.Where(line => Regex.IsMatch(line, @"\bconst\s+int\s+Wm\w*\s*=")).ToList(), Has.Count.EqualTo(1),
                "no message constant besides WM_ACTIVATE");
        }

        [Test]
        public void TheOnePost_IsCalledByTheActivatorOnly()
        {
            var calls = new List<string>();
            foreach (Tuple<string, int, string> line in CodeLines())
            {
                if (line.Item3.Contains("PostActivateMessage(") && !Regex.IsMatch(line.Item3, @"\bbool\s+PostActivateMessage\("))
                    calls.Add(line.Item1);
            }

            Assert.That(calls, Is.Not.Empty, "the check found no call at all");
            Assert.That(calls, Is.All.EqualTo(Activator));
        }

        [TestCase("[DllImport(\"user32.dll\", EntryPoint = \"PostMessageW\")] private static extern bool PostMessageW(IntPtr window, int message);", true)]
        [TestCase("private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int cx, int cy, uint flags);", true)]
        [TestCase("private static extern bool ShowWindowAsync(IntPtr window, int command);", true)]
        [TestCase("private static extern IntPtr SendMessageTimeout(IntPtr window, int message, IntPtr wParam, ref CopyDataStruct lParam,", true)]
        [TestCase("private static extern int SetWindowLongPtr(IntPtr window, int index, IntPtr value);", true)]
        [TestCase("private static extern bool DestroyWindow(IntPtr window);", true)]
        [TestCase("private static extern int GetWindowLong(IntPtr window, int index);", false)]
        [TestCase("private static extern bool IsWindowVisible(IntPtr window);", false)]
        [TestCase("windows.PostActivateMessage(window, out error);", false)]
        [TestCase("bool PostActivateMessage(IntPtr window, out int error);", false)]
        public void TheCheck_FindsAnImport(string line, bool found)
        {
            Assert.That(MessageOrWindowImport.IsMatch(line), Is.EqualTo(found));
        }
    }
}
