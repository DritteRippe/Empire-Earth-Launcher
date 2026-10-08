using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace Empire_Earth_Mod_Lib
{
    /// <summary>
    /// Loads the mod archives (.eem) of a folder.
    /// </summary>
    /// <remarks>
    /// TODO: installing and uninstalling mods into the game folders is not implemented yet. It was only
    /// present as empty public stubs, which suggested functionality that does not exist.
    /// </remarks>
    public class ModManager
    {
        private readonly DirectoryInfo modDirectory;
        private readonly List<ModData> mods = new List<ModData>();
        private readonly List<string> loadErrors = new List<string>();

        /// <param name="modDirectory">Directory with the mod archives, must exist.</param>
        /// <exception cref="DirectoryNotFoundException">The directory does not exist.</exception>
        public ModManager(string modDirectory)
        {
            this.modDirectory = new DirectoryInfo(modDirectory);
            if (!this.modDirectory.Exists)
                throw new DirectoryNotFoundException("Unable to find the mod directory " + this.modDirectory.FullName + "!");
        }

        /// <summary>Mods loaded by the last <see cref="Init"/>.</summary>
        public ReadOnlyCollection<ModData> Mods
        {
            get { return mods.AsReadOnly(); }
        }

        /// <summary>One message ("file: reason") per mod archive the last <see cref="Init"/> had to skip.</summary>
        public ReadOnlyCollection<string> LoadErrors
        {
            get { return loadErrors.AsReadOnly(); }
        }

        /// <summary>
        /// Loads all mod archives of the mod directory. A broken archive is skipped and reported in
        /// <see cref="LoadErrors"/> instead of preventing the other mods from loading.
        /// </summary>
        /// <returns>true if every archive was loaded, false if at least one was skipped.</returns>
        public bool Init()
        {
            mods.Clear();
            loadErrors.Clear();

            // On Windows the pattern "*.eem" also matches longer extensions such as ".eemx".
            foreach (FileInfo fi in modDirectory.GetFiles(EemFormat.SearchPattern)
                         .Where(file => file.Extension.Equals(EemFormat.Extension, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    mods.Add(ModArchiveReader.ReadModData(fi.FullName));
                }
                catch (Exception ex) when (!(ex is OutOfMemoryException))
                {
                    // Deliberately broad: ReadModData reports damaged archives as InvalidDataException (also the
                    // ArgumentException of a damaged ZIP directory) and a file it cannot open as IOException or
                    // UnauthorizedAccessException, but the vendored ZipStorer is not hardened against every
                    // possible archive, and one bad file must not keep the other mods from loading.
                    loadErrors.Add(fi.Name + ": " + ex.Message);
                }
            }
            return loadErrors.Count == 0;
        }
    }
}
