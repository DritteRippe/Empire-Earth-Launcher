using System;
using System.Linq;
using Empire_Earth_Launcher.RealMachineTests.Harness;
using NUnitLite;

namespace Empire_Earth_Launcher.RealMachineTests
{
    /// <summary>
    /// Runs the checks of the launcher core against a real installation on a GitHub-hosted Windows runner (category
    /// <c>RealMachine</c>), and the tests of this harness (category <c>SelfTest</c>), with NUnitLite.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Without options only the self-tests run; the real-machine checks are explicit and switched off (README, "Real machine",
    /// and <see cref="RealMachineGate"/>). The setup repository's end-to-end workflow runs them after each step of its
    /// scenarios: <c>--where "cat == RealMachine" "--params=expect=&lt;json&gt;;work=&lt;folder&gt;"</c> with
    /// <c>EE_LAUNCHER_REAL_MACHINE_TESTS=1</c>. The exit code is 0 when every test passed, else the number of failed tests or a
    /// negative NUnitLite error code; without a result option no result file is written.
    /// </para>
    /// <para>
    /// <c>pick-targets --root &lt;root&gt; --product &lt;EE|NeoEE&gt;</c> only reads the manifest of an installation and prints
    /// one file of each class (<see cref="PickTargets"/>).
    /// </para>
    /// </remarks>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == PickTargets.CommandName)
                return PickTargets.Run(args.Skip(1).ToList(), RealAdapters.CreateReadOnlyFileSystem(new HarnessViolations()), Console.Out,
                    Console.Error);

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
