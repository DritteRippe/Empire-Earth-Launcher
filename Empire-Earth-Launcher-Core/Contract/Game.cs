using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Empire_Earth_Launcher.Core.Contract
{
    /// <summary>
    /// One of the two games of an installation: Empire Earth or its expansion The Art of Conquest, with their
    /// fixed names (contract 0, "Folders, programs and mutexes"). There are exactly two instances.
    /// </summary>
    public sealed class Game
    {
        /// <summary>Empire Earth, in the EE folder of every installation.</summary>
        public static readonly Game EmpireEarth = new Game("EE", "Empire Earth", "Empire Earth.exe",
            "StainlessSteelStudiosPresentsEmpireEarth");

        /// <summary>The Art of Conquest, installed only with the component <c>gameaoc</c>.</summary>
        public static readonly Game ArtOfConquest = new Game("AoC", "Empire Earth - The Art of Conquest", "EE-AOC.exe",
            "MadDocSoftwarePresentsEmpireEarthExpansion");

        /// <summary>Both games, Empire Earth first.</summary>
        public static readonly IReadOnlyList<Game> All = new ReadOnlyCollection<Game>(new[] { EmpireEarth, ArtOfConquest });

        private Game(string id, string folderName, string programName, string mutexName)
        {
            Id = id;
            FolderName = folderName;
            ProgramName = programName;
            MutexName = mutexName;
        }

        /// <summary>
        /// Short name: <c>EE</c> or <c>AoC</c>. It is the value name of the game in the defaults marker
        /// (contract 3.5) and the name used in log messages.
        /// </summary>
        public string Id { get; }

        /// <summary>Name of the game folder below the install root of a community installation.</summary>
        public string FolderName { get; }

        /// <summary>File name of the program in the game folder.</summary>
        public string ProgramName { get; }

        /// <summary>
        /// Name of the mutex the running game creates (in the session namespace, without <c>Global\</c>); the
        /// setup uses it as <c>AppMutex</c> (contract 4.2).
        /// </summary>
        public string MutexName { get; }

        public override string ToString()
        {
            return Id;
        }
    }
}
