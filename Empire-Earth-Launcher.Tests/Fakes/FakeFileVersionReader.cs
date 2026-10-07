using System.Collections.Generic;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Tests.Fakes
{
    /// <summary><see cref="IFileVersionReader"/> with versions set per path (Windows path rules); other paths have none.</summary>
    internal sealed class FakeFileVersionReader : IFileVersionReader
    {
        private readonly Dictionary<string, string> versions = new Dictionary<string, string>(WinPath.Comparer);

        /// <summary>The version of <paramref name="path"/>.</summary>
        public FakeFileVersionReader With(string path, string version)
        {
            versions[path] = version;
            return this;
        }

        public string GetFileVersion(string path)
        {
            return versions.TryGetValue(path, out string version) ? version : null;
        }
    }
}
