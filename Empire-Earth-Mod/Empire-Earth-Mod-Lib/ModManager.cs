using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Empire_Earth_Mod_Lib
{
    public class ModManager
    {

        private DirectoryInfo ModWorkDirectory;
        private DirectoryInfo EEC_GameDirectory;
        /// <summary>null when The Art of Conquest is not installed.</summary>
        private DirectoryInfo AOC_GameDirectory;

        private readonly List<ModData> _modDatas = new List<ModData>();
        private readonly List<string> _loadErrors = new List<string>();

        /// <param name="eecGameDirectory">Empire Earth directory, must exist.</param>
        /// <param name="aocGameDirectory">The Art of Conquest directory; null or empty if it is not installed,
        /// otherwise it must exist.</param>
        /// <param name="modWorkDirectory">Directory with the mod archives, must exist.</param>
        /// <exception cref="DirectoryNotFoundException">A required directory does not exist.</exception>
        public ModManager(string eecGameDirectory, string aocGameDirectory, string modWorkDirectory)
        {
            EEC_GameDirectory = new DirectoryInfo(eecGameDirectory);
            AOC_GameDirectory = string.IsNullOrEmpty(aocGameDirectory) ? null : new DirectoryInfo(aocGameDirectory);
            ModWorkDirectory = new DirectoryInfo(modWorkDirectory);
            if (!ModWorkDirectory.Exists)
                throw new DirectoryNotFoundException("Unable to find the mod work directory " + ModWorkDirectory.FullName + "!");
            if (!EEC_GameDirectory.Exists)
                throw new DirectoryNotFoundException("Unable to find the Empire Earth directory " + EEC_GameDirectory.FullName + "!");
            if (AOC_GameDirectory != null && !AOC_GameDirectory.Exists)
                throw new DirectoryNotFoundException("Unable to find the Art of Conquest directory " + AOC_GameDirectory.FullName + "!");
        }

        /// <summary>Mods loaded by the last <see cref="Init"/>.</summary>
        public ReadOnlyCollection<ModData> Mods
        {
            get { return _modDatas.AsReadOnly(); }
        }

        /// <summary>One message ("file: reason") per mod archive the last <see cref="Init"/> had to skip.</summary>
        public ReadOnlyCollection<string> LoadErrors
        {
            get { return _loadErrors.AsReadOnly(); }
        }

        /// <summary>
        /// Loads all mod archives of the mod work directory. A broken archive is skipped and reported in
        /// <see cref="LoadErrors"/> instead of preventing the other mods from loading.
        /// </summary>
        /// <returns>true if every archive was loaded, false if at least one was skipped.</returns>
        public bool Init()
        {
            _modDatas.Clear();
            _loadErrors.Clear();

            // On Windows the pattern "*.eem" also matches longer extensions such as ".eemx".
            foreach (FileInfo fi in ModWorkDirectory.GetFiles(EemFormat.SearchPattern)
                         .Where(file => file.Extension.Equals(EemFormat.Extension, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    _modDatas.Add(ModData.LoadFromEEM(fi.FullName));
                }
                catch (Exception ex) when (!(ex is OutOfMemoryException))
                {
                    // Deliberately broad: besides the documented IOException/InvalidDataException, a damaged
                    // archive can make the vendored ZipStorer throw almost anything (e.g. index errors).
                    _loadErrors.Add(fi.Name + ": " + ex.Message);
                }
            }
            return _loadErrors.Count == 0;
        }

        public void Install(FileInfo fileInfo)
        {
            
        }
        
        public void Install(Guid uuid)
        {
            
        }
        
        public void Uninstall(string uuid)
        {
            
        }
        
    }
}