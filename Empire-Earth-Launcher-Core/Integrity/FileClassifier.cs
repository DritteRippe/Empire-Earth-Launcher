using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Empire_Earth_Launcher.Core.Integrity
{
    /// <summary>The class of an installed file (contract 2.4): what a missing or changed file means.</summary>
    public enum FileClass
    {
        /// <summary>
        /// A file that can contain code: missing or changed means <see cref="IntegrityState.Damaged"/> (an antivirus deletion,
        /// a modified program).
        /// </summary>
        Code,

        /// <summary>
        /// Changed by the game or the player by design (<c>cfg ini conf config log</c>): a different hash is not reported,
        /// missing means <see cref="IntegrityState.Incomplete"/> (O6).
        /// </summary>
        Mutable,

        /// <summary>Everything else (game data, textures, sounds, maps): changed means <see cref="IntegrityState.Modified"/>.</summary>
        Data
    }

    /// <summary>
    /// The file classes of contract 2.4, by the extension of the last name of a path, compared ignoring case. The code list
    /// is <c>CodeFileExtensions</c> of the setup's <c>utils.iss</c> (the download policy of the setup), in its order;
    /// <c>FileClassifierContractTests</c> compares both lists with the table of <c>docs/CONTRACT.md</c>.
    /// </summary>
    public static class FileClassifier
    {
        /// <summary>The extensions of class <see cref="FileClass.Code"/>, lowercase, in the order of the contract table.</summary>
        public static readonly IReadOnlyList<string> CodeExtensions = new ReadOnlyCollection<string>(new[]
        {
            "exe", "dll", "asi", "ocx", "sys", "drv", "scr", "com", "pif", "cpl", "efi", "ax", "acm", "mui", "flt", "m3d",
            "bat", "cmd", "ps1", "psm1", "psd1", "vbs", "vbe", "js", "jse", "wsf", "wsh", "wsc", "sct", "hta",
            "msi", "msp", "mst", "msc", "appx", "msix", "jar",
            "lnk", "url", "scf", "reg", "inf", "chm", "hlp"
        });

        /// <summary>The extensions of class <see cref="FileClass.Mutable"/>, lowercase, in the order of the contract table.</summary>
        public static readonly IReadOnlyList<string> MutableExtensions = new ReadOnlyCollection<string>(new[]
        {
            "cfg", "ini", "conf", "config", "log"
        });

        private static readonly HashSet<string> Code = new HashSet<string>(CodeExtensions, StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> Mutable = new HashSet<string>(MutableExtensions, StringComparer.OrdinalIgnoreCase);

        /// <summary>The class of the file <paramref name="path"/> (a manifest path with <c>/</c> or a Windows path).</summary>
        public static FileClass Classify(string path)
        {
            string extension = ExtensionOf(path);
            if (Code.Contains(extension))
                return FileClass.Code;
            return Mutable.Contains(extension) ? FileClass.Mutable : FileClass.Data;
        }

        /// <summary>
        /// The extension of the last name of <paramref name="path"/> (after the last <c>/</c> or <c>\</c>), lowercase and
        /// without the dot; empty if it has none. Trailing dots and spaces do not count, as Windows drops them from file
        /// names (the setup's <c>GetFileNameExtension</c> does the same).
        /// </summary>
        public static string ExtensionOf(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));
            int separator = path.LastIndexOfAny(new[] { '/', '\\' });
            string name = path.Substring(separator + 1).TrimEnd('.', ' ');
            int dot = name.LastIndexOf('.');
            return dot < 0 ? string.Empty : name.Substring(dot + 1).ToLowerInvariant();
        }

        /// <summary>The name of a class as the contract writes it: <c>code</c>, <c>mutable</c> or <c>data</c>.</summary>
        public static string Name(FileClass fileClass)
        {
            switch (fileClass)
            {
                case FileClass.Code:
                    return "code";
                case FileClass.Mutable:
                    return "mutable";
                default:
                    return "data";
            }
        }
    }
}
