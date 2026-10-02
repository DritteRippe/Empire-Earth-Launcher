namespace Empire_Earth_Launcher.Tests.TestSupport
{
    /// <summary>NUnit categories of the tests (ADR 0012 plan review).</summary>
    internal static class TestCategories
    {
        /// <summary>
        /// Tests that read the source tree (<see cref="RepositoryRoot"/>): they run in CI and with the verify script, not from
        /// the <c>Tests\</c> folder of the laptop package (<c>--where "cat != SourceTree"</c>, test plan WP1-11).
        /// </summary>
        public const string SourceTree = "SourceTree";
    }
}
