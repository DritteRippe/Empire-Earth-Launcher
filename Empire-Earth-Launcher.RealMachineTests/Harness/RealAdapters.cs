using System;
using Empire_Earth_Launcher.Core.Logging;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.RealMachineTests.Harness
{
    /// <summary>
    /// The real Windows adapters of the core, created here and nowhere else in this program (an architecture test of the test
    /// project checks that): the registry, the file system, the system information and the mutex probe, as the launcher's
    /// <c>Program</c> creates them. Only <c>RealMachine/</c> and the <c>pick-targets</c> command use them; the self-tests use
    /// in-memory fakes.
    /// </summary>
    internal sealed class RealAdapters
    {
        /// <summary>The log file of the core in the work folder (it may hold hashes; never uploaded).</summary>
        public const string CoreLogFileName = "core.log";

        private RealAdapters(IRegistry registry, IFileSystem fileSystem, ISystemInfo systemInfo, IMutexProbe mutexProbe, ILogger logger)
        {
            Registry = registry;
            FileSystem = fileSystem;
            SystemInfo = systemInfo;
            MutexProbe = mutexProbe;
            Logger = logger;
        }

        /// <summary>The real registry. Every user wraps it: read-only, or recorded below the launcher's write policy.</summary>
        public IRegistry Registry { get; }

        /// <summary>The real file system. Every user wraps it: read-only, or writing only below the work folder.</summary>
        public IFileSystem FileSystem { get; }

        public ISystemInfo SystemInfo { get; }

        public IMutexProbe MutexProbe { get; }

        /// <summary>Writes <see cref="CoreLogFileName"/> in the work folder.</summary>
        public ILogger Logger { get; }

        /// <summary>The adapters for one run; creates <paramref name="workFolder"/> if needed.</summary>
        /// <exception cref="InvalidOperationException">The work folder cannot be created.</exception>
        public static RealAdapters Create(string workFolder)
        {
            if (workFolder == null)
                throw new ArgumentNullException(nameof(workFolder));
            var fileSystem = new LocalFileSystem();
            FileSystemResult created = fileSystem.CreateDirectory(workFolder);
            if (!created.IsOk)
                throw new InvalidOperationException("The work folder " + workFolder + " cannot be created: " + created + ".");
            var logger = new FileLogger(WinPath.Combine(workFolder, CoreLogFileName));
            return new RealAdapters(new WindowsRegistry(), fileSystem, new WindowsSystemInfo(logger), new WindowsMutexProbe(logger), logger);
        }

        /// <summary>The real file system for a command that only reads (<c>pick-targets</c>).</summary>
        public static IFileSystem CreateReadOnlyFileSystem(HarnessViolations violations)
        {
            return new ReadOnlyFileSystem(new LocalFileSystem(), violations);
        }
    }
}
