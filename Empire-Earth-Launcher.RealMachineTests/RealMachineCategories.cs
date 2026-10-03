namespace Empire_Earth_Launcher.RealMachineTests
{
    /// <summary>NUnit categories of this test program.</summary>
    internal static class RealMachineCategories
    {
        /// <summary>
        /// The checks against the real computer (<c>RealMachine/</c>): explicit, so a run without a filter skips them; selected
        /// with <c>--where "cat == RealMachine"</c>, and then still ignored unless <see cref="Harness.RealMachineGate"/> allows
        /// them (switch variable set to 1, Windows, a GitHub-hosted runner).
        /// </summary>
        public const string RealMachine = "RealMachine";

        /// <summary>Tests of the harness itself with in-memory fakes (<c>SelfTest/</c>): they run everywhere, also under Mono.</summary>
        public const string SelfTest = "SelfTest";
    }
}
