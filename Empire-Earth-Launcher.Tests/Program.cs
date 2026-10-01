using System;
using System.Linq;
using NUnitLite;

namespace Empire_Earth_Launcher.Tests
{
    /// <summary>
    /// Runs the tests of this assembly with NUnitLite.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The exit code is 0 when every test passed. Otherwise it is the number of failed tests, or a negative
    /// NUnitLite error code (e.g. for an invalid option), so scripts and CI only have to check it.
    /// </para>
    /// <para>
    /// All NUnitLite options can be passed, e.g. <c>--where "class =~ Lobby"</c> to run some tests only or
    /// <c>--result=TestResult.xml</c> to write an NUnit 3 result file. Without a result option no result file
    /// is written, so running the tests from the repository root leaves no file behind.
    /// </para>
    /// <para>
    /// The tests work in folders below the temporary folder, never contact a server and never show UI.
    /// </para>
    /// </remarks>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            bool resultOptionGiven = args.Any(arg =>
            {
                string option = arg.TrimStart('-', '/');
                return option.StartsWith("result", StringComparison.OrdinalIgnoreCase) ||
                       option.StartsWith("noresult", StringComparison.OrdinalIgnoreCase);
            });
            if (!resultOptionGiven)
                args = args.Concat(new[] { "--noresult" }).ToArray();

            return new AutoRun(typeof(Program).Assembly).Execute(args);
        }
    }
}
