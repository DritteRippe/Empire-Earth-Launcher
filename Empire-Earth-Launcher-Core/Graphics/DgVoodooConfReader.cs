using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Empire_Earth_Launcher.Core.Diagnostics;
using Empire_Earth_Launcher.Core.Installations;
using Empire_Earth_Launcher.Core.Platform;

namespace Empire_Earth_Launcher.Core.Graphics
{
    /// <summary>One <c>key = value</c> line of <c>dgVoodoo.conf</c> with the section it is in.</summary>
    public sealed class DgVoodooConfEntry
    {
        internal DgVoodooConfEntry(string section, string key, string value)
        {
            Section = section;
            Key = key;
            Value = value;
        }

        /// <summary>The section without brackets (<c>General</c>); empty for a line before the first section (<c>Version</c>).</summary>
        public string Section { get; }

        public string Key { get; }

        /// <summary>The text after the <c>=</c>, without surrounding white space; empty if there is none.</summary>
        public string Value { get; }

        public override string ToString()
        {
            return "[" + Section + "] " + Key + " = " + Value;
        }
    }

    /// <summary>
    /// The text of a <c>dgVoodoo.conf</c> as lines of keys and values (INI style: <c>[Section]</c>, <c>key = value</c>, lines
    /// that start with <c>;</c> are comments). Reading only: the launcher of 1.1.0 never changes the file (ADR 0014).
    /// </summary>
    /// <remarks>
    /// Tolerant like the wrapper itself: CRLF, LF or CR line ends, a last line without a line end, tabs and spaces around
    /// the key and the <c>=</c>, a byte order mark, keys and sections in any case, a missing section or key. A line that is
    /// neither a section, a comment nor <c>key = value</c> is skipped. If a key appears twice in a section, the last one
    /// counts for <see cref="Find"/>, as for an INI reader that overwrites.
    /// </remarks>
    public sealed class DgVoodooConf
    {
        /// <summary>The keys the graphics page shows next to <c>OutputAPI</c>: what decides how the game window and the screen mode behave.</summary>
        public static readonly IReadOnlyList<string> ScreenModeKeys = new ReadOnlyCollection<string>(new[]
        {
            "FullScreenMode",
            "AppControlledScreenMode",
            "DisableAltEnterToToggleScreenMode",
            "DeferredScreenModeSwitch",
            "WindowedAttributes",
            "Resolution",
            "ScalingMode",
            "CaptureMouse",
        });

        /// <summary>The key of the graphics API (<c>d3d11_fl10_1</c>, <c>d3d12_fl12_0</c>).</summary>
        public const string OutputApiKey = "OutputAPI";

        private DgVoodooConf(IList<DgVoodooConfEntry> entries)
        {
            Entries = new ReadOnlyCollection<DgVoodooConfEntry>(entries);
        }

        /// <summary>The <c>key = value</c> lines in the order of the file.</summary>
        public IReadOnlyList<DgVoodooConfEntry> Entries { get; }

        /// <summary>Reads the text of a conf; null counts as empty.</summary>
        public static DgVoodooConf Parse(string text)
        {
            var entries = new List<DgVoodooConfEntry>();
            string section = string.Empty;
            string[] lines = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            foreach (string raw in lines)
            {
                string line = raw.Trim().TrimStart('﻿').Trim();
                if (line.Length == 0 || line[0] == ';')
                    continue;
                if (line[0] == '[')
                {
                    int close = line.IndexOf(']');
                    if (close > 0)
                        section = line.Substring(1, close - 1).Trim();
                    continue;
                }
                int equals = line.IndexOf('=');
                if (equals <= 0)
                    continue;
                string key = line.Substring(0, equals).Trim();
                if (key.Length == 0)
                    continue;
                entries.Add(new DgVoodooConfEntry(section, key, line.Substring(equals + 1).Trim()));
            }
            return new DgVoodooConf(entries);
        }

        /// <summary>
        /// The line of <paramref name="key"/> (ignoring case), in <paramref name="section"/> if one is given, else in any
        /// section (the first section that has it); null if the conf has none.
        /// </summary>
        public DgVoodooConfEntry Find(string key, string section = null)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            return Entries.LastOrDefault(entry =>
                string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase) &&
                (section == null || string.Equals(entry.Section, section, StringComparison.OrdinalIgnoreCase)));
        }
    }

    /// <summary>The <c>dgVoodoo.conf</c> of one game folder as the game reads it (<see cref="DgVoodooConfReader"/>).</summary>
    public sealed class DgVoodooConfFile
    {
        internal DgVoodooConfFile(string path, bool isVirtualStoreCopy, ConfigFileStatus status, string problem, DgVoodooConf conf)
        {
            Path = path;
            IsVirtualStoreCopy = isVirtualStoreCopy;
            Status = status;
            Problem = problem;
            Conf = conf;
        }

        /// <summary>The file that was read (or would have been).</summary>
        public string Path { get; }

        /// <summary>True if the copy below <c>%LOCALAPPDATA%\VirtualStore</c> was read, which the game uses (ADR 0016).</summary>
        public bool IsVirtualStoreCopy { get; }

        public ConfigFileStatus Status { get; }

        /// <summary>Why the file could not be read, for the log; null otherwise.</summary>
        public string Problem { get; }

        /// <summary>The lines of the file; null unless <see cref="Status"/> is <see cref="ConfigFileStatus.Read"/>.</summary>
        public DgVoodooConf Conf { get; }
    }

    /// <summary>
    /// Reads <c>dgVoodoo.conf</c> of a game folder where the game reads it (the VirtualStore copy first, ADR 0016). Only reads.
    /// </summary>
    public static class DgVoodooConfReader
    {
        /// <summary>The name of the file in a game folder.</summary>
        public const string FileName = "dgVoodoo.conf";

        public static DgVoodooConfFile Read(IFileSystem fileSystem, EffectivePathResolver effectivePaths, string gameFolder)
        {
            if (fileSystem == null)
                throw new ArgumentNullException(nameof(fileSystem));
            if (effectivePaths == null)
                throw new ArgumentNullException(nameof(effectivePaths));
            if (gameFolder == null)
                throw new ArgumentNullException(nameof(gameFolder));
            KeyValueFile file = KeyValueFile.Read(fileSystem, effectivePaths, gameFolder, FileName);
            return new DgVoodooConfFile(file.Path, file.IsVirtualStoreCopy, file.Status, file.Problem,
                file.Status == ConfigFileStatus.Read ? DgVoodooConf.Parse(file.Text) : null);
        }
    }
}
