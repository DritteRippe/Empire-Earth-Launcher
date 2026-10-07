using System;

namespace Empire_Earth_Launcher.Core.Platform
{
    /// <summary>
    /// Creates a named mutex that marks a running program (the single-instance mutex of the launcher, ADR 0010). Unlike
    /// <see cref="IMutexProbe"/>, which only looks, this holds a mutex until the handle is disposed.
    /// </summary>
    public interface IMutexOwner
    {
        /// <summary>
        /// Creates the mutex <paramref name="name"/> in the session namespace (without <c>Global\</c>, as Inno Setup's
        /// <c>AppMutex</c> checks it) and keeps it until the returned handle is disposed.
        /// </summary>
        /// <returns>The handle; null if a mutex of that name exists already (another instance holds it).</returns>
        IDisposable TryCreate(string name);
    }
}
