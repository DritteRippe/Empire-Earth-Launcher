using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Empire_Earth_Launcher.Core.Diagnostics;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Mods
{
    /// <summary>One preset in <c>Data\dxm\mods</c> of a game folder: a folder with sounds, textures, fonts and a lobby theme.</summary>
    public sealed class ModPreset
    {
        internal ModPreset(string folderName, string folderPath, ModCredits credits, long sizeBytes, bool isSizeComplete)
        {
            FolderName = folderName;
            FolderPath = folderPath;
            Credits = credits;
            SizeBytes = sizeBytes;
            IsSizeComplete = isSizeComplete;
        }

        /// <summary>The name of the folder: the value that <c>&lt;Mod&gt;</c> and <c>&lt;LobbyTheme&gt;</c> of <c>dreXmod.config</c> name.</summary>
        public string FolderName { get; }

        public string FolderPath { get; }

        /// <summary>The head of the <c>CREDITS</c> file; null if the folder has none or it could not be read.</summary>
        public ModCredits Credits { get; }

        /// <summary>The size of everything below the folder in bytes (at least this much if <see cref="IsSizeComplete"/> is false).</summary>
        public long SizeBytes { get; }

        /// <summary>False if the folder has more files or levels than the scan follows (<see cref="ModFolderScanner.MaxFiles"/>).</summary>
        public bool IsSizeComplete { get; }

        /// <summary>True for the skeleton for authors, which is no preset to use.</summary>
        public bool IsTemplate
        {
            get { return string.Equals(FolderName, ModFolderScanner.TemplateFolderName, StringComparison.OrdinalIgnoreCase); }
        }

        /// <summary>The <c>Name:</c> line of the credits when it differs from the folder name; null otherwise.</summary>
        public string CreditsName
        {
            get
            {
                string name = Credits?.Name;
                return name == null || string.Equals(name, FolderName, StringComparison.OrdinalIgnoreCase) ? null : name;
            }
        }

        public override string ToString()
        {
            return FolderName + (IsTemplate ? " (template)" : string.Empty);
        }
    }

    /// <summary>The presets of one <c>Data\dxm\mods</c> folder (<see cref="ModFolderScanner"/>).</summary>
    public sealed class ModFolderScan
    {
        internal ModFolderScan(string path, ConfigFileStatus status, string problem, IList<ModPreset> presets)
        {
            Path = path;
            Status = status;
            Problem = problem;
            Presets = new ReadOnlyCollection<ModPreset>(presets);
        }

        /// <summary>The folder that was scanned.</summary>
        public string Path { get; }

        /// <summary><see cref="ConfigFileStatus.Missing"/> if the folder does not exist, <see cref="ConfigFileStatus.Unreadable"/> if it cannot be listed.</summary>
        public ConfigFileStatus Status { get; }

        /// <summary>Why the folder could not be listed, for the log; null otherwise.</summary>
        public string Problem { get; }

        /// <summary>The preset folders by name, ignoring case.</summary>
        public IReadOnlyList<ModPreset> Presets { get; }
    }

    /// <summary>
    /// Lists the dreXmod presets of <c>Data\dxm\mods</c>: every folder in it, with the head of its <c>CREDITS</c> file and its
    /// size. Reading only; the launcher never installs, changes or deletes a preset (the presets of dreXmod come with the setup,
    /// and self-made ones are the player's, ADR 0014).
    /// </summary>
    public static class ModFolderScanner
    {
        /// <summary>The folder of the presets below a game folder.</summary>
        public const string ModsFolderPath = @"Data\dxm\mods";

        /// <summary>The skeleton for authors in the folder of the presets; the page hides it unless asked.</summary>
        public const string TemplateFolderName = "template";

        /// <summary>A <c>CREDITS</c> file is a few KB; a larger one is not read.</summary>
        public const long MaxCreditsBytes = 64 * 1024;

        /// <summary>The most files of a preset the size is added up for (a preset has about 100; a runaway folder must not stall the page).</summary>
        public const int MaxFiles = 20000;

        /// <summary>The deepest level below a preset the size follows.</summary>
        public const int MaxDepth = 12;

        /// <summary>The folder of the presets of <paramref name="gameFolder"/>.</summary>
        public static string ModsFolder(string gameFolder)
        {
            return WinPath.Combine(gameFolder, ModsFolderPath);
        }

        public static ModFolderScan Scan(IFileSystem fileSystem, string modsFolder)
        {
            if (fileSystem == null)
                throw new ArgumentNullException(nameof(fileSystem));
            if (modsFolder == null)
                throw new ArgumentNullException(nameof(modsFolder));
            if (!fileSystem.DirectoryExists(modsFolder))
                return new ModFolderScan(modsFolder, ConfigFileStatus.Missing, null, new ModPreset[0]);
            FileSystemResult<IReadOnlyList<string>> folders = fileSystem.GetDirectories(modsFolder);
            if (!folders.IsOk)
                return new ModFolderScan(modsFolder, ConfigFileStatus.Unreadable, folders.ToString(), new ModPreset[0]);
            List<ModPreset> presets = folders.Value
                .Select(folder => ReadPreset(fileSystem, folder))
                .OrderBy(preset => preset.FolderName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return new ModFolderScan(modsFolder, ConfigFileStatus.Read, null, presets);
        }

        private static ModPreset ReadPreset(IFileSystem fileSystem, string folder)
        {
            string name = WinPath.GetFileName(folder);
            ModCredits credits = null;
            string creditsFile = WinPath.Combine(folder, CreditsParser.FileName);
            if (fileSystem.FileExists(creditsFile))
            {
                FileSystemResult<byte[]> bytes = fileSystem.ReadAllBytes(creditsFile, MaxCreditsBytes);
                if (bytes.IsOk)
                    credits = CreditsParser.Parse(DreXmodConfigReader.Decode(bytes.Value));
            }
            long size = SizeOf(fileSystem, folder, out bool complete);
            return new ModPreset(name, folder, credits, size, complete);
        }

        /// <summary>Adds up the files below <paramref name="folder"/>; stops at <see cref="MaxFiles"/> files and <see cref="MaxDepth"/> levels.</summary>
        private static long SizeOf(IFileSystem fileSystem, string folder, out bool complete)
        {
            long size = 0;
            int files = 0;
            complete = true;
            var pending = new Stack<Tuple<string, int>>();
            pending.Push(Tuple.Create(folder, 0));
            while (pending.Count > 0)
            {
                Tuple<string, int> current = pending.Pop();
                FileSystemResult<IReadOnlyList<string>> names = fileSystem.GetFiles(current.Item1);
                if (names.IsOk)
                {
                    foreach (string file in names.Value)
                    {
                        if (++files > MaxFiles)
                        {
                            complete = false;
                            return size;
                        }
                        FileSystemResult<FileEntry> info = fileSystem.GetFileInfo(file);
                        if (info.IsOk)
                            size += info.Value.Length;
                    }
                }
                else
                {
                    complete = false;
                }
                FileSystemResult<IReadOnlyList<string>> subfolders = fileSystem.GetDirectories(current.Item1);
                if (!subfolders.IsOk)
                {
                    complete = false;
                    continue;
                }
                foreach (string subfolder in subfolders.Value)
                {
                    if (current.Item2 >= MaxDepth)
                        complete = false;
                    else
                        pending.Push(Tuple.Create(subfolder, current.Item2 + 1));
                }
            }
            return size;
        }
    }
}
