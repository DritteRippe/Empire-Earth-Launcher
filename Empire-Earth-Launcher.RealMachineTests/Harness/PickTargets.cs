using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Empire_Earth_Launcher.Core.Contract;
using Empire_Earth_Launcher.Core.Integrity;
using Empire_Earth_Launcher.Core.Platform;
using Empire_Earth_Launcher.RealMachineTests.Json;

namespace Empire_Earth_Launcher.RealMachineTests.Harness
{
    /// <summary>
    /// The files of the EE folder a CI job damages for the integrity checks (contract 2.4, 2.5): one of each class of the
    /// manifest of an installation, chosen by the launcher's own file classes, so the job does not repeat the extension table.
    /// Only relative paths are chosen and printed; the hashes of the manifest are never read out.
    /// </summary>
    internal static class PickTargets
    {
        /// <summary>The name of the command: <c>pick-targets --root &lt;root&gt; --product &lt;EE|NeoEE&gt;</c>.</summary>
        public const string CommandName = "pick-targets";

        /// <summary>The classes chosen, in the order of the output.</summary>
        private static readonly FileClass[] Classes = { FileClass.Code, FileClass.Data, FileClass.Mutable };

        /// <summary>
        /// The first <c>code</c>, <c>data</c> and <c>mutable</c> file below <c>Empire Earth/</c> (files directly in the folder
        /// first, then by ordinal order), without the programs of the games (the discovery needs them); null for a class without
        /// a file.
        /// </summary>
        public static IDictionary<FileClass, string> Choose(IEnumerable<ManifestEntry> entries)
        {
            if (entries == null)
                throw new ArgumentNullException(nameof(entries));
            string prefix = Game.EmpireEarth.FolderName + "/";
            var programs = new HashSet<string>(Game.All.Select(game => game.ProgramName), StringComparer.OrdinalIgnoreCase);
            List<ManifestEntry> candidates = entries
                .Where(entry => entry.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Where(entry => !programs.Contains(entry.Path.Substring(entry.Path.LastIndexOf('/') + 1)))
                .OrderBy(entry => entry.Path.Count(c => c == '/'))
                .ThenBy(entry => entry.Path, StringComparer.Ordinal)
                .ToList();
            var chosen = new Dictionary<FileClass, string>();
            foreach (FileClass fileClass in Classes)
                chosen[fileClass] = candidates.FirstOrDefault(entry => entry.Class == fileClass)?.Path;
            return chosen;
        }

        /// <summary>
        /// Runs the command: reads <c>&lt;root&gt;\_setupdata_&lt;product&gt;\files.sha256</c> through
        /// <paramref name="fileSystem"/> and writes <c>{"code": ..., "data": ..., "mutable": ...}</c> to
        /// <paramref name="output"/>. Exit code 0, 2 for wrong arguments, 1 if the manifest cannot be read or is invalid.
        /// </summary>
        public static int Run(IReadOnlyList<string> arguments, IFileSystem fileSystem, TextWriter output, TextWriter error)
        {
            if (arguments == null)
                throw new ArgumentNullException(nameof(arguments));
            if (fileSystem == null)
                throw new ArgumentNullException(nameof(fileSystem));
            string root = null;
            string productId = null;
            for (int i = 0; i + 1 < arguments.Count; i += 2)
            {
                if (arguments[i] == "--root")
                    root = arguments[i + 1];
                else if (arguments[i] == "--product")
                    productId = arguments[i + 1];
                else
                    root = productId = null;
            }
            Product product = Product.FromId(productId);
            if (arguments.Count != 4 || root == null || !WinPath.IsFullyQualified(root) || product == null || product.Id != productId)
            {
                error.WriteLine("Usage: " + CommandName + " --root <full path of the install root> --product <EE|NeoEE>");
                return 2;
            }

            string manifest = WinPath.Combine(root, product.SetupDataFolderName + @"\" + ContractNames.ManifestFileName);
            FileSystemResult<byte[]> content = fileSystem.ReadAllBytes(manifest, ManifestReader.MaxFileBytes);
            if (!content.IsOk)
            {
                error.WriteLine("The manifest " + manifest + " cannot be read: " + content.Status + ".");
                return 1;
            }
            ManifestParseResult parsed = ManifestReader.Parse(content.Value);
            if (!parsed.IsValid)
            {
                // The line of the problem holds a hash: only the kind and the line number are printed.
                error.WriteLine("The manifest " + manifest + " is invalid: " + parsed.Problem + " in line " + parsed.LineNumber + ".");
                return 1;
            }
            IDictionary<FileClass, string> chosen = Choose(parsed.Entries);
            output.WriteLine("{ " + string.Join(", ", Classes.Select(fileClass =>
                JsonWriter.Member(FileClassifier.Name(fileClass), JsonWriter.Quote(chosen[fileClass])))) + " }");
            return 0;
        }
    }
}
