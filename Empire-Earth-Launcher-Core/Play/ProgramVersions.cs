using System;
using System.Collections.Generic;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Play
{
    /// <summary>The program of one game of an installation and its file version.</summary>
    public sealed class ProgramVersion
    {
        internal ProgramVersion(Game game, string programPath, bool exists, string version)
        {
            Game = game;
            ProgramPath = programPath;
            Exists = exists;
            Version = version;
        }

        public Game Game { get; }

        /// <summary>Full path of <c>Empire Earth.exe</c> or <c>EE-AOC.exe</c> in the real game folder.</summary>
        public string ProgramPath { get; }

        /// <summary>True if the program exists.</summary>
        public bool Exists { get; }

        /// <summary>The file version (<c>2.0.0.2949</c>); null if the program is missing or has no version.</summary>
        public string Version { get; }

        public override string ToString()
        {
            return Game.ProgramName + " " + (!Exists ? "missing" : Version ?? "without version");
        }
    }

    /// <summary>
    /// The file versions of the programs of an installation for the Play page and the diagnostics report (ADR 0010
    /// amendment, forum report section 8 row 1: version conflicts, forum 4.12). Shown as they are; no reference list is
    /// compared (the integrity manifest does that for community installations, L-WP7).
    /// </summary>
    public sealed class ProgramVersions
    {
        private readonly IFileSystem fileSystem;
        private readonly IFileVersionReader reader;

        public ProgramVersions(IFileSystem fileSystem, IFileVersionReader reader)
        {
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            this.reader = reader ?? throw new ArgumentNullException(nameof(reader));
        }

        /// <summary>The program of every game of <paramref name="installation"/>: Empire Earth, then The Art of Conquest if installed.</summary>
        public IReadOnlyList<ProgramVersion> Read(Installation installation)
        {
            if (installation == null)
                throw new ArgumentNullException(nameof(installation));
            return Game.All.Where(game => installation.GetGameFolder(game) != null)
                       .Select(game =>
                       {
                           string program = WinPath.Combine(installation.GetGameFolder(game), game.ProgramName);
                           bool exists = fileSystem.FileExists(program);
                           return new ProgramVersion(game, program, exists, exists ? reader.GetFileVersion(program) : null);
                       })
                       .ToList();
        }
    }
}
