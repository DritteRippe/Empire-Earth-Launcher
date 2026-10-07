namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>
    /// Checks whether a named mutex exists (ADR 0010): the setups hold <c>EE_Setup</c>/<c>NeoEE_Setup</c> while
    /// they run, the games their own mutex (contract 0, 4.2). The probe never creates or holds a mutex.
    /// </summary>
    public interface IMutexProbe
    {
        /// <summary>
        /// True if a mutex named <paramref name="name"/> exists in the session namespace (as the setups and the games
        /// create it) or as <c>Global\&lt;name&gt;</c>. Names are compared as Windows does: case-sensitive.
        /// </summary>
        bool Exists(string name);
    }
}
