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
    /// <see cref="UiOperation"/> enables its trigger again when the work is done. A page that decides itself whether that
    /// control is enabled (an assignment <c>control.Enabled = ...</c> in the same file) must pass its state logic as
    /// <c>restore</c>, or a button would be enabled although it does nothing (build/UI review: the delete button of the
    /// registry cleanup after the last key was deleted, "Check all files" while a setup runs).
    /// </summary>
    [TestFixture]
    public class UiOperationRestoreTests
    {
        private const string LauncherFolder = "Empire Earth Launcher";
        private const string Call = "uiOperation.Run(";

        private static readonly Regex EnabledAssignment = new Regex(@"\b(?<control>[A-Za-z_][A-Za-z0-9_]*)\.Enabled\s*=[^=]",
            RegexOptions.CultureInvariant);

        /// <summary>The top-level arguments of the call that starts at <paramref name="open"/> (the opening parenthesis).</summary>
        private static List<string> Arguments(string code, int open)
        {
            var arguments = new List<string>();
            int depth = 0;
            int start = open + 1;
            for (int i = open; i < code.Length; i++)
            {
                char c = code[i];
                if (c == '(' || c == '{' || c == '[')
                    depth++;
                else if (c == ')' || c == '}' || c == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        arguments.Add(code.Substring(start, i - start).Trim());
                        return arguments;
                    }
                }
                else if (c == ',' && depth == 1)
                {
                    arguments.Add(code.Substring(start, i - start).Trim());
                    start = i + 1;
                }
            }
            throw new InvalidOperationException("Unbalanced call at " + open);
        }

        [Test]
        [Category(TestCategories.SourceTree)]
        public void TriggersWhoseStateThePageSets_AreRestoredByThePage()
        {
            var checkedCalls = 0;
            var offenders = new List<string>();
            IEnumerable<string> files = Directory.EnumerateFiles(RepositoryRoot.GetFullPath(LauncherFolder), "*.cs",
                                                                 SearchOption.AllDirectories)
                                                 .Where(file => !RepositoryRoot.ToRelativePath(file).Split('/')
                                                                               .Any(part => part == "bin" || part == "obj"));
            foreach (string file in files)
            {
                string code = File.ReadAllText(file);
                var controlled = new HashSet<string>(EnabledAssignment.Matches(code).Cast<Match>()
                                                                      .Select(m => m.Groups["control"].Value));
                for (int index = code.IndexOf(Call, StringComparison.Ordinal); index >= 0;
                     index = code.IndexOf(Call, index + Call.Length, StringComparison.Ordinal))
                {
                    List<string> arguments = Arguments(code, index + Call.Length - 1);
                    if (!controlled.Contains(arguments[0]))
                        continue;
                    checkedCalls++;
                    if (arguments.Count < 3)
                        offenders.Add(RepositoryRoot.ToRelativePath(file) + ": uiOperation.Run(" + arguments[0] + ", ...)");
                }
            }

            Assert.That(checkedCalls, Is.GreaterThanOrEqualTo(10), "the test finds the calls of the pages");
            Assert.That(offenders, Is.Empty, "pass the page's state logic (e.g. ShowState) as restore");
        }
    }
}
