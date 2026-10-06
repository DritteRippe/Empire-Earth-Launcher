using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Empire_Earth_Launcher.Properties;

namespace Empire_Earth_Launcher.Tests.TestSupport
{
    /// <summary>
    /// Switches the UI language of the launcher for a test (ADR 0009): the pages read their texts from the resources in
    /// <see cref="CultureInfo.CurrentUICulture"/> when they are created.
    /// </summary>
    internal static class TestUiLanguage
    {
        private const string SatelliteFileName = "Empire Earth Launcher.resources.dll";

        private static bool resolverInstalled;

        /// <summary>
        /// Uses <paramref name="name"/> (<c>en</c>, <c>de</c> or <c>fr</c>) on this thread until the result is disposed. Fails if the
        /// resources of the language are missing, because a test that silently measured the English texts would prove nothing
        /// about German and French.
        /// </summary>
        public static IDisposable Use(string name)
        {
            InstallSatelliteResolver();
            CultureInfo previous = Thread.CurrentThread.CurrentUICulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(name);
            var scope = new Scope(previous);
            try
            {
                CheckTranslated(name);
            }
            catch
            {
                scope.Dispose();
                throw;
            }
            return scope;
        }

        private sealed class Scope : IDisposable
        {
            private readonly CultureInfo previous;

            public Scope(CultureInfo previous)
            {
                this.previous = previous;
            }

            public void Dispose()
            {
                Thread.CurrentThread.CurrentUICulture = previous;
            }
        }

        private static void CheckTranslated(string name)
        {
            if (name == "en")
                return;
            string neutral = Resources.ResourceManager.GetString("NavigationPlay", CultureInfo.InvariantCulture);
            string translated = Resources.ResourceManager.GetString("NavigationPlay", CultureInfo.GetCultureInfo(name));
            if (neutral == translated)
                throw new InvalidOperationException("The resources of the language '" + name + "' were not found next to the test program.");
        }

        /// <summary>
        /// MSBuild copies the satellite assemblies of the launcher to the output folder of the test program; xbuild (Mono) does not.
        /// Where one is missing, it is taken from the output folder of the launcher project, which lies next to the one of the tests.
        /// </summary>
        private static void InstallSatelliteResolver()
        {
            if (resolverInstalled)
                return;
            resolverInstalled = true;
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                var name = new AssemblyName(args.Name);
                if (!name.Name.EndsWith(".resources", StringComparison.Ordinal) || string.IsNullOrEmpty(name.CultureName) ||
                    !string.Equals(name.Name, Path.GetFileNameWithoutExtension(SatelliteFileName), StringComparison.Ordinal))
                    return null;
                string file = FindSatellite(name.CultureName);
                return file == null ? null : Assembly.LoadFrom(file);
            };
        }

        private static string FindSatellite(string culture)
        {
            string configuration = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)).Name;
            for (DirectoryInfo folder = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory); folder != null; folder = folder.Parent)
            {
                string launcherBin = Path.Combine(folder.FullName, "Empire Earth Launcher", "bin");
                if (!Directory.Exists(launcherBin))
                    continue;
                // The same configuration first (Debug next to Debug), any other after it.
                return new[] { Path.Combine(launcherBin, configuration) }
                    .Concat(Directory.GetDirectories(launcherBin))
                    .Select(bin => Path.Combine(bin, culture, SatelliteFileName))
                    .FirstOrDefault(File.Exists);
            }
            return null;
        }
    }
}
